using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks if a completed procedure has a valid date.
    /// Validates that the procedure's status matches a configured list of completed statuses
    /// and that an effective date/time is populated.
    /// </summary>
    public class SAM_CompletedProcedureHasDate : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_CompletedProcedureHasDate"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_CompletedProcedureHasDate(SAM sam, SAMService samService)
            : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether a procedure has a completed status and a valid effective date/time.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing:
        /// <list type="bullet">
        ///   <item>The <see cref="PIQISAMRequest.EvaluationObject"/>, expected to be an <see cref="EvaluationItem"/> representing a procedure.</item>
        ///   <item>Required parameter "STATUS_ATTRIBUTE": The mnemonic of the child attribute containing the status.</item>
        ///   <item>Required parameter "COMPLETED_STATUS_CSV": A comma-separated list of status values considered as completed.</item>
        /// </list>
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The response indicates:
        /// <list type="bullet">
        ///   <item><c>Succeeded</c> if the procedure has a completed status and a valid effective date/time.</item>
        ///   <item><c>Failed</c> if the procedure is completed but has no valid date/time.</item>
        ///   <item><c>Skipped</c> if parameters are invalid, status attribute is missing, or procedure is not completed.</item>
        ///   <item><c>Errored</c> if an exception occurs during evaluation.</item>
        /// </list>
        /// </returns>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case an element item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Get our parameters
                string statusAttrMnemonic = request.GetParameterValue("STATUS_ATTRIBUTE");
                if (string.IsNullOrEmpty(statusAttrMnemonic)) 
                    return result.Skip("Parm [Status Attribute] not valid");
                string compListCSV = request.GetParameterValue("COMPLETED_STATUS_CSV");
                if (string.IsNullOrEmpty(compListCSV)) 
                    return result.Skip("Parm [Completed Status CSV] not valid");

                // Get our status attr
                string key = $"{statusAttrMnemonic}|{evaluationItem.ElementSequence}";
                EvaluationItem statusAttr = evaluationItem.GetChildItem(key);
                if (statusAttr == null || !statusAttr.HasText) 
                    return result.Skip("Status attribute missing or unpopulated");
                string statusAttrText = statusAttr.MessageItem.MessageData.Text;

                // Get our completed list
                List<string> statusList = Utility.Split(compListCSV);

                // See if we're completed
                bool complete = false;
                foreach (string status in statusList)
                    if (statusAttrText.Equals(status, StringComparison.CurrentCultureIgnoreCase)) complete = true;
                if (!complete) 
                    return result.Skip("Procedure not complete");


                // Eval
                BaseText dateText = evaluationItem.GetSimpleByRole(RoleTypeEnum.EFFECTIVE_DATETIME);
                if (dateText == null || dateText.DateTimeValue() == null || dateText.DateTimeValue() == DateTime.MinValue)
                    return result.Fail();

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
        public static string StaticMnemonic => "COMPLETED_PROCEDURE_HAS_DATE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
