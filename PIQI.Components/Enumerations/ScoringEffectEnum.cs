namespace PIQI.Components.Models
{
    /// <summary>
    /// Defines the scoring effect of an evaluation criterion.
    /// </summary>
    public enum ScoringEffectEnum
    {
        /// <summary>
        /// The criterion affects the score of the evaluation.
        /// </summary>
        Scoring = 1,

        /// <summary>
        /// The criterion is informational and does not affect the score.
        /// </summary>
        Informational = 2,

        /// <summary>
        /// The criterion is detectin based and does not affect the score.
        /// NOTE: Detection criteria are ignored by the PIQI Engine. This scoring affect is only handled in the PIQXL Engine.
        /// </summary>
        Detection = 3,

        /// <summary>
        /// The criterion is plausibility based and does not affect the score.
        /// </summary>
        Plausibility = 4
    }
}
