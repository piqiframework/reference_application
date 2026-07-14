using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM evaluation engine that determines whether an attribute value
    /// is formatted as a valid National Provider Identifier (NPI).
    /// </summary>
    /// <remarks>
    /// <para>
    /// This evaluator inspects the <see cref="MessageModelItem"/> contained in the request's
    /// <see cref="PIQISAMRequest.EvaluationObject"/> and validates the associated text value
    /// using the standard NPI checksum algorithm.
    /// </para>
    /// <para>
    /// The evaluator supports:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>
    ///   A standard 10-digit NPI value.
    ///   </description></item>
    ///   <item><description>
    ///   A 15-digit NPI representation prefixed with <c>80840</c>.
    ///   </description></item>
    /// </list>
    /// <para>
    /// Validation includes:
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Length validation.</description></item>
    ///   <item><description>Numeric character validation.</description></item>
    ///   <item><description>Luhn-based checksum validation.</description></item>
    /// </list>
    /// <para>
    /// If the attribute value is empty or null, the evaluation is skipped.
    /// </para>
    /// </remarks>
    public class SAM_AttrIsFormattedAsNPI : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of <see cref="SAM_AttrIsFormattedAsNPI"/>.
        /// </summary>
        /// <param name="sam">The SAM definition metadata.</param>
        /// <param name="samService">The SAM service providing dependencies/utilities.</param>
        public SAM_AttrIsFormattedAsNPI(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates the provided request to determine whether the attribute value
        /// is formatted as a valid NPI.
        /// </summary>
        /// <param name="request">
        /// The SAM request containing the <see cref="PIQISAMRequest.EvaluationObject"/>
        /// which must contain a <see cref="MessageModelItem"/> whose
        /// <see cref="MessageModelItem.MessageData"/> is a <see cref="BaseText"/>.
        /// </param>
        /// <returns>
        /// A <see cref="PIQISAMResponse"/> indicating whether the value passed NPI validation,
        /// failed validation, was skipped due to missing data, or encountered an error.
        /// </returns>
        /// <exception cref="InvalidCastException">
        /// Thrown if <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to
        /// <see cref="EvaluationItem"/> or if the message data cannot be cast to
        /// <see cref="BaseText"/>.
        /// </exception>
        /// <exception cref="Exception">
        /// Thrown if an unexpected error occurs during validation processing.
        /// </exception>
        /// <remarks>
        /// <para>
        /// The evaluation logic:
        /// </para>
        /// <list type="number">
        ///   <item><description>
        ///   Casts <see cref="PIQISAMRequest.EvaluationObject"/> to <see cref="EvaluationItem"/>.
        ///   </description></item>
        ///   <item><description>
        ///   Reads the attribute text value from <see cref="BaseText.Text"/>.
        ///   </description></item>
        ///   <item><description>
        ///   Skips evaluation if the attribute is null or empty.
        ///   </description></item>
        ///   <item><description>
        ///   Prepends the <c>80840</c> prefix if the value is only 10 digits.
        ///   </description></item>
        ///   <item><description>
        ///   Verifies that the final value is 15 digits in length.
        ///   </description></item>
        ///   <item><description>
        ///   Extracts and validates the checksum digit using the NPI Luhn algorithm.
        ///   </description></item>
        ///   <item><description>
        ///   Returns the result using <see cref="PIQISAMResponse.Done(bool)"/>,
        ///   <see cref="PIQISAMResponse.Fail(string)"/>, or
        ///   <see cref="PIQISAMResponse.Skip(string)"/>.
        ///   </description></item>
        /// </list>
        /// </remarks>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            {
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Evaluate the item's message data
                string? text = ((BaseText)evaluationItem.MessageItem.MessageData)?.Text;
                if (string.IsNullOrEmpty(text)) return result.Skip("Attribute not populated");

                // Convert 10-digit NPI to 15-digit format with standard prefix
                if (text.Length == 10) text = "80840" + text;

                // Verify overall length
                if (text.Length != 15) return result.Fail("Value was invalid length");

                // Separate base value and checksum digit
                string npiText = text.Substring(0, 14);
                int checkSum = Utility.ObjInt(text.Substring(14, 1));

                // Convert characters to integer list
                List<int> list = new List<int>();
                foreach (char c in npiText)
                {
                    if (!char.IsDigit(c)) return result.Fail("Value contains invalid characters");
                    list.Add(Utility.ObjInt(c.ToString()));
                }

                // Apply NPI checksum weighting
                list[13] = list[13] * 2;
                list[11] = list[11] * 2;
                list[9] = list[9] * 2;
                list[7] = list[7] * 2;
                list[5] = list[5] * 2;
                list[3] = list[3] * 2;
                list[1] = list[1] * 2;

                // Sum all digits, splitting multi-digit values
                int sum = 0;
                foreach (int i in list)
                {
                    string d = i.ToString();
                    foreach (char c in d)
                        sum += (Utility.ObjInt(c.ToString()));
                }

                // Determine expected checksum digit
                int diff = (sum % 10 == 0 ? 0 : 10 - (sum % 10));

                // Eval
                passed = (diff == checkSum);

                // Done
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
        public static string StaticMnemonic => "ATTRIBUTE_IS_FORMATTED_AS_NPI";

        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}