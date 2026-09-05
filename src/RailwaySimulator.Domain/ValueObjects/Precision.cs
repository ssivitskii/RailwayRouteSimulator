namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Precision
{
    public Precision(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Precision must be finite and greater than zero.");

        Value = value;
    }

    public double Value { get; }
}
