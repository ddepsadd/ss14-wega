using Content.Shared.Actions;
using Content.Shared.Heretic.Prototypes;
using Robust.Shared.Prototypes;
using Content.Shared.Eui;
using Content.Shared.Heretic.Components;
using Robust.Shared.Serialization;

namespace Content.Shared.Heretic;

public sealed partial class HereticMansusGraspEvent : InstantActionEvent
{
}

[ByRefEvent]
public readonly record struct MansusGraspHitEvent(EntityUid Heretic, EntityUid Target);

[ByRefEvent]
public readonly record struct HereticNodePurchasedEvent(EntityUid Heretic, ProtoId<HereticKnowledgePrototype> Node);

public sealed partial class HereticOpenStoreEvent : InstantActionEvent { }

public sealed partial class HereticAshExplosionEvent : InstantActionEvent { }

[Serializable, NetSerializable]
public enum HereticNodeStatus : byte { Owned, Available, Locked, Conflicted }

[Serializable, NetSerializable]
public struct HereticStoreNode
{
    public string Id;
    public string Name;
    public string Description;
    public int Cost;
    public string? Path;
    public int Stage;
    public bool SideKnowledge;
    public HereticNodeStatus Status;
}

[Serializable, NetSerializable]
public struct HereticStorePath
{
    public string Id;
    public string Name;
    public string EntryNode;
    public string Color;
    public bool Chosen;
}

[Serializable, NetSerializable]
public sealed class HereticStoreState : EuiStateBase
{
    public int Points;
    public string? CurrentPath;  // null = путь не выбран
    public List<HereticStoreNode> Nodes = new();
    public List<HereticStorePath> Paths = new();
}

[Serializable, NetSerializable]
public sealed partial class HereticBuyKnowledgeMessage(ProtoId<HereticKnowledgePrototype> node) : EuiMessageBase
{
    public readonly ProtoId<HereticKnowledgePrototype> Node = node;
}

[Serializable, NetSerializable]
public sealed class HereticStoreCloseMessage : EuiMessageBase
{
}
