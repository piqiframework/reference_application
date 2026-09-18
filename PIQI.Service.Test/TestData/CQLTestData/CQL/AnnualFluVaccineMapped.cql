library AnnualFluVaccine version '1.0.0'

using PIQI version '1.0.0'

valueset "Influenza Vaccines": 'http://cts.nlm.nih.gov/fhir/ValueSet/2.16.840.1.113883.3.464.1003.196.12.1218'

parameter "Measurement Period" Interval<DateTime>
  default Interval[@2024-09-01, @2025-08-31]

context Patient

// ── Helpers ────────────────────────────────────────────────────────────────

define function "HasCode"(concept PIQI.CodeableConcept, targetCode System.String):
  concept is not null
    and exists(concept.codings C where C.code = targetCode)

define function "CalculateAgeAt"(birthDate System.DateTime, asOf System.DateTime):
  years between birthDate and asOf

define function "ConceptInValueSet"(concept PIQI.CodeableConcept, vs ValueSet):
  concept is not null
    and exists(
      concept.codings C
        where System.Code { system: C.system, code: C.code } in vs
    )

// ── Denominator ──────────────────────────────────────────────────────────────

define "Patient Age":
  CalculateAgeAt(Patient.demographics.birthDate, end of "Measurement Period")

define "Is Deceased":
  Patient.demographics.deceased.text = 'true'
    or Patient.demographics.deathDate is not null

define "In Denominator":
  "Patient Age" >= 1
    and not "Is Deceased"

// ── Numerator ────────────────────────────────────────────────────────────────

define "Flu Vaccines in Period":
  Patient.immunizations I
    where "ConceptInValueSet"(I.immunization, "Influenza Vaccines")
      and I.administrationDate same or after start of "Measurement Period"
      and I.administrationDate same or before end of "Measurement Period"
      and I.immunizationStatus.text = 'completed'

define "Received Flu Vaccine":
  exists "Flu Vaccines in Period"

define "In Numerator":
  "Received Flu Vaccine"
