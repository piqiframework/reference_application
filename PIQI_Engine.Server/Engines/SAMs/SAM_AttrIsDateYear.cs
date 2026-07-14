using PIQI.Components.SAMs;
using PIQI.Components.Models;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM that succeeds if the date either is a parseable date OR it's a four digit year.
    /// </summary>
    /// <remarks>
    /// Derived from <see cref="SAM_AttrIsDate">SAM_AttrIsDate</see>.
    /// </remarks>
    public class SAM_AttrIsDateYear : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrIsDateYear"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrIsDateYear(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the text value of a message attribute represents either a valid
        /// <see cref="DateTime"/> or a four-digit year.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the message object to evaluate.
        /// The <c>EvaluationObject</c> must be an <see cref="EvaluationItem"/> whose
        /// <see cref="EvaluationItem.MessageItem"/> contains a
        /// <see cref="MessageModelItem"/> with <see cref="BaseText"/> message data.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The returned <see cref="PIQISAMResponse"/> indicates whether the attribute value
        /// is either a valid date or a four-digit year, or contains an error message if
        /// evaluation fails.
        /// </returns>
        /// <remarks>
        /// The value is considered valid if either:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// It can be successfully parsed into a <see cref="DateTime"/> whose date component
        /// is greater than <see cref="DateTime.MinValue"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// It consists of exactly four numeric digits (for example, <c>2025</c>),
        /// representing a year.
        /// </description>
        /// </item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to
        /// <see cref="EvaluationItem"/>, if the associated
        /// <see cref="MessageModelItem.MessageData"/> is not a <see cref="BaseText"/>,
        /// or if an unexpected error occurs during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            {
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem item = evaluationItem?.MessageItem;

                // Evaluate the item's message data
                BaseText data = (BaseText)item.MessageData;
                if (data == null || string.IsNullOrEmpty(data.Text)) return result.Fail("Attribute data not populated. Check sam dependencies");

                // Cast to DateTime and validate
                DateTime? dateTime = data.DateTimeValue();
                if (dateTime != null && dateTime.Value.Date > DateTime.MinValue) passed = true;
                else
                {
                    // If invalid DateTime, check if it's a year (a 4 digit number without additional symbols).
                    if (data.Text.Length == 4 && data.Text.All(char.IsDigit))
                        passed = true;
                }
                
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
        public static string StaticMnemonic => "ATTR_ISDATEYEAR";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;

    }
}
