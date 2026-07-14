namespace PIQI.Components.Models
{
    /// <summary>
    /// Defines the different types of entity roles supported by the PIQI Engine.
    /// </summary>
    public enum RoleTypeEnum
    {
        /// <summary>
        /// Represents the primary concept or entity being described.
        /// </summary>
        PRIMARY_CONCEPT = 1,

        /// <summary>
        /// Represents the date and time when something becomes effective.
        /// </summary>
        EFFECTIVE_DATETIME = 2,

        /// <summary>
        /// Represents the start date and time of a period or range.
        /// </summary>
        START_DATETIME = 3,

        /// <summary>
        /// Represents the end date and time of a period or range.
        /// </summary>
        END_DATETIME = 4,

        /// <summary>
        /// Represents the primary value associated with the entity.
        /// </summary>
        PRIMARY_VALUE = 5,

        /// <summary>
        /// Represents the primary unit of measure.
        /// </summary>
        PRIMARY_UOM = 6
    }
}
