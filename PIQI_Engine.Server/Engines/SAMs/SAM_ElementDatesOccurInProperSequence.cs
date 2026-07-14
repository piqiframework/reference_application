using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks whether two element date attributes occur in the proper sequence.
    /// </summary>
    public class SAM_ElementDatesOccursInProperSequence : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementDatesOccursInProperSequence"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementDatesOccursInProperSequence(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the DATE1 attribute occurs before or at the same time as the DATE2 attribute.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent an element with child attributes corresponding to DATE1 and DATE2.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if DATE1 is less than or equal to DATE2,
        /// a failed result if DATE2 occurs before DATE1,
        /// a skipped result if validation prerequisites are not met,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation compares two date attributes defined by SAM parameters:</para>
        /// <list type="bullet">
        /// <item><description><c>DATE1</c> – The first (earlier) date attribute</description></item>
        /// <item><description><c>DATE2</c> – The second (later) date attribute</description></item>
        /// </list>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>The evaluation item is not an element</description></item>
        /// <item><description>Either DATE1 or DATE2 parameter is not provided</description></item>
        /// <item><description>Either parameter does not resolve to a valid entity in the model</description></item>
        /// <item><description>Either attribute is not found on the element</description></item>
        /// <item><description>Either attribute has no message data</description></item>
        /// <item><description>Either attribute value cannot be parsed as a date</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case an element item
                EvaluationItem elementEval = (EvaluationItem)request.EvaluationObject;

                // Skip conditions
                if (elementEval.ItemType != EntityItemTypeEnum.Element) return result.Skip($"Sam [{SAMObject.Name}] must be bound to an element");

                // Get Date1
                string date1Parm = request.GetParameterValue("DATE1");
                if (string.IsNullOrEmpty(date1Parm)) return result.Skip("Parm [DATE1] not provided");
                Entity? date1Entity = _SAMService.Message?.RefData?.GetEntity(date1Parm);
                if (date1Entity == null) return result.Skip("Parm [DATE1] did not resolve to a valid entity in the model");
                var date1Key = $"{date1Entity.Mnemonic}|{elementEval.ElementSequence}";
                EvaluationItem date1AttrItem = elementEval.GetChildItem(date1Key);
                if (date1AttrItem == null) return result.Skip("Date1 attribute not found");
                if (!date1AttrItem.HasMessageItem || date1AttrItem.MessageItem.MessageData == null) return result.Skip("Attribute [DATE1] is unpopulated");
                DateTime? date1 = date1AttrItem.MessageItem.MessageData.DateTimeValue();
                if (date1 == null) return result.Skip("Attribute [DATE1] is not a valid date");

                // Get Date2
                string date2Parm = request.GetParameterValue("DATE2");
                if (string.IsNullOrEmpty(date2Parm)) return result.Skip("Parm [DATE2] not provided");
                Entity? date2Entity = _SAMService.Message?.RefData?.GetEntity(date2Parm);
                if (date2Entity == null) return result.Skip("Parm [DATE2] did not resolve to a valid entity in the model");
                var date2Key = $"{date2Entity.Mnemonic}|{elementEval.ElementSequence}";
                EvaluationItem date2AttrItem = elementEval.GetChildItem(date2Key);
                if (date2AttrItem == null) return result.Skip("Date2 attribute not found");
                if (!date2AttrItem.HasMessageItem || date2AttrItem.MessageItem.MessageData == null) return result.Skip("Attribute [DATE2] is unpopulated");
                DateTime? date2 = date2AttrItem.MessageItem.MessageData.DateTimeValue();
                if (date2 == null) return result.Skip("Attribute [DATE2] is not a valid date");

                // Failure condition
                if (date2.Value < date1.Value) return result.Fail();

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
        public static string StaticMnemonic => "ELEMENT_DATES_OCCUR_IN_PROPER_SEQUENCE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
