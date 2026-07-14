using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether elements from two value sets
    /// have matching counterparts based on their effective dates.
    /// </summary>
    public class SAM_ElementHasElementMatch : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementHasElementMatch"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementHasElementMatch(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether elements belonging to two specified value sets have matching elements
        /// based on their effective dates.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent a class containing child elements with primary concepts and effective dates.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if each element in the first value set has a corresponding element
        /// in the second value set with the same effective date,
        /// a failed result if the sets do not match,
        /// a skipped result if required parameters or data are missing,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a valid primary concept</description></item>
        /// <item><description>Have at least one valid and complete coding</description></item>
        /// <item><description>Belong to either of the specified value sets</description></item>
        /// <item><description>Have a valid effective date</description></item>
        /// </list>
        /// <para>
        /// Elements are separated into two groups based on value set membership.
        /// The evaluation passes when both groups contain the same number of elements
        /// and each element in the first group has a corresponding element in the second group
        /// with an identical effective date.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Required parameters are missing or invalid</description></item>
        /// <item><description>Either element set contains no qualifying items</description></item>
        /// <item><description>Qualifying elements are missing effective dates</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>
        /// or when an unexpected data type is encountered during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case a class item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Get our dataclass
                Entity dataClass = evaluationItem.ClassEntity;

                // Get our parms
                string setMnemonic1 = request.GetParameterValue("VALUESET_FIRST_ELEMENT");
                if (string.IsNullOrEmpty(setMnemonic1)) return result.Skip("Parm [Element 1 Value Set] was missing or invalid");
                string setMnemonic2 = request.GetParameterValue("VALUESET_SECOND_ELEMENT");
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
                    if (primaryConceptRole == null) 
                        continue;

                    // Verify that the element has valid data for the primary concept roles 
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                    BaseText? data = primaryConcept?.MessageData as BaseText;
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
                        bucket1.Add(elementEval.Key, elementEval);
                    }
                    if (codeableConcept?.CodingList != null &&
                        valueSet2.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                        cd.IsValid &&
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem :
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == _SAMService.Message?.RefData.GetCodeSystem(c.CodeSystem)))))
                    {
                        if (elementEval?.HasEffectiveDate == null) elementEval?.GetEffectiveDate();
                        if (elementEval?.HasEffectiveDate != true) return result.Skip("Some items in element set [2] lack an effective date");
                        bucket2.Add(elementEval.Key, elementEval);
                    }
                }

                // Skip conditions
                if (bucket1.Count < 1) return result.Skip("Element set [1] has no items");
                if (bucket2.Count < 1) return result.Skip("Element set [2] has no items");

                // At this point we have data in both buckets and all data is valid. There are no more skip conditions. It's pass/fail.
                if (bucket1.Count != bucket2.Count) return result.Fail("Element set [1] and element set [2] do not match up");

                // Pair up the elements
                foreach (EvaluationItem  e1 in bucket1.Values)
                {
                    EvaluationItem? e2 = bucket2.Values.Where(t => t.EffectiveDate == e1.EffectiveDate).FirstOrDefault();
                    if (e2 == null) return result.Fail("Element set [1] and element set [2] do not match up");
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
        public static string StaticMnemonic => "ELEMENT_HAS_ELEMENT_MATCH";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
