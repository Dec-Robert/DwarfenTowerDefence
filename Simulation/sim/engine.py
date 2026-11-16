from __future__ import annotations

import math
import random
from dataclasses import dataclass, field
from typing import Any, Callable

from .assets_loader import BuildingDef, GameData, HearthLevelSpec, TowerDef
from .combat import NightCombatResult, simulate_night_combat
from .enums import (
    BuildingType,
    ChunkState,
    MetaEffectType,
    MilestoneType,
    Race,
    ResourceType,
    RuneRarity,
    RuneStatType,
)
from .meta import MetaState
from .runes import (
    RuneInstance,
    collect_rune_modifiers,
    fuse_runes,
    transmute_runes,
)
from .spatial import MapWorld, PlacedBuilding, axial_distance


@dataclass
class ScoutingMissionState:
    mission_type: str  # "Recon" or "Survey"
    target_chunk: tuple[int, int]
    days_remaining: int
    elves_assigned: int
    equipment_used: int = 1


@dataclass
class RuneForgeState:
    building_uid: int
    infused_tower: str | None = None
    equipped_runes: list[RuneInstance] = field(default_factory=list)


@dataclass
class HopeTracker:
    unlocked_ids: set[str] = field(default_factory=set)
    total_hope: int = 0
    bosses_killed: int = 0
    expeditions_completed: int = 0
    enemies_killed: int = 0
    total_pop_peak: int = 0
    total_towers_peak: int = 0
    production_buildings_built: int = 0
    rune_crafting_done: int = 0
    legendary_or_cursed_obtained: int = 0
    nights_survived: int = 0
    victory: bool = False
    newly_unlocked_today: list[str] = field(default_factory=list)

    def evaluate(
        self,
        game_data: GameData,
        pop_by_race: dict[Race, int],
        active_tower_assets: list[str],
    ) -> list[str]:
        total_pop = sum(pop_by_race.values())
        self.total_pop_peak = max(self.total_pop_peak, total_pop)
        self.total_towers_peak = max(self.total_towers_peak, len(active_tower_assets))
        tower_counts: dict[str, int] = {}
        for t in active_tower_assets:
            tower_counts[t] = tower_counts.get(t, 0) + 1

        unlocked_now: list[str] = []
        for g in game_data.hope_milestones:
            if g.id in self.unlocked_ids:
                continue
            met = False
            mt = g.milestone_type
            if mt == MilestoneType.TotalPopulation:
                met = self.total_pop_peak >= g.target_value
            elif mt == MilestoneType.SpecificRacePopulation:
                met = pop_by_race.get(g.target_race, 0) >= g.target_value
            elif mt == MilestoneType.TotalTowers:
                met = self.total_towers_peak >= g.target_value
            elif mt == MilestoneType.SpecificTower:
                met = (g.target_tower is not None) and (tower_counts.get(g.target_tower, 0) >= g.target_value)
            elif mt == MilestoneType.NightsSurvived:
                met = self.nights_survived >= g.target_value
            elif mt == MilestoneType.BossesKilled:
                met = self.bosses_killed >= g.target_value
            elif mt == MilestoneType.EnemiesKilled:
                met = self.enemies_killed >= g.target_value
            elif mt == MilestoneType.ProductionBuildingsBuilt:
                met = self.production_buildings_built >= g.target_value
            elif mt == MilestoneType.LegendaryOrCursedRunesObtained:
                met = self.legendary_or_cursed_obtained >= g.target_value
            elif mt == MilestoneType.RuneCraftingDone:
                met = self.rune_crafting_done >= g.target_value
            elif mt == MilestoneType.Victory:
                met = self.victory
            elif mt == MilestoneType.ExpeditionsCompleted:
                met = self.expeditions_completed >= g.target_value

            if met:
                self.unlocked_ids.add(g.id)
                self.total_hope += g.hope_reward
                unlocked_now.append(g.id)
        self.newly_unlocked_today.extend(unlocked_now)
        return unlocked_now


@dataclass
class DayLog:
    day: int
    start_resources: dict[str, int]
    actions: list[str]
    production_gained: dict[str, int]
    upkeep_unpaid_buildings: list[str]
    housing_starved: list[str]
    hearth_level: int
    hearth_coal_cost: int
    night_result: dict[str, Any]
    end_resources: dict[str, int]
    end_city_hp: int
    population: dict[str, int]
    chunks_summary: dict[str, int]
    hope_total: int
    hope_unlocked_today: list[str]
    defeat_reason: str | None = None


class GameState:
    """Mutable simulation world + C#-faithful resource, housing, expansion, and rune mechanics."""

    def __init__(
        self,
        seed: int,
        game_data: GameData,
        meta_state: MetaState,
        world: MapWorld,
        wave_source: str = "predefined50",
    ) -> None:
        self.seed = seed
        self.game_data = game_data
        self.meta_state = meta_state
        self.world = world
        self.wave_source = wave_source

        self.day = 1
        self.sim_rng = random.Random(seed * 31337 + 17)
        self._next_building_uid = 1
        self._next_rune_uid = 1

        cb = game_data.city_base
        self.resources: dict[ResourceType, int] = {
            ResourceType.Ether: cb.start_ether + int(round(meta_state.get_value(MetaEffectType.StartingEther))),
            ResourceType.Wood: cb.start_wood + int(round(meta_state.get_value(MetaEffectType.StartingWood))),
            ResourceType.Stone: cb.start_stone + int(round(meta_state.get_value(MetaEffectType.StartingStone))),
            ResourceType.Iron: cb.start_iron + int(round(meta_state.get_value(MetaEffectType.StartingIron))),
            ResourceType.Coal: cb.start_coal + int(round(meta_state.get_value(MetaEffectType.StartingCoal))),
            ResourceType.Food: cb.start_food + int(round(meta_state.get_value(MetaEffectType.StartingFood))),
        }

        self.max_city_hp = cb.base_city_hp + int(round(meta_state.get_value(MetaEffectType.CityBaseHealth)))
        self.city_hp = self.max_city_hp

        self.placed_buildings: dict[int, PlacedBuilding] = {}
        self.rune_forges: dict[int, RuneForgeState] = {}
        self.rune_inventory: list[RuneInstance] = []
        self.active_missions: list[ScoutingMissionState] = []
        self.recon_intel_chunks: set[tuple[int, int]] = set()
        self.outpost_ready_to_transform: set[int] = set()

        self.beacon_first_outpost_day: int | None = None
        self.pending_next_day_prod_bonus: float = 0.0
        self.active_next_day_prod_bonus: float = 0.0
        self.chosen_hearth_level: int = 3

        self.hope = HopeTracker()
        self.actions_today: list[str] = []
        self.unpaid_buildings_today: list[str] = []
        self.housing_starved_today: list[str] = []
        self.prod_gained_today: dict[ResourceType, int] = {r: 0 for r in ResourceType if r != ResourceType.Hope}

        # Cumulative analytics for reporting
        self.total_damage_by_tower_type: dict[str, float] = {}
        self.total_production_by_resource: dict[ResourceType, int] = {r: 0 for r in ResourceType if r != ResourceType.Hope}
        self.total_ether_from_combat: int = 0
        self.total_runes_dropped: int = 0
        self.peak_population: int = 0
        self.day_logs: list[DayLog] = []
        self.victory: bool = False
        self.defeat: bool = False
        self.defeat_reason: str | None = None

        self._initialize_starting_buildings()

    def alloc_rune_uid(self) -> int:
        u = self._next_rune_uid
        self._next_rune_uid += 1
        return u

    def _initialize_starting_buildings(self) -> None:
        all_defs: dict[str, BuildingDef] = {
            **self.game_data.buildings,
            **self.game_data.houses,
            **self.game_data.towers,
        }
        # Spawn starting buildings embedded in chunk overrides (e.g. Ruined Human/Elven/Dwarven houses)
        for gc, tile in sorted(self.world.hexes.items()):
            sb_name = tile.starting_building
            if not sb_name:
                continue
            b_def = all_defs.get(sb_name)
            if b_def is None:
                # Case-insensitive or whitespace match fallback
                sb_low = sb_name.lower()
                for k, v in all_defs.items():
                    if k.lower() == sb_low or v.building_name.lower() == sb_low:
                        b_def = v
                        break
            if b_def is not None and tile.occupied_by is None:
                self.place_building(b_def, gc, pay_cost=False, is_initial=True)

        pop = self.total_population_by_race()
        self.peak_population = sum(pop.values())

    # ------------------------------------------------------------------
    # Resource & Population Queries
    # ------------------------------------------------------------------

    def can_afford(self, costs: list[Any]) -> bool:
        for c in costs:
            amt = int(math.floor(c.amount))
            if self.resources.get(c.resource, 0) < amt:
                return False
        return True

    def spend_resources(self, costs: list[Any]) -> bool:
        if not self.can_afford(costs):
            return False
        for c in costs:
            amt = int(math.floor(c.amount))
            self.resources[c.resource] = self.resources.get(c.resource, 0) - amt
        return True

    def can_afford_dict(self, costs: dict[ResourceType, int]) -> bool:
        return all(self.resources.get(r, 0) >= amt for r, amt in costs.items())

    def spend_dict(self, costs: dict[ResourceType, int]) -> bool:
        if not self.can_afford_dict(costs):
            return False
        for r, amt in costs.items():
            self.resources[r] = self.resources.get(r, 0) - amt
        return True

    def effective_max_residents(self, pb: PlacedBuilding) -> int:
        b = pb.building_def
        if not b.is_housing:
            return 0
        bonus = int(round(self.meta_state.get_value(MetaEffectType.HousingMaxResidents)))
        if b.housing_race == Race.Humans:
            bonus += int(round(self.meta_state.get_value(MetaEffectType.HousingMaxResidents_Humans)))
        elif b.housing_race == Race.Elves:
            bonus += int(round(self.meta_state.get_value(MetaEffectType.HousingMaxResidents_Elves)))
        elif b.housing_race == Race.Dwarves:
            bonus += int(round(self.meta_state.get_value(MetaEffectType.HousingMaxResidents_Dwarves)))
        return max(0, b.max_residents + bonus)

    def effective_max_workers(self, pb: PlacedBuilding) -> int:
        b = pb.building_def
        if b.building_type == BuildingType.Defense:
            return b.base_max_workers + int(round(self.meta_state.get_value(MetaEffectType.BonusTowerCrewSlots)))
        if b.building_type == BuildingType.Economic:
            return b.base_max_workers + int(round(self.meta_state.get_value(MetaEffectType.BonusProductionWorkers)))
        if b.asset_name == "RuneSmith":
            adj_towers = sum(
                1
                for nb in self.world.get_hex_neighbors(pb.global_coord, radius=1, revealed_only=True)
                if nb.occupied_by and nb.occupied_by in self.placed_buildings
                and self.placed_buildings[nb.occupied_by].asset_name == "RuneSmithTower"
            )
            return min(5, 1 + adj_towers)
        return b.base_max_workers

    def total_population_by_race(self) -> dict[Race, int]:
        counts = {Race.Humans: 0, Race.Elves: 0, Race.Dwarves: 0}
        for pb in self.placed_buildings.values():
            if pb.building_def.is_housing:
                counts[pb.building_def.housing_race] += pb.residents
        return counts

    def max_population_capacity_by_race(self) -> dict[Race, int]:
        caps = {Race.Humans: 0, Race.Elves: 0, Race.Dwarves: 0}
        for pb in self.placed_buildings.values():
            if pb.building_def.is_housing:
                caps[pb.building_def.housing_race] += self.effective_max_residents(pb)
        return caps

    def busy_expedition_elves(self) -> int:
        return sum(m.elves_assigned for m in self.active_missions)

    def assigned_workers_by_race(self) -> dict[Race, int]:
        counts = {Race.Humans: 0, Race.Elves: 0, Race.Dwarves: 0}
        for pb in self.placed_buildings.values():
            for r_int, cnt in pb.assigned_workers.items():
                r = Race(r_int)
                counts[r] += cnt
        counts[Race.Elves] += self.busy_expedition_elves()
        return counts

    def free_workers_by_race(self) -> dict[Race, int]:
        tot = self.total_population_by_race()
        used = self.assigned_workers_by_race()
        return {r: max(0, tot[r] - used[r]) for r in (Race.Humans, Race.Elves, Race.Dwarves)}

    def total_equipment(self) -> int:
        return sum(
            pb.building_def.expedition_equipment_provided
            for pb in self.placed_buildings.values()
            if pb.asset_name == "ExpeditionCenter" and pb.is_active
        )

    def used_equipment(self) -> int:
        return sum(m.equipment_used for m in self.active_missions)

    def available_equipment(self) -> int:
        return max(0, self.total_equipment() - self.used_equipment())

    def discovered_or_surveying_extra_chunks(self) -> int:
        open_extra = type(self.world).discovered_extra_chunks_count(self.world)
        active_surveys = sum(1 for m in self.active_missions if m.mission_type == "Survey")
        return open_extra + active_surveys

    # ------------------------------------------------------------------
    # Building Placement, Demolish, Outposts
    # ------------------------------------------------------------------

    def place_building(
        self,
        b_def: BuildingDef,
        global_coord: tuple[int, int],
        pay_cost: bool = True,
        is_initial: bool = False,
    ) -> PlacedBuilding | None:
        ok, _ = self.world.can_place_building(global_coord, b_def, self.placed_buildings)
        if not ok:
            return None
        if pay_cost and not self.spend_resources(b_def.construction_cost):
            return None

        uid = self._next_building_uid
        self._next_building_uid += 1
        tile = self.world.hexes[global_coord]
        tile.occupied_by = uid

        init_res = 0
        if b_def.is_housing:
            meta_start = int(round(self.meta_state.get_value(MetaEffectType.HousingStartPopulation)))
            # Temporarily compute effective max
            temp_pb = PlacedBuilding(
                uid=uid,
                asset_name=b_def.asset_name,
                building_def=b_def,
                global_coord=global_coord,
                chunk=tile.chunk,
                world_x=tile.world_x,
                world_z=tile.world_z,
                build_order=uid,
                built_day=self.day,
            )
            eff_max = self.effective_max_residents(temp_pb)
            init_res = min(eff_max, b_def.initial_residents + meta_start)

        pb = PlacedBuilding(
            uid=uid,
            asset_name=b_def.asset_name,
            building_def=b_def,
            global_coord=global_coord,
            chunk=tile.chunk,
            world_x=tile.world_x,
            world_z=tile.world_z,
            build_order=uid,
            built_day=self.day,
            residents=init_res,
            days_until_transform=b_def.outpost_days_until_transform,
        )
        self.placed_buildings[uid] = pb

        if b_def.is_outpost:
            ch = self.world.chunks[tile.chunk]
            ch.has_outpost = True
            if self.beacon_first_outpost_day is None:
                self.beacon_first_outpost_day = self.day

        if b_def.asset_name == "RuneSmith":
            self.rune_forges[uid] = RuneForgeState(building_uid=uid)

        if not is_initial:
            if b_def.building_type == BuildingType.Economic:
                self.hope.production_buildings_built += 1
            self.actions_today.append(f"Build {b_def.asset_name}@{tile.chunk}({tile.local[0]},{tile.local[1]})")

        return pb

    def demolish_building(self, uid: int, has_been_used: bool = True) -> bool:
        pb = self.placed_buildings.get(uid)
        if pb is None:
            return False
        if pb.building_def.is_housing and pb.residents > 0:
            return False
        mult = 0.5 if has_been_used else 1.0
        for c in pb.building_def.construction_cost:
            refund = int(math.floor(c.amount * mult))
            if refund > 0:
                self.resources[c.resource] = self.resources.get(c.resource, 0) + refund
        tile = self.world.hexes.get(pb.global_coord)
        if tile is not None and tile.occupied_by == uid:
            tile.occupied_by = None
        if uid in self.rune_forges:
            rf = self.rune_forges.pop(uid)
            self.rune_inventory.extend(rf.equipped_runes)
        self.outpost_ready_to_transform.discard(uid)
        self.placed_buildings.pop(uid, None)
        self.actions_today.append(f"Demolish {pb.asset_name}#{uid}")
        return True

    def transform_outpost(self, uid: int, target_house_asset: str) -> PlacedBuilding | None:
        if uid not in self.outpost_ready_to_transform:
            return None
        pb = self.placed_buildings.get(uid)
        if pb is None or not pb.building_def.is_outpost:
            return None
        h_def = self.game_data.houses.get(target_house_asset)
        if h_def is None:
            return None

        # Check tile settled
        ch = self.world.chunks[pb.chunk]
        if ch.state != ChunkState.Settled:
            return None

        # Remove Outpost without cost refund and spawn House in place
        tile = self.world.hexes[pb.global_coord]
        tile.occupied_by = None
        self.placed_buildings.pop(uid, None)
        self.outpost_ready_to_transform.discard(uid)
        ch.outpost_transformed = True

        new_uid = self._next_building_uid
        self._next_building_uid += 1
        tile.occupied_by = new_uid
        meta_start = int(round(self.meta_state.get_value(MetaEffectType.HousingStartPopulation)))
        temp_pb = PlacedBuilding(
            uid=new_uid,
            asset_name=h_def.asset_name,
            building_def=h_def,
            global_coord=pb.global_coord,
            chunk=pb.chunk,
            world_x=pb.world_x,
            world_z=pb.world_z,
            build_order=new_uid,
            built_day=self.day,
        )
        eff_max = self.effective_max_residents(temp_pb)
        temp_pb.residents = min(eff_max, h_def.initial_residents + meta_start)
        self.placed_buildings[new_uid] = temp_pb
        self.actions_today.append(f"TransformOutpost#{uid}->{h_def.asset_name}@{pb.chunk}")
        return temp_pb

    # ------------------------------------------------------------------
    # Expeditions (Recon & Survey)
    # ------------------------------------------------------------------

    def can_start_recon(self, chunk_coord: tuple[int, int]) -> tuple[bool, str]:
        ch = self.world.chunks.get(chunk_coord)
        if ch is None or ch.state != ChunkState.Borderlands:
            return False, "not_borderlands"
        if ch.recon_completed:
            return False, "already_reconned"
        if any(m.target_chunk == chunk_coord for m in self.active_missions):
            return False, "active_mission"
        if self.available_equipment() <= 0:
            return False, "no_equipment"
        days, elves_req, eth, food, wood = self.world.calculate_recon_cost()
        if self.free_workers_by_race()[Race.Elves] < elves_req:
            return False, "no_elves"
        costs = {ResourceType.Ether: eth, ResourceType.Food: food, ResourceType.Wood: wood}
        if not self.can_afford_dict(costs):
            return False, "no_resources"
        return True, "ok"

    def start_recon(self, chunk_coord: tuple[int, int]) -> bool:
        ok, _ = self.can_start_recon(chunk_coord)
        if not ok:
            return False
        days, elves_req, eth, food, wood = self.world.calculate_recon_cost()
        self.spend_dict({ResourceType.Ether: eth, ResourceType.Food: food, ResourceType.Wood: wood})
        self.active_missions.append(
            ScoutingMissionState(
                mission_type="Recon",
                target_chunk=chunk_coord,
                days_remaining=days,
                elves_assigned=elves_req,
            )
        )
        self.actions_today.append(f"StartRecon@{chunk_coord}(d={days},e={elves_req})")
        return True

    def calculate_survey_cost_live(
        self, chunk_coord: tuple[int, int], elves: int = 1
    ) -> tuple[int, int, int, int]:
        # Match MapExpansionManager using discovered + active surveys
        orig_fn = self.world.discovered_extra_chunks_count
        self.world.discovered_extra_chunks_count = self.discovered_or_surveying_extra_chunks  # type: ignore[method-assign]
        try:
            return self.world.calculate_survey_cost(chunk_coord, assigned_elves=elves)
        finally:
            self.world.discovered_extra_chunks_count = orig_fn  # type: ignore[method-assign]

    def can_start_survey(self, chunk_coord: tuple[int, int], elves: int = 1) -> tuple[bool, str]:
        ch = self.world.chunks.get(chunk_coord)
        if ch is None or ch.state != ChunkState.Borderlands:
            return False, "not_borderlands"
        if any(m.target_chunk == chunk_coord for m in self.active_missions):
            return False, "active_mission"
        if self.available_equipment() <= 0:
            return False, "no_equipment"
        min_e = self.game_data.expansion_cost.min_elves_for_survey
        if elves < min_e:
            return False, "too_few_elves"
        if self.free_workers_by_race()[Race.Elves] < elves:
            return False, "no_elves"
        days, eth, food, wood = self.calculate_survey_cost_live(chunk_coord, elves=elves)
        costs = {ResourceType.Ether: eth, ResourceType.Food: food, ResourceType.Wood: wood}
        if not self.can_afford_dict(costs):
            return False, "no_resources"
        return True, "ok"

    def start_survey(self, chunk_coord: tuple[int, int], elves: int = 1) -> bool:
        ok, _ = self.can_start_survey(chunk_coord, elves=elves)
        if not ok:
            return False
        days, eth, food, wood = self.calculate_survey_cost_live(chunk_coord, elves=elves)
        self.spend_dict({ResourceType.Ether: eth, ResourceType.Food: food, ResourceType.Wood: wood})
        self.world.chunks[chunk_coord].state = ChunkState.Surveying
        self.active_missions.append(
            ScoutingMissionState(
                mission_type="Survey",
                target_chunk=chunk_coord,
                days_remaining=days,
                elves_assigned=elves,
            )
        )
        self.actions_today.append(f"StartSurvey@{chunk_coord}(d={days},e={elves})")
        return True

    # ------------------------------------------------------------------
    # Worker Assignment & Economy Math
    # ------------------------------------------------------------------

    def assign_worker(self, uid: int, race: Race) -> bool:
        pb = self.placed_buildings.get(uid)
        if pb is None or not pb.is_active:
            return False
        if pb.building_def.requires_specific_citizen and pb.building_def.required_citizen_race != race:
            return False
        if pb.asset_name == "RuneSmith" and race != Race.Dwarves:
            return False
        cur = sum(pb.assigned_workers.values())
        if cur >= self.effective_max_workers(pb):
            return False
        if self.free_workers_by_race()[race] <= 0:
            return False
        pb.assigned_workers[int(race)] = pb.assigned_workers.get(int(race), 0) + 1
        return True

    def clear_non_expedition_workers(self) -> None:
        for pb in self.placed_buildings.values():
            pb.assigned_workers.clear()
            pb.last_upkeep_paid = True

    def compute_economic_efficiency(self, pb: PlacedBuilding, upkeep_paid: bool = True) -> float:
        b = pb.building_def
        total_w = sum(pb.assigned_workers.values())
        base_eff = b.base_efficiency(total_w)
        if total_w == 0:
            base_eff += self.meta_state.get_value(MetaEffectType.BuildingPassiveEfficiency)
        else:
            base_eff += total_w * self.meta_state.get_value(MetaEffectType.BuildingWorkerEfficiencyBonus)
            humans = pb.assigned_workers.get(int(Race.Humans), 0)
            if humans >= 4 and self.meta_state.has_effect(MetaEffectType.ProductionSynergy_Human):
                base_eff += 0.20
            elves = pb.assigned_workers.get(int(Race.Elves), 0)
            if elves > 0 and b.asset_name in ("Sawmill", "Farm") and self.meta_state.has_effect(MetaEffectType.ProductionSynergy_Elf):
                base_eff += 0.05 * elves
        base_eff = max(0.0, base_eff)
        if base_eff <= 0.0:
            return 0.0
        if not upkeep_paid:
            return max(0.1, base_eff - 0.5)
        return base_eff

    def compute_hearth_coal_cost(self, level_index: int) -> int:
        if level_index <= 1:
            return 0
        base_n = 5
        if self.beacon_first_outpost_day is None:
            n = base_n
        else:
            days_passed = max(0, self.day - self.beacon_first_outpost_day)
            n = base_n + days_passed * 1
        return (level_index - 1) * n

    # ------------------------------------------------------------------
    # Rune Crafting & Forge Management
    # ------------------------------------------------------------------

    def fuse_runes(self, r1_uid: int, r2_uid: int) -> RuneInstance | None:
        if not self.meta_state.is_unlocked(MetaEffectType.UnlockRuneFusion):
            return None
        r1 = next((r for r in self.rune_inventory if r.uid == r1_uid), None)
        r2 = next((r for r in self.rune_inventory if r.uid == r2_uid), None)
        if r1 is None or r2 is None:
            return None
        out = fuse_runes(r1, r2, self.alloc_rune_uid(), self.sim_rng, self.game_data, self.meta_state)
        if out is None:
            return None
        self.rune_inventory = [r for r in self.rune_inventory if r.uid not in (r1_uid, r2_uid)]
        self.rune_inventory.append(out)
        self.hope.rune_crafting_done += 1
        if out.rarity in (RuneRarity.Legendary, RuneRarity.Cursed):
            self.hope.legendary_or_cursed_obtained += 1
        self.actions_today.append(f"Fuse->{out.short_label()}")
        return out

    def transmute_runes(self, r1_uid: int, r2_uid: int, r3_uid: int, hearth_mult: float = 1.0) -> RuneInstance | None:
        if not self.meta_state.is_unlocked(MetaEffectType.UnlockRuneTransmutation):
            return None
        r1 = next((r for r in self.rune_inventory if r.uid == r1_uid), None)
        r2 = next((r for r in self.rune_inventory if r.uid == r2_uid), None)
        r3 = next((r for r in self.rune_inventory if r.uid == r3_uid), None)
        if r1 is None or r2 is None or r3 is None:
            return None
        out = transmute_runes(r1, r2, r3, self.alloc_rune_uid(), self.sim_rng, self.game_data, self.meta_state, hearth_mult)
        if out is None:
            return None
        self.rune_inventory = [r for r in self.rune_inventory if r.uid not in (r1_uid, r2_uid, r3_uid)]
        self.rune_inventory.append(out)
        self.hope.rune_crafting_done += 1
        if out.rarity in (RuneRarity.Legendary, RuneRarity.Cursed):
            self.hope.legendary_or_cursed_obtained += 1
        self.actions_today.append(f"Transmute->{out.short_label()}")
        return out

    def get_active_rune_modifiers_by_tower(self) -> dict[str, dict[RuneStatType, tuple[float, float]]]:
        merged: dict[str, dict[RuneStatType, list[float]]] = {}
        for uid, rf in self.rune_forges.items():
            pb = self.placed_buildings.get(uid)
            if pb is None or not pb.is_active or not rf.infused_tower:
                continue
            dwarves = pb.assigned_workers.get(int(Race.Dwarves), 0)
            if dwarves <= 0:
                continue
            max_slots = self.effective_max_workers(pb)
            active_slots = min(max_slots, dwarves)
            equipped = rf.equipped_runes[:active_slots]
            if not equipped:
                continue
            deltas = collect_rune_modifiers(equipped)
            t_dict = merged.setdefault(rf.infused_tower, {})
            for st, (fv, pv) in deltas.items():
                if st not in t_dict:
                    t_dict[st] = [0.0, 0.0]
                t_dict[st][0] += fv
                t_dict[st][1] += pv
        return {t: {st: (v[0], v[1]) for st, v in d.items()} for t, d in merged.items()}

    # ------------------------------------------------------------------
    # Phase Execution: Morning, Dusk, Beacon, Night
    # ------------------------------------------------------------------

    def execute_morning_phase(self) -> None:
        self.actions_today = []
        self.unpaid_buildings_today = []
        self.housing_starved_today = []
        self.prod_gained_today = {r: 0 for r in ResourceType if r != ResourceType.Hope}
        self.hope.newly_unlocked_today = []

        if self.day == 1:
            return

        # 1. Beacon Morning reset + activate next-day production bonus
        self.active_next_day_prod_bonus = self.pending_next_day_prod_bonus
        self.pending_next_day_prod_bonus = 0.0

        # 2. Reset building workers for new day
        self.clear_non_expedition_workers()

        # 3. Advance expeditions (Recon & Survey)
        completed: list[ScoutingMissionState] = []
        still_active: list[ScoutingMissionState] = []
        for m in self.active_missions:
            m.days_remaining -= 1
            if m.days_remaining <= 0:
                completed.append(m)
            else:
                still_active.append(m)
        self.active_missions = still_active
        for m in completed:
            ch = self.world.chunks[m.target_chunk]
            if m.mission_type == "Recon":
                ch.recon_completed = True
                self.recon_intel_chunks.add(m.target_chunk)
                self.actions_today.append(f"ReconDone@{m.target_chunk}")
            else:
                ch.state = ChunkState.Outskirts
                self.world.propagate_borderlands()
                self.actions_today.append(f"SurveyDone@{m.target_chunk}->Outskirts")
            self.hope.expeditions_completed += 1

        # 4. Tick Outposts
        for uid, pb in list(self.placed_buildings.items()):
            if not pb.building_def.is_outpost:
                continue
            ch = self.world.chunks[pb.chunk]
            if ch.state != ChunkState.Outskirts:
                continue
            pb.days_until_transform -= 1
            if pb.days_until_transform <= 0:
                ch.state = ChunkState.Settled
                self.world.propagate_borderlands()
                self.outpost_ready_to_transform.add(uid)
                self.actions_today.append(f"OutpostSettled@{pb.chunk}")

        # 5. Housing Morning Economy (BuildingRegistry.ExecuteMorningHousingEconomy order)
        housing_list = [pb for pb in self.placed_buildings.values() if pb.is_active and pb.building_def.is_housing]

        def _days_rem_key(h: PlacedBuilding) -> int:
            b = h.building_def
            eff_max = self.effective_max_residents(h)
            if h.residents >= eff_max:
                return 9999
            req = b.growth_cycles[h.residents] if h.residents < len(b.growth_cycles) else (b.growth_cycles[-1] if b.growth_cycles else 2)
            return max(0, req - h.growth_progress)

        housing_list.sort(key=_days_rem_key)

        # Survival food upkeep
        for h in housing_list:
            if h.residents <= 0:
                h.last_survival_paid = True
                continue
            surv_costs: dict[ResourceType, int] = {}
            for c in h.building_def.upkeep_per_resident:
                amt = int(math.floor(c.amount * h.residents))
                if amt > 0:
                    surv_costs[c.resource] = surv_costs.get(c.resource, 0) + amt
            paid = self.spend_dict(surv_costs)
            h.last_survival_paid = paid
            if not paid:
                h.growth_progress = max(0, h.growth_progress - 1)
                self.housing_starved_today.append(f"{h.asset_name}#{h.uid}")

        # Demographic growth
        for h in housing_list:
            if not h.last_survival_paid:
                continue
            b = h.building_def
            eff_max = self.effective_max_residents(h)
            if h.residents >= eff_max:
                continue
            if not self.spend_resources(b.growth_surplus_cost):
                continue
            h.growth_progress += 1
            req = b.growth_cycles[h.residents] if h.residents < len(b.growth_cycles) else (b.growth_cycles[-1] if b.growth_cycles else 2)
            if h.growth_progress >= max(1, req):
                h.residents += 1
                h.growth_progress = 0
                self.actions_today.append(f"Pop+1 {b.housing_race.name}@{h.chunk}")

    def execute_dusk_and_night_phase(self, hearth_level: int) -> NightCombatResult:
        # 1. Dusk Economic Upkeep -> Production (BuildingRegistry.ExecuteDuskEconomy)
        econ_buildings = [
            pb
            for pb in self.placed_buildings.values()
            if pb.is_active
            and pb.building_def.building_type not in (BuildingType.Defense, BuildingType.Housing)
            and not pb.building_def.is_housing
        ]
        # Upkeep first for all economic & utility buildings
        eff_map: dict[int, float] = {}
        for pb in econ_buildings:
            base_eff = self.compute_economic_efficiency(pb, upkeep_paid=True)
            if pb.building_def.upkeep_per_cycle and pb.building_def.building_type == BuildingType.Economic:
                if base_eff <= 0.0:
                    pb.last_upkeep_paid = True
                    eff_map[pb.uid] = 0.0
                    continue
                paid = self.spend_resources(pb.building_def.upkeep_per_cycle)
                pb.last_upkeep_paid = paid
                if not paid:
                    self.unpaid_buildings_today.append(f"{pb.asset_name}#{pb.uid}")
                eff_map[pb.uid] = self.compute_economic_efficiency(pb, upkeep_paid=paid)
            elif pb.building_def.upkeep_per_cycle and pb.asset_name == "RuneSmith":
                dwarves = pb.assigned_workers.get(int(Race.Dwarves), 0)
                if dwarves > 0:
                    paid = self.spend_resources(pb.building_def.upkeep_per_cycle)
                    pb.last_upkeep_paid = paid
                    if not paid:
                        self.unpaid_buildings_today.append(f"{pb.asset_name}#{pb.uid}")
                eff_map[pb.uid] = 1.0 if dwarves > 0 else 0.0
            else:
                eff_map[pb.uid] = base_eff

        # Production second
        for pb in econ_buildings:
            if pb.building_def.building_type != BuildingType.Economic:
                continue
            eff = eff_map.get(pb.uid, 0.0)
            if eff <= 0.0:
                continue
            eff1_map = self.world.compute_building_production_at_eff1(
                pb.global_coord,
                pb.building_def,
                self.placed_buildings,
                candidate_build_order=pb.build_order,
                exclude_building_uid=pb.uid,
                beacon_next_day_prod_bonus=self.active_next_day_prod_bonus,
            )
            for res, amt_at_1 in eff1_map.items():
                raw = amt_at_1 * eff
                pb.production_buffer[res] = pb.production_buffer.get(res, 0.0) + raw
                whole = int(math.floor(pb.production_buffer[res] + 1e-6))
                if whole > 0:
                    pb.production_buffer[res] = max(0.0, pb.production_buffer[res] - whole)
                    self.resources[res] = self.resources.get(res, 0) + whole
                    self.prod_gained_today[res] = self.prod_gained_today.get(res, 0) + whole
                    self.total_production_by_resource[res] = self.total_production_by_resource.get(res, 0) + whole

        # 2. Beacon Ignition (Dusk -> Night)
        chosen_lvl = max(1, min(5, hearth_level))
        coal_cost = self.compute_hearth_coal_cost(chosen_lvl)
        while chosen_lvl > 1 and self.resources.get(ResourceType.Coal, 0) < coal_cost:
            chosen_lvl -= 1
            coal_cost = self.compute_hearth_coal_cost(chosen_lvl)
        if coal_cost > 0:
            self.resources[ResourceType.Coal] -= coal_cost
        self.chosen_hearth_level = chosen_lvl
        hearth = self.game_data.hearth_levels[chosen_lvl]
        self.pending_next_day_prod_bonus = hearth.next_day_production_bonus

        # 3. Night Defense Upkeep (BuildingRegistry.ExecuteNightDefenseUpkeep)
        towers = [
            pb
            for pb in self.placed_buildings.values()
            if pb.is_active and pb.building_def.building_type == BuildingType.Defense
        ]
        # Order towers closest to Base gate / highest crew first for upkeep allocation
        towers.sort(key=lambda t: (-sum(t.assigned_workers.values()), t.build_order))
        for tw in towers:
            crew = sum(tw.assigned_workers.values())
            if crew <= 0:
                tw.last_upkeep_paid = False
                continue
            if tw.building_def.upkeep_per_cycle:
                paid = self.spend_resources(tw.building_def.upkeep_per_cycle)
                tw.last_upkeep_paid = paid
                if not paid:
                    self.unpaid_buildings_today.append(f"{tw.asset_name}#{tw.uid}")
            else:
                tw.last_upkeep_paid = True

        # 4. Night Combat
        rune_mods = self.get_active_rune_modifiers_by_tower()
        night_rng = random.Random(self.seed * 1009 + self.day * 7919)
        res = simulate_night_combat(
            day=self.day,
            world=self.world,
            towers=towers,
            hearth=hearth,
            rune_mods_by_tower=rune_mods,
            meta_state=self.meta_state,
            game_data=self.game_data,
            night_rng=night_rng,
            next_rune_uid_fn=self.alloc_rune_uid,
            wave_source=self.wave_source,
        )

        # Apply Night Combat outcomes
        self.resources[ResourceType.Ether] += res.ether_gained
        self.total_ether_from_combat += res.ether_gained
        for r in res.dropped_runes:
            self.rune_inventory.append(r)
            self.total_runes_dropped += 1
            if r.rarity in (RuneRarity.Legendary, RuneRarity.Cursed):
                self.hope.legendary_or_cursed_obtained += 1

        for ttype, dmg in res.damage_by_tower_type.items():
            self.total_damage_by_tower_type[ttype] = self.total_damage_by_tower_type.get(ttype, 0.0) + dmg

        self.hope.enemies_killed += res.kills_normal + res.kills_elite + res.kills_boss
        self.hope.bosses_killed += res.kills_boss

        if res.boss_leaked:
            self.city_hp = 0
            self.defeat = True
            self.defeat_reason = f"Boss leaked on Night {self.day}"
        else:
            self.city_hp = max(0, self.city_hp - res.city_hp_lost)
            if self.city_hp <= 0:
                self.defeat = True
                self.defeat_reason = f"CityHP reached 0 on Night {self.day} (leaks: N={res.leaks_normal}, E={res.leaks_elite})"

        if not self.defeat:
            self.hope.nights_survived = self.day
            if self.day >= 50:
                self.victory = True
                self.hope.victory = True

        pop = self.total_population_by_race()
        self.peak_population = max(self.peak_population, sum(pop.values()))
        tower_names = [t.asset_name for t in towers]
        unlocked_today = self.hope.evaluate(self.game_data, pop, tower_names)

        # Active chunk counts by state
        c_sum: dict[str, int] = {}
        for ch in self.world.chunks.values():
            c_sum[ch.state.name] = c_sum.get(ch.state.name, 0) + 1

        self.day_logs.append(
            DayLog(
                day=self.day,
                start_resources={},  # Filled by caller if desired or before morning
                actions=list(self.actions_today),
                production_gained={r.name: v for r, v in self.prod_gained_today.items() if v > 0},
                upkeep_unpaid_buildings=list(self.unpaid_buildings_today),
                housing_starved=list(self.housing_starved_today),
                hearth_level=chosen_lvl,
                hearth_coal_cost=coal_cost,
                night_result={
                    "total_enemies": res.total_enemies,
                    "kills": res.kills_normal + res.kills_elite + res.kills_boss,
                    "kills_elite": res.kills_elite,
                    "kills_boss": res.kills_boss,
                    "leaks_normal": res.leaks_normal,
                    "leaks_elite": res.leaks_elite,
                    "boss_leaked": res.boss_leaked,
                    "city_hp_lost": res.city_hp_lost,
                    "ether_gained": res.ether_gained,
                    "runes_dropped": [r.short_label() for r in res.dropped_runes],
                    "duration_sec": res.duration_sec,
                    "damage_by_type": {k: round(v, 1) for k, v in res.damage_by_tower_type.items()},
                },
                end_resources={r.name: self.resources[r] for r in (ResourceType.Ether, ResourceType.Wood, ResourceType.Stone, ResourceType.Iron, ResourceType.Coal, ResourceType.Food)},
                end_city_hp=self.city_hp,
                population={r.name: pop[r] for r in (Race.Humans, Race.Elves, Race.Dwarves)},
                chunks_summary=c_sum,
                hope_total=self.hope.total_hope,
                hope_unlocked_today=unlocked_today,
                defeat_reason=self.defeat_reason,
            )
        )
        return res


def run_simulation(
    seed: int,
    game_data: GameData,
    meta_state: MetaState,
    planner_fn: Callable[[GameState], int],
    max_days: int = 50,
    wave_source: str = "predefined50",
) -> GameState:
    """Execute full Day 1..max_days simulation using blind macro/spatial planner_fn."""
    world = MapWorld.generate(seed, game_data, meta_state)
    state = GameState(seed=seed, game_data=game_data, meta_state=meta_state, world=world, wave_source=wave_source)

    for d in range(1, max_days + 1):
        state.day = d
        start_res = {
            r.name: state.resources[r]
            for r in (
                ResourceType.Ether,
                ResourceType.Wood,
                ResourceType.Stone,
                ResourceType.Iron,
                ResourceType.Coal,
                ResourceType.Food,
            )
        }
        state.execute_morning_phase()
        hearth_lvl = planner_fn(state)
        state.execute_dusk_and_night_phase(hearth_lvl)
        state.day_logs[-1].start_resources = start_res

        if state.defeat:
            break
        if state.day >= max_days:
            if d >= 50:
                state.victory = True
            break

    return state
