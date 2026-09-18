using Newtonsoft.Json;

namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the root container for reference data profiles.
    /// </summary>
    public class ReferenceDataProfileRoot
    {
        /// <summary>
        /// A collection of evaluation profiles.
        /// </summary>
        [JsonProperty("EvaluationProfileLibrary")]
        public List<ReferenceDataProfile>? EvaluationProfiles { get; set; }

        /// <summary>
        /// A collection of model profiles.
        /// </summary>
        [JsonProperty("ModelLibrary")]
        public List<ReferenceDataProfile>? ModelProfiles { get; set; }

        /// <summary>
        /// A collection of CQL library profiles.
        /// </summary>
        [JsonProperty("CQLProfileLibrary")]
        public List<CQLReferenceDataProfile>? CQLProfiles { get; set; }
    }

    /// <summary>
    /// Represents an individual evaluation profile with basic metadata.
    /// </summary>
    public class ReferenceDataProfile
    {
        #region Properties

        /// <summary>
        /// The name of the evaluation profile.
        /// </summary>
        public string Name { get; set; } = null!;

        /// <summary>
        /// The unique mnemonic of the evaluation profile.
        /// </summary>
        public string Mnemonic { get; set; } = null!;

        /// <summary>
        /// The file path where the evaluation profile is stored.
        /// </summary>
        public string FilePath { get; set; } = null!;

        #endregion

        #region Constructors
        public ReferenceDataProfile() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="ReferenceDataProfile"/> class.
        /// </summary>
        /// <param name="name">The display name of the profile.</param>
        /// <param name="mnemonic">The unique mnemonic identifier associated with the profile.</param>
        /// <param name="filePath">
        /// An optional file path for the profile. 
        /// If not provided, the profile will not be associated with a file.
        /// </param>
        public ReferenceDataProfile(string name, string mnemonic, string? filePath = null)
        {
            Name = name;
            Mnemonic = mnemonic;
            if (filePath != null)
                FilePath = filePath;
        }

        #endregion
    }

    /// <summary>
    /// Represents an individual evaluation profile with basic metadata.
    /// </summary>
    public class CQLReferenceDataProfile : ReferenceDataProfile
    {
        #region Properties
        /// <summary>
        /// The file path where the field mapping is stored.
        /// </summary>
        public string? FieldMappingFilePath { get; set; }

        #endregion

        #region Constructors

        public CQLReferenceDataProfile() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="CQLReferenceDataProfile"/> class.
        /// </summary>
        /// <param name="name">The display name of the profile.</param>
        /// <param name="mnemonic">The unique mnemonic identifier associated with the profile.</param>
        /// <param name="cqlFilePath">The file path where the CQL is stored.</param>
        /// <param name="fieldMappingFilePath">An optional file path for the field mapping. If not provided, the profile will not be associated with a field mapping file.</param>
        public CQLReferenceDataProfile(string name, string mnemonic, string cqlFilePath, string? fieldMappingFilePath = null)
        {
            Name = name;
            Mnemonic = mnemonic;
            FilePath = cqlFilePath;
            FieldMappingFilePath = fieldMappingFilePath;
        }

        #endregion
    }
}
