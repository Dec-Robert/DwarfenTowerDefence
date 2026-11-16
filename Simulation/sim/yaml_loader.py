from __future__ import annotations

import re
import struct
from pathlib import Path
from typing import Any

_UNICODE_ESCAPE_RE = re.compile(r"\\u([0-9a-fA-F]{4})|\\x([0-9a-fA-F]{2})")
_HEX_BYTES_RE = re.compile(r"^[0-9a-f]+$")
_DOC_HEADER_RE = re.compile(r"^---\s*!u!(\d+)\s+&(-?\d+)")
_INT_RE = re.compile(r"^-?\d+$")
_FLOAT_RE = re.compile(r"^-?(?:\d+\.\d*|\.\d+|\d+[eE][+-]?\d+|\d+\.\d+[eE][+-]?\d+)$")


def _unescape_yaml_string(s: str) -> str:
    def _repl(m: re.Match[str]) -> str:
        hex_part = m.group(1) or m.group(2)
        return chr(int(hex_part, 16))

    s = s.replace(r"\r\n", "\n").replace(r"\n", "\n").replace(r"\t", "\t")
    s = s.replace(r"\"", '"').replace(r"\\", "\\")
    return _UNICODE_ESCAPE_RE.sub(_repl, s)


def _split_flow_items(inner: str) -> list[str]:
    items: list[str] = []
    cur: list[str] = []
    depth = 0
    quote: str | None = None
    i = 0
    while i < len(inner):
        ch = inner[i]
        if quote is not None:
            cur.append(ch)
            if ch == "\\" and i + 1 < len(inner):
                i += 1
                cur.append(inner[i])
            elif ch == quote:
                quote = None
        else:
            if ch in ('"', "'"):
                quote = ch
                cur.append(ch)
            elif ch in ("{", "["):
                depth += 1
                cur.append(ch)
            elif ch in ("}", "]"):
                depth -= 1
                cur.append(ch)
            elif ch == "," and depth == 0:
                part = "".join(cur).strip()
                if part:
                    items.append(part)
                cur = []
            else:
                cur.append(ch)
        i += 1
    tail = "".join(cur).strip()
    if tail:
        items.append(tail)
    return items


def _find_unquoted_colon(line: str) -> int:
    quote: str | None = None
    depth = 0
    i = 0
    while i < len(line):
        ch = line[i]
        if quote is not None:
            if ch == "\\" and i + 1 < len(line):
                i += 1
            elif ch == quote:
                quote = None
        else:
            if ch in ('"', "'"):
                quote = ch
            elif ch in ("{", "["):
                depth += 1
            elif ch in ("}", "]"):
                depth -= 1
            elif ch == ":" and depth == 0:
                if i + 1 == len(line) or line[i + 1] == " ":
                    return i
        i += 1
    return -1


def parse_scalar(raw: str) -> Any:
    raw = raw.strip()
    if raw == "" or raw == "null" or raw == "~":
        return None
    if raw == "[]":
        return []
    if raw == "{}":
        return {}
    if raw.startswith("{") and raw.endswith("}"):
        out: dict[str, Any] = {}
        for item in _split_flow_items(raw[1:-1]):
            c = _find_unquoted_colon(item)
            if c != -1:
                k = item[:c].strip().strip('"').strip("'")
                v = parse_scalar(item[c + 1 :])
                out[k] = v
        return out
    if raw.startswith("[") and raw.endswith("]"):
        return [parse_scalar(x) for x in _split_flow_items(raw[1:-1])]
    if len(raw) >= 2 and raw[0] == '"' and raw[-1] == '"':
        return _unescape_yaml_string(raw[1:-1])
    if len(raw) >= 2 and raw[0] == "'" and raw[-1] == "'":
        return raw[1:-1].replace("''", "'")
    if _INT_RE.match(raw):
        # Preserve zero-padded hex byte arrays like '0000000001000000' or '01000000' as str
        if len(raw) >= 8 and raw.startswith("0"):
            return raw
        return int(raw)
    if _FLOAT_RE.match(raw):
        return float(raw)
    return _unescape_yaml_string(raw)


def _needs_continuation(val: str) -> bool:
    if not val:
        return False
    if val.startswith('"') and not (len(val) >= 2 and val.endswith('"') and not val.endswith('\\"')):
        return True
    if val.startswith("'") and not (len(val) >= 2 and val.endswith("'")):
        return True
    if val.startswith("{") and not val.endswith("}"):
        return True
    if val.startswith("[") and not val.endswith("]"):
        return True
    # Unity also wraps plain unquoted strings across lines at indent > key_indent
    return not (val.startswith("{") or val.startswith("[") or val.startswith('"') or val.startswith("'"))


def _preprocess_lines(text: str) -> list[tuple[int, str]]:
    out: list[tuple[int, str]] = []
    for raw_line in text.splitlines():
        if not raw_line.strip() or raw_line.startswith("%"):
            continue
        stripped = raw_line.lstrip(" ")
        if stripped.startswith("#"):
            continue
        indent = len(raw_line) - len(stripped)
        out.append((indent, stripped))
    return out


def _parse_block(lines: list[tuple[int, str]], idx: int, min_indent: int) -> tuple[Any, int]:
    if idx >= len(lines):
        return {}, idx

    first_indent, first_text = lines[idx]
    if first_indent < min_indent:
        return {}, idx

    if first_text.startswith("- ") or first_text == "-":
        arr: list[Any] = []
        list_indent = first_indent
        while idx < len(lines):
            ind, txt = lines[idx]
            if ind < list_indent or not (txt.startswith("- ") or txt == "-"):
                break
            after = txt[2:].strip() if txt.startswith("- ") else ""
            idx += 1
            if after == "":
                child, idx = _parse_block(lines, idx, list_indent + 1)
                arr.append(child)
                continue
            colon = _find_unquoted_colon(after)
            if colon != -1 and not after.startswith("{") and not after.startswith("["):
                k = after[:colon].strip()
                v_raw = after[colon + 1 :].strip()
                item_dict: dict[str, Any] = {}
                kv_indent = list_indent + 2
                if v_raw == "":
                    # Could be empty scalar OR nested block on following lines with ind > kv_indent
                    if idx < len(lines) and lines[idx][0] > kv_indent:
                        v, idx = _parse_block(lines, idx, kv_indent + 1)
                    else:
                        v = None
                else:
                    # Handle line-wrapped continuation of scalar inside `- k: val`
                    while idx < len(lines):
                        n_ind, n_txt = lines[idx]
                        if n_ind <= kv_indent or n_txt.startswith("- ") or _find_unquoted_colon(n_txt) != -1:
                            break
                        if _needs_continuation(v_raw):
                            v_raw = f"{v_raw} {n_txt}"
                            idx += 1
                        else:
                            break
                    v = parse_scalar(v_raw)
                item_dict[k] = v
                # Parse remaining sibling keys in this list-item mapping at indent >= list_indent + 1 (usually list_indent + 2)
                if idx < len(lines) and lines[idx][0] > list_indent and not lines[idx][1].startswith("- "):
                    more, idx = _parse_block(lines, idx, list_indent + 1)
                    if isinstance(more, dict):
                        item_dict.update(more)
                arr.append(item_dict)
            else:
                v_raw = after
                while idx < len(lines):
                    n_ind, n_txt = lines[idx]
                    if n_ind <= list_indent or n_txt.startswith("- ") or _find_unquoted_colon(n_txt) != -1:
                        break
                    if _needs_continuation(v_raw):
                        v_raw = f"{v_raw} {n_txt}"
                        idx += 1
                    else:
                        break
                arr.append(parse_scalar(v_raw))
        return arr, idx

    mapping: dict[str, Any] = {}
    map_indent = first_indent
    while idx < len(lines):
        ind, txt = lines[idx]
        if ind < map_indent or txt.startswith("- "):
            break
        if ind > map_indent:
            # Unexpected deeper indentation; skip gracefully
            idx += 1
            continue
        colon = _find_unquoted_colon(txt)
        if colon == -1:
            idx += 1
            continue
        k = txt[:colon].strip()
        v_raw = txt[colon + 1 :].strip()
        idx += 1
        if v_raw == "":
            if idx < len(lines) and (lines[idx][0] > map_indent or (lines[idx][0] == map_indent and lines[idx][1].startswith("- "))):
                v, idx = _parse_block(lines, idx, map_indent)
            else:
                v = None
        else:
            while idx < len(lines):
                n_ind, n_txt = lines[idx]
                if n_ind <= map_indent or n_txt.startswith("- "):
                    break
                # Only append continuation line if current value isn't already a complete flow map/list or quoted string
                # and the continuation line doesn't itself start a nested key unless inside an open quote/brace.
                in_quote_or_brace = (
                    (v_raw.startswith('"') and not (len(v_raw) >= 2 and v_raw.endswith('"')))
                    or (v_raw.startswith("{") and not v_raw.endswith("}"))
                    or (v_raw.startswith("[") and not v_raw.endswith("]"))
                )
                if not in_quote_or_brace and _find_unquoted_colon(n_txt) != -1:
                    break
                if _needs_continuation(v_raw) or in_quote_or_brace:
                    sep = "" if (v_raw.endswith('"') is False and in_quote_or_brace and n_txt.startswith("\\")) else " "
                    v_raw = f"{v_raw}{sep}{n_txt}"
                    idx += 1
                else:
                    break
            v = parse_scalar(v_raw)
        mapping[k] = v
    return mapping, idx


def parse_unity_yaml_documents(path: Path) -> list[dict[str, Any]]:
    """Parse all documents in a Unity .asset / .prefab / .unity file."""
    text = path.read_text(encoding="utf-8", errors="replace")
    docs: list[dict[str, Any]] = []
    cur_header: tuple[int, int] | None = None
    cur_lines: list[str] = []

    def _flush() -> None:
        if cur_header is None:
            return
        class_id, file_id = cur_header
        lines = _preprocess_lines("\n".join(cur_lines))
        parsed, _ = _parse_block(lines, 0, 0)
        docs.append({"class_id": class_id, "file_id": file_id, "body": parsed})

    for line in text.splitlines():
        m = _DOC_HEADER_RE.match(line)
        if m:
            _flush()
            cur_header = (int(m.group(1)), int(m.group(2)))
            cur_lines = []
        elif cur_header is not None:
            cur_lines.append(line)
    _flush()
    return docs


def parse_unity_asset(path: Path) -> dict[str, Any]:
    """Parse a single ScriptableObject .asset file, returning the MonoBehaviour dict."""
    docs = parse_unity_yaml_documents(path)
    for d in docs:
        body = d["body"]
        if isinstance(body, dict) and "MonoBehaviour" in body:
            return body["MonoBehaviour"]
    raise ValueError(f"No MonoBehaviour document found in {path}")


def decode_hex_int_list(val: Any) -> list[int]:
    """Decode Unity little-endian hex-encoded int32 list (or normal list / None / int)."""
    if val is None or val == "" or val == []:
        return []
    if isinstance(val, list):
        return [int(x) for x in val]
    if isinstance(val, int):
        s = str(val)
        pad = (8 - (len(s) % 8)) % 8
        s = ("0" * pad) + s
        raw = bytes.fromhex(s)
        return list(struct.unpack("<" + "i" * (len(raw) // 4), raw))
    if isinstance(val, str):
        s = val.strip().lower()
        if not s:
            return []
        if _HEX_BYTES_RE.match(s) and len(s) % 8 == 0:
            raw = bytes.fromhex(s)
            return list(struct.unpack("<" + "i" * (len(raw) // 4), raw))
    raise ValueError(f"Cannot decode int list from {val!r}")


def parse_meta_guid(meta_path: Path) -> str | None:
    text = meta_path.read_text(encoding="utf-8", errors="replace")
    m = re.search(r"^guid:\s*([0-9a-fA-F]{32})\s*$", text, re.MULTILINE)
    return m.group(1).lower() if m else None


def build_guid_index(assets_root: Path) -> tuple[dict[str, Path], dict[Path, str]]:
    """Index all *.meta GUIDs under assets_root."""
    guid_to_path: dict[str, Path] = {}
    path_to_guid: dict[Path, str] = {}
    for meta_path in assets_root.rglob("*.meta"):
        guid = parse_meta_guid(meta_path)
        if not guid:
            continue
        asset_path = meta_path.with_suffix("")
        if asset_path.exists():
            res = asset_path.resolve()
            guid_to_path[guid] = res
            path_to_guid[res] = guid
    return guid_to_path, path_to_guid
