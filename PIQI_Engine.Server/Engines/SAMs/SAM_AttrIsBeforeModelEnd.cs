using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks if an attribute's date value is before the model end date.
    /// </summary>
    public class SAM_AttrIsBeforeModelEnd : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrIsBeforeModelEnd"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrIsBeforeModelEnd(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the message attribute's date value is before the model end date.  
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the evaluation object to evaluate. 
        /// The <c>EvaluationObject</c> property must be an <see cref="EvaluationItem"/> with
        /// a date value in its <c>MessageData</c> property.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result. 
        /// The <see cref="PIQISAMResponse"/> indicates whether the attribute's date value is before the model end date,
        /// or contains a skip message if validation cannot be performed, or an error message if evaluation fails.
        /// </returns>
        /// <remarks>
        /// The evaluation passes if the attribute's date value is less than the <see cref="EvaluationItem.ModelEndDate"/>.
        /// The evaluation is skipped if the model end date is undefined, the item is not an attribute, 
        /// the item has no data, or the data cannot be recognized as a date.
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>.
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
                if (evaluation.ModelEndDate == null) return result.Skip("Model End is undefined");
                if (evaluation.ItemType != EntityItemTypeEnum.Attribute) return result.Skip("Bound item is not an attribute");
                if (!evaluation.HasMessageItem || evaluation.MessageItem?.MessageData == null) return result.Skip("Bound item has no data");
                DateTime? attrDate = evaluation.MessageItem?.MessageData?.DateTimeValue();
                if (attrDate == null) return result.Skip("Bound item is not recognized as a date");

                // Validate date against modelEndDate 
                passed = attrDate < evaluation.ModelEndDate;

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
        public static string StaticMnemonic => "ATTR_IS_BEFORE_MODEL_END";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
