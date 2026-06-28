using Content.Shared.Heretic.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticComponent : Component
{
    [DataField]
    public EntityUid MansusGraspAction = EntityUid.Invalid;

    [DataField]
    public EntityUid ActiveGrasp = EntityUid.Invalid;

    [DataField]
    public EntityUid GraspAction = EntityUid.Invalid;

    [DataField]
    public EntityUid StoreAction = EntityUid.Invalid;

    [DataField]
    public int KnowledgePoints;

    [DataField]
    public ProtoId<HereticPathPrototype>? CurrentPath;

    [DataField]
    public int PathStage;

    [DataField]
    public bool Ascended;

    [DataField]
    public List<EntityUid> SacrificeTargets = new();

    [DataField]
    public List<ProtoId<HereticKnowledgePrototype>> KnownKnowledge = new();

    [DataField]
    public EntProtoId MansusGraspProto = "MansusGrasp";
}
