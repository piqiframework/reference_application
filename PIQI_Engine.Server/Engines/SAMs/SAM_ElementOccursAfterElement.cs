using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether elements from one value set
    /// occur before elements from another value set based on their effective dates.
    /// </summary>
    public class SAM_ElementOccursAfterElement : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementOccursAfterElement"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementOccursAfterElement(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether all elements in the "before" set occur on or before
        /// all elements in the "after" set based on their effective dates.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent a class containing child elements with primary concepts and effective dates.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if all elements in the "before" set occur on or before
        /// all elements in the "after" set,
        /// a failed result if any element in the "before" set occurs after an element in the "after" set,
        /// a skipped result if required parameters or data are missing,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a valid primary concept</description></item>
        /// <item><description>Have at least one valid and complete coding</description></item>
        /// <item><description>Belong to either the "before" or "after" value sets</description></item>
        /// <item><description>Have a valid effective date</description></item>
        /// </list>
        /// <para>
        /// Elements are separated into two groups based on value set membership:
        /// one representing "before" elements and one representing "after" elements.
        /// The evaluation compares the latest effective date in the "before" set
        /// to the earliest effective date in the "after" set.
        /// </para>
        /// <para>
        /// The evaluation fails if the maximum effective date in the "before" set
        /// is greater than the minimum effective date in the "after" set.
        /// Otherwise, the evaluation passes.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Required value set parameters are missing or invalid</description></item>
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
                EvaluationItem eval = (EvaluationItem)request.EvaluationObject;

                // Get our valueset parms
                string setMnemonic1 = request.GetParameterValue("BEFORE_VALUESET");
                if (string.IsNullOrEmpty(setMnemonic1)) return result.Skip("Parm [Before ValueSet] was missing or invalid");
                string setMnemonic2 = request.GetParameterValue("AFTER_VALUESET");
                if (string.IsNullOrEmpty(setMnemonic2)) return result.Skip("Parm [After ValueSet] was missing or invalid");

                // Get our dataclass parms and objects
                string dc1Mnemonic = request.GetParameterValue("BEFORE_DATACLASS");
                string dc2Mnemonic = request.GetParameterValue("AFTER_DATACLASS");
                Entity dc1 = null; Entity dc2 = null;
                if (!string.IsNullOrEmpty(dc1Mnemonic)) dc1 = _SAMService.Message?.RefData.EntityModel.GetEntity(dc1Mnemonic);
                if (!string.IsNullOrEmpty(dc2Mnemonic)) dc2 = _SAMService.Message?.RefData.EntityModel.GetEntity(dc2Mnemonic);

                // Get Value Sets
                ValueSet valueSet1 = await _SAMService.GetValueSetAsync(setMnemonic1);
                ValueSet valueSet2 = await _SAMService.GetValueSetAsync(setMnemonic2);

                // Create our buckets - each one is a dictionary of elements
                Dictionary<string, EvaluationItem> bucket1 = new Dictionary<string, EvaluationItem>();
                Dictionary<string, EvaluationItem> bucket2 = new Dictionary<string, EvaluationItem>();

                // Populate buckets
                foreach (EvaluationItem elementEval in eval.ChildDict.Values)
                {
                    MessageModelItem item = elementEval?.MessageItem;

                    // Verify the data class contains a defined primary concept and primary value
                    var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                    if (primaryConceptRole == null) continue;

                    // Verify that the element has valid data for the primary concept roles 
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                    BaseText? data = (BaseText)primaryConcept?.MessageData;
                    if (data == null || string.IsNullOrEmpty(data.Text)) continue;

                    // Validate the data  
                    if (data is not CodeableConcept codeableConcept)
                        throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");


                    // Verify at least one complete coding exists
                    if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) continue;

                    // Call FHIR server if not called already
                    if (!codeableConcept.FHIRServerCalled)
                        await _SAMService.LookupCodeAsync(codeableConcept);

                    // Check if any codings are valid
                    if (!codeableConcept.CodingList.Any(t => t.IsValid)) continue;

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
                        // We check date here simply for the performance benefit
                        if (elementEval?.HasEffectiveDate == null) elementEval?.GetEffectiveDate();
                        if (elementEval?.HasEffectiveDate != true) return result.Skip("Some items in element set [2] lack an effective date");
                        bucket2.Add(elementEval.Key, elementEval);
                    }
                }

                // Skip conditions
                if (bucket1.Count < 1) return result.Skip("Element set [1] has no items");
                if (bucket2.Count < 1) return result.Skip("Element set [2] has no items");

                // Get our max and min dates
                DateTime maxBefore = bucket1.Max(t => t.Value.EffectiveDate.Value);
                DateTime minAfter = bucket2.Min(t => t.Value.EffectiveDate.Value);

                // compare our dates
                if (maxBefore > minAfter) return result.Fail("At least one element from [Before] occurs later than an element from [After]");

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
        public static string StaticMnemonic => "ELEMENT_OCCURS_AFTER_ELEMENT";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
