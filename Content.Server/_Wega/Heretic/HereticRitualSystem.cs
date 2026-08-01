using System.Linq;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Content.Shared.Heretic.Prototypes;

namespace Content.Server.Heretic;

public sealed partial class HereticRitualSystem : EntitySystem
{
    private const float Range = 1.5f;

    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private EntityWhitelistSystem _whitelist = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private HereticKnowledgeSystem _knowledge = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HereticRuneComponent, InteractHandEvent>(OnRuneInteract);
    }

    private void OnRuneInteract(EntityUid uid, HereticRuneComponent comp, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        var user = args.User;
        if (!HasComp<HereticComponent>(user))
            return;

        var runeCoordinates = Transform(uid).Coordinates;
        var entitiesInRange = _lookup.GetEntitiesInRange(runeCoordinates, Range, LookupFlags.Uncontained);

        var recipes = _proto.EnumeratePrototypes<HereticRitualPrototype>()
            .Where(r => r.RequiredKnowledge == null || _knowledge.HasKnowledge(user, r.RequiredKnowledge.Value))
            .OrderByDescending(r => r.Input.Count);

        foreach (var recipe in recipes)
        {
            if (!TryMatch(recipe, entitiesInRange, out var toConsume))
                continue;

            foreach (var item in toConsume)
            {
                QueueDel(item);
            }

            foreach (var output in recipe.Output)
            {
                Spawn(output, runeCoordinates);
            }

            args.Handled = true;
            return;
        }
    }

    private bool ItemsMatches(RitualInput input, EntityUid entity)
    {
        if (input.Proto != null)
        {
                return (MetaData(entity).EntityPrototype?.ID == input.Proto.Value.Id);
        }
        else if (input.Whitelist != null)
        {
                return _whitelist.IsValid(input.Whitelist, entity);
        }
        return false;
    }

    private bool TryMatch(HereticRitualPrototype recipe, HashSet<EntityUid> items, out List<EntityUid> toConsume)
    {
        toConsume = new List<EntityUid>();

        foreach (var input in recipe.Input)
        {
            var found = 0;
            foreach (var item in items)
            {
                if (toConsume.Contains(item))
                    continue;

                if (ItemsMatches(input, item))
                {
                    toConsume.Add(item);
                    found++;
                    if (found == input.Amount)
                        break;
                }
            }
            if (found < input.Amount)
                return false;
        }
        return true;
    }
}
