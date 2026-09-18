# CQL Sidecar

A Spring Boot service that translates and evaluates Clinical Quality Language (CQL) against patient data using a pluggable custom data model.

---

## Architecture

```
C# Client (CQLTestClient)
        |
        | HTTP POST /cql/evaluate-cql  (CQL text + patient JSON)
        |        or /cql/evaluate       (pre-compiled ELM JSON + patient JSON)
        v
  CQL Sidecar (this service, port 8080)
        |-- CQL Translator (in-process, cqframework 3.29.0)
        |       Translates CQL text -> ELM JSON using the registered ModelInfo
        |
        |-- CQL Engine (info.cqframework engine 3.29.0)
        |       Evaluates ELM against patient data supplied in the request
        |       Uses CustomDataProvider to navigate the data model
        |
        `-- FhirR4TerminologyProvider
                Resolves "in ValueSet" checks against a FHIR R4 server
                Priority: $validate-code -> $expand -> raw resource read
```

---

## Quick Start

```bash
mvn package -DskipTests
java -jar target/cql-sidecar-1.0.0.jar
```

The service starts on port 8080. Check `GET /actuator/health`.

---

## Configuration (`application.properties`)

```properties
server.port=8080

# --- Models ---
# Register one model per index. Add cql.models[1]..., cql.models[2]... for additional models.
# uri    : must exactly match the 'url' attribute in the ModelInfo XML and the 'using' URI in CQL/ELM.
# info-path : classpath path to the ModelInfo XML file (place under src/main/resources/).
cql.models[0].uri=urn:piqi_patient-model:1.0.0
cql.models[0].info-path=model/piqi_patient-modelinfo.xml

# --- Terminology (ValueSet resolution) ---
# Leave blank to skip ValueSet resolution entirely.
cql.terminology.fhir-base-url=https://your-fhir-server/R4
# Set true if the server uses a self-signed or internal TLS certificate.
cql.terminology.skip-ssl-verify=false

# --- Terminology cache tuning ---
# Weighted caches for expanded value sets and O(1) membership lookups.
cql.terminology.valueset-cache-max-weight=500000
cql.terminology.membership-cache-max-weight=1000000
cql.terminology.valueset-cache-ttl-minutes=30

# validate-code result cache (key: valueSetId:system:code)
cql.terminology.validate-cache-max-size=200000
cql.terminology.validate-cache-ttl-minutes=20
```

### Runtime caching behavior

- Prepared ELM libraries are cached by `sha256(elmJson)` with LRU+TTL behavior (`max=128`, `ttl=30m`).
- `$validate-code` responses are cached by `valueSetId:system:code`.
- ValueSet expansions and membership indexes use Caffeine with weighted eviction.

---

## Adding a New Model

### 1. Write the ModelInfo XML

Place it under `src/main/resources/model/`. The root element must be `<modelInfo>` in the
`urn:hl7-org:elm-modelinfo:r1` namespace. Key attributes:

| Attribute | Purpose | Example |
|---|---|---|
| `name` | Short model name used in CQL `using` declarations | `ACME` |
| `version` | Version string, must match the CQL `version` clause | `2.0.0` |
| `url` | Canonical URI — must match `cql.models[n].uri` in properties | `urn:acme-model:2.0.0` |
| `targetQualifier` | Namespace prefix used in CQL type references | `ACME` |
| `patientClassName` | Fully qualified name of the root Patient type | `ACME.Patient` |
| `patientClassIdentifier` | Simple name used in CQL `context Patient` | `Patient` |
| `patientBirthDatePropertyName` | Dot-notation path from Patient to the birth date field | `demographics.birthDate` or `birthDate` |

```xml
<ns4:modelInfo
    name="ACME"
    version="2.0.0"
    url="urn:acme-model:2.0.0"
    targetQualifier="ACME"
    patientClassName="ACME.Patient"
    patientClassIdentifier="Patient"
    patientBirthDatePropertyName="demographics.birthDate"
    xmlns:ns4="urn:hl7-org:elm-modelinfo:r1"
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">

    <!-- Define your types here -->

</ns4:modelInfo>
```

### 2. Define Types

Each type is a `<typeInfo>` element. Use `retrievable="true"` only on the root Patient type.

```xml
<!-- A simple shared type -->
<ns4:typeInfo xsi:type="ns4:ClassInfo" name="ACME.Coding" baseType="System.Any" retrievable="false">
    <ns4:element name="system"  type="System.String"/>
    <ns4:element name="code"    type="System.String"/>
    <ns4:element name="display" type="System.String"/>
</ns4:typeInfo>

<!-- A type with a list property -->
<ns4:typeInfo xsi:type="ns4:ClassInfo" name="ACME.Medication" baseType="System.Any" retrievable="false">
    <ns4:element name="drug"           type="ACME.Coding"/>
    <ns4:element name="prescribedDate" type="System.DateTime"/>
    <ns4:element name="status"         type="System.String"/>
</ns4:typeInfo>

<!-- The root Patient type — retrievable="true" and identifier="Patient" are required -->
<ns4:typeInfo xsi:type="ns4:ClassInfo" name="ACME.Patient" baseType="System.Any"
              identifier="Patient" retrievable="true">
    <ns4:element name="demographics" type="ACME.Demographics"/>
    <ns4:element name="medications"  type="list&lt;ACME.Medication&gt;"/>
</ns4:typeInfo>
```

**Supported `type` values:**

| CQL/ModelInfo type | Meaning |
|---|---|
| `System.String` | String |
| `System.Integer` | Integer |
| `System.Decimal` | Decimal |
| `System.Boolean` | Boolean |
| `System.DateTime` | Date/time — strings matching `yyyy-MM-dd` or ISO 8601 are auto-converted |
| `System.Date` | Date only |
| `System.Quantity` | Quantity with value + unit |
| `list<MyModel.SomeType>` | List of a complex type |
| `MyModel.SomeType` | A nested complex type |

### 3. Register the Model in `application.properties`

```properties
cql.models[1].uri=urn:acme-model:2.0.0
cql.models[1].info-path=model/acme-modelinfo.xml
```

The `uri` value must exactly match the `url` attribute in the XML **and** the URI in the
CQL `using` declaration:

```cql
using ACME version '2.0.0'
```

### 4. Structure the Patient JSON

The sidecar receives patient data as a JSON object wrapped in a `Patient` array:

```json
{
    "Patient": [
        {
            "demographics": {
                "birthDate": "1975-06-15T00:00:00Z",
                "patientIdentifier": "001"
            },
            "immunizations": [
                {
                    "administrationDate": "2024-11-15T00:00:00Z",
                    "immunization": {
                        "codings": [
                            {
                                "code": "141",
                                "display": "Influenza, seasonal, injectable",
                                "system": "http://hl7.org/fhir/sid/cvx"
                            }
                        ],
                        "text": "Influenza, seasonal, injectable"
                    },
                    "immunizationStatus": {
                        "codings": [
                        ],
                        "text": "completed"
                    }
                }
            ]
        }
    ]
}
```

Rules:
- The outer key (`"Patient"`) must match the `patientClassIdentifier` in the ModelInfo XML.
- Date/DateTime strings must be ISO 8601 (`yyyy-MM-ddThh:mm:ssZ` or `yyyy-MM-dd`). They are auto-converted to CQL `DateTime`/`Date` types.
- Nested objects are plain JSON objects; lists are JSON arrays.

---

## `patientBirthDatePropertyName` and `AgeInYearsAt()`

The CQL system function `AgeInYearsAt(date)` (and `AgeInYears()`) internally resolves the
patient's birth date using the path declared in `patientBirthDatePropertyName`.

- If your Patient type has `birthDate` at the top level, set:
  `patientBirthDatePropertyName="birthDate"`

- If it is nested (e.g. under a demographics sub-object), use dot-notation:
  `patientBirthDatePropertyName="demographics.birthDate"`

The `CustomDataProvider` navigates dot-notation paths step by step, so this works for any
depth without any code changes — the model info XML is the only thing that needs to change.

---

## ValueSet Resolution

For CQL expressions like `code in "My ValueSet"`, the sidecar contacts a FHIR R4 server.
It tries operations in this priority order, using the first that succeeds:

| Step | Operation | Notes |
|---|---|---|
| 1 | `GET /metadata` | Fetched once at startup to detect supported operations |
| 2 | `GET /ValueSet/$validate-code` | Most efficient — single code check, no expansion |
| 3 | `GET /ValueSet/$expand` | Full expansion, cached per ValueSet |
| 4 | `GET /ValueSet/{id}` (raw read) | Parses `expansion.contains`, then `compose.include[].concept[]`, then top-level `codings[]` |

When `$validate-code` is unsupported or fails, the sidecar falls back to expansion and
checks membership from a cached O(1) lookup map built from the expanded ValueSet.

If no FHIR server is configured, all `in ValueSet` expressions return `false`.

### Per-Request FHIR Override

Callers can supply `fhirSettings` in the request body to override the default server:

```json
{
  "cqlText": "...",
  "contextDataJson": "...",
  "fhirSettings": {
    "url": "https://other-fhir-server/R4",
    "authType": "Bearer",
    "token": "my-token",
    "skipSslVerify": true
  }
}
```

`authType` can be `None`, `Basic`, `Bearer`, or `OAuth2` (client credentials).

---

## API Reference

### `POST /cql/evaluate-cql`

Translates CQL text to ELM then evaluates it.

```json
{
  "cqlText": "library MyLib version '1.0'\nusing ACME version '2.0.0'\n...",
  "contextDataJson": "{\"Patient\": [{...}]}",
  "expressions": ["In Numerator", "In Denominator"],
  "parameters": {
    "Measurement Period": { "start": "2024-01-01", "end": "2024-12-31" }
  },
  "modelUri": "urn:acme-model:2.0.0",
  "fhirSettings": { "url": "...", "skipSslVerify": true }
}
```

`expressions` — optional; if omitted, all defined expressions are evaluated.  
`parameters` — optional; interval parameters can be supplied as `{ "start": "date", "end": "date" }`.  
`modelUri` — optional; auto-detected from the CQL `using` declaration if absent.

### `POST /cql/evaluate`

Evaluates pre-compiled ELM JSON (skips translation). Same fields except `elmJson` instead of `cqlText`, plus explicit `libraryId` and `libraryVersion`.

---

## Building

```bash
# Build JAR
mvn package -DskipTests

# Run tests
mvn test

# The built JAR is at:
target/cql-sidecar-1.0.0.jar
```

Requires Java 17+ and Maven 3.8+.
