using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Polymorph.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared._Wega.Atmos;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Charges.Components;
using Content.Shared.Charges.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Heretic;
using Content.Shared.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Polymorph;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Temperature;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Toggleable;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Components;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;

namespace Content.Server.Heretic;

public sealed class AshSystem : EntitySystem
{
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HereticKnowledgeSystem _knowledge = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly PolymorphSystem _polymorph = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedChargesSystem _charges = default!;

    private const float GraspFireStacks = 3f;
    private const float FlamesFireStacks = 2f;
    private const float FlamesRange = 3f;

    private const float FurySelfMin = 1f;
    private const float FurySelfMax = 10f;
    private const float FurySelfRate = 1f;
    private const float FurySlowThreshold = 8f;
    private const float FurySpeedBase = 1.3f;
    private const float FuryAttackBase = 1.3f;
    private const float FuryBonusPerStack = 0.05f;
    private const float FuryBonusThreshold = 5f;
    private const float FuryTargetStackCap = 5f;
    private const float FuryHealPerHit = 5f;
    private const float FuryResistBase = 0.8f;
    private const float FuryResistPerStack = 0.015f;
    private const float FuryResistCap = 0.95f;

    private const float FuryTransferBase = 2f;
    private const float FuryTransferMin = 1f;
    private const float FuryTransferMax = 3f;

    private const float FuryVacuumExtraFade = 0.2f;
    private const float MantleExtinguishPerUnit = 0.05f;
    private const float FuryExtinguishPerUnit = 0.1f;

    private const float MantleHealBurning = 10f;
    private const float MantleHealIdle = 2f;
    private const float MantleTickInterval = 2f;
    private const float MantleStackBurnPerTick = 1f;

    private const float GibThreshold = 1500f;
    private const float GibBuffer = 35f;
    private const float GibFadeZone = 300f;
    private const float SafeTemp = 350f;
    private const float GibFadeRate = 3f;

    private const float JauntFireStacks = 3f;
    private const float JauntIgniteRange = 1.5f;
    private const float JauntExitRange = 2.5f;
    private const float JauntExitBurn = 20f;
    private const float JauntExitBlunt = 10f;
    private const float JauntExitFireStacks = 2f;
    private const float JauntExitThrowSpeed = 6f;
    private const float JauntExitKnockdown = 2f;

    private const int MantleShiftCharges = 2;

    private static readonly ProtoId<DamageTypePrototype> HeatType = "Heat";
    private readonly Dictionary<EntityUid, float> _mantleAccumulator = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticComponent, MansusGraspHitEvent>(OnGraspHit);
        SubscribeLocalEvent<HereticComponent, HereticAshExplosionEvent>(OnAshFlames);
        SubscribeLocalEvent<HereticComponent, GetFireProtectionEvent>(OnFireProtection);
        SubscribeLocalEvent<HereticComponent, ModifyChangedTemperatureEvent>(OnTempChange);
        SubscribeLocalEvent<HereticComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<HereticFuryComponent, ToggleActionEvent>(OnFuryToggle);
        SubscribeLocalEvent<MeleeWeaponComponent, GetMeleeAttackRateEvent>(OnAttackRate);
        SubscribeLocalEvent<MeleeWeaponComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<FlammableComponent, FireSpreadAttemptEvent>(OnFireSpread);
        SubscribeLocalEvent<HereticBurnVictimComponent, DamageChangedEvent>(OnVictimDamage);
        SubscribeLocalEvent<HereticComponent, FireExtinguishAttemptEvent>(OnExtinguishAttempt);
        SubscribeLocalEvent<HereticComponent, VacuumExtinguishAttemptEvent>(OnVacuumExtinguish);
        SubscribeLocalEvent<HereticComponent, PolymorphedEvent>(OnHereticPolymorphed);
        SubscribeLocalEvent<HereticComponent, HereticNodePurchasedEvent>(OnNodePurchased);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var furyQuery = EntityQueryEnumerator<HereticFuryComponent, FlammableComponent>();
        while (furyQuery.MoveNext(out var uid, out var fury, out var fl))
        {
            if (!fury.Active)
                continue;
            if (fl.FireStacks < FurySelfMin)
            {
                _flammable.AdjustFireStacks(uid, FurySelfMin - fl.FireStacks, fl, ignite: true);
                continue;
            }
            if (fl.FireStacks >= FurySelfMax)
                continue;
            var rate = fl.FireStacks >= FurySlowThreshold ? FurySelfRate * 0.5f : FurySelfRate;
            _flammable.AdjustFireStacks(uid, rate * frameTime, fl, ignite: true);
        }
        var mantleQuery = EntityQueryEnumerator<HereticComponent, FlammableComponent>();
        while (mantleQuery.MoveNext(out var uid, out _, out var fl))
        {
            if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
                TickMantleHeal(uid, fl, frameTime);
        }
        var query = EntityQueryEnumerator<HereticAshJauntFormComponent>();
        while (query.MoveNext(out var formUid, out _))
        {
            var coords = Transform(formUid).Coordinates;
            foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, JauntIgniteRange))
            {
                if (HasComp<HereticComponent>(other))
                    continue;
                if (_mobState.IsDead(other))
                    continue;
                if (HasComp<HereticBurnVictimComponent>(other))
                    continue;

                _flammable.AdjustFireStacks(other, JauntFireStacks, ignite: true);
                MarkBurnVictim(other);
            }
        }
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

    private void TickMantleHeal(EntityUid uid, FlammableComponent fl, float frameTime)
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

        float rate;
        if (fl.FireStacks > 0)
        {
            rate = MantleHealBurning;
            _flammable.AdjustFireStacks(uid, -MantleStackBurnPerTick, fl);
        }
        else
        {
            rate = MantleHealIdle;
        }

        var heal = new DamageSpecifier();
        foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
            heal.DamageDict[type.ID] = FixedPoint2.New(-rate);

        _damage.TryChangeDamage(uid, heal, true);
    }

    private void OnGraspHit(EntityUid uid, HereticComponent comp, ref MansusGraspHitEvent args)
    {
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
            args.Reduce(1f);
            return;
        }
        if (_knowledge.HasKnowledge(uid, "HereticAshFury"))
            args.Reduce(GetFuryResist(uid));
    }

    private void OnTempChange(EntityUid uid, HereticComponent comp, ModifyChangedTemperatureEvent args)
    {
        if (args.TemperatureDelta <= 0)
            return;
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
            args.TemperatureDelta = 0;
        else if (_knowledge.HasKnowledge(uid, "HereticAshFury"))
            args.TemperatureDelta *= 1f - GetFuryResist(uid);
    }

    private float GetFuryResist(EntityUid uid)
    {
        var stacks = TryComp<FlammableComponent>(uid, out var fl) ? fl.FireStacks : 0f;
        var resist = FuryResistBase + FuryResistPerStack * stacks;
        return resist > FuryResistCap ? FuryResistCap : resist;
    }

    private float GetFuryBonus(EntityUid uid, float baseMult)
    {
        if (!TryComp<FlammableComponent>(uid, out var fl) || !fl.OnFire)
            return 1f;
        var extra = fl.FireStacks - FuryBonusThreshold;
        if (extra <= 0)
            return baseMult;
        return baseMult + FuryBonusPerStack * extra;
    }

    private void OnRefreshSpeed(EntityUid uid, HereticComponent comp, RefreshMovementSpeedModifiersEvent args)
    {
        if (!_knowledge.HasKnowledge(uid, "HereticAshFury"))
            return;
        if (!TryComp<FlammableComponent>(uid, out var fl) || !fl.OnFire)
            return;
        args.ModifySpeed(GetFuryBonus(uid, FurySpeedBase));
    }

    private void OnAttackRate(EntityUid weapon, MeleeWeaponComponent comp, ref GetMeleeAttackRateEvent args)
    {
        var user = args.User;
        if (!_knowledge.HasKnowledge(user, "HereticAshFury"))
            return;
        if (!TryComp<FlammableComponent>(user, out var fl) || !fl.OnFire)
            return;
        args.Multipliers *= GetFuryBonus(user, FuryAttackBase);
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
        if (_knowledge.HasKnowledge(heretic, "HereticAshMantle"))
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

        var temp = TryComp<TemperatureComponent>(uid, out var t) ? t.CurrentTemperature : 0f;
        var notBurning = !TryComp<FlammableComponent>(uid, out var f) || !f.OnFire;
        if (notBurning && temp < SafeTemp)
            RemComp<HereticBurnVictimComponent>(uid);
    }


    private void OnExtinguishAttempt(EntityUid uid, HereticComponent comp, ref FireExtinguishAttemptEvent args)
    {
        float perUnit;
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
            perUnit = MantleExtinguishPerUnit;
        else if (_knowledge.HasKnowledge(uid, "HereticAshFury"))
            perUnit = FuryExtinguishPerUnit;
        else
            return;

        if (!TryComp<FlammableComponent>(uid, out var fl))
            return;

        var scale = args.BaseAdjustment / -1.5f;
        _flammable.AdjustFireStacks(uid, -perUnit * scale, fl);
        args.Handled = true;
    }

    private void OnVacuumExtinguish(EntityUid uid, HereticComponent comp, ref VacuumExtinguishAttemptEvent args)
    {
        if (_knowledge.HasKnowledge(uid, "HereticAshMantle"))
            args.Handled = true;
        else if (_knowledge.HasKnowledge(uid, "HereticAshFury"))
        {
            if (TryComp<FlammableComponent>(uid, out var fl))
                _flammable.AdjustFireStacks(uid, -FuryVacuumExtraFade, fl);
            args.Handled = true;
        }
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

            _charges.SetMaxCharges(action.Owner, MantleShiftCharges);
            _charges.AddCharges(action.Owner, 1);
            break;
        }
    }
    private void OnHereticPolymorphed(EntityUid uid, HereticComponent comp, ref PolymorphedEvent args)
    {
        if (!args.IsRevert)
            return;
        if (!HasComp<HereticAshJauntFormComponent>(args.OldEntity))
            return;
        if (!_knowledge.HasKnowledge(uid, "HereticAshMantle"))
            return;

        var coords = Transform(uid).Coordinates;
        var eretPos = _transform.GetWorldPosition(uid);

        foreach (var other in _lookup.GetEntitiesInRange<MobStateComponent>(coords, JauntExitRange))
        {
            if (other.Owner == uid || HasComp<HereticComponent>(other))
                continue;

            var isDead = _mobState.IsDead(other.Owner);

            if (!isDead)
            {
                var dmg = new DamageSpecifier();
                dmg.DamageDict["Heat"] = FixedPoint2.New(JauntExitBurn);
                dmg.DamageDict["Blunt"] = FixedPoint2.New(JauntExitBlunt);
                _damage.TryChangeDamage(other.Owner, dmg, true);

                _flammable.AdjustFireStacks(other.Owner, JauntExitFireStacks, ignite: true);
                MarkBurnVictim(other.Owner);
            }

            var targetPos = _transform.GetWorldPosition(other.Owner);
            var dir = targetPos - eretPos;
            if (dir.LengthSquared() > 0.01f
                && TryComp<PhysicsComponent>(other.Owner, out var phys)
                && TryComp<TransformComponent>(other.Owner, out var xform))
            {
                _throwing.TryThrow(other.Owner, dir, phys, xform, JauntExitThrowSpeed, uid);
            }

            _stun.TryKnockdown(other.Owner, TimeSpan.FromSeconds(JauntExitKnockdown), true, true, true);
        }
    }

    private void OnMeleeHit(EntityUid weapon, MeleeWeaponComponent comp, MeleeHitEvent args)
    {
        var user = args.User;
        if (!_knowledge.HasKnowledge(user, "HereticAshFury"))
            return;
        if (!TryComp<FlammableComponent>(user, out var userFl) || !userFl.OnFire)
            return;

        var available = userFl.FireStacks - FurySelfMin;
        if (available <= 0)
            return;

        var realRate = _melee.GetAttackRate(weapon, user, comp);
        var portion = Math.Clamp(FuryTransferBase / realRate, FuryTransferMin, FuryTransferMax);

        var hitMob = false;
        foreach (var hit in args.HitEntities)
        {
            if (hit == user || HasComp<HereticComponent>(hit))
                continue;
            if (!HasComp<MobStateComponent>(hit))
                continue;
            if (_mobState.IsDead(hit))
                continue;

            hitMob = true;

            if (available <= 0)
                continue;
            if (!TryComp<FlammableComponent>(hit, out var targetFl))
                continue;

            var room = FuryTargetStackCap - targetFl.FireStacks;
            if (room <= 0)
                continue;

            var give = MathF.Min(MathF.Min(portion, available), room);
            if (give <= 0)
                continue;

            _flammable.AdjustFireStacks(hit, give, targetFl, ignite: true);
            _flammable.AdjustFireStacks(user, -give, userFl);
            MarkBurnVictim(hit);
            available -= give;
        }

        if (!hitMob || !_mobState.IsAlive(user))
            return;

        var heal = new DamageSpecifier();
        foreach (var type in _proto.EnumeratePrototypes<DamageTypePrototype>())
            heal.DamageDict[type.ID] = FixedPoint2.New(-FuryHealPerHit);
        _damage.TryChangeDamage(user, heal, true);
    }
}
