namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Acceleration
{
    public Acceleration(double value)
    {
        if (!double.IsFinite(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Acceleration must be finite.");

        Value = value;
    }

    public double Value { get; }
}
