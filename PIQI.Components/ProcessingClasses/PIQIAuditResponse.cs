using System.Text.Json.Serialization;

namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents the top-level response returned from a PIQI audit operation.
    /// </summary>
    public class PIQIAuditResponse
    {
        #region Properties

        /// <summary>
        /// Gets or sets the mnemonic identifying the entity model associated with the audit.
        /// </summary>
        public string EntityModelMnemonic { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the contributor that submitted the message.
        /// </summary>
        public string ContributorID { get; set; }

        /// <summary>
        /// Gets or sets the identifier of the originating data source.
        /// </summary>
        public string DataSourceID { get; set; }

        /// <summary>
        /// Gets or sets the unique identifier of the audited message.
        /// </summary>
        public string MessageID { get; set; }

        /// <summary>
        /// Gets or sets the summary audit results for the message.
        /// </summary>
        public PIQIAuditResult? Audit { get; set; }

        /// <summary>
        /// Gets or sets the hierarchical audit data structure for the message.
        /// </summary>
        public PIQIAuditDataRoot? Root { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditResponse"/> class.
        /// </summary>
        public PIQIAuditResponse() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditResponse"/> class
        /// with the specified message metadata.
        /// </summary>
        /// <param name="entityModelMnemonic">The entity model mnemonic.</param>
        /// <param name="contributorID">The contributor identifier.</param>
        /// <param name="dataSourceID">The data source identifier.</param>
        /// <param name="messageID">The message identifier.</param>
        public PIQIAuditResponse(string entityModelMnemonic, string contributorID, string dataSourceID, string messageID)
        {
            EntityModelMnemonic = entityModelMnemonic;
            ContributorID = contributorID;
            DataSourceID = dataSourceID;
            MessageID = messageID;
        }

        #endregion
    }

    /// <summary>
    /// Represents overall audit scoring results for a message.
    /// </summary>
    public class PIQIAuditResult
    {
        #region Properties

        /// <summary>
        /// Gets the total number of successful audit checks.
        /// </summary>
        public int MessageNumerator { get; }

        /// <summary>
        /// Gets the total number of evaluated audit checks.
        /// </summary>
        public int MessageDenominator { get; }

        /// <summary>
        /// Gets the calculated message score.
        /// </summary>
        public int MessageScore { get; }

        /// <summary>
        /// Gets the weighted numerator value for the message score.
        /// </summary>
        public int MessageNumeratorWeighted { get; }

        /// <summary>
        /// Gets the weighted denominator value for the message score.
        /// </summary>
        public int MessageDenominatorWeighted { get; }

        /// <summary>
        /// Gets the weighted message score.
        /// </summary>
        public int MessageScoreWeighted { get; }

        /// <summary>
        /// Gets the number of critical failures identified during the audit.
        /// </summary>
        public int MessageCriticalFailureCount { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditResult"/> class.
        /// </summary>
        /// <param name="messageNumerator">The total successful audit checks.</param>
        /// <param name="messageDenominator">The total evaluated audit checks.</param>
        /// <param name="messageScore">The calculated message score.</param>
        /// <param name="messageNumeratorWeighted">The weighted numerator value.</param>
        /// <param name="messageDenominatorWeighted">The weighted denominator value.</param>
        /// <param name="messageScoreWeighted">The weighted message score.</param>
        /// <param name="messageCriticalFailureCount">The number of critical failures.</param>
        public PIQIAuditResult(int messageNumerator, int messageDenominator, int messageScore, int messageNumeratorWeighted, int messageDenominatorWeighted, int messageScoreWeighted, int messageCriticalFailureCount)
        {
            MessageNumerator = messageNumerator;
            MessageDenominator = messageDenominator;
            MessageScore = messageScore;
            MessageNumeratorWeighted = messageNumeratorWeighted;
            MessageDenominatorWeighted = messageDenominatorWeighted;
            MessageScoreWeighted = messageScoreWeighted;
            MessageCriticalFailureCount = messageCriticalFailureCount;
        }
    }

    /// <summary>
    /// Represents the root audit data container.
    /// </summary>
    public class PIQIAuditDataRoot
    {
        #region Properties

        /// <summary>
        /// Gets the name of the root audit node.
        /// </summary>
        public string RootName { get; }

        /// <summary>
        /// Gets or sets the collection of audit classes under the root node.
        /// </summary>
        public List<PIQIAuditDataClass>? Classes { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataRoot"/> class.
        /// </summary>
        /// <param name="rootName">The name of the root node.</param>
        public PIQIAuditDataRoot(string rootName)
        {
            RootName = rootName;
        }
    }

    /// <summary>
    /// Represents an audited class within the audit data hierarchy.
    /// </summary>
    public class PIQIAuditDataClass
    {
        #region Properties

        /// <summary>
        /// Gets the name of the audited class.
        /// </summary>
        public string ClassName { get; }

        /// <summary>
        /// Gets or sets the collection of audited elements within the class.
        /// </summary>
        public List<PIQIAuditDataElement>? Elements { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataClass"/> class.
        /// </summary>
        /// <param name="className">The class name.</param>
        public PIQIAuditDataClass(string className)
        {
            ClassName = className;
        }
    }

    /// <summary>
    /// Represents an audited element within a class.
    /// </summary>
    public class PIQIAuditDataElement
    {
        #region Properties

        /// <summary>
        /// Gets or sets the collection of attributes associated with the element.
        /// </summary>
        public List<PIQIAuditDataAttribute>? Attributes { get; set; }

        /// <summary>
        /// Gets or sets the audit results associated with the element.
        /// </summary>
        public PIQIAuditDataElementAudit? ElementAudit { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataElement"/> class.
        /// </summary>
        public PIQIAuditDataElement() { }
    }

    /// <summary>
    /// Represents an audited attribute within an element.
    /// </summary>
    public class PIQIAuditDataAttribute
    {
        #region Properties

        /// <summary>
        /// Gets the name of the audited attribute.
        /// </summary>
        public string AttributeName { get; }

        /// <summary>
        /// Gets or sets the attribute data payload.
        /// </summary>
        public PIQIAuditDataAttributeData? Data { get; set; }

        /// <summary>
        /// Gets or sets the audit information for the attribute.
        /// </summary>
        public PIQIAuditDataAttributeAudit? AttributeAudit { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttribute"/> class.
        /// </summary>
        /// <param name="attributeName">The attribute name.</param>
        public PIQIAuditDataAttribute(string attributeName)
        {
            AttributeName = attributeName;
        }
    }

    /// <summary>
    /// Represents audit scoring data for an element.
    /// </summary>
    public class PIQIAuditDataElementAudit
    {
        /// <summary>
        /// Gets the calculated element score.
        /// </summary>
        public int ElementScore { get; }

        /// <summary>
        /// Gets the weighted element score.
        /// </summary>
        public int ElementScoreWeighted { get; }

        /// <summary>
        /// Gets the number of critical failures identified for the element.
        /// </summary>
        public int ElementCriticalFailureCount { get; }

        /// <summary>
        /// Gets the total successful checks for the element.
        /// </summary>
        public int ElementNumerator { get; }

        /// <summary>
        /// Gets the total evaluated checks for the element.
        /// </summary>
        public int ElementDenominator { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataElementAudit"/> class.
        /// </summary>
        public PIQIAuditDataElementAudit(int elementScore, int elementScoreWeighted, int elementCriticalFailureCount, int elementNumerator, int elementDenominator)
        {
            ElementScore = elementScore;
            ElementScoreWeighted = elementScoreWeighted;
            ElementCriticalFailureCount = elementCriticalFailureCount;
            ElementNumerator = elementNumerator;
            ElementDenominator = elementDenominator;
        }
    }

    /// <summary>
    /// Represents the base type for all audit attribute data payloads.
    /// </summary>
    [JsonDerivedType(typeof(PIQIAuditDataAttributeData_CodeableConcept), typeDiscriminator: "cc")]
    [JsonDerivedType(typeof(PIQIAuditDataAttributeData_ObservationValue), typeDiscriminator: "ov")]
    [JsonDerivedType(typeof(PIQIAuditDataAttributeData_ReferenceRange), typeDiscriminator: "rr")]
    [JsonDerivedType(typeof(PIQIAuditDataAttributeData_Text), typeDiscriminator: "tx")]
    public abstract class PIQIAuditDataAttributeData
    {
        /// <summary>
        /// Gets the text representation of the attribute data.
        /// </summary>
        public string Text { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData"/> class.
        /// </summary>
        /// <param name="text">The text representation of the data.</param>
        public PIQIAuditDataAttributeData(string text)
        {
            Text = text;
        }
    }

    /// <summary>
    /// Represents codeable concept audit data.
    /// </summary>
    public class PIQIAuditDataAttributeData_CodeableConcept : PIQIAuditDataAttributeData
    {
        #region Properties

        /// <summary>
        /// Gets or sets the collection of codings associated with the concept.
        /// </summary>
        public List<PIQIAuditDataAttributeData_CodeableConceptCodings>? Codings { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData_CodeableConcept"/> class.
        /// </summary>
        /// <param name="text">The text representation of the concept.</param>
        public PIQIAuditDataAttributeData_CodeableConcept(string text)
            : base(text) { }
    }

    /// <summary>
    /// Represents an individual coding within a codeable concept.
    /// </summary>
    public class PIQIAuditDataAttributeData_CodeableConceptCodings
    {
        #region Properties

        /// <summary>
        /// Gets the coding system identifier.
        /// </summary>
        public string System { get; }

        /// <summary>
        /// Gets the code value.
        /// </summary>
        public string Code { get; }

        /// <summary>
        /// Gets the display text for the code.
        /// </summary>
        public string Display { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData_CodeableConceptCodings"/> class.
        /// </summary>
        public PIQIAuditDataAttributeData_CodeableConceptCodings(string system, string code, string display)
        {
            System = system;
            Code = code;
            Display = display;
        }
    }

    /// <summary>
    /// Represents observation value audit data.
    /// </summary>
    public class PIQIAuditDataAttributeData_ObservationValue : PIQIAuditDataAttributeData
    {
        #region Properties

        /// <summary>
        /// Gets or sets the observation type information.
        /// </summary>
        public PIQIAuditDataAttributeData_CodeableConcept? Type { get; set; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData_ObservationValue"/> class.
        /// </summary>
        /// <param name="text">The text representation of the observation value.</param>
        public PIQIAuditDataAttributeData_ObservationValue(string text)
            : base(text) { }
    }

    /// <summary>
    /// Represents reference range audit data.
    /// </summary>
    public class PIQIAuditDataAttributeData_ReferenceRange : PIQIAuditDataAttributeData
    {
        #region Properties

        /// <summary>
        /// Gets the low reference value.
        /// </summary>
        public string LowValue { get; }

        /// <summary>
        /// Gets the high reference value.
        /// </summary>
        public string HighValue { get; }

        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData_ReferenceRange"/> class.
        /// </summary>
        public PIQIAuditDataAttributeData_ReferenceRange(string text, string lowValue, string highValue)
            : base(text)
        {
            LowValue = lowValue;
            HighValue = highValue;
        }
    }

    /// <summary>
    /// Represents plain text audit data.
    /// </summary>
    public class PIQIAuditDataAttributeData_Text : PIQIAuditDataAttributeData
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeData_Text"/> class.
        /// </summary>
        /// <param name="text">The text value.</param>
        public PIQIAuditDataAttributeData_Text(string text)
            : base(text) { }
    }

    /// <summary>
    /// Represents audit details for an attribute.
    /// </summary>
    public class PIQIAuditDataAttributeAudit
    {
        /// <summary>
        /// Gets the scoring data associated with the attribute.
        /// </summary>
        public PIQIAuditDataAttributeAuditScoringData ScoringData { get; }

        /// <summary>
        /// Gets or sets the collection of assessment items.
        /// </summary>
        public List<PIQIAuditDataAttributeAuditAssessmentItem>? AssessmentItems { get; set; }

        /// <summary>
        /// Gets or sets the collection of informational items.
        /// </summary>
        public List<PIQIAuditDataAttributeAuditAssessmentItem>? InformationalItems { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeAudit"/> class.
        /// </summary>
        /// <param name="scoringData">The scoring data.</param>
        public PIQIAuditDataAttributeAudit(PIQIAuditDataAttributeAuditScoringData scoringData)
        {
            ScoringData = scoringData;
        }
    }

    /// <summary>
    /// Represents scoring metrics for an attribute audit.
    /// </summary>
    public class PIQIAuditDataAttributeAuditScoringData
    {
        /// <summary>
        /// Gets the calculated attribute score.
        /// </summary>
        public int AttributeScore { get; }

        /// <summary>
        /// Gets the weighted attribute score.
        /// </summary>
        public int AttributeScoreWeighted { get; }

        /// <summary>
        /// Gets the number of critical failures for the attribute.
        /// </summary>
        public int AttributeCriticalFailureCount { get; }

        /// <summary>
        /// Gets the total successful checks for the attribute.
        /// </summary>
        public int AttributeNumerator { get; }

        /// <summary>
        /// Gets the total evaluated checks for the attribute.
        /// </summary>
        public int AttributeDenominator { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeAuditScoringData"/> class.
        /// </summary>
        public PIQIAuditDataAttributeAuditScoringData(int attributeScore, int attributeScoreWeighted, int attributeCriticalFailureCount, int attributeNumerator, int attributeDenominator)
        {
            AttributeScore = attributeScore;
            AttributeScoreWeighted = attributeScoreWeighted;
            AttributeCriticalFailureCount = attributeCriticalFailureCount;
            AttributeNumerator = attributeNumerator;
            AttributeDenominator = attributeDenominator;
        }
    }

    /// <summary>
    /// Represents an individual assessment item for an attribute audit.
    /// </summary>
    public class PIQIAuditDataAttributeAuditAssessmentItem
    {
        /// <summary>
        /// Gets the attribute mnemonic.
        /// </summary>
        public string AttributeMnemonic { get; }

        /// <summary>
        /// Gets the attribute name.
        /// </summary>
        public string AttributeName { get; }

        /// <summary>
        /// Gets the assessment name or type.
        /// </summary>
        public string Assessment { get; }

        /// <summary>
        /// Gets the impact or effect of the assessment result.
        /// </summary>
        public string Effect { get; }

        /// <summary>
        /// Gets the assessment status.
        /// </summary>
        public string Status { get; }

        /// <summary>
        /// Gets the reason associated with the assessment result.
        /// </summary>
        public string Reason { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIAuditDataAttributeAuditAssessmentItem"/> class.
        /// </summary>
        public PIQIAuditDataAttributeAuditAssessmentItem(string attributeMnemonic, string attributeName, string assessment, string effect, string status, string reason)
        {
            AttributeMnemonic = attributeMnemonic;
            AttributeName = attributeName;
            Assessment = assessment;
            Effect = effect;
            Status = status;
            Reason = reason;
        }
    }
}