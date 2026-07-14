using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks the text value of the attr against all specified content sets.
    /// Returns true if text value is present in *all* content sets.
    /// </summary>
    public class SAM_AttrIsInExternalListSet : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrIsInExternalListSet"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrIsInExternalListSet(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the value of a message attribute exists in every external value list
        /// specified by the <c>EXTERNAL_LIST_CSV</c> parameter.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the evaluation context.
        /// The <c>EvaluationObject</c> must be an <see cref="EvaluationItem"/> bound to an
        /// attribute (<see cref="EntityItemTypeEnum.Attribute"/>).
        /// The attribute value may be either:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// A <see cref="BaseText"/>, in which case its <c>Text</c> value is evaluated.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A <see cref="CodeableConcept"/>, in which case the <c>CodeValue</c> of each
        /// complete <see cref="Coding"/> is evaluated.
        /// </description>
        /// </item>
        /// </list>
        /// The parameter list must contain an <c>EXTERNAL_LIST_CSV</c> entry whose value is a
        /// comma-separated list of external value list mnemonics.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The response succeeds only if at least one attribute value is found in every specified
        /// external value list. Otherwise, the response fails. Errors are returned if the request
        /// or reference data is invalid.
        /// </returns>
        /// <remarks>
        /// The method performs the following steps:
        /// <list type="bullet">
        /// <item><description>Verifies the evaluation item is bound to an attribute.</description></item>
        /// <item><description>Extracts one or more values from either a <see cref="BaseText"/> or a <see cref="CodeableConcept"/>.</description></item>
        /// <item><description>Retrieves the comma-separated list of external value list mnemonics from the <c>EXTERNAL_LIST_CSV</c> parameter.</description></item>
        /// <item><description>Validates that the required reference data and value lists are available.</description></item>
        /// <item><description>Performs case-insensitive comparisons against both <c>DataCode</c> and <c>DataText</c> values.</description></item>
        /// <item><description>Succeeds only if every specified value list contains at least one matching attribute value.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown if:
        /// <list type="bullet">
        /// <item><description>The parameter list is missing or does not contain <c>EXTERNAL_LIST_CSV</c>.</description></item>
        /// <item><description>The reference data or value lists are unavailable.</description></item>
        /// <item><description>Any specified value list mnemonic does not exist in the reference data.</description></item>
        /// </list>
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // Get the evaluation item from the request
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;

                // Verify the item type is an attribute
                if (evaluationItem.ItemType != EntityItemTypeEnum.Attribute)
                {
                    result.Error($"Sam [{this.SAMObject.Name}] incorrectly bound to non-attribute entity");
                    return result;
                }

                // Set the message model item
                MessageModelItem item = evaluationItem?.MessageItem;
                if (item == null) return result.Fail("Attribute not populated");

                List<string> valueTextList = new List<string>();
                if (item.MessageData is CodeableConcept)
                {
                    // Get all codes from complete codings
                    CodeableConcept concept = (CodeableConcept)item.MessageData;
                    if (concept.HasCodedItems)
                    {
                        foreach (Coding coding in concept.CodingList.Where(t => t.IsComplete))
                            valueTextList.Add(coding.CodeValue);
                    }

                    // Fail condition: no data
                    if (valueTextList.Count < 1) return result.Fail("Attribute contained no complete codings");
                }
                else
                {
                    // Get the text value
                    BaseText data = (BaseText)item.MessageData;
                    if (!string.IsNullOrEmpty(data.Text))
                        valueTextList.Add(data.Text);

                    // Fail condition: no data
                    if (valueTextList.Count < 1) return result.Fail("Attribute was unpopulated");
                }

                // Get the ValueListMnemonic parameter
                if (request.ParmList == null) throw new Exception("Parameter list was not supplied");
                Tuple<string, string> arg1 = request.ParmList.Where(t => t.Item1 == "EXTERNAL_LIST_CSV").FirstOrDefault();
                if (arg1 == null) throw new Exception("[External List CSV] parameter not found");
                List<string> setList = Utility.Split(arg1.Item2);

                // Verify _SAMService.ReferenceData is not null
                if (_SAMService.Message.RefData == null || _SAMService?.Message?.RefData?.ValueList == null) throw new Exception("Missing or invalid reference data for SAM_AttrIsInExternalList");

                foreach (string setMnemonic in setList)
                {
                    var setPassed = false;
                    // Verify that the value list exists in the reference data
                    if (!_SAMService.Message.RefData.ValueList.Any(v => v.Mnemonic == setMnemonic))
                        throw new Exception("Value data [" + setMnemonic + "] not in RefData. Check processing engine.");

                    // Retrieve the value list (case-insensitive match)
                    ValueList value = _SAMService.Message.RefData.ValueList
                        .FirstOrDefault(v => v.Mnemonic.Equals(setMnemonic, StringComparison.OrdinalIgnoreCase));

                    foreach (string valueText in valueTextList)
                    {
                        if (value.CodeList.Any(c =>
                                c.DataCode.Equals(valueText, StringComparison.OrdinalIgnoreCase) ||
                                c.DataText.Equals(valueText, StringComparison.OrdinalIgnoreCase)
                            )
                        )
                        {
                            setPassed = true;
                            break;
                        }
                    }

                    // If any set fails then the SAM as a whole fails
                    if (!setPassed) return result.Fail();
                }

                // Update result
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
        public static string StaticMnemonic => "ATTR_INEXTERNALLISTSET";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
