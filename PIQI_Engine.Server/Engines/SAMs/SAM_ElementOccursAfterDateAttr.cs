using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that verifies whether an element's effective date
    /// occurs after a specified date attribute.
    /// </summary>
    public class SAM_ElementOccursAfterDateAttr : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementOccursAfterDateAttr"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementOccursAfterDateAttr(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the element's effective date occurs after the specified date attribute.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with an effective date and a date attribute.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the element's effective date is on or before the attribute date,
        /// a failed result if the attribute date is after the element's effective date,
        /// a skipped result if required data or parameters are missing or invalid,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Are of type <see cref="EntityItemTypeEnum.Element"/></description></item>
        /// <item><description>Have a valid effective date</description></item>
        /// <item><description>Contain a valid date attribute specified by the <c>ATTRIBUTE_MNEMONIC</c> parameter</description></item>
        /// </list>
        /// <para>
        /// The evaluation fails when the attribute date is greater than the element's effective date.
        /// Otherwise, the evaluation passes.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>The evaluation item is not an element</description></item>
        /// <item><description>The effective date is not available</description></item>
        /// <item><description>The attribute mnemonic parameter is missing or invalid</description></item>
        /// <item><description>The attribute is not present or has no data</description></item>
        /// <item><description>The attribute data is not a valid date</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>
        /// or when an unexpected error occurs during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case a class item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Skip conditions
                if (evaluationItem.ItemType != EntityItemTypeEnum.Element) return result.Skip($"Sam [{SAMObject.Name}] must be bound to an element");

                // Get our effective date
                if (evaluationItem?.HasEffectiveDate == null) evaluationItem?.GetEffectiveDate();
                if (evaluationItem?.HasEffectiveDate != true) return result.Skip("Effective Date not available");

                // Get our date attr mnemonic 
                string attrMnemonic = request.GetParameterValue("ATTRIBUTE_MNEMONIC");
                if (string.IsNullOrEmpty(attrMnemonic)) return result.Skip("Parm [Attribute Mnemonic] not populated");

                // Get our date attr
                Entity entity = _SAMService.Message.RefData.GetEntity(attrMnemonic);
                if (entity == null) return result.Skip($"Failed to identify entity [{attrMnemonic}]");
                EvaluationItem attrEval = evaluationItem.GetChildItem(entity.Mnemonic);
                if (attrEval == null || !attrEval.HasMessageItem || attrEval.MessageItem.MessageData == null) return result.Skip("Date attribute was not populated");
                if (!attrEval.MessageItem.MessageData.IsDateTime()) return result.Skip("Date attribute was not a valid date");
                DateTime dt = attrEval.MessageItem.MessageData.DateTimeValue().Value;

                // Compare
                if (dt > evaluationItem.EffectiveDate) return result.Fail();

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
        public static string StaticMnemonic => "ELEMENT_OCCURS_AFTER_ATTR";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
