namespace Content.Server.Heretic;

[RegisterComponent]
public sealed partial class HereticFuryComponent : Component
{
    [DataField]
    public bool Active;

    [DataField]
    public EntityUid? ActionEntity;
}
