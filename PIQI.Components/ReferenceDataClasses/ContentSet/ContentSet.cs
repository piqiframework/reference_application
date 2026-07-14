namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the root object containing a library of content sets.
    /// </summary>
    public class ContentSetListRoot
    {
        /// <summary>
        /// Gets or sets the collection of <see cref="ContentSet"/> objects in the library.
        /// </summary>
        public List<ContentSet> ContentSetLibrary { get; set; } = new();
    }

    /// <summary>
    /// Represents a FHIR content set, including its mnemonic, name, description, type, and associated model and data type information.
    /// </summary>
    public class ContentSet
    {
        #region Properties

        /// <summary>
        /// Gets or sets the descriptive name of the content set.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic identifier for the content set.
        /// </summary>
        public string Mnemonic { get; set; } = null!;

        /// <summary>
        /// Gets or sets the description for the content set.
        /// </summary>
        public string Description { get; set; } = null!;

        /// <summary>
        /// Gets or sets the mnemonic identifier for the content set type.
        /// </summary>
        public string ContentSetTypeMnemonic { get; set; } = null!;

        /// <summary>
        /// Gets or sets the data type identifier this content set pertains to.
        /// </summary>
        public int KeyDataTypeID { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic identifier for the model this content set pertains to.
        /// </summary>
        public string ModelMnemonic { get; set; }

        #endregion
    }
}
