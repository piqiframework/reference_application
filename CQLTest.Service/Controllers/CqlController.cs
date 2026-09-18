using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;

namespace CQLTest.Service;

/// <summary>
/// Thin proxy between the WinForms client and the Java CQL sidecar.
/// Forwards the raw JSON body unchanged to avoid any serialization round-trip issues
/// (C# PascalCase ↔ Java camelCase mismatches, enum value casing, etc.).
/// </summary>
[ApiController]
[Route("cql")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CqlController : ControllerBase
{
    private readonly HttpClient _sidecar;
    private readonly ILogger<CqlController> _logger;

    public CqlController(IHttpClientFactory factory, ILogger<CqlController> logger)
    {
        _sidecar = factory.CreateClient("sidecar");
        _logger  = logger;
    }

    /// <summary>Evaluate pre-compiled ELM JSON against patient context data.</summary>
    [HttpPost("evaluate")]
    public Task<ActionResult> Evaluate(CancellationToken ct)
        => ForwardRawAsync("/cql/evaluate", ct);

    /// <summary>Translate CQL text to ELM on the sidecar, then evaluate.</summary>
    [HttpPost("evaluate-cql")]
    public Task<ActionResult> EvaluateCql(CancellationToken ct)
        => ForwardRawAsync("/cql/evaluate-cql", ct);

    /// <summary>Translate CQL text to ELM JSON (no evaluation).</summary>
    [HttpPost("translate")]
    public Task<ActionResult> Translate(CancellationToken ct)
        => ForwardRawAsync("/cql/translate", ct);

    /// <summary>Proxy the sidecar health check.</summary>
    [HttpGet("health")]
    public async Task<ActionResult> Health(CancellationToken ct)
    {
        try
        {
            var response = await _sidecar.GetAsync("/actuator/health", ct);
            return response.IsSuccessStatusCode ? Ok(new { status = "UP" }) : StatusCode(503);
        }
        catch { return StatusCode(503); }
    }

    // ── helpers ───────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the raw request body and forwards it byte-for-byte to the sidecar,
    /// then streams the sidecar response back. No deserialization occurs.
    /// </summary>
    private async Task<ActionResult> ForwardRawAsync(string path, CancellationToken ct)
    {
        using var bodyContent = new StreamContent(Request.Body);
        bodyContent.Headers.ContentType = MediaTypeHeaderValue.Parse(
            string.IsNullOrWhiteSpace(Request.ContentType) ? "application/json" : Request.ContentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = bodyContent
        };
        using var response = await _sidecar.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("CQL sidecar returned {Status}: {Body}", response.StatusCode, body);
            return StatusCode((int)response.StatusCode, body);
        }

        Response.StatusCode = (int)response.StatusCode;
        Response.ContentType = response.Content.Headers.ContentType?.ToString() ?? "application/json";
        if (response.Content.Headers.ContentLength.HasValue)
        {
            Response.ContentLength = response.Content.Headers.ContentLength.Value;
        }

        await using var sidecarStream = await response.Content.ReadAsStreamAsync(ct);
        await sidecarStream.CopyToAsync(Response.Body, ct);
        return new EmptyResult();
    }
}
