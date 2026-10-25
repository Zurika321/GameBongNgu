#!/usr/bin/env python3
"""Translate this Unity game's localization JSON files from vn.json.

Usage:
    python dichthuat.py                 # translate missing keys to en.json
    python dichthuat.py en              # choose another target language
    python dichthuat.py en --overwrite  # translate every value again

Requires googletrans and an internet connection when translation is needed.
"""

from __future__ import annotations

import argparse
import json
import re
import time
from pathlib import Path
from typing import Any

SOURCE_LANGUAGE = "vi"  # Google Translate code; the game's source file is named vn.json.
SOURCE_FILE = "vn.json"
TOKEN_PATTERN = re.compile(r"\{[^{}]+\}|<[^<>]+>|\[[A-Z0-9_ ]+\]")
LANGUAGE_CODE_PATTERN = re.compile(r"[a-z0-9]+(?:[-_][a-z0-9]+)*\Z", re.IGNORECASE)


def read_flat_json(path: Path, *, required: bool) -> dict[str, str]:
    if not path.exists():
        if required:
            raise SystemExit(f"Missing Vietnamese source file: {path}")
        return {}

    try:
        with path.open("r", encoding="utf-8-sig") as file:
            data: Any = json.load(file)
    except (OSError, json.JSONDecodeError) as error:
        raise SystemExit(f"Could not read JSON '{path.name}': {error}") from error

    if not isinstance(data, dict):
        raise SystemExit(f"'{path.name}' must be a JSON object containing string values.")

    if any(not isinstance(key, str) or not isinstance(value, str) for key, value in data.items()):
        raise SystemExit(f"'{path.name}' only supports string keys and string values.")
    return data


def protect_tokens(text: str) -> tuple[str, list[tuple[str, str]]]:
    protected: list[tuple[str, str]] = []

    def replace(match: re.Match[str]) -> str:
        marker = f"ZXQLOCALIZATIONTOKEN{len(protected)}ZXQ"
        protected.append((marker, match.group(0)))
        return marker

    return TOKEN_PATTERN.sub(replace, text), protected


def restore_tokens(text: str, protected: list[tuple[str, str]]) -> str:
    for marker, original in protected:
        text = text.replace(marker, original)
    return text


def main() -> int:
    parser = argparse.ArgumentParser(description="Translate this game's JSON localization from vn.json.")
    parser.add_argument(
        "target_language",
        nargs="?",
        default="en",
        help="Output language code, for example en, ja, or es (default: en).",
    )
    parser.add_argument(
        "--overwrite",
        action="store_true",
        help="Translate all values again, including existing translations.",
    )
    args = parser.parse_args()

    target_language = args.target_language.strip().lower().replace("_", "-")
    if not LANGUAGE_CODE_PATTERN.fullmatch(target_language):
        raise SystemExit("Invalid language code. Examples: en, ja, pt-br.")
    if target_language in {"vn", "vi"}:
        raise SystemExit("vn.json is the source file. Choose a different target language.")

    directory = Path(__file__).resolve().parent
    source_path = directory / SOURCE_FILE
    target_path = directory / f"{target_language}.json"
    source_data = read_flat_json(source_path, required=True)
    old_target_data = read_flat_json(target_path, required=False)

    pending = [
        (key, value)
        for key, value in source_data.items()
        if value.strip() and (args.overwrite or not old_target_data.get(key, "").strip())
    ]

    translator = None
    if pending:
        try:
            from googletrans import Translator
        except ImportError as error:
            raise SystemExit(
                "googletrans is required. Install it with 'python -m pip install googletrans'."
            ) from error
        translator = Translator()

    output_data: dict[str, str] = {}
    translated_count = 0
    preserved_count = 0
    failed_count = 0

    for key, source_value in source_data.items():
        old_value = old_target_data.get(key, "")
        should_translate = args.overwrite or not old_value.strip()

        if not should_translate or not source_value.strip():
            output_data[key] = old_value if old_value.strip() else source_value
            preserved_count += 1
            continue

        text_to_translate, protected = protect_tokens(source_value)
        try:
            result = translator.translate(text_to_translate, src=SOURCE_LANGUAGE, dest=target_language)
            output_data[key] = restore_tokens(result.text, protected)
            translated_count += 1
            time.sleep(0.15)
        except Exception as error:
            output_data[key] = old_value if old_value.strip() else source_value
            failed_count += 1
            print(f"Translation failed for key '{key}': {error}. Kept the old value or Vietnamese source.")

    # Keep target-only entries so manual translations are not silently deleted.
    for key, value in old_target_data.items():
        if key not in output_data:
            output_data[key] = value

    temporary_path = target_path.with_suffix(target_path.suffix + ".tmp")
    try:
        with temporary_path.open("w", encoding="utf-8", newline="\n") as file:
            json.dump(output_data, file, ensure_ascii=False, indent=2)
            file.write("\n")
        temporary_path.replace(target_path)
    except OSError as error:
        if temporary_path.exists():
            temporary_path.unlink()
        raise SystemExit(f"Could not write '{target_path.name}': {error}") from error

    print(f"Source: {source_path.name} (Vietnamese)")
    print(f"Output: {target_path.name}")
    print(f"Translated: {translated_count}; preserved: {preserved_count}; failed: {failed_count}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
