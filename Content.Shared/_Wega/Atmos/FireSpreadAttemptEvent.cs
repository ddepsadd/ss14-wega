namespace Content.Shared.Atmos;

[ByRefEvent]
public struct FireSpreadAttemptEvent(EntityUid first, EntityUid second)
{
    public readonly EntityUid First = first;
    public readonly EntityUid Second = second;
    public bool Cancelled;
}
