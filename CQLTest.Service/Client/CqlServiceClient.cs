using System.Net.Http.Json;
using System.Text.Json;

namespace CQLTest.Service;

/// <summary>
/// Thin HTTP client for the CQLTest.Service web API.
/// Used by the WinForms client and test project to call the service.
/// </summary>
public class CQLServiceClient(HttpClient http)
{
    // The .NET service forwards requests raw to the Java sidecar, which expects camelCase.
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNamingPolicy        = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<CQLEvaluationResult> EvaluateAsync(CQLEvaluationRequest request, CancellationToken ct = default)
        => await PostAsync<CQLEvaluationResult>("/cql/evaluate", request, ct);

    public async Task<CQLEvaluationResult> EvaluateCqlAsync(CQLDirectRequest request, CancellationToken ct = default)
        => await PostAsync<CQLEvaluationResult>("/cql/evaluate-cql", request, ct);

    public async Task<string> TranslateAsync(string cqlSource, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("/cql/translate", new { cqlSource }, _jsonOpts, ct);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(ct);
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try { return (await http.GetAsync("/cql/health", ct)).IsSuccessStatusCode; }
        catch { return false; }
    }

    private async Task<T> PostAsync<T>(string path, object request, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(path, request, _jsonOpts, ct);
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException(
                $"CQL service returned {(int)response.StatusCode}: {error}");
        }
        return await response.Content.ReadFromJsonAsync<T>(_jsonOpts, ct)
               ?? throw new InvalidOperationException("Empty response from CQL service.");
    }
}
