using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks if an attribute's date value is after the model start date.
    /// </summary>
    public class SAM_AttrIsAfterModelStart : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrIsAfterModelStart"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrIsAfterModelStart(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the message attribute's date value is after the model start date.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an attribute with a date value.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the attribute date is after the model start date,
        /// a skipped result if validation prerequisites are not met,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation passes when the attribute's date value is greater than <see cref="EvaluationItem.ModelStartDate"/>.</para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>The model start date is undefined</description></item>
        /// <item><description>The evaluation item is not an attribute</description></item>
        /// <item><description>The attribute has no message data</description></item>
        /// <item><description>The message data cannot be parsed as a date</description></item>
        /// </list>
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
                // First parameter is always an eval item - in this case an attribute item 
                EvaluationItem evaluation = (EvaluationItem)request.EvaluationObject;

                // Skip conditions
                if (evaluation.ModelStartDate == null) return result.Skip("Model Start is undefined");
                if (evaluation.ItemType != EntityItemTypeEnum.Attribute) return result.Skip("Bound item is not an attribute");
                if (!evaluation.HasMessageItem || evaluation.MessageItem?.MessageData == null) return result.Skip("Bound item has no data");
                DateTime? attrDate = evaluation.MessageItem?.MessageData?.DateTimeValue();
                if (attrDate == null) return result.Skip("Bound item is not recognized as a date");

                // Validate date against modelStartDate
                passed = attrDate > evaluation.ModelStartDate;

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
        public static string StaticMnemonic => "ATTR_IS_AFTER_MODEL_START";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
