library PlausibilityMaleBirthSexandFemaleCondition version '1.0.0'

using PIQI version '1.0.0'

/*
* Value Sets
*/

valueset "Female Disorders, All": 'FEMALE_DISORDERS_ALL'

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

define "Male Birth Sex":
  HasCodeInList(
    Patient.demographics.birthSex,
    { 'male' }
  )

/*
 * Conditions
 */

define "All Conditions":
  Patient.conditions

define "Female Disorder Conditions":
  "All Conditions" C
    where ConceptInValueSet(
      C.condition,
      "Female Disorders, All"
    )

define "Has Female Disorder":
  exists("Female Disorder Conditions")

/*
 * Primary Data Quality Rule
 */

define "Male Patient With Female Disorder":
  "Male Birth Sex"
    and "Has Female Disorder"

/*
 * Reporting Output
 */

define "Female Disorder Conditions Recorded For Male Patient":
  if "Male Birth Sex" then
    "Female Disorder Conditions"
  else
    { }

define "Female Disorder Count For Male Patient":
  if "Male Birth Sex" then
    Count("Female Disorder Conditions")
  else
    0

/*
 * Measure Population Expressions
 */

define "Initial Population":
  "Male Birth Sex" and exists("All Conditions")

define "Denominator":
  "Initial Population"

define "Numerator":
  not "Male Patient With Female Disorder"