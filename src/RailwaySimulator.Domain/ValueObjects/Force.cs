namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Force
{
    public Force(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Force must be finite.");

        Value = value;
    }

    public double Value { get; }
}
