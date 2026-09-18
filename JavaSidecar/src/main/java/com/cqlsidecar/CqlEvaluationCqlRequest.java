package com.cqlsidecar;

import java.util.List;
import java.util.Map;

/** Request DTO for the {@code POST /cql/evaluate-cql} endpoint. */
public class CqlEvaluationCqlRequest {
    /** CQL library source text to translate and evaluate. */
    private String cqlText;
    /** Subset of expression names to evaluate; null means evaluate all. */
    private List<String> expressions;
    /** Patient/context data as JSON: { "TypeName": [ {...}, {...} ] } */
    private String contextDataJson;
    /** Optional CQL parameter overrides (e.g. Measurement Period). */
    private Map<String, Object> parameters;
    /** Optional: override which model URI to use (auto-detected from ELM if null). */
    private String modelUri;

    public String getCqlText()                         { return cqlText; }
    public void setCqlText(String v)                   { this.cqlText = v; }

    public List<String> getExpressions()               { return expressions; }
    public void setExpressions(List<String> v)         { this.expressions = v; }

    public String getContextDataJson()                 { return contextDataJson; }
    public void setContextDataJson(String v)           { this.contextDataJson = v; }

    public Map<String, Object> getParameters()         { return parameters; }
    public void setParameters(Map<String, Object> v)   { this.parameters = v; }

    public String getModelUri()                        { return modelUri; }
    public void setModelUri(String v)                  { this.modelUri = v; }

    /** Optional: override the FHIR terminology server for this request. */
    private FhirSettings fhirSettings;
    public FhirSettings getFhirSettings()              { return fhirSettings; }
    public void setFhirSettings(FhirSettings v)        { this.fhirSettings = v; }
}
