using Azure;
using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether elements with the same primary concept
    /// have consistent units of measure.
    /// </summary>
    public class SAM_ElementsHaveConsistentUnits : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementsHaveConsistentUnits"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementsHaveConsistentUnits(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether elements sharing the same primary concept use consistent units of measure.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent a class containing child elements with primary concepts and units of measure.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if all elements with the same primary concept have consistent units of measure,
        /// a failed result if inconsistent units are detected,
        /// a skipped result if required roles or data are missing or invalid,
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
        /// only elements whose primary concept is a member of that value set are considered.
        /// </para>
        /// <para>
        /// Elements are grouped by their recognized code system and code value.
        /// For each group, all associated units of measure must be identical.
        /// The evaluation fails if any group contains more than one distinct unit.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Primary concept role is missing</description></item>
        /// <item><description>Primary unit of measure role is missing</description></item>
        /// <item><description>An element is not a member of the specified value set (if provided)</description></item>
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
                EvaluationItem classEval = (EvaluationItem)request.EvaluationObject;

                // Get our valueset parms
                string setMnemonic = request.GetParameterValue("PRIMARY_CONCEPT_VALUESET");
                ValueSet? valueSet = null;
                // If a valueset was specified, grab values
                if (setMnemonic != null)
                    valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                // Create our dictionary
                Dictionary<string, List<string>> codingDict = new Dictionary<string, List<string>>();

                // Populate our dictionary
                foreach (EvaluationItem elementEval in classEval.ChildDict.Values)
                {
                    MessageModelItem item = elementEval?.MessageItem;

                    // Verify the data class contains a defined primary concept and primary value
                    var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                    if (primaryConceptRole == null) return result.Skip("Missing primary concept role in data class.");

                    // We require that the UOM be defined
                    var primaryUOMRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_UOM);
                    if (primaryUOMRole == null) return result.Skip("PrimaryUOM role not defined");

                    // Get components
                    // Verify that the element has valid data for the primary concept roles 
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                    BaseText? data = (BaseText)primaryConcept?.MessageData;
                    if (data == null || string.IsNullOrEmpty(data.Text)) return result.Skip("Element has no primary concept");

                    // Validate the data format
                    if (data is not CodeableConcept primaryCodeableConcept)
                        throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                    // Verify at least one complete coding exists
                    if (!primaryCodeableConcept.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Element has no primary concept"); ;

                    // Call FHIR server if not called already
                    if (!primaryCodeableConcept.FHIRServerCalled)
                        await _SAMService.LookupCodeAsync(primaryCodeableConcept);

                    // Check if any codings are valid
                    if (!primaryCodeableConcept.CodingList.Any(t => t.IsValid)) return result.Skip("Element has no primary concept"); ;

                    // If a valueset was specified, ensure we are a member
                    if (valueSet != null)
                    {
                        //Check if there are any codings in the data that are in the codingList from the value set
                        if (valueSet == null || primaryCodeableConcept?.CodingList == null ||
                            !valueSet.CodingList.Any(c => primaryCodeableConcept.CodingList.Any(cd =>
                            cd.IsValid &&
                            cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                            cd.CodeSystemList.Any(cs =>
                            _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem :
                            _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))))
                            return result.Skip("Element is not a member of the specified value set");
                    }

                    // Get the UOM text
                    if (elementEval?.HasUOMText == null) elementEval?.GetPrimaryUOM();
                    string? uomText = elementEval?.UOMText;
                    if (uomText == null) return result.Skip("Element has no UOM");

                    // Store all the valid codings
                    foreach (Coding coding in primaryCodeableConcept.CodingList.Where(t => t.IsValid))
                    {
                        var key = $"{coding.RecognizedCodeSystem}|{coding.CodeValue}";
                        if (!codingDict.ContainsKey(key))
                            codingDict.Add(key, new List<string>());
                        codingDict[key].Add(uomText);
                    }
                }

                // Check each bucket to ensure that all the UOMs are the same
                foreach (KeyValuePair<string, List<string>> kvp in codingDict)
                {
                    if (kvp.Value.Select(t => t).Distinct().Count() > 1)
                        return result.Fail();
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
        public static string StaticMnemonic => "ELEMENTS_HAVE_CONSISTENT_UNITS";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
