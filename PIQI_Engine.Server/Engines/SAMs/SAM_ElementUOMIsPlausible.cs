using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether an element's unit of measure (UOM)
    /// is plausible for its primary concept.
    /// </summary>
    public class SAM_ElementUOMIsPlausible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementUOMIsPlausible"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data,
        /// perform FHIR API calls, and validate unit mappings.
        /// </param>
        public SAM_ElementUOMIsPlausible(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element's unit of measure is valid for its primary concept.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with a primary concept and unit of measure.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the unit of measure is valid for at least one coding of the primary concept,
        /// a failed result if no valid mapping exists,
        /// a skipped result if required roles, parameters, or data are missing or invalid,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a defined primary concept role</description></item>
        /// <item><description>Contain a defined primary unit of measure (UOM) role</description></item>
        /// <item><description>Have valid and complete codings for the primary concept</description></item>
        /// </list>
        /// <para>
        /// If a value set is specified via the <c>PRIMARY_CONCEPT_VALUESET</c> parameter,
        /// only elements whose primary concept is a member of that value set are evaluated.
        /// </para>
        /// <para>
        /// The evaluation checks whether at least one valid coding for the primary concept
        /// has a corresponding entry in the PrimaryUnitMart for the given unit of measure.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Primary concept role is missing</description></item>
        /// <item><description>Primary unit of measure role is missing</description></item>
        /// <item><description>Primary concept is not in the specified value set (if provided)</description></item>
        /// <item><description>Unit of measure is missing, invalid, or not represented in the data mart</description></item>
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
                if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");

                // We require that the UOM be defined
                var primaryUOMRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_UOM);
                if (primaryUOMRole == null) return result.Skip("PrimaryUOM role not defined");

                // Verify that the element has valid data for the primary concept roles 
                MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                BaseText? data = (BaseText)primaryConcept?.MessageData;
                if (data == null || string.IsNullOrEmpty(data.Text)) return result.Skip("Primary concept data is missing or empty.");  

                // Validate the data format 
                if (data is not CodeableConcept primaryCodeableConcept)
                    throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                // Verify at least one complete coding exists
                if (!primaryCodeableConcept.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Primary concept does not contain any complete codings.");

                // Call FHIR server if not called already
                if (!primaryCodeableConcept.FHIRServerCalled)
                    await _SAMService.LookupCodeAsync(primaryCodeableConcept);
                 
                // Check if any codings are valid 
                if (!primaryCodeableConcept.CodingList.Any(t => t.IsValid)) return result.Skip("Primary concept does not contain any valid codings.");

                // Get our valueset parm 
                string setMnemonic = request.GetParameterValue("PRIMARY_CONCEPT_VALUESET");
                
                // If setMnemonic is defined, ensure this PC is in the value set 
                if (!string.IsNullOrEmpty(setMnemonic))
                {
                    // Get the value set from the SAM service
                    ValueSet valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                    // Check if there are any codings in the data that are in the codingList from the value set
                    if (primaryCodeableConcept?.CodingList == null ||
                    !valueSet.CodingList.Any(c => primaryCodeableConcept.CodingList.Any(cd =>
                    cd.IsValid &&
                    cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                    cd.CodeSystemList.Any(cs =>
                    _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem : 
                    _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))))
                        return result.Skip("PrimaryConcept is not in valueset [" + setMnemonic + "]");
                }

                // Analyze
                if (evaluationItem?.HasUOMText == null) evaluationItem?.GetPrimaryUOM();
                var uomText = string.IsNullOrEmpty(evaluationItem?.UOMText) ? "{NO UNIT}" : evaluationItem?.UOMText;
                if (uomText.Length > 50 || !primaryCodeableConcept.CodingList.Where(c => c.IsValid).Any()) return result.Skip("None of the PrimaryConcept codings for this element were represented in the datamart");

                // Check if any valid codings match in the PrimaryUnitMart database
                var validCodings = primaryCodeableConcept.CodingList.Where(c => c.IsValid);
                bool hasMatchInPrimaryUnitMart = false;
                foreach (var coding in validCodings)
                {
                    if (await _SAMService.CheckPrimaryUnitMartAsync(coding.RecognizedCodeSystem, coding.CodeValue, uomText))
                    {
                        hasMatchInPrimaryUnitMart = true;
                        break;
                    }
                }
                if (!hasMatchInPrimaryUnitMart) return result.Fail("No matching entry found in PrimaryUnitMart for the given codesystem, codevalue, and UOM");

                // If we get to here then
                // 1) at least one PC coding is represented in the UOM mart
                // 2) all codings represented in the UOM mart are valid for UOMText 
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
        public static string StaticMnemonic => "ELEMENT_UOM_IS_PLAUSIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
