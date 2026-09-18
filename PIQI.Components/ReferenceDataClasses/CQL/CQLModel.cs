namespace PIQI.Components.Models
{
    public class CQLModel
    {
        public int ID { get; set; }

        public string ModelUrl { get; set; } = null!;

        public string Qualifier { get; set; } = null!;

        public string ModelName { get; set; } = null!;

        public string ModelVersion { get; set; } = null!;

        public string RootName { get; set; } = null!;

        public string ModelMnemonic { get; set; } = null!;

        public string ModelInfoXml { get; set; } = null!;

        public string HelperFunctionsCql { get; set; } = null!;

        public bool IsActive { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public DateTime ModifiedDateTime { get; set; }

        public string PIQIModelMnemonic { get; set; } = null!;
    }
}
