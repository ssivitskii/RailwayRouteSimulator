using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Entities;

public sealed class Train
{
    private readonly Precision _precision;

    public Train(Mass mass, Force maximumForce, Precision precision, Speed initialSpeed = default)
    {
        if (maximumForce.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumForce), "Maximum force cannot be negative.");

        Mass = mass;
        MaximumForce = maximumForce;
        _precision = precision;
        CurrentSpeed = initialSpeed;
        CurrentAcceleration = new Acceleration(0);
    }

    public Speed CurrentSpeed { get; private set; }

    public Acceleration CurrentAcceleration { get; private set; }

    public Mass Mass { get; }

    public Force MaximumForce { get; }

    public ApplyForceResult ApplyForce(Force force)
    {
        var requested = new Force(Math.Abs(force.Value));
        if (requested.Value > MaximumForce.Value)
            return new ApplyForceResult.ExceedsLimit(requested, MaximumForce);

        CurrentAcceleration = new Acceleration(force.Value / Mass.Value);
        return new ApplyForceResult.Success(CurrentAcceleration);
    }

    public void ResetAcceleration()
    {
        CurrentAcceleration = new Acceleration(0);
    }

    public SectionPassResult Traverse(Distance distance)
    {
        double remainingDistance = distance.Value;
        double speed = CurrentSpeed.Value;
        double acceleration = CurrentAcceleration.Value;
        double elapsed = 0;

        if (speed <= 0 && Math.Abs(acceleration) < PhysicsConstants.KinematicsEpsilon)
            return new SectionPassResult.CannotMove("The train has neither speed nor acceleration.");

        while (remainingDistance > 0)
        {
            speed += acceleration * _precision.Value;
            if (speed <= 0)
                return new SectionPassResult.CannotMove("The train stopped before completing the section.");

            double stepDistance = speed * _precision.Value;
            if (stepDistance >= remainingDistance)
            {
                elapsed += _precision.Value * (remainingDistance / stepDistance);
                remainingDistance = 0;
            }
            else
            {
                remainingDistance -= stepDistance;
                elapsed += _precision.Value;
            }

            CurrentSpeed = new Speed(speed);
        }

        return new SectionPassResult.Success(new Time(elapsed));
    }

    public SectionPassResult ChangeSpeedTo(Speed target)
    {
        double current = CurrentSpeed.Value;
        if (Math.Abs(current - target.Value) < PhysicsConstants.KinematicsEpsilon)
        {
            CurrentSpeed = target;
            ResetAcceleration();
            return new SectionPassResult.Success(new Time(0));
        }

        if (MaximumForce.Value <= 0)
            return new SectionPassResult.CannotMove("The train cannot change speed because its maximum force is zero.");

        double accelerationMagnitude = MaximumForce.Value / Mass.Value;
        double direction = target.Value > current ? 1 : -1;
        double elapsed = 0;

        while (Math.Abs(current - target.Value) >= PhysicsConstants.KinematicsEpsilon)
        {
            double timeToTarget = Math.Abs(target.Value - current) / accelerationMagnitude;
            double step = Math.Min(_precision.Value, timeToTarget);
            current += direction * accelerationMagnitude * step;
            elapsed += step;

            if (current < -PhysicsConstants.KinematicsEpsilon)
                return new SectionPassResult.CannotMove("The maneuver would produce a negative speed.");

            CurrentSpeed = new Speed(Math.Max(0, current));
            CurrentAcceleration = new Acceleration(direction * accelerationMagnitude);
        }

        CurrentSpeed = target;
        ResetAcceleration();
        return new SectionPassResult.Success(new Time(elapsed));
    }
}
