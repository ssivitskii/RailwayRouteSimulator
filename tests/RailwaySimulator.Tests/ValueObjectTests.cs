using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Tests;

public sealed class ValueObjectTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void MassWithNonPositiveOrNonFiniteValueThrows(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Mass(value));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(double.NegativeInfinity)]
    public void PrecisionWithNonPositiveOrNonFiniteValueThrows(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Precision(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void SpeedWithInvalidValueThrows(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Speed(value));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    public void TimeWithInvalidValueThrows(double value)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Time(value));
    }
}
