#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""One-click migration of Unity DemoNpcDialogueProfile inline dialogue.

Place this file at Assets/Data/Dialogues/dialogue_localization_migrator.py and run:
    python Assets/Data/Dialogues/dialogue_localization_migrator.py --dry-run
    python Assets/Data/Dialogues/dialogue_localization_migrator.py

No third-party packages are required. The root Assets/Resources/transitions/vn.json is read-only.
New keys are written to sibling vn.json, vn_2.json, vn_3.json, and later patches for manual merging.
A true localization flag preserves its key; a false flag converts raw text into a key. Run --dry-run first.
Backups of changed existing files are written
outside Assets to .dialogue_migration_backup/<timestamp>/.
"""
from __future__ import annotations

import argparse
import datetime as _datetime
import json
import os
import re
import shutil
import sys
import unicodedata
import uuid
from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Dict, List, Optional, Tuple

DOC_START_RE = re.compile(r"(?m)^--- !u!(\d+) &(-?\d+)\s*$")
GUID_RE = re.compile(r"(?m)^guid:\s*([0-9a-fA-F]{32})\s*$")
FIELD_RE = re.compile(r"^(?P<indent>\s*)(?P<dash>-\s*)?(?P<key>[A-Za-z_][A-Za-z0-9_]*):(?:\s*(?P<value>.*))?$")
KEYISH_RE = re.compile(r"^[A-Z][A-Z0-9]*(?:_[A-Z0-9]+)+$")
ZONE_RE = re.compile(r"(?:^|[^A-Z0-9])(?:ZONE|Z)[ _-]*([1-9][0-9]*)(?:$|[^0-9])", re.IGNORECASE)

PROFILE_FIELDS = {"interactionRange", "dialogueAsset", "dialogue"}
CHOICE_STRING_FIELDS = (
    "sessionId", "choiceId", "label", "requiredFlag", "requiredItemName",
    "resultText", "resultSpeakerId",
)
CHOICE_INT_FIELDS = {
    "requiredItemAmount": 1, "nextNodeIndex": -1, "ltDelta": 0, "qtDelta": 0,
}
NODE_FIELDS = ("speakerId", "text", "nextNodeIndex")


@dataclass
class UnityDoc:
    type_id: int
    file_id: str
    text: str


@dataclass
class ChoiceRecord:
    values: Dict[str, Any]
    start: int
    end: int
    indent: int
    field_lines: Dict[str, int] = field(default_factory=dict)


@dataclass
class NodeRecord:
    values: Dict[str, Any]
    start: int
    end: int
    indent: int
    field_lines: Dict[str, int] = field(default_factory=dict)
    choices: List[ChoiceRecord] = field(default_factory=list)


@dataclass
class ProfileRecord:
    path: Path
    doc_index: int
    object_id: str
    game_object_id: str
    game_object_name: str
    lines: List[str]
    dialogue_property_line: int
    dialogue_end_line: int
    nodes: List[NodeRecord]
    has_assigned_asset: bool
    zone: str
    prefix: str = ""
    asset_path: Optional[Path] = None
    asset_guid: Optional[str] = None


class MigrationError(RuntimeError):
    pass


def scalar_value(raw: Optional[str]) -> Any:
    """Decode Unity YAML scalar values, including Unity's \\xNN and wrapped strings."""
    if raw is None:
        return ""
    value = raw.strip()
    if not value or value in ("~", "null", "Null", "NULL"):
        return ""
    if value.startswith('"') and _double_quoted_scalar_complete(value):
        return _decode_yaml_double_quoted(value)
    if value.startswith("'") and value.endswith("'") and len(value) >= 2:
        return value[1:-1].replace("''", "'")
    if value in ("0", "1"):
        return int(value)
    if re.fullmatch(r"-?\d+", value):
        try:
            return int(value)
        except ValueError:
            pass
    if value in ("true", "True", "TRUE"):
        return True
    if value in ("false", "False", "FALSE"):
        return False
    return value


def _double_quoted_scalar_complete(value: str) -> bool:
    if not value.startswith('"'):
        return True
    escaped = False
    for character in value[1:]:
        if escaped:
            escaped = False
        elif character == "\\":
            escaped = True
        elif character == '"':
            return True
    return False


def _decode_yaml_double_quoted(value: str) -> str:
    body = value[1:-1]
    result: List[str] = []
    index = 0
    escapes = {
        "0": chr(0), "a": chr(7), "b": chr(8), "t": chr(9),
        "n": chr(10), "v": chr(11), "f": chr(12), "r": chr(13),
        "e": chr(27), " ": " ", '"': '"', "/": "/", "\\": "\\",
        "N": chr(0x85), "_": chr(0xA0), "L": chr(0x2028), "P": chr(0x2029),
    }
    while index < len(body):
        character = body[index]
        if character != "\\":
            result.append(character)
            index += 1
            continue
        index += 1
        if index >= len(body):
            result.append("\\")
            break
        escape = body[index]
        if escape in escapes:
            result.append(escapes[escape])
            index += 1
            continue
        digit_count = {"x": 2, "u": 4, "U": 8}.get(escape)
        if digit_count is not None:
            digits = body[index + 1:index + 1 + digit_count]
            if len(digits) == digit_count and re.fullmatch(r"[0-9A-Fa-f]+", digits):
                result.append(chr(int(digits, 16)))
                index += digit_count + 1
                continue
        # Preserve unknown escapes literally instead of silently losing a backslash.
        result.extend(("\\", escape))
        index += 1
    decoded = "".join(result)
    try:
        return decoded.encode("utf-16", "surrogatepass").decode("utf-16")
    except UnicodeError:
        return decoded


def quote_yaml_string(value: Any) -> str:
    # JSON double-quoted strings are valid YAML double-quoted scalar strings.
    return json.dumps("" if value is None else str(value), ensure_ascii=False)


def parse_field_line(line: str) -> Optional[Tuple[int, bool, str, str]]:
    match = FIELD_RE.match(line.rstrip("\r\n"))
    if not match:
        return None
    indent = len(match.group("indent"))
    return indent, bool(match.group("dash")), match.group("key"), match.group("value") or ""


def _collect_field_raw_value(lines: List[str], line_index: int) -> Tuple[str, int]:
    parsed = parse_field_line(lines[line_index])
    if not parsed:
        return "", line_index + 1
    raw = parsed[3].strip()
    end = line_index + 1
    if raw.startswith('"') and not _double_quoted_scalar_complete(raw):
        while end < len(lines) and not _double_quoted_scalar_complete(raw):
            continuation = lines[end].strip()
            trailing_slashes = len(raw) - len(raw.rstrip("\\"))
            if trailing_slashes % 2:
                raw = raw[:-1] + continuation
            else:
                raw += " " + continuation
            end += 1
        if not _double_quoted_scalar_complete(raw):
            raise MigrationError(f"Chuỗi YAML nhiều dòng chưa đóng tại dòng {line_index + 1}.")
    return raw, end


def read_field_value(lines: List[str], line_index: int) -> Any:
    raw, _ = _collect_field_raw_value(lines, line_index)
    return scalar_value(raw)


def replace_field_line(lines: List[str], line_index: int, key: str, value: Any, string_value: bool = False) -> None:
    old = lines[line_index]
    match = FIELD_RE.match(old)
    if not match or match.group("key") != key:
        raise MigrationError(f"Không thể cập nhật trường {key} tại dòng {line_index + 1}: {old!r}")
    _, end = _collect_field_raw_value(lines, line_index)
    prefix = match.group("indent") + (match.group("dash") or "") + key + ": "
    new_value = quote_yaml_string(value) if string_value else str(value)
    lines[line_index] = prefix + new_value
    # Keep blank placeholders so indexes collected for sibling fields remain valid.
    for continuation_index in range(line_index + 1, end):
        lines[continuation_index] = ""

def find_direct_field(lines: List[str], start: int, end: int, key: str, indent: Optional[int] = None) -> Optional[int]:
    for index in range(start, min(end, len(lines))):
        parsed = parse_field_line(lines[index])
        if not parsed:
            continue
        line_indent, is_list_item, line_key, _ = parsed
        if is_list_item or line_key != key:
            continue
        if indent is not None and line_indent != indent:
            continue
        return index
    return None


def field_map_for_item(lines: List[str], item_start: int, item_end: int, item_indent: int,
                       item_field: str, direct_indent: int) -> Dict[str, int]:
    result: Dict[str, int] = {}
    start_parsed = parse_field_line(lines[item_start])
    if start_parsed and start_parsed[2] == item_field:
        result[item_field] = item_start
    for i in range(item_start + 1, item_end):
        parsed = parse_field_line(lines[i])
        if not parsed:
            continue
        indent, is_list, key, _ = parsed
        if not is_list and indent == direct_indent:
            result[key] = i
    return result


def _find_array_bounds(lines: List[str], property_index: int) -> Tuple[int, int]:
    parsed = parse_field_line(lines[property_index])
    if not parsed:
        return property_index + 1, len(lines)
    property_indent = parsed[0]
    raw_value = parsed[3].strip()
    if raw_value == "[]":
        return property_index + 1, property_index + 1
    end = len(lines)
    for i in range(property_index + 1, len(lines)):
        if not lines[i].strip():
            continue
        item = parse_field_line(lines[i])
        if not item:
            continue
        indent, is_list, _, _ = item
        if indent <= property_indent and not is_list:
            end = i
            break
    return property_index + 1, end


def parse_nodes(lines: List[str], array_field: str) -> Tuple[Optional[int], int, List[NodeRecord]]:
    prop_idx = find_direct_field(lines, 0, len(lines), array_field, indent=2)
    if prop_idx is None:
        # Some Unity YAML variants may indent serialized fields differently.
        prop_idx = find_direct_field(lines, 0, len(lines), array_field)
    if prop_idx is None:
        return None, len(lines), []
    _, end_idx = _find_array_bounds(lines, prop_idx)
    if end_idx == prop_idx + 1 and parse_field_line(lines[prop_idx]) and parse_field_line(lines[prop_idx])[3].strip() == "[]":
        return prop_idx, end_idx, []

    node_field_names = {"speakerId", "textIsLocalizationKey", "text", "nextNodeIndex", "choices"}
    candidates: List[Tuple[int, int]] = []
    for i in range(prop_idx + 1, end_idx):
        parsed = parse_field_line(lines[i])
        if parsed and parsed[1] and parsed[2] in node_field_names:
            candidates.append((parsed[0], i))
    if not candidates:
        return prop_idx, end_idx, []
    node_indent = min(indent for indent, _ in candidates)
    starts = [line_index for indent, line_index in candidates if indent == node_indent]
    nodes: List[NodeRecord] = []

    for pos, node_start in enumerate(starts):
        node_end = starts[pos + 1] if pos + 1 < len(starts) else end_idx
        for j in range(node_start + 1, node_end):
            if not lines[j].strip():
                continue
            parsed = parse_field_line(lines[j])
            if parsed and parsed[0] < node_indent and not parsed[1]:
                node_end = j
                break
        start_parsed = parse_field_line(lines[node_start])
        start_field = start_parsed[2] if start_parsed else "speakerId"
        node_fields = field_map_for_item(lines, node_start, node_end, node_indent, start_field, node_indent + 2)
        values: Dict[str, Any] = {
            "speakerId": "", "textIsLocalizationKey": False, "text": "",
            "nextNodeIndex": -1,
        }
        for key in ("speakerId", "textIsLocalizationKey", "text", "nextNodeIndex"):
            if key in node_fields:
                values[key] = read_field_value(lines, node_fields[key])
        choices_idx = node_fields.get("choices")
        choices: List[ChoiceRecord] = []
        if choices_idx is not None:
            choices = parse_choices(lines, choices_idx, node_end, node_indent + 2)
        nodes.append(NodeRecord(values, node_start, node_end, node_indent, node_fields, choices))
    return prop_idx, end_idx, nodes


def parse_choices(lines: List[str], choices_prop_idx: int, node_end: int, property_indent: int) -> List[ChoiceRecord]:
    raw = (parse_field_line(lines[choices_prop_idx]) or (0, False, "", ""))[3].strip()
    if raw == "[]":
        return []
    choice_field_names = set(CHOICE_STRING_FIELDS) | set(CHOICE_INT_FIELDS) | {"labelIsLocalizationKey", "resultTextIsLocalizationKey"}
    candidates: List[Tuple[int, int]] = []
    for i in range(choices_prop_idx + 1, node_end):
        parsed = parse_field_line(lines[i])
        if parsed and parsed[1] and parsed[2] in choice_field_names and parsed[0] >= property_indent:
            candidates.append((parsed[0], i))
    if not candidates:
        return []
    choice_indent = min(indent for indent, _ in candidates)
    starts = [line_index for indent, line_index in candidates if indent == choice_indent]
    choices: List[ChoiceRecord] = []
    for pos, choice_start in enumerate(starts):
        choice_end = starts[pos + 1] if pos + 1 < len(starts) else node_end
        for j in range(choice_start + 1, choice_end):
            if not lines[j].strip():
                continue
            parsed = parse_field_line(lines[j])
            if parsed and parsed[0] < choice_indent and not parsed[1]:
                choice_end = j
                break
        start_parsed = parse_field_line(lines[choice_start])
        start_field = start_parsed[2] if start_parsed else "sessionId"
        fields = field_map_for_item(lines, choice_start, choice_end, choice_indent, start_field, choice_indent + 2)
        values: Dict[str, Any] = {
            "sessionId": "", "choiceId": "", "labelIsLocalizationKey": False, "label": "",
            "requiredFlag": "", "requiredItemName": "", "requiredItemAmount": 1,
            "resultText": "", "resultTextIsLocalizationKey": False, "resultSpeakerId": "",
            "nextNodeIndex": -1, "ltDelta": 0, "qtDelta": 0,
        }
        for key in choice_field_names:
            idx = fields.get(key)
            if idx is not None:
                values[key] = read_field_value(lines, idx)
        choices.append(ChoiceRecord(values, choice_start, choice_end, choice_indent, fields))
    return choices

def docs_in_text(text: str) -> Tuple[str, List[UnityDoc]]:
    matches = list(DOC_START_RE.finditer(text))
    if not matches:
        return text, []
    prefix = text[:matches[0].start()]
    docs: List[UnityDoc] = []
    for i, match in enumerate(matches):
        end = matches[i + 1].start() if i + 1 < len(matches) else len(text)
        docs.append(UnityDoc(int(match.group(1)), match.group(2), text[match.start():end]))
    return prefix, docs


def reassemble_docs(prefix: str, docs: List[UnityDoc]) -> str:
    return prefix + "".join(doc.text for doc in docs)


def get_guid_from_meta(meta_path: Path) -> Optional[str]:
    if not meta_path.exists():
        return None
    match = GUID_RE.search(meta_path.read_text(encoding="utf-8", errors="replace"))
    return match.group(1).lower() if match else None


def should_skip_asset_path(path: Path, assets_root: Path) -> bool:
    ignored_dirs = {"_recovery", "foraiagentedit"}
    try:
        relative_parts = path.relative_to(assets_root).parts
    except ValueError:
        return False
    return any(part.casefold() in ignored_dirs for part in relative_parts)

def discover_class_guid(assets_root: Path, class_name: str) -> Optional[str]:
    matches: List[Tuple[Path, str]] = []
    for cs_path in assets_root.rglob("*.cs"):
        if should_skip_asset_path(cs_path, assets_root):
            continue
        try:
            source = cs_path.read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        if not re.search(r"\bclass\s+" + re.escape(class_name) + r"\b", source):
            continue
        guid = get_guid_from_meta(Path(str(cs_path) + ".meta"))
        if guid:
            matches.append((cs_path, guid))
    if not matches:
        return None
    if len(matches) > 1:
        matches.sort(key=lambda item: (len(item[0].parts), str(item[0]).lower()))
        print(f"[WARN] Tìm thấy nhiều file khai báo {class_name}; dùng {matches[0][0]}")
    return matches[0][1]


def read_file(path: Path) -> str:
    return path.read_text(encoding="utf-8-sig", errors="replace")


def field_value(lines: List[str], key: str, indent: Optional[int] = 2, start: int = 0, end: Optional[int] = None) -> Any:
    index = find_direct_field(lines, start, len(lines) if end is None else end, key, indent=indent)
    if index is None:
        return ""
    parsed = parse_field_line(lines[index])
    return read_field_value(lines, index) if parsed else ""


def game_object_names(docs: List[UnityDoc]) -> Dict[str, str]:
    names: Dict[str, str] = {}
    for doc in docs:
        if doc.type_id != 1:
            continue
        lines = doc.text.splitlines()
        value = field_value(lines, "m_Name", indent=2)
        if value:
            names[doc.file_id] = str(value)
    return names


def script_guid_from_doc(doc: UnityDoc) -> str:
    match = re.search(r"(?m)^\s*m_Script:\s*\{[^\n]*?guid:\s*([0-9a-fA-F]{32})", doc.text)
    return match.group(1).lower() if match else ""


def game_object_id_from_doc(doc: UnityDoc) -> str:
    match = re.search(r"(?m)^\s*m_GameObject:\s*\{\s*fileID:\s*(-?\d+)", doc.text)
    return match.group(1) if match else ""


def normalize_slug(value: str, fallback: str = "DIALOGUE") -> str:
    decomposed = unicodedata.normalize("NFKD", value)
    ascii_value = decomposed.encode("ascii", "ignore").decode("ascii")
    slug = re.sub(r"[^A-Za-z0-9]+", "_", ascii_value).strip("_").upper()
    return slug or fallback


def zone_prefix(path_text: str) -> str:
    match = ZONE_RE.search(path_text.upper().replace("\\", "/"))
    if match:
        return f"Z{int(match.group(1))}"
    return ""


def namespace_for_path(project_root: Path, path: Path) -> str:
    zone = zone_prefix(str(path.relative_to(project_root)))
    if zone:
        return zone
    stem = normalize_slug(path.stem, "SCENE")
    return stem[:36]


def keyish(value: str) -> bool:
    return bool(KEYISH_RE.fullmatch(value.strip()))


class TranslationStore:
    def __init__(self, values: Dict[str, Any]):
        self.values = values
        self.added_values: Dict[str, str] = {}

    @property
    def added(self) -> int:
        return len(self.added_values)

    def localize(self, value: Any, currently_key: bool, base_key: str, warnings: List[str], context: str) -> Tuple[str, bool]:
        source = "" if value is None else str(value)
        if not source.strip():
            return source, bool(currently_key)

        # The serialized checkbox is authoritative: a key is never converted into another key.
        if currently_key:
            if source not in self.values:
                warnings.append(f"{context}: key '{source}' không có trong vn.json gốc hoặc các patch; giữ nguyên và không tạo key mới.")
            return source, True

        # An unchecked field is raw text, even when its text happens to look like a key.
        candidate = base_key
        suffix = 2
        while candidate in self.values and self.values[candidate] != source:
            candidate = f"{base_key}_{suffix:02d}"
            suffix += 1
        if candidate not in self.values:
            self.values[candidate] = source
            self.added_values[candidate] = source
        return candidate, True

def profile_has_text(nodes: List[NodeRecord]) -> bool:
    for node in nodes:
        if str(node.values.get("text", "")).strip():
            return True
        for choice in node.choices:
            if str(choice.values.get("label", "")).strip() or str(choice.values.get("resultText", "")).strip():
                return True
    return False


def find_project_root(script_path: Path) -> Path:
    # Prefer the nearest directory that has both Unity project markers.
    for parent in [script_path.parent, *script_path.parents]:
        if (parent / "Assets").is_dir() and (parent / "ProjectSettings").is_dir():
            return parent
    # Fallback still requires actual project content, avoiding nested ForAIAgentEdit/Assets folders.
    for parent in [script_path.parent, *script_path.parents]:
        if (parent / "Assets" / "Scripts").is_dir():
            return parent
    raise MigrationError("Không tìm thấy thư mục gốc Unity. Hãy đặt script bên trong project Assets.")


def find_patch_files(folder: Path) -> List[Path]:
    patch_pattern = re.compile(r"^vn(?:_(\d+))?\.json$", re.IGNORECASE)
    paths = [path for path in folder.glob("vn*.json") if patch_pattern.fullmatch(path.name)]

    def patch_number(path: Path) -> int:
        match = patch_pattern.fullmatch(path.name)
        return int(match.group(1)) if match and match.group(1) else 1

    return sorted(paths, key=lambda path: (patch_number(path), path.name.casefold()))


def next_patch_path(folder: Path, patch_sources: Dict[Path, Dict[str, Any]]) -> Path:
    primary = folder / "vn.json"
    if not patch_sources:
        return primary
    if len(patch_sources) == 1 and primary in patch_sources and not patch_sources[primary]:
        return primary
    numbers = [1]
    for path in patch_sources:
        match = re.fullmatch(r"vn_(\d+)\.json", path.name, re.IGNORECASE)
        if match:
            numbers.append(int(match.group(1)))
    return folder / f"vn_{max(numbers) + 1}.json"


def find_vn_json(project_root: Path, excluded_paths: Optional[List[Path]] = None) -> Path:
    excluded = {path.resolve() for path in (excluded_paths or [])}
    candidates = [
        project_root / "Assets/Resources/transitions/vn.json",
        project_root / "Assets/transitions/vn.json",
        project_root / "Assets/Resources/Transitions/vn.json",
    ]
    for candidate in candidates:
        if candidate.is_file() and candidate.resolve() not in excluded:
            return candidate
    found = [path for path in (project_root / "Assets").rglob("vn.json")
             if path.is_file() and path.resolve() not in excluded]
    if found:
        found.sort(key=lambda path: ("transitions" not in str(path).lower(),
                                     "resources" not in str(path).lower(), len(path.parts), str(path).lower()))
        return found[0]
    raise MigrationError("Không tìm thấy vn.json gốc trong Assets. Không tạo hoặc ghi đè file localization gốc.")

def new_asset_yaml(script_guid: str, name: str, npc_name: str, nodes: List[NodeRecord]) -> str:
    lines = [
        "%YAML 1.1",
        "%TAG !u! tag:unity3d.com,2011:",
        "--- !u!114 &11400000",
        "MonoBehaviour:",
        "  m_ObjectHideFlags: 0",
        "  m_CorrespondingSourceObject: {fileID: 0}",
        "  m_PrefabInstance: {fileID: 0}",
        "  m_PrefabAsset: {fileID: 0}",
        "  m_GameObject: {fileID: 0}",
        "  m_Enabled: 1",
        "  m_EditorHideFlags: 0",
        f"  m_Script: {{fileID: 11500000, guid: {script_guid}, type: 3}}",
        f"  m_Name: {quote_yaml_string(name)}",
        "  m_EditorClassIdentifier: ",
        "  sessionId: \"\"",
        f"  npcName: {quote_yaml_string(npc_name)}",
        "  postDialogueText: \"\"",
        "  postDialogueTextIsLocalizationKey: 0",
        "  nodes:",
    ]
    if not nodes:
        lines[-1] = "  nodes: []"
    for node in nodes:
        v = node.values
        lines.extend([
            f"  - speakerId: {quote_yaml_string(v.get('speakerId', ''))}",
            f"    textIsLocalizationKey: {1 if v.get('textIsLocalizationKey', False) else 0}",
            f"    text: {quote_yaml_string(v.get('text', ''))}",
            f"    nextNodeIndex: {int(v.get('nextNodeIndex', -1) if str(v.get('nextNodeIndex', -1)).strip() else -1)}",
        ])
        if not node.choices:
            lines.append("    choices: []")
        else:
            lines.append("    choices:")
            for choice in node.choices:
                cv = choice.values
                lines.extend([
                    f"    - sessionId: {quote_yaml_string(cv.get('sessionId', ''))}",
                    f"      choiceId: {quote_yaml_string(cv.get('choiceId', ''))}",
                    f"      labelIsLocalizationKey: {1 if cv.get('labelIsLocalizationKey', False) else 0}",
                    f"      label: {quote_yaml_string(cv.get('label', ''))}",
                    f"      requiredFlag: {quote_yaml_string(cv.get('requiredFlag', ''))}",
                    f"      requiredItemName: {quote_yaml_string(cv.get('requiredItemName', ''))}",
                    f"      requiredItemAmount: {int(cv.get('requiredItemAmount', 1) if str(cv.get('requiredItemAmount', 1)).strip() else 1)}",
                    f"      resultText: {quote_yaml_string(cv.get('resultText', ''))}",
                    f"      resultTextIsLocalizationKey: {1 if cv.get('resultTextIsLocalizationKey', False) else 0}",
                    f"      resultSpeakerId: {quote_yaml_string(cv.get('resultSpeakerId', ''))}",
                    f"      nextNodeIndex: {int(cv.get('nextNodeIndex', -1) if str(cv.get('nextNodeIndex', -1)).strip() else -1)}",
                    f"      ltDelta: {int(cv.get('ltDelta', 0) if str(cv.get('ltDelta', 0)).strip() else 0)}",
                    f"      qtDelta: {int(cv.get('qtDelta', 0) if str(cv.get('qtDelta', 0)).strip() else 0)}",
                ])
    lines.extend(["  m_AssetBundleName: ", "  m_AssetBundleVariant: "])
    return "\n".join(lines) + "\n"


def asset_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "NativeFormatImporter:\n"
        "  externalObjects: {}\n"
        "  mainObjectFileID: 11400000\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def text_asset_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )

def folder_meta(guid: str) -> str:
    return (
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "folderAsset: yes\n"
        "DefaultImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )


def get_profile_records(project_root: Path, profile_guid: str) -> List[ProfileRecord]:
    assets_root = project_root / "Assets"
    records: List[ProfileRecord] = []
    for suffix in ("*.unity", "*.prefab"):
        for path in assets_root.rglob(suffix):
            if should_skip_asset_path(path, assets_root) or ".dialogue_migration_backup" in str(path).casefold():
                continue
            try:
                content = read_file(path)
            except OSError:
                continue
            prefix, docs = docs_in_text(content)
            names = game_object_names(docs)
            for doc_index, doc in enumerate(docs):
                if doc.type_id != 114 or script_guid_from_doc(doc) != profile_guid:
                    continue
                lines = doc.text.splitlines()
                gobj_id = game_object_id_from_doc(doc)
                go_name = names.get(gobj_id, path.stem)
                dialogue_idx = find_direct_field(lines, 0, len(lines), "dialogue", indent=2)
                if dialogue_idx is None:
                    continue
                dialogue_asset_idx = find_direct_field(lines, 0, len(lines), "dialogueAsset", indent=2)
                assigned = False
                if dialogue_asset_idx is not None:
                    parsed_ref = parse_field_line(lines[dialogue_asset_idx])
                    value = parsed_ref[3] if parsed_ref else ""
                    fileid_match = re.search(r"fileID:\s*(-?\d+)", value)
                    guid_match = re.search(r"guid:\s*([0-9a-fA-F]{32})", value)
                    nonzero_guid = bool(guid_match and int(guid_match.group(1), 16) != 0)
                    assigned = bool((fileid_match and fileid_match.group(1) != "0") or nonzero_guid)
                dialogue_prop, dialogue_end, nodes = parse_nodes(lines, "dialogue")
                if dialogue_prop is None or not profile_has_text(nodes):
                    continue
                records.append(ProfileRecord(
                    path=path, doc_index=doc_index, object_id=doc.file_id,
                    game_object_id=gobj_id, game_object_name=go_name,
                    lines=lines, dialogue_property_line=dialogue_idx,
                    dialogue_end_line=dialogue_end, nodes=nodes,
                    has_assigned_asset=assigned,
                    zone=namespace_for_path(project_root, path),
                ))
    return records


def record_sort_key(record: ProfileRecord, project_root: Path) -> Tuple[str, str, str, str]:
    return (record.zone, record.game_object_name.casefold(), str(record.path.relative_to(project_root)).casefold(), record.object_id)


def make_key_prefixes(records: List[ProfileRecord], project_root: Path) -> None:
    per_namespace: Dict[str, int] = {}
    # Stable alphabetical numbering per zone/scene namespace. Names and relative paths keep ordering reproducible.
    for record in sorted(records, key=lambda r: record_sort_key(r, project_root)):
        per_namespace[record.zone] = per_namespace.get(record.zone, 0) + 1
        record.prefix = f"{record.zone}_NPC_{per_namespace[record.zone]:02d}"


def localize_node_data(nodes: List[NodeRecord], prefix: str, translations: TranslationStore,
                       warnings: List[str], context: str) -> None:
    for node_index, node in enumerate(nodes, start=1):
        line_key = f"{prefix}_LINE_{node_index:02d}"
        node_text = str(node.values.get("text", "") or "")
        text_key, text_flag = translations.localize(
            node_text, bool(node.values.get("textIsLocalizationKey", False)), line_key,
            warnings, f"{context} / node {node_index} / text",
        )
        if node_text.strip():
            node.values["text"] = text_key
            node.values["textIsLocalizationKey"] = text_flag
        for choice_index, choice in enumerate(node.choices, start=1):
            choice_key = f"{line_key}_CHOICE_{choice_index:02d}"
            label = str(choice.values.get("label", "") or "")
            if label.strip():
                new_label, flag = translations.localize(
                    label, bool(choice.values.get("labelIsLocalizationKey", False)), choice_key,
                    warnings, f"{context} / node {node_index} / choice {choice_index} / label",
                )
                choice.values["label"] = new_label
                choice.values["labelIsLocalizationKey"] = flag
            result = str(choice.values.get("resultText", "") or "")
            if result.strip():
                new_result, flag = translations.localize(
                    result, bool(choice.values.get("resultTextIsLocalizationKey", False)), choice_key + "_RESULT",
                    warnings, f"{context} / node {node_index} / choice {choice_index} / resultText",
                )
                choice.values["resultText"] = new_result
                choice.values["resultTextIsLocalizationKey"] = flag


def field_has_nonempty_string(lines: List[str], index: Optional[int]) -> bool:
    if index is None:
        return False
    parsed = parse_field_line(lines[index])
    return bool(parsed and str(read_field_value(lines, index)).strip())


def _insert_localization_flag(lines: List[str], target_idx: int, flag_name: str,
                              after: bool = False) -> None:
    parsed = parse_field_line(lines[target_idx])
    if not parsed:
        raise MigrationError(f"Kh?ng t?m th?y tr??ng ??ch c?a {flag_name} t?i d?ng {target_idx + 1}.")
    indent, is_list_item, key, _ = parsed
    if after:
        _, insert_at = _collect_field_raw_value(lines, target_idx)
        flag_indent = indent + 2 if is_list_item else indent
        lines.insert(insert_at, " " * flag_indent + f"{flag_name}: 1")
        return
    if is_list_item:
        original = lines[target_idx]
        key_start = original.find(key + ":")
        if key_start < 0:
            raise MigrationError(f"Kh?ng th? ??t {flag_name} tr??c tr??ng danh s?ch.")
        lines[target_idx] = " " * (indent + 2) + original[key_start:]
        lines.insert(target_idx, " " * indent + f"- {flag_name}: 1")
        return
    lines.insert(target_idx, " " * indent + f"{flag_name}: 1")


def _move_localization_flag(lines: List[str], flag_idx: int, target_idx: int,
                            flag_name: str, after: bool) -> None:
    flag = parse_field_line(lines[flag_idx])
    if not flag:
        raise MigrationError(f"Kh?ng ??c ???c tr??ng {flag_name} t?i d?ng {flag_idx + 1}.")
    flag_value = flag[3].strip() or "1"
    lines.pop(flag_idx)
    if flag_idx < target_idx:
        target_idx -= 1
    parsed = parse_field_line(lines[target_idx])
    if not parsed:
        raise MigrationError(f"Kh?ng t?m th?y tr??ng ??ch c?a {flag_name}.")
    indent, is_list_item, key, _ = parsed
    if after:
        _, insert_at = _collect_field_raw_value(lines, target_idx)
        flag_indent = indent + 2 if is_list_item else indent
        lines.insert(insert_at, " " * flag_indent + f"{flag_name}: {flag_value}")
        return
    if is_list_item:
        original = lines[target_idx]
        key_start = original.find(key + ":")
        if key_start < 0:
            raise MigrationError(f"Kh?ng th? ??t {flag_name} tr??c tr??ng danh s?ch.")
        lines[target_idx] = " " * (indent + 2) + original[key_start:]
        lines.insert(target_idx, " " * indent + f"- {flag_name}: {flag_value}")
        return
    lines.insert(target_idx, " " * indent + f"{flag_name}: {flag_value}")


def normalize_localization_flag_order(lines: List[str]) -> int:
    moved = 0
    for _ in range(1000):
        operation = None
        post_idx = find_direct_field(lines, 0, len(lines), "postDialogueText", indent=2)
        post_flag_idx = find_direct_field(lines, 0, len(lines), "postDialogueTextIsLocalizationKey", indent=2)
        if post_idx is not None and post_flag_idx is not None:
            _, expected_idx = _collect_field_raw_value(lines, post_idx)
            if post_flag_idx != expected_idx:
                operation = (post_flag_idx, post_idx, "postDialogueTextIsLocalizationKey", True)

        if operation is None:
            _, _, nodes = parse_nodes(lines, "nodes")
            for node in nodes:
                flag_idx = node.field_lines.get("textIsLocalizationKey")
                value_idx = node.field_lines.get("text")
                if flag_idx is not None and value_idx is not None and flag_idx != value_idx - 1:
                    operation = (flag_idx, value_idx, "textIsLocalizationKey", False)
                    break
                for choice in node.choices:
                    for flag_name, value_name, after in (
                        ("labelIsLocalizationKey", "label", False),
                        ("resultTextIsLocalizationKey", "resultText", True),
                    ):
                        flag_idx = choice.field_lines.get(flag_name)
                        value_idx = choice.field_lines.get(value_name)
                        if flag_idx is None or value_idx is None:
                            continue
                        if after:
                            _, expected_idx = _collect_field_raw_value(lines, value_idx)
                        else:
                            expected_idx = value_idx - 1
                        if flag_idx != expected_idx:
                            operation = (flag_idx, value_idx, flag_name, after)
                            break
                    if operation:
                        break
                if operation:
                    break
        if operation is None:
            return moved
        _move_localization_flag(lines, *operation)
        moved += 1
    raise MigrationError("Kh?ng th? s?p x?p ?n ??nh c?c c? localization.")


def update_existing_dialogue_asset(path: Path, project_root: Path, asset_guid: str,
                                   translations: TranslationStore, warnings: List[str]) -> Optional[str]:
    content = read_file(path)
    prefix_text, docs = docs_in_text(content)
    any_changed = False
    rel = str(path.relative_to(project_root)).replace(chr(92), "/")
    for doc in docs:
        if doc.type_id != 114 or script_guid_from_doc(doc) != asset_guid:
            continue
        changed = False
        lines = doc.text.splitlines()
        pending_flags: List[Tuple[int, str, bool]] = []
        asset_name = str(field_value(lines, "m_Name", indent=2) or path.stem)
        npc_name = str(field_value(lines, "npcName", indent=2) or "")
        found_zone = zone_prefix(rel + "/" + asset_name)
        generated_match = re.search(r"(Z\d+_NPC_\d+)", normalize_slug(asset_name, ""))
        if generated_match:
            base_prefix = generated_match.group(1)
        elif found_zone:
            base_prefix = f"{found_zone}_{normalize_slug(npc_name or asset_name, 'NPC')[:40]}"
        else:
            stem_slug = normalize_slug(asset_name.removeprefix("Dialogue_"), "DIALOGUE")
            base_prefix = f"DIALOGUE_{stem_slug}"[:64]

        def localize_field(value_idx: Optional[int], flag_idx: Optional[int], flag_name: str,
                           base_key: str, context: str, insert_after: bool) -> None:
            nonlocal changed
            if not field_has_nonempty_string(lines, value_idx):
                return
            old_value = str(read_field_value(lines, value_idx))
            old_flag = bool(read_field_value(lines, flag_idx)) if flag_idx is not None else False
            new_value, _ = translations.localize(old_value, old_flag, base_key, warnings, context)
            if new_value != old_value:
                replace_field_line(lines, value_idx, parse_field_line(lines[value_idx])[2], new_value, string_value=True)
                changed = True
            if flag_idx is not None:
                if not old_flag:
                    replace_field_line(lines, flag_idx, flag_name, 1)
                    changed = True
            else:
                pending_flags.append((value_idx, flag_name, insert_after))

        post_idx = find_direct_field(lines, 0, len(lines), "postDialogueText", indent=2)
        post_flag_idx = find_direct_field(lines, 0, len(lines), "postDialogueTextIsLocalizationKey", indent=2)
        if post_idx is not None:
            localize_field(post_idx, post_flag_idx, "postDialogueTextIsLocalizationKey",
                           f"{base_prefix}_POST_DIALOGUE", f"{rel} / postDialogueText", True)

        _, _, nodes = parse_nodes(lines, "nodes")
        for node_index, node in enumerate(nodes, start=1):
            line_key = f"{base_prefix}_LINE_{node_index:02d}"
            text_idx = node.field_lines.get("text")
            flag_idx = node.field_lines.get("textIsLocalizationKey")
            localize_field(text_idx, flag_idx, "textIsLocalizationKey", line_key,
                           f"{rel} / node {node_index} / text", False)

            for choice_index, choice in enumerate(node.choices, start=1):
                choice_key = f"{line_key}_CHOICE_{choice_index:02d}"
                label_idx = choice.field_lines.get("label")
                label_flag_idx = choice.field_lines.get("labelIsLocalizationKey")
                localize_field(label_idx, label_flag_idx, "labelIsLocalizationKey", choice_key,
                               f"{rel} / node {node_index} / choice {choice_index} / label", False)

                result_idx = choice.field_lines.get("resultText")
                result_flag_idx = choice.field_lines.get("resultTextIsLocalizationKey")
                localize_field(result_idx, result_flag_idx, "resultTextIsLocalizationKey", choice_key + "_RESULT",
                               f"{rel} / node {node_index} / choice {choice_index} / resultText", True)

        for target_idx, flag_name, insert_after in sorted(pending_flags, key=lambda entry: entry[0], reverse=True):
            _insert_localization_flag(lines, target_idx, flag_name, insert_after)
            changed = True
        if normalize_localization_flag_order(lines):
            changed = True

        if changed:
            any_changed = True
            doc.text = "\n".join(lines) + ("\n" if doc.text.endswith("\n") else "")
    if any_changed:
        return reassemble_docs(prefix_text, docs)
    return None

def create_or_read_json(path: Path) -> Dict[str, Any]:
    if not path.exists():
        return {}
    try:
        data = json.loads(read_file(path))
    except Exception as exc:
        raise MigrationError(f"Không đọc được JSON {path}: {exc}")
    if not isinstance(data, dict):
        raise MigrationError(f"{path} phải là JSON object dạng key-value, không phải array/scalar.")
    return data


def write_changes(project_root: Path, changes: Dict[Path, str], dry_run: bool) -> Optional[Path]:
    actual_changes = {path: content for path, content in changes.items() if not path.exists() or read_file(path) != content}
    if dry_run:
        print("\n--- DRY RUN: không ghi file ---")
        for path in sorted(actual_changes, key=lambda p: str(p).lower()):
            try:
                shown = path.relative_to(project_root)
            except ValueError:
                shown = path
            status = "CREATE" if not path.exists() else "UPDATE"
            print(f"[{status}] {shown}")
        return None
    if not actual_changes:
        return None
    stamp = _datetime.datetime.now().strftime("%Y%m%d_%H%M%S_%f")
    backup_root = project_root / ".dialogue_migration_backup" / stamp
    existing = [path for path in actual_changes if path.exists()]
    for path in existing:
        try:
            rel = path.relative_to(project_root)
        except ValueError:
            continue
        backup_path = backup_root / rel
        backup_path.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, backup_path)
    for path, content in actual_changes.items():
        path.parent.mkdir(parents=True, exist_ok=True)
        temp_path = path.with_name(path.name + ".migration_tmp")
        temp_path.write_text(content, encoding="utf-8", newline="\n")
        os.replace(temp_path, path)
    print(f"\nBackup: {backup_root if existing else '(không có file cũ cần backup)'}")
    return backup_root if existing else None


def main() -> int:
    for stream in (sys.stdout, sys.stderr):
        reconfigure = getattr(stream, "reconfigure", None)
        if reconfigure is not None:
            reconfigure(encoding="utf-8", errors="replace")
    parser = argparse.ArgumentParser(description="Chuyển thoại inline DemoNpcDialogueProfile sang DemoDialogueAsset và localization key.")
    parser.add_argument("--dry-run", action="store_true", help="Chỉ phân tích, không ghi file.")
    parser.add_argument("--include-existing-assets", action="store_true", help="Tùy chọn localization cho DemoDialogueAsset đã tồn tại.")
    args = parser.parse_args()

    try:
        script_path = Path(__file__).resolve()
        project_root = find_project_root(script_path)
        assets_root = project_root / "Assets"
        if not assets_root.is_dir():
            raise MigrationError(f"Không thấy thư mục Assets tại {assets_root}")

        profile_guid = discover_class_guid(assets_root, "DemoNpcDialogueProfile")
        dialogue_asset_guid = discover_class_guid(assets_root, "DemoDialogueAsset")
        if not profile_guid:
            raise MigrationError("Không tìm được GUID của DemoNpcDialogueProfile.cs. Kiểm tra file .cs.meta có tồn tại và được Unity tạo chưa.")
        if not dialogue_asset_guid:
            raise MigrationError("Không tìm được GUID của DemoDialogueAsset.cs. Kiểm tra file .cs.meta có tồn tại và được Unity tạo chưa.")

        patch_paths = find_patch_files(script_path.parent)
        patch_sources = {path: create_or_read_json(path) for path in patch_paths}
        vn_path = find_vn_json(project_root, patch_paths)
        translations_source = create_or_read_json(vn_path)
        translations_data = dict(translations_source)
        warnings: List[str] = []
        for patch_path in patch_paths:
            for key, value in patch_sources[patch_path].items():
                if key in translations_data:
                    if translations_data[key] != value:
                        warnings.append(f"Key '{key}' khác giá trị giữa vn.json gốc và các patch; bản được đọc trước được ưu tiên.")
                    continue
                translations_data[key] = value
        translations = TranslationStore(translations_data)
        output_vn_path = next_patch_path(script_path.parent, patch_sources)
        changes: Dict[Path, str] = {}

        # Scan all scene and prefab files for directly serialized MonoBehaviours.
        profile_records = get_profile_records(project_root, profile_guid)
        make_key_prefixes(profile_records, project_root)
        print(f"Project: {project_root}")
        print(f"vn.json gốc (chỉ đọc): {vn_path.relative_to(project_root) if vn_path.is_relative_to(project_root) else vn_path}")
        print(f"vn.json patch mới sẽ ghi cạnh script: {output_vn_path.relative_to(project_root) if output_vn_path.is_relative_to(project_root) else output_vn_path}")
        print(f"DemoNpcDialogueProfile có inline thoại: {len(profile_records)}")

        used_asset_paths: set[Path] = set()
        migrated_count = 0
        skipped_assigned = 0
        # Build per-file changes while keeping all docs except the target component unchanged.
        grouped_records: Dict[Path, List[ProfileRecord]] = {}
        for record in profile_records:
            grouped_records.setdefault(record.path, []).append(record)

        for file_path, records_for_file in grouped_records.items():
            original_content = read_file(file_path)
            prefix_text, docs = docs_in_text(original_content)
            for record in records_for_file:
                if record.has_assigned_asset:
                    skipped_assigned += 1
                    warnings.append(f"{file_path.relative_to(project_root)} / {record.game_object_name}: dialogueAsset đã được gán; bỏ qua inline data để tránh ghi đè.")
                    continue
                localize_node_data(record.nodes, record.prefix, translations, warnings,
                                   f"{file_path.relative_to(project_root)} / {record.game_object_name}")
                folder = project_root / "Assets/Data/Dialogues/Migrated"
                base_name = f"Dialogue_{record.prefix}"
                candidate = folder / f"{base_name}.asset"
                suffix = 2
                while candidate.exists() or candidate in used_asset_paths:
                    candidate = folder / f"{base_name}_{suffix:02d}.asset"
                    suffix += 1
                used_asset_paths.add(candidate)
                guid = uuid.uuid4().hex
                record.asset_path = candidate
                record.asset_guid = guid
                asset_yaml = new_asset_yaml(dialogue_asset_guid, candidate.stem, record.game_object_name, record.nodes)
                changes[candidate] = asset_yaml
                changes[Path(str(candidate) + ".meta")] = asset_meta(guid)
                migrated_count += 1

                doc = docs[record.doc_index]
                lines = doc.text.splitlines()
                dialogue_idx = find_direct_field(lines, 0, len(lines), "dialogue", indent=2)
                if dialogue_idx is None:
                    warnings.append(f"{file_path.relative_to(project_root)} / {record.game_object_name}: không tìm thấy field dialogue, bỏ qua component.")
                    continue
                _, dialogue_end = _find_array_bounds(lines, dialogue_idx)
                # Unity serializes this profile's dialogue array as its final declared field.
                lines[dialogue_idx] = "  dialogue: []"
                del lines[dialogue_idx + 1:dialogue_end]
                asset_ref_idx = find_direct_field(lines, 0, len(lines), "dialogueAsset", indent=2)
                asset_reference = f"{{fileID: 11400000, guid: {guid}, type: 2}}"
                if asset_ref_idx is None:
                    dialogue_idx = find_direct_field(lines, 0, len(lines), "dialogue", indent=2)
                    if dialogue_idx is None:
                        warnings.append(f"{file_path.relative_to(project_root)} / {record.game_object_name}: không thể chèn dialogueAsset vì thiếu field dialogue.")
                    else:
                        lines.insert(dialogue_idx, "  dialogueAsset: " + asset_reference)
                else:
                    replace_field_line(lines, asset_ref_idx, "dialogueAsset", asset_reference)
                doc.text = "\n".join(lines) + ("\n" if doc.text.endswith("\n") else "")
            changes[file_path] = reassemble_docs(prefix_text, docs)

        # Existing dialogue assets are opt-in so the first migration only changes inline profiles.
        existing_asset_count = 0
        if args.include_existing_assets:
            for path in assets_root.rglob("*.asset"):
                if should_skip_asset_path(path, assets_root) or "Assets/Data/Dialogues/Migrated" in str(path).replace("\\", "/"):
                    continue
                try:
                    update = update_existing_dialogue_asset(path, project_root, dialogue_asset_guid, translations, warnings)
                except Exception as exc:
                    warnings.append(f"Không xử lý được {path.relative_to(project_root)}: {exc}")
                    continue
                if update is not None:
                    changes[path] = update
                    existing_asset_count += 1

        # A newly created Migrated folder gets its own .meta so its GUID remains stable under version control.
        migrated_folder = project_root / "Assets/Data/Dialogues/Migrated"
        if migrated_count and not Path(str(migrated_folder) + ".meta").exists():
            changes[Path(str(migrated_folder) + ".meta")] = folder_meta(uuid.uuid4().hex)

        # Each applied run writes only its new keys to a fresh numbered patch file.
        if translations.added_values:
            changes[output_vn_path] = json.dumps(translations.added_values, ensure_ascii=False, indent=2) + "\n"
            output_meta = Path(str(output_vn_path) + ".meta")
            if not output_meta.exists():
                changes[output_meta] = text_asset_meta(uuid.uuid4().hex)

        # Summarize planned operations before applying.
        print(f"Inline profile sẽ chuyển thành asset mới: {migrated_count}")
        print(f"DemoDialogueAsset cũ có thay đổi localization: {existing_asset_count}")
        print(f"Key dịch mới sẽ ghi vào patch mới: {translations.added}")
        print(f"Profile inline bị bỏ qua vì đã có DialogueAsset: {skipped_assigned}")

        # Remove unchanged files from plan to avoid unnecessary writes/imports.
        changes = {path: content for path, content in changes.items() if not path.exists() or read_file(path) != content}
        write_changes(project_root, changes, args.dry_run)

        if warnings:
            print(f"\nCảnh báo ({len(warnings)}):")
            for warning in warnings:
                print(f"- {warning}")
        if not args.dry_run:
            print("\nHoàn tất. Đóng/mở lại Unity hoặc chờ Asset Database import các .asset và .meta mới.")
        return 0
    except MigrationError as exc:
        print(f"[ERROR] {exc}", file=sys.stderr)
        return 2
    except KeyboardInterrupt:
        print("Đã dừng bởi người dùng.", file=sys.stderr)
        return 130


if __name__ == "__main__":
    raise SystemExit(main())
