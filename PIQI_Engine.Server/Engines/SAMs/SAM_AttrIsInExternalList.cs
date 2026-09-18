using Azure;
using Azure.Core;
using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;
using System.Xml.Linq;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks if an attribute's value exists in an external value list.
    /// </summary>
    public class SAM_AttrIsInExternalList : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrIsInExternalList"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrIsInExternalList(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the text value of a message attribute exists in a specified external value list.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the message object and parameters for evaluation. 
        /// The <c>MessageObject</c> property must be a <see cref="MessageModelItem"/> whose 
        /// <c>MessageData</c> is of type <see cref="BaseText"/>. 
        /// The <c>ParameterList</c> must include a tuple with <c>Item1</c> equal to "Code System List"
        /// and <c>Item2</c> containing the value list mnemonic to check against.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result. 
        /// The <see cref="PIQISAMResponse"/> indicates whether the text value exists in the specified value list,
        /// or contains an error message if evaluation fails.
        /// </returns>
        /// <remarks>
        /// The method performs the following checks:
        /// <list type="bullet">
        /// <item><description>Validates that the parameter list contains the "Code System List" entry.</description></item>
        /// <item><description>Ensures the <see cref="_SAMService.ReferenceData"/> and its <c>ValueList</c> are available.</description></item>
        /// <item><description>Verifies that the specified value list mnemonic exists in the reference data.</description></item>
        /// <item><description>Performs a case-insensitive comparison of the attribute's text against both <c>DataCode</c> and <c>DataText</c> entries in the value list.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if:
        /// <list type="bullet">
        /// <item>The parameter list is null or does not contain "Code System List".</item>
        /// <item>The reference data or value list is missing.</item>
        /// <item>The specified value list mnemonic does not exist in the reference data.</item>
        /// </list>
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            {
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                if (evaluationItem.ItemType != EntityItemTypeEnum.Attribute) throw new Exception($"Sam [{this.SAMObject.Name}] incorrectly bound to non-attribute entity");

                // Message item should be populated - dependent SAM should have caught if it's not
                MessageModelItem item = evaluationItem?.MessageItem;
                if (item == null) return result.Fail("Attribute not populated");

                // Get value text
                BaseText data = (BaseText)item.MessageData;
                if (data == null || data.Text == null) return result.Fail("Attribute not populated");
                string valueText = data.Text;

                // Get set mnemonic
                if (request.ParmList == null) throw new Exception("Parameter list was not supplied");
                string setMnemonic = request.GetParameterValue("EXTERNAL_LIST_MNEMONIC");
                if (string.IsNullOrEmpty(setMnemonic)) throw new Exception("Parameter [External List Mnemonic] was not supplied");

                // Verify _SAMService.ReferenceData is not null
                if (_SAMService.Message.RefData == null || _SAMService?.Message?.RefData?.ValueList == null) throw new Exception("Missing or invalid reference data for SAM_AttrIsInExternalList");

                // Verify that the value list exists in the reference data
                if (!_SAMService.Message.RefData.ValueList.Any(v => v.Mnemonic == setMnemonic))
                    throw new Exception("Value data [" + setMnemonic + "] not in RefData. Check processing engine.");

                // Retrieve the value list (case-insensitive match)
                ValueList value = _SAMService.Message.RefData.ValueList
                    .FirstOrDefault(v => v.Mnemonic.Equals(setMnemonic, StringComparison.OrdinalIgnoreCase));

                if (value.CodeList.Any(c =>
                    c.DataCode.Equals(valueText, StringComparison.OrdinalIgnoreCase) ||
                    c.DataText.Equals(valueText, StringComparison.OrdinalIgnoreCase))
                )
                    passed = true;

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
        public static string StaticMnemonic => "ATTR_INEXTERNALLIST";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
