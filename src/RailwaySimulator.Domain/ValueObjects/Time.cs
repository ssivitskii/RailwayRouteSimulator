namespace RailwaySimulator.Domain.ValueObjects;

public readonly record struct Time
{
    public Time(double value)
    {
        if (!double.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), "Time must be finite and non-negative.");

        Value = value;
    }

    public double Value { get; }
}
