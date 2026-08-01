using Robust.Shared.GameStates;

namespace Content.Shared.Heretic;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticAshBlindnessComponent : Component
{
    [DataField]
    public TimeSpan ExpireAt;

    [DataField, AutoNetworkedField]
    public float Intensity;
}
