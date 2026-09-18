package com.cqlsidecar;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import org.cqframework.cql.cql2elm.LibraryManager;
import org.cqframework.cql.cql2elm.ModelManager;
import org.cqframework.cql.cql2elm.model.CompiledLibrary;
import org.cqframework.cql.elm.serializing.ElmJsonLibraryReader;
import org.hl7.cql.model.ModelIdentifier;
import org.hl7.elm.r1.Library;
import org.hl7.elm.r1.ParameterDef;
import org.hl7.elm.r1.VersionedIdentifier;
import org.opencds.cqf.cql.engine.data.CompositeDataProvider;
import org.opencds.cqf.cql.engine.data.DataProvider;
import org.opencds.cqf.cql.engine.execution.CqlEngine;
import org.opencds.cqf.cql.engine.execution.Environment;
import org.opencds.cqf.cql.engine.execution.EvaluationParams;
import org.opencds.cqf.cql.engine.execution.EvaluationResult;
import org.opencds.cqf.cql.engine.execution.EvaluationResults;
import org.opencds.cqf.cql.engine.runtime.DateTime;
import org.opencds.cqf.cql.engine.runtime.Interval;
import org.opencds.cqf.cql.engine.runtime.Value;
import org.opencds.cqf.cql.engine.terminology.TerminologyProvider;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.*;

import jakarta.annotation.PostConstruct;
import kotlin.Pair;
import java.io.InputStream;
import java.math.BigDecimal;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.time.ZonedDateTime;
import java.util.*;

import static org.hl7.elm_modelinfo.r1.serializing.XmlModelInfoReaderKt.parseModelInfoXml;

@RestController
@RequestMapping("/cql")
public class CqlController {

    private static final Logger log = LoggerFactory.getLogger(CqlController.class);

    private final ObjectMapper objectMapper = new ObjectMapper();
    private final CqlProperties cqlProperties;
    private final FhirR4TerminologyProvider terminologyProvider;
    private final CqlTranslatorClient translatorClient;

    private ModelManager modelManager;

    /** All configured model URIs, registered in order. */
    private final Set<String> registeredUris = new LinkedHashSet<>();
    /** Declared class/property types per model URI, read from each model's ModelInfo XML. */
    private final Map<String, CustomDataProvider.ModelSchema> modelSchemas = new LinkedHashMap<>();
    private static final int PREPARED_LIBRARY_CACHE_MAX = 128;
    private static final Duration PREPARED_LIBRARY_CACHE_TTL = Duration.ofMinutes(30);
    private final Object preparedLibraryCacheLock = new Object();
    private final LinkedHashMap<String, PreparedLibraryCacheEntry> preparedLibraryCache =
        new LinkedHashMap<>(128, 0.75f, true);

    public CqlController(CqlProperties cqlProperties,
                         FhirR4TerminologyProvider terminologyProvider,
                         CqlTranslatorClient translatorClient) {
        this.cqlProperties = cqlProperties;
        this.terminologyProvider = terminologyProvider;
        this.translatorClient = translatorClient;
    }

    @PostConstruct
    public void init() throws Exception {
        if (cqlProperties.getModels().isEmpty()) {
            throw new IllegalStateException(
                "No models configured. Add at least one entry under cql.models[] in application.properties.");
        }

        modelManager = new ModelManager();

        for (CqlProperties.ModelConfig model : cqlProperties.getModels()) {
            try (InputStream stream = getClass().getClassLoader()
                    .getResourceAsStream(model.getInfoPath())) {
                if (stream == null) {
                    throw new IllegalStateException(
                        "ModelInfo XML not found on classpath: " + model.getInfoPath());
                }
                // Read eagerly so the stream is consumed before it closes. ModelInfo XML
                // parsing is now a top-level function built into the cql module (no more
                // ModelInfoReaderFactory/SPI lookup, and no separate model-jackson artifact).
                String modelInfoXml = new String(stream.readAllBytes(), StandardCharsets.UTF_8);
                final var modelInfo = parseModelInfoXml(modelInfoXml);
                // Only handle our custom model by name/URI; return null for all others
                // (System, FHIR, etc.) so the built-in providers can resolve them.
                modelManager.getModelInfoLoader().registerModelInfoProvider(
                    (ModelIdentifier id) ->
                        (modelInfo.getName() != null && modelInfo.getName().equals(id.getId()))
                            || model.getUri().equals(id.getSystem())
                            ? modelInfo : null,
                    true);
                registeredUris.add(model.getUri());
                // Parsed separately (and independently of the generated ModelInfo Kotlin API)
                // so CustomDataProvider knows each property's declared type when converting
                // JSON context data into CQL runtime Values.
                modelSchemas.put(model.getUri(), CustomDataProvider.ModelSchema.parse(modelInfoXml));
            }
        }
    }

    @PostMapping("/evaluate")
    public ResponseEntity<Map<String, Object>> evaluate(
            @RequestBody EvaluationRequest request) throws Exception {
        return doEvaluate(
            request.getElmJson(),
            request.getLibraryId(),
            request.getLibraryVersion(),
            request.getExpressions(),
            request.getContextDataJson(),
            request.getParameters(),
            request.getModelUri(),
            request.getFhirSettings()
        );
    }

    /**
     * Accepts raw CQL text, translates it to ELM via the configured translator service,
     * then evaluates the result. Library id/version are auto-extracted from the ELM.
     */
    @PostMapping("/evaluate-cql")
    public ResponseEntity<Map<String, Object>> evaluateCql(
            @RequestBody CqlEvaluationCqlRequest request) throws Exception {

        if (request.getCqlText() == null || request.getCqlText().isBlank()) {
            return ResponseEntity.badRequest().body(Map.of("error", "cqlText must not be blank"));
        }

        String elmJson = translatorClient.translate(request.getCqlText(), modelManager);

        // Extract library id/version from the translated ELM
        JsonNode root = objectMapper.readTree(elmJson);
        String libraryId = root.path("library").path("identifier").path("id").asText(null);
        String libraryVersion = root.path("library").path("identifier").path("version").asText(null);

        if (libraryId == null) {
            return ResponseEntity.badRequest().body(Map.of(
                "error", "Translated ELM contains no library identifier"));
        }

        return doEvaluate(
            elmJson,
            libraryId,
            libraryVersion,
            request.getExpressions(),
            request.getContextDataJson(),
            request.getParameters(),
            request.getModelUri(),
            request.getFhirSettings()
        );
    }

    private ResponseEntity<Map<String, Object>> doEvaluate(
            String elmJson,
            String libraryId,
            String libraryVersion,
            List<String> expressionList,
            String contextDataJson,
            Map<String, Object> rawParams,
            String modelUriOverride,
            FhirSettings fhirSettings) throws Exception {

        // Use per-request FHIR settings if a URL was provided, otherwise fall back to singleton
        TerminologyProvider effectiveTerminologyProvider =
            (fhirSettings != null && fhirSettings.getUrl() != null && !fhirSettings.getUrl().isBlank())
                ? FhirR4TerminologyProvider.create(fhirSettings)
                : terminologyProvider;

        // Resolve model URI: explicit override → auto-detect from ELM usings → first registered
        String modelUri = resolveModelUri(elmJson, modelUriOverride);

        // Deserialize context data: { "TypeName": [ {...}, {...} ] }
        Map<String, List<Map<String, Object>>> contextData = Collections.emptyMap();
        if (contextDataJson != null && !contextDataJson.isBlank()) {
            contextData = objectMapper.readValue(contextDataJson, new TypeReference<>() {});
        }

        CustomDataProvider dataProvider = new CustomDataProvider(contextData, modelUri, modelSchemas.get(modelUri));
        CompositeDataProvider compositeProvider = new CompositeDataProvider(
            dataProvider,   // ModelResolver
            dataProvider    // RetrieveProvider
        );

        PreparedLibrary preparedLibrary = getOrPrepareLibrary(elmJson);
        Library library = preparedLibrary.library();

        LibraryManager libraryManager = new LibraryManager(modelManager);
        libraryManager.getCompiledLibraries().put(
            preparedLibrary.compiledLibrary().getIdentifier(),
            preparedLibrary.compiledLibrary());

        // Populate providers for every registered model so the engine can satisfy all usings
        Map<String, DataProvider> providers = new LinkedHashMap<>();
        for (String uri : registeredUris) {
            providers.put(uri, uri.equals(modelUri)
                ? compositeProvider
                : new CompositeDataProvider(
                    new CustomDataProvider(Collections.emptyMap(), uri, modelSchemas.get(uri)),
                    new CustomDataProvider(Collections.emptyMap(), uri, modelSchemas.get(uri))));
        }

        Environment env = new Environment(libraryManager, providers, effectiveTerminologyProvider);
        CqlEngine engine = new CqlEngine(env);

        VersionedIdentifier id = new VersionedIdentifier()
            .withId(libraryId)
            .withVersion(libraryVersion);

        // Determine context type from ELM (e.g. "Patient"), then pass the id of the first
        // matching record from contextData. The engine now identifies the evaluation
        // context by a String id (not the object itself) -- CustomDataProvider.retrieve()
        // is what resolves that id back to the actual record.
        String contextType = preparedLibrary.contextType();
        List<Map<String, Object>> contextObjects = contextData.getOrDefault(contextType, List.of());
        Map<String, Object> contextObject = contextObjects.isEmpty() ? null : contextObjects.get(0);
        String contextId = contextObject != null && contextObject.get("id") != null
            ? contextObject.get("id").toString()
            : null;

        // Build CQL runtime parameter map (CQL runtime values are a sealed Value hierarchy
        // now, so every parameter has to become a concrete Value, not a raw Java object).
        // Interval-typed params are accepted as {"start":"...","end":"..."} and converted here.
        // If a param is absent AND the library defines no default for it, we inject a fallback.
        Map<String, Value> cqlParams = buildCqlParameters(rawParams, library);

        EvaluationParams.Builder paramsBuilder = new EvaluationParams.Builder();
        EvaluationParams.LibraryParams.Builder libParamsBuilder =
            new EvaluationParams.LibraryParams.Builder();
        if (expressionList != null) {
            libParamsBuilder.expressions(expressionList);
        }
        paramsBuilder.library(id, libParamsBuilder.build());
        if (contextId != null) {
            paramsBuilder.setContextParameter(new Pair<>(contextType, contextId));
        }
        paramsBuilder.setParameters(cqlParams);

        EvaluationResults results = engine.evaluate(paramsBuilder.build());
        EvaluationResult result = results.getOnlyResultOrThrow();

        Map<String, Object> expressionResults = new LinkedHashMap<>();
        result.getExpressionResults().forEach((key, val) -> {
            Map<String, Object> entry = new LinkedHashMap<>();
            entry.put("value",     describeValue(val.getValue()));
            entry.put("valueType", val.getValue() != null ? val.getValue().getClass().getSimpleName() : null);
            expressionResults.put(key, entry);
        });

        return ResponseEntity.ok(Map.of(
            "results", expressionResults,
            "errors",  List.of()
        ));
    }

    private PreparedLibrary getOrPrepareLibrary(String elmJson) throws Exception {
        String cacheKey = sha256(elmJson);
        Instant now = Instant.now();

        synchronized (preparedLibraryCacheLock) {
            PreparedLibraryCacheEntry cached = preparedLibraryCache.get(cacheKey);
            if (cached != null && cached.expiresAt().isAfter(now)) {
                return cached.preparedLibrary();
            }
            if (cached != null) {
                preparedLibraryCache.remove(cacheKey);
            }
        }

        PreparedLibrary prepared = prepareLibrary(elmJson);
        Instant expiresAt = now.plus(PREPARED_LIBRARY_CACHE_TTL);
        synchronized (preparedLibraryCacheLock) {
            preparedLibraryCache.put(cacheKey, new PreparedLibraryCacheEntry(prepared, expiresAt));
            evictPreparedLibraryCache();
        }

        return prepared;
    }

    private PreparedLibrary prepareLibrary(String elmJson) throws Exception {
        // No more factory/SPI lookup (and no separate elm-jackson artifact) -- the JSON
        // reader is just a plain class now, built into the elm module.
        Library library = new ElmJsonLibraryReader().read(elmJson);

        // Libraries.resolveExpressionRef uses Collections.binarySearch, so the
        // expression defs list MUST be sorted alphabetically by name.
        if (library.getStatements() != null && library.getStatements().getDef() != null) {
            library.getStatements().getDef().sort(
                java.util.Comparator.comparing(org.hl7.elm.r1.ExpressionDef::getName));
        }

        CompiledLibrary compiledLibrary = new CompiledLibrary();
        compiledLibrary.setIdentifier(library.getIdentifier());
        compiledLibrary.setLibrary(library);

        return new PreparedLibrary(library, compiledLibrary, resolveContextType(library));
    }

    private void evictPreparedLibraryCache() {
        Instant now = Instant.now();
        var iterator = preparedLibraryCache.entrySet().iterator();
        while (iterator.hasNext()) {
            if (iterator.next().getValue().expiresAt().isBefore(now)) {
                iterator.remove();
            }
        }

        while (preparedLibraryCache.size() > PREPARED_LIBRARY_CACHE_MAX) {
            var eldest = preparedLibraryCache.entrySet().iterator();
            if (!eldest.hasNext()) {
                break;
            }
            eldest.next();
            eldest.remove();
        }
    }

    private static String sha256(String input) {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            byte[] bytes = digest.digest(input.getBytes(StandardCharsets.UTF_8));
            StringBuilder hex = new StringBuilder(bytes.length * 2);
            for (byte b : bytes) {
                hex.append(String.format("%02x", b));
            }
            return hex.toString();
        } catch (NoSuchAlgorithmException ex) {
            throw new IllegalStateException("Missing SHA-256 algorithm", ex);
        }
    }

    /**
     * Converts JSON-friendly parameter objects to CQL runtime types.
     * Interval params come as {"start":"YYYY-MM-DD","end":"YYYY-MM-DD"}.
     *
     * <p>For "Measurement Period": if the caller did not supply it AND the CQL library
     * does not define its own default, we fall back to the current calendar year.
     * When the library already defines a default (e.g. {@code default Interval[@2024-09-01, ...]})
     * we do NOT inject our own — the CQL engine will use the library default automatically.
     */
    @SuppressWarnings("unchecked")
    private Map<String, Value> buildCqlParameters(Map<String, Object> raw, Library library) {
        Map<String, Value> params = new LinkedHashMap<>();

        if (raw != null) {
            for (var entry : raw.entrySet()) {
                params.put(entry.getKey(), convertParamValue(entry.getValue()));
            }
        }

        // Only inject a Measurement Period fallback when the caller didn't supply one
        // AND the CQL library itself has no default expression for this parameter.
        if (!params.containsKey("Measurement Period")
                && !libraryHasParameterDefault(library, "Measurement Period")) {
            int year = ZonedDateTime.now(ZoneOffset.UTC).getYear();
            log.info("No Measurement Period in request and no library default — falling back to calendar year {}", year);
            params.put("Measurement Period", new Interval(
                new DateTime(year + "-01-01T00:00:00", ZoneOffset.UTC), true,
                new DateTime(year + "-12-31T23:59:59", ZoneOffset.UTC), true));
        }

        return params;
    }

    /**
     * Renders a CQL runtime {@link Value} for the JSON API response. Unwraps the common
     * scalar wrapper types (String/Integer/Boolean/Long/Decimal) to their plain value so the
     * API doesn't leak CQL's quoted-string literal formatting (e.g. runtime.String.toString()
     * returns {@code 'foo'}, not {@code foo}); anything else (dates, class instances, lists,
     * intervals, codes, ...) falls back to Value's own toString().
     */
    private static Object describeValue(Value value) {
        if (value == null) return null;
        if (value instanceof org.opencds.cqf.cql.engine.runtime.String s) return s.getValue();
        if (value instanceof org.opencds.cqf.cql.engine.runtime.Integer i) return i.getValue();
        if (value instanceof org.opencds.cqf.cql.engine.runtime.Boolean b) return b.getValue();
        if (value instanceof org.opencds.cqf.cql.engine.runtime.Long l) return l.getValue();
        if (value instanceof org.opencds.cqf.cql.engine.runtime.Decimal d) return d.getValue().toString();
        return value.toString();
    }

    /** Returns true if the ELM library declares a default expression for the named parameter. */
    private static boolean libraryHasParameterDefault(Library library, String paramName) {
        if (library.getParameters() == null || library.getParameters().getDef() == null) {
            return false;
        }
        return library.getParameters().getDef().stream()
            .anyMatch(p -> paramName.equals(p.getName()) && p.getDefault() != null);
    }

    @SuppressWarnings("unchecked")
    private Value convertParamValue(Object value) {
        if (value == null) {
            return null;
        }
        if (value instanceof Map<?, ?> map) {
            // Interval: {"start": "...", "end": "..."}
            Object start = map.get("start");
            Object end   = map.get("end");
            if (start != null || end != null) {
                return new Interval(
                    start != null ? new DateTime(start.toString(), ZoneOffset.UTC) : null, true,
                    end   != null ? new DateTime(end.toString(),   ZoneOffset.UTC) : null, true);
            }
        }
        if (value instanceof String str) {
            try { return new DateTime(str, ZoneOffset.UTC); } catch (Exception ignored) { }
            return new org.opencds.cqf.cql.engine.runtime.String(str);
        }
        if (value instanceof Boolean b) {
            return new org.opencds.cqf.cql.engine.runtime.Boolean(b);
        }
        if (value instanceof Integer i) {
            return new org.opencds.cqf.cql.engine.runtime.Integer(i);
        }
        if (value instanceof Long l) {
            return new org.opencds.cqf.cql.engine.runtime.Long(l);
        }
        if (value instanceof Double || value instanceof BigDecimal) {
            return new org.opencds.cqf.cql.engine.runtime.Decimal(new BigDecimal(value.toString()));
        }
        if (value instanceof List<?> list) {
            List<Value> converted = new ArrayList<>();
            for (Object item : list) {
                converted.add(convertParamValue(item));
            }
            return new org.opencds.cqf.cql.engine.runtime.List(converted);
        }
        // CQL runtime values are a sealed Value hierarchy now, so unlike the old API we can't
        // just pass an arbitrary Java object through unchanged. Fall back to a CQL String
        // rather than failing outright.
        log.warn("No specific CQL Value mapping for parameter type {}; passing through as a CQL String",
            value.getClass());
        return new org.opencds.cqf.cql.engine.runtime.String(value.toString());
    }

    /**
     * Resolves model URI in priority order:
     * 1. Explicit override provided by caller
     * 2. Auto-detected from the ELM JSON's usings[0].uri
     * 3. First registered model (single-model convenience)
     */
    private String resolveModelUri(String elmJson, String modelUriOverride) {
        if (modelUriOverride != null && !modelUriOverride.isBlank()) {
            return modelUriOverride;
        }
        try {
            JsonNode root = objectMapper.readTree(elmJson);
            JsonNode usings = root.path("library").path("usings").path("def");
            if (usings.isArray()) {
                for (JsonNode using : usings) {
                    String uri = using.path("uri").asText(null);
                    if (uri != null && registeredUris.contains(uri)) {
                        return uri;
                    }
                }
            }
        } catch (Exception ignored) { }
        return registeredUris.iterator().next();
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<Map<String, Object>> handleException(Exception ex) {
        log.error("CQL evaluation error", ex);
        // Unwrap to root cause for a cleaner message
        Throwable root = ex;
        while (root.getCause() != null) root = root.getCause();
        return ResponseEntity.status(HttpStatus.INTERNAL_SERVER_ERROR).body(Map.of(
            "error",   ex.getClass().getSimpleName(),
            "message", ex.getMessage() != null ? ex.getMessage() : "(no message)",
            "cause",   root.getClass().getSimpleName() + ": " + (root.getMessage() != null ? root.getMessage() : "")
        ));
    }

    /**
     * Reads the primary context type from the ELM library (e.g. "Patient").
     * Falls back to "Patient" if the library defines no context.
     */
    private static String resolveContextType(Library library) {
        if (library.getContexts() != null
                && library.getContexts().getDef() != null
                && !library.getContexts().getDef().isEmpty()) {
            return library.getContexts().getDef().get(0).getName();
        }
        return "Patient";
    }

    private record PreparedLibrary(Library library, CompiledLibrary compiledLibrary, String contextType) {}

    private record PreparedLibraryCacheEntry(PreparedLibrary preparedLibrary, Instant expiresAt) {}
}
