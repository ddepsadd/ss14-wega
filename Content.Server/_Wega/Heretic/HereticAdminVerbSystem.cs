using Content.Server.Administration.Managers;
using Content.Server.Antag;
using Content.Server.GameTicking.Rules.Components;
using Content.Shared.Database;
using Content.Shared.Verbs;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Server.Heretic;

public sealed partial class HereticAdminVerbSystem : EntitySystem
{
    [Dependency] private IAdminManager _admin = default!;
    [Dependency] private AntagSelectionSystem _antag = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ActorComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<ActorComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!_admin.IsAdmin(args.User))
            return;

        var session = ent.Comp.PlayerSession;
        args.Verbs.Add(new Verb
        {
            Text = "Make Heretic",
            Icon = new SpriteSpecifier.Rsi(new ("/Textures/_Wega/Heretic/abilities_heretic.rsi"), "mansus_grasp"),
            Category = VerbCategory.Antag,
            Act = () => _antag.ForceMakeAntag<HereticRuleComponent>(session, "Heretic"),
            Impact = LogImpact.High,
        });
    }
}
