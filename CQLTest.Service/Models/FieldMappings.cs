namespace CQLTest.Service;

/// <summary>
/// Maps the six standard measure components to the actual CQL expression names
/// returned by a given library. Defaults mirror the canonical component names.
/// </summary>
public class FieldMappings
{
    public string InitialPopulation    { get; set; } = "InitialPopulation";
    public string Denominator          { get; set; } = "Denominator";
    public string DenominatorExclusion { get; set; } = "DenominatorExclusion";
    public string DenominatorException { get; set; } = "DenominatorException";
    public string Numerator            { get; set; } = "Numerator";
    public string NumeratorExclusion   { get; set; } = "NumeratorExclusion";

    public FieldMappings Clone() => (FieldMappings)MemberwiseClone();

    /// <summary>Ordered metadata for each standard field.</summary>
    public static readonly (string PropertyName, string Label)[] Fields =
    [
        (nameof(InitialPopulation),    "Initial Population"),
        (nameof(Denominator),          "Denominator"),
        (nameof(DenominatorExclusion), "Denominator Exclusion"),
        (nameof(DenominatorException), "Denominator Exception"),
        (nameof(Numerator),            "Numerator"),
        (nameof(NumeratorExclusion),   "Numerator Exclusion"),
    ];

    public string GetByName(string propertyName) => propertyName switch
    {
        nameof(InitialPopulation)    => InitialPopulation,
        nameof(Denominator)          => Denominator,
        nameof(DenominatorExclusion) => DenominatorExclusion,
        nameof(DenominatorException) => DenominatorException,
        nameof(Numerator)            => Numerator,
        nameof(NumeratorExclusion)   => NumeratorExclusion,
        _ => throw new ArgumentException($"Unknown field: {propertyName}")
    };

    public void SetByName(string propertyName, string value)
    {
        switch (propertyName)
        {
            case nameof(InitialPopulation):    InitialPopulation    = value; break;
            case nameof(Denominator):          Denominator          = value; break;
            case nameof(DenominatorExclusion): DenominatorExclusion = value; break;
            case nameof(DenominatorException): DenominatorException = value; break;
            case nameof(Numerator):            Numerator            = value; break;
            case nameof(NumeratorExclusion):   NumeratorExclusion   = value; break;
        }
    }
}
