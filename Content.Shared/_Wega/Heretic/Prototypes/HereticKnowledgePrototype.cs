using Content.Shared.Heretic;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticKnowledgePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField]
    public LocId Name;

    [DataField]
    public LocId Description;

    [DataField]
    public int Cost = 1;

    [DataField]
    public List<ProtoId<HereticKnowledgePrototype>> Prerequisites = new();

    [DataField]
    public ProtoId<HereticPathPrototype>? Path;

    [DataField]
    public int Stage;

    [DataField]
    public List<EntProtoId> Actions = new();

    [DataField]
    public ComponentRegistry Components = new();

    [DataField]
    public bool SideKnowledge;

    [DataField]
    public List<ProtoId<HereticKnowledgePrototype>> ConflictsWith = new();

    [DataField]
    public SpriteSpecifier? Icon;
}
