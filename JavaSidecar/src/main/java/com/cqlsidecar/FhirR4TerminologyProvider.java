package com.cqlsidecar;

import java.net.URI;
import java.net.URLEncoder;
import java.net.http.HttpClient;
import java.net.http.HttpRequest;
import java.net.http.HttpResponse;
import java.nio.charset.StandardCharsets;
import java.security.SecureRandom;
import java.security.cert.X509Certificate;
import java.time.Duration;
import java.time.Instant;
import java.util.ArrayList;
import java.util.Base64;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import javax.net.ssl.SSLContext;
import javax.net.ssl.TrustManager;
import javax.net.ssl.X509TrustManager;

import org.opencds.cqf.cql.engine.runtime.Code;
import org.opencds.cqf.cql.engine.terminology.CodeSystemInfo;
import org.opencds.cqf.cql.engine.terminology.TerminologyProvider;
import org.opencds.cqf.cql.engine.terminology.ValueSetInfo;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.github.benmanes.caffeine.cache.Cache;
import com.github.benmanes.caffeine.cache.Caffeine;

/**
 * Terminology provider with FHIR R4 server integration and Caffeine-backed caches.
 */
@Component
public class FhirR4TerminologyProvider implements TerminologyProvider {

    private static final Logger log = LoggerFactory.getLogger(FhirR4TerminologyProvider.class);

    private final String fhirBaseUrl;
    private final String authType;
    private final String username;
    private final String password;
    private final String staticToken;
    private final String tokenUrl;
    private final String clientId;
    private final String clientSecret;
    private final String scope;
    private final HttpClient httpClient;
    private final ObjectMapper objectMapper = new ObjectMapper();

    /** Value-set expansion cache: valueSetId → list of codes. */
    private final Cache<String, List<Code>> cache;
    /** O(1) membership cache built from expanded value-set codes. */
    private final Cache<String, ValueSetMembership> membershipCache;

    private volatile String oauth2Token;
    private volatile Instant oauth2TokenExpiry = Instant.EPOCH;
    private final Object tokenLock = new Object();

    private volatile boolean capabilitiesLoaded = false;
    private volatile boolean supportsValidateCode = false;
    private volatile boolean supportsExpand = false;
    private final Object capLock = new Object();

    /** Cache for validate-code results: "valueSetId:system:code" -> boolean result. */
    private final Cache<String, Boolean> validateCodeCache;

    @Autowired
    public FhirR4TerminologyProvider(
            @Value("${cql.terminology.fhir-base-url:}") String fhirBaseUrl,
            @Value("${cql.terminology.skip-ssl-verify:false}") boolean skipSslVerify,
            @Value("${cql.terminology.valueset-cache-max-weight:500000}") long valueSetCacheMaxWeight,
            @Value("${cql.terminology.membership-cache-max-weight:1000000}") long membershipCacheMaxWeight,
            @Value("${cql.terminology.valueset-cache-ttl-minutes:30}") int valueSetCacheTtlMinutes,
            @Value("${cql.terminology.validate-cache-max-size:200000}") long validateCacheMaxSize,
            @Value("${cql.terminology.validate-cache-ttl-minutes:20}") int validateCacheTtlMinutes) {
        this(normalise(fhirBaseUrl), "NONE", null, null, null, null, null, null, null,
                skipSslVerify, valueSetCacheMaxWeight, membershipCacheMaxWeight,
                Duration.ofMinutes(valueSetCacheTtlMinutes), validateCacheMaxSize,
                Duration.ofMinutes(validateCacheTtlMinutes));
    }

    public static FhirR4TerminologyProvider create(FhirSettings s) {
        String type = s.getAuthType() != null ? s.getAuthType().toUpperCase() : "NONE";
        // Per-request providers are ephemeral — disable the validate-code cache (size=0).
        return new FhirR4TerminologyProvider(
                normalise(s.getUrl()), type,
                s.getUsername(), s.getPassword(), s.getToken(),
                s.getTokenUrl(), s.getClientId(), s.getClientSecret(), s.getScope(),
                s.isSkipSslVerify(), 0, 0, Duration.ofMinutes(5), 0, Duration.ofMinutes(5));
    }

    private FhirR4TerminologyProvider(String fhirBaseUrl, String authType,
            String username, String password, String staticToken,
            String tokenUrl, String clientId, String clientSecret, String scope,
            boolean skipSslVerify, long valueSetCacheMaxWeight, long membershipCacheMaxWeight,
            Duration valueSetCacheTtl, long validateCacheMaxSize, Duration validateCacheTtl) {
        this.fhirBaseUrl = fhirBaseUrl;
        this.authType = authType;
        this.username = username;
        this.password = password;
        this.staticToken = staticToken;
        this.tokenUrl = tokenUrl;
        this.clientId = clientId;
        this.clientSecret = clientSecret;
        this.scope = scope;
        this.httpClient = buildHttpClient(skipSslVerify);
        this.cache = buildValueSetCache(valueSetCacheMaxWeight, valueSetCacheTtl);
        this.membershipCache = buildMembershipCache(membershipCacheMaxWeight, valueSetCacheTtl);
        this.validateCodeCache = buildValidateCodeCache(validateCacheMaxSize, validateCacheTtl);
    }

    private static HttpClient buildHttpClient(boolean skipSslVerify) {
        HttpClient.Builder builder = HttpClient.newBuilder()
                .connectTimeout(Duration.ofSeconds(10L));
        if (skipSslVerify) {
            try {
                SSLContext sc = SSLContext.getInstance("TLS");
                sc.init(null, new TrustManager[]{new X509TrustManager() {
                    @Override public void checkClientTrusted(X509Certificate[] c, String a) {}
                    @Override public void checkServerTrusted(X509Certificate[] c, String a) {}
                    @Override public X509Certificate[] getAcceptedIssuers() { return new X509Certificate[0]; }
                }}, new SecureRandom());
                builder.sslContext(sc);
                log.warn("SSL certificate verification disabled for FHIR terminology provider");
            } catch (Exception e) {
                log.error("Failed to create trust-all SSL context: {}", e.getMessage());
            }
        }
        return builder.build();
    }

    @Override
    public Iterable<Code> expand(ValueSetInfo valueSet) {
        if (fhirBaseUrl == null) {
            log.warn("No FHIR server configured. Value set '{}' treated as empty.", valueSet.getId());
            return List.of();
        }
        List<Code> cached = cache.getIfPresent(valueSet.getId());
        if (cached != null) return cached;
        try {
            List<Code> result = fetchExpansion(valueSet.getId());
            // Only cache successful (non-empty) expansions so transient errors don't poison the cache.
            if (!result.isEmpty()) {
                cache.put(valueSet.getId(), result);
                membershipCache.put(valueSet.getId(), buildMembership(result));
            }
            return result;
        } catch (Exception e) {
            log.error("Failed to expand value set '{}': {}", valueSet.getId(), e.getMessage());
            return List.of();
        }
    }

    @Override
    public boolean in(Code code, ValueSetInfo valueSet) {
        if (fhirBaseUrl == null) return false;
        ensureCapabilitiesLoaded();

        if (supportsValidateCode) {
            String codeSystem = code.getSystem() != null ? code.getSystem() : "";
            String cacheKey = valueSet.getId() + ":" + codeSystem + ":" + code.getCode();

            Boolean cachedResult = validateCodeCache.getIfPresent(cacheKey);
            if (cachedResult != null) {
                log.debug("Using cached $validate-code result for vs={}, code={}", valueSet.getId(), code.getCode());
                return cachedResult;
            }

            Boolean result = tryValidateCode(code, valueSet);
            if (result != null) {
                validateCodeCache.put(cacheKey, result);
                log.debug("Cached $validate-code result for vs={}, code={}: {}",
                        valueSet.getId(), code.getCode(), result);
                return result;
            }
        }

        // Fall back to expansion if $validate-code is unsupported or failed.
        ValueSetMembership membership = membershipCache.getIfPresent(valueSet.getId());
        if (membership == null) {
            List<Code> expanded = (List<Code>) expand(valueSet);
            log.debug("Expansion check: {} codes for vs={}", expanded.size(), valueSet.getId());
            if (!expanded.isEmpty()) {
                membership = buildMembership(expanded);
                membershipCache.put(valueSet.getId(), membership);
            }
        }

        if (membership != null && membership.contains(code)) {
            log.debug("Expansion match: code={} system={} in vs={}", code.getCode(), code.getSystem(), valueSet.getId());
            return true;
        }

        log.debug("No match for code={} system={} in vs={}", code.getCode(), code.getSystem(), valueSet.getId());
        return false;
    }

    private void ensureCapabilitiesLoaded() {
        if (capabilitiesLoaded) return;
        synchronized (capLock) {
            if (capabilitiesLoaded) return;
            if (fhirBaseUrl == null) {
                capabilitiesLoaded = true;
                return;
            }
            try {
                String metaUrl = fhirBaseUrl + "/metadata?_summary=true";
                log.info("Fetching FHIR capability statement: {}", metaUrl);
                HttpResponse<String> resp = httpClient.send(
                        buildGetRequest(metaUrl), HttpResponse.BodyHandlers.ofString());
                if (resp.statusCode() == 200) {
                    JsonNode root = objectMapper.readTree(resp.body());
                    supportsValidateCode = hasOperation(root, "validate-code");
                    supportsExpand = hasOperation(root, "expand");
                } else {
                    log.warn("Capability statement returned HTTP {} -- assuming no terminology ops", resp.statusCode());
                }
            } catch (Exception e) {
                log.warn("Capability check failed: {} -- assuming no terminology ops", e.getMessage());
            }
            log.info("FHIR capabilities: $validate-code={}, $expand={}", supportsValidateCode, supportsExpand);
            capabilitiesLoaded = true;
        }
    }

    private boolean hasOperation(JsonNode capability, String opName) {
        JsonNode restArray = capability.path("rest");
        if (!restArray.isArray()) return false;
        for (JsonNode rest : restArray) {
            for (JsonNode resource : rest.path("resource")) {
                if ("ValueSet".equals(resource.path("type").asText(null))
                        && operationInArray(resource.path("operation"), opName)) {
                    return true;
                }
            }
            if (operationInArray(rest.path("operation"), opName)) return true;
        }
        return false;
    }

    private boolean operationInArray(JsonNode ops, String opName) {
        if (!ops.isArray()) return false;
        for (JsonNode op : ops) {
            if (opName.equals(op.path("name").asText(null))) return true;
        }
        return false;
    }

    private Boolean tryValidateCode(Code code, ValueSetInfo valueSet) {
        try {
            String vsUrl = normaliseValueSetUrl(valueSet.getId());
            String codeSystem = code.getSystem() != null ? code.getSystem() : "";
            String requestUrl = fhirBaseUrl + "/ValueSet/$validate-code"
                    + "?url=" + URLEncoder.encode(vsUrl, StandardCharsets.UTF_8)
                    + "&system=" + URLEncoder.encode(codeSystem, StandardCharsets.UTF_8)
                    + "&code=" + URLEncoder.encode(code.getCode(), StandardCharsets.UTF_8);
            log.debug("$validate-code: {}", requestUrl);
            HttpResponse<String> resp = httpClient.send(
                    buildGetRequest(requestUrl), HttpResponse.BodyHandlers.ofString());
            if (resp.statusCode() != 200) {
                log.warn("$validate-code returned HTTP {} for code '{}'", resp.statusCode(), code.getCode());
                return null;
            }
            JsonNode params = objectMapper.readTree(resp.body()).path("parameter");
            if (params.isArray()) {
                for (JsonNode p : params) {
                    if ("result".equals(p.path("name").asText(null))) {
                        return p.path("valueBoolean").asBoolean(false);
                    }
                }
            }
            log.warn("$validate-code response missing 'result' parameter for code '{}'", code.getCode());
            return null;
        } catch (Exception e) {
            log.warn("$validate-code call failed: {}", e.getMessage());
            return null;
        }
    }

    @Override
    public Code lookup(Code code, CodeSystemInfo codeSystem) {
        return code;
    }

    private List<Code> fetchExpansion(String valueSetId) throws Exception {
        String url = normaliseValueSetUrl(valueSetId);
        ensureCapabilitiesLoaded();
        if (supportsExpand) {
            String expandUrl = fhirBaseUrl + "/ValueSet/$expand?url="
                    + URLEncoder.encode(url, StandardCharsets.UTF_8);
            log.debug("Expanding value set via $expand: {}", expandUrl);
            HttpResponse<String> response = httpClient.send(
                    buildGetRequest(expandUrl), HttpResponse.BodyHandlers.ofString());
            if (response.statusCode() == 200) {
                List<Code> codes = parseExpansion(response.body());
                if (!codes.isEmpty()) return codes;
            } else {
                log.warn("$expand returned HTTP {} for '{}'", response.statusCode(), url);
            }
        }
        String searchUrl = fhirBaseUrl + "/ValueSet?url="
                + URLEncoder.encode(url, StandardCharsets.UTF_8) + "&_elements=id";
        log.debug("Searching for value set by URL: {}", searchUrl);
        HttpResponse<String> searchResp = httpClient.send(
                buildGetRequest(searchUrl), HttpResponse.BodyHandlers.ofString());
        if (searchResp.statusCode() != 200) {
            log.error("ValueSet search returned HTTP {} for '{}'", searchResp.statusCode(), url);
            return List.of();
        }
        JsonNode bundle = objectMapper.readTree(searchResp.body());
        JsonNode entries = bundle.path("entry");
        if (!entries.isArray() || entries.isEmpty()) {
            log.warn("No ValueSet found on server for URL '{}'", url);
            return List.of();
        }
        String vsId = null;
        for (JsonNode entry : entries) {
            JsonNode identifiers = entry.path("resource").path("identifier");
            if (!identifiers.isArray()) {
                continue;
            }
            boolean matchesValueSetId = false;
            for (JsonNode identifier : identifiers) {
                String identifierValue = identifier.path("value").asText(null);
                if (valueSetId.equals(identifierValue)) {
                    matchesValueSetId = true;
                    break;
                }
            }
            if (matchesValueSetId) {
                vsId = entry.path("resource").path("id").asText(null);
                break;
            }
        }
        if (vsId == null || vsId.isBlank()) {
            log.warn("No ValueSet entry with identifier value '{}' for URL '{}'", valueSetId, url);
            return List.of();
        }
        String readUrl = fhirBaseUrl + "/ValueSet/" + vsId;
        log.debug("Reading raw ValueSet resource: {}", readUrl);
        HttpResponse<String> readResp = httpClient.send(
                buildGetRequest(readUrl), HttpResponse.BodyHandlers.ofString());
        if (readResp.statusCode() != 200) {
            log.error("ValueSet read returned HTTP {} for id '{}'", readResp.statusCode(), vsId);
            return List.of();
        }
        return parseValueSetResource(readResp.body());
    }

    private HttpRequest buildGetRequest(String url) throws Exception {
        HttpRequest.Builder builder = HttpRequest.newBuilder()
                .uri(URI.create(url))
                .header("Accept", "application/fhir+json")
                .timeout(Duration.ofSeconds(30L));
        addAuthHeader(builder);
        return builder.GET().build();
    }

    private void addAuthHeader(HttpRequest.Builder builder) throws Exception {
        switch (authType) {
            case "BASIC":
                if (username != null && password != null) {
                    String encoded = Base64.getEncoder().encodeToString(
                            (username + ":" + password).getBytes(StandardCharsets.UTF_8));
                    builder.header("Authorization", "Basic " + encoded);
                }
                break;
            case "BEARER":
                if (staticToken != null && !staticToken.isBlank()) {
                    builder.header("Authorization", "Bearer " + staticToken);
                }
                break;
            case "OAUTH2":
                builder.header("Authorization", "Bearer " + resolveOAuth2Token());
                break;
        }
    }

    private String resolveOAuth2Token() throws Exception {
        synchronized (tokenLock) {
            if (oauth2Token != null && Instant.now().isBefore(oauth2TokenExpiry)) {
                return oauth2Token;
            }
            return fetchOAuth2Token();
        }
    }

    private String fetchOAuth2Token() throws Exception {
        if (tokenUrl == null || tokenUrl.isBlank()) {
            throw new IllegalStateException("OAUTH2 auth type requires a tokenUrl");
        }
        StringBuilder body = new StringBuilder("grant_type=client_credentials")
                .append("&client_id=").append(URLEncoder.encode(blankIfNull(clientId), StandardCharsets.UTF_8))
                .append("&client_secret=").append(URLEncoder.encode(blankIfNull(clientSecret), StandardCharsets.UTF_8));
        if (scope != null && !scope.isBlank()) {
            body.append("&scope=").append(URLEncoder.encode(scope, StandardCharsets.UTF_8));
        }
        HttpRequest req = HttpRequest.newBuilder()
                .uri(URI.create(tokenUrl))
                .header("Content-Type", "application/x-www-form-urlencoded")
                .timeout(Duration.ofSeconds(30L))
                .POST(HttpRequest.BodyPublishers.ofString(body.toString()))
                .build();
        HttpResponse<String> resp = httpClient.send(req, HttpResponse.BodyHandlers.ofString());
        if (resp.statusCode() != 200) {
            throw new RuntimeException("OAuth2 token request failed HTTP " + resp.statusCode() + ": " + resp.body());
        }
        JsonNode json = objectMapper.readTree(resp.body());
        String accessToken = json.path("access_token").asText(null);
        if (accessToken == null) {
            throw new RuntimeException("OAuth2 response missing access_token: " + resp.body());
        }
        int expiresIn = json.path("expires_in").asInt(3600);
        oauth2Token = accessToken;
        oauth2TokenExpiry = Instant.now().plusSeconds(Math.max(expiresIn - 60, 0));
        log.debug("OAuth2 token acquired, expires in {}s", expiresIn);
        return oauth2Token;
    }

    private List<Code> parseExpansion(String body) throws Exception {
        JsonNode root = objectMapper.readTree(body);
        JsonNode contains = root.path("expansion").path("contains");
        List<Code> codes = new ArrayList<>();
        if (contains.isArray()) {
            for (JsonNode entry : contains) {
                codes.add(new Code()
                        .withCode(entry.path("code").asText())
                        .withSystem(entry.path("system").asText(null))
                        .withDisplay(entry.path("display").asText(null))
                        .withVersion(entry.path("version").asText(null)));
            }
        }
        log.debug("Expanded {} codes from value set", codes.size());
        return codes;
    }

    private List<Code> parseValueSetResource(String body) throws Exception {
        JsonNode root = objectMapper.readTree(body);
        List<Code> codes = new ArrayList<>();
        JsonNode contains = root.path("expansion").path("contains");
        if (contains.isArray() && !contains.isEmpty()) {
            for (JsonNode entry : contains) {
                codes.add(new Code()
                        .withCode(entry.path("code").asText())
                        .withSystem(entry.path("system").asText(null))
                        .withDisplay(entry.path("display").asText(null)));
            }
            log.debug("Parsed {} codes from expansion.contains", codes.size());
            return codes;
        }
        JsonNode includes = root.path("compose").path("include");
        if (includes.isArray()) {
            for (JsonNode inc : includes) {
                String system = inc.path("system").asText(null);
                JsonNode concepts = inc.path("concept");
                if (!concepts.isArray()) continue;
                for (JsonNode c : concepts) {
                    codes.add(new Code()
                            .withCode(c.path("code").asText())
                            .withSystem(system)
                            .withDisplay(c.path("display").asText(null)));
                }
            }
        }
        if (!codes.isEmpty()) {
            log.debug("Parsed {} codes from compose.include", codes.size());
            return codes;
        }
        JsonNode codings = root.path("codings");
        if (codings.isArray()) {
            for (JsonNode c : codings) {
                codes.add(new Code()
                        .withCode(c.path("code").asText())
                        .withSystem(c.path("system").asText(null))
                        .withDisplay(c.path("display").asText(null)));
            }
            log.debug("Parsed {} codes from top-level codings[]", codes.size());
        }
        return codes;
    }

    private static String normalise(String url) {
        if (url == null || url.isBlank()) return null;
        return url.strip().replaceAll("/$", "");
    }

    private static String normaliseValueSetUrl(String id) {
        if (id != null && id.startsWith("urn:oid:")) {
            return "http://cts.nlm.nih.gov/fhir/ValueSet/" + id.substring("urn:oid:".length());
        }
        return id;
    }

    private static String blankIfNull(String s) {
        return s != null ? s : "";
    }

    private static Cache<String, List<Code>> buildValueSetCache(long maxWeight, Duration ttl) {
        Caffeine<Object, Object> builder = Caffeine.newBuilder()
            .expireAfterAccess(ttl);
        if (maxWeight > 0) {
            builder.maximumWeight(maxWeight)
                .weigher((String key, List<Code> codes) -> Math.max(1, codes.size()));
        } else {
            builder.maximumSize(0);
        }
        return builder.build();
    }

    private static Cache<String, ValueSetMembership> buildMembershipCache(long maxWeight, Duration ttl) {
        Caffeine<Object, Object> builder = Caffeine.newBuilder()
            .expireAfterAccess(ttl);
        if (maxWeight > 0) {
            builder.maximumWeight(maxWeight)
                .weigher((String key, ValueSetMembership membership) -> Math.max(1, membership.entryCount()));
        } else {
            builder.maximumSize(0);
        }
        return builder.build();
    }

    private static Cache<String, Boolean> buildValidateCodeCache(long maxSize, Duration ttl) {
        Caffeine<Object, Object> builder = Caffeine.newBuilder()
            .expireAfterAccess(ttl);
        if (maxSize > 0) {
            builder.maximumSize(maxSize);
        } else {
            builder.maximumSize(0);
        }
        return builder.build();
    }

    private static ValueSetMembership buildMembership(List<Code> codes) {
        Set<String> exact = new HashSet<>();
        Set<String> wildcardSystem = new HashSet<>();
        for (Code c : codes) {
            if (c == null || c.getCode() == null) continue;
            if (c.getSystem() == null || c.getSystem().isBlank()) {
                wildcardSystem.add(c.getCode());
            } else {
                exact.add(c.getSystem() + "|" + c.getCode());
            }
        }
        return new ValueSetMembership(exact, wildcardSystem);
    }

    private static final class ValueSetMembership {
        private final Set<String> exactSystemAndCode;
        private final Set<String> wildcardSystemCodes;

        private ValueSetMembership(Set<String> exactSystemAndCode, Set<String> wildcardSystemCodes) {
            this.exactSystemAndCode = exactSystemAndCode;
            this.wildcardSystemCodes = wildcardSystemCodes;
        }

        private boolean contains(Code code) {
            if (code == null || code.getCode() == null) return false;
            if (wildcardSystemCodes.contains(code.getCode())) return true;
            if (code.getSystem() == null || code.getSystem().isBlank()) return false;
            return exactSystemAndCode.contains(code.getSystem() + "|" + code.getCode());
        }

        private int entryCount() {
            return exactSystemAndCode.size() + wildcardSystemCodes.size();
        }
    }
}
