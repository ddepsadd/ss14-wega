using Content.Server.Actions;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Heretic.Components;

namespace Content.Server.GameTicking.Rules;

public sealed partial class HereticRuleSystem : GameRuleSystem<HereticRuleComponent>
{
    [Dependency] private AntagSelectionSystem _antag = default!;
    [Dependency] private ActionsSystem _action = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticRuleComponent, AfterAntagEntitySelectedEvent>(OnHereticSelected);
    }

    private void OnHereticSelected(Entity<HereticRuleComponent> ent, ref AfterAntagEntitySelectedEvent args)
    {
        _antag.SendBriefing(args.EntityUid, Loc.GetString("heretic-role-greeting"), Color.Purple, null);

        if (TryComp<HereticComponent>(args.EntityUid, out var h))
        {
            h.GraspAction = _action.AddAction(args.EntityUid, ent.Comp.MansusGraspAction) ?? EntityUid.Invalid;
            h.StoreAction = _action.AddAction(args.EntityUid, ent.Comp.StoreAction) ?? EntityUid.Invalid;
        }
    }
}
