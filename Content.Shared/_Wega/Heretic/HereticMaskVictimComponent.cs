using Robust.Shared.GameStates;

namespace Content.Shared.Heretic;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class HereticMaskVictimComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Stacks;
}
