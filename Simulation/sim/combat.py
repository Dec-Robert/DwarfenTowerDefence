from __future__ import annotations

import math
import random
from dataclasses import dataclass, field
from typing import Any

from .assets_loader import (
    EnemyDef,
    EnemyTierSpec,
    GameData,
    HearthLevelSpec,
    TowerDef,
    WaveDef,
    WaveEntrySpec,
)
from .enums import DamageType, EnemyRank, MetaEffectType, Race, RuneStatType, TargetingMode
from .meta import MetaState
from .runes import RuneInstance, generate_random_rune
from .spatial import ActiveRoute, HEX_TO_WORLD_RANGE_MULT, MapWorld, PlacedBuilding


@dataclass
class TowerRuntimeStats:
    uid: int
    asset_name: str
    world_x: float
    world_z: float
    efficiency: float
    damage: float
    range_hex: float
    world_range: float
    fire_rate: float
    crit_chance: float
    crit_mult: float
    armor_pen: float
    magic_pen: float
    damage_type: DamageType
    is_feared: bool = False


def compute_tower_stats(
    pb: PlacedBuilding,
    hearth: HearthLevelSpec,
    rune_mods_by_tower: dict[str, dict[RuneStatType, tuple[float, float]]],
    meta_state: MetaState,
    is_feared: bool = False,
) -> TowerRuntimeStats:
    """Exact parity with C# TowerEntity.RecalculateStats + TowerController.ApplyCalculatedStats."""
    td = pb.building_def
    if not isinstance(td, TowerDef):
        raise TypeError(f"PlacedBuilding {pb.asset_name} is not a TowerDef")

    total_crew = sum(pb.assigned_workers.values())
    crew_eff = td.base_efficiency(total_crew)
    supply_penalty = 0.0 if pb.last_upkeep_paid else 0.5
    final_eff = max(0.1, crew_eff - supply_penalty)

    # GlobalModifierRegistry sums (mod.multiplier - 1f) additively across all active sources:
    # Beacon night bonuses + matching active RuneForges.
    pct_bonus: dict[RuneStatType, float] = {}
    flat_bonus: dict[RuneStatType, float] = {}

    def _add_mod(stat: RuneStatType, mult_delta: float, flat_delta: float) -> None:
        pct_bonus[stat] = pct_bonus.get(stat, 0.0) + mult_delta
        flat_bonus[stat] = flat_bonus.get(stat, 0.0) + flat_delta

    if hearth.tower_range_bonus != 0.0:
        _add_mod(RuneStatType.Range, hearth.tower_range_bonus, 0.0)
    if hearth.tower_fire_rate_bonus != 0.0:
        _add_mod(RuneStatType.FireRate, hearth.tower_fire_rate_bonus, 0.0)
    if hearth.tower_damage_bonus != 0.0:
        _add_mod(RuneStatType.Damage, hearth.tower_damage_bonus, 0.0)

    forge_mods = rune_mods_by_tower.get(td.asset_name)
    if forge_mods:
        for st, (f_val, p_val) in forge_mods.items():
            _add_mod(st, p_val / 100.0, f_val)

    range_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.Range, 0.0))
    damage_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.Damage, 0.0))
    fire_rate_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.FireRate, 0.0))
    armor_pen_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.ArmorPenetration, 0.0))
    magic_pen_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.MagicPenetration, 0.0))
    crit_chan_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.CriticalChance, 0.0))
    crit_dmg_multi = max(0.1, 1.0 + pct_bonus.get(RuneStatType.CriticalDamage, 0.0))

    flat_range = flat_bonus.get(RuneStatType.Range, 0.0)
    flat_damage = flat_bonus.get(RuneStatType.Damage, 0.0)
    flat_fire_rate = flat_bonus.get(RuneStatType.FireRate, 0.0)

    def _norm_pct(v: float) -> float:
        return v / 100.0 if abs(v) >= 1.0 else v

    base_crit = td.crit_chance / 100.0 if td.crit_chance > 1.0 else td.crit_chance
    base_ap = td.armor_pen / 100.0 if td.armor_pen > 1.0 else td.armor_pen
    base_mp = td.magic_pen / 100.0 if td.magic_pen > 1.0 else td.magic_pen

    norm_flat_crit = _norm_pct(flat_bonus.get(RuneStatType.CriticalChance, 0.0))
    norm_flat_crit_dmg = _norm_pct(flat_bonus.get(RuneStatType.CriticalDamage, 0.0))
    norm_flat_ap = _norm_pct(flat_bonus.get(RuneStatType.ArmorPenetration, 0.0))
    norm_flat_mp = _norm_pct(flat_bonus.get(RuneStatType.MagicPenetration, 0.0))

    # Racial synergies (TowerEntity.cs:115-145)
    dwarf_count = pb.assigned_workers.get(int(Race.Dwarves), 0)
    elf_count = pb.assigned_workers.get(int(Race.Elves), 0)
    human_count = pb.assigned_workers.get(int(Race.Humans), 0)

    meta_dmg_multi = 0.0
    meta_range_multi = 0.0
    meta_crit_chan = 0.0
    meta_ap = 0.0
    meta_eff_bonus = 0.0

    if dwarf_count > 0 and meta_state.has_effect(MetaEffectType.TowerCrewSynergy_Dwarf):
        meta_dmg_multi += 0.08 * dwarf_count
        meta_ap += 0.05 * dwarf_count
    if elf_count > 0 and meta_state.has_effect(MetaEffectType.TowerCrewSynergy_Elf):
        meta_range_multi += 0.10 * elf_count
        meta_crit_chan += 0.05 * elf_count
    if human_count >= 4 and meta_state.has_effect(MetaEffectType.TowerCrewSynergy_Human):
        meta_eff_bonus += 0.20

    # Meta global upgrades (TowerEntity.cs:148-157)
    flat_damage += meta_state.get_value(MetaEffectType.TowerGlobalDamage)
    fire_rate_multi += meta_state.get_value(MetaEffectType.TowerGlobalFireRate)
    g_ap = meta_state.get_value(MetaEffectType.TowerGlobalArmorPenetration)
    meta_ap += _norm_pct(g_ap)
    g_mp = meta_state.get_value(MetaEffectType.TowerGlobalMagicPenetration)
    norm_flat_mp += _norm_pct(g_mp)

    final_eff += meta_eff_bonus
    damage_multi += meta_dmg_multi
    range_multi += meta_range_multi
    norm_flat_crit += meta_crit_chan
    norm_flat_ap += meta_ap

    dmg = max(0.0, (td.base_damage + flat_damage) * max(0.0, damage_multi))
    rng_hex = max(0.1, (td.base_range + flat_range) * max(0.1, range_multi))
    fr = max(0.05, (td.fire_rate + flat_fire_rate) * max(0.05, fire_rate_multi) * final_eff)
    if is_feared:
        fr *= 0.5
    crit_c = min(1.0, max(0.0, (base_crit + norm_flat_crit) * crit_chan_multi))
    crit_m = max(1.0, (td.crit_mult + norm_flat_crit_dmg) * crit_dmg_multi)
    ap = min(1.0, max(0.0, (base_ap + norm_flat_ap) * armor_pen_multi))
    mp = min(1.0, max(0.0, (base_mp + norm_flat_mp) * magic_pen_multi))

    return TowerRuntimeStats(
        uid=pb.uid,
        asset_name=td.asset_name,
        world_x=pb.world_x,
        world_z=pb.world_z,
        efficiency=final_eff,
        damage=dmg,
        range_hex=rng_hex,
        world_range=rng_hex * HEX_TO_WORLD_RANGE_MULT,
        fire_rate=fr,
        crit_chance=crit_c,
        crit_mult=crit_m,
        armor_pen=ap,
        magic_pen=mp,
        damage_type=td.damage_type,
        is_feared=is_feared,
    )


@dataclass
class ScheduledEnemySpawn:
    spawn_time: float
    enemy_def: EnemyDef
    tier: int
    rank: EnemyRank
    hp_mult: float
    speed_mult: float


def select_wave_def(
    day: int, game_data: GameData, wave_source: str = "predefined50"
) -> tuple[WaveDef | None, int]:
    """Select (WaveDef, days_beyond_last) for day."""
    waves = game_data.waves_predefined_50
    if not waves:
        return None, 0

    if wave_source == "scene20" and game_data.waves_scene_days:
        available_days = [d for d in sorted(game_data.waves_scene_days) if d in waves]
        if not available_days:
            available_days = sorted(d for d in waves if d <= 20) or sorted(waves)
    else:
        available_days = sorted(waves)

    max_day = max(available_days)
    if day > max_day:
        return waves[max_day], day - max_day
    if day in waves and day in available_days:
        return waves[day], 0
    prev = [d for d in available_days if d <= day]
    pick = prev[-1] if prev else available_days[0]
    return waves[pick], 0


def build_wave_spawn_schedule(
    day: int,
    game_data: GameData,
    wave_source: str = "predefined50",
) -> tuple[WaveDef | None, list[ScheduledEnemySpawn]]:
    """Build deterministic timed spawn sequence matching EnemySpawner.PreparePredefinedWave."""
    wave_def, days_beyond = select_wave_def(day, game_data, wave_source=wave_source)
    if wave_def is None:
        return None, []

    sp_params = game_data.scene_spawner_params
    base_interval = (
        wave_def.custom_spawn_interval
        if wave_def.custom_spawn_interval > 0.0
        else float(sp_params.get("nightSpawnInterval", 1.0))
    )
    enable_late = bool(sp_params.get("enableLateScaling", True))
    late_base = float(sp_params.get("lateScalingBase", 1.1))
    hp_mult = (late_base**days_beyond) if (days_beyond > 0 and enable_late) else 1.0
    speed_mult = 1.0

    fixed_seed = int(sp_params.get("fixedWaveSeed", 0))
    wave_seed = fixed_seed if fixed_seed != 0 else (day * 10007 + 42)
    rng = random.Random(wave_seed)
    elites_generated = 0
    raw_items: list[dict[str, Any]] = []

    def _entry_rank(e_def: EnemyDef, e_spec: WaveEntrySpec) -> EnemyRank:
        nonlocal elites_generated
        if e_def.rank == EnemyRank.Boss:
            return EnemyRank.Boss
        if e_spec.force_elite:
            return EnemyRank.Elite
        if wave_def.allow_random_elites and e_def.can_be_elite:
            if wave_def.max_elites_count <= 0 or elites_generated < wave_def.max_elites_count:
                chance = e_def.elite_spawn_chance / 100.0 if e_def.elite_spawn_chance > 1.0 else e_def.elite_spawn_chance
                if rng.random() < chance:
                    elites_generated += 1
                    return EnemyRank.Elite
        return EnemyRank.Normal

    def _shuffle_fisher_yates(lst: list[Any]) -> None:
        n = len(lst)
        for i in range(n):
            j = rng.randrange(i, n)
            lst[i], lst[j] = lst[j], lst[i]

    if wave_def.spawn_groups:
        for grp in wave_def.spawn_groups:
            grp_items: list[dict[str, Any]] = []
            interval = grp.spawn_interval if grp.spawn_interval > 0.0 else base_interval
            for e_spec in grp.entries:
                e_def = game_data.enemies.get(e_spec.enemy_asset)
                if e_def is None:
                    continue
                for _ in range(max(0, e_spec.count)):
                    grp_items.append(
                        {
                            "enemy_def": e_def,
                            "tier": max(1, min(len(e_def.tiers), e_spec.tier)),
                            "rank": _entry_rank(e_def, e_spec),
                            "interval": interval,
                            "jitter": grp.interval_jitter,
                            "delay_before": 0.0,
                        }
                    )
            if grp.shuffle_inside and len(grp_items) > 1:
                _shuffle_fisher_yates(grp_items)
            if grp_items:
                grp_items[0]["delay_before"] = max(0.0, grp.delay_before)
                raw_items.extend(grp_items)
    else:
        for e_spec in wave_def.entries:
            e_def = game_data.enemies.get(e_spec.enemy_asset)
            if e_def is None:
                continue
            for _ in range(max(0, e_spec.count)):
                raw_items.append(
                    {
                        "enemy_def": e_def,
                        "tier": max(1, min(len(e_def.tiers), e_spec.tier)),
                        "rank": _entry_rank(e_def, e_spec),
                        "interval": base_interval,
                        "jitter": 0.0,
                        "delay_before": 0.0,
                    }
                )
        if wave_def.shuffle_enemies and len(raw_items) > 1:
            _shuffle_fisher_yates(raw_items)

    schedule: list[ScheduledEnemySpawn] = []
    cur_t = 0.0
    for idx, item in enumerate(raw_items):
        cur_t += item["delay_before"]
        if idx > 0 and item["delay_before"] <= 0.0:
            j = item["jitter"]
            delta = item["interval"] + (rng.uniform(-j, j) if j > 0.0 else 0.0)
            cur_t += max(0.05, delta)
        schedule.append(
            ScheduledEnemySpawn(
                spawn_time=cur_t,
                enemy_def=item["enemy_def"],
                tier=item["tier"],
                rank=item["rank"],
                hp_mult=hp_mult,
                speed_mult=speed_mult,
            )
        )
    return wave_def, schedule


@dataclass
class ActiveEnemy:
    eid: int
    enemy_def: EnemyDef
    tier: int
    rank: EnemyRank
    max_hp: float
    hp: float
    base_speed: float
    armor: float
    magic_resist: float
    dodge: float
    hit_radius: float
    hyper_armor_left: int
    has_maddening_gaze: bool
    gaze_radius: float
    split_children: int
    split_hp_pct: float
    route_idx: int
    route: ActiveRoute
    seg_idx: int
    seg_progress: float  # distance along current segment
    x: float
    z: float
    dist_to_base: float
    alive: bool = True
    reached_base: bool = False


@dataclass
class ActiveProjectile:
    kind: str  # "Simple", "Linear", "Ground"
    source_uid: int
    damage: float
    damage_type: DamageType
    is_crit: bool
    crit_mult: float
    armor_pen: float
    magic_pen: float
    speed: float
    x: float
    z: float
    dir_x: float = 0.0
    dir_z: float = 0.0
    target_eid: int | None = None
    target_x: float = 0.0
    target_z: float = 0.0
    bullet_radius: float = 0.15
    remaining_lifetime: float = 1.0
    pierce_left: int = 1
    hit_eids: set[int] = field(default_factory=set)
    zone_size: float = 1.6
    zone_tick_interval: float = 0.1
    zone_duration: float = 5.0


@dataclass
class ActiveGroundZone:
    source_uid: int
    cx: float
    cz: float
    half_size: float
    damage: float
    damage_type: DamageType
    crit_chance: float
    crit_mult: float
    armor_pen: float
    magic_pen: float
    tick_interval: float
    remaining_duration: float
    next_tick_timer: float


@dataclass
class NightCombatResult:
    day: int
    duration_sec: float
    total_enemies: int
    kills_normal: int
    kills_elite: int
    kills_boss: int
    leaks_normal: int
    leaks_elite: int
    leaks_boss: int
    city_hp_lost: int
    boss_leaked: bool
    ether_gained: int
    dropped_runes: list[RuneInstance]
    damage_by_tower_uid: dict[int, float]
    damage_by_tower_type: dict[str, float]
    kills_by_tower_uid: dict[int, int]


def simulate_night_combat(
    day: int,
    world: MapWorld,
    towers: list[PlacedBuilding],
    hearth: HearthLevelSpec,
    rune_mods_by_tower: dict[str, dict[RuneStatType, tuple[float, float]]],
    meta_state: MetaState,
    game_data: GameData,
    night_rng: random.Random,
    next_rune_uid_fn: Any,
    wave_source: str = "predefined50",
    dt: float = 0.05,
) -> NightCombatResult:
    """Tick-by-tick deterministic 2D night combat simulation."""
    wave_def, schedule = build_wave_spawn_schedule(day, game_data, wave_source=wave_source)
    routes = world.get_active_routes()
    if not routes:
        return NightCombatResult(day, 0.0, len(schedule), 0, 0, 0, 0, 0, 0, 0, False, 0, [], {}, {}, {})

    # Fallback route weights
    total_flow = sum(max(0.01, r.flow_weight) for r in routes)

    def _pick_route() -> tuple[int, ActiveRoute]:
        roll = night_rng.uniform(0.0, total_flow)
        acc = 0.0
        for idx, r in enumerate(routes):
            acc += max(0.01, r.flow_weight)
            if roll <= acc:
                return idx, r
        return 0, routes[0]

    # Ether Kill-Pool init
    wave_ether_pool = wave_def.ether_pool if wave_def is not None else 1
    guaranteed_pct = hearth.guaranteed_ether_percent
    guaranteed_ether_target = max(0, min(wave_ether_pool, int(round(wave_ether_pool * guaranteed_pct))))
    guaranteed_dropped = 0
    natural_dropped = 0
    total_normal_kills = 0
    estimated_normal_in_wave = max(1, sum(1 for s in schedule if s.rank == EnemyRank.Normal))
    natural_drop_chance = hearth.natural_ether_drop_chance
    ether_gained = 0

    hearth_enemy_mult = max(0.1, 1.0 + hearth.enemy_stats_bonus)
    hearth_rune_mult = max(0.0, 1.0 + hearth.rune_drop_and_rarity_bonus)
    dropped_runes: list[RuneInstance] = []

    # Precompute un-feared + feared tower stats so Boss fear toggle is O(1) per tower
    tower_defs: dict[int, TowerDef] = {}
    stats_normal: dict[int, TowerRuntimeStats] = {}
    stats_feared: dict[int, TowerRuntimeStats] = {}
    cooldowns: dict[int, float] = {}
    damage_by_uid: dict[int, float] = {}
    damage_by_type: dict[str, float] = {}
    kills_by_uid: dict[int, int] = {}

    for tb in towers:
        if not tb.is_active or not isinstance(tb.building_def, TowerDef):
            continue
        tower_defs[tb.uid] = tb.building_def
        stats_normal[tb.uid] = compute_tower_stats(tb, hearth, rune_mods_by_tower, meta_state, is_feared=False)
        stats_feared[tb.uid] = compute_tower_stats(tb, hearth, rune_mods_by_tower, meta_state, is_feared=True)
        cooldowns[tb.uid] = 0.0
        damage_by_uid[tb.uid] = 0.0
        kills_by_uid[tb.uid] = 0
        damage_by_type.setdefault(tb.asset_name, 0.0)

    active_enemies: dict[int, ActiveEnemy] = {}
    projectiles: list[ActiveProjectile] = []
    zones: list[ActiveGroundZone] = []

    next_eid = 1
    spawn_cursor = 0
    n_schedule = len(schedule)

    kills_norm = kills_elite = kills_boss = 0
    leaks_norm = leaks_elite = leaks_boss = 0

    def _spawn_enemy_instance(
        e_def: EnemyDef,
        tier: int,
        rank: EnemyRank,
        route_idx: int,
        route: ActiveRoute,
        hp_scale: float,
        spd_scale: float,
        seg_idx: int = 0,
        seg_prog: float = 0.0,
    ) -> ActiveEnemy:
        nonlocal next_eid
        ts: EnemyTierSpec = e_def.tiers[max(0, min(len(e_def.tiers) - 1, tier - 1))]
        elite_hp = e_def.elite_hp_mult if (rank == EnemyRank.Elite and e_def.elite_hp_mult > 0) else 1.0
        elite_spd = e_def.elite_speed_mult if (rank == EnemyRank.Elite and e_def.elite_speed_mult > 0) else 1.0

        max_hp = max(1.0, ts.base_hp * hearth_enemy_mult * max(0.01, hp_scale) * elite_hp)
        spd = max(0.1, ts.move_speed * hearth_enemy_mult * max(0.01, spd_scale) * elite_spd)
        armor = ts.armor if ts.armor <= 1.0 else ts.armor / 100.0
        mr = ts.magic_resist if ts.magic_resist <= 1.0 else ts.magic_resist / 100.0
        dodge = ts.dodge_chance if ts.dodge_chance <= 1.0 else ts.dodge_chance / 100.0

        hyper = 0
        has_gaze = False
        gaze_r = 8.0
        split_n = 0
        split_hp = 0.3
        for sk in ts.skills:
            if sk.kind == "HyperArmor":
                hyper += sk.charges
            elif sk.kind == "MaddeningGaze":
                has_gaze = True
                gaze_r = sk.aura_radius
            elif sk.kind == "SplitOnDeath":
                split_n = sk.amount_of_children
                split_hp = sk.child_hp_percent

        seg = route.segments[seg_idx]
        frac = seg_prog / max(1e-6, seg.length)
        px = seg.x0 + (seg.x1 - seg.x0) * frac
        pz = seg.z0 + (seg.z1 - seg.z0) * frac
        rem = (seg.length - seg_prog) + sum(s.length for s in route.segments[seg_idx + 1 :])

        en = ActiveEnemy(
            eid=next_eid,
            enemy_def=e_def,
            tier=tier,
            rank=rank,
            max_hp=max_hp,
            hp=max_hp,
            base_speed=spd,
            armor=armor,
            magic_resist=mr,
            dodge=dodge,
            hit_radius=e_def.hit_radius,
            hyper_armor_left=hyper,
            has_maddening_gaze=has_gaze,
            gaze_radius=gaze_r,
            split_children=split_n,
            split_hp_pct=split_hp,
            route_idx=route_idx,
            route=route,
            seg_idx=seg_idx,
            seg_progress=seg_prog,
            x=px,
            z=pz,
            dist_to_base=rem,
        )
        next_eid += 1
        active_enemies[en.eid] = en
        return en

    def _on_enemy_killed_combat(en: ActiveEnemy, killer_uid: int) -> None:
        nonlocal ether_gained, guaranteed_dropped, natural_dropped, total_normal_kills
        nonlocal kills_norm, kills_elite, kills_boss
        if en.rank == EnemyRank.Normal:
            kills_norm += 1
        elif en.rank == EnemyRank.Elite:
            kills_elite += 1
        else:
            kills_boss += 1
        if killer_uid in kills_by_uid:
            kills_by_uid[killer_uid] += 1

        # Split skill if present (for completeness; Blob.asset currently has null ref)
        if en.split_children > 0:
            for _ in range(en.split_children):
                _spawn_enemy_instance(
                    en.enemy_def,
                    en.tier,
                    EnemyRank.Normal,
                    en.route_idx,
                    en.route,
                    en.split_hp_pct,
                    1.0,
                    en.seg_idx,
                    en.seg_progress,
                )

        # Ether drops (EnemyEtherDropManager.cs parity)
        if en.rank == EnemyRank.Elite or en.rank == EnemyRank.Boss:
            drop_amt = en.enemy_def.ether_drop_elite if en.rank == EnemyRank.Elite else en.enemy_def.ether_drop_boss
            ether_gained += max(0, drop_amt)
        else:
            total_normal_kills += 1
            needed_g = guaranteed_ether_target - guaranteed_dropped
            rem_kills = max(1, estimated_normal_in_wave - total_normal_kills + 1)
            awarded = False
            if needed_g > 0:
                if rem_kills <= needed_g or night_rng.random() <= (needed_g / rem_kills):
                    guaranteed_dropped += 1
                    ether_gained += 1
                    awarded = True
            if not awarded and (guaranteed_dropped + natural_dropped) < wave_ether_pool:
                if night_rng.random() <= natural_drop_chance:
                    natural_dropped += 1
                    ether_gained += 1

        # Rune drops (RuneManager.cs:65-88 parity)
        rs = game_data.rune_system
        n_runes = 0
        if en.rank == EnemyRank.Normal:
            if night_rng.uniform(0.0, 100.0) <= rs.drop_chance_normal * hearth_rune_mult:
                n_runes = 1
        elif en.rank == EnemyRank.Elite:
            if night_rng.uniform(0.0, 100.0) <= rs.drop_chance_elite * hearth_rune_mult:
                n_runes = 1
        else:
            n_runes = night_rng.randint(rs.min_runes_from_boss, rs.max_runes_from_boss)

        for _ in range(n_runes):
            r = generate_random_rune(next_rune_uid_fn(), night_rng, game_data, meta_state, hearth_rune_mult)
            if r is not None:
                dropped_runes.append(r)

    def _apply_damage(
        en: ActiveEnemy,
        raw_damage: float,
        dtype: DamageType,
        ap: float,
        mp: float,
        is_crit: bool,
        crit_mult: float,
        source_uid: int,
    ) -> None:
        if not en.alive:
            return
        if en.hyper_armor_left > 0:
            en.hyper_armor_left -= 1
            return
        if en.dodge > 0.0 and not is_crit and dtype != DamageType.TrueDmg:
            if night_rng.random() < en.dodge:
                return

        dmg = raw_damage * (crit_mult if is_crit else 1.0)
        if dtype == DamageType.Physical:
            eff_armor = min(1.0, max(0.0, en.armor - ap))
            dmg *= 1.0 - eff_armor
        elif dtype == DamageType.Magic:
            eff_mr = min(1.0, max(0.0, en.magic_resist - mp))
            dmg *= 1.0 - eff_mr

        if dmg <= 0.0:
            return
        dealt = min(en.hp, dmg)
        en.hp -= dmg
        if source_uid in damage_by_uid:
            damage_by_uid[source_uid] += dealt
            tname = stats_normal[source_uid].asset_name
            damage_by_type[tname] = damage_by_type.get(tname, 0.0) + dealt

        if en.hp <= 0.0:
            en.alive = False
            _on_enemy_killed_combat(en, source_uid)

    t = 0.0
    max_sim_time = 600.0
    while t < max_sim_time:
        # 1. Spawn scheduled enemies
        while spawn_cursor < n_schedule and schedule[spawn_cursor].spawn_time <= t + 1e-9:
            item = schedule[spawn_cursor]
            r_idx, route = _pick_route()
            _spawn_enemy_instance(
                item.enemy_def,
                item.tier,
                item.rank,
                r_idx,
                route,
                item.hp_mult,
                item.speed_mult,
            )
            spawn_cursor += 1

        # 2. Check wave termination
        if spawn_cursor >= n_schedule and not active_enemies and not projectiles and not zones:
            break
        if spawn_cursor >= n_schedule and not any(e.alive for e in active_enemies.values()) and not projectiles and not zones:
            break

        # 3. Collect live MaddeningGaze bosses
        gaze_sources = [
            (e.x, e.z, e.gaze_radius)
            for e in active_enemies.values()
            if e.alive and e.has_maddening_gaze
        ]

        # 4. Move alive enemies
        to_remove: list[int] = []
        for eid, en in active_enemies.items():
            if not en.alive:
                to_remove.append(eid)
                continue
            segs = en.route.segments
            seg = segs[en.seg_idx]
            spd = en.base_speed * (1.5 if seg.is_surveying else 1.0)
            move_rem = spd * dt
            while move_rem > 1e-9:
                seg = segs[en.seg_idx]
                left_in_seg = seg.length - en.seg_progress
                if move_rem < left_in_seg:
                    en.seg_progress += move_rem
                    en.dist_to_base = max(0.0, en.dist_to_base - move_rem)
                    frac = en.seg_progress / max(1e-6, seg.length)
                    en.x = seg.x0 + (seg.x1 - seg.x0) * frac
                    en.z = seg.z0 + (seg.z1 - seg.z0) * frac
                    move_rem = 0.0
                else:
                    move_rem -= left_in_seg
                    en.dist_to_base = max(0.0, en.dist_to_base - left_in_seg)
                    en.seg_idx += 1
                    en.seg_progress = 0.0
                    if en.seg_idx >= len(segs):
                        en.alive = False
                        en.reached_base = True
                        if en.rank == EnemyRank.Normal:
                            leaks_norm += 1
                        elif en.rank == EnemyRank.Elite:
                            leaks_elite += 1
                        else:
                            leaks_boss += 1
                        to_remove.append(eid)
                        move_rem = 0.0
                    else:
                        en.x = segs[en.seg_idx].x0
                        en.z = segs[en.seg_idx].z0
                        if segs[en.seg_idx].is_surveying != seg.is_surveying:
                            # adjust remaining distance budget cleanly at fog boundary
                            ratio = 1.5 if segs[en.seg_idx].is_surveying else (1.0 / 1.5)
                            move_rem *= ratio
        for eid in to_remove:
            active_enemies.pop(eid, None)

        alive_list = [e for e in active_enemies.values() if e.alive]

        # 5. Update GroundDamageZones
        next_zones: list[ActiveGroundZone] = []
        for gz in zones:
            gz.remaining_duration -= dt
            gz.next_tick_timer -= dt
            if gz.next_tick_timer <= 1e-9:
                gz.next_tick_timer += gz.tick_interval
                for en in alive_list:
                    if not en.alive:
                        continue
                    r_lim = gz.half_size + en.hit_radius
                    if abs(en.x - gz.cx) <= r_lim and abs(en.z - gz.cz) <= r_lim:
                        is_crit = night_rng.random() < gz.crit_chance
                        _apply_damage(en, gz.damage, gz.damage_type, gz.armor_pen, gz.magic_pen, is_crit, gz.crit_mult, gz.source_uid)
            if gz.remaining_duration > 1e-6:
                next_zones.append(gz)
        zones = next_zones

        # 6. Tower target acquisition & firing
        if alive_list:
            for uid, td in tower_defs.items():
                cooldowns[uid] = max(0.0, cooldowns[uid] - dt)
                if cooldowns[uid] > 1e-9:
                    continue

                base_st = stats_normal[uid]
                feared = False
                if gaze_sources:
                    for gx, gz_pos, gr in gaze_sources:
                        if math.hypot(base_st.world_x - gx, base_st.world_z - gz_pos) <= gr:
                            feared = True
                            break
                st = stats_feared[uid] if feared else base_st
                r2 = st.world_range * st.world_range

                # Find best target in range (FurthestOnTrack = minimal dist_to_base)
                best_en: ActiveEnemy | None = None
                best_key = 1e18
                for en in alive_list:
                    if not en.alive:
                        continue
                    dx = en.x - st.world_x
                    dz = en.z - st.world_z
                    if dx * dx + dz * dz <= r2:
                        if en.dist_to_base < best_key:
                            best_key = en.dist_to_base
                            best_en = en

                if best_en is None:
                    continue

                cooldowns[uid] = 1.0 / max(0.05, st.fire_rate)
                is_crit = night_rng.random() < st.crit_chance

                if td.bullet_kind == "HitScan":
                    _apply_damage(best_en, st.damage, st.damage_type, st.armor_pen, st.magic_pen, is_crit, st.crit_mult, uid)
                elif td.bullet_kind == "Fragmented":
                    # Main shard + extraShards to other enemies within spread_radius around primary target
                    projectiles.append(
                        ActiveProjectile(
                            kind="Simple",
                            source_uid=uid,
                            damage=st.damage,
                            damage_type=st.damage_type,
                            is_crit=is_crit,
                            crit_mult=st.crit_mult,
                            armor_pen=st.armor_pen,
                            magic_pen=st.magic_pen,
                            speed=td.bullet_speed,
                            x=st.world_x,
                            z=st.world_z,
                            target_eid=best_en.eid,
                            target_x=best_en.x,
                            target_z=best_en.z,
                        )
                    )
                    sr2 = td.spread_radius * td.spread_radius
                    cands = [
                        e
                        for e in alive_list
                        if e.alive
                        and e.eid != best_en.eid
                        and ((e.x - best_en.x) ** 2 + (e.z - best_en.z) ** 2) <= sr2
                    ]
                    for _ in range(td.extra_shards):
                        if not cands:
                            break
                        idx_c = night_rng.randrange(0, len(cands))
                        extra_t = cands.pop(idx_c)
                        projectiles.append(
                            ActiveProjectile(
                                kind="Simple",
                                source_uid=uid,
                                damage=st.damage,
                                damage_type=st.damage_type,
                                is_crit=is_crit,
                                crit_mult=st.crit_mult,
                                armor_pen=st.armor_pen,
                                magic_pen=st.magic_pen,
                                speed=td.bullet_speed,
                                x=st.world_x,
                                z=st.world_z,
                                target_eid=extra_t.eid,
                                target_x=extra_t.x,
                                target_z=extra_t.z,
                            )
                        )
                elif td.bullet_kind == "Linear":
                    vx = best_en.x - st.world_x
                    vz = best_en.z - st.world_z
                    d = max(1e-6, math.hypot(vx, vz))
                    projectiles.append(
                        ActiveProjectile(
                            kind="Linear",
                            source_uid=uid,
                            damage=st.damage,
                            damage_type=st.damage_type,
                            is_crit=is_crit,
                            crit_mult=st.crit_mult,
                            armor_pen=st.armor_pen,
                            magic_pen=st.magic_pen,
                            speed=td.bullet_speed,
                            x=st.world_x,
                            z=st.world_z,
                            dir_x=vx / d,
                            dir_z=vz / d,
                            bullet_radius=td.bullet_radius,
                            remaining_lifetime=td.bullet_lifetime,
                            pierce_left=td.pierce_count,
                        )
                    )
                elif td.bullet_kind == "Ground":
                    projectiles.append(
                        ActiveProjectile(
                            kind="Ground",
                            source_uid=uid,
                            damage=st.damage,
                            damage_type=st.damage_type,
                            is_crit=is_crit,
                            crit_mult=st.crit_mult,
                            armor_pen=st.armor_pen,
                            magic_pen=st.magic_pen,
                            speed=td.bullet_speed,
                            x=st.world_x,
                            z=st.world_z,
                            target_x=best_en.x,
                            target_z=best_en.z,
                            zone_size=td.zone_size,
                            zone_tick_interval=td.zone_tick_interval,
                            zone_duration=td.zone_duration,
                        )
                    )
                else:
                    # Simple homing
                    projectiles.append(
                        ActiveProjectile(
                            kind="Simple",
                            source_uid=uid,
                            damage=st.damage,
                            damage_type=st.damage_type,
                            is_crit=is_crit,
                            crit_mult=st.crit_mult,
                            armor_pen=st.armor_pen,
                            magic_pen=st.magic_pen,
                            speed=td.bullet_speed,
                            x=st.world_x,
                            z=st.world_z,
                            target_eid=best_en.eid,
                            target_x=best_en.x,
                            target_z=best_en.z,
                        )
                    )

        # 7. Advance active projectiles
        next_proj: list[ActiveProjectile] = []
        for p in projectiles:
            step_dist = p.speed * dt
            if p.kind == "Simple":
                tgt = active_enemies.get(p.target_eid) if p.target_eid is not None else None
                if tgt is None or not tgt.alive:
                    # target vanished mid-flight
                    tx, tz = p.target_x, p.target_z
                    d = math.hypot(tx - p.x, tz - p.z)
                    if d <= step_dist:
                        continue
                    p.x += (tx - p.x) / d * step_dist
                    p.z += (tz - p.z) / d * step_dist
                    p.remaining_lifetime -= dt
                    if p.remaining_lifetime > 0.0:
                        next_proj.append(p)
                else:
                    p.target_x, p.target_z = tgt.x, tgt.z
                    d = math.hypot(tgt.x - p.x, tgt.z - p.z)
                    if d <= step_dist + tgt.hit_radius:
                        _apply_damage(tgt, p.damage, p.damage_type, p.armor_pen, p.magic_pen, p.is_crit, p.crit_mult, p.source_uid)
                    else:
                        p.x += (tgt.x - p.x) / d * step_dist
                        p.z += (tgt.z - p.z) / d * step_dist
                        p.remaining_lifetime -= dt
                        if p.remaining_lifetime > 0.0:
                            next_proj.append(p)
            elif p.kind == "Ground":
                d = math.hypot(p.target_x - p.x, p.target_z - p.z)
                if d <= step_dist:
                    base_st = stats_normal[p.source_uid]
                    zones.append(
                        ActiveGroundZone(
                            source_uid=p.source_uid,
                            cx=p.target_x,
                            cz=p.target_z,
                            half_size=0.5 * p.zone_size,
                            damage=p.damage,
                            damage_type=p.damage_type,
                            crit_chance=base_st.crit_chance,
                            crit_mult=base_st.crit_mult,
                            armor_pen=p.armor_pen,
                            magic_pen=p.magic_pen,
                            tick_interval=p.zone_tick_interval,
                            remaining_duration=p.zone_duration,
                            next_tick_timer=0.0,
                        )
                    )
                else:
                    p.x += (p.target_x - p.x) / d * step_dist
                    p.z += (p.target_z - p.z) / d * step_dist
                    next_proj.append(p)
            elif p.kind == "Linear":
                nx = p.x + p.dir_x * step_dist
                nz = p.z + p.dir_z * step_dist
                seg_dx = nx - p.x
                seg_dz = nz - p.z
                seg_l2 = seg_dx * seg_dx + seg_dz * seg_dz
                hits_now: list[tuple[float, ActiveEnemy]] = []
                for en in active_enemies.values():
                    if not en.alive or en.eid in p.hit_eids:
                        continue
                    t_proj = (
                        max(0.0, min(1.0, ((en.x - p.x) * seg_dx + (en.z - p.z) * seg_dz) / seg_l2))
                        if seg_l2 > 1e-9
                        else 0.0
                    )
                    cx = p.x + seg_dx * t_proj
                    cz = p.z + seg_dz * t_proj
                    hit_r = p.bullet_radius + en.hit_radius
                    if (en.x - cx) ** 2 + (en.z - cz) ** 2 <= hit_r * hit_r:
                        hits_now.append((t_proj, en))
                hits_now.sort(key=lambda item: item[0])
                for _, en in hits_now:
                    if p.pierce_left <= 0:
                        break
                    p.hit_eids.add(en.eid)
                    p.pierce_left -= 1
                    _apply_damage(en, p.damage, p.damage_type, p.armor_pen, p.magic_pen, p.is_crit, p.crit_mult, p.source_uid)
                p.x, p.z = nx, nz
                p.remaining_lifetime -= dt
                if p.pierce_left > 0 and p.remaining_lifetime > 1e-6:
                    next_proj.append(p)

        projectiles = next_proj
        t += dt

    hp_lost = leaks_norm * 1 + leaks_elite * 3
    return NightCombatResult(
        day=day,
        duration_sec=round(t, 2),
        total_enemies=n_schedule,
        kills_normal=kills_norm,
        kills_elite=kills_elite,
        kills_boss=kills_boss,
        leaks_normal=leaks_norm,
        leaks_elite=leaks_elite,
        leaks_boss=leaks_boss,
        city_hp_lost=hp_lost,
        boss_leaked=(leaks_boss > 0),
        ether_gained=ether_gained,
        dropped_runes=dropped_runes,
        damage_by_tower_uid=damage_by_uid,
        damage_by_tower_type=damage_by_type,
        kills_by_tower_uid=kills_by_uid,
    )
