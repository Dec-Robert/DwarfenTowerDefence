from __future__ import annotations

import math

from .assets_loader import BuildingDef, TowerDef
from .combat import compute_tower_stats
from .engine import GameState
from .enums import (
    BuildingType,
    ChunkState,
    DamageType,
    HexFeatureType,
    MetaEffectType,
    Race,
    ResourceType,
    RuneRarity,
)
from .runes import RuneInstance, can_fuse_pair, score_rune_for_tower
from .spatial import (
    PlacedBuilding,
    choose_best_economic_hex,
    choose_best_housing_or_utility_hex,
    choose_best_tower_hex,
)


def _cost_dict(costs: list) -> dict[ResourceType, int]:
    out: dict[ResourceType, int] = {}
    for c in costs:
        amt = int(math.ceil(c.amount))
        if amt > 0:
            out[c.resource] = out.get(c.resource, 0) + amt
    return out


class BlindMacroPlanner:
    """Blind optimal-placement & macro/micro AI for DwarfenTowerDefence.

    STRICT BLINDNESS:
    - Inspects ONLY current state (day, resources, population, placed buildings,
      Settled/Outskirts/Recon-revealed tiles, active routes, past DayLogs, unlocked MetaState).
    - NEVER inspects future unseen wave definitions or un-reconned Wilderness/Borderlands tiles.
    """

    def __init__(self, prefer_rune_infusion: bool = True) -> None:
        self.prefer_rune_infusion = prefer_rune_infusion

    # ------------------------------------------------------------------
    # Main Per-Day Entry Point
    # ------------------------------------------------------------------

    def __call__(self, state: GameState) -> int:
        # 1. Transform matured Outposts immediately (free House in Settled chunk!)
        self._handle_outpost_transforms(state)

        # 2. Rune crafting (Fusion / Transmutation) & Forge Optimization
        self._handle_runes_and_forges(state)

        # 3. Dynamic Resource Weights & Budgeted Economy / Defense / Housing / Expansion Loop
        for _ in range(18):
            if self._perform_best_day_action(state):
                continue
            break

        # 4. Optimal Expeditions (Survey & Recon) using remaining safe Elves/Equipment/Resources
        self._handle_expeditions(state)

        # 5. Re-optimize Rune Sockets/Infusions after building any new towers/forges today
        self._handle_runes_and_forges(state)

        # 6. Assign workers first at tentative Hearth, then pick optimal Hearth given manned crew
        tentative_h = self._choose_hearth_level(state)
        self._allocate_workers(state, tentative_h)
        hearth_lvl = self._choose_hearth_level(state)
        if hearth_lvl != tentative_h:
            self._allocate_workers(state, hearth_lvl)
        return hearth_lvl

    # ------------------------------------------------------------------
    # Blind Threat & Economy Estimation
    # ------------------------------------------------------------------

    def _recent_leak_pressure(self, state: GameState) -> float:
        """Compute danger pressure from recent past nights (strictly historical)."""
        if not state.day_logs:
            return 0.0
        last = state.day_logs[-1]
        nr = last.night_result
        if nr is None:
            return 0.0
        leaks = nr.get("leaks_normal", 0) + nr.get("leaks_elite", 0) * 3
        hp_ratio = 1.0 - (state.city_hp / max(1, state.max_city_hp))
        pressure = 0.0
        if leaks > 0:
            pressure += min(1.5, 0.35 + 0.12 * leaks)
        if len(state.day_logs) >= 2:
            prev_nr = state.day_logs[-2].night_result or {}
            prev_leaks = prev_nr.get("leaks_normal", 0) + prev_nr.get("leaks_elite", 0) * 3
            if prev_leaks > 0:
                pressure += 0.25
        pressure += hp_ratio * 0.6
        return pressure

    def _active_route_summary(self, state: GameState) -> tuple[float, set[tuple[int, int]]]:
        """Return (flow-weighted average route length, entry Borderlands chunks on visible routes)."""
        routes = state.world.get_active_routes()
        if not routes:
            return 2.0, set()
        total_w = sum(max(0.01, r.flow_weight) for r in routes)
        weighted_len = sum(r.total_length * max(0.01, r.flow_weight) for r in routes) / total_w
        entry_chunks: set[tuple[int, int]] = set()
        for r in routes:
            if r.waypoints:
                wc = r.waypoints[0][2]
                if wc in state.world.chunks:
                    entry_chunks.add(wc)
            for seg in r.segments:
                if seg.chunk in state.world.chunks:
                    entry_chunks.add(seg.chunk)
        return weighted_len, entry_chunks

    def _estimate_tower_combat_value(
        self,
        pb: PlacedBuilding,
        state: GameState,
        hearth_lvl: int,
        assume_one_crew: bool = False,
    ) -> float:
        """Estimate blind effective DPS contribution of a placed tower (coverage + AoE/pierce/AP)."""
        td = pb.building_def
        if not isinstance(td, TowerDef):
            return 0.0
        hearth = state.game_data.hearth_levels[hearth_lvl]
        rune_mods = state.get_active_rune_modifiers_by_tower()

        restore_workers = False
        if assume_one_crew and sum(pb.assigned_workers.values()) == 0:
            pb.assigned_workers[int(Race.Humans)] = 1
            restore_workers = True
        try:
            st = compute_tower_stats(pb, hearth, rune_mods, state.meta_state, is_feared=False)
        finally:
            if restore_workers:
                pb.assigned_workers.clear()

        single_dps = st.damage * st.fire_rate * (1.0 + st.crit_chance * max(0.0, st.crit_mult - 1.0))

        if td.bullet_kind == "Ground":
            ticks = td.zone_duration / max(0.05, td.zone_tick_interval)
            aoe_mult = min(14.0, ticks * 0.24)
        elif td.bullet_kind == "Fragmented":
            aoe_mult = 1.0 + 0.35 * td.extra_shards
        elif td.bullet_kind == "Linear":
            aoe_mult = 1.0
            single_dps *= 0.45
        elif td.bullet_kind == "HitScan":
            aoe_mult = 1.15
        else:
            aoe_mult = 1.0

        pen_bonus = 1.0 + 0.35 * (st.magic_pen if st.damage_type == DamageType.Magic else st.armor_pen)
        magic_flex = 1.12 if st.damage_type == DamageType.Magic else 1.0
        range_factor = 1.0 + min(0.6, st.world_range / 18.0)
        return single_dps * aoe_mult * pen_bonus * magic_flex * range_factor

    def _total_defense_score(self, state: GameState, hearth_lvl: int, assume_staffed: bool = True) -> float:
        total = 0.0
        total_pop = sum(state.total_population_by_race().values())
        budget = max(1, total_pop - 1)
        towers = [pb for pb in state.placed_buildings.values() if pb.building_def.building_type == BuildingType.Defense]
        for idx, pb in enumerate(towers):
            staff = assume_staffed and (idx < budget)
            total += self._estimate_tower_combat_value(pb, state, hearth_lvl, assume_one_crew=staff)
        return total

    def _target_defense_score(self, state: GameState) -> float:
        """Blind Day-curve target DPS score adjusted for corridor length & recent leaks."""
        day = state.day
        base_target = 95.0 * (1.095 ** (day - 1))
        if day >= 18:
            base_target *= 1.15
        if day >= 25:
            base_target *= 1.15
        if day >= 38:
            base_target *= 1.15
        avg_len, _ = self._active_route_summary(state)
        corridor_mult = max(0.85, min(1.85, 5.2 / max(1.6, avg_len)))
        leak_mult = 1.0 + self._recent_leak_pressure(state)
        return base_target * corridor_mult * leak_mult

    def _estimate_daily_economy(
        self,
        state: GameState,
        assume_staffed: bool = True,
    ) -> dict[str, dict[ResourceType, float]]:
        """Estimate expected daily production, mandatory upkeep, and food burn.

        Note: During pre-Dusk planning, clear_non_expedition_workers() has cleared
        building workers! When assume_staffed=True, we estimate each Economic building
        with 1 expected worker (capped by available non-tower workforce) instead of
        seeing 0.15 passive meta efficiency and falsely panic-building multiple Farms.
        """
        prod_rates: dict[ResourceType, float] = {r: 0.0 for r in ResourceType if r != ResourceType.Hope}
        econ_upkeep: dict[ResourceType, float] = {r: 0.0 for r in ResourceType if r != ResourceType.Hope}
        def_upkeep: dict[ResourceType, float] = {r: 0.0 for r in ResourceType if r != ResourceType.Hope}
        morning_food = 0.0

        econ_pbs: list[PlacedBuilding] = []
        tower_cnt = 0
        for pb in state.placed_buildings.values():
            b = pb.building_def
            if b.is_housing and pb.residents > 0:
                for u in b.upkeep_per_resident:
                    if u.resource == ResourceType.Food:
                        morning_food += u.amount * pb.residents
                for u in b.upkeep_per_cycle:
                    if u.resource == ResourceType.Food:
                        morning_food += u.amount
            elif b.building_type == BuildingType.Economic:
                econ_pbs.append(pb)
                for u in b.upkeep_per_cycle:
                    econ_upkeep[u.resource] = econ_upkeep.get(u.resource, 0.0) + u.amount
            elif b.building_type == BuildingType.Defense:
                tower_cnt += 1
                for u in b.upkeep_per_cycle:
                    def_upkeep[u.resource] = def_upkeep.get(u.resource, 0.0) + u.amount
            elif b.is_outpost or b.asset_name in ("RuneSmith", "RuneSmithTower"):
                target = econ_upkeep if b.is_outpost else def_upkeep
                for u in b.upkeep_per_cycle:
                    target[u.resource] = target.get(u.resource, 0.0) + u.amount

        free_total = sum(state.free_workers_by_race().values())
        assigned_total = sum(sum(pb.assigned_workers.values()) for pb in state.placed_buildings.values())
        # Workpool available to staff economy after minimum 1 crew per vital tower (up to half of pop)
        total_avail = free_total + assigned_total
        econ_worker_budget = max(0, total_avail - min(tower_cnt, max(1, total_avail // 2)))

        # Sort priority for hypothetical staffing if assume_staffed=True matching real scarcity priorities
        econ_raw_pairs: list[tuple[float, PlacedBuilding, dict[ResourceType, float]]] = []
        for pb in econ_pbs:
            b = pb.building_def
            raw = state.world.compute_building_production_at_eff1(
                pb.global_coord,
                b,
                state.placed_buildings,
                candidate_build_order=pb.build_order,
                exclude_building_uid=pb.uid,
                beacon_next_day_prod_bonus=state.active_next_day_prod_bonus,
            )
            prio = 0.0
            for res, val in raw.items():
                stk = state.resources.get(res, 0)
                if res == ResourceType.Coal:
                    w_r = 9.0 if stk < 25 else 3.5
                elif res == ResourceType.Stone:
                    w_r = 7.0 if stk < 25 else 3.0
                elif res == ResourceType.Wood:
                    w_r = 6.0 if stk < 45 else 2.2
                elif res == ResourceType.Iron:
                    w_r = 6.0 if stk < 25 else 2.5
                elif res == ResourceType.Food:
                    w_r = 5.0 if stk < max(30.0, morning_food * 3.0) else (0.3 if stk > morning_food * 4.5 + 30 else 1.8)
                else:
                    w_r = 2.0
                prio += val * w_r
            econ_raw_pairs.append((prio, pb, raw))

        if assume_staffed:
            econ_raw_pairs.sort(key=lambda x: x[0], reverse=True)

        for _, pb, raw in econ_raw_pairs:
            b = pb.building_def
            cur_w = sum(pb.assigned_workers.values())
            if cur_w > 0:
                eff = state.compute_economic_efficiency(pb, upkeep_paid=True)
            elif assume_staffed and econ_worker_budget > 0:
                econ_worker_budget -= 1
                worker_bonus = state.meta_state.get_value(MetaEffectType.BuildingWorkerEfficiencyBonus)
                eff = max(0.0, b.base_efficiency(1) + worker_bonus)
            else:
                eff = state.compute_economic_efficiency(pb, upkeep_paid=True)

            for res, val in raw.items():
                prod_rates[res] = prod_rates.get(res, 0.0) + val * eff

        net = {
            r: prod_rates.get(r, 0.0) - econ_upkeep.get(r, 0.0) - def_upkeep.get(r, 0.0)
            for r in prod_rates
        }
        net[ResourceType.Food] -= morning_food
        return {
            "prod": prod_rates,
            "econ_upkeep": econ_upkeep,
            "def_upkeep": def_upkeep,
            "morning_food": {ResourceType.Food: morning_food},
            "net": net,
        }

    def _mandatory_reserve(self, state: GameState, planned_hearth: int = 2) -> dict[ResourceType, int]:
        """Resources held in reserve so active Dusk/Night upkeep & Hearth Coal remain payable.

        IMPORTANT: Dusk runs ALL econ_upkeep BEFORE Economic production! So 100% of
        econ_upkeep must remain in stock before Dusk starts, whereas Night defense upkeep
        and Beacon Hearth Coal run AFTER Dusk Economic production.
        """
        econ = self._estimate_daily_economy(state, assume_staffed=True)
        res: dict[ResourceType, int] = {}
        for r in (ResourceType.Wood, ResourceType.Stone, ResourceType.Coal, ResourceType.Iron, ResourceType.Ether):
            dusk_upk = econ["econ_upkeep"].get(r, 0.0)
            net_dusk_prod = max(0.0, econ["prod"].get(r, 0.0) - dusk_upk)
            night_outflow = econ["def_upkeep"].get(r, 0.0) + (
                state.compute_hearth_coal_cost(planned_hearth) if r == ResourceType.Coal else 0.0
            )
            night_shortfall = max(0.0, night_outflow - net_dusk_prod * 0.85)
            need = dusk_upk + night_shortfall
            if need > 0:
                res[r] = int(math.ceil(need))
        mf = int(math.ceil(max(0.0, econ["morning_food"][ResourceType.Food] - econ["prod"].get(ResourceType.Food, 0.0) * 0.85)))
        if mf > 0:
            res[ResourceType.Food] = mf
        return res

    def _compute_resource_weights(self, state: GameState) -> dict[ResourceType, float]:
        econ = self._estimate_daily_economy(state, assume_staffed=True)
        net = econ["net"]
        stock = state.resources

        w: dict[ResourceType, float] = {
            ResourceType.Food: 1.5,
            ResourceType.Wood: 1.8,
            ResourceType.Stone: 1.8,
            ResourceType.Coal: 1.8,
            ResourceType.Iron: 3.0,
            ResourceType.Ether: 2.5,
        }

        morning_burn = max(6.0, econ["morning_food"][ResourceType.Food])
        food_runway = (stock.get(ResourceType.Food, 0) + max(0.0, econ["prod"].get(ResourceType.Food, 0.0))) / morning_burn
        if food_runway < 2.2 or net[ResourceType.Food] < 2.0:
            w[ResourceType.Food] = 5.5 if food_runway < 1.6 else 3.8
        elif stock.get(ResourceType.Food, 0) > morning_burn * 6.0 + 60:
            w[ResourceType.Food] = 0.15
        elif stock.get(ResourceType.Food, 0) > morning_burn * 3.5 + 25:
            w[ResourceType.Food] = 0.75

        wood_upk_floor = econ["econ_upkeep"].get(ResourceType.Wood, 0.0) * 1.2 + 12.0
        if net[ResourceType.Wood] < 2.0 or stock.get(ResourceType.Wood, 0) < wood_upk_floor:
            w[ResourceType.Wood] = 5.5 if net[ResourceType.Wood] < 2.0 else 3.8
        elif net[ResourceType.Wood] < 8.0:
            w[ResourceType.Wood] = 2.8
        elif stock.get(ResourceType.Wood, 0) > wood_upk_floor + 55:
            w[ResourceType.Wood] = 0.7
        elif stock.get(ResourceType.Wood, 0) > wood_upk_floor + 28 and net[ResourceType.Wood] >= 12.0:
            w[ResourceType.Wood] = 1.15

        if net[ResourceType.Stone] < 0.0 or stock.get(ResourceType.Stone, 0) < 16:
            w[ResourceType.Stone] = 8.5
        elif net[ResourceType.Stone] < 6.0 or stock.get(ResourceType.Stone, 0) < 35:
            w[ResourceType.Stone] = 6.2 if (stock.get(ResourceType.Stone, 0) < 28 or self._count_buildings(state, "Retort") == 0) else 3.8
        elif stock.get(ResourceType.Stone, 0) > 120:
            w[ResourceType.Stone] = 0.6

        if stock.get(ResourceType.Coal, 0) < 18 or net[ResourceType.Coal] < 2.0:
            w[ResourceType.Coal] = 9.5
        elif stock.get(ResourceType.Coal, 0) < 32 or net[ResourceType.Coal] < 5.0:
            w[ResourceType.Coal] = 5.0
        elif stock.get(ResourceType.Coal, 0) > 70 and net[ResourceType.Coal] >= 8.0:
            w[ResourceType.Coal] = 0.15

        if stock.get(ResourceType.Iron, 0) < 20 or net[ResourceType.Iron] <= 0.0:
            w[ResourceType.Iron] = 6.0 if state.day >= 5 else 3.2
        elif stock.get(ResourceType.Iron, 0) > 60:
            w[ResourceType.Iron] = 1.0

        return w

    def _can_afford_with_reserve(
        self,
        state: GameState,
        costs: list,
        reserve: dict[ResourceType, int],
        allow_dip_reserve: bool = False,
    ) -> bool:
        cdict = _cost_dict(costs)
        for r, amt in cdict.items():
            req = amt if allow_dip_reserve else (amt + reserve.get(r, 0))
            if state.resources.get(r, 0) < req:
                return False
        return True

    # ------------------------------------------------------------------
    # Settled Resource Discovery Helper (Blind)
    # ------------------------------------------------------------------

    def _has_free_settled_feature(self, state: GameState, feature: HexFeatureType) -> bool:
        fid = int(feature)
        for tile in state.world.hexes.values():
            if not state.world.is_chunk_effective_settled(tile.chunk):
                continue
            if tile.occupied_by is None and not tile.is_road and tile.feature == fid:
                return True
        return False

    def _count_buildings(self, state: GameState, asset_name: str) -> int:
        return sum(1 for pb in state.placed_buildings.values() if pb.asset_name == asset_name)

    def _count_towers(self, state: GameState) -> int:
        return sum(1 for pb in state.placed_buildings.values() if pb.building_def.building_type == BuildingType.Defense)

    def _free_settled_slots(self, state: GameState) -> int:
        cnt = 0
        for tile in state.world.hexes.values():
            if not state.world.is_chunk_effective_settled(tile.chunk):
                continue
            if tile.occupied_by is None and not tile.is_road and tile.feature != int(HexFeatureType.Mountain):
                cnt += 1
        return cnt

    # ------------------------------------------------------------------
    # Outpost Transform
    # ------------------------------------------------------------------

    def _handle_outpost_transforms(self, state: GameState) -> None:
        if not state.outpost_ready_to_transform:
            return
        pop = state.total_population_by_race()
        for uid in list(state.outpost_ready_to_transform):
            if pop[Race.Dwarves] < 3 or (
                state.meta_state.has_effect(MetaEffectType.TowerCrewSynergy_Dwarf)
                and pop[Race.Dwarves] < pop[Race.Humans]
            ):
                target = "Dwarven house"
            elif pop[Race.Elves] < 3:
                target = "Elf house"
            else:
                target = "Human house"
            state.transform_outpost(uid, target)
            pop = state.total_population_by_race()

    # ------------------------------------------------------------------
    # Main Constructive Decision Step
    # ------------------------------------------------------------------

    def _perform_best_day_action(self, state: GameState) -> bool:
        w = self._compute_resource_weights(state)
        econ = self._estimate_daily_economy(state, assume_staffed=True)
        net = econ["net"]
        planned_hearth = 3 if state.day <= 2 else (2 if (state.day <= 8 or state.resources.get(ResourceType.Coal, 0) < 25) else 3)
        reserve = self._mandatory_reserve(state, planned_hearth=planned_hearth)

        pop = state.total_population_by_race()
        total_pop = sum(pop.values())
        max_pop_cap = sum(state.max_population_capacity_by_race().values())

        econ_slots = sum(
            state.effective_max_workers(pb)
            for pb in state.placed_buildings.values()
            if pb.building_def.building_type == BuildingType.Economic
        )
        def_slots = sum(
            min(2, state.effective_max_workers(pb))
            for pb in state.placed_buildings.values()
            if pb.building_def.building_type == BuildingType.Defense
        )

        cur_towers = self._count_towers(state)
        cur_def = self._total_defense_score(state, planned_hearth, assume_staffed=True)
        target_def = self._target_defense_score(state)
        leaking_recently = self._recent_leak_pressure(state) >= 0.30

        morning_burn = max(6.0, econ["morning_food"][ResourceType.Food])
        expected_food_tomorrow = state.resources.get(ResourceType.Food, 0) + econ["prod"].get(ResourceType.Food, 0.0)
        food_runway = expected_food_tomorrow / morning_burn

        farm_def = state.game_data.buildings.get("Farm")
        saw_def = state.game_data.buildings.get("Sawmill")
        quarry_def = state.game_data.buildings.get("Quarry")
        retort_def = state.game_data.buildings.get("Retort")
        mines_def = state.game_data.buildings.get("Mines")

        n_saw = self._count_buildings(state, "Sawmill")
        n_farm = self._count_buildings(state, "Farm")
        n_quarry = self._count_buildings(state, "Quarry")
        n_retort = self._count_buildings(state, "Retort")

        # ---- 0. Day 1 Disciplined Opening Spine: Sawmill -> 1st Tower -> 1st Farm -> 1st Quarry ----
        # Ensures Sawmill builds before Farm (so Wood upkeep works in build_order!),
        # guarantees 1st Tower on Day 1, and reserves 5 Stone so 1st Quarry is NEVER locked out!
        if n_saw == 0 and saw_def:
            if self._try_build_economic(state, saw_def, w, reserve, allow_dip=True):
                return True

        if cur_towers == 0:
            def_res = dict(reserve)
            if state.day == 1 and n_farm == 0 and state.resources.get(ResourceType.Wood, 0) >= 34 and state.resources.get(ResourceType.Stone, 0) >= 10:
                def_res[ResourceType.Wood] = def_res.get(ResourceType.Wood, 0) + 10
                def_res[ResourceType.Stone] = def_res.get(ResourceType.Stone, 0) + 5
            if self._try_build_best_tower(state, w, def_res, allow_dip=True, hearth_lvl=planned_hearth):
                return True

        if n_farm == 0 and farm_def:
            if self._try_build_economic(state, farm_def, w, reserve, allow_dip=True):
                return True

        # Stone reservation: until we own a Quarry, NEVER spend last 5 Stone on extra Sawmills/Houses
        non_quarry_res = dict(reserve)
        if n_quarry == 0:
            non_quarry_res[ResourceType.Stone] = non_quarry_res.get(ResourceType.Stone, 0) + 5

        # ---- 1. Food Survival ----
        if state.day >= 2 and food_runway < 2.2 and (food_runway < 1.6 or net[ResourceType.Food] < 2.0) and farm_def:
            if self._try_build_economic(state, farm_def, w, non_quarry_res, allow_dip=False):
                return True

        # ---- 2. Critical Progression Milestones (Quarry -> ExpeditionCenter -> Coal/Mines -> RuneSmith) ----
        if n_quarry == 0 and quarry_def:
            if self._try_build_economic(state, quarry_def, w, reserve, allow_dip=(state.day >= 2)):
                return True

        exp_def = state.game_data.buildings.get("ExpeditionCenter")
        n_exp = self._count_buildings(state, "ExpeditionCenter")
        if exp_def and n_exp == 0 and state.day >= 2 and cur_towers >= 1 and food_runway >= 2.0:
            if self._try_build_utility(state, exp_def, w, non_quarry_res):
                return True

        # Mines on Settled Mountain right away when Wood >= 45 & Stone >= 35
        n_mines = self._count_buildings(state, "Mines")
        has_settled_mtn = self._has_free_settled_feature(state, HexFeatureType.Mountain)
        if mines_def and has_settled_mtn and (n_mines == 0 or net[ResourceType.Iron] < 1.5):
            if (cur_def >= target_def * 0.90 or state.day >= 8) and (net[ResourceType.Wood] >= 8.0 or state.resources.get(ResourceType.Wood, 0) >= 48) and not leaking_recently:
                if self._try_build_economic(state, mines_def, w, non_quarry_res, allow_dip=False):
                    return True

        est_coal_burn = state.compute_hearth_coal_cost(planned_hearth) + econ["econ_upkeep"].get(ResourceType.Coal, 0.0) + econ["def_upkeep"].get(ResourceType.Coal, 0.0)
        coal_critical = state.resources.get(ResourceType.Coal, 0) < 30 or (n_retort == 0 and econ["prod"].get(ResourceType.Coal, 0.0) < 5.0 and state.day >= 3)
        if coal_critical and retort_def and (net[ResourceType.Coal] < max(5.0, est_coal_burn * 0.8)):
            if net[ResourceType.Wood] >= 8.0 or state.resources.get(ResourceType.Wood, 0) >= 25:
                if self._try_build_economic(state, retort_def, w, non_quarry_res, allow_dip=False):
                    return True

        forge_def = state.game_data.buildings.get("RuneSmith")
        n_forge = self._count_buildings(state, "RuneSmith")
        if forge_def and n_forge == 0 and state.day >= 7 and len(state.rune_inventory) >= 2 and pop[Race.Dwarves] >= 2:
            if self._try_build_utility(state, forge_def, w, reserve, want_free_neighbors=2):
                return True

        # Determine strategic goal to prevent optional builds from nibbling away Stone/Iron/Wood!
        goal_res = dict(non_quarry_res)
        need_first_retort_stone = (
            retort_def is not None
            and n_retort == 0
            and n_quarry >= 1
            and econ["prod"].get(ResourceType.Coal, 0.0) < 5.0
            and state.resources.get(ResourceType.Coal, 0) < 24
        )
        # Always protect critical Stone milestones even after early leaks so optional builds can't starve Retort/ExpCenter!
        if exp_def and n_exp == 0 and state.day >= 2 and n_quarry >= 1:
            goal_res[ResourceType.Stone] = max(goal_res.get(ResourceType.Stone, 0), non_quarry_res.get(ResourceType.Stone, 0) + 15)
            if not leaking_recently:
                goal_res[ResourceType.Wood] = max(goal_res.get(ResourceType.Wood, 0), non_quarry_res.get(ResourceType.Wood, 0) + 20)
        elif need_first_retort_stone:
            goal_res[ResourceType.Stone] = max(goal_res.get(ResourceType.Stone, 0), non_quarry_res.get(ResourceType.Stone, 0) + 20)
        elif not leaking_recently:
            if mines_def and has_settled_mtn and n_mines == 0 and n_quarry >= 1 and state.day >= 6 and cur_def >= target_def * 0.90:
                goal_res[ResourceType.Stone] = max(goal_res.get(ResourceType.Stone, 0), non_quarry_res.get(ResourceType.Stone, 0) + 35)
                goal_res[ResourceType.Wood] = max(goal_res.get(ResourceType.Wood, 0), non_quarry_res.get(ResourceType.Wood, 0) + 35)
            elif forge_def and n_forge == 0 and state.day >= 8 and len(state.rune_inventory) >= 2 and n_mines >= 1 and state.resources.get(ResourceType.Iron, 0) >= 10:
                goal_res[ResourceType.Stone] = max(goal_res.get(ResourceType.Stone, 0), non_quarry_res.get(ResourceType.Stone, 0) + 55)
                goal_res[ResourceType.Iron] = max(goal_res.get(ResourceType.Iron, 0), non_quarry_res.get(ResourceType.Iron, 0) + 20)

        # ---- 2b. Workforce Bottleneck Relief (avoid spamming unmanned towers while Sawmill/Quarry/Runes starve!) ----
        n_econ_curr = sum(1 for pb in state.placed_buildings.values() if pb.building_def.building_type == BuildingType.Economic)
        free_settled = self._free_settled_slots(state)
        if (
            (cur_towers >= max(5, total_pop - 1) or total_pop < n_econ_curr + min(cur_towers, 4) - 1)
            and state.day >= 6
            and food_runway >= 2.3
            and (net[ResourceType.Food] >= 2.0 or food_runway >= 3.2)
            and free_settled >= 1
        ):
            if self._try_build_best_house(state, pop, w, goal_res):
                return True

        # ---- 2c. Territory / Settled Space Relief via 1 Outpost when settled slots run tight ----
        outpost_def = state.game_data.buildings.get("Outpost")
        active_outposts = sum(1 for pb in state.placed_buildings.values() if pb.building_def.is_outpost)
        if (
            outpost_def
            and active_outposts == 0
            and state.day >= 9
            and cur_towers >= 3
            and (free_settled <= 5 or (not has_settled_mtn and not leaking_recently and cur_def >= target_def * 0.90))
            and food_runway >= 2.5
            and net[ResourceType.Wood] >= 4.0
            and net[ResourceType.Stone] >= 4.0
            and (net[ResourceType.Coal] >= 2.0 or state.resources.get(ResourceType.Coal, 0) >= 25)
        ):
            if self._try_build_best_outpost(state, outpost_def, w, goal_res):
                return True

        # Stone income relief: build 2nd Quarry ONLY when Stone net actually falls negative/tight in midgame!
        if (
            quarry_def
            and n_quarry < 2
            and cur_towers >= 5
            and state.day >= 12
            and net[ResourceType.Stone] < 2.0
            and net[ResourceType.Wood] >= 4.0
        ):
            if self._try_build_economic(state, quarry_def, w, non_quarry_res, allow_dip=False):
                return True

        # ---- 3. Urgent Defense Scaling ----
        need_urgent_def = (cur_def < target_def * (1.20 if leaking_recently else 0.95))
        if need_urgent_def:
            tower_res = dict(non_quarry_res) if (leaking_recently or cur_def < target_def * 0.85) else dict(goal_res)
            if need_first_retort_stone:
                # Never let Stone-heavy towers eat the 20 Stone needed for 1st Retort (0-Stone towers still build!)
                tower_res[ResourceType.Stone] = max(tower_res.get(ResourceType.Stone, 0), non_quarry_res.get(ResourceType.Stone, 0) + 20)
            stone_floor = int(tower_res.get(ResourceType.Stone, 0))
            coal_floor = max(
                int(reserve.get(ResourceType.Coal, 0)),
                state.compute_hearth_coal_cost(min(planned_hearth, 3)) if n_retort >= 1 else 0,
            )
            if self._try_build_best_tower(state, w, tower_res, allow_dip=False, hearth_lvl=planned_hearth, hard_stone_floor=stone_floor, hard_coal_floor=coal_floor):
                return True
            if leaking_recently and self._try_build_best_tower(state, w, tower_res, allow_dip=True, hearth_lvl=planned_hearth, hard_stone_floor=stone_floor, hard_coal_floor=coal_floor):
                return True

        # Protect Defense / Goal Stone+Wood savings before spending on optional Houses/Farms (Sawmill<=2 exempt on Wood)
        econ_res = dict(goal_res)
        if need_urgent_def and n_exp >= 1:
            # Reserve 20 Wood for cheapest reliable Arbalest tower so optional builds don't nibble it away!
            econ_res[ResourceType.Wood] = max(econ_res.get(ResourceType.Wood, 0), non_quarry_res.get(ResourceType.Wood, 0) + 20)

        # ---- 4. Core Economy Sustain (only build Wood-upkeep buildings when Wood net stays positive!) ----
        n_econ_total = sum(1 for pb in state.placed_buildings.values() if pb.building_def.building_type == BuildingType.Economic)
        econ_cap = max(4, total_pop - max(1, cur_towers // 2))
        max_sawmills = max(3, total_pop // 3 + 1) if net[ResourceType.Wood] < 2.0 else max(2, total_pop // 4 + 1)
        wood_stock = state.resources.get(ResourceType.Wood, 0)
        need_more_saw = n_saw < max_sawmills and (
            net[ResourceType.Wood] < 3.0
            or (wood_stock < 18 and net[ResourceType.Wood] < 8.0)
        )

        if need_more_saw and saw_def and (n_econ_total < econ_cap + 1 or net[ResourceType.Wood] < 4.0):
            # 0-upkeep Sawmill (+18W/day THAT same Dusk!) breaks severe Wood-upkeep soft-locks
            saw_res = dict(econ_res)
            if net[ResourceType.Wood] < 3.0:
                saw_res[ResourceType.Wood] = 0
            elif n_saw < 2 and net[ResourceType.Wood] < 14.0:
                saw_res[ResourceType.Wood] = goal_res.get(ResourceType.Wood, 0)
            if self._try_build_economic(state, saw_def, w, saw_res, allow_dip=False):
                return True

        if n_econ_total < econ_cap:
            if (food_runway < 2.5 and (net[ResourceType.Food] < 5.0 or food_runway < 2.1)) and farm_def and net[ResourceType.Wood] >= 6.0:
                if self._try_build_economic(state, farm_def, w, econ_res, allow_dip=False):
                    return True

            if quarry_def and (net[ResourceType.Stone] < 8.0 or state.resources.get(ResourceType.Stone, 0) < 25) and net[ResourceType.Wood] >= 4.0:
                if self._try_build_economic(state, quarry_def, w, goal_res if n_exp == 0 else non_quarry_res, allow_dip=False):
                    return True

            if retort_def and (net[ResourceType.Coal] < max(5.0, est_coal_burn * 0.8) or state.resources.get(ResourceType.Coal, 0) < 35) and net[ResourceType.Wood] >= 12.0:
                if self._try_build_economic(state, retort_def, w, non_quarry_res, allow_dip=False):
                    return True

        # ---- 5. 2nd ExpeditionCenter later if expanding heavily ----
        desired_exp = 1 if state.day <= 14 else 2
        if exp_def and n_exp < desired_exp and state.day >= 12 and cur_towers >= 2 and food_runway >= 2.2:
            if self._try_build_utility(state, exp_def, w, econ_res):
                return True

        # ---- 6. Outpost placement on Outskirts chunks (if net economy sustains F5 W5 S4 C2) ----
        outpost_def = state.game_data.buildings.get("Outpost")
        if outpost_def and food_runway >= 2.5:
            sustainable_for_outpost = (
                net[ResourceType.Food] >= 6.0
                and net[ResourceType.Wood] >= 6.0
                and net[ResourceType.Stone] >= 4.0
                and (net[ResourceType.Coal] >= 2.0 or state.resources.get(ResourceType.Coal, 0) >= 35)
            )
            active_outposts = sum(1 for pb in state.placed_buildings.values() if pb.building_def.is_outpost)
            max_outposts = 2 if state.day < 20 else 3
            if sustainable_for_outpost and active_outposts < max_outposts:
                if self._try_build_best_outpost(state, outpost_def, w, econ_res):
                    return True

        # ---- 7. Housing Expansion (grow workforce only when Food surplus is stable) ----
        want_pop = min(105, max(8, def_slots + min(econ_slots, 22) + n_exp * 2 + 2))
        if (total_pop < want_pop or max_pop_cap <= total_pop + 1) and food_runway >= 2.5 and net[ResourceType.Food] >= 5.0:
            if self._try_build_best_house(state, pop, w, econ_res):
                return True

        # ---- 8. Rune Towers ----
        rtower_def = state.game_data.buildings.get("RuneSmithTower")
        if rtower_def and n_forge >= 1:
            max_rt = 1 + int(round(state.meta_state.get_value(MetaEffectType.RuneTowerLimit)))
            if self._count_buildings(state, "RuneSmithTower") < max_rt and net[ResourceType.Iron] >= 1.5:
                if self._try_build_rune_tower_next_to_forge(state, rtower_def, reserve):
                    return True

        # ---- 9. Proactive Defense Buffering ----
        if cur_def < target_def * 1.45:
            if self._try_build_best_tower(state, w, econ_res, allow_dip=False, hearth_lvl=planned_hearth):
                return True

        # ---- 10. Optional Economy Scaling ONLY when surplus uncommitted workforce & Wood upkeep won't break ----
        if self._free_settled_slots(state) >= 5 and n_econ_total < econ_cap:
            busy_exp = sum(m.elves_assigned for m in state.active_missions)
            avail_workers = total_pop - busy_exp
            min_needed_staff = n_econ_total + min(cur_towers, max(1, cur_towers))
            uncommitted_pop = avail_workers - min_needed_staff
            if uncommitted_pop >= 1:
                if saw_def and n_saw < max(3, total_pop // 3 + 1) and net[ResourceType.Wood] < 18.0 and self._try_build_economic(state, saw_def, w, econ_res, allow_dip=False):
                    return True
                if farm_def and food_runway < 4.5 and net[ResourceType.Food] < 20.0 and net[ResourceType.Wood] >= 8.0 and self._try_build_economic(state, farm_def, w, econ_res, allow_dip=False):
                    return True
                if quarry_def and net[ResourceType.Stone] < 16.0 and net[ResourceType.Wood] >= 6.0 and self._try_build_economic(state, quarry_def, w, econ_res, allow_dip=False):
                    return True
                if retort_def and net[ResourceType.Coal] < 14.0 and net[ResourceType.Wood] >= 14.0 and self._try_build_economic(state, retort_def, w, econ_res, allow_dip=False):
                    return True

        return False

    # ------------------------------------------------------------------
    # Specific Builders (delegating optimal placement to sim.spatial)
    # ------------------------------------------------------------------

    def _try_build_economic(
        self,
        state: GameState,
        b_def: BuildingDef,
        weights: dict[ResourceType, float],
        reserve: dict[ResourceType, int],
        allow_dip: bool = False,
    ) -> bool:
        if not self._can_afford_with_reserve(state, b_def.construction_cost, reserve, allow_dip_reserve=allow_dip):
            return False
        coord, prod, _ = choose_best_economic_hex(
            state.world,
            b_def,
            state.placed_buildings,
            state._next_building_uid,
            weights,
            beacon_next_day_prod=state.active_next_day_prod_bonus,
        )
        if coord is None:
            return False
        net_val = sum(v * weights.get(r, 1.0) for r, v in prod.items()) - sum(
            u.amount * weights.get(u.resource, 1.0) for u in b_def.upkeep_per_cycle
        )
        if net_val <= 0.0 and b_def.asset_name != "Farm":
            return False
        return state.place_building(b_def, coord, pay_cost=True) is not None

    def _try_build_utility(
        self,
        state: GameState,
        b_def: BuildingDef,
        weights: dict[ResourceType, float],
        reserve: dict[ResourceType, int],
        want_free_neighbors: int = 0,
    ) -> bool:
        if not self._can_afford_with_reserve(state, b_def.construction_cost, reserve, allow_dip_reserve=False):
            return False
        coord = choose_best_housing_or_utility_hex(
            state.world,
            b_def,
            state.placed_buildings,
            weights,
            want_free_neighbors=want_free_neighbors,
        )
        if coord is None:
            return False
        return state.place_building(b_def, coord, pay_cost=True) is not None

    def _try_build_rune_tower_next_to_forge(
        self,
        state: GameState,
        rtower_def: BuildingDef,
        reserve: dict[ResourceType, int],
    ) -> bool:
        if not self._can_afford_with_reserve(state, rtower_def.construction_cost, reserve, allow_dip_reserve=False):
            return False
        best_coord: tuple[int, int] | None = None
        best_score = -1e18
        for uid in state.rune_forges:
            fpb = state.placed_buildings.get(uid)
            if fpb is None:
                continue
            for nb in state.world.get_hex_neighbors(fpb.global_coord, radius=1, revealed_only=True):
                if not state.world.is_chunk_effective_settled(nb.chunk):
                    continue
                ok, _ = state.world.can_place_building(nb.global_coord, rtower_def, state.placed_buildings)
                if not ok:
                    continue
                sc = -0.2 * nb.elevation - (2.0 if nb.feature != int(HexFeatureType.NoneFeature) else 0.0)
                if sc > best_score:
                    best_score = sc
                    best_coord = nb.global_coord
        if best_coord is None:
            return False
        return state.place_building(rtower_def, best_coord, pay_cost=True) is not None

    def _try_build_best_house(
        self,
        state: GameState,
        pop: dict[Race, int],
        weights: dict[ResourceType, float],
        reserve: dict[ResourceType, int],
    ) -> bool:
        candidates: list[tuple[float, str]] = []
        free_dwarves = pop[Race.Dwarves]
        free_elves = pop[Race.Elves]
        free_humans = pop[Race.Humans]

        desired_elves = 2 if state.day <= 12 else 4
        if free_elves < desired_elves:
            candidates.append((3.0, "Elf house"))
        if free_dwarves < max(3, self._count_towers(state) // 2 + self._count_buildings(state, "RuneSmith") * 2):
            candidates.append((2.6, "Dwarven house"))
        if free_humans < 16 or free_humans <= max(free_dwarves, free_elves):
            candidates.append((2.4, "Human house"))
        candidates.append((1.5, "Human house"))
        candidates.append((1.2, "Dwarven house"))
        candidates.append((0.8, "Elf house"))

        candidates.sort(key=lambda x: x[0], reverse=True)
        seen: set[str] = set()
        for _, hname in candidates:
            if hname in seen:
                continue
            seen.add(hname)
            hdef = state.game_data.houses.get(hname)
            if hdef is None:
                continue
            if self._try_build_utility(state, hdef, weights, reserve):
                return True
        return False

    def _try_build_best_outpost(
        self,
        state: GameState,
        outpost_def: BuildingDef,
        weights: dict[ResourceType, float],
        reserve: dict[ResourceType, int],
    ) -> bool:
        if not self._can_afford_with_reserve(state, outpost_def.construction_cost, reserve, allow_dip_reserve=False):
            return False

        _, route_chunks = self._active_route_summary(state)
        best_coord: tuple[int, int] | None = None
        best_score = -1e18

        for ccoord, ch in state.world.chunks.items():
            if ch.state != ChunkState.Outskirts or ch.has_outpost:
                continue
            chunk_bonus = 0.0
            if ccoord in route_chunks:
                chunk_bonus += 25.0
            for hc in ch.hex_coords:
                t = state.world.hexes[hc]
                if t.feature == int(HexFeatureType.Mountain):
                    chunk_bonus += 18.0
                elif t.feature == int(HexFeatureType.Meadow):
                    chunk_bonus += 3.5 * weights.get(ResourceType.Food, 1.0)
                elif t.feature == int(HexFeatureType.Forest):
                    chunk_bonus += 3.0 * weights.get(ResourceType.Wood, 1.0)

            for hc in ch.hex_coords:
                t = state.world.hexes[hc]
                ok, _ = state.world.can_place_building(hc, outpost_def, state.placed_buildings)
                if not ok:
                    continue
                self_pen = 0.0
                if t.feature == int(HexFeatureType.Mountain):
                    self_pen += 50.0
                elif t.feature == int(HexFeatureType.Meadow):
                    self_pen += 10.0
                elif t.feature == int(HexFeatureType.Forest):
                    self_pen += 8.0
                sc = chunk_bonus - self_pen - 0.1 * math.hypot(t.world_x, t.world_z)
                if sc > best_score:
                    best_score = sc
                    best_coord = hc

        if best_coord is None:
            return False
        return state.place_building(outpost_def, best_coord, pay_cost=True) is not None

    def _try_build_best_tower(
        self,
        state: GameState,
        weights: dict[ResourceType, float],
        reserve: dict[ResourceType, int],
        allow_dip: bool = False,
        hearth_lvl: int = 3,
        hard_stone_floor: int = 0,
        hard_coal_floor: int = 0,
    ) -> bool:
        unlocked = state.meta_state.effective_unlocked_towers
        hearth = state.game_data.hearth_levels[hearth_lvl]
        hearth_low = state.game_data.hearth_levels[1]
        econ = self._estimate_daily_economy(state, assume_staffed=True)
        n_retort = self._count_buildings(state, "Retort")
        n_quarry = self._count_buildings(state, "Quarry")

        type_counts: dict[str, int] = {}
        existing_tower_coords: list[tuple[float, float]] = []
        for pb in state.placed_buildings.values():
            if pb.building_def.building_type == BuildingType.Defense:
                type_counts[pb.asset_name] = type_counts.get(pb.asset_name, 0) + 1
                existing_tower_coords.append((pb.world_x, pb.world_z))
        total_existing_towers = len(existing_tower_coords)

        best_choice: tuple[float, TowerDef, tuple[int, int]] | None = None

        for tname in sorted(unlocked):
            td = state.game_data.towers.get(tname)
            if td is None:
                continue
            # Do not burn Stone on Stone-upkeep towers before our first Quarry is operational!
            if n_quarry == 0 and any(u.resource == ResourceType.Stone for u in td.upkeep_per_cycle):
                continue
            # Never build a Coal-cost/Coal-upkeep tower if doing so drops Beacon Hearth below safe tier!
            coal_cost = sum(c.amount for c in td.construction_cost if c.resource == ResourceType.Coal)
            coal_upk = sum(u.amount for u in td.upkeep_per_cycle if u.resource == ResourceType.Coal)
            if (coal_cost > 0 or coal_upk > 0) and total_existing_towers >= 1:
                if n_retort == 0 and state.resources.get(ResourceType.Coal, 0) < 28:
                    continue
                min_h = 3 if (state.day % 5 == 0) else 2
                post_coal = (
                    state.resources.get(ResourceType.Coal, 0)
                    - int(math.ceil(coal_cost))
                    + int(math.floor(econ["prod"].get(ResourceType.Coal, 0.0) * 0.85))
                    - int(math.ceil(econ["econ_upkeep"].get(ResourceType.Coal, 0.0) + econ["def_upkeep"].get(ResourceType.Coal, 0.0) + coal_upk))
                )
                if post_coal < state.compute_hearth_coal_cost(min_h):
                    continue
            # Avoid slow low-single-target Linear bombard until strong core towers exist (or never spam when better towers unlocked)
            cnt = type_counts.get(td.asset_name, 0)
            if td.bullet_kind == "Linear":
                if len(unlocked) > 1 and total_existing_towers < 2:
                    continue
                if len(unlocked) > 2 and (total_existing_towers < 3 or cnt >= 1):
                    continue
            # Enforce strategic Stone & Beacon Coal floors even during dipped Urgent Defense!
            stone_cost = sum(int(math.ceil(c.amount)) for c in td.construction_cost if c.resource == ResourceType.Stone)
            if hard_stone_floor > 0 and stone_cost > 0 and state.resources.get(ResourceType.Stone, 0) - stone_cost < hard_stone_floor:
                continue
            coal_cost_int = int(math.ceil(coal_cost))
            if hard_coal_floor > 0 and coal_cost_int > 0 and state.resources.get(ResourceType.Coal, 0) - coal_cost_int < hard_coal_floor:
                continue
            if not self._can_afford_with_reserve(state, td.construction_cost, reserve, allow_dip_reserve=allow_dip):
                continue

            # Ensure Ether-upkeep towers aren't built early or without large Ether headroom!
            ether_upk = sum(u.amount for u in td.upkeep_per_cycle if u.resource == ResourceType.Ether)
            if ether_upk > 0:
                if state.day < 6:
                    continue
                ether_cost = sum(c.amount for c in td.construction_cost if c.resource == ResourceType.Ether)
                min_ether_buffer = ether_cost + ether_upk * 10.0 + econ["def_upkeep"].get(ResourceType.Ether, 0.0) * 6.0
                if state.resources.get(ResourceType.Ether, 0) < min_ether_buffer:
                    continue
            # Ensure Stone/Wood/Coal/Iron defense upkeep doesn't spiral permanently negative & unpaid
            upkeep_unsustainable = False
            for u in td.upkeep_per_cycle:
                if u.resource in (ResourceType.Stone, ResourceType.Wood, ResourceType.Coal, ResourceType.Iron):
                    if total_existing_towers >= 2 and econ["net"].get(u.resource, 0.0) - u.amount < -2.0 and state.resources.get(u.resource, 0) < u.amount * 7.0:
                        upkeep_unsustainable = True
                        break
            if upkeep_unsustainable:
                continue

            fake_pb = PlacedBuilding(
                uid=-1,
                asset_name=td.asset_name,
                building_def=td,
                global_coord=(0, 0),
                chunk=(0, 0),
                world_x=0.0,
                world_z=0.0,
                build_order=0,
                built_day=state.day,
            )
            fake_pb.assigned_workers[int(Race.Humans)] = 1
            st = compute_tower_stats(fake_pb, hearth, state.get_active_rune_modifiers_by_tower(), state.meta_state)
            st_low = compute_tower_stats(fake_pb, hearth_low, state.get_active_rune_modifiers_by_tower(), state.meta_state)

            # Evaluate spatial hex using Hearth Lv1 minimum range so tower NEVER goes blind at low Hearth!
            eval_range = min(st.world_range, st_low.world_range)
            coord, spatial_score, coverage = choose_best_tower_hex(
                state.world,
                td,
                state.placed_buildings,
                weights,
                effective_world_range=eval_range,
            )
            if coord is None or coverage < 1.15:
                continue

            raw_dps = self._estimate_tower_combat_value(fake_pb, state, hearth_lvl)
            cov_mult = min(2.4, coverage / 3.0)

            cnt = type_counts.get(td.asset_name, 0)
            total_existing_towers = len(existing_tower_coords)
            div_mult = 1.0 / (1.0 + 0.18 * cnt)
            if td.bullet_kind == "Ground" and cnt < 2:
                div_mult *= 1.40
            elif td.bullet_kind == "HitScan" and state.day >= 12 and cnt < 3:
                div_mult *= 1.25
            elif td.bullet_kind == "Linear":
                if total_existing_towers < 2 or (cnt >= 1 and len(unlocked) > 2):
                    div_mult *= 0.40
                elif coverage >= 4.0 and cnt < 2:
                    div_mult *= 0.95
                else:
                    div_mult *= 0.70
            if td.base_range <= 1.2:
                # Short-range melee/monolith needs high corridor density or cheap cost
                div_mult *= 0.65 if coverage < 1.8 else 0.90

            spread_bonus = 1.0
            if state.day >= 18 and existing_tower_coords:
                t_hex = state.world.hexes[coord]
                min_d = min(math.hypot(t_hex.world_x - tx, t_hex.world_z - tz) for tx, tz in existing_tower_coords)
                if min_d >= 8.2:
                    spread_bonus = 1.12

            cost_val = sum(
                c.amount * weights.get(c.resource, 1.5) for c in td.construction_cost
            ) + 4.0 * sum(u.amount * weights.get(u.resource, 1.5) for u in td.upkeep_per_cycle)

            efficiency_score = (raw_dps * cov_mult * div_mult * spread_bonus) / max(10.0, cost_val)
            total_val = efficiency_score * 100.0 + 0.12 * raw_dps * cov_mult + 0.5 * spatial_score

            if best_choice is None or total_val > best_choice[0]:
                best_choice = (total_val, td, coord)

        if best_choice is None:
            return False
        _, chosen_td, chosen_coord = best_choice
        return state.place_building(chosen_td, chosen_coord, pay_cost=True) is not None

    # ------------------------------------------------------------------
    # Blind Expeditions (Recon & Survey)
    # ------------------------------------------------------------------

    def _handle_expeditions(self, state: GameState) -> None:
        if state.available_equipment() <= 0:
            return
        free_elves = state.free_workers_by_race()[Race.Elves]
        if free_elves <= 0:
            return

        econ = self._estimate_daily_economy(state, assume_staffed=True)
        cur_def = self._total_defense_score(state, 3, assume_staffed=True)
        target_def = self._target_defense_score(state)
        _, route_chunks = self._active_route_summary(state)

        candidates: list[tuple[float, str, tuple[int, int], int]] = []
        for ccoord, ch in state.world.chunks.items():
            if ch.state != ChunkState.Borderlands:
                continue
            if any(m.target_chunk == ccoord for m in state.active_missions):
                continue

            elves_to_send = min(free_elves, 2 if free_elves >= 3 else 1)
            ok_s, _ = state.can_start_survey(ccoord, elves=elves_to_send)
            if not ok_s and elves_to_send > 1:
                elves_to_send = 1
                ok_s, _ = state.can_start_survey(ccoord, elves=1)

            if ok_s:
                days, eth, food, wood = state.calculate_survey_cost_live(ccoord, elves=elves_to_send)
                min_wood_after = int(math.ceil(econ["econ_upkeep"].get(ResourceType.Wood, 0.0)))
                min_food_after = max(12, int(econ["morning_food"][ResourceType.Food] * 1.1))
                if (
                    state.resources.get(ResourceType.Food, 0) - food >= min_food_after
                    and state.resources.get(ResourceType.Wood, 0) - wood >= min_wood_after
                ):
                    is_active_road_entry = ccoord in route_chunks
                    if is_active_road_entry and ch.ring >= 2 and (cur_def < target_def * 1.0 or self._recent_leak_pressure(state) > 0.25):
                        pass
                    else:
                        score = 40.0 / max(1, ch.ring) - days * 1.5 - eth * 2.0
                        if is_active_road_entry:
                            score += 32.0 if ch.ring == 1 else 18.0
                        if ccoord in state.recon_intel_chunks or ch.recon_completed:
                            for hc in ch.hex_coords:
                                ft = state.world.hexes[hc].feature
                                if ft == int(HexFeatureType.Mountain):
                                    score += 14.0
                                elif ft == int(HexFeatureType.Meadow):
                                    score += 3.0
                                elif ft == int(HexFeatureType.Forest):
                                    score += 2.0
                        candidates.append((score, "Survey", ccoord, elves_to_send))

            ok_r, _ = state.can_start_recon(ccoord)
            if ok_r and free_elves >= 2 and len(state.recon_intel_chunks) < 4 and state.day >= 4:
                _, _, eth, food, wood = state.world.calculate_recon_cost()
                if state.resources.get(ResourceType.Food, 0) - food >= 25 and state.resources.get(ResourceType.Ether, 0) - eth >= 12:
                    score = 12.0 / max(1, ch.ring)
                    candidates.append((score, "Recon", ccoord, 1))

        if not candidates:
            return
        candidates.sort(key=lambda x: x[0], reverse=True)
        for _, kind, ccoord, e_cnt in candidates:
            if state.available_equipment() <= 0 or state.free_workers_by_race()[Race.Elves] <= 0:
                break
            if kind == "Survey":
                state.start_survey(ccoord, elves=min(e_cnt, state.free_workers_by_race()[Race.Elves]))
            else:
                state.start_recon(ccoord)

    # ------------------------------------------------------------------
    # Rune Crafting & Forge Infusions (Strictly Blind Inventory)
    # ------------------------------------------------------------------

    def _handle_runes_and_forges(self, state: GameState) -> None:
        if state.meta_state.is_unlocked(MetaEffectType.UnlockRuneTransmutation):
            hearth_mult = 1.0 + state.game_data.hearth_levels[1].rune_drop_and_rarity_bonus
            for _ in range(8):
                by_rarity: dict[RuneRarity, list[RuneInstance]] = {}
                for r in state.rune_inventory:
                    by_rarity.setdefault(r.rarity, []).append(r)
                did_transmute = False
                for rar in (RuneRarity.Common, RuneRarity.Uncommon):
                    pool = by_rarity.get(rar, [])
                    if len(pool) >= 4:
                        if state.transmute_runes(pool[0].uid, pool[1].uid, pool[2].uid, hearth_mult=hearth_mult):
                            did_transmute = True
                            break
                if not did_transmute:
                    break

        if state.meta_state.is_unlocked(MetaEffectType.UnlockRuneFusion):
            for _ in range(10):
                inv = list(state.rune_inventory)
                fused = False
                for i in range(len(inv)):
                    for j in range(i + 1, len(inv)):
                        if can_fuse_pair(inv[i], inv[j], state.meta_state):
                            if state.fuse_runes(inv[i].uid, inv[j].uid) is not None:
                                fused = True
                                break
                    if fused:
                        break
                if not fused:
                    break

        if not state.rune_forges:
            return

        for rf in state.rune_forges.values():
            if rf.equipped_runes:
                state.rune_inventory.extend(rf.equipped_runes)
                rf.equipped_runes.clear()

        tower_dps_by_type: dict[str, float] = {}
        for pb in state.placed_buildings.values():
            if pb.building_def.building_type == BuildingType.Defense:
                tower_dps_by_type[pb.asset_name] = tower_dps_by_type.get(pb.asset_name, 0.0) + self._estimate_tower_combat_value(pb, state, 3)

        if not tower_dps_by_type or not state.rune_inventory:
            return

        dwarves_total = state.total_population_by_race()[Race.Dwarves]
        ranked_types = sorted(tower_dps_by_type.items(), key=lambda x: x[1], reverse=True)

        dwarf_budget = dwarves_total
        for f_idx, (uid, rf) in enumerate(state.rune_forges.items()):
            fpb = state.placed_buildings.get(uid)
            if fpb is None or dwarf_budget <= 0:
                rf.infused_tower = None
                continue
            target_type = ranked_types[min(f_idx, len(ranked_types) - 1)][0]
            td = state.game_data.towers.get(target_type)
            if td is None:
                continue
            rf.infused_tower = target_type
            max_slots = min(state.effective_max_workers(fpb), dwarf_budget)
            scored = sorted(
                state.rune_inventory,
                key=lambda r: score_rune_for_tower(r, td),
                reverse=True,
            )
            chosen = scored[:max_slots]
            chosen_uids = {r.uid for r in chosen}
            rf.equipped_runes = chosen
            state.rune_inventory = [r for r in state.rune_inventory if r.uid not in chosen_uids]
            dwarf_budget -= len(chosen)

    # ------------------------------------------------------------------
    # Micro Worker & Tower Crew Assignment
    # ------------------------------------------------------------------

    def _allocate_workers(self, state: GameState, hearth_lvl: int) -> None:
        state.clear_non_expedition_workers()
        free = state.free_workers_by_race()

        for uid, rf in state.rune_forges.items():
            if not rf.infused_tower or not rf.equipped_runes:
                continue
            fpb = state.placed_buildings.get(uid)
            if fpb is None or not fpb.is_active:
                continue
            need_dw = min(len(rf.equipped_runes), state.effective_max_workers(fpb), free[Race.Dwarves])
            for _ in range(need_dw):
                if state.assign_worker(uid, Race.Dwarves):
                    free[Race.Dwarves] -= 1

        weights = self._compute_resource_weights(state)
        cur_def = self._total_defense_score(state, hearth_lvl, assume_staffed=False)
        target_def = self._target_defense_score(state)
        def_need_mult = 1.8 if cur_def < target_def * 1.15 else (1.1 if cur_def < target_def * 1.6 else 0.6)
        if state.day == 1:
            def_need_mult = 2.5

        while sum(free.values()) > 0:
            best_gain = -1.0
            best_move: tuple[int, Race] | None = None

            for uid, pb in state.placed_buildings.items():
                if not pb.is_active:
                    continue
                cur_w = sum(pb.assigned_workers.values())
                max_w = state.effective_max_workers(pb)
                if cur_w >= max_w:
                    continue

                b = pb.building_def
                if b.building_type == BuildingType.Defense:
                    before_dps = self._estimate_tower_combat_value(pb, state, hearth_lvl)
                    for race in (Race.Dwarves, Race.Elves, Race.Humans):
                        if free[race] <= 0:
                            continue
                        pb.assigned_workers[int(race)] = pb.assigned_workers.get(int(race), 0) + 1
                        after_dps = self._estimate_tower_combat_value(pb, state, hearth_lvl)
                        pb.assigned_workers[int(race)] -= 1
                        if pb.assigned_workers[int(race)] == 0:
                            del pb.assigned_workers[int(race)]
                        delta = (after_dps - before_dps) * 0.30 * def_need_mult
                        if delta > best_gain:
                            best_gain = delta
                            best_move = (uid, race)

                elif b.building_type == BuildingType.Economic:
                    raw = state.world.compute_building_production_at_eff1(
                        pb.global_coord,
                        b,
                        state.placed_buildings,
                        candidate_build_order=pb.build_order,
                        exclude_building_uid=pb.uid,
                        beacon_next_day_prod_bonus=state.active_next_day_prod_bonus,
                    )
                    raw_val = sum(amt * weights.get(r, 1.0) for r, amt in raw.items())
                    before_eff = state.compute_economic_efficiency(pb, upkeep_paid=True)
                    for race in (Race.Humans, Race.Elves, Race.Dwarves):
                        if free[race] <= 0:
                            continue
                        if b.requires_specific_citizen and b.required_citizen_race != race:
                            continue
                        pb.assigned_workers[int(race)] = pb.assigned_workers.get(int(race), 0) + 1
                        after_eff = state.compute_economic_efficiency(pb, upkeep_paid=True)
                        pb.assigned_workers[int(race)] -= 1
                        if pb.assigned_workers[int(race)] == 0:
                            del pb.assigned_workers[int(race)]
                        delta = (after_eff - before_eff) * raw_val
                        if race == Race.Dwarves:
                            delta *= 0.88
                        elif race == Race.Elves and not (
                            b.asset_name in ("Sawmill", "Farm")
                            and state.meta_state.has_effect(MetaEffectType.ProductionSynergy_Elf)
                        ):
                            delta *= 0.90
                        if delta > best_gain:
                            best_gain = delta
                            best_move = (uid, race)

            if best_move is None or best_gain <= 0.0:
                break
            uid_pick, race_pick = best_move
            if state.assign_worker(uid_pick, race_pick):
                free[race_pick] -= 1
            else:
                break

    # ------------------------------------------------------------------
    # Hearth Level Optimization (1..5)
    # ------------------------------------------------------------------

    def _choose_hearth_level(self, state: GameState) -> int:
        econ = self._estimate_daily_economy(state, assume_staffed=True)
        coal_stock = state.resources.get(ResourceType.Coal, 0)
        coal_upkeep = int(math.ceil(econ["def_upkeep"].get(ResourceType.Coal, 0.0) + econ["econ_upkeep"].get(ResourceType.Coal, 0.0)))
        # Credit Retort Coal produced at Dusk before Beacon!
        coal_prod = int(math.floor(econ["prod"].get(ResourceType.Coal, 0.0) * 0.85))
        avail_coal = max(0, coal_stock + coal_prod - coal_upkeep)

        max_affordable = 1
        for lvl in (5, 4, 3, 2, 1):
            if state.compute_hearth_coal_cost(lvl) <= avail_coal:
                max_affordable = lvl
                break

        leak_p = self._recent_leak_pressure(state)
        avg_len, _ = self._active_route_summary(state)
        target_def = self._target_defense_score(state)
        cur_def = self._total_defense_score(state, 3, assume_staffed=True)
        safety_ratio = cur_def / max(1.0, target_def)
        has_coal_prod = econ["prod"].get(ResourceType.Coal, 0.0) >= 6.0

        # Early Game (Days 1..3) with short 1.73u (0,0) corridor:
        # Need Hearth Lv3 unless a tower clearly exceeds Lv2's 1.05 enemy HP/speed threshold!
        if state.day <= 2 and avg_len <= 2.2:
            # Check max single-shot damage among placed combat towers at Hearth Lv2
            h2 = state.game_data.hearth_levels[2]
            rune_mods = state.get_active_rune_modifiers_by_tower()
            max_single_dmg = 0.0
            for pb in state.placed_buildings.values():
                if pb.building_def.building_type == BuildingType.Defense:
                    st2 = compute_tower_stats(pb, h2, rune_mods, state.meta_state)
                    max_single_dmg = max(max_single_dmg, st2.damage)
            can_one_shot_lv2 = max_single_dmg >= 64.0 or self._count_towers(state) >= 2
            desired = 2 if can_one_shot_lv2 else 3
            return min(desired, max_affordable)

        # Milestone / leak / danger nights
        if state.day % 10 == 0 or leak_p >= 0.35 or state.city_hp <= 12:
            desired = 5 if avail_coal >= state.compute_hearth_coal_cost(5) * (1.2 if has_coal_prod else 2.5) else 4
            return min(desired, max_affordable)

        # Conserve Coal while Coal economy isn't net-positive yet!
        if not has_coal_prod and coal_stock < 35:
            desired = 2 if safety_ratio >= 1.10 else 3
            return min(desired, max_affordable)

        if safety_ratio >= 2.0 and avg_len >= 4.0 and state.resources.get(ResourceType.Ether, 0) < 25:
            desired = 1 if state.day <= 14 else 2
        elif safety_ratio >= 1.40:
            desired = 2 if avail_coal < 50 else 3
        elif safety_ratio >= 0.95:
            desired = 3 if avail_coal < 65 else 4
        else:
            desired = 4 if avail_coal < 80 else 5

        # Late-game Hearth Lv5 (+5% next-day economy +35% Rng/FR -15% enemy) when Coal overflows
        if desired < 5 and state.day >= 15 and has_coal_prod and avail_coal >= state.compute_hearth_coal_cost(5) * 3:
            desired = 5

        return max(1, min(desired, max_affordable))
