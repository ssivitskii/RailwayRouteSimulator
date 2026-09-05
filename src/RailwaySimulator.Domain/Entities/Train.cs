using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Entities;

public sealed class Train
{
    // Bounds all fixed-step work over this train's lifetime so a route cannot
    // multiply CPU cost by adding more sections or station maneuvers.
    private const int MaximumIntegrationSteps = 1_000_000;
    private readonly Precision _precision;
    private int _remainingIntegrationSteps = MaximumIntegrationSteps;

    public Train(Mass mass, Force maximumForce, Precision precision, Speed initialSpeed = default)
    {
        if (!double.IsFinite(mass.Value) || mass.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(mass), "Mass must be finite and greater than zero.");
        if (!double.IsFinite(maximumForce.Value) || maximumForce.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumForce), "Maximum force cannot be negative.");
        if (!double.IsFinite(precision.Value) || precision.Value <= 0)
            throw new ArgumentOutOfRangeException(nameof(precision), "Precision must be finite and greater than zero.");
        if (!double.IsFinite(initialSpeed.Value) || initialSpeed.Value < 0)
            throw new ArgumentOutOfRangeException(nameof(initialSpeed), "Initial speed must be finite and non-negative.");
        if (!double.IsFinite(maximumForce.Value / mass.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumForce),
                "Maximum force and mass must produce a finite acceleration.");
        }

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
            if (!TryConsumeIntegrationStep())
            {
                return new SectionPassResult.CannotMove(
                    "The train exhausted its shared integration-step safety limit.");
            }

            double speedDelta = acceleration * _precision.Value;
            double nextSpeed = speed + speedDelta;
            if (!double.IsFinite(speedDelta) || !double.IsFinite(nextSpeed))
                return new SectionPassResult.CannotMove("The traversal exceeded the supported numeric range.");

            speed = nextSpeed;
            if (speed <= 0)
                return new SectionPassResult.CannotMove("The train stopped before completing the section.");

            double stepDistance = speed * _precision.Value;
            if (!double.IsFinite(stepDistance))
                return new SectionPassResult.CannotMove("The traversal exceeded the supported numeric range.");

            if (stepDistance >= remainingDistance)
            {
                double elapsedIncrement = _precision.Value * (remainingDistance / stepDistance);
                if (!double.IsFinite(elapsedIncrement) || !double.IsFinite(elapsed + elapsedIncrement))
                    return new SectionPassResult.CannotMove("The traversal exceeded the supported numeric range.");

                elapsed += elapsedIncrement;
                remainingDistance = 0;
            }
            else
            {
                remainingDistance -= stepDistance;
                if (!double.IsFinite(elapsed + _precision.Value))
                    return new SectionPassResult.CannotMove("The traversal exceeded the supported numeric range.");

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
        if (!double.IsFinite(accelerationMagnitude))
            return new SectionPassResult.CannotMove("The speed change exceeded the supported numeric range.");

        double direction = target.Value > current ? 1 : -1;
        double elapsed = 0;

        while (Math.Abs(current - target.Value) >= PhysicsConstants.KinematicsEpsilon)
        {
            if (!TryConsumeIntegrationStep())
            {
                return new SectionPassResult.CannotMove(
                    "The train exhausted its shared integration-step safety limit.");
            }

            double timeToTarget = Math.Abs(target.Value - current) / accelerationMagnitude;
            double step = Math.Min(_precision.Value, timeToTarget);
            double speedDelta = direction * accelerationMagnitude * step;
            double nextSpeed = current + speedDelta;
            if (!double.IsFinite(timeToTarget)
                || !double.IsFinite(step)
                || !double.IsFinite(speedDelta)
                || !double.IsFinite(nextSpeed)
                || !double.IsFinite(elapsed + step))
            {
                return new SectionPassResult.CannotMove("The speed change exceeded the supported numeric range.");
            }

            current = nextSpeed;
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

    private bool TryConsumeIntegrationStep()
    {
        if (_remainingIntegrationSteps <= 0)
            return false;

        _remainingIntegrationSteps--;
        return true;
    }
}
