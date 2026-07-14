using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks whether an element's duration between start and end dates is within a plausible threshold.
    /// </summary>
    public class SAM_ElementDurationIsPlausible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementDurationIsPlausible"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementDurationIsPlausible(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the duration between an element's start and end dates is within an acceptable threshold.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with start and end date roles.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the duration is less than or equal to the configured threshold,
        /// a failed result if the duration exceeds the threshold,
        /// a skipped result if validation prerequisites are not met,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation enforces the following rules:</para>
        /// <list type="bullet">
        /// <item><description>Start and end dates must be present and valid</description></item>
        /// <item><description>The duration (in days) must not exceed the <c>THRESHOLD_IN_DAYS</c> parameter</description></item>
        /// </list>
        /// <para>If the optional <c>VALUE_SET</c> parameter is provided, the evaluation only applies when:</para>
        /// <list type="bullet">
        /// <item><description>A primary concept role is defined and populated</description></item>
        /// <item><description>The primary concept is a valid <see cref="CodeableConcept"/></description></item>
        /// <item><description>At least one coding is complete and valid</description></item>
        /// <item><description>The primary concept is a member of the specified value set</description></item>
        /// </list>
        /// <para>The evaluation is skipped if any prerequisite condition is not met.</para>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>,
        /// or when the primary concept is not of type <see cref="CodeableConcept"/>.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case an element item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem item = evaluationItem?.MessageItem;

                // We require that StartDate and EndDate roles are defined for this dataclass
                if (item?.ClassEntity?.Roles?.Any(r => r.RoleTypeMnemonic == RoleTypeEnum.START_DATETIME) == null) return result.Skip("StartDate role not defined");
                if (item?.ClassEntity?.Roles?.Any(r => r.RoleTypeMnemonic == RoleTypeEnum.END_DATETIME) == null) return result.Skip("EndDate role not defined");

                // Get our startdate and enddate
                DateTime? startDate = evaluationItem.GetStartDate();
                DateTime? endDate = evaluationItem.GetEndDate();

                // Skip conditions
                if (startDate == null || startDate.Value == DateTime.MinValue) return result.Skip("StartDate not populated");
                if (endDate == null || endDate.Value == DateTime.MinValue) return result.Skip("EndDate not populated");

                // Get our threshold parm
                int threshold = Utility.ObjInt(request.GetParameterValue("THRESHOLD_IN_DAYS"));
                if (threshold < 1) return result.Skip("Parameter [Threshold in days] not supplied or invalid");

                // If value set parm is defined
                string setMnemonic = request.GetParameterValue("VALUE_SET");
                if (!string.IsNullOrWhiteSpace(setMnemonic))
                {
                    // Primary concept must be defined and populated
                    Role? primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                    if (primaryConceptRole == null) return result.Skip("PrimaryConcept role not defined");

                    // Verify that the element has valid data for the primary concept roles 
                    string primaryConceptKey = $"{item.Key}|{primaryConceptRole.AttributeMnemonic}";
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptKey);
                    BaseText? data = (BaseText)primaryConcept?.MessageData;
                    if (data == null || string.IsNullOrEmpty(data.Text)) return result.Skip("Primary concept data is missing or empty.");

                    // Validate the data format
                    if (data is not CodeableConcept codeableConcept)
                        throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                    // Verify at least one complete coding exists
                    if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Primary concept data is has no complete codings.");

                    // Call FHIR server if not called already
                    if (!codeableConcept.FHIRServerCalled)
                        await _SAMService.LookupCodeAsync(codeableConcept);

                    // Check if any codings are valid
                    if (!codeableConcept.CodingList.Any(t => t.IsValid)) return result.Skip("Primary concept data is has no valid codings.");

                    // Check if the primary concept is a member of the specified value set
                    // Get all valid code/code systems from the value set via the value set mnemonic parameter
                    ValueSet valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                    //Check if there are any codings in the data that are in the codingList from the value set
                    if (codeableConcept?.CodingList == null ||
                        !valueSet.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                        cd.IsValid && 
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem : 
                        _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))))
                        return result.Skip("PrimaryConcept not a member of value set [" + setMnemonic + "]");
                }

                // Get our duration
                TimeSpan ts = endDate.Value.Subtract(startDate.Value);
                int duration = (int)ts.TotalDays;

                // Eval
                if (duration > threshold) return result.Fail();

                // If we get to here then we succeeded
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
        public static string StaticMnemonic => "ELEMENT_DURATION_IS_PLAUSIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
