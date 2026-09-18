using System.Text.Json.Serialization;

namespace CQLTest.Service;

// ── Shared request / response models ─────────────────────────────────────────
// These are the typed contracts used by CQLServiceClient (WinForms client and
// test project) to build and parse requests. The service itself does not
// deserialize them — it raw-forwards the JSON body directly to the Java sidecar.

public record CQLEvaluationRequest(
    string ElmJson,
    string LibraryId,
    string? LibraryVersion,
    List<string>? Expressions,
    string? ContextDataJson,
    Dictionary<string, object>? Parameters,
    string? ModelUri = null,
    FhirSettings? FhirSettings = null
);

public record CQLDirectRequest(
    string CQLText,
    List<string>? Expressions,
    string? ContextDataJson,
    Dictionary<string, object>? Parameters,
    string? ModelUri = null,
    FhirSettings? FhirSettings = null
);

public record CQLEvaluationResult(Dictionary<string, CQLExpressionResult> Results, List<string>? Errors);

public record CQLExpressionResult(object? Value, string? ValueType, string? Error);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FhirAuthType { None, Basic, Bearer, OAuth2 }

public record FhirSettings(
    string Url,
    FhirAuthType AuthType = FhirAuthType.None,
    string? Username = null,
    string? Password = null,
    string? Token = null,
    string? TokenUrl = null,
    string? ClientId = null,
    string? ClientSecret = null,
    string? Scope = null,
    bool SkipSslVerify = false
);

public enum eMeasureResult { Skip, Pass, Fail }
