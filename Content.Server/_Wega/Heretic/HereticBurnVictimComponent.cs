namespace Content.Server.Heretic;

[RegisterComponent]
public sealed partial class HereticBurnVictimComponent : Component
{
    [DataField]
    public float AccumulatedHeat;
}
