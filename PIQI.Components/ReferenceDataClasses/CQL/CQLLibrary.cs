using CQLTest.Service;

namespace PIQI.Components.Models
{
    public class CQLLibrary
    {
        public int ID { get; set; }

        public string Name { get; set; } = null!;

        public string Version { get; set; } = null!;

        public string Mnemonic { get; set; } = null!;

        public int CQLTypeID { get; set; }

        public string CQLText { get; set; } = null!;

        public string? Description { get; set; }

        public int? CQLModelID { get; set; }

        public string? ElmJson { get; set; }

        public string? CodeSystems { get; set; }

        public string? ValueSets { get; set; }

        public string? HDQTDimensionMnemonic { get; set; }
         
        public string? EntityMnemonic { get; set; }

        public DateTime CreatedDateTime { get; set; }

        public DateTime ModifiedDateTime { get; set; }
    }

    public class CQLItem
    {
        public string Name { get; set; } = null!;

        public string Mnemonic { get; set; } = null!;

        public string CQLText { get; set; } = null!;
        public FieldMappings? FieldMappings { get; set; }

        #region Constructors
        public CQLItem() { }
        public CQLItem(CQLReferenceDataProfile cqlProfile, string cqlText, FieldMappings? fieldMappings = null) 
        {
            Name = cqlProfile.Name;
            Mnemonic = cqlProfile.Mnemonic;
            CQLText = cqlText;
            FieldMappings = fieldMappings;
        }
        #endregion
    }
}
