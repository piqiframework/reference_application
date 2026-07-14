using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether an element's primary concept value
    /// is plausible based on codings, value sets, and supported range constraints.
    /// </summary>
    public class SAM_ElementValueIsPlausible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementValueIsPlausible"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// Service used to access reference data, perform FHIR lookups,
        /// retrieve value sets, and validate range constraints.
        /// </param>
        public SAM_ElementValueIsPlausible(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element's primary concept value is plausible.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must include a primary concept, unit of measure, and numeric value.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the value is plausible based on validated codings and range constraints,
        /// a failed result if the value is invalid or out of range,
        /// a skipped result if required roles, parameters, or data are missing or invalid,
        /// or an error result if an unexpected exception occurs.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a defined primary concept, primary UOM, and primary value role</description></item>
        /// <item><description>Have valid and complete codings for the primary concept</description></item>
        /// <item><description>Have numeric values that can be evaluated against range constraints</description></item>
        /// </list>
        /// <para>
        /// If a value set is provided via the <c>PRIMARY_CONCEPT_VALUESET</c> parameter,
        /// only elements whose primary concept is part of that value set will be evaluated.
        /// </para>
        /// <para>
        /// The evaluation validates:
        /// </para>
        /// <list type="bullet">
        /// <item><description>At least one complete and valid coding exists for the primary concept</description></item>
        /// <item><description>The concept has a corresponding range definition in the RangeSetMart</description></item>
        /// <item><description>The element value falls within the defined min/max range (if applicable)</description></item>
        /// </list>
        /// <para>
        /// The evaluation is skipped when:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Required roles (concept, UOM, value) are missing</description></item>
        /// <item><description>Primary concept or value data is missing or invalid</description></item>
        /// <item><description>Value or UOM cannot be resolved or parsed</description></item>
        /// <item><description>No applicable concept/UOM mappings exist in the data mart</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to
        /// <see cref="EvaluationItem"/> or when an unexpected error occurs during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem? item = evaluationItem?.MessageItem;

                // Verify the data class contains a defined primary concept, primary uom, and primary value
                var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                var primaryUOMRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_UOM);
                var primaryValueRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_VALUE);
                if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");
                if (primaryUOMRole == null) return result.Skip("PrimaryUOM role not defined");
                if (primaryValueRole == null) return result.Skip("Missing primary value role in data class.");

                //Verify populated data
                if (evaluationItem?.HasUOMText == null) evaluationItem?.GetPrimaryUOM();
                if (evaluationItem?.HasValueText == null) evaluationItem?.GetPrimaryValue(); 
                if (evaluationItem?.HasValueFloat == null) evaluationItem?.GetPrimaryValueAsFloat();
                if (evaluationItem?.UOMText == null) return result.Skip("PrimaryUnit is not populated");
                if (evaluationItem?.ValueText == null) return result.Skip("PrimaryValue is not populated");
                if (evaluationItem?.ValueFloat == null) return result.Skip("PrimaryValue is not numeric");

                // Verify validity of the primary concept data
                MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                BaseText? primaryConceptData = primaryConcept?.MessageData as BaseText;
                if (primaryConceptData == null || string.IsNullOrEmpty(primaryConceptData.Text)) return result.Skip("Primary concept data is missing or empty.");

                // Validate the data format
                if (primaryConceptData is not CodeableConcept primaryCodeableConcept)
                    return result.Skip("ELEMENT_VALUE_IS_PLAUSIBLE expects a CodeableConcept value.");

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
                // Check if any valid codings match in the RangeSetMart database
                var validCodings = primaryCodeableConcept.CodingList.Where(c => c.IsValid);
                bool hasMatchInRangeSetMart = false;
                bool hasInvalidRangeInRangeSetMart = false;
                foreach (var coding in validCodings)
                {
                    var range = await _SAMService.CheckRangeSetMartAsync(coding.RecognizedCodeSystem, coding.CodeValue, evaluationItem.UOMText);
                    if (range != null)
                    {
                        hasMatchInRangeSetMart = true;
                        if (range.Value.MaxValue == null || evaluationItem.ValueFloat > range.Value.MaxValue
                            || range.Value.MinValue == null || evaluationItem.ValueFloat < range.Value.MinValue)
                        {
                            hasInvalidRangeInRangeSetMart = true;
                            break;
                        }
                    }
                }
                if (!hasMatchInRangeSetMart) return result.Skip("None of the PrimaryConcept/UOM combinations for this element were represented in the datamart");
                if (hasInvalidRangeInRangeSetMart) return result.Fail("The value was invalid for at least one legitimate PrimaryConcept/UOM combination");

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
        public static string StaticMnemonic => "ELEMENT_VALUE_IS_PLAUSIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
