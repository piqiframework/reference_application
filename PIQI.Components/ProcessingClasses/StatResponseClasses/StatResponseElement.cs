namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the result statistics for a specific PIQI SAM element.
    /// Tracks counts of total, skipped, processed, passed, failed, and critical failures.
    /// </summary>
    public class StatResponseElement : StatResponseEntity
    {
        #region Properties

        /// <summary>
        /// The entity type mnemonic associated with this element.
        /// </summary>
        public string? ClassMnemonic { get; set; }

        /// <summary>
        /// The sequence number of this element within its entity type.
        /// </summary>
        public int? Sequence { get; set; }

        /// <summary>
        /// Unique key for this element, typically combining entity type mnemonic and sequence.
        /// </summary>
        public string Key { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Default constructor.
        /// </summary>
        public StatResponseElement() { }

        /// <summary>
        /// Initializes a new instance of <see cref="StatResponseElement"/> with specified entity type and sequence.
        /// </summary>
        /// <param name="evaluationResult">The evaluation result item.</param>
        public StatResponseElement(EvaluationResult evaluationResult)
        {
            Sequence = evaluationResult.EvaluationItem?.ElementSequence;
            ClassMnemonic = evaluationResult.EvaluationItem?.ClassEntityMnemonic;
            Key = $"{evaluationResult.EvaluationItem?.ClassEntityMnemonic}.{evaluationResult.EvaluationItem?.ElementSequence}";
        }

        #endregion
    }
}
