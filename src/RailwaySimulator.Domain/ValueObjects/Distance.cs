namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Distance
{
    public Distance(double value)
    {
        if (!double.IsFinite(value) || value <= 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Distance must be finite and greater than zero.");

        Value = value;
    }

    public double Value { get; }
}
