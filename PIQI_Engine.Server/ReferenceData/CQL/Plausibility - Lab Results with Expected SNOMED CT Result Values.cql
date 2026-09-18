library LabResultswithExpectedSNOMEDCTResultValues version '1.0.0'

using PIQI version '1.0.0'

valueset "LOINC Lab Results with Expected SNOMED CT Result Values":
  'LOINC_LAB_RESULTS_WITH_EXPECTED_SNOMED_CT_RESULT_VALUES'

valueset "SNOMED CT Coded Lab Result Values":
  'SNOMED_CT_CODED_LAB_RESULT_VALUES'

context Patient

/*
  Standard CodeableConcept helper
*/
define function "ConceptInValueSet"(
  concept CodeableConcept,
  vs ValueSet
):
  concept is not null
    and exists(
      concept.codings C
        where System.Code {
          system: C.system,
          code: C.code
        } in vs
    )

/*
  ObservationValue helper

  A coded PIQI ObservationValue is represented as:
    type.text = 'CE'
    codings[]

  according to the PIQI transformation templates.
*/
define function "ObservationValueInValueSet"(
  obsValue ObservationValue,
  vs ValueSet
):
  obsValue is not null
    and exists(
      obsValue.codings C
        where System.Code {
          system: C.system,
          code: C.code
        } in vs
    )

/*
  Eligible lab results are those whose performedTest
  belongs to the LOINC value set.
*/
define "Eligible Lab Results":
  Patient.labResults LR
    where ConceptInValueSet(
      LR.performedTest,
      "LOINC Lab Results with Expected SNOMED CT Result Values"
    )

/*
  Measure applicability.
  If no qualifying lab results exist, evaluation is skipped.
*/
define "Initial Population":
  exists("Eligible Lab Results")

define "Denominator":
  "Initial Population"

/*
  Passing results:
  - Result is coded (CE)
  - Result code belongs to SNOMED CT result value set
*/
define "Passing Lab Results":
  "Eligible Lab Results" LR
    where LR.resultValue is not null
      and LR.resultValue.type.text = 'CE'
      and ObservationValueInValueSet(
        LR.resultValue,
        "SNOMED CT Coded Lab Result Values"
      )


define "CE Lab":
  "Eligible Lab Results" LR
    where LR.resultValue is not null
      and LR.resultValue.type.text = 'CE'


define "In Snomed":
  "Eligible Lab Results" LR
    where ObservationValueInValueSet(
        LR.resultValue,
        "SNOMED CT Coded Lab Result Values"
      )


/*
  Pass only when ALL eligible lab results
  contain a SNOMED-coded result value.
*/
define "Numerator":
  Count("Passing Lab Results")
    = Count("Eligible Lab Results")