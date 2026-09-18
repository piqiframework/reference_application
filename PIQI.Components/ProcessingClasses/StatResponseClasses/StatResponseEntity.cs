namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the aggregated results for an entity's PIQI SAMs.
    /// Tracks totals, skips, processed counts, passes, fails, and weighted scores for a specific entity type.
    /// </summary>
    public class StatResponseEntity
    {
        #region Properties

        /// <summary>
        /// Total number of PIQI SAMs for this entity.
        /// </summary>
        public int SAMTotalCount { get; set; }

        /// <summary>
        /// Total number of skipped PIQI SAMs for this entity.
        /// </summary>
        public int SAMSkipCount { get; set; }

        /// <summary>
        /// Total number of processed PIQI SAMs for this entity.
        /// </summary>
        public int SAMProcessedCount { get; set; }

        /// <summary>
        /// Total number of scoring PIQI SAMs processed for this entity.
        /// </summary>
        public int SAMScoringProcessedCount { get; set; }

        /// <summary>
        /// Total number of informational PIQI SAMs processed for this entity.
        /// </summary>
        public int SAMInfoProcessedCount { get; set; }

        /// <summary>
        /// Total number of PIQI SAMs that passed for this entity.
        /// </summary>
        public int SAMPassCount { get; set; }

        /// <summary>
        /// Total number of PIQI SAMs that failed for this entity.
        /// </summary>
        public int SAMFailCount { get; set; }

        /// <summary>
        /// Weighted denominator for scoring calculations for this entity.
        /// </summary>
        public int SAMWeightedDenominator { get; set; }

        /// <summary>
        /// Weighted numerator for scoring calculations for this entity.
        /// </summary>
        public int SAMWeightedNumerator { get; set; }

        /// <summary>
        /// Number of critical failures for this entity.
        /// </summary>
        public int SAMCriticalFailureCount { get; set; }

        /// <summary>
        /// Indicates if the entity is clean (no failed SAMs).
        /// </summary>
        public bool IsClean { get { return SAMFailCount < 1; } }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="StatResponseEntity"/> entity
        /// for the specified entity type mnemonic.
        /// </summary>
        public StatResponseEntity() { }

        #endregion

        #region Methods

        /// <summary>
        /// Increments the statistics of the entity based on the provided PIQI SAM.
        /// </summary>
        /// <param name="evaluationResult">The evaluation result item.</param>
        public void Increment(EvaluationResult evaluationResult)
        {
            // Always increment total 
            SAMTotalCount++;

            if (evaluationResult.EvalSkipped)
            {
                // Increment skip count if the state is skipped
                SAMSkipCount++;
            }
            else
            {
                // Increment processed count and weighted total if the state is not skipped
                SAMProcessedCount++;

                if (evaluationResult.IsScoring)
                {
                    SAMScoringProcessedCount++;
                    SAMWeightedDenominator += evaluationResult.Criterion?.ScoringWeight ?? 0;

                    if (evaluationResult.EvalPassed)
                    {
                        // Increment pass count and weighted numerator if passed
                        SAMPassCount++;
                        SAMWeightedNumerator += evaluationResult.Criterion?.ScoringWeight ?? 0;
                    }
                    else
                    {
                        // Increment fail count if failed
                        SAMFailCount++;

                        // Increment critical fail count if the failure is critical
                        if (evaluationResult.IsCritical) SAMCriticalFailureCount++;
                    }
                }
                else
                {
                    // Increment informational processed count if not scoring
                    SAMInfoProcessedCount++;
                }
            }
        }

        #endregion
    }
}
