using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks whether two concepts are different.
    /// </summary>
    public class SAM_ElementConceptsAreDifferent : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementConceptsAreDifferent"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementConceptsAreDifferent(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether two <see cref="CodeableConcept"/> attributes within the same element
        /// represent different concepts by ensuring they do not share any valid coding.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the evaluation context.
        /// The <c>EvaluationObject</c> must be an <see cref="EvaluationItem"/> bound to an
        /// element. The parameter list must define the <c>CONCEPT1</c> and
        /// <c>CONCEPT2</c> attribute mnemonics identifying the two
        /// <see cref="CodeableConcept"/> attributes to compare.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The response succeeds if the two concepts have no matching valid codings,
        /// fails if they share at least one valid coding with the same recognized code
        /// system and code value, returns <c>Skip</c> if the evaluation prerequisites
        /// are not met, or returns an error if an unexpected exception occurs.
        /// </returns>
        /// <remarks>
        /// The evaluation performs the following steps:
        /// <list type="bullet">
        /// <item><description>Verifies the evaluation item is bound to an element.</description></item>
        /// <item><description>Retrieves the attributes identified by the <c>CONCEPT1</c> and <c>CONCEPT2</c> parameters.</description></item>
        /// <item><description>Verifies that both attributes contain <see cref="CodeableConcept"/> values.</description></item>
        /// <item><description>Ensures each concept contains at least one complete coding.</description></item>
        /// <item><description>Performs FHIR code lookup for each concept if it has not already been performed.</description></item>
        /// <item><description>Verifies that each concept contains at least one valid coding after lookup.</description></item>
        /// <item><description>Compares all valid codings and fails if any coding has both the same recognized code system and code value.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Any unexpected exception encountered during evaluation is caught and returned
        /// as an error in the <see cref="PIQISAMResponse"/>.
        /// </exception>

        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case an element item
                EvaluationItem elementEval = (EvaluationItem)request.EvaluationObject;

                // Skip conditions
                if (elementEval.ItemType != EntityItemTypeEnum.Element) return result.Skip($"Sam [{this.SAMObject.Name}] must be bound to an element");

                // Get concept1
                CodeableConcept? concept1 = null;
                string attrParm1 = request.GetParameterValue("CONCEPT1");
                if (string.IsNullOrEmpty(attrParm1)) return result.Skip("Parm [CONCEPT1] not provided");
                Entity? attr1Entity = _SAMService.Message?.RefData?.GetEntity(attrParm1);
                var attr1Key = $"{attr1Entity}|{elementEval.ElementSequence}";
                EvaluationItem attrItem1 = elementEval.GetChildItem(attr1Key);

                if (attrItem1 != null && attrItem1.HasMessageItem && attrItem1.MessageItem.MessageData != null && attrItem1.MessageItem.MessageData is CodeableConcept)
                    concept1 = (CodeableConcept)attrItem1.MessageItem.MessageData;
                if (concept1 == null) return result.Skip("Concept1 not available");
                // Verify at least one complete coding exists
                if (!concept1.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Concept1 has no complete codings");

                // Call FHIR server if not called already
                if (!concept1.FHIRServerCalled)
                    await _SAMService.LookupCodeAsync(concept1);

                // Check if any codings are valid
                if (!concept1.CodingList.Any(t => t.IsValid)) return result.Skip("Concept1 has no valid codings");


                // Get concept2
                CodeableConcept? concept2 = null;
                string attrParm2 = request.GetParameterValue("CONCEPT2");
                if (string.IsNullOrEmpty(attrParm2)) return result.Skip("Parm [CONCEPT2] not provided");
                Entity? attr2Entity = _SAMService.Message?.RefData?.GetEntity(attrParm2);
                var attr2Key = $"{attr2Entity}|{elementEval.ElementSequence}";
                EvaluationItem attrItem2 = elementEval.GetChildItem(attr2Key);

                if (attrItem2 != null && attrItem2.HasMessageItem && attrItem2.MessageItem.MessageData != null && attrItem2.MessageItem.MessageData is CodeableConcept)
                    concept2 = (CodeableConcept)attrItem2.MessageItem.MessageData;
                if (concept2 == null) return result.Skip("Concept2 not available");
                // Verify at least one complete coding exists
                if (!concept1.CodingList.Any(c => c.IsComplete == true)) return result.Skip("Concept2 has no complete codings");

                // Call FHIR server if not called already
                if (!concept2.FHIRServerCalled)
                    await _SAMService.LookupCodeAsync(concept2);

                // Check if any codings are valid
                if (!concept2.CodingList.Any(t => t.IsValid)) return result.Skip("Concept2 has no valid codings");


                // Intersect codings
                foreach (Coding c1 in concept1.CodingList.Where(t => t.IsValid))
                {
                    // If any valid coding in Concept1 has a match on Concept2 then we fail
                    if (concept2.CodingList.Where(t => t.IsValid && t.RecognizedCodeSystem == c1.RecognizedCodeSystem && t.CodeValue == c1.CodeValue).Any())
                        return result.Fail();
                }

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
        public static string StaticMnemonic => "ELEMENT_CONCEPTS_ARE_DIFFERENT"; 
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
