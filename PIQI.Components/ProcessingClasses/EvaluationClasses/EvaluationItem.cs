using System.Text.Json.Serialization;

namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents a single item within an evaluation, including its associated entity, message data,
    /// child evaluation items, and the results of evaluation criteria.
    /// </summary>
    public class EvaluationItem
    {
        #region Properties

        /// <summary>
        /// Gets or sets the unique key identifying this evaluation item.
        /// </summary>
        public string Key { get; set; }

        /// <summary>
        /// Gets or sets the message model item associated with this evaluation item.
        /// This property is ignored during JSON serialization.
        /// </summary>
        [JsonIgnore]
        public MessageModelItem? MessageItem { get; set; }

        /// <summary>
        /// Gets or sets the entity this evaluation item represents.
        /// </summary>
        public Entity Entity { get; set; }

        /// <summary>
        /// Gets or sets the class entity if this item represents a class or lower hierarchy level.
        /// </summary>
        public Entity? ClassEntity { get; set; }

        /// <summary>
        /// Gets or sets the effective date associated with this evaluation item.
        /// </summary>
        public DateTime? EffectiveDate { get; set; }

        /// <summary>
        /// Gets or sets the model start date associated with this evaluation item.
        /// </summary>
        public DateTime? ModelStartDate { get; set; }

        /// <summary>
        /// Gets or sets the model end date associated with this evaluation item.
        /// </summary>
        public DateTime? ModelEndDate { get; set; } 

        /// <summary>
        /// Gets or sets the unit of measure text associated with this evaluation item.
        /// </summary>
        public string? UOMText { get; set; }

        /// <summary>
        /// Gets or sets the value text associated with this evaluation item.
        /// </summary>
        public string? ValueText { get; set; }

        /// <summary>
        /// Gets or sets the numeric value associated with this evaluation item.
        /// </summary>
        public float? ValueFloat { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic of the root entity for this evaluation item.
        /// </summary>
        public string RootEntityMnemonic { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic of the class entity if this item represents a class or lower.
        /// </summary>
        public string? ClassEntityMnemonic { get; set; }

        /// <summary>
        /// Gets or sets the mnemonic of the element entity if this item represents an element or lower.
        /// </summary>
        public string? ElementEntityMnemonic { get; set; }

        /// <summary>
        /// Gets or sets the sequence number of the element within its parent.
        /// </summary>
        public int? ElementSequence { get; set; }

        /// <summary>
        /// Gets the type of this evaluation item based on the entity's type.
        /// </summary>
        public EntityItemTypeEnum? ItemType
        {
            get
            {
                if (Entity.EntityType.EntityTypeValue == null)
                    return null;
                return (EntityItemTypeEnum)Math.Min((int)Entity.EntityType.EntityTypeValue, 4);
            }
        }

        /// <summary>
        /// Gets or sets the dictionary of child evaluation items.
        /// Keys are based on the message item's local key.
        /// </summary>
        public Dictionary<string, EvaluationItem> ChildDict { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of evaluation results for each criterion, keyed by SAM mnemonic.
        /// </summary>
        public Dictionary<string, EvaluationResult> CriteriaResultDict { get; set; }

        /// <summary>
        /// Gets or sets the dictionary of all evaluation results, including conditionals and non-skipped dependents.
        /// </summary>
        public Dictionary<string, EvaluationResult> FullResultDict { get; set; }

        /// <summary>
        /// Gets or sets the group state of this evaluation item.
        /// Values correspond to eGROUP_RESULT_STATE: None, All passed, Some passed, None passed. 
        /// </summary>
        public int GroupState { get; set; }

        /// <summary>
        /// Gets a value indicating whether this evaluation item has an associated message item.
        /// </summary>
        public bool HasMessageItem => (MessageItem != null);

        /// <summary>
        /// Gets a value indicating whether this evaluation item has any child items.
        /// </summary>
        public bool HasChildren => ChildDict.Values.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this evaluation item has any criterion results.
        /// </summary>
        public bool HasResults => CriteriaResultDict.Values.Count > 0;

        /// <summary>
        /// Gets a value indicating whether this evaluation item has text data.
        /// </summary>
        public bool HasText
        {
            get
            {
                return HasMessageItem
                    && MessageItem.MessageData != null
                    && !string.IsNullOrEmpty(MessageItem.MessageData.Text);
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has an effective date.
        /// </summary>
        public bool? HasEffectiveDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has a model start date.
        /// </summary>
        public bool? HasModelStartDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has a model end date.
        /// </summary>
        public bool? HasModelEndDate { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has unit of measure text.
        /// </summary>
        public bool? HasUOMText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has value text.
        /// </summary>
        public bool? HasValueText { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this evaluation item has a numeric value.
        /// </summary>
        public bool? HasValueFloat { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="EvaluationItem"/> class with the specified entity,
        /// parent evaluation item, and associated message model item.
        /// </summary>
        /// <param name="entity">The entity this evaluation item represents.</param>
        /// <param name="parentItem">The parent evaluation item, if any.</param>
        /// <param name="messageItem">The associated message model item, if any.</param>
        public EvaluationItem(Entity entity, EvaluationItem? parentItem, MessageModelItem? messageItem)
        {
            Entity = entity;
            MessageItem = messageItem;

            if (messageItem?.ElementSequence != null)
                ElementSequence = messageItem.ElementSequence;
            else if (parentItem?.ElementSequence != null)
                ElementSequence = parentItem.ElementSequence;

            Key = messageItem?.Key ?? $"{parentItem?.Key}|{(ItemType == EntityItemTypeEnum.Element ? $"{entity.Mnemonic}|{ElementSequence}" : entity.Mnemonic)}";

            ChildDict = new Dictionary<string, EvaluationItem>(); 
            CriteriaResultDict = new Dictionary<string, EvaluationResult>();
            FullResultDict = new Dictionary<string, EvaluationResult>();

            switch (ItemType)
            {
                case EntityItemTypeEnum.Root:
                    RootEntityMnemonic = entity.Mnemonic;
                    break;
                case EntityItemTypeEnum.Class:
                    RootEntityMnemonic = parentItem?.RootEntityMnemonic;
                    ClassEntityMnemonic = entity.Mnemonic;
                    ClassEntity = entity;
                    break;
                case EntityItemTypeEnum.Element:
                    RootEntityMnemonic = parentItem?.RootEntityMnemonic;
                    ClassEntityMnemonic = parentItem?.ClassEntityMnemonic;
                    ClassEntity = parentItem?.ClassEntity;
                    ElementEntityMnemonic = entity.Mnemonic;
                    break;
                case EntityItemTypeEnum.Attribute:
                    RootEntityMnemonic = parentItem?.RootEntityMnemonic;
                    ClassEntityMnemonic = parentItem?.ClassEntityMnemonic;
                    ClassEntity = parentItem?.ClassEntity;
                    ElementEntityMnemonic = parentItem?.ElementEntityMnemonic;
                    break;
            }
        }

        #endregion

        #region Put/Get Methods

        /// <summary>
        /// Adds a child evaluation item to this item's child dictionary.
        /// </summary>
        /// <param name="childItem">The child evaluation item to add.</param>
        public void AddChildItem(EvaluationItem childItem)
        {
            string key = childItem.ElementSequence != null ? $"{childItem.Entity.Mnemonic}|{childItem.ElementSequence}" : childItem.Entity.Mnemonic;
            ChildDict.Add(key, childItem);
        }

        /// <summary>
        /// Retrieves a child evaluation item by its key.
        /// </summary>
        /// <param name="childKey">The key of the child item to retrieve.</param>
        /// <returns>The child <see cref="EvaluationItem"/> if found and this is an Element type; otherwise, null.</returns>
        public EvaluationItem? GetChildItem(string childKey)
        {
             if (ItemType == EntityItemTypeEnum.Element && ChildDict.ContainsKey(childKey))
                return ChildDict[childKey];
            return null;
        }

        /// <summary>
        /// Adds a new criterion result for this evaluation item at the time of creation.
        /// </summary>
        /// <param name="criterion">The evaluation criterion to apply.</param>
        /// <param name="sam">The SAM object used for evaluation.</param>
        /// <returns>The created <see cref="EvaluationResult"/>.</returns>
        public EvaluationResult AddCriterionResult(EvaluationCriterion criterion, SAM sam)
        {
            EvaluationResult result = new EvaluationResult(this, criterion, sam, false, false);
            CriteriaResultDict.Add($"{sam.Mnemonic}.{criterion.Sequence}", result);
            return result;
        }

        /// <summary>
        /// Adds an evaluation result to the full result dictionary, including non-skipped dependent results.
        /// </summary>
        /// <param name="result">The evaluation result to add.</param>
        public void AddFullResult(EvaluationResult result)
        {
            if (!result.EvalSkipped)
                FullResultDict.Add($"{result.Sam.Mnemonic}.{result.Criterion.Sequence}", result);
        }

        #endregion

        #region Data Role Methods

        #region Get Methods
        /// <summary>
        /// Gets the effective date from the element's child attribute with the EFFECTIVE_DATETIME role.
        /// Also sets the <see cref="EffectiveDate"/> and <see cref="HasEffectiveDate"/> properties.
        /// </summary>
        /// <returns>The effective date as a <see cref="DateTime"/>, or null if the attribute does not exist or cannot be parsed.</returns>
        public DateTime? GetEffectiveDate()
        {
            BaseText? textObject = GetSimpleByRole(RoleTypeEnum.EFFECTIVE_DATETIME);
            if (textObject == null)
            {
                HasEffectiveDate = false;
                return null;
            }
            EffectiveDate = textObject.DateTimeValue();
            HasEffectiveDate = EffectiveDate != null ? true : false;
            return textObject.DateTimeValue();
        }

        /// <summary>
        /// Gets the start date from the element's child attribute with the START_DATETIME role.
        /// Also sets the <see cref="HasModelStartDate"/> property.
        /// </summary>
        /// <returns>The start date as a <see cref="DateTime"/>, or null if the attribute does not exist or cannot be parsed.</returns>
        public DateTime? GetStartDate()
        {
            BaseText? textObject = GetSimpleByRole(RoleTypeEnum.START_DATETIME);
            if (textObject == null)
            {
                HasModelStartDate = false;
                return null;
            }
            HasModelStartDate = textObject.DateTimeValue() != null ? true : false;
            return textObject.DateTimeValue();
        }

        /// <summary>
        /// Gets the end date from the element's child attribute with the END_DATETIME role.
        /// Also sets the <see cref="HasModelEndDate"/> property.
        /// </summary>
        /// <returns>The end date as a <see cref="DateTime"/>, or null if the attribute does not exist or cannot be parsed.</returns>
        public DateTime? GetEndDate()
        {
            BaseText? textObject = GetSimpleByRole(RoleTypeEnum.END_DATETIME);
            if (textObject == null)
            {
                HasModelEndDate = false;
                return null;
            }
            HasModelEndDate = textObject.DateTimeValue() != null ? true : false;
            return textObject.DateTimeValue();
        }
        /// <summary>
        /// Gets the primary unit of measure from the element's child attribute with the PRIMARY_UOM role.
        /// Also sets the <see cref="UOMText"/> and <see cref="HasUOMText"/> properties.
        /// </summary>
        /// <returns>
        /// The code value from the first complete coding if available; otherwise, the text from the codeable concept.
        /// Returns null if the attribute does not exist.
        /// </returns>
        public string? GetPrimaryUOM()
        {
            CodeableConcept conceptObject = GetConceptByRole(RoleTypeEnum.PRIMARY_UOM);
            if (conceptObject == null)
            {
                HasUOMText = false;
                return null;
            }
            Coding? coding = conceptObject.CodingList?.Where(t => t.IsComplete).FirstOrDefault();
            string returnText = (coding == null ? conceptObject.Text : coding.CodeValue);
            UOMText = returnText;
            HasUOMText = UOMText != null? true : false;
            return returnText;
        }

        /// <summary>
        /// Gets the primary value as text from the element's value attribute with the PRIMARY_VALUE role.
        /// Also sets the <see cref="ValueText"/> and <see cref="HasValueText"/> properties.
        /// </summary>
        /// <returns>The primary value as a string, or null if the value attribute does not exist.</returns>
        public string? GetPrimaryValue()
        {
            Value? valueObject = GetValueByRole(RoleTypeEnum.PRIMARY_VALUE);
            if (valueObject == null)
            {
                HasValueText = false;
                return null;
            }
            ValueText = valueObject.Text;
            HasValueText = ValueText != null && ValueText != string.Empty ? true : false;
            return valueObject.Text;
        }

        /// <summary>
        /// Gets the primary value as a float from the element's value attribute with the PRIMARY_VALUE role.
        /// </summary>
        /// <returns>
        /// The primary value converted to a float, or null if:
        /// - The value does not exist
        /// - The value cannot be parsed as a float
        /// - The value parses as zero but does not contain the digit '0' in its text representation
        /// </returns>
        public float? GetPrimaryValueAsFloat()
        {
            // Get our observation value
            Value v = GetValueByRole(RoleTypeEnum.PRIMARY_VALUE);
            if (v == null)
            {
                HasValueFloat = false;
                return null;
            }

            // Convert to float
            float f;
            bool ret = float.TryParse(v.Text, out f);

            // If it's 0 we check to see if there's any evidence that it's actually a float
            if (!ret)
            {
                HasValueFloat = false;
                return null;
            }
            if (f == 0 && v.Text.IndexOf("0") < 0)
            {
                HasValueFloat = false;
                return null;
            }
            ValueFloat = f;
            HasValueFloat = ValueFloat != null ? true : false;
            return f;
        }
        #endregion

        /// <summary>
        /// Gets a BaseText object from a child attribute identified by its role type.
        /// </summary>
        /// <param name="role">The role type to search for.</param>
        /// <param name="sequence">Optional sequence number to override the element's sequence.</param>
        /// <returns>The <see cref="BaseText"/> object if found; otherwise, null.</returns>
        public BaseText? GetSimpleByRole(RoleTypeEnum role, int? sequence = null) 
        {
            if (ItemType != EntityItemTypeEnum.Element) return null;
            var roleItem = MessageItem?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role);
            if (roleItem == null) return null; 

            string? entityMnemonic = ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role)?.AttributeMnemonic;
            if (entityMnemonic == null) return null;
            var key = $"{entityMnemonic}|{(sequence != null? sequence : ElementSequence)}";
            EvaluationItem? item = GetChildItem(key); 
            if (item == null || !item.HasMessageItem || item.MessageItem?.MessageData == null) return null;
            if (item.MessageItem.MessageData is BaseText == false) return null;

            return (BaseText)item.MessageItem.MessageData;
        }

        /// <summary>
        /// Gets a CodeableConcept object from a child attribute identified by its role type.
        /// </summary>
        /// <param name="role">The role type to search for.</param>
        /// <param name="sequence">Optional sequence number to override the element's sequence.</param>
        /// <returns>The <see cref="CodeableConcept"/> object if found; otherwise, null.</returns>
        public CodeableConcept? GetConceptByRole(RoleTypeEnum role, int? sequence = null)
        {
            if (ItemType != EntityItemTypeEnum.Element) return null;
            var roleItem = MessageItem?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role);
            if (roleItem == null) return null;

            string? entityMnemonic = ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role)?.AttributeMnemonic;
            if (entityMnemonic == null) return null;
            var key = $"{entityMnemonic}|{(sequence != null ? sequence : ElementSequence)}";
            EvaluationItem? item = GetChildItem(key);
            if (item == null || !item.HasMessageItem || item.MessageItem?.MessageData == null) return null;
            if (item.MessageItem.MessageData is CodeableConcept == false) return null;

            return (CodeableConcept)item.MessageItem.MessageData;
        }

        /// <summary>
        /// Gets a Value object from a child attribute identified by its role type.
        /// </summary>
        /// <param name="role">The role type to search for.</param>
        /// <param name="sequence">Optional sequence number to override the element's sequence.</param>
        /// <returns>The <see cref="Value"/> object if found; otherwise, null.</returns>
        public Value? GetValueByRole(RoleTypeEnum role, int? sequence = null)
        {
            if (ItemType != EntityItemTypeEnum.Element) return null;
            var roleItem = MessageItem?.ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role);
            if (roleItem == null) return null;

            string? entityMnemonic = ClassEntity?.Roles?.FirstOrDefault(r => r.RoleTypeMnemonic == role)?.AttributeMnemonic;
            if (entityMnemonic == null) return null;
            var key = $"{entityMnemonic}|{(sequence != null ? sequence : ElementSequence)}";
            EvaluationItem? item = GetChildItem(key);
            if (item == null || !item.HasMessageItem || item.MessageItem?.MessageData == null) return null;
            if (item.MessageItem.MessageData is Value == false) return null;

            return (Value)item.MessageItem.MessageData;
        }

        #endregion
    }
}