namespace PIQI.Components.Models
{
    /// <summary>
    /// Represents a PIQI message and its associated evaluation context.
    /// </summary>
    public class PIQIMessage
    {
        #region Properties

        /// <summary>
        /// The request that initiated this PIQI message.
        /// </summary>
        public PIQIRequest PIQIRequest { get; set; }

        /// <summary>
        /// Reference data used during evaluation.
        /// </summary>
        public PIQIReferenceData RefData { get; set; }

        /// <summary>
        /// The message model that represents the structure and content of this message.
        /// </summary>
        public MessageModel MessageModel { get; set; }

        /// <summary>
        /// Gets or sets the evaluation manager responsible for executing and tracking evaluation processes.
        /// </summary>
        public EvaluationManager EvaluationManager { get; set; }

        /// <summary>
        /// Statistical results generated from processing this message.
        /// </summary>
        public StatResponse StatResponse { get; set; }

        /// <summary>
        /// Formatted statistical result for this message.
        /// </summary>
        public PIQIStatResponse FormattedStatResponse { get; set; }

        #endregion

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="PIQIMessage"/> class with the specified request.
        /// </summary>
        /// <param name="piqiRequest">The PIQI request associated with this message.</param>
        public PIQIMessage(PIQIRequest piqiRequest)
        {
            PIQIRequest = piqiRequest;
            EvaluationManager = new EvaluationManager();
        }

        #endregion

        #region Stat Methods

        /// <summary>
        /// Generates the statistical result for this PIQI message by processing all applicable PIQI SAMs.
        /// </summary>
        /// <returns>A <see cref="StatResponse"/> containing aggregated scoring, pass/fail, and informational results.</returns>
        public StatResponse GenerateStatResponse()
        {
            // Create a new stat result object
            StatResponse = new StatResponse();

            try  
            {
                // Process results for each scorable evaluation result
                foreach (var evaluationResult in EvaluationManager.EvaluationResultDict.Values)
                {
                    // Ignore conditional or dependent results
                    if (evaluationResult.IsConditional || evaluationResult.IsDependent) continue;

                    StatResponse.ProcessResult(evaluationResult, RefData);
                }

                // Calc classes
                foreach (EvaluationItem classItem in EvaluationManager.EvaluationItemDict.Values.Where(t => t.ItemType == EntityItemTypeEnum.Class))
                {
                    StatResponseClass statClass = new StatResponseClass(classItem.Entity.Mnemonic, classItem?.MessageItem?.ChildDict?.Count() ?? 0);
                    List<StatResponseElement>? elementResponseList = StatResponse.ElementDict?.Values?.Where(t => t.ClassMnemonic == statClass.ClassMnemonic).ToList();
                    if (elementResponseList != null)
                    {
                        statClass.Calc(elementResponseList);
                        StatResponse.ClassDict.Add(statClass.Key, statClass);
                    }
                }
            }
            catch
            {
                throw;
            }

            // Return the generated statistical result
            return StatResponse;
        }

        #endregion

        #region Audit Methods

        /// <summary>
        /// Generates a JSON-formatted audit result for the current PIQI message.
        /// </summary>
        /// <returns>A JSON string representing the audit result.</returns>
        public PIQIAuditResponse GenerateAuditResponse()
        {
            try
            {
                // Create base AuditData object
                PIQIAuditResponse auditResponse = new PIQIAuditResponse(MessageModel.Header.EntityModelMnemonic, MessageModel.Header.ContributorName, MessageModel.Header.DataSourceName, MessageModel.Header.ClientMessageID);

                // Add message-level audit info
                auditResponse.Audit = Audit_AddMessageInfo(FormattedStatResponse);

                // Process message model
                auditResponse.Root = Audit_ProcessRoot(EvaluationManager.RootItem);

                // Return
                return auditResponse;
            }
            catch
            {
                throw;
            }
        }
        public PIQIAuditResult Audit_AddMessageInfo(PIQIStatResponse statResponse)
        {
            try
            {
                PIQIAuditResult auditResult = new PIQIAuditResult(
                    statResponse.MessageResults.Numerator,
                    statResponse.MessageResults.Denominator,
                    (statResponse.MessageResults.Denominator > 0 ? ((int)((double)statResponse.MessageResults.Numerator / (double)statResponse.MessageResults.Denominator * 100)) : 0),
                    statResponse.MessageResults.WeightedNumerator,
                    statResponse.MessageResults.WeightedDenominator,
                    (statResponse.MessageResults.WeightedDenominator > 0 ? (int)((double)statResponse.MessageResults.WeightedNumerator / (double)statResponse.MessageResults.WeightedDenominator * 100) : 0),
                    statResponse.MessageResults.CriticalFailureCount
                );

                return auditResult;
            }
            catch
            {
                throw;
            }
        }

        #region Root Audit

        private PIQIAuditDataRoot Audit_ProcessRoot(EvaluationItem item)
        {
            PIQIAuditDataRoot auditDataRoot = new PIQIAuditDataRoot(item.Entity.FieldName);

            // Assert it's a root node.
            if (item.ItemType != EntityItemTypeEnum.Root)
                throw new ArgumentException();

            // The root node contains classes.
            if (item.ChildDict.Count > 0)
            {
                auditDataRoot.Classes = new List<PIQIAuditDataClass>();

                foreach (EvaluationItem child in item.ChildDict.Values)
                    auditDataRoot.Classes.Add(Audit_ProcessClass(child));
            }

            return auditDataRoot;
        }

        #endregion

        #region Class Audit

        private PIQIAuditDataClass Audit_ProcessClass(EvaluationItem item)
        {
            PIQIAuditDataClass auditDataClass = new PIQIAuditDataClass(item.Entity.FieldName);

            // Assert it's a class node.
            if (item.ItemType != EntityItemTypeEnum.Class)
                throw new ArgumentException();

            // The class node contains elements.
            if (item.ChildDict.Count > 0)
            {
                auditDataClass.Elements = new List<PIQIAuditDataElement>();
                foreach (EvaluationItem child in item.ChildDict.Values)
                    auditDataClass.Elements.Add(Audit_ProcessElement(child));
            }

            return auditDataClass;
        }

        #endregion

        #region Element Audit

        private PIQIAuditDataElement Audit_ProcessElement(EvaluationItem item)
        {
            PIQIAuditDataElement auditDataElement = new PIQIAuditDataElement();

            // Assert it's an element node.
            if (item.ItemType != EntityItemTypeEnum.Element)
                throw new ArgumentException();

            // The element node contains attributes.
            if (item.ChildDict.Values.Count > 0)
            {
                auditDataElement.Attributes = new List<PIQIAuditDataAttribute>();
                foreach (EvaluationItem child in item.ChildDict.Values)
                    auditDataElement.Attributes.Add(Audit_ProcessAttribute(child));

                auditDataElement.ElementAudit = Audit_AddElementAudit(item);
            }

            return auditDataElement;
        }

        private static PIQIAuditDataElementAudit? Audit_AddElementAudit(EvaluationItem elementItem)
        {
            // Get all results for this element, including all attribute results
            List<EvaluationResult> resultList = elementItem.CriteriaResultDict.Values.Where(t => t.IsScoring).ToList();
            foreach (EvaluationItem attrItem in elementItem.ChildDict.Values)
            {
                List<EvaluationResult> attrResultList = attrItem.CriteriaResultDict.Values.Where(t => t.IsScoring).ToList();
                if (attrResultList.Count > 0)
                    resultList.AddRange(attrResultList);
            }

            // Exit condition - no results to audit
            if (resultList.Count < 1)
                return null;

            // Calculate scoring 
            int denominator = resultList.Where(t => t.IsScoring && !t.EvalSkipped).Count();
            int weightedDenominator = resultList.Where(t => t.IsScoring && !t.EvalSkipped).Sum(t => t.Criterion.ScoringWeight);
            int numerator = resultList.Where(t => t.IsScoring && t.EvalPassed).Count();
            int weightedNumerator = resultList.Where(t => t.IsScoring && t.EvalPassed).Sum(t => t.Criterion.ScoringWeight);
            int score = 0;
            if (denominator > 0)
                score = (int)Math.Truncate((float)numerator / (float)denominator * (float)100);
            int weightedScore = 0;
            if (weightedDenominator > 0)
                weightedScore = (int)Math.Truncate((float)weightedNumerator / (float)weightedDenominator * (float)100);
            int criticalFailureCount = resultList.Where(t => t.IsScoring && t.IsCritical && t.EvalFailed).Count();

            // Create the Audit and scoringData nodes
            PIQIAuditDataElementAudit auditDataElementAudit = new PIQIAuditDataElementAudit(score, weightedScore, criticalFailureCount, numerator, denominator);

            return auditDataElementAudit;
        }

        #endregion

        #region Attribute Audit

        private PIQIAuditDataAttribute Audit_ProcessAttribute(EvaluationItem item)
        {
            PIQIAuditDataAttribute auditDataAttribute = new PIQIAuditDataAttribute(item.Entity.FieldName);

            // Assert it's an attribute node.
            if (item.ItemType != EntityItemTypeEnum.Attribute)
                throw new ArgumentException();

            // Attributes are a bit more involved.
            // First, print the data, based on datatype.
            if (item.HasMessageItem)
            {
                switch (item.Entity.EntityType.EntityTypeValue)
                {
                    case EntityDataTypeEnum.CC:
                        auditDataAttribute.Data = Audit_ProcessAttribute_CodeableConcept((CodeableConcept)item.MessageItem.MessageData);
                        break;
                    case EntityDataTypeEnum.OBSVAL:
                        auditDataAttribute.Data = Audit_ProcessAttribute_ObservationValue((Value)item.MessageItem.MessageData);
                        break;
                    case EntityDataTypeEnum.RV:
                        auditDataAttribute.Data = Audit_ProcessAttribute_ReferenceRange((ReferenceRange)item.MessageItem.MessageData);
                        break;
                    case EntityDataTypeEnum.ROOT:
                    case EntityDataTypeEnum.CLS:
                    case EntityDataTypeEnum.ELM:
                    case EntityDataTypeEnum.ATR:
                    default:
                        auditDataAttribute.Data = Audit_ProcessAttribute_Text(item.MessageItem.MessageData);
                        break;
                }
            }
            // Then do the audit.
            if (item.HasResults)
                auditDataAttribute.AttributeAudit = Audit_AddAttributeAudit(item);

            return auditDataAttribute;
        }

        private static PIQIAuditDataAttributeData_CodeableConcept Audit_ProcessAttribute_CodeableConcept(CodeableConcept concept)
        {
            List<PIQIAuditDataAttributeData_CodeableConceptCodings> codings = new List<PIQIAuditDataAttributeData_CodeableConceptCodings>();

            PIQIAuditDataAttributeData_CodeableConcept auditDataAttributeData_CodeableConcept = new PIQIAuditDataAttributeData_CodeableConcept(concept.Text);

            // List codings, if they exist
            if (concept.CodingList != null && concept.CodingList.Count > 0)
            {
                auditDataAttributeData_CodeableConcept.Codings = new List<PIQIAuditDataAttributeData_CodeableConceptCodings>();
                foreach (Coding coding in concept.CodingList)
                    auditDataAttributeData_CodeableConcept.Codings.Add(new PIQIAuditDataAttributeData_CodeableConceptCodings(coding.CodeSystem, coding.CodeValue, coding.CodeText));
            }

            return auditDataAttributeData_CodeableConcept;
        }

        private static PIQIAuditDataAttributeData_ObservationValue Audit_ProcessAttribute_ObservationValue(Value value)
        {
            // There will always be a text value
            PIQIAuditDataAttributeData_ObservationValue auditDataAttributeData_ObservationValue = new PIQIAuditDataAttributeData_ObservationValue(value.Text);

            // We might have a TypeCC, and said type (might?) have a CodeableConcept.
            if (value.TypeCC != null)
                auditDataAttributeData_ObservationValue.Type = Audit_ProcessAttribute_CodeableConcept(value.TypeCC);

            // Probably should add value cc here, even though we've never actually seen one

            return auditDataAttributeData_ObservationValue;
        }

        private static PIQIAuditDataAttributeData_ReferenceRange Audit_ProcessAttribute_ReferenceRange(ReferenceRange range)
        {
            return new PIQIAuditDataAttributeData_ReferenceRange(range.Text, range.LowValue, range.HighValue);
        }

        private static PIQIAuditDataAttributeData_Text Audit_ProcessAttribute_Text(BaseText item)
        {
            return new PIQIAuditDataAttributeData_Text(item.Text);
        }
        private PIQIAuditDataAttributeAudit? Audit_AddAttributeAudit(EvaluationItem data)
        {
            PIQIAuditDataAttributeAuditScoringData scoringData = Audit_AddAttributeAudit_ScoringData(data);
            PIQIAuditDataAttributeAudit auditDataAttributeAudit = new PIQIAuditDataAttributeAudit(scoringData);

            // Scoring and Informational assessments
            foreach (EvaluationResult result in data.CriteriaResultDict.Values.Where(t => t.IsScoring == true).OrderBy(t => t.SamDisplayName))
            {
                if (auditDataAttributeAudit.AssessmentItems == null)
                    auditDataAttributeAudit.AssessmentItems = new List<PIQIAuditDataAttributeAuditAssessmentItem>();

                auditDataAttributeAudit.AssessmentItems.Add(Audit_AddAttributeAudit_AssessmentItem(result, "Scoring"));
            }
            foreach (EvaluationResult result in data.CriteriaResultDict.Values.Where(t => t.IsInformational == true).OrderBy(t => t.SamDisplayName))
            {
                if (auditDataAttributeAudit.InformationalItems == null)
                    auditDataAttributeAudit.InformationalItems = new List<PIQIAuditDataAttributeAuditAssessmentItem>();

                auditDataAttributeAudit.InformationalItems.Add(Audit_AddAttributeAudit_AssessmentItem(result, "Informational"));
            }

            return auditDataAttributeAudit;
        }

        private static PIQIAuditDataAttributeAuditScoringData Audit_AddAttributeAudit_ScoringData(EvaluationItem data)
        {
            // Calculate scoring 
            List<EvaluationResult> resultList = data.CriteriaResultDict.Values.ToList();
            int denominator = resultList.Where(t => t.IsScoring && !t.EvalSkipped).Count();
            int weightedDenominator = resultList.Where(t => t.IsScoring && !t.EvalSkipped).Sum(t => t.Criterion.ScoringWeight);
            int numerator = resultList.Where(t => t.IsScoring && t.EvalPassed).Count();
            int weightedNumerator = resultList.Where(t => t.IsScoring && t.EvalPassed).Sum(t => t.Criterion.ScoringWeight);
            int score = 0;
            if (denominator > 0)
                score = (int)(Math.Truncate((float)numerator / (float)denominator * (float)100));
            int weightedScore = 0;
            if (weightedDenominator > 0)
                weightedScore = (int)(Math.Truncate((float)weightedNumerator / (float)weightedDenominator * (float)100));
            int criticalFailureCount = resultList.Where(t => t.IsScoring && t.IsCritical && t.EvalFailed).Count();

            return new PIQIAuditDataAttributeAuditScoringData(score, weightedScore, criticalFailureCount, numerator, denominator);
        }

        private PIQIAuditDataAttributeAuditAssessmentItem Audit_AddAttributeAudit_AssessmentItem(EvaluationResult result, string effect)
        {
            var reason = result.EvalPassed ? ""
                : result.EvalSkipped
                    ? result.Reason ?? RefData.GetSAM(result.SkipSamMnemonic ?? "")?.FailName ?? RefData.GetSAM(result.SkipSamMnemonic ?? "")?.Name
                    : result.Reason ?? (result.FailSamMnemonic == result.Criterion.SAMMnemonic ?
                        (result.Criterion.FailureNameOverride ?? result.Criterion.SamNameOverride ?? RefData.GetSAM(result.FailSamMnemonic ?? "")?.FailName ?? RefData.GetSAM(result.FailSamMnemonic ?? "")?.Name)
                        : (RefData.GetSAM(result.FailSamMnemonic ?? "")?.FailName ?? RefData.GetSAM(result.FailSamMnemonic ?? "")?.Name));

            return new PIQIAuditDataAttributeAuditAssessmentItem(
                result.EntityMnemonic,
                result.EntityName,
                result.SamDisplayName,
                effect,
                result.EvalSkipped ? "Skipped" : (result.EvalPassed ? "Passed" : "Failed"),
                reason
            );
        }

        #endregion

        #endregion

        #region Criteria 

        /// <summary>
        /// Retrieves all evaluation criteria that match the specified entity mnemonic from the reference data.
        /// </summary>
        /// <param name="entityMnemonic">
        /// The mnemonic identifier of the entity used to filter evaluation criteria.
        /// </param>
        /// <returns>
        /// A <see cref="List{EvaluationCriterion}"/> containing all criteria associated with the specified entity.
        /// Returns an empty list if no matching criteria are found.
        /// </returns>
        public List<EvaluationCriterion> GetCriteriaList(string entityMnemonic)
        {
            return RefData.EvaluationRubric.Criteria.Where(c => c.Entity.Equals(entityMnemonic)).ToList();
        }

        #endregion

    }
}
