from __future__ import annotations

import json
import math
import subprocess
import tempfile
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any

from .assets_loader import (
    BuildingDef,
    ExpansionCostSpec,
    GameData,
    TowerDef,
)
from .enums import (
    BuildingType,
    ChunkState,
    ElevationFilterMode,
    HexFeatureType,
    MetaEffectType,
    ResourceType,
)
from .meta import MetaState

AXIAL_DIRS = ((1, 0), (1, -1), (0, -1), (-1, 0), (-1, 1), (0, 1))
HEX_TO_WORLD_RANGE_MULT = 2.0


def axial_distance(a: tuple[int, int], b: tuple[int, int]) -> int:
    dq = a[0] - b[0]
    dr = a[1] - b[1]
    return (abs(dq) + abs(dq + dr) + abs(dr)) // 2


def csharp_round_to_int(x: float) -> int:
    """Match C# Mathf.RoundToInt / Math.Round (banker's rounding to even on .5)."""
    return int(round(x))


@dataclass
class HexTile:
    chunk: tuple[int, int]
    local: tuple[int, int]
    global_coord: tuple[int, int]
    world_x: float
    world_z: float
    feature: int
    feature_name: str
    feature_level: int
    elevation: int
    is_road: bool
    starting_building: str | None = None
    occupied_by: int | None = None  # -1=Base, -2=Beacon, >=1=building id


@dataclass
class ChunkInfo:
    coord: tuple[int, int]
    ring: int
    chunk_type: str
    state: ChunkState = ChunkState.Wilderness
    recon_completed: bool = False
    is_road_chunk: bool = False
    has_outpost: bool = False
    outpost_transformed: bool = False
    path_data: dict[str, Any] | None = None
    hex_coords: list[tuple[int, int]] = field(default_factory=list)


@dataclass
class PlacedBuilding:
    uid: int
    asset_name: str
    building_def: BuildingDef
    global_coord: tuple[int, int]
    chunk: tuple[int, int]
    world_x: float
    world_z: float
    build_order: int
    built_day: int
    # Runtime mutable fields used across engine/combat
    residents: int = 0
    growth_progress: int = 0
    last_survival_paid: bool = True
    assigned_workers: dict[int, int] = field(default_factory=dict)  # Race(int) -> count
    production_buffer: dict[ResourceType, float] = field(default_factory=dict)
    days_until_transform: int = 10
    is_active: bool = True
    last_upkeep_paid: bool = True
    targeting_mode: str = "FurthestOnTrack"


@dataclass
class ActiveRouteSegment:
    x0: float
    z0: float
    x1: float
    z1: float
    length: float
    chunk: tuple[int, int]
    is_surveying: bool


@dataclass
class ActiveRoute:
    name: str
    flow_weight: float
    spawner_chunk: tuple[int, int]
    is_loop_route: bool
    waypoints: list[tuple[float, float, tuple[int, int]]]  # (x, z, chunk)
    segments: list[ActiveRouteSegment]
    total_length: float


class MapWorld:
    def __init__(
        self,
        raw_map: dict[str, Any],
        game_data: GameData,
        meta_state: MetaState,
    ) -> None:
        self.seed: int = int(raw_map["seed"])
        self.chunk_radius: int = int(raw_map["chunkRadius"])
        self.hex_size: float = float(raw_map["hexSize"])
        self.padding: float = float(raw_map["padding"])
        self.main_spawner_chunk: tuple[int, int] = tuple(raw_map["mainSpawnerChunk"])
        self.extra_spawner_chunks: list[tuple[int, int]] = [
            tuple(c) for c in raw_map["extraSpawnerChunks"]
        ]
        self.crossroads_chunks: list[tuple[int, int]] = [
            tuple(c) for c in raw_map["crossroadsChunks"]
        ]
        self.game_data = game_data
        self.meta_state = meta_state

        self.chunks: dict[tuple[int, int], ChunkInfo] = {}
        self.hexes: dict[tuple[int, int], HexTile] = {}
        self.spawn_routes_raw: list[dict[str, Any]] = list(raw_map["spawnRoutes"])
        self.road_dependencies: dict[tuple[int, int], list[tuple[int, int]]] = {
            tuple(d["chunk"]): [tuple(x) for x in d["dependsOn"]]
            for d in raw_map.get("roadDependencies", [])
        }

        for cobj in raw_map["chunks"]:
            coord = tuple(cobj["coord"])
            pdata = cobj.get("path")
            cinfo = ChunkInfo(
                coord=coord,
                ring=int(cobj["ring"]),
                chunk_type=str(cobj["chunkType"]),
                is_road_chunk=(pdata is not None) or any(bool(h["isRoad"]) for h in cobj["hexes"]),
                path_data=pdata,
            )
            for h in cobj["hexes"]:
                gc = tuple(h["global"])
                feat = int(h["feature"])
                occ = None
                if feat == int(HexFeatureType.Base):
                    occ = -1
                elif feat == int(HexFeatureType.Beacon):
                    occ = -2
                tile = HexTile(
                    chunk=coord,
                    local=tuple(h["local"]),
                    global_coord=gc,
                    world_x=float(h["world"][0]),
                    world_z=float(h["world"][1]),
                    feature=feat,
                    feature_name=str(h["featureName"]),
                    feature_level=int(h["featureLevel"]),
                    elevation=int(h["elevation"]),
                    is_road=bool(h["isRoad"]),
                    starting_building=h.get("startingBuilding"),
                    occupied_by=occ,
                )
                self.hexes[gc] = tile
                cinfo.hex_coords.append(gc)
            self.chunks[coord] = cinfo

        # Pre-tag route waypoints with exact containing chunk (nearest hex center = exact hex Voronoi)
        self._route_waypoints_with_chunks: list[dict[str, Any]] = []
        for r in self.spawn_routes_raw:
            tagged_pts: list[tuple[float, float, tuple[int, int]]] = []
            for p in r["fullPath"]:
                wx, wz = float(p[0]), float(p[2])
                c = self.find_chunk_of_world_pos(wx, wz)
                tagged_pts.append((wx, wz, c))
            self._route_waypoints_with_chunks.append(
                {
                    "name": r["name"],
                    "flowWeight": float(r["flowWeight"]),
                    "spawnerChunk": tuple(r["spawnerChunk"]),
                    "isLoopRoute": bool(r["isLoopRoute"]),
                    "taggedPath": tagged_pts,
                }
            )

        # Initialize starting Fog of War state:
        # (0,0) and meta-unlocked expansion chunks start Settled; propagate Borderlands!
        if (0, 0) in self.chunks:
            self.chunks[(0, 0)].state = ChunkState.Settled
        for exp in game_data.meta_map.expansions:
            if meta_state.is_id_unlocked(exp["required_upgrade"]):
                cc = exp["chunk_coord"]
                if cc in self.chunks:
                    self.chunks[cc].state = ChunkState.Settled
        self.propagate_borderlands()

    @classmethod
    def generate(
        cls,
        seed: int,
        game_data: GameData,
        meta_state: MetaState,
        exporter_exe: Path | None = None,
    ) -> "MapWorld":
        if exporter_exe is None:
            exporter_exe = (
                Path(__file__).resolve().parent.parent
                / "MapExporter"
                / "bin"
                / "Release"
                / "net9.0"
                / "MapExporter.exe"
            )
        cfg = meta_state.build_map_exporter_config(game_data)
        with tempfile.NamedTemporaryFile(
            mode="w", suffix=".json", delete=False, encoding="utf-8"
        ) as tf:
            json.dump(cfg, tf)
            cfg_path = Path(tf.name)
        try:
            cmd = (
                [str(exporter_exe), "--seed", str(seed), "--config", str(cfg_path)]
                if exporter_exe.exists()
                else [
                    "dotnet",
                    "run",
                    "--project",
                    str(Path(__file__).resolve().parent.parent / "MapExporter"),
                    "--",
                    "--seed",
                    str(seed),
                    "--config",
                    str(cfg_path),
                ]
            )
            proc = subprocess.run(
                cmd,
                capture_output=True,
                text=True,
                check=True,
                encoding="utf-8",
            )
            raw_map = json.loads(proc.stdout)
        finally:
            cfg_path.unlink(missing_ok=True)
        return cls(raw_map=raw_map, game_data=game_data, meta_state=meta_state)

    def find_chunk_of_world_pos(self, wx: float, wz: float) -> tuple[int, int]:
        best_dist = 1e18
        best_chunk = (0, 0)
        for h in self.hexes.values():
            dx = h.world_x - wx
            dz = h.world_z - wz
            d2 = dx * dx + dz * dz
            if d2 < best_dist:
                best_dist = d2
                best_chunk = h.chunk
        return best_chunk

    def is_chunk_revealed(self, coord: tuple[int, int]) -> bool:
        ch = self.chunks.get(coord)
        return ch is not None and ch.state in (ChunkState.Outskirts, ChunkState.Settled)

    def is_chunk_effective_settled(self, coord: tuple[int, int]) -> bool:
        ch = self.chunks.get(coord)
        if ch is None:
            return False
        return ch.state == ChunkState.Settled or (ch.state == ChunkState.Outskirts and ch.has_outpost)

    def is_chunk_open_for_spawn(self, coord: tuple[int, int]) -> bool:
        ch = self.chunks.get(coord)
        return ch is not None and ch.state in (
            ChunkState.Settled,
            ChunkState.Outskirts,
            ChunkState.Surveying,
        )

    def propagate_borderlands(self) -> list[tuple[int, int]]:
        newly_borderlands: list[tuple[int, int]] = []
        settled_or_outskirts = [
            c for c, info in self.chunks.items() if info.state in (ChunkState.Settled, ChunkState.Outskirts)
        ]
        for sc in settled_or_outskirts:
            for dq, dr in AXIAL_DIRS:
                nb = (sc[0] + dq, sc[1] + dr)
                nb_info = self.chunks.get(nb)
                if nb_info and nb_info.state == ChunkState.Wilderness:
                    nb_info.state = ChunkState.Borderlands
                    newly_borderlands.append(nb)
        return newly_borderlands

    def discovered_extra_chunks_count(self) -> int:
        # Chunks settled/outskirts beyond starting (0,0) + meta starting chunks?
        # Look at CombatOptimizationTests Test51/Test54: caller passes discoveredChunks beyond starting zone
        # matching active Outskirts + Settled player-expanded chunks!
        base_settled = 1 + sum(
            1
            for e in self.game_data.meta_map.expansions
            if self.meta_state.is_id_unlocked(e["required_upgrade"]) and e["chunk_coord"] in self.chunks
        )
        cur_open = sum(
            1
            for c in self.chunks.values()
            if c.state in (ChunkState.Outskirts, ChunkState.Settled)
        )
        return max(0, cur_open - base_settled)

    def calculate_survey_cost(
        self, chunk_coord: tuple[int, int], assigned_elves: int = 1
    ) -> tuple[int, int, int, int]:
        """Exact C# ExpansionCostCalculator.CalculateSurveyCost parity: returns (days, ether, food, wood)."""
        cfg: ExpansionCostSpec = self.game_data.expansion_cost
        ch = self.chunks[chunk_coord]
        elves = max(cfg.min_elves_for_survey, assigned_elves)
        dist = max(1, axial_distance((0, 0), chunk_coord))
        max_dist = max(2, cfg.max_distance_for_time)
        t = min(1.0, max(0.0, (dist - 1) / float(max_dist - 1)))
        base_days = csharp_round_to_int(cfg.min_days + (cfg.max_days - cfg.min_days) * t)
        if ch.is_road_chunk:
            base_days = csharp_round_to_int(base_days * cfg.road_time_multiplier)
        extra_chunks = self.discovered_extra_chunks_count()
        extra_days = (
            extra_chunks // cfg.chunks_per_extra_survey_day
            if cfg.chunks_per_extra_survey_day > 0
            else 0
        )
        days = max(1, base_days + extra_days)

        tier = extra_chunks
        if tier < 4:
            food_per_elf = 10 + 5 * tier
            wood_per_elf = 5 + 2 * tier
            ether_per_elf = 0
        else:
            n = tier - 4
            food_per_elf = 30 + 4 * n
            wood_per_elf = 15 + 1 * n
            ether_per_elf = 4 + 2 * n

        ether = ether_per_elf * elves
        food = food_per_elf * elves
        wood = wood_per_elf * elves
        if ch.is_road_chunk:
            ether = csharp_round_to_int(ether * cfg.road_cost_multiplier)
            food = csharp_round_to_int(food * cfg.road_cost_multiplier)
            wood = csharp_round_to_int(wood * cfg.road_cost_multiplier)
        return days, int(ether), int(food), int(wood)

    def calculate_recon_cost(self) -> tuple[int, int, int, int, int]:
        """Returns (days, elves_needed, ether, food, wood)."""
        cfg = self.game_data.expansion_cost
        return (
            cfg.recon_days,
            cfg.recon_elf_requirement,
            cfg.recon_ether_cost,
            cfg.recon_food_cost,
            cfg.recon_wood_cost,
        )

    def get_active_routes(self) -> list[ActiveRoute]:
        """Compute night combat routes trimmed to Settled/Outskirts/Surveying fog boundary.
        Matches C# EnemySpawner.RecalculateActiveRoutes by merging identical trimmed paths
        and summing their flow weights.
        """
        candidates: list[dict[str, Any]] = []
        for r in self._route_waypoints_with_chunks:
            pts: list[tuple[float, float, tuple[int, int]]] = r["taggedPath"]
            if len(pts) < 2:
                continue
            # Walk backward from Base (last index) while chunk is open for spawn
            # Note: Base chunk (0,0) is always open.
            start_idx = len(pts) - 1
            while start_idx > 0 and self.is_chunk_open_for_spawn(pts[start_idx - 1][2]):
                start_idx -= 1
            sub = pts[start_idx:]
            if len(sub) < 2:
                # Guarantee at least entry segment inside (0,0) if Base is adjacent
                sub = pts[-2:]
            key = tuple((round(x, 4), round(z, 4)) for x, z, _ in sub)
            merged = next((c for c in candidates if c["key"] == key), None)
            if merged is not None:
                merged["flow_weight"] += float(r["flowWeight"])
            else:
                candidates.append(
                    {
                        "key": key,
                        "name": r["name"],
                        "flow_weight": float(r["flowWeight"]),
                        "spawner_chunk": r["spawnerChunk"],
                        "is_loop_route": r["isLoopRoute"],
                        "sub": sub,
                    }
                )

        candidates.sort(key=lambda c: c["flow_weight"], reverse=True)
        active: list[ActiveRoute] = []
        for c in candidates:
            sub = c["sub"]
            segs: list[ActiveRouteSegment] = []
            tot_len = 0.0
            for i in range(len(sub) - 1):
                x0, z0, _c0 = sub[i]
                x1, z1, _c1 = sub[i + 1]
                seg_len = math.hypot(x1 - x0, z1 - z0)
                if seg_len <= 1e-6:
                    continue
                mid_chunk = self.find_chunk_of_world_pos(0.5 * (x0 + x1), 0.5 * (z0 + z1))
                ch_info = self.chunks.get(mid_chunk)
                is_surv = ch_info is not None and ch_info.state == ChunkState.Surveying
                segs.append(
                    ActiveRouteSegment(
                        x0=x0, z0=z0, x1=x1, z1=z1, length=seg_len, chunk=mid_chunk, is_surveying=is_surv
                    )
                )
                tot_len += seg_len
            if segs:
                active.append(
                    ActiveRoute(
                        name=c["name"],
                        flow_weight=c["flow_weight"],
                        spawner_chunk=c["spawner_chunk"],
                        is_loop_route=c["is_loop_route"],
                        waypoints=sub,
                        segments=segs,
                        total_length=tot_len,
                    )
                )
        return active

    def get_hex_neighbors(
        self, center: tuple[int, int], radius: int = 1, revealed_only: bool = True
    ) -> list[HexTile]:
        out: list[HexTile] = []
        cq, cr = center
        for dq in range(-radius, radius + 1):
            r1 = max(-radius, -dq - radius)
            r2 = min(radius, -dq + radius)
            for dr in range(r1, r2 + 1):
                if dq == 0 and dr == 0:
                    continue
                t = self.hexes.get((cq + dq, cr + dr))
                if t is None:
                    continue
                if revealed_only and not self.is_chunk_revealed(t.chunk):
                    continue
                out.append(t)
        return out

    def can_place_building(
        self,
        global_coord: tuple[int, int],
        b_def: BuildingDef,
        placed_buildings: dict[int, PlacedBuilding],
    ) -> tuple[bool, str]:
        tile = self.hexes.get(global_coord)
        if tile is None:
            return False, "invalid_hex"
        if tile.occupied_by is not None:
            return False, "occupied"
        if tile.is_road and not b_def.allow_on_road:
            return False, "on_road"
        if tile.feature in (int(HexFeatureType.Base), int(HexFeatureType.Beacon), int(HexFeatureType.Wall)):
            return False, "special_hex"

        ch = self.chunks.get(tile.chunk)
        if ch is None:
            return False, "no_chunk"

        if b_def.is_outpost:
            if ch.state != ChunkState.Outskirts or ch.has_outpost:
                return False, "outpost_requires_clean_outskirts"
        elif b_def.building_type == BuildingType.Defense:
            if ch.state not in (ChunkState.Outskirts, ChunkState.Settled):
                return False, "requires_outskirts_or_settled"
        else:
            if not self.is_chunk_effective_settled(tile.chunk):
                return False, "requires_settled_chunk"

        if b_def.allowed_terrain and tile.feature not in b_def.allowed_terrain:
            return False, "disallowed_terrain"

        er = b_def.elevation_req
        if er.mode == ElevationFilterMode.OnlyFlat and tile.elevation != 0:
            return False, "requires_flat"
        elif er.mode == ElevationFilterMode.OnlyHills and tile.elevation <= 0:
            return False, "requires_hill"
        elif er.mode == ElevationFilterMode.OnlyCavities and tile.elevation >= 0:
            return False, "requires_cavity"
        elif er.mode == ElevationFilterMode.CustomRange and not (er.min_elevation <= tile.elevation <= er.max_elevation):
            return False, "elevation_out_of_range"
        elif er.mode == ElevationFilterMode.SpecificValues and tile.elevation not in er.allowed_elevations:
            return False, "elevation_not_allowed"

        # RuneSmithTower must neighbor an existing RuneSmith AND respect RuneTowerLimit
        if b_def.asset_name == "RuneSmithTower":
            limit = int(self.meta_state.get_value(MetaEffectType.RuneTowerLimit))
            cur_towers = sum(1 for pb in placed_buildings.values() if pb.asset_name == "RuneSmithTower")
            if cur_towers >= limit:
                return False, "rune_tower_limit"
            nbs = self.get_hex_neighbors(global_coord, radius=1, revealed_only=True)
            has_forge = any(
                nb.occupied_by and nb.occupied_by in placed_buildings and placed_buildings[nb.occupied_by].asset_name == "RuneSmith"
                for nb in nbs
            )
            if not has_forge:
                return False, "needs_adjacent_runesmith"

        return True, "ok"

    def compute_building_production_at_eff1(
        self,
        global_coord: tuple[int, int],
        b_def: BuildingDef,
        placed_buildings: dict[int, PlacedBuilding],
        candidate_build_order: int | None = None,
        exclude_building_uid: int | None = None,
        beacon_next_day_prod_bonus: float = 0.0,
    ) -> dict[ResourceType, float]:
        """Exact C# BuildingProductionCalculator daily yield at crew efficiency 1.0."""
        tile = self.hexes.get(global_coord)
        if tile is None or not self.is_chunk_revealed(tile.chunk):
            return {}

        base_map: dict[ResourceType, float] = {}
        on_top_map: dict[ResourceType, float] = {}
        adj_map: dict[ResourceType, float] = {}

        for prod in b_def.production_per_cycle:
            base_map[prod.resource] = base_map.get(prod.resource, 0.0) + prod.amount

        # 1. Bonus rules (Forest/Sawmill, Meadow/Farm, Mountain/Mines)
        for rule in b_def.bonus_rules:
            if tile.feature == int(rule.required_feature):
                for y in rule.yields:
                    on_top_map[y.resource] = on_top_map.get(y.resource, 0.0) + y.on_top_bonus

            nbs = self.get_hex_neighbors(global_coord, radius=rule.search_radius, revealed_only=True)
            for nb in nbs:
                if nb.occupied_by is not None:
                    continue
                if nb.feature != int(rule.required_feature):
                    continue
                # Count earlier economic buildings using this neighbor resource hex
                existing_users = 0
                for pb in placed_buildings.values():
                    if exclude_building_uid is not None and pb.uid == exclude_building_uid:
                        continue
                    if candidate_build_order is not None and pb.build_order >= candidate_build_order:
                        continue
                    pb_rules = pb.building_def.bonus_rules
                    matched = False
                    for pr in pb_rules:
                        if int(pr.required_feature) == nb.feature:
                            if axial_distance(pb.global_coord, nb.global_coord) <= pr.search_radius:
                                matched = True
                                break
                    if matched:
                        existing_users += 1
                for y in rule.yields:
                    val = max(y.min_bonus, y.base_bonus_per_hex - y.penalty_per_user * existing_users)
                    adj_map[y.resource] = adj_map.get(y.resource, 0.0) + val

        # 2. Elevation bonus (Quarry)
        eb = b_def.elevation_bonus
        if eb.enabled:
            own_elev = max(0, tile.elevation)
            if own_elev > 0:
                on_top_map[eb.resource] = on_top_map.get(eb.resource, 0.0) + eb.on_top_per_level * own_elev
            nbs = self.get_hex_neighbors(global_coord, radius=eb.search_radius, revealed_only=True)
            for nb in nbs:
                if nb.occupied_by is not None or nb.elevation <= 0:
                    continue
                existing_users = 0
                for pb in placed_buildings.values():
                    if exclude_building_uid is not None and pb.uid == exclude_building_uid:
                        continue
                    if candidate_build_order is not None and pb.build_order >= candidate_build_order:
                        continue
                    if pb.building_def.elevation_bonus.enabled:
                        if axial_distance(pb.global_coord, nb.global_coord) <= pb.building_def.elevation_bonus.search_radius:
                            existing_users += 1
                eff_elev = max(0.0, float(nb.elevation) - eb.penalty_per_extra * existing_users)
                if eff_elev > 0:
                    adj_map[eb.resource] = adj_map.get(eb.resource, 0.0) + eb.adj_per_level * eff_elev

        meta_terrain_bonus = self.meta_state.get_building_value(
            MetaEffectType.BuildingTerrainBonusMultiplier, b_def.asset_name
        )
        meta_prod_bonus = self.meta_state.get_building_value(
            MetaEffectType.SpecificBuildingProductionBonus, b_def.asset_name
        )
        terrain_mult = 1.0 + meta_terrain_bonus
        global_mult = max(0.1, 1.0 + meta_prod_bonus + beacon_next_day_prod_bonus)

        result: dict[ResourceType, float] = {}
        all_res = set(base_map) | set(on_top_map) | set(adj_map)
        for r in all_res:
            raw = (base_map.get(r, 0.0) + on_top_map.get(r, 0.0) + adj_map.get(r, 0.0) * terrain_mult) * global_mult
            result[r] = raw
        return result


def compute_hex_opportunity_cost(
    world: MapWorld,
    global_coord: tuple[int, int],
    placed_buildings: dict[int, PlacedBuilding],
    resource_weights: dict[ResourceType, float],
    beacon_next_day_prod: float = 0.0,
) -> float:
    """Estimate daily production lost if global_coord becomes occupied (blocks neighbor harvests + consumes potential)."""
    tile = world.hexes[global_coord]
    # Temporarily compute existing neighbor buildings' yield delta
    lost_val = 0.0
    nbs = world.get_hex_neighbors(global_coord, radius=1, revealed_only=True)
    affected_uids: set[int] = set()
    for nb in nbs:
        if nb.occupied_by and nb.occupied_by in placed_buildings:
            affected_uids.add(nb.occupied_by)

    if affected_uids:
        before: dict[int, dict[ResourceType, float]] = {}
        for uid in affected_uids:
            pb = placed_buildings[uid]
            before[uid] = world.compute_building_production_at_eff1(
                pb.global_coord,
                pb.building_def,
                placed_buildings,
                candidate_build_order=pb.build_order,
                exclude_building_uid=uid,
                beacon_next_day_prod_bonus=beacon_next_day_prod,
            )
        prev_occ = tile.occupied_by
        tile.occupied_by = 999999
        try:
            for uid in affected_uids:
                pb = placed_buildings[uid]
                after = world.compute_building_production_at_eff1(
                    pb.global_coord,
                    pb.building_def,
                    placed_buildings,
                    candidate_build_order=pb.build_order,
                    exclude_building_uid=uid,
                    beacon_next_day_prod_bonus=beacon_next_day_prod,
                )
                for res, bval in before[uid].items():
                    aval = after.get(res, 0.0)
                    if bval > aval:
                        lost_val += (bval - aval) * resource_weights.get(res, 1.0)
        finally:
            tile.occupied_by = prev_occ

    # Also reserve precious raw hexes so houses/towers prefer barren hexes when possible
    if tile.feature == int(HexFeatureType.Mountain):
        lost_val += 40.0  # Rare Mountain slot reserved exclusively for Mines!
    elif tile.feature == int(HexFeatureType.Meadow):
        lost_val += 8.0 * resource_weights.get(ResourceType.Food, 1.0)
    elif tile.feature == int(HexFeatureType.Forest):
        lost_val += 6.0 * resource_weights.get(ResourceType.Wood, 1.0)
    elif tile.elevation > 0:
        lost_val += 1.5 * tile.elevation * resource_weights.get(ResourceType.Stone, 1.0)

    return lost_val


def choose_best_economic_hex(
    world: MapWorld,
    b_def: BuildingDef,
    placed_buildings: dict[int, PlacedBuilding],
    next_build_order: int,
    resource_weights: dict[ResourceType, float],
    beacon_next_day_prod: float = 0.0,
) -> tuple[tuple[int, int] | None, dict[ResourceType, float], float]:
    best_coord: tuple[int, int] | None = None
    best_score = -1e18
    best_prod: dict[ResourceType, float] = {}

    for gc, tile in world.hexes.items():
        if not world.is_chunk_effective_settled(tile.chunk):
            continue
        ok, _ = world.can_place_building(gc, b_def, placed_buildings)
        if not ok:
            continue
        prod = world.compute_building_production_at_eff1(
            gc,
            b_def,
            placed_buildings,
            candidate_build_order=next_build_order,
            beacon_next_day_prod_bonus=beacon_next_day_prod,
        )
        prod_value = sum(amt * resource_weights.get(res, 1.0) for res, amt in prod.items())
        # Subtract upkeep value
        upkeep_value = sum(u.amount * resource_weights.get(u.resource, 1.0) for u in b_def.upkeep_per_cycle)
        opp_cost = compute_hex_opportunity_cost(
            world, gc, placed_buildings, resource_weights, beacon_next_day_prod
        )
        # Note: opp_cost adds a self-feature penalty for Meadow/Forest/Hill which shouldn't penalize Farm/Sawmill/Quarry using its OWN tile!
        self_refund = 0.0
        if b_def.asset_name == "Mines" and tile.feature == int(HexFeatureType.Mountain):
            self_refund = 40.0
        elif b_def.asset_name == "Farm" and tile.feature == int(HexFeatureType.Meadow):
            self_refund = 8.0 * resource_weights.get(ResourceType.Food, 1.0)
        elif b_def.asset_name == "Sawmill" and tile.feature == int(HexFeatureType.Forest):
            self_refund = 6.0 * resource_weights.get(ResourceType.Wood, 1.0)
        elif b_def.asset_name == "Quarry" and tile.elevation > 0:
            self_refund = 1.5 * tile.elevation * resource_weights.get(ResourceType.Stone, 1.0)

        net_score = prod_value - upkeep_value - (opp_cost - self_refund) * 0.65
        # Reserve barren road-choke tiles (<=2.2w from active road) for combat towers!
        if tile.feature == int(HexFeatureType.NoneFeature):
            min_road_dist = 99.0
            for r in world.get_active_routes():
                for seg in r.segments:
                    d = _point_to_segment_dist(tile.world_x, tile.world_z, seg.x0, seg.z0, seg.x1, seg.z1)
                    if d < min_road_dist:
                        min_road_dist = d
            if min_road_dist <= 2.2:
                net_score -= 4.5
        # Tiny deterministic tie-breaker: prefer closer to Base (safer, easier to defend/reach) then coords
        net_score -= 0.01 * math.hypot(tile.world_x, tile.world_z)
        if net_score > best_score:
            best_score = net_score
            best_coord = gc
            best_prod = prod

    return best_coord, best_prod, best_score


def choose_best_housing_or_utility_hex(
    world: MapWorld,
    b_def: BuildingDef,
    placed_buildings: dict[int, PlacedBuilding],
    resource_weights: dict[ResourceType, float],
    want_free_neighbors: int = 0,
) -> tuple[int, int] | None:
    """Pick optimal Settled hex for House / ExpeditionCenter / RuneSmith / RuneSmithTower."""
    best_coord: tuple[int, int] | None = None
    best_score = -1e18

    active_routes = world.get_active_routes()
    for gc, tile in world.hexes.items():
        if not world.is_chunk_effective_settled(tile.chunk):
            continue
        ok, _ = world.can_place_building(gc, b_def, placed_buildings)
        if not ok:
            continue
        opp = compute_hex_opportunity_cost(world, gc, placed_buildings, resource_weights)
        # Preserve hexes close to active roads for combat towers!
        min_road_dist = 99.0
        for r in active_routes:
            for seg in r.segments:
                d = _point_to_segment_dist(tile.world_x, tile.world_z, seg.x0, seg.z0, seg.x1, seg.z1)
                if d < min_road_dist:
                    min_road_dist = d
        road_reserve_pen = 6.0 if min_road_dist <= 2.2 else (2.0 if min_road_dist <= 4.2 else 0.0)

        # For RuneSmith, ensure neighbor capacity for RuneSmithTowers (prefer barren neighbors)
        forge_bonus = 0.0
        if want_free_neighbors > 0:
            nbs = world.get_hex_neighbors(gc, radius=1, revealed_only=True)
            free_barren = sum(
                1
                for nb in nbs
                if nb.occupied_by is None
                and not nb.is_road
                and nb.feature in (int(HexFeatureType.NoneFeature), int(HexFeatureType.Sinkhole))
            )
            forge_bonus = min(want_free_neighbors, free_barren) * 5.0

        # Small penalty if blocking potential Sawmill/Farm/Quarry on currently empty neighbor resource clusters
        nb_resource_pen = 0.0
        for nb in world.get_hex_neighbors(gc, radius=1, revealed_only=True):
            if nb.occupied_by is None:
                if nb.feature == int(HexFeatureType.Forest):
                    nb_resource_pen += 0.8
                elif nb.feature == int(HexFeatureType.Meadow):
                    nb_resource_pen += 1.0
                elif nb.elevation > 0:
                    nb_resource_pen += 0.3 * nb.elevation

        score = forge_bonus - opp - road_reserve_pen - nb_resource_pen - 0.02 * math.hypot(tile.world_x, tile.world_z)
        if score > best_score:
            best_score = score
            best_coord = gc
    return best_coord


def _point_to_segment_dist(px: float, pz: float, x0: float, z0: float, x1: float, z1: float) -> float:
    vx = x1 - x0
    vz = z1 - z0
    l2 = vx * vx + vz * vz
    if l2 <= 1e-9:
        return math.hypot(px - x0, pz - z0)
    t = max(0.0, min(1.0, ((px - x0) * vx + (pz - z0) * vz) / l2))
    proj_x = x0 + t * vx
    proj_z = z0 + t * vz
    return math.hypot(px - proj_x, pz - proj_z)


def score_tower_hex(
    world: MapWorld,
    tile: HexTile,
    tower_def: TowerDef,
    active_routes: list[ActiveRoute],
    placed_buildings: dict[int, PlacedBuilding],
    resource_weights: dict[ResourceType, float],
    effective_world_range: float | None = None,
) -> tuple[float, float]:
    """Return (score, flow_weighted_covered_road_length) for placing tower_def at tile."""
    r_world = (
        effective_world_range
        if effective_world_range is not None
        else tower_def.base_range * HEX_TO_WORLD_RANGE_MULT
    )
    if r_world <= 0.1 or not active_routes:
        return -1e9, 0.0

    step = 0.5
    covered_weighted_len = 0.0
    collinear_bonus = 0.0
    zone_cluster_bonus = 0.0
    chokepoint_hits = 0.0

    # Sample active route segments inside range
    hit_points: list[tuple[float, float, float, float, float]] = []  # (x, z, dx, dz, weight)
    for route in active_routes:
        fw = max(0.05, route.flow_weight)
        for seg in route.segments:
            n_steps = max(1, int(math.ceil(seg.length / step)))
            ds = seg.length / n_steps
            udx = (seg.x1 - seg.x0) / seg.length
            udz = (seg.z1 - seg.z0) / seg.length
            for k in range(n_steps):
                t = (k + 0.5) / n_steps
                px = seg.x0 + (seg.x1 - seg.x0) * t
                pz = seg.z0 + (seg.z1 - seg.z0) * t
                dist = math.hypot(px - tile.world_x, pz - tile.world_z)
                if dist <= r_world:
                    # Enemies in Surveying move 1.5x faster => 0.67x dwell time per unit length
                    dwell_factor = (1.0 / 1.5) if seg.is_surveying else 1.0
                    # Proximity / range-margin boost: hexes closer to the road remain inside range under
                    # Hearth Lv1-2 (-20%/-10% range) and minimize projectile flight lag.
                    slack = max(0.0, 1.0 - (dist / max(0.1, r_world)))
                    w_pt = ds * fw * dwell_factor
                    covered_weighted_len += w_pt
                    hit_points.append((px, pz, udx, udz, w_pt))
                    if dist <= 1.8:
                        chokepoint_hits += ds * fw
                    elif tower_def.base_range < 7.0:
                        chokepoint_hits += w_pt * 0.35 * slack

    if covered_weighted_len <= 0.01:
        return -1e6, 0.0

    # Archetype-specific spatial bonuses
    if tower_def.bullet_kind == "Linear":
        # Royal Bombard fires piercing line shot (speed=8, lifeTime=1s -> ~8u travel):
        # alignment |dot(dir_to_enemy, enemy_road_dir)| maximizes multi-enemy pierce!
        align_sum = 0.0
        for px, pz, udx, udz, w in hit_points:
            vx = px - tile.world_x
            vz = pz - tile.world_z
            vd = math.hypot(vx, vz)
            if vd > 0.25:
                dot = abs((vx / vd) * udx + (vz / vd) * udz)
                align_sum += dot * w
        collinear_bonus = align_sum * 0.55
    elif tower_def.bullet_kind == "Ground":
        # Crucible drops 1.6x1.6 ground zone for 5s: rewards high weighted path density / bends near road
        zone_cluster_bonus = chokepoint_hits * 0.8 + min(covered_weighted_len, 10.0) * 0.25
    elif tower_def.base_range >= 7.0:
        # Shadow Spire (16u range): mildly prefer off-road tiles (`dist >= 2.2`) so close road slots stay open for short-range towers!
        min_road = min(
            (math.hypot(px - tile.world_x, pz - tile.world_z) for px, pz, *_ in hit_points),
            default=99.0,
        )
        if min_road >= 2.1:
            collinear_bonus += 2.2
    elif tower_def.base_range <= 1.5:
        # Soul Monolith (2.0u range): desperately needs tight road bend hugging multiple road tiles
        zone_cluster_bonus = covered_weighted_len * 0.6
    else:
        # Short/mid-range towers (Arbalest, Thunderbuss, Steam Repeater): reward tight range margin
        zone_cluster_bonus = chokepoint_hits * 0.55

    # Avoid crowding duplicate towers onto the exact same hex? Occupied check handles 1/hex;
    # mild spread tie-breaker prevents all long-range towers stacking in a single cluster vulnerable to MaddeningGaze (r=8u).
    opp_cost = compute_hex_opportunity_cost(world, tile.global_coord, placed_buildings, resource_weights)

    # Slight penalty for building in Outskirts vs Settled only if far, otherwise Outskirts chokes are GREAT for forward defense!
    raw_dps_norm = covered_weighted_len * 2.5 + collinear_bonus + zone_cluster_bonus
    # Don't waste a precious Meadow/Mountain/existing Sawmill Forest if a nearby Sinkhole/None hex covers 90% as well!
    score = raw_dps_norm - 0.12 * opp_cost
    return score, covered_weighted_len


def choose_best_tower_hex(
    world: MapWorld,
    tower_def: TowerDef,
    placed_buildings: dict[int, PlacedBuilding],
    resource_weights: dict[ResourceType, float],
    effective_world_range: float | None = None,
) -> tuple[tuple[int, int] | None, float, float]:
    active_routes = world.get_active_routes()
    best_coord: tuple[int, int] | None = None
    best_score = -1e18
    best_cov = 0.0

    for gc, tile in world.hexes.items():
        if not world.is_chunk_revealed(tile.chunk):
            continue
        ok, _ = world.can_place_building(gc, tower_def, placed_buildings)
        if not ok:
            continue
        sc, cov = score_tower_hex(
            world,
            tile,
            tower_def,
            active_routes,
            placed_buildings,
            resource_weights,
            effective_world_range=effective_world_range,
        )
        if cov <= 0.01:
            continue
        if sc > best_score:
            best_score = sc
            best_coord = gc
            best_cov = cov

    return best_coord, best_cov, best_score
