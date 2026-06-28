using Content.Shared.Damage;

namespace Content.Shared.Heretic.Components;

[RegisterComponent]
public sealed partial class MansusGraspComponent : Component
{
    [DataField]
    public TimeSpan CooldownAfterUse = TimeSpan.FromSeconds(15);

    [DataField]
    public TimeSpan KnockdownTime = TimeSpan.FromSeconds(3);

    [DataField]
    public DamageSpecifier Damage = new() { DamageDict = { { "Heat", 10 } } };

    [DataField]
    public List<string> Invocations = new();
}
