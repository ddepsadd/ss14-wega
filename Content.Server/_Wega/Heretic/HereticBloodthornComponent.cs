namespace Content.Server.Heretic;

[RegisterComponent]
public sealed partial class HereticBloodthornComponent : Component
{
    [DataField]
    public EntityUid Heretic;

    [DataField]
    public TimeSpan ExpireAt;

    [DataField]
    public float Accumulated;
}
