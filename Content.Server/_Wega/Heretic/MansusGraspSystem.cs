using System.Text;
using Content.Server.Chat.Systems;
using Content.Shared.Actions;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.Interaction;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Heretic;

public sealed partial class MansusGraspSystem : EntitySystem
{
    [Dependency] private SharedStunSystem _stun = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private SharedHandsSystem _hands = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HereticComponent, HereticMansusGraspEvent>(OnMansusGrasp);
        SubscribeLocalEvent<MansusGraspComponent, AfterInteractEvent>(OnGraspInteract);
    }
    private void OnMansusGrasp(EntityUid uid, HereticComponent component, HereticMansusGraspEvent args)
    {
        if (args.Handled)
            return;

        // грасп активен → повторное нажатие отменяет
        if (component.ActiveGrasp != EntityUid.Invalid)
        {
            QueueDel(component.ActiveGrasp);
            component.ActiveGrasp = EntityUid.Invalid;
            args.Handled = true;
            return;
        }

        // призыв
        if (TryGiveGrasp(uid, component.MansusGraspProto, out var grasp))
        {
            component.ActiveGrasp = grasp;
            args.Handled = true;
        }
    }

    private bool TryGiveGrasp(EntityUid uid, EntProtoId proto, out EntityUid grasp)
    {
        grasp = EntityUid.Invalid;
        if (!TryComp<HandsComponent>(uid, out var hands))
            return false;
        if (_hands.CountFreeHands((uid, hands)) == 0)
        {
            _popup.PopupEntity(Loc.GetString("heretic-grasp-no-hands"), uid, uid, PopupType.SmallCaution);
            return false;
        }
        grasp = Spawn(proto, Transform(uid).Coordinates);   // ← proto, не константа
        if (!_hands.TryPickupAnyHand(uid, grasp))
        {
            QueueDel(grasp);
            grasp = EntityUid.Invalid;
            return false;
        }
        return true;
    }

    private string Garble(string message, float readable)
    {
        var sb = new StringBuilder(message);
        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace(sb[i]))
                continue;
            if (_random.Prob(1f - readable))
                sb[i] = '~';
        }
        return sb.ToString();
    }

    private void OnGraspInteract(Entity<MansusGraspComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { Valid: true } target)
            return;
        if (!HasComp<MobStateComponent>(target) || HasComp<HereticComponent>(target))
            return;
        var user = args.User;

        if (ent.Comp.Invocations.Count > 0)
        {
            var phrase = _random.Pick(ent.Comp.Invocations);
            _chat.TrySendInGameICMessage(user, Garble(phrase, 0.5f), InGameICChatType.Speak, false);
        }

        _stun.TryKnockdown(target, ent.Comp.KnockdownTime);

        _damage.TryChangeDamage(target, ent.Comp.Damage, true);

        var ev = new MansusGraspHitEvent(user, target);
        RaiseLocalEvent(user, ref ev);

        if (TryComp<HereticComponent>(user, out var heretic) && heretic.GraspAction != EntityUid.Invalid)
        {
            _actions.SetCooldown(heretic.GraspAction, ent.Comp.CooldownAfterUse);
            heretic.ActiveGrasp = EntityUid.Invalid;
        }

        // расход — грасп исчезает
        QueueDel(ent);
        args.Handled = true;
    }
}
