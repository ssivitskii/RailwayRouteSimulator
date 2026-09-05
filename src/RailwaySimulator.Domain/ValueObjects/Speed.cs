namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Speed
{
    public Speed(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Speed must be finite and non-negative.");

        Value = value;
    }

    public double Value { get; }
}
