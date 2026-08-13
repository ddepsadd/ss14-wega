using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Damage.Systems;
using Content.Server.Hallucinations;
using Content.Server.Polymorph.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Actions;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.IdentityManagement.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Heretic;

public sealed partial class AshSystem : EntitySystem
{
    [Dependency] private SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private FlammableSystem _flammable = default!;
    [Dependency] private EntityLookupSystem _lookup = default!;
    [Dependency] private HereticKnowledgeSystem _knowledge = default!;
    [Dependency] private MovementSpeedModifierSystem _movement = default!;
    [Dependency] private DamageableSystem _damage = default!;
    [Dependency] private MobStateSystem _mobState = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private PolymorphSystem _polymorph = default!;
    [Dependency] private SharedStealthSystem _stealth = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private StaminaSystem _stamina = default!;
    [Dependency] private IdentitySystem _identity = default!;
    [Dependency] private ExamineSystemShared _examine = default!;
    [Dependency] private HallucinationsSystem _hallucinations = default!;

    private const float GraspFireStacks = 1f;
    private const float FlamesFireStacks = 2f;
    private const float FlamesRange = 3f;

    private const float FurySelf = 1f;
    private const float FurySpeedBase = 1.15f;
    private const float FuryAttackBase = 1.15f;
    private const float FuryHealPerHit = 3f;

    private const float FuryBurnPerHit = 10f;

    private const float MantleTickInterval = 2f;
    private const float MantleSpeedBase = 1.2f;
    private const float MantleRevealSeconds = 3f;
    private const float MantleVisibility = 0.2f;

    private const float MaskSpeedBase = 1.2f;
    private const float MaskAuraRange = 4f;
    private const float MaskCloseRange = 1.5f;
    private const float MaskStackMax = 8f;
    private const float MaskGainPerSec = 1f;
    private const float MaskGainClose = 2.5f;
    private const float MaskDecayPerSec = 1f;
    private const float MaskDamageFactor = 0.05f;
    private const float MaskHitConsume = 1f;
    private const float HitHallucinationSeconds = 1f;
    private const float GraspHallucinationSeconds = 5f;

    private const float GibThreshold = 1500f;
    private const float GibBuffer = 35f;
    private const float GibFadeZone = 300f;
    private const float SafeTemp = 350f;
    private const float GibFadeRate = 3f;

    private const float JauntBlindRange = 3f;
    private const float JauntBlindRampSeconds = 1.0f;
    private const float JauntBlindRefresh = 2.0f;
    private const float JauntBlindFadeSeconds = 5f;
    private const float JauntStaminaPerSecond = 10f;
    private const float MantleShiftCooldown = 15f;

    private const float BloodthornSeconds = 3f;
    private const float BloodthornFraction = 0.75f;

    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private readonly Dictionary<EntityUid, float> _mantleAccumulator = new();
    private readonly List<EntityUid> _bloodthornRemove = new();
    private readonly List<EntityUid> _ashBlindRemove = new();
    private readonly HashSet<EntityUid> _maskTouched = new();
    private readonly List<EntityUid> _maskVictimRemove = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, MansusGraspHitEvent>(OnGraspHit);
        SubscribeLocalEvent<HereticComponent, HereticAshExplosionEvent>(OnAshFlames);
        SubscribeLocalEvent<HereticComponent, GetFireProtectionEvent>(OnFireProtection);
        SubscribeLocalEvent<HereticComponent, BeforeHeatExchangeEvent>(OnTempChange);
        SubscribeLocalEvent<HereticComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<HereticFuryComponent, ToggleActionEvent>(OnFuryToggle);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnAttackRate);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FlammableComponent, FireSpreadAttemptEvent>(OnFireSpread);
        SubscribeLocalEvent<HereticBurnVictimComponent, DamageChangedEvent>(OnVictimDamage);
        SubscribeLocalEvent<HereticComponent, HereticNodePurchasedEvent>(OnNodePurchased);
        SubscribeLocalEvent<HereticComponent, HereticAshShiftEvent>(OnAshShift);
        SubscribeLocalEvent<HereticMantleComponent, ComponentInit>(OnMantleInit);
        SubscribeLocalEvent<HereticMantleComponent, ComponentShutdown>(OnMantleShutdown);
        SubscribeLocalEvent<HereticMantleComponent, ToggleActionEvent>(OnMantleToggle);
        SubscribeLocalEvent<HereticBloodthornComponent, DamageChangedEvent>(OnBloodthornDamage);
        SubscribeLocalEvent<HereticMaskComponent, ToggleActionEvent>(OnMaskToggle);
        SubscribeLocalEvent<HereticMaskVictimComponent, DamageModifyEvent>(OnMaskVictimDamage);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var furyQuery = EntityQueryEnumerator<HereticFuryComponent, FlammableComponent>();
        while (furyQuery.MoveNext(out var uid, out var fury, out var fl))
        {
            if (fury.Active && fl.FireStacks < FurySelf)
                _flammable.AdjustFireStacks(uid, FurySelf - fl.FireStacks, fl, ignite: true);
        }
        var mantleQuery = EntityQueryEnumerator<HereticComponent>();
        while (mantleQuery.MoveNext(out var uid, out _))
        {
            if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
                TickMantleHeal(uid, frameTime);
        }
        var query = EntityQueryEnumerator<HereticAshJauntFormComponent>();
        while (query.MoveNext(out var formUid, out var jauntForm))
        {
            var coords = Transform(formUid).Coordinates;

            if (jauntForm.MantleAura)
            {
                foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, JauntBlindRange))
                {
                    if (other.Owner == formUid)
                        continue;
                    if (HasComp<HereticComponent>(other))
                        continue;
                    if (_mobState.IsDead(other))
                        continue;

                    _stamina.TakeStaminaDamage(other, JauntStaminaPerSecond * frameTime, source: formUid, visual: false);

                    var blind = EnsureComp<HereticAshBlindnessComponent>(other);
                    blind.Intensity = MathF.Min(1f, blind.Intensity + frameTime / JauntBlindRampSeconds);
                    blind.ExpireAt = _timing.CurTime + TimeSpan.FromSeconds(JauntBlindRefresh);
                    Dirty(other, blind);
                }
            }
        }

        var blindQuery = EntityQueryEnumerator<HereticAshBlindnessComponent>();
        while (blindQuery.MoveNext(out var buid, out var blind))
        {
            if (_timing.CurTime <= blind.ExpireAt)
                continue;
            blind.Intensity -= frameTime / JauntBlindFadeSeconds;
            if (blind.Intensity <= 0f)
            {
                blind.Intensity = 0f;
                Dirty(buid, blind);
                _ashBlindRemove.Add(buid);
            }
            else
                Dirty(buid, blind);
        }

        foreach (var u in _ashBlindRemove)
        {
            RemComp<HereticAshBlindnessComponent>(u);
        }
        _ashBlindRemove.Clear();

        var revealQuery = EntityQueryEnumerator<HereticMantleComponent>();
        while (revealQuery.MoveNext(out var uid, out var mantle))
        {
            if (mantle.RevealUntil == default)
                continue;
            if (_timing.CurTime < mantle.RevealUntil)
                continue;

            if (mantle.Active && TryComp<StealthComponent>(uid, out var stealth))
                _stealth.SetVisibility(uid, MantleVisibility, stealth);
            mantle.RevealUntil = default;
        }
        var bloodthornQuery = EntityQueryEnumerator<HereticBloodthornComponent>();
        while (bloodthornQuery.MoveNext(out var uid, out var mark))
        {
            if (_timing.CurTime < mark.ExpireAt)
                continue;

            if (mark.Accumulated > 0f)
            {
                var burn = new DamageSpecifier();
                burn.DamageDict["Heat"] = FixedPoint2.New(mark.Accumulated * BloodthornFraction);
                _damage.TryChangeDamage(uid, burn, true);
            }
            _bloodthornRemove.Add(uid);
        }

        foreach (var toRemove in _bloodthornRemove)
        {
            RemComp<HereticBloodthornComponent>(toRemove);
        }
        _bloodthornRemove.Clear();

        _maskTouched.Clear();
        var maskQuery = EntityQueryEnumerator<HereticMaskComponent>();
        while (maskQuery.MoveNext(out var heretic, out var mask))
        {
            if (!mask.Active)
                continue;
            var coords = Transform(heretic).Coordinates;
            foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, MaskAuraRange))
            {
                if (other.Owner == heretic || HasComp<HereticComponent>(other) || _mobState.IsDead(other))
                    continue;
                if (!_examine.InRangeUnOccluded(heretic, other, MaskAuraRange))
                    continue;
                var gain = MaskGainPerSec;
                if (coords.TryDistance(EntityManager, Transform(other).Coordinates, out var dist) &&
                    dist <= MaskCloseRange)
                    gain = MaskGainClose;

                var victim = EnsureComp<HereticMaskVictimComponent>(other);
                victim.Stacks = MathF.Min(MaskStackMax, victim.Stacks + gain * frameTime);
                Dirty(other, victim);
                _maskTouched.Add(other);
            }
        }
        var victimQuery = EntityQueryEnumerator<HereticMaskVictimComponent>();
        while (victimQuery.MoveNext(out var vuid, out var victim))
        {
            if (_maskTouched.Contains(vuid))
                continue;
            victim.Stacks -= MaskDecayPerSec * frameTime;
            if (victim.Stacks <= 0f)
            {
                victim.Stacks = 0f;
                _maskVictimRemove.Add(vuid);
            }
            Dirty(vuid, victim);
        }

        foreach (var u in _maskVictimRemove)
        {
            RemComp<HereticMaskVictimComponent>(u);
        }
        _maskVictimRemove.Clear();
    }

    private void OnMantleInit(EntityUid uid, HereticMantleComponent comp, ref ComponentInit args)
    {
        var stealth = EnsureComp<StealthComponent>(uid);
        _stealth.SetVisibility(uid, MantleVisibility, stealth);
        _stealth.SetEnabled(uid, false, stealth);

        if (HasComp<RespiratorComponent>(uid))
            RemComp<RespiratorComponent>(uid);
    }

    private void OnMantleShutdown(EntityUid uid, HereticMantleComponent comp, ref ComponentShutdown args)
    {
        if (HasComp<StealthComponent>(uid))
            RemComp<StealthComponent>(uid);

        EnsureComp<RespiratorComponent>(uid);
    }

    private void OnMantleToggle(EntityUid uid, HereticMantleComponent comp, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        comp.Active = !comp.Active;
        _actions.SetToggled((args.Action, null), comp.Active);

        if (TryComp<StealthComponent>(uid, out var stealth))
        {
            _stealth.SetEnabled(uid, comp.Active, stealth);
            if (comp.Active)
                _stealth.SetVisibility(uid, MantleVisibility, stealth);
        }

        _movement.RefreshMovementSpeedModifiers(uid);

        var msg = comp.Active ? "heretic-mantle-cloak-on" : "heretic-mantle-cloak-off";
        _popup.PopupEntity(Loc.GetString(msg), uid, uid);
    }

    private void OnFuryToggle(EntityUid uid, HereticFuryComponent comp, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        comp.Active = !comp.Active;
        _actions.SetToggled((args.Action, null), comp.Active);
        _movement.RefreshMovementSpeedModifiers(uid);

        var msg = comp.Active ? "heretic-fury-on" : "heretic-fury-off";
        _popup.PopupEntity(Loc.GetString(msg), uid, uid);
    }

    private void OnMaskToggle(EntityUid uid, HereticMaskComponent comp, ref ToggleActionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        comp.Active = !comp.Active;
        _actions.SetToggled((args.Action, null), comp.Active);
        _movement.RefreshMovementSpeedModifiers(uid);
        if (comp.Active)
        {
            EnsureComp<IdentityBlockerComponent>(uid);
        }
        else
        {
            RemComp<IdentityBlockerComponent>(uid);
        }

        _identity.QueueIdentityUpdate(uid);
        var msg = comp.Active ? "heretic-mask-on" : "heretic-mask-off";
        _popup.PopupEntity(Loc.GetString(msg), uid, uid);
    }

    private void OnMaskVictimDamage(EntityUid uid, HereticMaskVictimComponent comp, DamageModifyEvent args)
    {
        if (args.Damage.GetTotal() <= FixedPoint2.Zero)
            return;
        if (!HasComp<HereticComponent>(args.Origin))
            return;

        args.Damage *= 1f + comp.Stacks * MaskDamageFactor;

        comp.Stacks = MathF.Max(0f, comp.Stacks - MaskHitConsume);
        Dirty(uid, comp);
    }

    private void TickMantleHeal(EntityUid uid, float frameTime)
    {
        if (!_mobState.IsAlive(uid))
            return;

        var acc = _mantleAccumulator.GetValueOrDefault(uid) + frameTime;
        if (acc < MantleTickInterval)
        {
            _mantleAccumulator[uid] = acc;
            return;
        }
        _mantleAccumulator[uid] = acc - MantleTickInterval;

        float rate = 2f;

        var heal = new DamageSpecifier();
        foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
        {
            heal.DamageDict[type.ID] = FixedPoint2.New(-rate);
        }
        _damage.TryChangeDamage(uid, heal, true);
    }

    private void OnBloodthornDamage(EntityUid uid, HereticBloodthornComponent comp, DamageChangedEvent args)
    {
        if (!args.DamageIncreased || args.DamageDelta == null)
            return;
        if (args.Origin != comp.Heretic)
            return;
        comp.Accumulated += args.DamageDelta.GetTotal().Float();
    }

    private void OnGraspHit(EntityUid uid, HereticComponent comp, ref MansusGraspHitEvent args)
    {
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
        {
            var mark = EnsureComp<HereticBloodthornComponent>(args.Target);
            mark.Heretic = uid;
            mark.ExpireAt = _timing.CurTime + TimeSpan.FromSeconds(BloodthornSeconds);
            mark.Accumulated = 0f;
        }

        if (HasComp<HereticMaskComponent>(uid))
            _hallucinations.StartHallucinations(args.Target, "Hallucinations", TimeSpan.FromSeconds(GraspHallucinationSeconds), true, "HereticAsh");

        if (!_knowledge.HasKnowledge(uid, "HereticAshGrasp"))
            return;
        _flammable.AdjustFireStacks(args.Target, GraspFireStacks, ignite: true);
        MarkBurnVictim(args.Target);
    }

    private void OnAshFlames(EntityUid uid, HereticComponent comp, HereticAshExplosionEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;

        var coords = Transform(uid).Coordinates;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(coords, FlamesRange))
        {
            if (mob.Owner == uid || HasComp<HereticComponent>(mob))
                continue;
            _flammable.AdjustFireStacks(mob, FlamesFireStacks, ignite: true);
            MarkBurnVictim(mob);
        }
    }

    private void OnFireProtection(EntityUid uid, HereticComponent comp, ref GetFireProtectionEvent args)
    {
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
        {
            args.Reduce(0.5f);
        }
    }

    private void OnTempChange(EntityUid uid, HereticComponent comp, ref BeforeHeatExchangeEvent args)
    {
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
            args.HeatTransferModifier *= 0.5f;
    }

    private void OnRefreshSpeed(EntityUid uid, HereticComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (_knowledge.HasKnowledge(uid, "HereticAshFury")
            && TryComp<FlammableComponent>(uid, out var fl) && fl.OnFire)
        {
            args.ModifySpeed(FurySpeedBase);
        }

        if (TryComp<HereticMantleComponent>(uid, out var mantle) && mantle.Active)
        {
            args.ModifySpeed(MantleSpeedBase);
        }

        if (TryComp<HereticMaskComponent>(uid, out var mask) && mask.Active)
        {
            args.ModifySpeed(MaskSpeedBase);
        }
    }

    private void OnAttackRate(EntityUid weapon, MeleeWeaponComponent comp, ref GetMeleeAttackRateEvent args)
    {
        var user = args.User;
        if (!_knowledge.HasKnowledge(user, "HereticAshFury"))
            return;
        if (!TryComp<FlammableComponent>(user, out var fl) || !fl.OnFire)
            return;
        args.Multipliers *= FuryAttackBase;
    }
    private void OnFireSpread(EntityUid uid, FlammableComponent comp, ref FireSpreadAttemptEvent args)
    {
        if (ShouldBlockSpread(args.First, args.Second) || ShouldBlockSpread(args.Second, args.First))
            args.Cancelled = true;
    }

    private bool ShouldBlockSpread(EntityUid heretic, EntityUid other)
    {
        if (!HasComp<HereticComponent>(heretic))
            return false;
        if (_knowledge.HasKnowledge(heretic, "HereticAshMantle") && _mobState.IsDead(other))
            return true;
        if (_knowledge.HasKnowledge(heretic, "HereticAshFury") && _mobState.IsDead(other))
            return true;
        return false;
    }

    private float GetRealHeat(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var dmg))
            return 0f;
        var all = _damage.GetAllDamage((uid, dmg));
        return all.DamageDict.GetValueOrDefault(HeatType).Float();
    }

    private void MarkBurnVictim(EntityUid target)
    {
        if (!HasComp<MobStateComponent>(target) || HasComp<HereticComponent>(target))
            return;
        var comp = EnsureComp<HereticBurnVictimComponent>(target);
        comp.AccumulatedHeat = GetRealHeat(target);
    }

    private void OnVictimDamage(EntityUid uid, HereticBurnVictimComponent comp, DamageChangedEvent args)
    {
        comp.AccumulatedHeat = GetRealHeat(uid);

        var cap = GibThreshold - GibBuffer;
        if (comp.AccumulatedHeat >= cap - GibFadeZone
            && TryComp<FlammableComponent>(uid, out var fl)
            && fl.FireStacks > 0)
        {
            var proximity = (comp.AccumulatedHeat - (cap - GibFadeZone)) / GibFadeZone;
            var curved = proximity * proximity;
            _flammable.AdjustFireStacks(uid, -GibFadeRate * curved, fl);
        }

        var temp = TryComp<TemperatureComponent>(uid, out var t) ? t.Temperature : 0f;
        var notBurning = !TryComp<FlammableComponent>(uid, out var f) || !f.OnFire;
        if (notBurning && temp < SafeTemp)
            RemComp<HereticBurnVictimComponent>(uid);
    }

    private void OnNodePurchased(EntityUid uid, HereticComponent comp, ref HereticNodePurchasedEvent args)
    {
        if (args.Node != "HereticAshMantle" && args.Node != "HereticAshShift")
            return;
        if (!_knowledge.HasKnowledge(uid, "HereticAshMantle")
            || !_knowledge.HasKnowledge(uid, "HereticAshShift"))
            return;

        foreach (var action in _actions.GetActions(uid))
        {
            if (MetaData(action).EntityPrototype?.ID != "ActionHereticAshShift")
                continue;

            _actions.SetUseDelay(action.Owner, TimeSpan.FromSeconds(MantleShiftCooldown));
            break;
        }
    }

    private void OnAshShift(EntityUid uid, HereticComponent comp, HereticAshShiftEvent args)
    {
        if (args.Handled)
            return;
        if (!_knowledge.HasKnowledge(uid, "HereticAshShift"))
            return;

        var proto = _knowledge.HasKnowledge(uid, "HereticAshMantle") ? "AshJauntMantle" : "AshJaunt";
        var form = _polymorph.PolymorphEntity(uid, proto);
        if (form == null)
            return;

        args.Handled = true;
    }

    private void OnMeleeHit(EntityUid weapon, MeleeWeaponComponent comp, MeleeHitEvent args)
    {
        var user = args.User;

        if (TryComp<HereticMantleComponent>(user, out var mantle) && mantle.Active
                                                                  && TryComp<StealthComponent>(user, out var stealth))
        {
            _stealth.SetVisibility(user, 1f, stealth);
            mantle.RevealUntil = _timing.CurTime + TimeSpan.FromSeconds(MantleRevealSeconds);
        }

        if (!HasComp<HereticComponent>(user))
        {
            foreach (var hit in args.HitEntities)
            {
                if (hit == user)
                    continue;
                if (!TryComp<HereticMaskComponent>(hit, out var mask) || !mask.Active)
                    continue;
                _hallucinations.StartHallucinations(user, "Hallucinations", TimeSpan.FromSeconds(HitHallucinationSeconds), true, "HereticAshFlash");
                break;
            }
        }

        if (!_knowledge.HasKnowledge(user, "HereticAshFury"))
            return;
        if (!TryComp<FlammableComponent>(user, out var userFl) || !userFl.OnFire)
            return;

        var realRate = _melee.GetAttackRate(weapon, user, comp);
        var burnDamage = FuryBurnPerHit / realRate;

        var hitMob = false;
        foreach (var hit in args.HitEntities)
        {
            if (hit == user)
                continue;
            if (!HasComp<MobStateComponent>(hit))
                continue;

            hitMob = true;

            var burn = new DamageSpecifier();
            burn.DamageDict["Heat"] = FixedPoint2.New(burnDamage);
            _damage.TryChangeDamage(hit, burn, true);
            MarkBurnVictim(hit);
        }

        if (!hitMob || !_mobState.IsAlive(user))
            return;

        var heal = new DamageSpecifier();
        foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
            heal.DamageDict[type.ID] = FixedPoint2.New(-FuryHealPerHit);
        _damage.TryChangeDamage(user, heal, true);
    }
}
