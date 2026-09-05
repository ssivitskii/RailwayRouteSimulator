namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Mass
{
    public Mass(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Mass must be finite and greater than zero.");

        Value = value;
    }

    public double Value { get; }
}
