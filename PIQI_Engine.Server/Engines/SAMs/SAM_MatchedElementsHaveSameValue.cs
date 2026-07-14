using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that evaluates whether matched elements across two value sets
    /// contain identical values when paired by effective date.
    /// </summary>
    /// <remarks>
    /// This SAM:
    /// <list type="bullet">
    /// <item><description>Partitions evaluation items into two buckets based on value set membership</description></item>
    /// <item><description>Validates that all items have valid CodeableConcept codings and FHIR resolution</description></item>
    /// <item><description>Pairs elements across buckets using EffectiveDate</description></item>
    /// <item><description>Fails if any paired items share the same numeric value</description></item>
    /// </list>
    /// </remarks>
    public class SAM_MatchedElementsHaveSameValue : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_MatchedElementsHaveSameValue"/> class.
        /// </summary>
        /// <param name="sam">The SAM configuration object associated with this evaluator.</param>
        /// <param name="samService">
        /// Service used to retrieve value sets, perform FHIR lookups, and resolve reference data.
        /// </param>
        public SAM_MatchedElementsHaveSameValue(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether paired elements from two value sets have differing values.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the parent <see cref="EvaluationItem"/>.
        /// The evaluation object must contain child elements representing both value set groups.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the evaluation result.
        /// Returns:
        /// <list type="bullet">
        /// <item><description>Success if all paired elements differ in value</description></item>
        /// <item><description>Failure if any paired elements have identical values</description></item>
        /// <item><description>Skipped if required parameters, roles, or data are missing or invalid</description></item>
        /// <item><description>Error if an unexpected exception occurs during evaluation</description></item>
        /// </list>
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown when an element cannot be cast to the expected <see cref="CodeableConcept"/>
        /// or when unexpected structural data issues occur.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case a class item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Get our parms
                string setMnemonic1 = request.GetParameterValue("ELEMENT_1_VALUE_SET");
                if (string.IsNullOrEmpty(setMnemonic1)) return result.Skip("Parm [Element 1 Value Set] was missing or invalid");
                string setMnemonic2 = request.GetParameterValue("ELEMENT_2_VALUE_SET");
                if (string.IsNullOrEmpty(setMnemonic2)) return result.Skip("Parm [Element 2 Value Set] was missing or invalid");

                // Get Value Sets
                ValueSet valueSet1 = await _SAMService.GetValueSetAsync(setMnemonic1);
                ValueSet valueSet2 = await _SAMService.GetValueSetAsync(setMnemonic2);

                // Create our buckets - each one is a dictionary of elements
                Dictionary<string, EvaluationItem> bucket1 = new Dictionary<string, EvaluationItem>();
                Dictionary<string, EvaluationItem> bucket2 = new Dictionary<string, EvaluationItem>();

                // Populate buckets
                foreach (EvaluationItem elementEval in evaluationItem.ChildDict.Values)
                {
                    MessageModelItem item = elementEval?.MessageItem;

                    // Verify the data class contains a defined primary concept and primary value
                    var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                    if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");

                    // Verify that the element has valid data for the primary concept roles 
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                    BaseText? data = (BaseText)primaryConcept?.MessageData;
                    if (data == null || string.IsNullOrEmpty(data.Text)) 
                        continue;

                    // Validate the data  
                    if (data is not CodeableConcept codeableConcept)
                        throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                    // Verify at least one complete coding exists
                    if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) 
                        continue;

                    // Call FHIR server if not called already
                    if (!codeableConcept.FHIRServerCalled)
                        await _SAMService.LookupCodeAsync(codeableConcept);

                    // Check if any codings are valid
                    if (!codeableConcept.CodingList.Any(t => t.IsValid)) 
                        continue;

                    // Determine bucket membership					
                    if (codeableConcept?.CodingList != null &&
                        valueSet1.CodingList.Any(c => codeableConcept.CodingList.Any(cd => 
                        cd.IsValid &&
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem : 
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == _SAMService.Message?.RefData.GetCodeSystem(c.CodeSystem)))))
                    {
                        // We check date here simply for the performance benefit
                        if (elementEval?.HasEffectiveDate == null) elementEval?.GetEffectiveDate();
                        if (elementEval?.HasEffectiveDate != true) return result.Skip("Some items in element set [1] lack an effective date");
                        if (elementEval.HasValueFloat == null) elementEval.GetPrimaryValueAsFloat();
                        if (elementEval.HasValueFloat != true) return result.Skip("Missing or invalid value");
                        bucket1.Add(elementEval.Key, elementEval);
                    }
                    if (codeableConcept?.CodingList != null &&
                        valueSet2.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                        cd.IsValid &&
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null? cs == c.CodeSystem : 
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == _SAMService.Message?.RefData.GetCodeSystem(c.CodeSystem)))))
                    {
                        // We check date here simply for the performance benefit
                        if (elementEval?.HasEffectiveDate == null) elementEval?.GetEffectiveDate();
                        if (elementEval?.HasEffectiveDate != true) return result.Skip("Some items in element set [2] lack an effective date");
                        if (elementEval.HasValueFloat == null) elementEval.GetPrimaryValueAsFloat();
                        if (elementEval.HasValueFloat != true) return result.Skip("Missing or invalid value");
                        bucket2.Add(elementEval.Key, elementEval);
                    }
                }

                // Skip conditions
                if (bucket1.Count < 1) return result.Skip("Element set [1] has no items");
                if (bucket2.Count < 1) return result.Skip("Element set [2] has no items");

                // At this point we have data in both buckets and all data is valid. 
                if (bucket1.Count != bucket2.Count) return result.Skip("Element set [1] and element set [2] do not match up");

                // Pair up the elements
                List<Tuple<EvaluationItem, EvaluationItem>> pairList = new List<Tuple<EvaluationItem, EvaluationItem>>();
                foreach (EvaluationItem e1 in bucket1.Values)
                {
                    // Find our pairing
                    EvaluationItem e2 = bucket2.Values.Where(t => t.EffectiveDate == e1.EffectiveDate).FirstOrDefault();
                    if (e2 == null) return result.Skip("Element set [1] and element set [2] do not match up");

                    // See if they have the same value
                    if (e1.ValueFloat == e2.ValueFloat) return result.Fail("Paired items exist with the same value");
                }

                // If we get to here, we passed
                result.Succeed();
            } 
            catch (Exception ex)
            {
                result.Error(ex.Message);
            }

            return result;
        } 

        /// <summary>
        /// Gets the mnemonic code for this SAM implementation.
        /// </summary>
        public static string StaticMnemonic => "MATCHED_ELEMENTS_HAVE_SAME_VALUE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
