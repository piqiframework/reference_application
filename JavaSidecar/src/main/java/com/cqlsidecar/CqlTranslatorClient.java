package com.cqlsidecar;

import org.cqframework.cql.cql2elm.CqlCompilerException;
import org.cqframework.cql.cql2elm.CqlTranslator;
import org.cqframework.cql.cql2elm.LibraryManager;
import org.cqframework.cql.cql2elm.ModelManager;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Component;

import java.util.stream.Collectors;

/**
 * Translates CQL text to ELM JSON using the in-process cql-to-elm library.
 * No external service required — translation runs in the same JVM.
 */
@Component
public class CqlTranslatorClient {

    private static final Logger log = LoggerFactory.getLogger(CqlTranslatorClient.class);

    /**
     * Translates CQL source text to ELM JSON using the supplied ModelManager.
     *
     * @param cqlText      CQL library source
     * @param modelManager already-initialized ModelManager with custom model(s) registered
     * @return ELM JSON string
     */
    public String translate(String cqlText, ModelManager modelManager) throws Exception {
        LibraryManager libraryManager = new LibraryManager(modelManager);

        CqlTranslator translator = CqlTranslator.fromText(cqlText, libraryManager);

        if (!translator.getErrors().isEmpty()) {
            String errors = translator.getErrors().stream()
                .map(e -> "[" + e.getSeverity() + "] " + e.getMessage())
                .collect(Collectors.joining("\n  "));
            throw new RuntimeException("CQL translation failed:\n  " + errors);
        }

        String elmJson = translator.toJson();
        if (elmJson == null || elmJson.isBlank()) {
            throw new RuntimeException("In-process CQL translation produced empty ELM JSON");
        }

        log.debug("In-process CQL translation succeeded, ELM JSON length={}", elmJson.length());
        return elmJson;
    }
}
