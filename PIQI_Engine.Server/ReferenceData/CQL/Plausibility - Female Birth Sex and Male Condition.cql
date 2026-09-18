library PlausibilityFemaleBirthSexandMaleCondition version '1.0.0'

using PIQI version '1.0.0'

/*
* Value Sets
*/

valueset "Male Disorders, All": 'MALE_DISORDERS'

parameter "Measurement Period" Interval<DateTime>

context Patient

/*
 * Helper Functions
 */

define function "ConceptInValueSet"(concept PIQI.CodeableConcept, vs ValueSet):
  concept is not null
    and exists(
      concept.codings C
        where System.Code { system: C.system, code: C.code } in vs
    )

define function "HasCodeInList"(concept PIQI.CodeableConcept, codes List<System.String>):
  concept is not null
    and exists(
      concept.codings C
        where C.code in codes
    )

/*
 * Demographics
 */

define "Female Birth Sex":
  HasCodeInList(
    Patient.demographics.birthSex,
    { 'female' }
  )

/*
 * Conditions
 */

define "All Conditions":
  Patient.conditions

define "Male Disorder Conditions":
  "All Conditions" C
    where ConceptInValueSet(
      C.condition,
      "Male Disorders, All"
    )

define "Has Male Disorder":
  exists("Male Disorder Conditions")

/*
 * Primary Data Quality Rule
 */

define "Female Patient With Male Disorder":
  "Female Birth Sex"
    and "Has Male Disorder"

/*
 * Reporting Output
 */

define "Male Disorder Conditions Recorded For Female Patient":
  if "Female Birth Sex" then
    "Male Disorder Conditions"
  else
    { }

define "Male Disorder Count For Female Patient":
  if "Female Birth Sex" then
    Count("Male Disorder Conditions")
  else
    0

/*
 * Measure Population Expressions
 */

define "Initial Population":
  "Female Birth Sex" and exists("All Conditions")

define "Denominator":
  "Initial Population"

define "Numerator":
  not "Female Patient With Male Disorder"