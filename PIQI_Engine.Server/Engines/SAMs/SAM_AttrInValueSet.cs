using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// A SAM implementation that evaluates whether an attribute (specifically a <see cref="CodeableConcept"/>) contained
    /// within a <see cref="MessageModelItem"/> exists in a specified value set.
    /// </summary>
    public class SAM_AttrInValueSet : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrInValueSet"/> class.
        /// </summary>
        /// <param name="sam">The parent SAM object that owns this evaluation.</param>
        /// <param name="samService">
        /// Service used to access reference data, including value sets and code systems,
        /// and for performing supporting FHIR API calls.
        /// </param>
        public SAM_AttrInValueSet(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the <see cref="CodeableConcept"/> in the request is contained
        /// within a value set specified via the request parameters.
        /// </summary>
        /// <param name="request">
        /// The SAM evaluation request containing the target message object and any parameters,
        /// including the expected value set mnemonic.
        /// </param>
        /// <returns>
        /// A <see cref="PIQISAMResponse"/> indicating whether the concept was found in the value set,
        /// or an error if evaluation failed.
        /// </returns>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            {
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                if (evaluationItem.ItemType != EntityItemTypeEnum.Attribute) throw new Exception($"Sam [{this.SAMObject.Name}] must be bound to an attribute");

                // Get message data from the message model item and validate that it is a CodeableConcept
                MessageModelItem item = evaluationItem?.MessageItem;
                BaseText data = (BaseText)item.MessageData;

                // Validate the data format
                if (data is not CodeableConcept codeableConcept)
                    throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                // The value set mnemonic and attribute mnemonic are passed as parameters
                if (request.ParmList == null) throw new Exception("Parameter list was not supplied");

                // Get value set
                string valueSetMnemonic = request.GetParameterValue("VALUE_SET_MNEMONIC");
                if (string.IsNullOrWhiteSpace(valueSetMnemonic)) return result.Skip("Parameter [Value Set Mnemonic] was not supplied");

                // Get all valid code/code systems from the value set via the value set mnemonic parameter
                ValueSet valueSet = await _SAMService.GetValueSetAsync(valueSetMnemonic);

                //Check if there are any codings in the data that are in the codingList from the value set
                if (codeableConcept?.CodingList != null &&
                    valueSet.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                    cd.IsValid && 
                    cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                    cd.CodeSystemList.Any(cs =>
                    _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem : 
                    _SAMService.Message.RefData.GetCodeSystem(cs) == _SAMService.Message.RefData.GetCodeSystem(c.CodeSystem)))))
                {
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
        public static string StaticMnemonic => "ATTR_INVALUESET";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
