using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;

namespace Content.Shared.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticRitualPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;

    [DataField] public ProtoId<HereticKnowledgePrototype>? RequiredKnowledge;

    [DataField] public List<RitualInput> Input = new();

    [DataField] public List<EntProtoId> Output = new();

    [DataField] public List<ProtoId<HereticKnowledgePrototype>> GrantKnowledge = new();

}

[DataDefinition]
public sealed partial class RitualInput
{
    [DataField]
    public EntProtoId? Proto;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public int Amount = 1;
}
