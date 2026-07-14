using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks whether an element's start and end date range is plausible.
    /// </summary>
    public class SAM_ElementDateRangeIsPlausible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementDateRangeIsPlausible"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementDateRangeIsPlausible(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element's start and end dates form a valid and plausible range.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with start and end date roles.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the date range is valid,
        /// a failed result if any validation rule is violated,
        /// a skipped result if required roles are not defined,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation enforces the following rules:</para>
        /// <list type="bullet">
        /// <item><description>Start date must be populated</description></item>
        /// <item><description>Start date must not be in the future</description></item>
        /// <item><description>Start date must not occur before the model start date (if defined)</description></item>
        /// <item><description>End date must be populated</description></item>
        /// <item><description>End date must not occur before the start date</description></item>
        /// <item><description>End date must not occur after the model end date (if defined)</description></item>
        /// <item><description>End date must not be more than one year in the future</description></item>
        /// </list>
        /// <para>The evaluation is skipped if required date roles are not defined on the element.</para>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

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

                // Useful vars
                DateTime now = DateTime.Now;
                DateTime? modelStart = evaluationItem.ModelStartDate;
                DateTime? modelEnd = evaluationItem.ModelEndDate;

                // Fail conditions
                if (startDate == null) return result.Fail("Start date is not populated");
                if (startDate > now) return result.Fail("Start date is in the future");
                if (modelStart != null && startDate < modelStart) return result.Fail("Start date occurs before Model Start");

                if (endDate == null) return result.Fail("End date is not populated");
                if (endDate < startDate) return result.Fail("End date occurs before start date");
                if (modelEnd != null && endDate > modelEnd) return result.Fail("End date occurs after Model End");
                TimeSpan ts = endDate.Value - now; 
                if (ts.TotalHours > 8760) return result.Fail("End date occurs more than 1 year into the future");

                // Update result
                passed = true;
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
        public static string StaticMnemonic => "ELEMENT_DATE_RANGE_IS_PLAUSIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
