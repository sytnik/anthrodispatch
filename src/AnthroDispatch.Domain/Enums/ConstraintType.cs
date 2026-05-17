namespace AnthroDispatch.Domain.Enums;

public enum ConstraintType
{
    UnavailablePeriod,
    UnavailableDay,
    AvoidFirstPeriod,
    AvoidLatePeriods,
    MaxClassesPerDay,
    MaxConsecutiveClasses,
    RequiredBreakAfterClass,
    PreferredPeriods,
    RoomOrBuildingRestriction,
    OnlineOnly,
    HealthRelated
}