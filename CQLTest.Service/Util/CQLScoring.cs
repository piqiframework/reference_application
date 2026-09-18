using System.Text.Json;

namespace CQLTest.Service;

/// <summary>
/// Shared scoring logic — maps raw CQL expression results to Pass / Fail / Skip.
/// </summary>
public static class CQLScoring
{
    public static eMeasureResult CalculateMeasureResult(
        Dictionary<string, CQLExpressionResult> components,
        FieldMappings? mapping = null)
    {
        mapping ??= new FieldMappings();

        bool Get(string key, bool fallback) =>
            components.TryGetValue(key, out var r) ? ToBool(r.Value, fallback) : fallback;

        if (!Get(mapping.InitialPopulation, true) || !Get(mapping.Denominator, false)) return eMeasureResult.Skip;
        if (Get(mapping.DenominatorExclusion, false)) return eMeasureResult.Skip;
        if (Get(mapping.DenominatorException, false)) return eMeasureResult.Skip;
        if (Get(mapping.NumeratorExclusion,   false)) return eMeasureResult.Fail;
        return Get(mapping.Numerator, false) ? eMeasureResult.Pass : eMeasureResult.Fail;
    }

    public static bool ToBool(object? value, bool fallback) => value switch
    {
        null    => fallback,
        bool b  => b,
        JsonElement { ValueKind: JsonValueKind.True  } => true,
        JsonElement { ValueKind: JsonValueKind.False } => false,
        JsonElement je => bool.TryParse(je.GetString(), out var p) ? p : fallback,
        string s       => bool.TryParse(s, out var p) ? p : fallback,
        _              => bool.TryParse(value.ToString(), out var p) ? p : fallback,
    };
}
