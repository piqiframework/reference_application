package com.cqlsidecar;

import java.util.List;
import java.util.Map;

public class EvaluationRequest {
    private String elmJson;
    private String libraryId;
    private String libraryVersion;
    private List<String> expressions;
    private String contextDataJson;
    private Map<String, Object> parameters;
    /** Optional: override which model URI to use. If null, auto-detected from ELM usings. */
    private String modelUri;

    public String getElmJson()            { return elmJson; }
    public void setElmJson(String v)      { this.elmJson = v; }

    public String getLibraryId()          { return libraryId; }
    public void setLibraryId(String v)    { this.libraryId = v; }

    public String getLibraryVersion()         { return libraryVersion; }
    public void setLibraryVersion(String v)   { this.libraryVersion = v; }

    public List<String> getExpressions()       { return expressions; }
    public void setExpressions(List<String> v) { this.expressions = v; }

    public String getContextDataJson()         { return contextDataJson; }
    public void setContextDataJson(String v)   { this.contextDataJson = v; }

    public Map<String, Object> getParameters()        { return parameters; }
    public void setParameters(Map<String, Object> v)  { this.parameters = v; }

    public String getModelUri()           { return modelUri; }
    public void setModelUri(String v)     { this.modelUri = v; }

    /** Optional: override the FHIR terminology server for this request. */
    private FhirSettings fhirSettings;
    public FhirSettings getFhirSettings()             { return fhirSettings; }
    public void setFhirSettings(FhirSettings v)       { this.fhirSettings = v; }
}


