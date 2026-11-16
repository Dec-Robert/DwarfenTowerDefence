from __future__ import annotations

import random
from dataclasses import dataclass

from .assets_loader import GameData, RuneDef, RuneSystemSpec, TowerDef
from .enums import MetaEffectType, RuneRarity, RuneStatType, RuneValueType
from .meta import MetaState


RUNE_STAT_TO_TOWER_STAT: dict[RuneStatType, RuneStatType] = {s: s for s in RuneStatType}


@dataclass
class RuneInstance:
    uid: int
    definition: RuneDef
    primary_value: float
    has_penalty: bool = False
    penalty_stat: RuneStatType | None = None
    penalty_value_type: RuneValueType | None = None
    penalty_value: float = 0.0

    @property
    def rarity(self) -> RuneRarity:
        return self.definition.rarity

    @property
    def primary_stat(self) -> RuneStatType:
        return self.definition.primary_stat

    def short_label(self) -> str:
        unit = "%" if self.definition.value_type == RuneValueType.Percent else ""
        pen = ""
        if self.has_penalty and self.penalty_stat is not None:
            pu = "%" if self.penalty_value_type == RuneValueType.Percent else ""
            pen = f" ({self.penalty_stat.name}{self.penalty_value:g}{pu})"
        return f"[{self.rarity.name[:3]}] {self.primary_stat.name}+{self.primary_value:g}{unit}{pen}"


def format_rune_value(val: float, vtype: RuneValueType) -> float:
    if vtype == RuneValueType.Percent or abs(val) >= 1.0:
        return float(round(val))
    return float(round(val, 1))


def roll_rune_rarity(
    rng: random.Random,
    rune_system: RuneSystemSpec,
    meta_state: MetaState,
    hearth_rune_mult: float,
) -> RuneRarity:
    """Exact C# RuneManager.RollRarity parity."""
    w_common = float(rune_system.weight_common)
    unc_unlocked = meta_state.is_unlocked(MetaEffectType.UnlockRuneRarity_Uncommon)
    rare_unlocked = meta_state.is_unlocked(MetaEffectType.UnlockRuneRarity_Rare)
    leg_unlocked = meta_state.is_unlocked(MetaEffectType.UnlockRuneRarity_Legendary)
    cursed_unlocked = meta_state.is_unlocked(MetaEffectType.UnlockRuneRarity_Cursed)

    w_unc = (
        max(0.0, (rune_system.weight_uncommon + meta_state.get_value(MetaEffectType.RuneDropChance_Uncommon)) * hearth_rune_mult)
        if unc_unlocked
        else 0.0
    )
    w_rare = (
        max(0.0, (rune_system.weight_rare + meta_state.get_value(MetaEffectType.RuneDropChance_Rare)) * hearth_rune_mult)
        if rare_unlocked
        else 0.0
    )
    w_leg = (
        max(0.0, (rune_system.weight_legendary + meta_state.get_value(MetaEffectType.RuneDropChance_Legendary)) * hearth_rune_mult)
        if leg_unlocked
        else 0.0
    )
    w_cursed = (
        max(0.0, (rune_system.weight_cursed + meta_state.get_value(MetaEffectType.RuneDropChance_Cursed)) * hearth_rune_mult)
        if cursed_unlocked
        else 0.0
    )

    total_bonus = 0.0
    if unc_unlocked and w_unc > rune_system.weight_uncommon:
        total_bonus += w_unc - rune_system.weight_uncommon
    if rare_unlocked and w_rare > rune_system.weight_rare:
        total_bonus += w_rare - rune_system.weight_rare
    if leg_unlocked and w_leg > rune_system.weight_legendary:
        total_bonus += w_leg - rune_system.weight_legendary
    if cursed_unlocked and w_cursed > rune_system.weight_cursed:
        total_bonus += w_cursed - rune_system.weight_cursed
    w_common = max(0.0, w_common - total_bonus)

    total_weight = w_common + w_unc + w_rare + w_leg + w_cursed
    if total_weight <= 0.0:
        return RuneRarity.Common

    roll = rng.uniform(0.0, total_weight)
    if roll <= w_common:
        return RuneRarity.Common
    roll -= w_common
    if unc_unlocked and roll <= w_unc:
        return RuneRarity.Uncommon
    roll -= w_unc
    if rare_unlocked and roll <= w_rare:
        return RuneRarity.Rare
    roll -= w_rare
    if leg_unlocked and roll <= w_leg:
        return RuneRarity.Legendary
    roll -= w_leg
    if cursed_unlocked and roll <= w_cursed:
        return RuneRarity.Cursed
    return RuneRarity.Common


def _instantiate_from_def(
    uid: int,
    defn: RuneDef,
    rng: random.Random,
    primary_override: float | None = None,
) -> RuneInstance:
    p_val = (
        primary_override
        if primary_override is not None
        else format_rune_value(rng.uniform(defn.min_val, defn.max_val), defn.value_type)
    )
    item = RuneInstance(uid=uid, definition=defn, primary_value=p_val)
    if defn.rarity == RuneRarity.Cursed and defn.possible_penalties:
        pen = defn.possible_penalties[rng.randrange(0, len(defn.possible_penalties))]
        pen_val = format_rune_value(rng.uniform(pen["min_val"], pen["max_val"]), pen["value_type"])
        item.has_penalty = True
        item.penalty_stat = pen["stat"]
        item.penalty_value_type = pen["value_type"]
        item.penalty_value = -abs(pen_val)
    return item


def generate_random_rune(
    uid: int,
    rng: random.Random,
    game_data: GameData,
    meta_state: MetaState,
    hearth_rune_mult: float,
    forced_rarity: RuneRarity | None = None,
) -> RuneInstance | None:
    rs = game_data.rune_system
    rarity = forced_rarity if forced_rarity is not None else roll_rune_rarity(rng, rs, meta_state, hearth_rune_mult)
    pool = rs.runes_by_rarity.get(rarity) or rs.runes_by_rarity.get(RuneRarity.Common) or []
    if not pool:
        return None
    defn = pool[rng.randrange(0, len(pool))]
    return _instantiate_from_def(uid, defn, rng)


def find_specific_rune_def(
    game_data: GameData,
    rarity: RuneRarity,
    primary_stat: RuneStatType,
    value_type: RuneValueType,
) -> RuneDef | None:
    pool = game_data.rune_system.runes_by_rarity.get(rarity) or []
    for d in pool:
        if d.primary_stat == primary_stat and d.value_type == value_type:
            return d
    for d in pool:
        if d.primary_stat == primary_stat:
            return d
    return pool[0] if pool else None


def can_fuse_pair(r1: RuneInstance, r2: RuneInstance, meta_state: MetaState) -> bool:
    if r1.uid == r2.uid:
        return False
    if r1.rarity != r2.rarity or r1.primary_stat != r2.primary_stat:
        return False
    if r1.rarity == RuneRarity.Cursed:
        return False
    next_rarity = RuneRarity(int(r1.rarity) + 1)
    if next_rarity == RuneRarity.Cursed and not meta_state.is_unlocked(MetaEffectType.UnlockCursedCrafting):
        return False
    return True


def fuse_runes(
    r1: RuneInstance,
    r2: RuneInstance,
    next_uid: int,
    rng: random.Random,
    game_data: GameData,
    meta_state: MetaState,
) -> RuneInstance | None:
    if not can_fuse_pair(r1, r2, meta_state):
        return None
    next_rarity = RuneRarity(int(r1.rarity) + 1)
    if r1.definition.value_type == r2.definition.value_type:
        new_vt = r1.definition.value_type
    else:
        new_vt = RuneValueType.Flat if rng.random() <= 0.60 else RuneValueType.Percent
    new_def = find_specific_rune_def(game_data, next_rarity, r1.primary_stat, new_vt)
    if new_def is None:
        return None
    return _instantiate_from_def(next_uid, new_def, rng)


def can_transmute_triple(r1: RuneInstance, r2: RuneInstance, r3: RuneInstance, meta_state: MetaState) -> bool:
    if len({r1.uid, r2.uid, r3.uid}) < 3:
        return False
    if r1.rarity != r2.rarity or r1.rarity != r3.rarity:
        return False
    if r1.rarity == RuneRarity.Cursed:
        return False
    next_rarity = RuneRarity(int(r1.rarity) + 1)
    if next_rarity == RuneRarity.Cursed and not meta_state.is_unlocked(MetaEffectType.UnlockCursedCrafting):
        return False
    return True


def transmute_runes(
    r1: RuneInstance,
    r2: RuneInstance,
    r3: RuneInstance,
    next_uid: int,
    rng: random.Random,
    game_data: GameData,
    meta_state: MetaState,
    hearth_rune_mult: float = 1.0,
) -> RuneInstance | None:
    if not can_transmute_triple(r1, r2, r3, meta_state):
        return None
    next_rarity = RuneRarity(int(r1.rarity) + 1)
    if r1.rarity == RuneRarity.Rare and meta_state.is_unlocked(MetaEffectType.UnlockCursedCrafting):
        if rng.random() <= 0.20:
            next_rarity = RuneRarity.Cursed
    elif r1.rarity == RuneRarity.Legendary:
        next_rarity = RuneRarity.Cursed
    return generate_random_rune(next_uid, rng, game_data, meta_state, hearth_rune_mult, forced_rarity=next_rarity)


def collect_rune_modifiers(runes: list[RuneInstance]) -> dict[RuneStatType, tuple[float, float]]:
    """Aggregate (flat, percent) deltas across equipped runes in a RuneForge, matching RuneForgeEntity."""
    sums: dict[RuneStatType, list[float]] = {}
    for rune in runes:
        ts = RUNE_STAT_TO_TOWER_STAT[rune.definition.primary_stat]
        if ts not in sums:
            sums[ts] = [0.0, 0.0]
        if rune.definition.value_type == RuneValueType.Flat:
            sums[ts][0] += rune.primary_value
        else:
            sums[ts][1] += rune.primary_value
        if rune.has_penalty and rune.penalty_stat is not None:
            pts = RUNE_STAT_TO_TOWER_STAT[rune.penalty_stat]
            if pts not in sums:
                sums[pts] = [0.0, 0.0]
            if rune.penalty_value_type == RuneValueType.Flat:
                sums[pts][0] += rune.penalty_value
            else:
                sums[pts][1] += rune.penalty_value
    return {k: (v[0], v[1]) for k, v in sums.items()}


def score_rune_for_tower(rune: RuneInstance, tower_def: TowerDef) -> float:
    """Blind DPS/effectiveness heuristic for prioritizing which runes to infuse/keep for tower_def."""
    from .enums import DamageType

    def _stat_weight(stat: RuneStatType, vtype: RuneValueType, val: float) -> float:
        if stat == RuneStatType.Damage:
            return val * 1.25 if vtype == RuneValueType.Percent else (val / max(1.0, tower_def.base_damage)) * 110.0
        if stat == RuneStatType.FireRate:
            return val * 1.25 if vtype == RuneValueType.Percent else (val / max(0.2, tower_def.fire_rate)) * 110.0
        if stat == RuneStatType.Range:
            # Soul Monolith (R=1) & Steam Repeater (R=2) get huge relative value from Range%
            r_factor = 1.6 if tower_def.base_range <= 2.5 else 0.9
            return val * r_factor
        if stat == RuneStatType.CriticalChance:
            return val * 0.9
        if stat == RuneStatType.CriticalDamage:
            return val * 0.45
        if stat == RuneStatType.ArmorPenetration:
            return val * (0.95 if tower_def.damage_type == DamageType.Physical else 0.0)
        if stat == RuneStatType.MagicPenetration:
            return val * (0.95 if tower_def.damage_type == DamageType.Magic else 0.0)
        return 0.0

    sc = _stat_weight(rune.definition.primary_stat, rune.definition.value_type, rune.primary_value)
    if rune.has_penalty and rune.penalty_stat is not None and rune.penalty_value_type is not None:
        sc += _stat_weight(rune.penalty_stat, rune.penalty_value_type, rune.penalty_value) * 1.15
    return sc
