using Content.Shared.Heretic.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Heretic.Prototypes;
[Prototype]
public sealed partial class HereticPathPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;
    [DataField]
    public LocId Name;
    [DataField]
    public Color Color = Color.White;
    [DataField]
    public EntProtoId GraspProto = "MansusGrasp";
    [DataField]
    public SpriteSpecifier? GraspIcon;
    [DataField]
    public SpriteSpecifier? StoreIcon;
    [DataField]
    public ProtoId<HereticKnowledgePrototype> EntryNode;
    [DataField]
    public SpriteSpecifier? Icon;
}
