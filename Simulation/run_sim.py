"""CLI runner for the DwarfenTowerDefence blind gameplay simulator."""

from __future__ import annotations

import argparse
import csv
import sys
from pathlib import Path

# Ensure Simulation/ is importable regardless of current working directory
_SIM_DIR = Path(__file__).resolve().parent
if str(_SIM_DIR) not in sys.path:
    sys.path.insert(0, str(_SIM_DIR))

from sim.assets_loader import GameData, load_game_data
from sim.engine import run_simulation
from sim.macro_ai import BlindMacroPlanner
from sim.meta import DRAFTABLE_TOWER_POOL, build_meta_state
from sim.reporting import (
    aggregate_batch_results,
    export_batch_json,
    export_day_logs_csv,
    export_single_run_json,
    print_batch_summary,
    print_day_summary,
    render_ascii_map,
)


def _configure_utf8_console() -> None:
    for stream in (sys.stdout, sys.stderr):
        reconf = getattr(stream, "reconfigure", None)
        if callable(reconf):
            try:
                reconf(encoding="utf-8", errors="replace")
            except Exception:
                pass


def _parse_variants(raw_variants: list[str] | None) -> dict[str, str] | None:
    if not raw_variants:
        return None
    out: dict[str, str] = {}
    for item in raw_variants:
        for part in item.split(","):
            part = part.strip()
            if not part:
                continue
            if "=" not in part:
                raise ValueError(f"Invalid --variants entry '{part}'; expected <upgrade_id>=<A|B>.")
            k, v = part.split("=", 1)
            k_clean = k.strip()
            v_clean = v.strip().upper()
            if v_clean not in ("A", "B"):
                raise ValueError(f"Invalid variant choice '{v_clean}' for '{k_clean}'; expected A or B.")
            out[k_clean] = v_clean
    return out


def _resolve_draft_towers(raw_spec: str | None, game_data: GameData) -> list[str] | None:
    if raw_spec is None:
        return None
    txt = raw_spec.strip()
    if not txt or txt.lower() == "none":
        return []
    if txt.lower() == "all":
        return [t for t in DRAFTABLE_TOWER_POOL if t in game_data.towers]

    all_towers = sorted(game_data.towers.keys())
    resolved: list[str] = []
    for token in txt.split(","):
        tok = token.strip()
        if not tok:
            continue
        if tok in game_data.towers:
            resolved.append(tok)
            continue
        # Allow numeric prefix ("4") or substring ("Crucible") match
        matches = [
            t
            for t in all_towers
            if t.lower() == tok.lower()
            or t.startswith(f"{tok}.")
            or tok.lower() in t.lower()
        ]
        if len(matches) == 1:
            resolved.append(matches[0])
        elif len(matches) > 1:
            raise ValueError(f"Ambiguous tower name '{tok}': matches {matches}")
        else:
            raise ValueError(f"Unknown tower '{tok}'. Available: {all_towers}")
    return resolved


def _print_meta_catalog(game_data: GameData) -> None:
    print("=== META UPGRADES ===")
    for uid in sorted(game_data.meta_upgrades.keys()):
        u = game_data.meta_upgrades[uid]
        if u.is_variant_option:
            continue
        prereq = f" req={u.prerequisites}" if u.prerequisites else ""
        if u.has_variants:
            a_up = game_data.meta_upgrades.get(u.variant_a)
            b_up = game_data.meta_upgrades.get(u.variant_b)
            a_txt = f"A:{a_up.effect_type.name}={a_up.effect_value}" if a_up else "A:?"
            b_txt = f"B:{b_up.effect_type.name}={b_up.effect_value}" if b_up else "B:?"
            print(f"  {uid:<32} [VARIANT] {a_txt} | {b_txt}{prereq}")
        else:
            dflt = " [default]" if u.is_unlocked_default else ""
            print(f"  {uid:<32} {u.effect_type.name}={u.effect_value}{dflt}{prereq}")

    print("\n=== TOWERS ===")
    for tname in sorted(game_data.towers.keys()):
        td = game_data.towers[tname]
        draft_tag = " [draftable]" if tname in DRAFTABLE_TOWER_POOL else " [starter]"
        cost = ", ".join(f"{int(c.amount)}{c.resource.name[0]}" for c in td.construction_cost)
        print(f"  {tname:<32} {draft_tag:<12} kind={td.bullet_kind:<10} cost=[{cost}]")

    print(f"\n=== HOPE MILESTONES === ({len(game_data.hope_milestones)} total)")


def _export_batch_per_seed_csv(stats: dict, path: str | Path) -> Path:
    p = Path(path)
    p.parent.mkdir(parents=True, exist_ok=True)
    rows = stats.get("per_seed", [])
    fieldnames = [
        "seed",
        "victory",
        "defeat",
        "day",
        "city_hp",
        "max_city_hp",
        "peak_population",
        "hope_total",
        "hope_unlocked",
        "runes_dropped",
        "combat_ether",
        "unpaid_days",
        "starved_days",
        "defeat_reason",
    ]
    with p.open("w", newline="", encoding="utf-8") as fh:
        writer = csv.DictWriter(fh, fieldnames=fieldnames)
        writer.writeheader()
        for r in rows:
            writer.writerow(r)
    return p


def build_arg_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        description="DwarfenTowerDefence Hybrid C#+Python Blind Gameplay Simulator",
    )
    parser.add_argument(
        "--project-root",
        default=str(_SIM_DIR.parent),
        help="Path to Unity project root (default: parent of Simulation/).",
    )
    parser.add_argument(
        "--seed",
        type=int,
        default=12345,
        help="Map RNG seed (default: 12345). Also starting seed for --batch.",
    )
    parser.add_argument(
        "--seeds",
        type=str,
        default=None,
        help="Comma-separated list of explicit seeds for batch mode (overrides --seed/--batch).",
    )
    parser.add_argument(
        "--batch",
        type=int,
        default=1,
        help="Number of sequential Monte Carlo seeds starting from --seed (default: 1).",
    )
    parser.add_argument(
        "--meta",
        type=str,
        default="full",
        help="Meta profile: none | assets | full | path/to/profile.json | profile_name (default: full).",
    )
    parser.add_argument(
        "--variants",
        nargs="*",
        default=None,
        help="Override meta variant choices: e.g. --variants ulepszenie_wiez_1=A ulepszenie_wiez_2=B",
    )
    parser.add_argument(
        "--draft-towers",
        type=str,
        default=None,
        help="Comma-separated drafted towers (names or numbers like '2,4,7', 'all', or 'none').",
    )
    parser.add_argument(
        "--ignore-building-target-bug",
        action="store_true",
        help="Enable fix for C# TowerGlobalDamage BuildingStatsSO vs EnemyTarget bug (+0.1 vs +10%%).",
    )
    parser.add_argument(
        "--wave-source",
        choices=["predefined50", "scene20"],
        default="predefined50",
        help="Enemy wave schedule source (default: predefined50).",
    )
    parser.add_argument(
        "--max-days",
        type=int,
        default=50,
        help="Maximum days to simulate (default: 50).",
    )
    parser.add_argument(
        "--ascii-map",
        action="store_true",
        help="Print end-of-run ASCII hex map (single-seed mode).",
    )
    parser.add_argument(
        "--json-out",
        type=str,
        default=None,
        help="Path to export JSON report (single run or batch stats).",
    )
    parser.add_argument(
        "--csv-out",
        type=str,
        default=None,
        help="Path to export CSV (day logs for single seed, or per-seed summary for batch).",
    )
    parser.add_argument(
        "--quiet",
        "-q",
        action="store_true",
        help="Suppress console summary tables.",
    )
    parser.add_argument(
        "--verbose",
        "-v",
        action="store_true",
        help="Include end-of-day stockpiles and Hope IDs in single-seed console table.",
    )
    parser.add_argument(
        "--list-meta",
        action="store_true",
        help="List all MetaUpgrade IDs, variant choices, and tower names, then exit.",
    )
    parser.add_argument(
        "--no-c-sharp-check",
        action="store_true",
        help="Skip preflight check for compiled MapExporter.exe binary.",
    )
    return parser


def main(argv: list[str] | None = None) -> int:
    _configure_utf8_console()
    parser = build_arg_parser()
    args = parser.parse_args(argv)

    project_root = Path(args.project_root).resolve()
    if not args.no_c_sharp_check:
        exporter_exe = _SIM_DIR / "MapExporter" / "bin" / "Release" / "net9.0" / "MapExporter.exe"
        if not exporter_exe.exists():
            print(
                f"ERROR: Compiled C# MapExporter.exe not found at {exporter_exe}. "
                "Build Simulation/MapExporter first or check paths.",
                file=sys.stderr,
            )
            return 2

    game_data = load_game_data(project_root)

    if args.list_meta:
        _print_meta_catalog(game_data)
        return 0

    variants_override = _parse_variants(args.variants)
    drafted_override = _resolve_draft_towers(args.draft_towers, game_data)
    ignore_bug_override = True if args.ignore_building_target_bug else None

    meta_state = build_meta_state(
        profile_spec=args.meta,
        game_data=game_data,
        variants_override=variants_override,
        drafted_towers_override=drafted_override,
        ignore_building_target_bug=ignore_bug_override,
    )
    for w in meta_state.warnings:
        print(f"META WARNING: {w}", file=sys.stderr)

    if args.seeds:
        seed_list = [int(x.strip()) for x in args.seeds.split(",") if x.strip()]
    elif args.batch > 1:
        seed_list = list(range(args.seed, args.seed + args.batch))
    else:
        seed_list = [args.seed]

    if len(seed_list) == 1 and not args.seeds and args.batch <= 1:
        seed = seed_list[0]
        planner = BlindMacroPlanner()
        state = run_simulation(
            seed=seed,
            game_data=game_data,
            meta_state=meta_state,
            planner_fn=planner,
            max_days=args.max_days,
            wave_source=args.wave_source,
        )
        if not args.quiet:
            print_day_summary(state, verbose=args.verbose)
            if args.ascii_map:
                print()
                print(render_ascii_map(state.world, state.placed_buildings))
        if args.json_out:
            jp = export_single_run_json(state, args.json_out)
            if not args.quiet:
                print(f"JSON report written to: {jp}")
        if args.csv_out:
            cp = export_day_logs_csv(state, args.csv_out)
            if not args.quiet:
                print(f"CSV day log written to: {cp}")
        return 0

    # Batch / Monte Carlo mode
    states = []
    for idx, seed in enumerate(seed_list, start=1):
        planner = BlindMacroPlanner()
        st = run_simulation(
            seed=seed,
            game_data=game_data,
            meta_state=meta_state,
            planner_fn=planner,
            max_days=args.max_days,
            wave_source=args.wave_source,
        )
        states.append(st)
        if not args.quiet:
            status = "VICTORY" if st.victory else ("DEFEAT" if st.defeat else "STOP")
            print(
                f"[{idx}/{len(seed_list)}] Seed={seed}: {status} Day={st.day} "
                f"HP={st.city_hp}/{st.max_city_hp} Hope={st.hope.total_hope} Pop={st.peak_population}",
                flush=True,
            )

    batch_stats = aggregate_batch_results(states, meta_state=meta_state)
    if not args.quiet:
        print_batch_summary(batch_stats)
    if args.json_out:
        jp = export_batch_json(batch_stats, args.json_out)
        if not args.quiet:
            print(f"Batch JSON written to: {jp}")
    if args.csv_out:
        cp = _export_batch_per_seed_csv(batch_stats, args.csv_out)
        if not args.quiet:
            print(f"Batch CSV written to: {cp}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
