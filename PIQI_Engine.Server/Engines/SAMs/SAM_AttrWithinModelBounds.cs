using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM implementation that checks if an attribute's occurs after ModelStartDate and before ModelEndDate.
    /// </summary>
    public class SAM_AttrWithinModelBounds : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_AttrWithinModelBounds"/> class.
        /// </summary>
        /// <param name="sam">The SAM object associated with this evaluator.</param>
        /// /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data and make FHIR API calls.
        /// </param>
        public SAM_AttrWithinModelBounds(SAM sam, SAMService samService) : base(sam, samService) { }

        /// <summary>
        /// Evaluates whether an attribute's date value falls strictly after the evaluation model's
        /// start date and, if defined, strictly before the evaluation model's end date.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing the evaluation context.
        /// The <c>EvaluationObject</c> must be an <see cref="EvaluationItem"/> bound to an
        /// message attribute. The attribute's value must be convertible to a
        /// <see cref="DateTime"/> using <see cref="MessageData.DateTimeValue"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// <para>
        /// The response:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Succeeds if the attribute date is strictly greater than <see cref="EvaluationItem.ModelStartDate"/> and, when present, strictly less than <see cref="EvaluationItem.ModelEndDate"/>.</description></item>
        /// <item><description>Fails if the attribute date falls outside those bounds.</description></item>
        /// <item><description>Returns <c>Skip</c> if the evaluation cannot be performed due to missing model dates, an invalid binding, missing attribute data, or an unparseable date.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The evaluation performs the following steps:
        /// <list type="bullet">
        /// <item><description>Verifies that <see cref="EvaluationItem.ModelStartDate"/> is defined.</description></item>
        /// <item><description>Verifies that the bound evaluation item is an attribute.</description></item>
        /// <item><description>Verifies that the attribute contains message data.</description></item>
        /// <item><description>Attempts to obtain a <see cref="DateTime"/> from the attribute using <see cref="MessageData.DateTimeValue"/>.</description></item>
        /// <item><description>Checks that the attribute date is strictly after the model start date and, if a model end date exists, strictly before the model end date.</description></item>
        /// </list>
        /// </remarks>
        /// <exception cref="Exception">
        /// Any unexpected exception encountered during evaluation is caught and returned as an
        /// error in the <see cref="PIQISAMResponse"/>.
        /// </exception>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new();
            bool passed = false;

            try
            {
                // First parameter is always an eval item - in this case an attribute item 
                EvaluationItem evaluation = (EvaluationItem)request.EvaluationObject;

                // Skip conditions
                if (evaluation.ModelStartDate == null) return result.Skip("Model Start is undefined");
                if (evaluation.ItemType != EntityItemTypeEnum.Attribute) return result.Skip("Bound item is not an attribute");
                if (!evaluation.HasMessageItem || evaluation.MessageItem?.MessageData == null) return result.Skip("Bound item has no data");
                DateTime? attrDate = evaluation.MessageItem?.MessageData?.DateTimeValue();
                if (attrDate == null) return result.Skip("Bound item is not recognized as a date");

                // Validate date against modelStartDate and modelEndDate (if there is an end date)
                passed = attrDate > evaluation.ModelStartDate && (evaluation.ModelEndDate == null || attrDate < evaluation.ModelEndDate);

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
        public static string StaticMnemonic => "ATTR_WITHIN_MODEL_BOUNDS";
        /// <summary>
        /// Gets the mnemonic string associated with this instance.
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
}
