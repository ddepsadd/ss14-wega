using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent]
public sealed partial class HereticRuleComponent : Component
{
    [DataField]
    public EntProtoId MansusGraspAction = "ActionMansusGrasp";

    [DataField]
    public EntProtoId StoreAction = "ActionHereticOpenStore";
}
