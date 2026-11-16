"""Reporting, ASCII hex map rendering, JSON/CSV export, and batch analytics."""

from __future__ import annotations

import csv
import json
import math
import statistics
from pathlib import Path
from typing import Any

from .engine import GameState
from .enums import BuildingType, ChunkState, HexFeatureType, Race, ResourceType
from .meta import MetaState
from .spatial import MapWorld, PlacedBuilding


# ---------------------------------------------------------------------------
# ASCII Map Rendering
# ---------------------------------------------------------------------------

_TOWER_SHORT_TAGS: dict[str, str] = {
    "1. Sentinel Arbalest": "Arb",
    "2. Thunderbuss": "Thb",
    "3. Steam Repeater": "Stm",
    "4. Crucible of Conflagrations": "Crc",
    "5. Shadow Spire": "Shd",
    "6. Royal Bombard": "Bmb",
    "7. Soul Monolith": "Mon",
}

_BUILDING_SHORT_TAGS: dict[str, str] = {
    "Sawmill": "Saw",
    "Farm": "Frm",
    "Quarry": "Qry",
    "Retort": "Ret",
    "Mines": "Min",
    "Human house": "Hum",
    "Elf house": "Elf",
    "Dwarven house": "Dwf",
    "Ruined Human house": "RHu",
    "Ruined Elf house": "REl",
    "Ruined Elven house": "REl",
    "Ruined Dwarven house": "RDw",
    "ExpeditionCenter": "Exp",
    "Outpost": "Out",
    "RuneSmith": "RFr",
    "RuneSmithTower": "RTW",
}


def _building_tag(pb: PlacedBuilding) -> str:
    if pb.asset_name in _TOWER_SHORT_TAGS:
        tag = _TOWER_SHORT_TAGS[pb.asset_name]
    elif pb.asset_name in _BUILDING_SHORT_TAGS:
        tag = _BUILDING_SHORT_TAGS[pb.asset_name]
    else:
        clean = "".join(ch for ch in pb.asset_name if ch.isalnum())
        tag = (clean[:3] or "Bld").ljust(3)
    return tag[:3] if pb.is_active else tag[:2].lower() + "!"


def _tile_token(
    world: MapWorld,
    tile: Any,
    placed_buildings: dict[int, PlacedBuilding],
) -> str:
    if tile.feature == int(HexFeatureType.Base):
        return "[B]"
    if tile.feature == int(HexFeatureType.Beacon):
        return "[H]"
    if tile.occupied_by is not None and tile.occupied_by in placed_buildings:
        return _building_tag(placed_buildings[tile.occupied_by])

    ch = world.chunks.get(tile.chunk)
    cstate = ch.state if ch is not None else ChunkState.Wilderness
    if tile.is_road:
        if cstate in (ChunkState.Settled, ChunkState.Outskirts):
            return "***"
        if cstate == ChunkState.Surveying:
            return "+++"
        return ":::"

    ft = tile.feature
    if ft == int(HexFeatureType.Mountain):
        return "^^^"
    if ft == int(HexFeatureType.Forest):
        return "TTT"
    if ft == int(HexFeatureType.Meadow):
        return ",,,"
    if ft == int(HexFeatureType.Hill):
        return "nnn"
    if ft == int(HexFeatureType.Sinkhole):
        return "ooo"

    if cstate == ChunkState.Settled:
        return " . "
    if cstate == ChunkState.Outskirts:
        return " : "
    if cstate == ChunkState.Surveying:
        return " ~ "
    return " ? "


def render_ascii_map(
    world: MapWorld,
    placed_buildings: dict[int, PlacedBuilding],
    include_borderlands: bool = True,
) -> str:
    """Render an ASCII hex grid over all non-wilderness tiles with legend and structure index."""
    visible_states = {ChunkState.Settled, ChunkState.Outskirts, ChunkState.Surveying}
    if include_borderlands:
        visible_states.add(ChunkState.Borderlands)

    visible_tiles = []
    for tile in world.hexes.values():
        ch = world.chunks.get(tile.chunk)
        if ch is not None and ch.state in visible_states:
            visible_tiles.append(tile)

    if not visible_tiles:
        return "(Empty map: no visible tiles)"

    # Map continuous hex world coordinates (hex spacing: dx=sqrt(3), dz=1.5 per row)
    # Using col_step = sqrt(3)/2 => adjacent horizontal hexes are +2 grid cols; width 2 chars per col gives +4 chars spacing.
    xs = [t.world_x for t in visible_tiles]
    zs = [t.world_z for t in visible_tiles]
    min_x = min(xs)
    max_z = max(zs)

    half_hx = math.sqrt(3.0) * 0.5  # ~0.866025
    row_step = 1.5

    cells: dict[tuple[int, int], str] = {}
    max_row = 0
    max_col = 0
    for tile in visible_tiles:
        row = int(round((max_z - tile.world_z) / row_step))
        col_idx = int(round((tile.world_x - min_x) / half_hx))
        char_col = col_idx * 2
        cells[(row, char_col)] = _tile_token(world, tile, placed_buildings)
        if row > max_row:
            max_row = row
        if char_col > max_col:
            max_col = char_col

    width = max_col + 4
    lines: list[str] = ["=== HEX MAP VIEW (North=+Z top, East=+X right) ==="]
    for r in range(max_row + 1):
        buf = [" "] * width
        row_cells = [(cc, tok) for (rr, cc), tok in cells.items() if rr == r]
        if not row_cells:
            continue
        for cc, tok in row_cells:
            t3 = tok[:3].ljust(3)
            for idx, ch in enumerate(t3):
                if cc + idx < width:
                    buf[cc + idx] = ch
        lines.append("".join(buf).rstrip())

    # Active routes summary
    routes = world.get_active_routes()
    lines.append("")
    lines.append(
        "Legend: [B]=Base [H]=Beacon ***=ActiveRoad :::=BorderRoad "
        "^^^=Mtn TTT=Forest ,,,=Meadow nnn=Hill ooo=Sink .=Settled :=Outskirts ?=Border"
    )
    lines.append(
        "Tags:   Arb=Arbalest Thb=Thunderbuss Stm=Repeater Crc=Crucible Shd=Shadow Bmb=Bombard Mon=Monolith | "
        "Saw Frm Qry Ret Min | Hum Elf Dwf | Exp Out RFr RTW (!=unpaid/off)"
    )
    lines.append(
        "Routes: "
        + (
            ", ".join(
                f"#{idx} len={r.total_length:.2f}w ({len(r.segments)} segs, w={r.flow_weight:.2f})"
                for idx, r in enumerate(routes)
            )
            if routes
            else "None"
        )
    )

    # Compact placed structures summary by type & chunk
    counts: dict[str, int] = {}
    for pb in placed_buildings.values():
        counts[pb.asset_name] = counts.get(pb.asset_name, 0) + 1
    if counts:
        lines.append(
            "Placed Counts: " + ", ".join(f"{k}x{v}" for k, v in sorted(counts.items()))
        )

    return "\n".join(lines)


# ---------------------------------------------------------------------------
# Single Run Formatting & Serialization
# ---------------------------------------------------------------------------


def _chunk_counts_by_state(state: GameState) -> dict[str, int]:
    counts = {cs.name: 0 for cs in ChunkState}
    for ch in state.world.chunks.values():
        counts[ch.state.name] = counts.get(ch.state.name, 0) + 1
    return counts


def format_day_summary(state: GameState, verbose: bool = False) -> str:
    """Format full single-seed Day 1..N simulation trace and end-of-run breakdown."""
    m = state.meta_state
    outcome = "VICTORY" if state.victory else ("DEFEAT" if state.defeat else "STOPPED")
    lines: list[str] = []
    lines.append("=" * 112)
    lines.append(
        f"SIMULATION SUMMARY | Seed={state.seed} | Meta={m.name} (Upgrades={len(m.unlocked_ids)}, "
        f"Drafted={len(m.drafted_towers)}) | Waves={state.wave_source}"
    )
    lines.append(
        f"Outcome: {outcome} on Day {state.day}"
        + (f" ({state.defeat_reason})" if state.defeat_reason else "")
        + f" | CityHP={state.city_hp}/{state.max_city_hp} | PeakPop={state.peak_population} | "
        + f"Hope={state.hope.total_hope} ({len(state.hope.unlocked_ids)}/{len(state.game_data.hope_milestones)}) | "
        + f"RunesDropped={state.total_runes_dropped} | CombatEther={state.total_ether_from_combat}"
    )
    if m.variant_choices:
        lines.append(
            "Meta Variants: " + ", ".join(f"{k}={v}" for k, v in sorted(m.variant_choices.items()))
        )
    if m.effective_unlocked_towers:
        lines.append(
            "Unlocked Towers: " + ", ".join(sorted(m.effective_unlocked_towers))
        )
    lines.append("-" * 112)
    lines.append(
        f"{'Day':>3} | {'H':>2} | {'C_c':>3} | {'HP':>5} | {'Pop(H/E/D)':>11} | "
        f"{'Chk S/O/V/B':>13} | {'K(E/B)':>9} | {'Lk(N/E/B)':>9} | {'U/S':>3} | "
        f"{'Prod(W/S/F/C/I)':>19} | Actions"
    )
    lines.append("-" * 112)

    for log in state.day_logs:
        nr = log.night_result or {}
        pop = log.population or {}
        pop_str = f"{pop.get('Humans', 0):2d}/{pop.get('Elves', 0):2d}/{pop.get('Dwarves', 0):2d}"
        cs = log.chunks_summary or {}
        chk_str = (
            f"{cs.get('Settled', 0):2d}/{cs.get('Outskirts', 0):2d}/"
            f"{cs.get('Surveying', 0):1d}/{cs.get('Borderlands', 0):2d}"
        )
        kills = f"{int(nr.get('kills', 0)):3d}({int(nr.get('kills_elite', 0))}/{int(nr.get('kills_boss', 0))})"
        leaks = f"{int(nr.get('leaks_normal', 0)):3d}/{int(nr.get('leaks_elite', 0)):2d}/{int(bool(nr.get('boss_leaked', False)))}"
        us = f"{len(log.upkeep_unpaid_buildings)}/{len(log.housing_starved)}"
        pr = log.production_gained or {}
        prod_str = (
            f"{pr.get('Wood', 0):3d}/{pr.get('Stone', 0):3d}/{pr.get('Food', 0):3d}/"
            f"{pr.get('Coal', 0):3d}/{pr.get('Iron', 0):2d}"
        )
        act_str = ", ".join(log.actions) if log.actions else "-"
        hp_str = f"{log.end_city_hp:2d}/{state.max_city_hp:<2d}"
        lines.append(
            f"{log.day:3d} | L{log.hearth_level} | {log.hearth_coal_cost:3d} | {hp_str} | "
            f"{pop_str:>11} | {chk_str:>13} | {kills:>9} | {leaks:>9} | {us:>3} | "
            f"{prod_str:>19} | {act_str}"
        )
        if verbose:
            er = log.end_resources or {}
            stock_str = " ".join(f"{k[0]}:{v}" for k, v in er.items())
            hope_str = ", ".join(log.hope_unlocked_today) if log.hope_unlocked_today else "-"
            lines.append(f"      EndRes=[{stock_str}] | Hope={log.hope_total} (+{hope_str})")

    lines.append("-" * 112)

    # Damage breakdown
    total_dmg = sum(state.total_damage_by_tower_type.values())
    if state.total_damage_by_tower_type:
        lines.append("Tower Damage Share:")
        for tname, dmg in sorted(state.total_damage_by_tower_type.items(), key=lambda x: -x[1]):
            pct = (100.0 * dmg / total_dmg) if total_dmg > 0 else 0.0
            lines.append(f"  - {tname:<32} {dmg:12,.1f} ({pct:5.1f}%)")

    # Cumulative economy & end resources
    cum_prod = ", ".join(
        f"{r.name}={state.total_production_by_resource.get(r, 0)}"
        for r in (ResourceType.Wood, ResourceType.Stone, ResourceType.Food, ResourceType.Coal, ResourceType.Iron, ResourceType.Ether)
    )
    end_res = ", ".join(
        f"{r.name}={state.resources.get(r, 0)}"
        for r in (ResourceType.Ether, ResourceType.Wood, ResourceType.Stone, ResourceType.Iron, ResourceType.Coal, ResourceType.Food)
    )
    lines.append(f"Cumulative Production: {cum_prod}")
    lines.append(f"End Stockpiles:        {end_res}")

    # Rune Forge loadouts
    if state.rune_forges:
        lines.append("Rune Forges:")
        for uid, rf in sorted(state.rune_forges.items()):
            runes_txt = ", ".join(r.short_label() for r in rf.equipped_runes) if rf.equipped_runes else "(empty)"
            lines.append(f"  - Forge#{uid}: infused={rf.infused_tower or 'None'} | runes=[{runes_txt}]")

    # Hope milestones summary
    if state.hope.unlocked_ids:
        sorted_ids = sorted(state.hope.unlocked_ids)
        lines.append(
            f"Hope Milestones ({len(sorted_ids)} unlocked, {state.hope.total_hope} Hope): "
            + ", ".join(sorted_ids)
        )
    lines.append("=" * 112)
    return "\n".join(lines)


def print_day_summary(state: GameState, verbose: bool = False) -> None:
    print(format_day_summary(state, verbose=verbose))


def state_to_report_dict(state: GameState) -> dict[str, Any]:
    """Convert full GameState simulation result into a deterministic JSON-serializable dict."""
    pop = state.total_population_by_race()
    total_dmg = sum(state.total_damage_by_tower_type.values())
    dmg_sorted = {
        k: round(v, 2)
        for k, v in sorted(state.total_damage_by_tower_type.items(), key=lambda x: (-x[1], x[0]))
    }
    dmg_share = {
        k: round((100.0 * v / total_dmg) if total_dmg > 0 else 0.0, 2)
        for k, v in dmg_sorted.items()
    }

    placed_summary: list[dict[str, Any]] = []
    for uid in sorted(state.placed_buildings.keys()):
        pb = state.placed_buildings[uid]
        placed_summary.append(
            {
                "uid": pb.uid,
                "asset_name": pb.asset_name,
                "building_type": pb.building_def.building_type.name,
                "global_coord": list(pb.global_coord),
                "chunk": list(pb.chunk),
                "built_day": pb.built_day,
                "build_order": pb.build_order,
                "is_active": pb.is_active,
                "last_upkeep_paid": pb.last_upkeep_paid,
                "residents": pb.residents,
                "assigned_workers": {
                    Race(k).name: v for k, v in sorted(pb.assigned_workers.items()) if v > 0
                },
            }
        )

    forges_summary = []
    for uid in sorted(state.rune_forges.keys()):
        rf = state.rune_forges[uid]
        forges_summary.append(
            {
                "building_uid": uid,
                "infused_tower": rf.infused_tower,
                "equipped_runes": [r.short_label() for r in rf.equipped_runes],
            }
        )

    day_logs_list = []
    for log in state.day_logs:
        day_logs_list.append(
            {
                "day": log.day,
                "start_resources": dict(sorted(log.start_resources.items())),
                "actions": list(log.actions),
                "production_gained": dict(sorted(log.production_gained.items())),
                "upkeep_unpaid_buildings": list(log.upkeep_unpaid_buildings),
                "housing_starved": list(log.housing_starved),
                "hearth_level": log.hearth_level,
                "hearth_coal_cost": log.hearth_coal_cost,
                "night_result": log.night_result,
                "end_resources": dict(sorted(log.end_resources.items())),
                "end_city_hp": log.end_city_hp,
                "population": dict(sorted(log.population.items())),
                "chunks_summary": dict(sorted(log.chunks_summary.items())),
                "hope_total": log.hope_total,
                "hope_unlocked_today": list(log.hope_unlocked_today),
                "defeat_reason": log.defeat_reason,
            }
        )

    return {
        "seed": state.seed,
        "wave_source": state.wave_source,
        "meta": {
            "name": state.meta_state.name,
            "unlocked_ids": sorted(state.meta_state.unlocked_ids),
            "variant_choices": dict(sorted(state.meta_state.variant_choices.items())),
            "drafted_towers": sorted(state.meta_state.drafted_towers),
            "effective_unlocked_towers": sorted(state.meta_state.effective_unlocked_towers),
            "ignore_building_target_bug": state.meta_state.ignore_building_target_bug,
            "warnings": list(state.meta_state.warnings),
        },
        "outcome": {
            "victory": state.victory,
            "defeat": state.defeat,
            "defeat_reason": state.defeat_reason,
            "day_reached": state.day,
            "city_hp": state.city_hp,
            "max_city_hp": state.max_city_hp,
            "peak_population": state.peak_population,
            "end_population": {r.name: pop[r] for r in (Race.Humans, Race.Elves, Race.Dwarves)},
            "total_ether_from_combat": state.total_ether_from_combat,
            "total_runes_dropped": state.total_runes_dropped,
            "unequipped_runes_count": len(state.rune_inventory),
        },
        "hope": {
            "total_hope": state.hope.total_hope,
            "unlocked_count": len(state.hope.unlocked_ids),
            "total_milestones": len(state.game_data.hope_milestones),
            "unlocked_ids": sorted(state.hope.unlocked_ids),
            "bosses_killed": state.hope.bosses_killed,
            "expeditions_completed": state.hope.expeditions_completed,
            "enemies_killed": state.hope.enemies_killed,
            "production_buildings_built": state.hope.production_buildings_built,
            "rune_crafting_done": state.hope.rune_crafting_done,
            "legendary_or_cursed_obtained": state.hope.legendary_or_cursed_obtained,
        },
        "end_resources": {
            r.name: state.resources.get(r, 0)
            for r in (ResourceType.Ether, ResourceType.Wood, ResourceType.Stone, ResourceType.Iron, ResourceType.Coal, ResourceType.Food)
        },
        "cumulative_production": {
            r.name: state.total_production_by_resource.get(r, 0)
            for r in (ResourceType.Wood, ResourceType.Stone, ResourceType.Food, ResourceType.Coal, ResourceType.Iron, ResourceType.Ether)
        },
        "total_damage_by_tower_type": dmg_sorted,
        "damage_share_pct": dmg_share,
        "chunks_summary": _chunk_counts_by_state(state),
        "active_routes": [
            {
                "index": idx,
                "total_length": round(r.total_length, 3),
                "segments_count": len(r.segments),
                "flow_weight": round(r.flow_weight, 4),
            }
            for idx, r in enumerate(state.world.get_active_routes())
        ],
        "rune_forges": forges_summary,
        "placed_buildings": placed_summary,
        "day_logs": day_logs_list,
    }


def export_single_run_json(state: GameState, path: str | Path) -> Path:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    payload = state_to_report_dict(state)
    p.write_text(json.dumps(payload, indent=2, ensure_ascii=False), encoding="utf-8")
    return p


def export_day_logs_csv(state: GameState, path: str | Path) -> Path:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    fieldnames = [
        "seed",
        "day",
        "hearth_level",
        "hearth_coal_cost",
        "city_hp",
        "max_city_hp",
        "pop_humans",
        "pop_elves",
        "pop_dwarves",
        "pop_total",
        "chunks_settled",
        "chunks_outskirts",
        "chunks_surveying",
        "chunks_borderlands",
        "chunks_wilderness",
        "unpaid_count",
        "starved_count",
        "prod_wood",
        "prod_stone",
        "prod_food",
        "prod_coal",
        "prod_iron",
        "prod_ether",
        "kills_total",
        "kills_elite",
        "kills_boss",
        "leaks_normal",
        "leaks_elite",
        "boss_leaked",
        "ether_gained",
        "runes_dropped",
        "end_ether",
        "end_wood",
        "end_stone",
        "end_iron",
        "end_coal",
        "end_food",
        "hope_total",
        "hope_unlocked_count",
        "actions",
        "defeat_reason",
    ]
    running_hope_unlocked = 0
    with p.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=fieldnames)
        writer.writeheader()
        for log in state.day_logs:
            running_hope_unlocked += len(log.hope_unlocked_today)
            pop = log.population or {}
            cs = log.chunks_summary or {}
            pr = log.production_gained or {}
            nr = log.night_result or {}
            er = log.end_resources or {}
            writer.writerow(
                {
                    "seed": state.seed,
                    "day": log.day,
                    "hearth_level": log.hearth_level,
                    "hearth_coal_cost": log.hearth_coal_cost,
                    "city_hp": log.end_city_hp,
                    "max_city_hp": state.max_city_hp,
                    "pop_humans": pop.get("Humans", 0),
                    "pop_elves": pop.get("Elves", 0),
                    "pop_dwarves": pop.get("Dwarves", 0),
                    "pop_total": sum(pop.values()),
                    "chunks_settled": cs.get("Settled", 0),
                    "chunks_outskirts": cs.get("Outskirts", 0),
                    "chunks_surveying": cs.get("Surveying", 0),
                    "chunks_borderlands": cs.get("Borderlands", 0),
                    "chunks_wilderness": cs.get("Wilderness", 0),
                    "unpaid_count": len(log.upkeep_unpaid_buildings),
                    "starved_count": len(log.housing_starved),
                    "prod_wood": pr.get("Wood", 0),
                    "prod_stone": pr.get("Stone", 0),
                    "prod_food": pr.get("Food", 0),
                    "prod_coal": pr.get("Coal", 0),
                    "prod_iron": pr.get("Iron", 0),
                    "prod_ether": pr.get("Ether", 0),
                    "kills_total": nr.get("kills", 0),
                    "kills_elite": nr.get("kills_elite", 0),
                    "kills_boss": nr.get("kills_boss", 0),
                    "leaks_normal": nr.get("leaks_normal", 0),
                    "leaks_elite": nr.get("leaks_elite", 0),
                    "boss_leaked": int(bool(nr.get("boss_leaked", False))),
                    "ether_gained": nr.get("ether_gained", 0),
                    "runes_dropped": len(nr.get("runes_dropped", [])),
                    "end_ether": er.get("Ether", 0),
                    "end_wood": er.get("Wood", 0),
                    "end_stone": er.get("Stone", 0),
                    "end_iron": er.get("Iron", 0),
                    "end_coal": er.get("Coal", 0),
                    "end_food": er.get("Food", 0),
                    "hope_total": log.hope_total,
                    "hope_unlocked_count": running_hope_unlocked,
                    "actions": "; ".join(log.actions),
                    "defeat_reason": log.defeat_reason or "",
                }
            )
    return p


# ---------------------------------------------------------------------------
# Monte Carlo Batch Aggregation & Formatting
# ---------------------------------------------------------------------------


def aggregate_batch_results(
    states: list[GameState],
    meta_state: MetaState | None = None,
) -> dict[str, Any]:
    """Aggregate N simulation runs into Monte Carlo summary metrics."""
    if not states:
        return {"total_runs": 0, "per_seed": []}

    n = len(states)
    victories = sum(1 for s in states if s.victory)
    defeats = sum(1 for s in states if s.defeat)
    days = [s.day for s in states]
    win_hps = [s.city_hp for s in states if s.victory]
    hopes = [s.hope.total_hope for s in states]
    hope_counts = [len(s.hope.unlocked_ids) for s in states]
    pops = [s.peak_population for s in states]
    runes = [s.total_runes_dropped for s in states]
    ethers = [s.total_ether_from_combat for s in states]

    unpaid_days_per_run = [sum(1 for l in s.day_logs if l.upkeep_unpaid_buildings) for s in states]
    starved_days_per_run = [sum(1 for l in s.day_logs if l.housing_starved) for s in states]

    defeat_reasons: dict[str, int] = {}
    dmg_totals: dict[str, float] = {}
    prod_totals: dict[str, int] = {}
    per_seed: list[dict[str, Any]] = []

    for s in states:
        if s.defeat:
            reason = s.defeat_reason or "Unknown defeat"
            defeat_reasons[reason] = defeat_reasons.get(reason, 0) + 1
        for tname, dmg in s.total_damage_by_tower_type.items():
            dmg_totals[tname] = dmg_totals.get(tname, 0.0) + dmg
        for r, amt in s.total_production_by_resource.items():
            prod_totals[r.name] = prod_totals.get(r.name, 0) + amt

        per_seed.append(
            {
                "seed": s.seed,
                "victory": s.victory,
                "defeat": s.defeat,
                "day": s.day,
                "city_hp": s.city_hp,
                "max_city_hp": s.max_city_hp,
                "peak_population": s.peak_population,
                "hope_total": s.hope.total_hope,
                "hope_unlocked": len(s.hope.unlocked_ids),
                "runes_dropped": s.total_runes_dropped,
                "combat_ether": s.total_ether_from_combat,
                "unpaid_days": sum(1 for l in s.day_logs if l.upkeep_unpaid_buildings),
                "starved_days": sum(1 for l in s.day_logs if l.housing_starved),
                "defeat_reason": s.defeat_reason,
            }
        )

    total_dmg_all = sum(dmg_totals.values())
    avg_damage_by_tower = {
        t: {
            "avg_damage": round(v / n, 2),
            "share_pct": round((100.0 * v / total_dmg_all) if total_dmg_all > 0 else 0.0, 2),
        }
        for t, v in sorted(dmg_totals.items(), key=lambda x: (-x[1], x[0]))
    }

    avg_production = {
        rname: round(val / n, 2)
        for rname, val in sorted(prod_totals.items())
    }

    sorted_reasons = dict(sorted(defeat_reasons.items(), key=lambda x: (-x[1], x[0])))

    ms = meta_state or states[0].meta_state
    return {
        "meta": {
            "name": ms.name,
            "unlocked_count": len(ms.unlocked_ids),
            "variant_choices": dict(sorted(ms.variant_choices.items())),
            "drafted_towers": sorted(ms.drafted_towers),
        },
        "wave_source": states[0].wave_source,
        "total_runs": n,
        "victories": victories,
        "defeats": defeats,
        "win_rate_pct": round(100.0 * victories / n, 2),
        "survival_day": {
            "avg": round(statistics.mean(days), 2),
            "median": round(statistics.median(days), 2),
            "min": min(days),
            "max": max(days),
        },
        "avg_city_hp_all": round(statistics.mean(s.city_hp for s in states), 2),
        "avg_city_hp_on_wins": round(statistics.mean(win_hps), 2) if win_hps else None,
        "hope": {
            "avg_score": round(statistics.mean(hopes), 2),
            "min_score": min(hopes),
            "max_score": max(hopes),
            "avg_milestones_unlocked": round(statistics.mean(hope_counts), 2),
        },
        "economy_and_pop": {
            "avg_peak_pop": round(statistics.mean(pops), 2),
            "avg_runes_dropped": round(statistics.mean(runes), 2),
            "avg_combat_ether": round(statistics.mean(ethers), 2),
            "avg_production_per_run": avg_production,
            "runs_with_unpaid_upkeep": sum(1 for x in unpaid_days_per_run if x > 0),
            "avg_unpaid_days": round(statistics.mean(unpaid_days_per_run), 2),
            "runs_with_starvation": sum(1 for x in starved_days_per_run if x > 0),
            "avg_starved_days": round(statistics.mean(starved_days_per_run), 2),
        },
        "defeat_reasons": sorted_reasons,
        "avg_damage_by_tower_type": avg_damage_by_tower,
        "per_seed": per_seed,
    }


def format_batch_summary(stats: dict[str, Any]) -> str:
    if not stats or stats.get("total_runs", 0) == 0:
        return "No batch runs to report."

    m = stats["meta"]
    sd = stats["survival_day"]
    hp = stats["hope"]
    ec = stats["economy_and_pop"]
    lines: list[str] = [
        "=" * 98,
        f"MONTE CARLO BATCH REPORT | Runs={stats['total_runs']} | Meta={m['name']} "
        f"(Upgrades={m['unlocked_count']}, Drafted={len(m['drafted_towers'])}) | Waves={stats['wave_source']}",
        f"Win Rate: {stats['victories']}/{stats['total_runs']} ({stats['win_rate_pct']:.1f}%) | "
        f"Survival Days: avg={sd['avg']:.2f}, med={sd['median']:.1f}, min={sd['min']}, max={sd['max']}",
        f"CityHP: avg_all={stats['avg_city_hp_all']:.2f}"
        + (f", avg_wins={stats['avg_city_hp_on_wins']:.2f}" if stats["avg_city_hp_on_wins"] is not None else "")
        + f" | PeakPop avg={ec['avg_peak_pop']:.1f} | Runes avg={ec['avg_runes_dropped']:.1f} | "
        + f"CombatEther avg={ec['avg_combat_ether']:.1f}",
        f"Hope: avg={hp['avg_score']:.1f} (min={hp['min_score']}, max={hp['max_score']}), "
        f"milestones avg={hp['avg_milestones_unlocked']:.1f}",
        f"Friction: unpaid_runs={ec['runs_with_unpaid_upkeep']}/{stats['total_runs']} "
        f"(avg {ec['avg_unpaid_days']:.2f}d), starved_runs={ec['runs_with_starvation']}/{stats['total_runs']} "
        f"(avg {ec['avg_starved_days']:.2f}d)",
        "-" * 98,
        "Average Tower Damage Breakdown:",
    ]
    for tname, info in stats["avg_damage_by_tower_type"].items():
        lines.append(f"  - {tname:<32} {info['avg_damage']:12,.1f} ({info['share_pct']:5.1f}%)")

    if stats["defeat_reasons"]:
        lines.append("Defeat Causes:")
        for reason, cnt in stats["defeat_reasons"].items():
            lines.append(f"  - [{cnt:2d}x] {reason}")

    lines.append("-" * 98)
    lines.append(f"{'Seed':>8} | {'Status':>7} | {'Day':>3} | {'HP':>5} | {'Pop':>3} | {'Hope':>5} | {'Runes':>5} | {'Unp':>3} | Notes")
    lines.append("-" * 98)
    for row in stats["per_seed"]:
        status = "VICTORY" if row["victory"] else ("DEFEAT" if row["defeat"] else "STOP")
        hp_s = f"{row['city_hp']:2d}/{row['max_city_hp']:<2d}"
        note = row["defeat_reason"] or "-"
        lines.append(
            f"{row['seed']:>8} | {status:>7} | {row['day']:>3} | {hp_s} | "
            f"{row['peak_population']:>3} | {row['hope_total']:>5} | {row['runes_dropped']:>5} | "
            f"{row['unpaid_days']:>3} | {note}"
        )
    lines.append("=" * 98)
    return "\n".join(lines)


def print_batch_summary(stats: dict[str, Any]) -> None:
    print(format_batch_summary(stats))


def export_batch_json(stats: dict[str, Any], path: str | Path) -> Path:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    p.write_text(json.dumps(stats, indent=2, ensure_ascii=False), encoding="utf-8")
    return p
