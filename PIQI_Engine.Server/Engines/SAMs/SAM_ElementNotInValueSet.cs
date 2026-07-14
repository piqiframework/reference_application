using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether an element's primary concept
    /// is not a member of a specified value set.
    /// </summary>
    public class SAM_ElementNotInValueSet : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementNotInValueSet"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementNotInValueSet(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element's primary concept does not exist within the specified value set.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with a primary concept value.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the primary concept is not found in the specified value set,
        /// a failed result if it is found or invalid,
        /// a skipped result if required parameters or roles are missing,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a defined primary concept role</description></item>
        /// <item><description>Have primary concept data present and non-empty</description></item>
        /// <item><description>Contain at least one complete coding</description></item>
        /// </list>
        /// <para>
        /// A FHIR lookup is performed if necessary to validate codings.
        /// The evaluation passes when none of the valid codings in the primary concept
        /// match any coding in the specified value set.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Primary concept role is missing</description></item>
        /// <item><description>Value set mnemonic parameter is missing or empty</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>
        /// or when an unexpected data type is encountered during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            { 
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem item = evaluationItem?.MessageItem;

                // Verify the data class contains a defined primary concept and primary value
                var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");

                // Get our valueset parameter 
                string setMnemonic = request.GetParameterValue("VALUE_SET_MNEMONIC");
                if (string.IsNullOrWhiteSpace(setMnemonic)) return result.Skip("Parameter [Value Set Mnemonic] was not supplied");

                // Verify that the element has valid data for the primary concept roles 
                MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                BaseText? data = primaryConcept?.MessageData as BaseText;
                if (data == null || string.IsNullOrEmpty(data.Text)) return result.Skip("Primary concept data is missing or empty.");  

                // Validate the data format
                if (data is not CodeableConcept codeableConcept)
                    throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                // Verify at least one complete coding exists
                if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Primary concept does not contain any complete codings.");

                // Call FHIR server if not called already
                if (!codeableConcept.FHIRServerCalled)
                    await _SAMService.LookupCodeAsync(codeableConcept);
                 
                // Check if any codings are valid
                if (!codeableConcept.CodingList.Any(t => t.IsValid)) return result.Skip("Primary concept does not contain any valid codings.");

                // Check if the primary concept is a member of the specified value set
                // Get all valid code/code systems from the value set via the value set mnemonic parameter
                ValueSet valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                //Check if there are any codings in the data that are in the codingList from the value set
                passed = (codeableConcept?.CodingList == null ||
                    !valueSet.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                    cd.IsValid &&
                    cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                    cd.CodeSystemList.Any(cs =>
                    _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem : 
                    _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))));

                // Update result
                result.Done(passed);
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
        public static string StaticMnemonic => "ELEMENT_PRIM_CONCEPT_NOT_IN_VALUESET";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
