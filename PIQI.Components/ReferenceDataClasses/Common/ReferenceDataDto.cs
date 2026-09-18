namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents a reference data item with a name and a mnemonic.
    /// </summary>
    public class ReferenceDataDto
    {
        /// <summary>
        /// The display name for the reference data.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The mnemonic corresponding to the reference data label.
        /// </summary>
        public string Mnemonic { get; set; }
    }
}
