using Azure;
using Azure.Core;
using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;
using System.Xml.Linq;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether an element has a valid primary concept value.
    /// </summary>
    public class SAM_ElementHasValue : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementHasValue"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementHasValue(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element contains a valid primary concept value.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with defined primary concept and primary value roles.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the primary concept contains at least one valid coding,
        /// a failed result if the primary concept is missing, invalid, or contains no valid codings,
        /// a skipped result if required roles are not defined,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a defined primary concept role</description></item>
        /// <item><description>Contain a defined primary value role</description></item>
        /// <item><description>Have primary concept data present and non-empty</description></item>
        /// </list>
        /// <para>
        /// The evaluation passes when at least one coding within the primary concept is both complete and valid.
        /// If necessary, a FHIR lookup is performed to validate codings.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Primary concept role is missing</description></item>
        /// <item><description>Primary value role is missing</description></item>
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
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem item = evaluationItem?.MessageItem;

                // Verify the data class contains a defined primary concept and primary value
                var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                var primaryValueRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_VALUE);
                if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");
                if (primaryValueRole == null) return result.Skip("Missing primary value role in data class.");

                // Verify that the element has valid data for the primary concept roles 
                MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                BaseText? data = (BaseText)primaryConcept?.MessageData;
                if (data == null || string.IsNullOrEmpty(data.Text)) return result.Skip("Primary concept data is missing or empty.");  

                // Validate the data format
                if (data is not CodeableConcept codeableConcept)
                    throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                // Verify at least one complete coding exists
                if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Primary concept data has no complete codings.");

                // Call FHIR server if not called already
                if (!codeableConcept.FHIRServerCalled)
                    await _SAMService.LookupCodeAsync(codeableConcept);

                // Check if any codings are valid
                if (!codeableConcept.CodingList.Any(t => t.IsValid)) return result.Skip("Primary concept data has no valid codings.");

                // Get our value set paramater 
                string setMnemonic = request.GetParameterValue("VALUE_SET_MNEMONIC");

                // If vs is defined, ensure this PC is in the value set
                if (!string.IsNullOrEmpty(setMnemonic))
                {
                    // Get all valid code/code systems from the value set via the value set mnemonic parameter
                    ValueSet valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                    //Check if there are any codings in the data that are in the codingList from the value set
                    if (codeableConcept?.CodingList == null ||
                        !(valueSet.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                        cd.IsValid &&
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem :
                        _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))))
                    )
                        return result.Skip("PrimaryConcept is not in valueset [" + setMnemonic + "]");
                }

                // Failure if we get to here and don't have a value
                if (evaluationItem?.HasValueText == null) evaluationItem?.GetPrimaryValue();
                if (evaluationItem?.HasValueText != true) return result.Fail("PrimaryConcept does not have a populated value");

                // Update result
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
        public static string StaticMnemonic => "ELEMENT_HAS_VALUE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
