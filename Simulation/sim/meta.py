from __future__ import annotations

import json
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .assets_loader import GameData, MetaUpgradeDef
from .enums import MetaEffectType, RuneRarity

DEFAULT_STARTER_TOWERS = [
    "1. Sentinel Arbalest",
    "6. Royal Bombard",
]

DRAFTABLE_TOWER_POOL = [
    "4. Crucible of Conflagrations",
    "3. Steam Repeater",
    "5. Shadow Spire",
    "2. Thunderbuss",
    "7. Soul Monolith",
]

EXCLUDED_EFFECT_TYPES = {
    MetaEffectType.GateBaseHP,
    MetaEffectType.WallSystem_Solution1,
    MetaEffectType.WallSystem_Solution2,
    MetaEffectType.UnlockDruidTower,
}


def resolve_tower_name(spec: str, all_towers: dict[str, Any]) -> str | None:
    s = spec.strip().lower()
    for name in all_towers:
        if name.lower() == s:
            return name
    for name in all_towers:
        if name.lower().startswith(f"{s}.") or s in name.lower():
            return name
    return None


@dataclass
class MetaState:
    name: str
    unlocked_ids: set[str] = field(default_factory=set)
    variant_choices: dict[str, str] = field(default_factory=dict)
    drafted_towers: list[str] = field(default_factory=list)
    ignore_building_target_bug: bool = False
    warnings: list[str] = field(default_factory=list)

    # Computed caches
    applied_values: dict[MetaEffectType, float] = field(default_factory=dict)
    building_values: dict[tuple[MetaEffectType, str], float] = field(default_factory=dict)
    unlocked_effect_types: set[MetaEffectType] = field(default_factory=set)
    effective_unlocked_towers: set[str] = field(default_factory=set)

    def recompute(self, game_data: GameData) -> None:
        self.applied_values.clear()
        self.building_values.clear()
        self.unlocked_effect_types.clear()
        self.warnings.clear()

        # Resolve effective unlocked IDs including chosen A/B variants cleanly
        effective_ids: set[str] = set()
        for uid in self.unlocked_ids:
            u = game_data.meta_upgrades.get(uid)
            if u is None:
                self.warnings.append(f"Unknown meta upgrade id ignored: {uid}")
                continue
            if u.has_variants:
                effective_ids.add(uid)
                choice = self.variant_choices.get(uid, "A").upper()
                chosen_id = u.variant_b if choice == "B" else u.variant_a
                if chosen_id and chosen_id in game_data.meta_upgrades:
                    effective_ids.add(chosen_id)
            elif not u.is_variant_option:
                effective_ids.add(uid)
            else:
                # Explicitly listed variant option
                effective_ids.add(uid)

        # Ensure if a parent with has_variants also had the unselected sibling listed manually, keep choice authoritative
        for uid in list(effective_ids):
            u = game_data.meta_upgrades.get(uid)
            if u and u.has_variants and uid in self.variant_choices:
                choice = self.variant_choices[uid].upper()
                drop_id = u.variant_a if choice == "B" else u.variant_b
                if drop_id in effective_ids:
                    effective_ids.discard(drop_id)

        self.unlocked_ids = effective_ids

        draft_slots = 0
        for uid in sorted(self.unlocked_ids):
            u = game_data.meta_upgrades.get(uid)
            if not u:
                continue
            if u.effect_type in EXCLUDED_EFFECT_TYPES:
                continue
            if u.effect_type == MetaEffectType.NoneEffect:
                continue
            self.unlocked_effect_types.add(u.effect_type)
            if u.effect_type == MetaEffectType.TowerDraftUnlock:
                draft_slots += 1
                continue
            if u.effect_type in (
                MetaEffectType.BuildingTerrainBonusMultiplier,
                MetaEffectType.SpecificBuildingUpgradeCostReduction,
                MetaEffectType.SpecificBuildingProductionBonus,
            ):
                if u.target_building:
                    key = (u.effect_type, u.target_building)
                    self.building_values[key] = self.building_values.get(key, 0.0) + u.effect_value
                elif self.ignore_building_target_bug:
                    # Apply only to clear intended buildings if optional override flag requested
                    for b_name in ("Farm", "Mines", "Quarry", "Sawmill"):
                        key = (u.effect_type, b_name)
                        self.building_values[key] = self.building_values.get(key, 0.0) + u.effect_value
                continue
            self.applied_values[u.effect_type] = (
                self.applied_values.get(u.effect_type, 0.0) + u.effect_value
            )

        # Resolve unlocked towers: 2 starter towers + up to draft_slots drafted towers
        unlocked_towers = {t for t in DEFAULT_STARTER_TOWERS if t in game_data.towers}
        valid_drafted: list[str] = []
        for raw_t in self.drafted_towers:
            resolved = resolve_tower_name(raw_t, game_data.towers)
            if resolved and resolved not in unlocked_towers and resolved not in valid_drafted:
                valid_drafted.append(resolved)
        for t in valid_drafted[:draft_slots]:
            unlocked_towers.add(t)
        self.drafted_towers = valid_drafted[:draft_slots]
        self.effective_unlocked_towers = unlocked_towers

    def get_value(self, effect_type: MetaEffectType) -> float:
        return self.applied_values.get(effect_type, 0.0)

    def has_effect(self, effect_type: MetaEffectType) -> bool:
        return self.applied_values.get(effect_type, 0.0) != 0.0 or effect_type in self.unlocked_effect_types

    def is_unlocked(self, effect_type: MetaEffectType) -> bool:
        return effect_type in self.unlocked_effect_types

    def is_id_unlocked(self, uid: str | None) -> bool:
        if not uid:
            return True
        return uid in self.unlocked_ids

    def get_building_value(self, effect_type: MetaEffectType, building_asset: str) -> float:
        return self.building_values.get((effect_type, building_asset), 0.0)

    def is_rune_rarity_unlocked(self, rarity: RuneRarity) -> bool:
        if rarity == RuneRarity.Common:
            return True
        if rarity == RuneRarity.Uncommon:
            return self.is_unlocked(MetaEffectType.UnlockRuneRarity_Uncommon)
        if rarity == RuneRarity.Rare:
            return self.is_unlocked(MetaEffectType.UnlockRuneRarity_Rare)
        if rarity == RuneRarity.Legendary:
            return self.is_unlocked(MetaEffectType.UnlockRuneRarity_Legendary)
        if rarity == RuneRarity.Cursed:
            return self.is_unlocked(MetaEffectType.UnlockRuneRarity_Cursed)
        return False

    def build_map_exporter_config(self, game_data: GameData) -> dict[str, Any]:
        def _layout_dto(layout) -> dict[str, Any]:
            return {
                "type": layout.layout_type,
                "roadConnectionEdge": [layout.road_connection_edge[0], layout.road_connection_edge[1]],
                "hexes": [
                    {
                        "localCoord": [h.local[0], h.local[1]],
                        "feature": h.feature,
                        "featureLevel": h.feature_level,
                        "elevation": h.elevation,
                        "startingBuilding": h.starting_building,
                    }
                    for h in layout.hexes
                ],
            }

        cfg = json.loads(json.dumps(game_data.generator_config))
        cfg["metaConfig"] = {
            "startingChunkBase": _layout_dto(game_data.meta_map.starting_chunk_base),
            "expansions": [
                {
                    "chunkCoordinate": [e["chunk_coord"][0], e["chunk_coord"][1]],
                    "baseLayout": _layout_dto(e["layout"]),
                }
                for e in game_data.meta_map.expansions
                if self.is_id_unlocked(e["required_upgrade"]) and e["layout"] is not None
            ],
            "globalModifiers": [
                {
                    "name": m["name"],
                    "targetChunk": [m["target_chunk"][0], m["target_chunk"][1]],
                    "modifierLayout": _layout_dto(m["layout"]),
                }
                for m in game_data.meta_map.global_modifiers
                if self.is_id_unlocked(m["required_upgrade"]) and m["layout"] is not None
            ],
        }
        return cfg

    def summary_dict(self) -> dict[str, Any]:
        return {
            "profile_name": self.name,
            "unlocked_count": len(self.unlocked_ids),
            "unlocked_ids": sorted(self.unlocked_ids),
            "variants": dict(sorted(self.variant_choices.items())),
            "unlocked_towers": sorted(self.effective_unlocked_towers),
            "drafted_towers": list(self.drafted_towers),
            "applied_effects": {et.name: val for et, val in sorted(self.applied_values.items(), key=lambda x: x[0].value)},
            "ignore_building_target_bug": self.ignore_building_target_bug,
        }


def _expand_prerequisites(seed_ids: set[str], meta_upgrades: dict[str, MetaUpgradeDef]) -> set[str]:
    result = set(seed_ids)
    stack = list(seed_ids)
    while stack:
        uid = stack.pop()
        u = meta_upgrades.get(uid)
        if not u:
            continue
        for pre in u.prerequisites:
            if pre not in result:
                result.add(pre)
                stack.append(pre)
    return result


def build_meta_state(
    profile_spec: str | Path | None,
    game_data: GameData,
    variants_override: dict[str, str] | None = None,
    drafted_towers_override: list[str] | None = None,
    ignore_building_target_bug: bool | None = None,
) -> MetaState:
    unlocked_ids: set[str] = set()
    variants: dict[str, str] = {}
    drafted: list[str] = []
    name = "none"
    auto_prereq = True
    ignore_bug = False

    if profile_spec is None or str(profile_spec).lower() == "none":
        name = "none"
    elif str(profile_spec).lower() == "assets":
        name = "assets"
        unlocked_ids = {uid for uid, u in game_data.meta_upgrades.items() if u.is_unlocked_default}
    elif str(profile_spec).lower() == "full":
        name = "full"
        unlocked_ids = {
            uid
            for uid, u in game_data.meta_upgrades.items()
            if not u.is_variant_option and u.effect_type not in EXCLUDED_EFFECT_TYPES
        }
        # Default to variant B (FireRate +10% per tier) unless overridden because in C#
        # Variant A (TowerGlobalDamage effectValue=0.1) is +0.1 flat damage instead of +10%.
        for uid, u in game_data.meta_upgrades.items():
            if u.has_variants:
                variants[uid] = "B"
        drafted = [t for t in DRAFTABLE_TOWER_POOL if t in game_data.towers]
    else:
        p = Path(profile_spec)
        if not p.exists():
            # Also try Simulation/meta_profiles/<name>.json
            cand = Path(__file__).resolve().parent.parent / "meta_profiles" / f"{profile_spec}.json"
            if cand.exists():
                p = cand
            else:
                raise FileNotFoundError(f"Meta profile not found: {profile_spec}")
        raw = json.loads(p.read_text(encoding="utf-8"))
        name = str(raw.get("name") or p.stem)
        raw_ids = raw.get("unlocked_upgrades", [])
        if raw_ids == "all":
            unlocked_ids = {
                uid
                for uid, u in game_data.meta_upgrades.items()
                if not u.is_variant_option and u.effect_type not in EXCLUDED_EFFECT_TYPES
            }
        elif raw_ids == "assets":
            unlocked_ids = {uid for uid, u in game_data.meta_upgrades.items() if u.is_unlocked_default}
        else:
            unlocked_ids = {str(x) for x in raw_ids}
        variants = {str(k): str(v).upper() for k, v in (raw.get("variants") or {}).items()}
        drafted = [str(x) for x in (raw.get("drafted_towers") or [])]
        auto_prereq = bool(raw.get("auto_resolve_prerequisites", True))
        ignore_bug = bool(raw.get("ignore_building_target_bug", False))

    if variants_override:
        for k, v in variants_override.items():
            variants[k] = v.upper()

    if drafted_towers_override is not None:
        drafted = list(drafted_towers_override)
    elif name == "full" and not drafted:
        drafted = [t for t in DRAFTABLE_TOWER_POOL if t in game_data.towers]

    if ignore_building_target_bug is not None:
        ignore_bug = ignore_building_target_bug

    if auto_prereq:
        unlocked_ids = _expand_prerequisites(unlocked_ids, game_data.meta_upgrades)

    state = MetaState(
        name=name,
        unlocked_ids=unlocked_ids,
        variant_choices=variants,
        drafted_towers=drafted,
        ignore_building_target_bug=ignore_bug,
    )
    state.recompute(game_data)
    return state
