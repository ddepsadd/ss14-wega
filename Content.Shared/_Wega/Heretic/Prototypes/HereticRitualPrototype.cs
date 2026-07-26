using Robust.Shared.Prototypes;

namespace Content.Shared.Heretic.Prototypes;

[Prototype]
public sealed partial class HereticRitualPrototype : IPrototype
{
    [IdDataField] public string ID { get; private set; } = default!;
}
