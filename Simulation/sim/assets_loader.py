from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .enums import (
    BuildingType,
    DamageType,
    ElevationFilterMode,
    EnemyRank,
    HexFeatureType,
    MetaEffectType,
    MilestoneType,
    Race,
    ResourceType,
    RuneRarity,
    RuneStatType,
    RuneValueType,
    TargetingMode,
)
from .yaml_loader import (
    build_guid_index,
    decode_hex_int_list,
    parse_scalar,
    parse_unity_asset,
    parse_unity_yaml_documents,
)


@dataclass
class ResourceCost:
    resource: ResourceType
    amount: float


@dataclass
class BonusYieldSpec:
    resource: ResourceType
    base_bonus_per_hex: float
    on_top_bonus: float
    penalty_per_user: float
    min_bonus: float


@dataclass
class BonusRuleSpec:
    required_feature: HexFeatureType
    search_radius: int
    yields: list[BonusYieldSpec]


@dataclass
class ElevationBonusSpec:
    enabled: bool
    resource: ResourceType
    on_top_per_level: float
    adj_per_level: float
    penalty_per_extra: float
    search_radius: int


@dataclass
class ElevationRequirementSpec:
    mode: ElevationFilterMode
    min_elevation: int
    max_elevation: int
    allowed_elevations: list[int]


@dataclass
class BuildingDef:
    asset_name: str
    building_name: str
    building_type: BuildingType
    worker_scaling_factor: float
    first_worker_production: float
    efficiency_table: list[float]
    allowed_terrain: list[int]
    elevation_req: ElevationRequirementSpec
    allow_on_road: bool
    requires_occupied: bool
    requires_specific_citizen: bool
    required_citizen_race: Race
    is_outpost: bool
    is_unlocked: bool
    base_max_workers: int
    construction_cost: list[ResourceCost]
    production_per_cycle: list[ResourceCost]
    upkeep_per_cycle: list[ResourceCost]
    bonus_rules: list[BonusRuleSpec]
    elevation_bonus: ElevationBonusSpec
    # Housing extensions
    is_housing: bool = False
    housing_race: Race = Race.Humans
    max_residents: int = 0
    initial_residents: int = 0
    growth_cycles: list[int] = field(default_factory=list)
    upkeep_per_resident: list[ResourceCost] = field(default_factory=list)
    growth_surplus_cost: list[ResourceCost] = field(default_factory=list)
    # Special prefab overrides
    outpost_days_until_transform: int = 10
    expedition_equipment_provided: int = 1

    def base_efficiency(self, workers: int) -> float:
        if workers <= 0:
            return float(self.efficiency_table[0]) if self.efficiency_table else 0.0
        if workers < len(self.efficiency_table):
            return float(self.efficiency_table[workers])
        last = float(self.efficiency_table[-1]) if self.efficiency_table else 1.0
        extra = workers - (len(self.efficiency_table) - 1)
        return last + extra * self.worker_scaling_factor


@dataclass
class TowerDef(BuildingDef):
    base_range: float = 1.0
    base_damage: float = 10.0
    fire_rate: float = 1.0
    crit_chance: float = 0.0
    crit_mult: float = 2.0
    armor_pen: float = 0.0
    magic_pen: float = 0.0
    damage_type: DamageType = DamageType.Physical
    can_shoot_chunk: bool = False
    default_priorities: list[TargetingMode] = field(default_factory=list)
    bullet_kind: str = "Simple"
    bullet_speed: float = 20.0
    bullet_radius: float = 0.15
    bullet_lifetime: float = 1.0
    pierce_count: int = 1
    extra_shards: int = 0
    spread_radius: float = 0.0
    zone_size: float = 1.6
    zone_tick_interval: float = 0.1
    zone_duration: float = 5.0


@dataclass
class EnemySkillSpec:
    kind: str
    charges: int = 0
    amount_of_children: int = 0
    child_hp_percent: float = 0.3
    aura_radius: float = 8.0


@dataclass
class EnemyTierSpec:
    base_hp: float
    move_speed: float
    armor: float
    magic_resist: float
    dodge_chance: float
    skills: list[EnemySkillSpec]


@dataclass
class EnemyDef:
    asset_name: str
    enemy_name: str
    threat_cost: int
    rank: EnemyRank
    can_be_elite: bool
    elite_spawn_chance: float
    elite_hp_mult: float
    elite_speed_mult: float
    ether_drop_elite: int
    ether_drop_boss: int
    hit_radius: float
    tiers: list[EnemyTierSpec]


@dataclass
class WaveEntrySpec:
    enemy_asset: str
    tier: int
    count: int
    force_elite: bool


@dataclass
class SpawnGroupSpec:
    name: str
    delay_before: float
    spawn_interval: float
    interval_jitter: float
    shuffle_inside: bool
    entries: list[WaveEntrySpec]


@dataclass
class WaveDef:
    day_number: int
    ether_pool: int
    custom_spawn_interval: float
    hp_mult: float
    speed_mult: float
    shuffle_enemies: bool
    allow_random_elites: bool
    max_elites_count: int
    entries: list[WaveEntrySpec]
    spawn_groups: list[SpawnGroupSpec]


@dataclass
class HearthLevelSpec:
    level_index: int
    title: str
    coal_cost: int
    tower_damage_bonus: float
    tower_fire_rate_bonus: float
    tower_range_bonus: float
    enemy_stats_bonus: float
    rune_drop_and_rarity_bonus: float
    guaranteed_ether_percent: float
    natural_ether_drop_chance: float
    next_day_production_bonus: float


@dataclass
class RuneDef:
    asset_name: str
    rune_name: str
    rarity: RuneRarity
    primary_stat: RuneStatType
    value_type: RuneValueType
    min_val: float
    max_val: float
    possible_penalties: list[dict[str, Any]]


@dataclass
class RuneSystemSpec:
    drop_chance_normal: float
    drop_chance_elite: float
    min_runes_from_boss: int
    max_runes_from_boss: int
    weight_common: int
    weight_uncommon: int
    weight_rare: int
    weight_legendary: int
    weight_cursed: int
    runes_by_rarity: dict[RuneRarity, list[RuneDef]]


@dataclass
class MetaUpgradeDef:
    id: str
    asset_name: str
    upgrade_name: str
    description: str
    cost: int
    is_unlocked_default: bool
    prerequisites: list[str]
    effect_type: MetaEffectType
    effect_value: float
    effect_value2: float
    target_building: str | None
    has_variants: bool
    is_variant_option: bool
    variant_a: str | None
    variant_b: str | None


@dataclass
class ChunkOverrideHex:
    local: tuple[int, int]
    feature: int
    feature_level: int
    elevation: int
    starting_building: str | None


@dataclass
class ChunkLayoutDef:
    layout_type: int
    road_connection_edge: tuple[int, int]
    hexes: list[ChunkOverrideHex]


@dataclass
class MetaMapSpec:
    starting_chunk_base: ChunkLayoutDef
    expansions: list[dict[str, Any]]
    global_modifiers: list[dict[str, Any]]


@dataclass
class HopeMilestoneGoal:
    id: str
    description: str
    milestone_type: MilestoneType
    target_value: int
    hope_reward: int
    group_title: str
    target_race: Race
    target_tower: str | None


@dataclass
class ExpansionCostSpec:
    min_days: int
    max_days: int
    max_distance_for_time: int
    base_ether_cost: int
    base_food_cost: int
    ether_per_hex: int
    food_per_hex: int
    road_cost_multiplier: float
    road_time_multiplier: float
    min_elves_for_survey: int
    chunks_per_extra_survey_day: int
    recon_days: int
    recon_elf_requirement: int
    recon_ether_cost: int
    recon_food_cost: int
    recon_wood_cost: int


@dataclass
class CityBaseSpec:
    start_ether: int
    start_wood: int
    start_stone: int
    start_iron: int
    start_coal: int
    start_food: int
    base_city_hp: int
    override_housing_data: bool
    human_start_pop: int
    human_max_pop: int
    elf_start_pop: int
    elf_max_pop: int
    dwarf_start_pop: int
    dwarf_max_pop: int


@dataclass
class GameData:
    project_root: Path
    city_base: CityBaseSpec
    expansion_cost: ExpansionCostSpec
    hearth_levels: dict[int, HearthLevelSpec]
    hope_milestones: list[HopeMilestoneGoal]
    buildings: dict[str, BuildingDef]
    houses: dict[str, BuildingDef]
    towers: dict[str, TowerDef]
    enemies: dict[str, EnemyDef]
    waves_predefined_50: dict[int, WaveDef]
    waves_scene_days: list[int]
    scene_spawner_params: dict[str, Any]
    rune_system: RuneSystemSpec
    meta_upgrades: dict[str, MetaUpgradeDef]
    meta_map: MetaMapSpec
    generator_config: dict[str, Any]
    workforce_defaults: dict[str, int]


def _resolve_guid(ref: Any, guid_to_path: dict[str, Path]) -> Path | None:
    if not isinstance(ref, dict):
        return None
    guid = ref.get("guid")
    if not guid or str(guid) == "0":
        return None
    return guid_to_path.get(str(guid).lower())


def _parse_costs(items: Any) -> list[ResourceCost]:
    if not items:
        return []
    return [ResourceCost(ResourceType(int(x["type"])), float(x["amount"])) for x in items]


def _parse_building_common(raw: dict[str, Any], asset_name: str, is_housing: bool = False) -> dict[str, Any]:
    er = raw.get("elevationRequirement") or {}
    elev_req = ElevationRequirementSpec(
        mode=ElevationFilterMode(int(er.get("mode", 0))),
        min_elevation=int(er.get("minElevation", 0)),
        max_elevation=int(er.get("maxElevation", 0)),
        allowed_elevations=decode_hex_int_list(er.get("allowedElevations")),
    )
    bonus_rules: list[BonusRuleSpec] = []
    for br in raw.get("bonusRules") or []:
        yields = [
            BonusYieldSpec(
                resource=ResourceType(int(ry["resourceType"])),
                base_bonus_per_hex=float(ry["baseBonusPerHex"]),
                on_top_bonus=float(ry["onTopProductionBonus"]),
                penalty_per_user=float(ry["penaltyPerUser"]),
                min_bonus=float(ry["minBonus"]),
            )
            for ry in (br.get("resourceYields") or [])
        ]
        bonus_rules.append(
            BonusRuleSpec(
                required_feature=HexFeatureType(int(br["requiredFeature"])),
                search_radius=int(br["searchRadius"]),
                yields=yields,
            )
        )
    eb_raw = raw.get("elevationBonus") or {}
    elev_bonus = ElevationBonusSpec(
        enabled=bool(int(eb_raw.get("enabled", 0))),
        resource=ResourceType(int(eb_raw.get("resourceType", 0))),
        on_top_per_level=float(eb_raw.get("onTopBonusPerLevel", 0.0)),
        adj_per_level=float(eb_raw.get("adjacentBonusPerLevel", 0.0)),
        penalty_per_extra=float(eb_raw.get("penaltyPerExtraBuilding", 0.0)),
        search_radius=int(eb_raw.get("searchRadius", 0)),
    )
    eff_table = [float(x) for x in (raw.get("efficiencyTable") or [])]
    if not eff_table and int(raw.get("type", 0)) == int(BuildingType.Defense):
        # TowerData.cs: DefaultTowerEfficiency (0.25 only if name contains Arbalest, or 0.4; .asset sets it explicitly)
        eff_table = [0.4, 1.0, 1.1, 1.2, 1.3, 1.35, 1.4]

    out: dict[str, Any] = {
        "asset_name": asset_name,
        "building_name": str(raw.get("buildingName") or asset_name),
        "building_type": BuildingType(int(raw.get("type", 0))),
        "worker_scaling_factor": float(raw.get("workerScalingFactor", 0.25)),
        "first_worker_production": float(raw.get("firstWorkerProduction", 0.7)),
        "efficiency_table": eff_table,
        "allowed_terrain": decode_hex_int_list(raw.get("allowedTerrain")),
        "elevation_req": elev_req,
        "allow_on_road": bool(int(raw.get("allowOnRoad", 0))),
        "requires_occupied": bool(int(raw.get("requiresOccupiedSpace", 0))),
        "requires_specific_citizen": bool(int(raw.get("requiresSpecificCitizen", 0))),
        "required_citizen_race": Race(int(raw.get("requiredCitizenRace", 0))),
        "is_outpost": bool(int(raw.get("isOutpost", 0))),
        "is_unlocked": bool(int(raw.get("isUnlocked", 1))),
        "base_max_workers": int(raw.get("baseMaxWorkers", 1)),
        "construction_cost": _parse_costs(raw.get("constructionCost")),
        "production_per_cycle": _parse_costs(raw.get("productionPerCycle")),
        "upkeep_per_cycle": _parse_costs(raw.get("upkeepPerCycle")),
        "bonus_rules": bonus_rules,
        "elevation_bonus": elev_bonus,
        "is_housing": is_housing,
    }
    if is_housing:
        out.update(
            {
                "housing_race": Race(int(raw.get("housingRace", 0))),
                "max_residents": int(raw.get("maxResidents", 0)),
                "initial_residents": int(raw.get("initialResidents", 0)),
                "growth_cycles": decode_hex_int_list(raw.get("growthCycles")),
                "upkeep_per_resident": _parse_costs(raw.get("upkeepPerResident")),
                "growth_surplus_cost": _parse_costs(raw.get("growthSurplusCost")),
            }
        )
    return out


def _parse_bullet_prefab(prefab_path: Path, guid_to_path: dict[str, Path]) -> dict[str, Any]:
    docs = parse_unity_yaml_documents(prefab_path)
    info: dict[str, Any] = {
        "bullet_kind": "Simple",
        "bullet_speed": 20.0,
        "bullet_radius": 0.15,
        "bullet_lifetime": 1.0,
        "pierce_count": 1,
        "extra_shards": 0,
        "spread_radius": 0.0,
        "zone_size": 1.6,
        "zone_tick_interval": 0.1,
        "zone_duration": 5.0,
    }
    scale_x = 1.0
    collider_radius = 0.5
    for d in docs:
        body = d["body"]
        if not isinstance(body, dict):
            continue
        if "Transform" in body:
            tr = body["Transform"]
            if isinstance(tr.get("m_LocalScale"), dict):
                scale_x = float(tr["m_LocalScale"].get("x", 1.0))
        if "SphereCollider" in body:
            sc = body["SphereCollider"]
            collider_radius = float(sc.get("m_Radius", 0.5))
        if "MonoBehaviour" in body:
            mb = body["MonoBehaviour"]
            sp = _resolve_guid(mb.get("m_Script"), guid_to_path)
            script_stem = sp.stem if sp else ""
            if "SimpleProjectile" in script_stem:
                info["bullet_kind"] = "Simple"
                if "speed" in mb:
                    info["bullet_speed"] = float(mb["speed"])
            elif "LinearProjectile" in script_stem:
                info["bullet_kind"] = "Linear"
                info["bullet_speed"] = float(mb.get("speed", 8.0))
                info["bullet_lifetime"] = float(mb.get("lifeTime", 1.0))
                info["pierce_count"] = int(mb.get("pierceCount", 3))
            elif "HitScanProjectile" in script_stem:
                info["bullet_kind"] = "HitScan"
            elif "FragmentedProjectile" in script_stem:
                info["bullet_kind"] = "Fragmented"
                info["extra_shards"] = int(mb.get("extraShards", 4))
                info["spread_radius"] = float(mb.get("spreadRadius", 3.0))
                shard_p = _resolve_guid(mb.get("shardPrefab"), guid_to_path)
                if shard_p and shard_p.exists():
                    shard_info = _parse_bullet_prefab(shard_p, guid_to_path)
                    info["bullet_speed"] = shard_info["bullet_speed"]
            elif "GroundProjectile" in script_stem:
                info["bullet_kind"] = "Ground"
                info["bullet_speed"] = float(mb.get("speed", 10.0))
                info["zone_tick_interval"] = float(mb.get("tickInterval", 0.1))
                info["zone_duration"] = float(mb.get("duration", 5.0))
                gz_p = _resolve_guid(mb.get("groundEffectPrefab"), guid_to_path)
                if gz_p and gz_p.exists():
                    gz_docs = parse_unity_yaml_documents(gz_p)
                    for gd in gz_docs:
                        gb = gd["body"]
                        if isinstance(gb, dict) and "Transform" in gb:
                            s = gb["Transform"].get("m_LocalScale") or {}
                            info["zone_size"] = float(s.get("x", 1.6))
    info["bullet_radius"] = collider_radius * scale_x
    return info


def _parse_enemy_skill(skill_prefab: Path, guid_to_path: dict[str, Path]) -> list[EnemySkillSpec]:
    docs = parse_unity_yaml_documents(skill_prefab)
    skills: list[EnemySkillSpec] = []
    for d in docs:
        body = d["body"]
        if not isinstance(body, dict) or "MonoBehaviour" not in body:
            continue
        mb = body["MonoBehaviour"]
        sp = _resolve_guid(mb.get("m_Script"), guid_to_path)
        stem = sp.stem if sp else ""
        if "HyperArmorSkill" in stem:
            skills.append(EnemySkillSpec(kind="HyperArmor", charges=int(mb.get("charges", 6))))
        elif "BlobSplitSkill" in stem:
            skills.append(
                EnemySkillSpec(
                    kind="BlobSplit",
                    amount_of_children=int(mb.get("amountOfChildren", 2)),
                    child_hp_percent=float(mb.get("childHpPercent", 0.5)),
                )
            )
        elif "MaddeningGazeSkill" in stem:
            skills.append(
                EnemySkillSpec(
                    kind="MaddeningGaze",
                    aura_radius=float(mb.get("auraRadius", 8.0)),
                )
            )
    return skills


def _parse_enemy_hit_radius(prefab_path: Path) -> float:
    docs = parse_unity_yaml_documents(prefab_path)
    root_scale = 1.0
    best_radius = 0.5
    for d in docs:
        body = d["body"]
        if not isinstance(body, dict):
            continue
        if "Transform" in body and body["Transform"].get("m_Father", {}).get("fileID") == 0:
            s = body["Transform"].get("m_LocalScale") or {}
            root_scale = float(s.get("x", 1.0))
        if "SphereCollider" in body:
            best_radius = float(body["SphereCollider"].get("m_Radius", 0.5))
    return best_radius * root_scale


def _parse_chunk_layout(path: Path, guid_to_path: dict[str, Path]) -> ChunkLayoutDef:
    raw = parse_unity_asset(path)
    rce = raw.get("roadConnectionEdge") or {"x": 0, "y": 0}
    hexes: list[ChunkOverrideHex] = []
    for h in raw.get("hexes") or []:
        lc = h["localCoord"]
        bp = _resolve_guid(h.get("building"), guid_to_path)
        hexes.append(
            ChunkOverrideHex(
                local=(int(lc["x"]), int(lc["y"])),
                feature=int(h.get("feature", 0)),
                feature_level=int(h.get("featureLevel", 0)),
                elevation=int(h.get("elevation", 0)),
                starting_building=bp.stem if bp else None,
            )
        )
    return ChunkLayoutDef(
        layout_type=int(raw.get("type", 0)),
        road_connection_edge=(int(rce["x"]), int(rce["y"])),
        hexes=hexes,
    )


def _extract_scene_configs(scene_path: Path, guid_to_path: dict[str, Path]) -> tuple[dict[str, Any], list[int], dict[str, Any]]:
    """Slice only HexMapGenerator, TerrainGenerationConfig, and EnemySpawner YAML docs from Game scene.unity."""
    text = scene_path.read_text(encoding="utf-8", errors="replace")
    parts = text.split("--- !u!")
    terrain_doc_text = None
    gen_doc_text = None
    spawner_doc_text = None
    for part in parts[1:]:
        if "TerrainGenerationConfig" in part and "ringRules:" in part:
            terrain_doc_text = "--- !u!" + part
        elif "mapWidth:" in part and "topologySettings:" in part and "baseRoadEndLocal:" in part:
            gen_doc_text = "--- !u!" + part
        elif "predefinedWaves:" in part and "enableLateScaling:" in part:
            spawner_doc_text = "--- !u!" + part

    # Parse via yaml_loader temporary string reuse
    from .yaml_loader import _preprocess_lines, _parse_block

    def _parse_doc(doc_str: str) -> dict[str, Any]:
        lines = doc_str.splitlines()[1:]  # skip header line
        pp = _preprocess_lines("\n".join(lines))
        parsed, _ = _parse_block(pp, 0, 0)
        return parsed.get("MonoBehaviour", parsed)

    gen_raw = _parse_doc(gen_doc_text) if gen_doc_text else {}
    ter_raw = _parse_doc(terrain_doc_text) if terrain_doc_text else {}
    spw_raw = _parse_doc(spawner_doc_text) if spawner_doc_text else {}

    topo = gen_raw.get("topologySettings") or {}
    intl = gen_raw.get("internalSettings") or {}
    edr = gen_raw.get("extraSpawnDistanceRange") or {"x": 2, "y": 6}
    lsr = topo.get("loopSpanRange") or {"x": 2, "y": 4}
    bre = gen_raw.get("baseRoadEndLocal") or {"x": 3, "y": -2}

    ring_rules = []
    for r in ter_raw.get("ringRules") or []:
        ring_rules.append(
            {
                "ruleName": str(r.get("ruleName", "")),
                "ringMin": int(r["ringMin"]),
                "ringMax": int(r["ringMax"]),
                "forestWeight": int(r["forestWeight"]),
                "fertileSoilWeight": int(r["fertileSoilWeight"]),
                "hillWeight": int(r["hillWeight"]),
                "mountainWeight": int(r["mountainWeight"]),
                "sinkholeWeight": int(r["sinkholeWeight"]),
                "emptyWeight": int(r["emptyWeight"]),
                "clusterForests": bool(int(r["clusterForests"])),
                "forestClusterChance": int(r["forestClusterChance"]),
                "minForestClusters": int(r["minForestClusters"]),
                "maxForestClusters": int(r["maxForestClusters"]),
                "clusterMountains": bool(int(r["clusterMountains"])),
                "mountainClusterChance": int(r["mountainClusterChance"]),
                "minMountainClusters": int(r["minMountainClusters"]),
                "maxMountainClusters": int(r["maxMountainClusters"]),
            }
        )

    guarantees = []
    for g in ter_raw.get("guarantees") or []:
        guarantees.append(
            {
                "guaranteeName": str(g.get("guaranteeName", "")),
                "ringMin": int(g["ringMin"]),
                "ringMax": int(g["ringMax"]),
                "feature": int(g["feature"]),
                "minCount": int(g["minCount"]),
                "minDistinctChunks": int(g["minDistinctChunks"]),
                "maxAllowed": int(g["maxAllowed"]),
            }
        )

    gen_cfg = {
        "mapWidth": int(gen_raw.get("mapWidth", 8)),
        "mapMinY": int(gen_raw.get("mapMinY", -3)),
        "mapMaxY": int(gen_raw.get("mapMaxY", 3)),
        "chunkRadius": int(gen_raw.get("chunkRadius", 3)),
        "hexSize": float(gen_raw.get("hexSize", 1.0)),
        "padding": float(gen_raw.get("padding", 0.0)),
        "minChunkDistance": int(gen_raw.get("minChunkDistance", 11)),
        "maxGenerationAttempts": int(gen_raw.get("maxGenerationAttempts", 1000)),
        "allowExtraSpawners": bool(int(gen_raw.get("allowExtraSpawners", 1))),
        "extraSpawnerChances": decode_hex_int_list(gen_raw.get("extraSpawnerChances")),
        "extraSpawnDistanceRange": [int(edr["x"]), int(edr["y"])],
        "chanceForMoreBranching": int(gen_raw.get("chanceForMoreBranching", 30)),
        "globalObstacleChance": int(gen_raw.get("globalObstacleChance", 70)),
        "baseRoadEndLocal": [int(bre["x"]), int(bre["y"])],
        "topologySettings": {
            "safeStartChunks": int(topo.get("safeStartChunks", 2)),
            "minStartRoadLength": int(topo.get("minStartRoadLength", 6)),
            "minChunksBetweenCrossroads": int(topo.get("minChunksBetweenCrossroads", 1)),
            "guaranteedLoops": int(topo.get("guaranteedLoops", 1)),
            "extraLoopChance": int(topo.get("extraLoopChance", 30)),
            "loopSpanRange": [int(lsr["x"]), int(lsr["y"])],
            "loopFlowRatio": float(topo.get("loopFlowRatio", 0.3)),
            "sideBranchChances": decode_hex_int_list(topo.get("sideBranchChances")),
            "branchFlowRatio": float(topo.get("branchFlowRatio", 0.3)),
        },
        "internalSettings": {
            "highwayWeight": int(intl.get("highwayWeight", 20)),
            "windingWeight": int(intl.get("windingWeight", 30)),
            "mazeWeight": int(intl.get("mazeWeight", 50)),
            "wallCost": int(intl.get("wallCost", 50)),
            "windingWallCount": int(intl.get("windingWallCount", 2)),
            "windingWallLength": int(intl.get("windingWallLength", 3)),
            "mazeWallCount": int(intl.get("mazeWallCount", 5)),
            "mazeWallLength": int(intl.get("mazeWallLength", 3)),
        },
        "terrainConfig": {
            "ringRules": ring_rules,
            "guarantees": guarantees,
        },
    }

    scene_wave_days: list[int] = []
    for wref in spw_raw.get("predefinedWaves") or []:
        wp = _resolve_guid(wref, guid_to_path)
        if wp and wp.exists():
            w_raw = parse_unity_asset(wp)
            scene_wave_days.append(int(w_raw["dayNumber"]))

    spawner_params = {
        "nightSpawnInterval": float(spw_raw.get("nightSpawnInterval", 1.0)),
        "enableLateScaling": bool(int(spw_raw.get("enableLateScaling", 1))),
        "lateScalingBase": float(spw_raw.get("lateScalingBase", 1.1)),
        "fixedWaveSeed": int(spw_raw.get("fixedWaveSeed", 0)),
        "baseFormulaConst": float(spw_raw.get("baseFormulaConst", 10.0)),
        "perSpawnerMultiplier": float(spw_raw.get("perSpawnerMultiplier", 2.5)),
    }
    return gen_cfg, scene_wave_days, spawner_params


def load_game_data(project_root: str | Path) -> GameData:
    project_root = Path(project_root).resolve()
    assets_root = project_root / "Assets" / "Main folder"
    so_root = assets_root / "ScriptableObjects"
    guid_to_path, _ = build_guid_index(assets_root)

    # 1. CityBaseConfig
    cb_raw = parse_unity_asset(so_root / "Configs" / "CityBaseConfig.asset")
    city_base = CityBaseSpec(
        start_ether=int(cb_raw["startEther"]),
        start_wood=int(cb_raw["startWood"]),
        start_stone=int(cb_raw["startStone"]),
        start_iron=int(cb_raw["startIron"]),
        start_coal=int(cb_raw["startCoal"]),
        start_food=int(cb_raw["startFood"]),
        base_city_hp=int(cb_raw["baseCityHP"]),
        override_housing_data=bool(int(cb_raw["overrideHousingData"])),
        human_start_pop=int(cb_raw["humanStartPop"]),
        human_max_pop=int(cb_raw["humanMaxPop"]),
        elf_start_pop=int(cb_raw["elfStartPop"]),
        elf_max_pop=int(cb_raw["elfMaxPop"]),
        dwarf_start_pop=int(cb_raw["dwarfStartPop"]),
        dwarf_max_pop=int(cb_raw["dwarfMaxPop"]),
    )

    # 2. ExpansionCostConfig
    ec_raw = parse_unity_asset(so_root / "Configs" / "ExpansionCostConfig.asset")
    expansion_cost = ExpansionCostSpec(
        min_days=int(ec_raw["minDays"]),
        max_days=int(ec_raw["maxDays"]),
        max_distance_for_time=int(ec_raw["maxDistanceForTime"]),
        base_ether_cost=int(ec_raw["baseEtherCost"]),
        base_food_cost=int(ec_raw["baseFoodCost"]),
        ether_per_hex=int(ec_raw["etherPerHex"]),
        food_per_hex=int(ec_raw["foodPerHex"]),
        road_cost_multiplier=float(ec_raw["roadCostMultiplier"]),
        road_time_multiplier=float(ec_raw["roadTimeMultiplier"]),
        min_elves_for_survey=int(ec_raw["minElvesForSurvey"]),
        chunks_per_extra_survey_day=int(ec_raw["chunksPerExtraSurveyDay"]),
        recon_days=int(ec_raw["reconDays"]),
        recon_elf_requirement=int(ec_raw["reconElfRequirement"]),
        recon_ether_cost=int(ec_raw["reconEtherCost"]),
        recon_food_cost=int(ec_raw["reconFoodCost"]),
        recon_wood_cost=int(ec_raw["reconWoodCost"]),
    )

    # 3. HearthConfig
    hc_raw = parse_unity_asset(so_root / "Buildings" / "Special" / "HearthConfig.asset")
    hearth_levels: dict[int, HearthLevelSpec] = {}
    for lv in hc_raw.get("levels") or []:
        idx = int(lv["levelIndex"])
        hearth_levels[idx] = HearthLevelSpec(
            level_index=idx,
            title=str(lv["title"]),
            coal_cost=int(lv["coalCost"]),
            tower_damage_bonus=float(lv["towerDamageBonus"]),
            tower_fire_rate_bonus=float(lv["towerFireRateBonus"]),
            tower_range_bonus=float(lv["towerRangeBonus"]),
            enemy_stats_bonus=float(lv["enemyStatsBonus"]),
            rune_drop_and_rarity_bonus=float(lv["runeDropAndRarityBonus"]),
            guaranteed_ether_percent=float(lv["guaranteedEtherPercent"]),
            natural_ether_drop_chance=float(lv["naturalEtherDropChance"]),
            next_day_production_bonus=float(lv["nextDayProductionBonus"]),
        )

    # 4. HopeMilestonesConfig
    hm_raw = parse_unity_asset(so_root / "Configs" / "HopeMilestonesConfig.asset")
    hope_milestones: list[HopeMilestoneGoal] = []
    for m in hm_raw.get("milestones") or []:
        tp = _resolve_guid(m.get("targetTower"), guid_to_path)
        hope_milestones.append(
            HopeMilestoneGoal(
                id=str(m["id"]),
                description=str(m.get("description", "")),
                milestone_type=MilestoneType(int(m["type"])),
                target_value=int(m["targetValue"]),
                hope_reward=int(m["hopeReward"]),
                group_title=str(m.get("groupTitle", "")),
                target_race=Race(int(m.get("targetRace", 0))),
                target_tower=tp.stem if tp else None,
            )
        )

    # 5. Buildings & Houses (non-tower)
    buildings: dict[str, BuildingDef] = {}
    for p in sorted((so_root / "Buildings").glob("*.asset")):
        raw = parse_unity_asset(p)
        kwargs = _parse_building_common(raw, p.stem, is_housing=False)
        prefab_p = _resolve_guid(raw.get("prefab"), guid_to_path)
        if prefab_p and prefab_p.exists():
            for d in parse_unity_yaml_documents(prefab_p):
                body = d["body"]
                if isinstance(body, dict) and "MonoBehaviour" in body:
                    mb = body["MonoBehaviour"]
                    if "daysUntilTransform" in mb:
                        kwargs["outpost_days_until_transform"] = int(mb["daysUntilTransform"])
                    if "equipmentProvided" in mb:
                        kwargs["expedition_equipment_provided"] = int(mb["equipmentProvided"])
        buildings[p.stem] = BuildingDef(**kwargs)

    houses: dict[str, BuildingDef] = {}
    for p in sorted((so_root / "Buildings" / "Houses").glob("*.asset")):
        raw = parse_unity_asset(p)
        kwargs = _parse_building_common(raw, p.stem, is_housing=True)
        hdef = BuildingDef(**kwargs)
        houses[p.stem] = hdef
        buildings[p.stem] = hdef

    # 6. Towers
    towers: dict[str, TowerDef] = {}
    for p in sorted((so_root / "Towers").glob("*.asset")):
        raw = parse_unity_asset(p)
        kwargs = _parse_building_common(raw, p.stem, is_housing=False)
        bp = _resolve_guid(raw.get("bulletPrefab"), guid_to_path)
        b_info = _parse_bullet_prefab(bp, guid_to_path) if (bp and bp.exists()) else {}
        prios = [TargetingMode(x) for x in decode_hex_int_list(raw.get("defaultTargetingPriorities"))]
        tdef = TowerDef(
            **kwargs,
            base_range=float(raw["baseRange"]),
            base_damage=float(raw["baseDamage"]),
            fire_rate=float(raw["fireRate"]),
            crit_chance=float(raw["criticalChancel"]),
            crit_mult=float(raw["criticalDamageMultiplier"]),
            armor_pen=float(raw["armorPenetration"]),
            magic_pen=float(raw["magicPenetration"]),
            damage_type=DamageType(int(raw["damageType"])),
            can_shoot_chunk=bool(int(raw.get("canShootChunk", 0))),
            default_priorities=prios,
            **b_info,
        )
        towers[p.stem] = tdef

    # 7. Enemies
    enemies: dict[str, EnemyDef] = {}
    for p in sorted((so_root / "Enemies").glob("*.asset")):
        raw = parse_unity_asset(p)
        ep = _resolve_guid(raw.get("prefab"), guid_to_path)
        hit_radius = _parse_enemy_hit_radius(ep) if (ep and ep.exists()) else 0.5
        base_skills: list[EnemySkillSpec] = []
        for sref in raw.get("skillPrefabs") or []:
            sp = _resolve_guid(sref, guid_to_path)
            if sp and sp.exists():
                base_skills.extend(_parse_enemy_skill(sp, guid_to_path))
        tiers: list[EnemyTierSpec] = []
        for t in raw.get("tiers") or []:
            if int(t.get("overrideSkills", 0)):
                tskills: list[EnemySkillSpec] = []
                for sref in t.get("skillPrefabs") or []:
                    sp = _resolve_guid(sref, guid_to_path)
                    if sp and sp.exists():
                        tskills.extend(_parse_enemy_skill(sp, guid_to_path))
            else:
                tskills = list(base_skills)
            tiers.append(
                EnemyTierSpec(
                    base_hp=float(t["baseHp"]),
                    move_speed=float(t["moveSpeed"]),
                    armor=float(t["armor"]),
                    magic_resist=float(t["magicResist"]),
                    dodge_chance=float(t["dodgeChance"]),
                    skills=tskills,
                )
            )
        if not tiers:
            tiers.append(
                EnemyTierSpec(
                    base_hp=float(raw["baseHp"]),
                    move_speed=float(raw["moveSpeed"]),
                    armor=float(raw["armor"]),
                    magic_resist=float(raw["magicResist"]),
                    dodge_chance=float(raw["dodgeChance"]),
                    skills=base_skills,
                )
            )
        enemies[p.stem] = EnemyDef(
            asset_name=p.stem,
            enemy_name=str(raw.get("enemyName") or p.stem),
            threat_cost=int(raw.get("threatCost", 1)),
            rank=EnemyRank(int(raw.get("rank", 0))),
            can_be_elite=bool(int(raw.get("canBeElite", 0))),
            elite_spawn_chance=float(raw.get("eliteSpawnChance", 0.0)),
            elite_hp_mult=float(raw.get("eliteHpMultiplier", 2.0)),
            elite_speed_mult=float(raw.get("eliteSpeedMultiplier", 1.3)),
            ether_drop_elite=int(raw.get("etherDropElite", 0)),
            ether_drop_boss=int(raw.get("etherDropBoss", 0)),
            hit_radius=hit_radius,
            tiers=tiers,
        )

    # 8. Predefined Waves (1..50)
    def _parse_wave_entries(items: Any) -> list[WaveEntrySpec]:
        out: list[WaveEntrySpec] = []
        for x in items or []:
            ep = _resolve_guid(x.get("enemy"), guid_to_path)
            if not ep:
                continue
            out.append(
                WaveEntrySpec(
                    enemy_asset=ep.stem,
                    tier=int(x.get("tier", 1)),
                    count=int(x.get("count", 1)),
                    force_elite=bool(int(x.get("forceElite", 0))),
                )
            )
        return out

    waves_50: dict[int, WaveDef] = {}
    for p in sorted((so_root / "PredefinedWaves").glob("*.asset")):
        raw = parse_unity_asset(p)
        day = int(raw["dayNumber"])
        groups: list[SpawnGroupSpec] = []
        for g in raw.get("spawnGroups") or []:
            groups.append(
                SpawnGroupSpec(
                    name=str(g.get("groupName", "")),
                    delay_before=float(g.get("delayBefore", 0.0)),
                    spawn_interval=float(g.get("spawnInterval", 0.9)),
                    interval_jitter=float(g.get("intervalJitter", 0.0)),
                    shuffle_inside=bool(int(g.get("shuffleInside", 0))),
                    entries=_parse_wave_entries(g.get("enemies")),
                )
            )
        waves_50[day] = WaveDef(
            day_number=day,
            ether_pool=int(raw.get("etherPool", 0)),
            custom_spawn_interval=float(raw.get("customSpawnInterval", 0.0)),
            hp_mult=float(raw.get("hpMultiplier", 1.0)),
            speed_mult=float(raw.get("speedMultiplier", 1.0)),
            shuffle_enemies=bool(int(raw.get("shuffleEnemies", 0))),
            allow_random_elites=bool(int(raw.get("allowRandomElites", 0))),
            max_elites_count=int(raw.get("maxElitesCount", 0)),
            entries=_parse_wave_entries(raw.get("enemies")),
            spawn_groups=groups,
        )

    # 9. Runes
    rs_raw = parse_unity_asset(so_root / "Runes" / "DropChanceConfig" / "RuneSystemConfig.asset")
    runes_by_path: dict[Path, RuneDef] = {}
    for p in sorted((so_root / "Runes" / "Runes").rglob("*.asset")):
        r_raw = parse_unity_asset(p)
        pvr = r_raw.get("primaryValueRange") or {"x": 0.0, "y": 0.0}
        penalties = []
        for pen in r_raw.get("possiblePenalties") or []:
            vr = pen.get("valueRange") or {"x": 0.0, "y": 0.0}
            penalties.append(
                {
                    "stat": RuneStatType(int(pen["stat"])),
                    "value_type": RuneValueType(int(pen["valueType"])),
                    "min_val": float(vr["x"]),
                    "max_val": float(vr["y"]),
                }
            )
        runes_by_path[p.resolve()] = RuneDef(
            asset_name=p.stem,
            rune_name=str(r_raw.get("runeName") or p.stem),
            rarity=RuneRarity(int(r_raw["rarity"])),
            primary_stat=RuneStatType(int(r_raw["primaryStat"])),
            value_type=RuneValueType(int(r_raw["valueType"])),
            min_val=float(pvr["x"]),
            max_val=float(pvr["y"]),
            possible_penalties=penalties,
        )

    def _collect_rune_list(key: str, rarity: RuneRarity) -> list[RuneDef]:
        out: list[RuneDef] = []
        for ref in rs_raw.get(key) or []:
            rp = _resolve_guid(ref, guid_to_path)
            if rp and rp.resolve() in runes_by_path:
                out.append(runes_by_path[rp.resolve()])
        if not out:
            out = [r for r in runes_by_path.values() if r.rarity == rarity]
        return out

    rune_system = RuneSystemSpec(
        drop_chance_normal=float(rs_raw.get("dropChanceNormal", 2.0)),
        drop_chance_elite=float(rs_raw.get("dropChanceElite", 100.0)),
        min_runes_from_boss=int(rs_raw.get("minRunesFromBoss", 2)),
        max_runes_from_boss=int(rs_raw.get("maxRunesFromBoss", 4)),
        weight_common=int(rs_raw.get("weightCommon", 60)),
        weight_uncommon=int(rs_raw.get("weightUncommon", 20)),
        weight_rare=int(rs_raw.get("weightRare", 10)),
        weight_legendary=int(rs_raw.get("weightLegendary", 6)),
        weight_cursed=int(rs_raw.get("weightCursed", 4)),
        runes_by_rarity={
            RuneRarity.Common: _collect_rune_list("commonRunes", RuneRarity.Common),
            RuneRarity.Uncommon: _collect_rune_list("uncommonRunes", RuneRarity.Uncommon),
            RuneRarity.Rare: _collect_rune_list("rareRunes", RuneRarity.Rare),
            RuneRarity.Legendary: _collect_rune_list("legendaryRunes", RuneRarity.Legendary),
            RuneRarity.Cursed: _collect_rune_list("cursedRunes", RuneRarity.Cursed),
        },
    )

    # 10. Meta Upgrades
    meta_raw_by_path: dict[Path, tuple[str, dict[str, Any]]] = {}
    path_to_meta_id: dict[Path, str] = {}
    for p in sorted((so_root / "Meta").glob("*.asset")):
        m_raw = parse_unity_asset(p)
        mid = str(m_raw.get("id") or p.stem)
        meta_raw_by_path[p.resolve()] = (mid, m_raw)
        path_to_meta_id[p.resolve()] = mid

    meta_upgrades: dict[str, MetaUpgradeDef] = {}
    for p_res, (mid, m_raw) in meta_raw_by_path.items():
        prereqs: list[str] = []
        for pref in m_raw.get("prerequisites") or []:
            pp = _resolve_guid(pref, guid_to_path)
            if pp and pp.resolve() in path_to_meta_id:
                prereqs.append(path_to_meta_id[pp.resolve()])
        tb_p = _resolve_guid(m_raw.get("targetBuilding"), guid_to_path)
        va_p = _resolve_guid(m_raw.get("variantA"), guid_to_path)
        vb_p = _resolve_guid(m_raw.get("variantB"), guid_to_path)
        et_val = int(m_raw.get("effectType", 0))
        try:
            et = MetaEffectType(et_val)
        except ValueError:
            et = MetaEffectType.NoneEffect
        meta_upgrades[mid] = MetaUpgradeDef(
            id=mid,
            asset_name=p_res.stem,
            upgrade_name=str(m_raw.get("upgradeName") or p_res.stem),
            description=str(m_raw.get("description") or ""),
            cost=int(m_raw.get("cost", 0)),
            is_unlocked_default=bool(int(m_raw.get("isUnlocked", 0))),
            prerequisites=prereqs,
            effect_type=et,
            effect_value=float(m_raw.get("effectValue", 0.0)),
            effect_value2=float(m_raw.get("effectValue2", 0.0)),
            target_building=tb_p.stem if tb_p else None,
            has_variants=bool(int(m_raw.get("hasVariants", 0))),
            is_variant_option=bool(int(m_raw.get("isVariantOption", 0))),
            variant_a=path_to_meta_id[va_p.resolve()] if (va_p and va_p.resolve() in path_to_meta_id) else None,
            variant_b=path_to_meta_id[vb_p.resolve()] if (vb_p and vb_p.resolve() in path_to_meta_id) else None,
        )

    # 11. MetaMapConfig & Starting chunk layouts
    mm_raw = parse_unity_asset(so_root / "Starting chunks" / "MetaMapConfig.asset")
    sc_base_p = _resolve_guid(mm_raw.get("startingChunkBase"), guid_to_path)
    starting_base = (
        _parse_chunk_layout(sc_base_p, guid_to_path)
        if sc_base_p
        else ChunkLayoutDef(0, (3, -2), [])
    )
    expansions: list[dict[str, Any]] = []
    for exp in mm_raw.get("expansions") or []:
        cc = exp["chunkCoordinate"]
        ru_p = _resolve_guid(exp.get("requiredUpgrade"), guid_to_path)
        bl_p = _resolve_guid(exp.get("baseLayout"), guid_to_path)
        expansions.append(
            {
                "chunk_coord": (int(cc["x"]), int(cc["y"])),
                "required_upgrade": path_to_meta_id[ru_p.resolve()] if (ru_p and ru_p.resolve() in path_to_meta_id) else None,
                "layout": _parse_chunk_layout(bl_p, guid_to_path) if bl_p else None,
            }
        )
    global_mods: list[dict[str, Any]] = []
    for gm in mm_raw.get("globalModifiers") or []:
        tc = gm["targetChunk"]
        ru_p = _resolve_guid(gm.get("requiredUpgrade"), guid_to_path)
        ml_p = _resolve_guid(gm.get("modifierLayout"), guid_to_path)
        global_mods.append(
            {
                "name": str(gm.get("name", "")),
                "target_chunk": (int(tc["x"]), int(tc["y"])),
                "required_upgrade": path_to_meta_id[ru_p.resolve()] if (ru_p and ru_p.resolve() in path_to_meta_id) else None,
                "layout": _parse_chunk_layout(ml_p, guid_to_path) if ml_p else None,
            }
        )
    meta_map = MetaMapSpec(
        starting_chunk_base=starting_base,
        expansions=expansions,
        global_modifiers=global_mods,
    )

    # 12. Scene configs (HexMapGenerator, TerrainGenerationConfig, EnemySpawner)
    gen_cfg, scene_wave_days, spawner_params = _extract_scene_configs(
        assets_root / "_Scenes" / "Game scene.unity", guid_to_path
    )

    return GameData(
        project_root=project_root,
        city_base=city_base,
        expansion_cost=expansion_cost,
        hearth_levels=hearth_levels,
        hope_milestones=hope_milestones,
        buildings=buildings,
        houses=houses,
        towers=towers,
        enemies=enemies,
        waves_predefined_50=waves_50,
        waves_scene_days=scene_wave_days,
        scene_spawner_params=spawner_params,
        rune_system=rune_system,
        meta_upgrades=meta_upgrades,
        meta_map=meta_map,
        generator_config=gen_cfg,
        workforce_defaults={"base_shifts": 1, "base_workers_per_shift": 1},
    )
