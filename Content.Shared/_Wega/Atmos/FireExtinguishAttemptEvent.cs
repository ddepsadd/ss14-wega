namespace Content.Shared._Wega.Atmos;

[ByRefEvent]
public struct FireExtinguishAttemptEvent(EntityUid target, float baseAdjustment)
{
    public readonly EntityUid Target = target;
    public readonly float BaseAdjustment = baseAdjustment;
    public bool Handled;
}
