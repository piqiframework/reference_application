using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM (Semantic Assessment Module) that evaluates whether a <see cref="CodeableConcept"/>
    /// contains at least one valid coding according to the FHIR server.
    /// </summary>
    public class SAM_ConceptIsCompatible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ConceptIsCompatible"/> class.
        /// </summary>
        /// <param name="sam">The parent <see cref="SAM"/> object providing configuration and context.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ConceptIsCompatible(SAM sam, SAMService samService)
            : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the provided <see cref="MessageModelItem"/> contains
        /// a <see cref="CodeableConcept"/> with at least one valid coding.
        /// Calls the FHIR $lookup API if needed.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing:
        /// <list type="bullet">
        ///   <item>The <see cref="PIQISAMRequest.MessageObject"/>, expected to be a <see cref="MessageModelItem"/> containing a <see cref="CodeableConcept"/>.</item>
        /// </list>
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The response indicates:
        /// <list type="bullet">
        ///   <item><c>Succeeded</c> if at least one coding in the <see cref="CodeableConcept"/> is valid.</item>
        ///   <item><c>Failed</c> if no codings are valid.</item>
        ///   <item><c>Errored</c> if the input data is invalid or an exception occurs.</item>
        /// </list>
        /// </returns>
        /// <exception cref="Exception">
        /// Thrown if the input is not a <see cref="MessageModelItem"/> 
        /// or if its <see cref="MessageModelItem.MessageData"/> is not a <see cref="CodeableConcept"/>.
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

                // Since we're an attr sam we want to play with the item's message data
                BaseText data = (BaseText)item.MessageData;

                // Validate the data format
                if (data is not CodeableConcept codeableConcept)
                    throw new Exception("[coded entity is compatible] expects a Code System CSV codeable concept value.");


                // Get our valueset parameter 
                string codeSystemCSV = request.GetParameterValue("CODE_SYSTEM_CSV");
                if (string.IsNullOrWhiteSpace(codeSystemCSV)) 
                    return result.Skip("Parameter [Code System List] was not supplied");

                // Split the CSV into a list of code systems
                List<string> systemsList = Utility.Split(codeSystemCSV);

                // Update all codings against the list
                foreach (Coding coding in codeableConcept.CodingList)
                    coding.IsInteroperable = systemsList.Contains(coding.RecognizedCodeSystem);

                // Evaluate whether any codings are complete and interoperable
                passed = (codeableConcept.CodingList.Where(t => t.IsComplete && t.IsInteroperable).Any());

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
        public static string StaticMnemonic => "CONCEPT_ISCOMPATIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
