using Newtonsoft.Json.Linq;
using PIQI.Components.Models;
using PIQI.Components.SAMs;
using PIQI.Components.Services;
using System.Text.Json.Nodes;
using CQLTest.Service;

namespace PIQI_Engine.Server.Engines.SAMs
{
    /// <summary>
    /// SAM (Semantic Assessment Module) that evaluates patient data against Clinical Quality Language (CQL) expressions.
    /// This evaluator executes CQL libraries to assess clinical quality measures and determine pass/fail/skip outcomes.
    /// </summary>
    public class SAM_CQLEvaluator : SAMBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SAM_CQLEvaluator"/> class.
        /// </summary>
        /// <param name="sam">The parent <see cref="SAM"/> object providing configuration and context.</param>
        /// <param name="samService">
        /// An implementation of <see cref="SAMService"/> used to access reference data, CQL service, and make FHIR API calls.
        /// </param>
        public SAM_CQLEvaluator(SAM sam, SAMService samService)
            : base(sam, samService) { }

        /// <summary>
        /// Evaluates patient data against a CQL library to determine clinical quality measure outcomes.
        /// </summary>
        /// <param name="request">
        /// The <see cref="PIQISAMRequest"/> containing:
        /// <list type="bullet">
        ///   <item><description>The <see cref="PIQISAMRequest.EvaluationObject"/> containing the patient data in FHIR format.</description></item>
        ///   <item><description>Parameter "CQL_LIBRARY_MNEMONIC" - The mnemonic identifying which CQL library to execute.</description></item>
        ///   <item><description>Parameter "MEASUREMENT_PERIOD" (optional) - Date range in format "yyyy-MM-dd|yyyy-MM-dd" for measure evaluation.</description></item>
        /// </list>
        /// </param>
        /// <returns>
        /// A <see cref="Task{PIQISAMResponse}"/> representing the asynchronous evaluation result.
        /// The response indicates:
        /// <list type="bullet">
        ///   <item><description><c>Passed</c> if the CQL measure evaluation returns a passing result.</description></item>
        ///   <item><description><c>Failed</c> if the CQL measure evaluation returns a failing result.</description></item>
        ///   <item><description><c>Skipped</c> if CQL service is unavailable, required parameters are missing, or evaluation returns indeterminate results.</description></item>
        ///   <item><description><c>Errored</c> if an exception occurs during evaluation.</description></item>
        /// </list>
        /// </returns>
        public override async Task<PIQISAMResponse> EvaluateAsync(PIQISAMRequest request)
        {
            PIQISAMResponse result = new(); 
            bool passed = false;

            try
            {
                // Verify that the CQL evaluator is configured 
                if (_SAMService?.CqlService == null) 
                    return result.Skip("CQL evaluator is not configured.");

                // Get the patient data as JSON from the request object
                // Set the message model item
                EvaluationItem evaluationItem = (EvaluationItem)request.EvaluationObject;
                MessageModelItem? messageItem = evaluationItem?.MessageItem;
                if (string.IsNullOrEmpty(messageItem?.MessageText))
                    return result.Skip("Patient data is missing or invalid.");
          
                // Get the CQL library mnemonic from the request parameters  
                string cqlLib = request.GetParameterValue("CQL_LIBRARY_MNEMONIC");
                if (string.IsNullOrEmpty(cqlLib))
                    return result.Skip("CQL library mnemonic is missing or invalid.");

                // Get the CQL library JSON from the SAM service
                var cqlItem = _SAMService?.Message?.RefData?.GetCQL(cqlLib);
                var cql = cqlItem?.CQLText;

                if (string.IsNullOrEmpty(cql))
                    return result.Skip("CQL is missing or invalid.");

                // Get the measurement period from the request parameters (optional)
                Dictionary<string, object?>? parameters = null;
                string measurementPeriod = request.GetParameterValue("MEASUREMENT_PERIOD");
                if (!string.IsNullOrEmpty(measurementPeriod))
                {
                    // Validate measurement period format (should be "date1|date2")
                    string[] parts = measurementPeriod.Split('|');
                    if (parts.Length != 2)
                        return result.Skip("Measurement period must contain exactly two dates separated by a '|'.");

                    if (!DateTime.TryParseExact(parts[0], "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime startDate))
                        return result.Skip($"Invalid start date format: '{parts[0]}' (expected yyyy-MM-dd)");

                    if (!DateTime.TryParseExact(parts[1], "yyyy-MM-dd", null,
                        System.Globalization.DateTimeStyles.None, out DateTime endDate))
                        return result.Skip($"Invalid end date format: '{parts[1]}' (expected yyyy-MM-dd)");

                    var measurementPeriodInterval = new Dictionary<string, string> 
                    {
                        ["start"] = startDate.ToString("yyyy-MM-dd"),
                        ["end"] = endDate.ToString("yyyy-MM-dd")
                    };
                    parameters = new Dictionary<string, object?> { { "Measurement Period", measurementPeriodInterval } };
                }

                var contextData = BuildContextData(messageItem.MessageText);

                var cqlRequest = new CQLDirectRequest(
                    CQLText: cql,
                    Expressions: null,
                    ContextDataJson: contextData,
                    Parameters: parameters,
                    FhirSettings: null
                );

                // Await the result synchronously (SAM framework is synchronous)   
                var evalResult = _SAMService?.CqlService.EvaluateCqlAsync(cqlRequest).GetAwaiter().GetResult();
                if (evalResult == null) return result.Skip("CQL evaluation returned null result.");

                // Determine pass/fail based on evaluation results
                var evalMeasureResult = CalculateMeasureResult(evalResult.Results, cqlItem?.FieldMappings);

                if (evalMeasureResult == eMeasureResult.Pass)
                    passed = true;
                else if (evalMeasureResult == eMeasureResult.Fail)
                    passed = false;
                else
                    return result.Skip("CQL evaluation returned an indeterminate result.");  

                result.Done(passed); 
            }
            catch (Exception ex) {
              
                result.Error(ex.Message);
            } 
            return result; 
        } 

        /// <summary>
        /// Calculates the overall measure result by analyzing CQL expression evaluation results.
        /// Delegates to <see cref="CQLScoring.CalculateMeasureResult"/> for the actual calculation logic.
        /// </summary>
        /// <param name="components">Dictionary of CQL expression names to their evaluation results.</param>
        /// <param name="mapping">Optional field mappings to determine which expressions represent numerator, denominator, and exclusions.</param>
        /// <returns>
        /// An <see cref="eMeasureResult"/> indicating:
        /// <list type="bullet">
        ///   <item><description><see cref="eMeasureResult.Pass"/> if the measure criteria are met.</description></item>
        ///   <item><description><see cref="eMeasureResult.Fail"/> if the measure criteria are not met.</description></item>
        ///   <item><description><see cref="eMeasureResult.NotApplicable"/> if the measure does not apply to this patient.</description></item>
        /// </list>
        /// </returns>
        private static eMeasureResult CalculateMeasureResult(
                    Dictionary<string, CQLExpressionResult> components,
                    FieldMappings? mapping = null)
                    => CQLScoring.CalculateMeasureResult(components, mapping);

        /// <summary>
        /// Builds the FHIR context data structure required by the CQL evaluator from patient JSON.
        /// Extracts the patient resource from the PIQI message and wraps it in the expected context format.
        /// </summary>
        /// <param name="patientJson">JSON string containing the PIQI message with embedded patient FHIR resource.</param>
        /// <returns>JSON string containing the patient resource wrapped in a "Patient" context array.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the PIQI file does not contain a 'patient' property.</exception>
        private string BuildContextData(string patientJson)
        {
            var piqi = JsonNode.Parse(patientJson)!;
            var patient = piqi["patient"]
                ?? throw new InvalidOperationException("PIQI file has no 'patient' property.");
            return new JsonObject { ["Patient"] = new JsonArray(patient.DeepClone()) }.ToJsonString();
        }

        /// <summary>
        /// Gets the valid static mnemonic identifiers for this SAM implementation.
        /// The first value is treated as the primary mnemonic.
        /// </summary>
        public static IReadOnlyList<string> StaticMnemonics => 
        [
            "CQL_EVALUATOR",
            "CQL_ACIG_EVALUATOR", 
            "CQL_AVI_EVALUATOR", 
            "CQL_PCI_EVALUATOR", 
            "CQL_PSI_EVALUATOR",
            "CQL_PTI_EVALUATOR",
            "CQL_AVU_EVALUATOR",
            "CQL_ACIV_EVALUATOR",
            "CQL_CO_EVALUATOR",
            "CQL_CIM_EVALUATOR",
            "CQL_ACIF_EVALUATOR",
            "CQL_AVM_EVALUATOR"
        ];

        /// <summary>
        /// Gets the static mnemonic identifier for this SAM implementation.
        /// Maintained for backward compatibility with workers that expose a single mnemonic.
        /// Used for registering and referencing this SAM type in the system.
        /// </summary>
        public static string StaticMnemonic => StaticMnemonics[0];

        /// <summary>
        /// Gets the instance mnemonic identifier for this SAM.
        /// Returns the static mnemonic "CQL_EVALUATOR".
        /// </summary>
        public override string Mnemonic => StaticMnemonic;
    }
} 
