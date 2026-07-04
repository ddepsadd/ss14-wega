namespace Content.Server.Heretic;

[RegisterComponent]
public sealed partial class HereticMantleComponent : Component
{
    [DataField]
    public bool Active;

    [DataField]
    public TimeSpan RevealUntil;
}
