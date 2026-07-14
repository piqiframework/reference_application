using Newtonsoft.Json.Linq;
using PIQI.Components.Services;

namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the header portion of a message, including metadata such as contributor, data source, and transaction date.
    /// </summary>
    public class MessageModelHeader
    {
        #region Properties

        /// <summary>
        /// The entity model mnemonic information.
        /// </summary>
        public MessageModelHeaderString EntityModelMnemonicData { get; set; }

        /// <summary>
        /// The contributor name information.
        /// </summary>
        public MessageModelHeaderString ContributorNameData { get; set; }

        /// <summary>
        /// The data source name information.
        /// </summary>
        public MessageModelHeaderString DataSourceNameData { get; set; }

        /// <summary>
        /// The client message ID information.
        /// </summary>
        public MessageModelHeaderString ClientMessageIDData { get; set; }

        /// <summary>
        /// The transaction date information.
        /// </summary>
        public MessageModelHeaderDate TransactionDateData { get; set; }

        /// <summary>
        /// Gets the entity model mnemonic value.
        /// </summary>
        public string EntityModelMnemonic => EntityModelMnemonicData.Value;

        /// <summary>
        /// Gets the contributor name value.
        /// </summary>
        public string ContributorName => ContributorNameData.Value;

        /// <summary>
        /// Gets the data source name value.
        /// </summary>
        public string DataSourceName => DataSourceNameData.Value;

        /// <summary>
        /// Gets the client message ID value.
        /// </summary>
        public string ClientMessageID => ClientMessageIDData.Value;

        /// <summary>
        /// Gets the transaction date value.
        /// </summary>
        public DateTime? TransactionDate => TransactionDateData.Value;

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageModelHeader"/> class with default values.
        /// </summary>
        public MessageModelHeader()
        {
            Initialize();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageModelHeader"/> class from a JSON token and optional overrides.
        /// </summary>
        /// <param name="pToken">The JSON token containing header data.</param>
        /// <param name="EntityModelMnemonic">Optional override for entity model mnemonic.</param>
        /// <param name="contributorID">Optional override for contributor name.</param>
        /// <param name="dataSourceID">Optional override for data source name.</param>
        /// <param name="messageID">Optional override for client message ID.</param>
        public MessageModelHeader(JToken pToken, string? EntityModelMnemonic, string? contributorID, string? dataSourceID, string? messageID)
        {
            Initialize();

            EntityModelMnemonicData.OriginalValue = Utility.GetJSONString(pToken, "EntityModel") ?? EntityModelMnemonic;
            ContributorNameData.OriginalValue = contributorID ?? Utility.GetJSONString(pToken, "ContributorID");
            DataSourceNameData.OriginalValue = dataSourceID ?? Utility.GetJSONString(pToken, "DataSourceID");
            ClientMessageIDData.OriginalValue = messageID ?? Utility.GetJSONString(pToken, "MessageID");
            TransactionDateData.OriginalValue = Utility.ObjNullableDateTime(Utility.GetJSONString(pToken, "TransactionDate"));
        }

        /// <summary>
        /// Initializes the header properties with default instances.
        /// </summary>
        private void Initialize()
        {
            EntityModelMnemonicData = new MessageModelHeaderString();
            ContributorNameData = new MessageModelHeaderString();
            DataSourceNameData = new MessageModelHeaderString();
            ClientMessageIDData = new MessageModelHeaderString();
            TransactionDateData = new MessageModelHeaderDate();
        }

        #endregion
    }
}
