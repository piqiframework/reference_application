package com.cqlsidecar;

import org.opencds.cqf.cql.engine.model.ModelResolver;
import org.opencds.cqf.cql.engine.retrieve.RetrieveProvider;
import org.opencds.cqf.cql.engine.runtime.ClassInstance;
import org.opencds.cqf.cql.engine.runtime.Code;
import org.opencds.cqf.cql.engine.runtime.Date;
import org.opencds.cqf.cql.engine.runtime.DateTime;
import org.opencds.cqf.cql.engine.runtime.Interval;
import org.opencds.cqf.cql.engine.runtime.Value;

import javax.xml.namespace.QName;
import javax.xml.parsers.DocumentBuilder;
import javax.xml.parsers.DocumentBuilderFactory;
import org.w3c.dom.Document;
import org.w3c.dom.Element;
import org.w3c.dom.NodeList;
import org.xml.sax.InputSource;

import java.io.StringReader;
import java.math.BigDecimal;
import java.time.ZoneOffset;
import java.util.*;
import java.util.regex.Pattern;

/**
 * A map-backed data provider for custom CQL models.
 *
 * Context data is expected as: { "TypeName": [ { "prop": "val", ... }, ... ] }
 * deserialized into Map<String, List<Map<String, Object>>>.
 *
 * <p>As of the org.cqframework 4.x/5.x (Java-to-Kotlin) rewrite, CQL runtime values are a
 * sealed {@link Value} hierarchy -- there is no more "hand back an arbitrary Java Object /
 * navigate it by reflection" trick, and {@link ModelResolver} lost the resolvePath/setValue/
 * resolveType/as/getPackageName methods this class used to implement. Every value handed to
 * the engine now has to be one of the engine's own concrete runtime types (String, Integer,
 * Boolean, Decimal, Long, Date, DateTime, {@link ClassInstance}, runtime List, ...), and
 * RetrieveProvider.retrieve() returns fully-populated instances directly instead of the
 * engine building them field-by-field via ModelResolver.
 *
 * <p>To convert each JSON record to the right runtime types, this class needs to know the
 * *declared* type of each property (e.g. "System.DateTime" vs "PIQI.CodeableConcept" vs
 * "list&lt;PIQI.Coding&gt;") -- that's what {@link ModelSchema} reads directly out of the raw
 * ModelInfo XML (independent of the org.hl7.elm_modelinfo.r1.ModelInfo Kotlin API, which is
 * generated code we can't easily verify offline). Properties that aren't declared in the
 * ModelInfo (e.g. a passthrough "id" field used only for context-matching) fall back to
 * best-effort shape-based inference, same as the old code's permissive behavior.
 */
public class CustomDataProvider implements RetrieveProvider, ModelResolver {

    private final Map<String, List<Map<String, Object>>> dataByType;

    // The model URI must match the 'using' declaration in your CQL/ELM
    // e.g. using MyModel version '1.0.0' -> uri = "urn:piqi_patient-model:1.0.0"
    private final String modelUri;

    // Optional: declared class/property types read from the ModelInfo XML. May be null if a
    // caller doesn't have a schema handy (e.g. the placeholder providers CqlController creates
    // for models other than the one actually in use for a given request) -- everything just
    // falls back to shape-based inference in that case.
    private final ModelSchema schema;

    // Matches ISO date ("2025-04-03") and ISO datetime ("2025-04-03T12:00:00")
    private static final Pattern ISO_DATE = Pattern.compile("^\\d{4}-\\d{2}-\\d{2}(T.*)?$");

    public CustomDataProvider(Map<String, List<Map<String, Object>>> dataByType, String modelUri) {
        this(dataByType, modelUri, null);
    }

    public CustomDataProvider(Map<String, List<Map<String, Object>>> dataByType, String modelUri, ModelSchema schema) {
        this.dataByType = dataByType != null ? dataByType : Collections.emptyMap();
        this.modelUri = modelUri;
        this.schema = schema;
    }

    // -------------------------------------------------------------------------
    // RetrieveProvider — returns instances of a type for a given context
    // -------------------------------------------------------------------------

    @Override
    public Iterable<Value> retrieve(
            String context, String contextPath, String contextValue,
            String dataType, String templateId,
            String codePath, Iterable<Code> codes,
            String valueSet, String datePath,
            String dateLowPath, String dateHighPath, Interval dateRange) {

        // ELM qualifies dataType with namespace: "{urn:piqi_patient-model:1.0.0}Patient"
        // Our contextData map keys are simple names like "Patient", so strip the qualifier.
        String simpleType = stripNamespaceQualifier(dataType);

        List<Map<String, Object>> instances = dataByType.containsKey(dataType)
            ? dataByType.get(dataType)
            : dataByType.getOrDefault(simpleType, Collections.emptyList());

        // Filter by context id. The engine now passes the context as a plain String id
        // (not the full object), so this is a straightforward string comparison.
        if (context != null && contextPath != null && contextValue != null) {
            instances = instances.stream()
                .filter(obj -> contextValue.equals(String.valueOf(obj.get(contextPath))))
                .toList();
        }

        List<Value> result = new ArrayList<>(instances.size());
        for (Map<String, Object> obj : instances) {
            result.add(toClassInstance(simpleType, obj));
        }
        return result;
    }

    // -------------------------------------------------------------------------
    // ModelResolver — the runtime resolver interface shrank considerably in the rewrite;
    // resolvePath/setValue/resolveType/as/getPackageName are gone. What's left mostly
    // supports type-checking and id resolution now that retrieve() returns fully-formed
    // Values directly.
    // -------------------------------------------------------------------------

    /**
     * Returns the path expression used to navigate from a context type to the context value.
     * For map-backed objects the path IS the property key, so return targetType directly
     * (same passthrough behavior as before -- this method's second parameter is the target
     * type's context-linking property name, e.g. "subject" in a FHIR-style model).
     */
    @Override
    public String getContextPath(String contextType, String targetType) {
        return targetType;
    }

    @Override
    public Boolean is(String valueType, QName type) {
        if (valueType == null || type == null) {
            return false;
        }
        String targetSimple = simpleName(type.getLocalPart());
        if ("Any".equals(targetSimple)) {
            return true;
        }
        String current = valueType;
        for (int guard = 0; current != null && guard < 32; guard++) {
            String currentSimple = simpleName(current);
            if (currentSimple.equals(targetSimple)) {
                return true;
            }
            ModelSchema.ClassDef def = schema != null ? schema.classFor(currentSimple) : null;
            current = def != null ? def.baseType() : null;
        }
        return false;
    }

    @Override
    public Value createInstance(String typeName) {
        if (typeName == null) {
            return null;
        }
        return new ClassInstance(new QName(modelUri, typeName), new LinkedHashMap<>());
    }

    // objectEquivalent(ClassInstance, ClassInstance, (Value?, Value?) -> Boolean) is left to
    // the interface's default implementation, which compares elements structurally -- that's
    // exactly what this map-backed provider would do anyway.

    /**
     * Returns a string ID for an object instance. ClassInstances built from our map-backed
     * objects carry an "id" element when the source JSON had one; fall back to null otherwise
     * (the old identity-hash fallback doesn't map cleanly onto the new Value hierarchy and
     * isn't meaningful for CQL semantics anyway).
     */
    @Override
    public String resolveId(Value target) {
        if (target instanceof ClassInstance instance) {
            Value id = instance.getElements().get("id");
            return id != null ? stringify(id) : null;
        }
        return null;
    }

    // -------------------------------------------------------------------------
    // JSON -> CQL runtime Value conversion, driven by the ModelInfo schema where available.
    // -------------------------------------------------------------------------

    private Value toClassInstance(String simpleType, Map<String, Object> obj) {
        ModelSchema.ClassDef classDef = schema != null ? schema.classFor(simpleType) : null;
        Map<String, Value> elements = new LinkedHashMap<>();
        for (Map.Entry<String, Object> entry : obj.entrySet()) {
            String declaredType = classDef != null ? classDef.propertyTypes().get(entry.getKey()) : null;
            elements.put(entry.getKey(), toValue(entry.getValue(), declaredType));
        }
        return new ClassInstance(new QName(modelUri, simpleType), elements);
    }

    private Value toValue(Object raw, String declaredType) {
        if (raw == null) {
            return null;
        }

        if (declaredType != null && declaredType.startsWith("list<") && declaredType.endsWith(">")) {
            String elementType = declaredType.substring("list<".length(), declaredType.length() - 1);
            List<Value> items = new ArrayList<>();
            if (raw instanceof List<?> list) {
                for (Object item : list) {
                    items.add(toValue(item, elementType));
                }
            }
            return new org.opencds.cqf.cql.engine.runtime.List(items);
        }

        if (declaredType != null && declaredType.startsWith("System.")) {
            return toSystemValue(raw, declaredType.substring("System.".length()));
        }

        if (declaredType != null && raw instanceof Map<?, ?> nested) {
            @SuppressWarnings("unchecked")
            Map<String, Object> nestedMap = (Map<String, Object>) nested;
            return toClassInstance(simpleName(declaredType), nestedMap);
        }

        // No declared type -- e.g. a passthrough "id" field that isn't part of the CQL-visible
        // model, or a slightly-stale ModelInfo. Fall back to best-effort shape-based inference
        // instead of hard-failing, matching the old code's permissive behavior.
        return inferValue(raw);
    }

    private Value toSystemValue(Object raw, String systemType) {
        return switch (systemType) {
            case "DateTime" -> toTemporal(raw, false);
            case "Date" -> toTemporal(raw, true);
            case "Boolean" -> raw instanceof Boolean b
                ? new org.opencds.cqf.cql.engine.runtime.Boolean(b)
                : new org.opencds.cqf.cql.engine.runtime.Boolean(Boolean.parseBoolean(raw.toString()));
            case "Integer" -> raw instanceof Number n
                ? new org.opencds.cqf.cql.engine.runtime.Integer(n.intValue())
                : new org.opencds.cqf.cql.engine.runtime.Integer(Integer.parseInt(raw.toString()));
            case "Long" -> raw instanceof Number n
                ? new org.opencds.cqf.cql.engine.runtime.Long(n.longValue())
                : new org.opencds.cqf.cql.engine.runtime.Long(Long.parseLong(raw.toString()));
            case "Decimal" -> new org.opencds.cqf.cql.engine.runtime.Decimal(new BigDecimal(raw.toString()));
            case "String" -> new org.opencds.cqf.cql.engine.runtime.String(raw.toString());
            default -> inferValue(raw);
        };
    }

    private Value toTemporal(Object raw, boolean dateOnly) {
        String str = raw.toString();
        try {
            return dateOnly ? new Date(str) : new DateTime(str, ZoneOffset.UTC);
        } catch (Exception ignored) {
            return new org.opencds.cqf.cql.engine.runtime.String(str);
        }
    }

    /**
     * Best-effort conversion for values with no declared ModelInfo type: auto-converts
     * ISO date/datetime strings to CQL DateTime (same heuristic the old code's autoConvert
     * used), and otherwise wraps by shape.
     */
    @SuppressWarnings("unchecked")
    private Value inferValue(Object raw) {
        if (raw instanceof String str) {
            if (ISO_DATE.matcher(str).matches()) {
                try { return new DateTime(str, ZoneOffset.UTC); } catch (Exception ignored) { }
            }
            return new org.opencds.cqf.cql.engine.runtime.String(str);
        }
        if (raw instanceof Boolean b) return new org.opencds.cqf.cql.engine.runtime.Boolean(b);
        if (raw instanceof Integer i) return new org.opencds.cqf.cql.engine.runtime.Integer(i);
        if (raw instanceof Long l) return new org.opencds.cqf.cql.engine.runtime.Long(l);
        if (raw instanceof Double || raw instanceof BigDecimal) {
            return new org.opencds.cqf.cql.engine.runtime.Decimal(new BigDecimal(raw.toString()));
        }
        if (raw instanceof List<?> list) {
            List<Value> items = new ArrayList<>();
            for (Object item : list) {
                items.add(inferValue(item));
            }
            return new org.opencds.cqf.cql.engine.runtime.List(items);
        }
        if (raw instanceof Map<?, ?> map) {
            // Untyped nested object -- wrap as a ClassInstance under this model's namespace
            // so the engine can still navigate it structurally, even without a matching
            // ModelInfo class declaration.
            Map<String, Value> elements = new LinkedHashMap<>();
            for (Map.Entry<?, ?> entry : map.entrySet()) {
                elements.put(String.valueOf(entry.getKey()), inferValue(entry.getValue()));
            }
            return new ClassInstance(new QName(modelUri, "Any"), elements);
        }
        return new org.opencds.cqf.cql.engine.runtime.String(String.valueOf(raw));
    }

    private static String stringify(Value value) {
        if (value == null) return null;
        if (value instanceof org.opencds.cqf.cql.engine.runtime.String s) return s.getValue();
        return value.toString();
    }

    private static String stripNamespaceQualifier(String dataType) {
        if (dataType != null && dataType.contains("}")) {
            return dataType.substring(dataType.indexOf('}') + 1);
        }
        return dataType;
    }

    private static String simpleName(String qualifiedOrSimple) {
        if (qualifiedOrSimple == null) return null;
        int dot = qualifiedOrSimple.lastIndexOf('.');
        return dot >= 0 ? qualifiedOrSimple.substring(dot + 1) : qualifiedOrSimple;
    }

    /**
     * Minimal, standalone reader of the ModelInfo XML's class/property type declarations.
     * Deliberately independent of the org.hl7.elm_modelinfo.r1.ModelInfo Kotlin API surface
     * (generated code from the modelinfo.xsd schema that we can't verify offline) -- this
     * only needs class names, each class's baseType, and each property's declared type,
     * which it reads straight out of the XML with plain JDK DOM parsing.
     */
    public static final class ModelSchema {
        private static final String MODELINFO_NS = "urn:hl7-org:elm-modelinfo:r1";
        private static final String XSI_NS = "http://www.w3.org/2001/XMLSchema-instance";

        public record ClassDef(String qualifiedName, String baseType, Map<String, String> propertyTypes) {}

        private final Map<String, ClassDef> classesBySimpleName;

        private ModelSchema(Map<String, ClassDef> classesBySimpleName) {
            this.classesBySimpleName = classesBySimpleName;
        }

        public static ModelSchema parse(String modelInfoXml) throws Exception {
            DocumentBuilderFactory factory = DocumentBuilderFactory.newInstance();
            factory.setNamespaceAware(true);
            // Defensive: this XML is a trusted, locally-bundled resource, but disable DOCTYPE
            // processing regardless.
            try {
                factory.setFeature("http://apache.org/xml/features/disallow-doctype-decl", true);
            } catch (Exception ignored) { }
            DocumentBuilder builder = factory.newDocumentBuilder();
            Document doc = builder.parse(new InputSource(new StringReader(modelInfoXml)));

            Map<String, ClassDef> classes = new LinkedHashMap<>();
            NodeList typeInfos = doc.getElementsByTagNameNS(MODELINFO_NS, "typeInfo");
            for (int i = 0; i < typeInfos.getLength(); i++) {
                Element typeInfo = (Element) typeInfos.item(i);
                String xsiType = typeInfo.getAttributeNS(XSI_NS, "type");
                if (xsiType == null || !xsiType.endsWith("ClassInfo")) {
                    continue; // skip SimpleTypeInfo etc. -- only classes declare properties
                }
                String qualifiedName = typeInfo.getAttribute("name");
                String baseType = typeInfo.getAttribute("baseType");

                Map<String, String> props = new LinkedHashMap<>();
                NodeList elements = typeInfo.getElementsByTagNameNS(MODELINFO_NS, "element");
                for (int j = 0; j < elements.getLength(); j++) {
                    Element el = (Element) elements.item(j);
                    String name = el.getAttribute("name");
                    String type = el.hasAttribute("type") ? el.getAttribute("type") : el.getAttribute("elementType");
                    if (!name.isBlank()) {
                        props.put(name, type);
                    }
                }
                classes.put(simpleName(qualifiedName), new ClassDef(qualifiedName, baseType, props));
            }
            return new ModelSchema(classes);
        }

        public ClassDef classFor(String simpleTypeName) {
            return classesBySimpleName.get(simpleName(simpleTypeName));
        }
    }
}
