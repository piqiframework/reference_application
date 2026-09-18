package com.cqlsidecar;

import org.springframework.boot.context.properties.ConfigurationProperties;
import org.springframework.stereotype.Component;

import java.util.ArrayList;
import java.util.List;

@Component
@ConfigurationProperties(prefix = "cql")
public class CqlProperties {

    private List<ModelConfig> models = new ArrayList<>();

    public List<ModelConfig> getModels() { return models; }
    public void setModels(List<ModelConfig> models) { this.models = models; }

    public static class ModelConfig {
        /** Must exactly match the 'url' attribute in the ModelInfo XML and the 'using' URI in your ELM. */
        private String uri;
        /** Classpath-relative path to the ModelInfo XML, e.g. model/piqi_patient-modelinfo.xml */
        private String infoPath;

        public String getUri()             { return uri; }
        public void setUri(String uri)     { this.uri = uri; }
        public String getInfoPath()        { return infoPath; }
        public void setInfoPath(String v)  { this.infoPath = v; }
    }
}
