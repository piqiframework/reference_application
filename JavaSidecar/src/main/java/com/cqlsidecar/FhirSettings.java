package com.cqlsidecar;

/** FHIR terminology server connection settings, optionally supplied per-request. */
public class FhirSettings {

    /** FHIR R4 base URL, e.g. https://fhir.example.com/R4 */
    private String url;

    /**
     * Auth type: NONE, BASIC, BEARER, or OAUTH2 (case-insensitive).
     * Defaults to NONE when absent.
     */
    private String authType = "NONE";

    // ── Basic auth ──────────────────────────────────────────────────────────
    private String username;
    private String password;

    // ── Bearer / static token ───────────────────────────────────────────────
    private String token;

    // ── OAuth2 client credentials ───────────────────────────────────────────
    private String tokenUrl;
    private String clientId;
    private String clientSecret;
    private String scope;

    /** When true, skips TLS certificate validation (useful for self-signed certs). */
    private boolean skipSslVerify = false;

    public String getUrl()             { return url; }
    public void setUrl(String v)       { this.url = v; }

    public String getAuthType()        { return authType; }
    public void setAuthType(String v)  { this.authType = v != null ? v : "NONE"; }

    public String getUsername()        { return username; }
    public void setUsername(String v)  { this.username = v; }

    public String getPassword()        { return password; }
    public void setPassword(String v)  { this.password = v; }

    public String getToken()           { return token; }
    public void setToken(String v)     { this.token = v; }

    public String getTokenUrl()        { return tokenUrl; }
    public void setTokenUrl(String v)  { this.tokenUrl = v; }

    public String getClientId()        { return clientId; }
    public void setClientId(String v)  { this.clientId = v; }

    public String getClientSecret()    { return clientSecret; }
    public void setClientSecret(String v) { this.clientSecret = v; }

    public String getScope()           { return scope; }
    public void setScope(String v)     { this.scope = v; }

    public boolean isSkipSslVerify()            { return skipSslVerify; }
    public void setSkipSslVerify(boolean v)     { this.skipSslVerify = v; }
}
