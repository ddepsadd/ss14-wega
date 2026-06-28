using Content.Server.EUI;
using Content.Shared.Actions;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.Heretic.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Server.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.Heretic;

public sealed partial class HereticKnowledgeSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private EuiManager _euiMan = default!;

    private readonly Dictionary<EntityUid, HereticStoreEui> _openStores = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, HereticOpenStoreEvent>(OnOpenStore);
    }

    private void OnOpenStore(EntityUid uid, HereticComponent comp, HereticOpenStoreEvent args)
    {
        args.Handled = true;

        if (_openStores.TryGetValue(uid, out var existing))
        {
            existing.Close();
            _openStores.Remove(uid);
            return;
        }

        if (!TryComp<ActorComponent>(uid, out var actor))
            return;

        var eui = new HereticStoreEui(uid, this);
        _euiMan.OpenEui(eui, actor.PlayerSession);
        _openStores[uid] = eui;
    }

    public void OnStoreClosed(EntityUid uid)
    {
        _openStores.Remove(uid);
    }

    private void ApplyPathTheme(EntityUid uid, ProtoId<HereticPathPrototype> path, HereticComponent heretic)
    {
        if (!_proto.TryIndex(path, out var proto))
            return;

        heretic.MansusGraspProto = proto.GraspProto;

        if (proto.GraspIcon != null && heretic.GraspAction != EntityUid.Invalid)
            _actions.SetIcon(heretic.GraspAction, proto.GraspIcon);

        if (proto.StoreIcon != null && heretic.StoreAction != EntityUid.Invalid)
            _actions.SetIcon(heretic.StoreAction, proto.StoreIcon);
    }

    private bool Conflicts(HereticKnowledgePrototype node, HereticComponent heretic)
    {
        foreach (var ownedId in heretic.KnownKnowledge)
        {
            if (node.ConflictsWith.Contains(ownedId))
                return true;

            if (_proto.TryIndex(ownedId, out var owned) && owned.ConflictsWith.Contains(node.ID))
                return true;
        }
        return false;
    }

    public bool HasKnowledge(EntityUid uid, ProtoId<HereticKnowledgePrototype> id, HereticComponent? heretic = null)
        => Resolve(uid, ref heretic, false) && heretic.KnownKnowledge.Contains(id);

    public bool TryPurchase(EntityUid uid, ProtoId<HereticKnowledgePrototype> nodeId, HereticComponent? heretic = null)
    {
        if (!Resolve(uid, ref heretic))
            return false;
        if (!_proto.TryIndex(nodeId, out var node))
            return false;

        if (heretic.KnownKnowledge.Contains(nodeId))
            return false;
        if (heretic.KnowledgePoints < node.Cost)
            return false;
        foreach (var prereq in node.Prerequisites)
            if (!heretic.KnownKnowledge.Contains(prereq))
                return false;
        if (Conflicts(node, heretic))
            return false;
        if (node.Path is { } nodePath
            && heretic.CurrentPath is { } cur
            && cur != nodePath)
            return false;

        heretic.KnowledgePoints -= node.Cost;

        foreach (var action in node.Actions)
            _actions.AddAction(uid, action);

        if (node.Components.Count > 0)
            EntityManager.AddComponents(uid, node.Components);

        if (node.Path is { } p && heretic.CurrentPath == null)
        {
            heretic.CurrentPath = p;
            ApplyPathTheme(uid, p, heretic);
        }

        heretic.KnownKnowledge.Add(nodeId);

        var ev = new HereticNodePurchasedEvent(uid, nodeId);
        RaiseLocalEvent(uid, ref ev);

        return true;
    }

    public HereticStoreState BuildStoreState(EntityUid uid, HereticComponent? heretic = null)
    {
        if (!Resolve(uid, ref heretic))
            return new HereticStoreState();

        var nodes = new List<HereticStoreNode>();
        foreach (var proto in _proto.EnumeratePrototypes<HereticKnowledgePrototype>())
        {
            if (!IsRelevant(proto, heretic))
                continue;

            nodes.Add(new HereticStoreNode
            {
                Id = proto.ID,
                Name = proto.Name,
                Description = proto.Description,
                Cost = proto.Cost,
                Path = proto.Path?.Id,
                Stage = proto.Stage,
                SideKnowledge = proto.SideKnowledge,
                Status = GetStatus(proto, heretic),
            });
        }

        var paths = new List<HereticStorePath>();
        foreach (var p in _proto.EnumeratePrototypes<HereticPathPrototype>())
        {
            paths.Add(new HereticStorePath
            {
                Id = p.ID,
                Name = p.Name,
                EntryNode = p.EntryNode,
                Color = p.Color.ToHex(),
                Chosen = heretic.CurrentPath is { } cp && cp.Id == p.ID,
            });
        }

        return new HereticStoreState
        {
            Points = heretic.KnowledgePoints,
            CurrentPath = heretic.CurrentPath?.Id,
            Nodes = nodes,
            Paths = paths,
        };
    }

    private bool IsRelevant(HereticKnowledgePrototype node, HereticComponent heretic)
    {
        if (node.Path is not { } p)
            return true;
        if (heretic.CurrentPath is not { } cur)
            return false;
        return cur == p;
    }

    private HereticNodeStatus GetStatus(HereticKnowledgePrototype node, HereticComponent heretic)
    {
        if (heretic.KnownKnowledge.Contains(node.ID))
            return HereticNodeStatus.Owned;

        if (Conflicts(node, heretic))
            return HereticNodeStatus.Conflicted;

        foreach (var prereq in node.Prerequisites)
            if (!heretic.KnownKnowledge.Contains(prereq))
                return HereticNodeStatus.Locked;
        return HereticNodeStatus.Available;
    }
}
