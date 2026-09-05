using RailwaySimulator.Domain.Abstractions;
using RailwaySimulator.Domain.Entities;
using RailwaySimulator.Domain.Results;
using RailwaySimulator.Domain.ValueObjects;

namespace RailwaySimulator.Domain.Sections;

public sealed record Station(Time AlightingTime, Time BoardingTime, Speed EntrySpeedLimit) : IRouteSection
{
    public SectionPassResult Pass(Train train)
    {
        ArgumentNullException.ThrowIfNull(train);
        if (train.CurrentSpeed.Value > EntrySpeedLimit.Value + PhysicsConstants.KinematicsEpsilon)
            return new SectionPassResult.SpeedLimitExceeded(EntrySpeedLimit, train.CurrentSpeed);

        Speed arrivalSpeed = train.CurrentSpeed;
        var elapsed = new Time(AlightingTime.Value + BoardingTime.Value);
        if (arrivalSpeed.Value <= 0)
            return new SectionPassResult.Success(elapsed);

        SectionPassResult braking = train.ChangeSpeedTo(new Speed(0));
        if (braking is not SectionPassResult.Success brakingSuccess)
            return braking;

        SectionPassResult acceleration = train.ChangeSpeedTo(arrivalSpeed);
        if (acceleration is not SectionPassResult.Success accelerationSuccess)
            return acceleration;

        double totalElapsed = elapsed.Value + brakingSuccess.Elapsed.Value;
        if (!double.IsFinite(totalElapsed))
            return new SectionPassResult.CannotMove("The station elapsed time exceeded the supported numeric range.");

        totalElapsed += accelerationSuccess.Elapsed.Value;
        if (!double.IsFinite(totalElapsed))
            return new SectionPassResult.CannotMove("The station elapsed time exceeded the supported numeric range.");

        return new SectionPassResult.Success(new Time(totalElapsed));
    }
}
