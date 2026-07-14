using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that evaluates whether the frequency of elements
    /// within a specified time span exceeds a defined limit.
    /// </summary>
    public class SAM_ElementFrequencyIsPlausible : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_ElementFrequencyIsPlausible"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_ElementFrequencyIsPlausible(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether the frequency of qualifying elements falls within an acceptable threshold.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the <see cref="EvaluationItem"/> to evaluate.
        /// The evaluation item must represent a class containing child elements with primary concepts and effective dates.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous operation.
        /// Returns a passed result if the number of qualifying elements within the defined time span does not exceed the limit,
        /// a failed result if the limit is exceeded,
        /// a skipped result if required parameters or data are missing,
        /// or an error result if evaluation fails.
        /// </returns>
        /// <remarks>
        /// <para>The evaluation processes elements that:</para>
        /// <list type="bullet">
        /// <item><description>Contain a valid primary concept</description></item>
        /// <item><description>Have at least one valid and complete coding</description></item>
        /// <item><description>Belong to the specified value set</description></item>
        /// <item><description>Have a valid effective date</description></item>
        /// </list>
        /// <para>
        /// A sliding time window is applied using the configured <c>SPAN_IN_DAYS</c> parameter.
        /// The evaluation fails if the number of qualifying elements within any window exceeds the <c>LIMIT</c>.
        /// </para>
        /// <para>The evaluation is skipped under the following conditions:</para>
        /// <list type="bullet">
        /// <item><description>Required parameters are missing or invalid</description></item>
        /// <item><description>Qualifying elements are missing effective dates</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Thrown when the <see cref="PIQISAMRequest.EvaluationObject"/> cannot be cast to <see cref="EvaluationItem"/>
        /// or when an unexpected data type is encountered during evaluation.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();

            try
            {
                // First parm is always an eval item - in this case a class item
                EvaluationItem classEval = (EvaluationItem)request.EvaluationObject;

                // Get our valueset parms
                string setMnemonic = request.GetParameterValue("PRIMARY_CONCEPT_VALUESET");
                string spanInDaysText = request.GetParameterValue("SPAN_IN_DAYS"); 
                string limitText = request.GetParameterValue("LIMIT");

                // Error conditions
                if (string.IsNullOrEmpty(setMnemonic)) return result.Skip("Parameter [Primary Concept ValueSet] was not supplied");
                if (string.IsNullOrEmpty(spanInDaysText)) return result.Skip("Parameter [Span In Days] was not supplied");
                if (string.IsNullOrEmpty(limitText)) return result.Skip("Parameter [Limit] was not supplied");
                int spanInDays = Utility.ObjInt(spanInDaysText);
                if (spanInDays < 1) return result.Skip("Parameter [Span In Days] was invalid");
                int limit = Utility.ObjInt(limitText);
                if (limit < 1) return result.Skip("Parameter [Limit] was invalid");

                // Get Value Set
                ValueSet valueSet = await _SAMService.GetValueSetAsync(setMnemonic);

                // Get our list of qualifiying elements
                List<FrequencyItem> frequencyList = new List<FrequencyItem>();
                foreach (EvaluationItem elementEval in classEval.ChildDict.Values)
                { 
                    MessageModelItem item = elementEval?.MessageItem;

                    // Verify the data class contains a defined primary concept and primary value
                    var primaryConceptRole = item?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == RoleTypeEnum.PRIMARY_CONCEPT);
                    if (primaryConceptRole == null) continue;

                    // Verify that the element has valid data for the primary concept roles
                    MessageModelItem? primaryConcept = item?.ChildDict?.GetValueOrDefault(primaryConceptRole.AttributeMnemonic);
                    BaseText? data = (BaseText)primaryConcept?.MessageData;
                    if (data == null || string.IsNullOrEmpty(data.Text)) 
                        continue;

                    // Validate the data  
                    if (data is not CodeableConcept codeableConcept)
                        throw new Exception("CodeableConceptIsValidConcept expects a CodeableConcept value.");

                    // Verify at least one complete coding exists
                    if (!codeableConcept.CodingList.Any(c => c.IsComplete == true)) continue;

                    // Call FHIR server if not called already
                    if (!codeableConcept.FHIRServerCalled)
                        await _SAMService.LookupCodeAsync(codeableConcept);

                    // Check if any codings are valid
                    if (!codeableConcept.CodingList.Any(t => t.IsValid)) continue;

                    // If this element isn't a member of the specified value set then we don't care about it
                    if (codeableConcept?.CodingList == null ||
                        !valueSet.CodingList.Any(c => codeableConcept.CodingList.Any(cd =>
                        cd.IsValid &&
                        cd.CodeValue.Equals(c.CodeValue) && cd.CodeSystemList != null &&
                        cd.CodeSystemList.Any(cs =>
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == null ? cs == c.CodeSystem :
                        _SAMService.Message?.RefData.GetCodeSystem(cs) == _SAMService.Message?.RefData.GetCodeSystem(c.CodeSystem)))))
                        continue;

                    // Get the intersect element 
                    if (elementEval?.HasEffectiveDate == null) elementEval?.GetEffectiveDate();
                    if (elementEval?.HasEffectiveDate != true) return result.Skip("Some qualifying elements were missing an effective date");
                    var freqItem = new FrequencyItem((DateTime)elementEval.EffectiveDate, spanInDays);

                    // Add to our list
                    frequencyList.Add(freqItem);
                }

                // We use a queue to process items effeciently
                Queue<FrequencyItem> itemQueue = new Queue<FrequencyItem>();

                // Process items
                foreach (FrequencyItem freqItem in frequencyList.OrderBy(t => t.EffectiveDate))
                {
                    // Add new item
                    itemQueue.Enqueue(freqItem);

                    // Remove any expired items
                    while (itemQueue.Peek() != null && itemQueue.Peek().ExpirationDate < freqItem.EffectiveDate)
                        itemQueue.Dequeue();

                    // Check our count
                    if (itemQueue.Count > limit) return result.Fail();
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
        /// Represents an item used in frequency calculations, including its effective
        /// date and calculated expiration date based on the configured time span.
        /// </summary>
        internal class FrequencyItem
        {
            #region Properties

            /// <summary>
            /// Gets or sets the effective date of the element.
            /// </summary>
            public DateTime EffectiveDate { get; set; }

            /// <summary>
            /// Gets or sets the expiration date of the element within the sliding window.
            /// </summary>
            public DateTime ExpirationDate { get; set; }

            #endregion

            #region Constructors

            /// <summary>
            /// Initializes a new instance of the <see cref="FrequencyItem"/> class.
            /// </summary>
            /// <param name="effectiveDate">The effective date of the element.</param>
            /// <param name="expireDays">The number of days before the item expires.</param>
            public FrequencyItem(DateTime effectiveDate, int expireDays)
            {
                EffectiveDate = effectiveDate;
                ExpirationDate = EffectiveDate.AddDays((double)expireDays);
            }

            #endregion
        }

        /// <summary>
        /// Gets the mnemonic code for this SAM implementation.
        /// </summary>
        public static string StaticMnemonic => "ELEMENT_FREQUENCY_IS_PLAUSIBLE";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
