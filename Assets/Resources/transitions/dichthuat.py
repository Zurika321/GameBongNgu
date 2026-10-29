#!/usr/bin/env python3
"""Translate a Vietnamese localization JSON batch into a separate language patch.

Set SOURCE_FILE, TARGET_LANGUAGE, OUTPUT_FILE, and OVERWRITE_EXISTING below,
then run once from the Unity project's Assets directory:
    python Resources/transitions/dichthuat.py

Relative source/output paths are resolved beside this script. If OUTPUT_FILE is
empty, it is generated per batch, for example en_hoithoai1.json.
Requires googletrans and an internet connection when translation is needed.
"""
 
from __future__ import annotations

import asyncio
import json
import re
import time
from pathlib import Path
from typing import Any

# Edit these settings, then run the script with one command.
SOURCE_FILE = "vn_hoithoai1.json"  # Vietnamese JSON to translate.
TARGET_LANGUAGE = "en"  # Examples: en, ja, es.
OUTPUT_FILE = "en_hoithoai1.json"  # Output file; change to ja_hoithoai1.json when translating to Japanese.
OVERWRITE_EXISTING = True  # True retranslates this source batch's existing translations.
REQUEST_TIMEOUT_SECONDS = 30.0  # Maximum wait for each translation request.
REQUEST_DELAY_SECONDS = 30.0  # Pause between requests to reduce rate limiting.
SOURCE_LANGUAGE = "vi"  # Google Translate code for Vietnamese source text.
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


async def translate_pending(pending: list[tuple[str, str]], translator_class: Any,
                            target_language: str) -> tuple[dict[str, str], set[str], set[str]]:
    translated: dict[str, str] = {}
    failed_keys: set[str] = set()
    not_attempted_keys: set[str] = set()
    total = len(pending)

    print(f"Bắt đầu dịch {total} dòng; timeout {REQUEST_TIMEOUT_SECONDS:g}s/dòng; nghỉ {REQUEST_DELAY_SECONDS:g}s giữa các dòng.", flush=True)
    async with translator_class(timeout=REQUEST_TIMEOUT_SECONDS, raise_exception=True) as translator:
        for index, (key, source_value) in enumerate(pending, start=1):
            started = time.monotonic()
            print(f"[{index}/{total}] {key}: đang gửi yêu cầu...", end="", flush=True)
            text_to_translate, protected = protect_tokens(source_value)
            rate_limited = False
            try:
                result = await asyncio.wait_for(
                    translator.translate(text_to_translate, src=SOURCE_LANGUAGE, dest=target_language),
                    timeout=REQUEST_TIMEOUT_SECONDS,
                )
                translated[key] = restore_tokens(result.text, protected)
                elapsed = time.monotonic() - started
                print(f" OK; phản hồi sau {elapsed:.1f}s.")
            except asyncio.TimeoutError:
                failed_keys.add(key)
                elapsed = time.monotonic() - started
                print(f" TIMEOUT sau {elapsed:.1f}s; giữ bản cũ hoặc văn bản nguồn.")
            except Exception as error:
                failed_keys.add(key)
                elapsed = time.monotonic() - started
                response = getattr(error, "response", None)
                status_code = getattr(response, "status_code", None)
                rate_limited = status_code == 429 or re.search(r"\b429\b", str(error)) is not None
                if rate_limited:
                    print(f" RATE LIMIT 429 sau {elapsed:.1f}s; dừng lô để tránh gửi thêm request.")
                else:
                    error_label = "TIMEOUT" if "timeout" in type(error).__name__.casefold() else "LỖI"
                    print(f" {error_label} sau {elapsed:.1f}s: {error}; giữ bản dịch cũ nếu có.")
            if rate_limited:
                remaining_keys = [remaining_key for remaining_key, _ in pending[index:]]
                not_attempted_keys.update(remaining_keys)
                print(f"Dừng do rate limit; {len(remaining_keys)} dòng còn lại chưa được gửi.")
                break
            await asyncio.sleep(REQUEST_DELAY_SECONDS)

    return translated, failed_keys, not_attempted_keys

def resolve_json_path(value: str, script_directory: Path) -> Path:
    path = Path(value).expanduser()
    if not path.is_absolute():
        path = script_directory / path
    return path.resolve()


def default_output_name(source_path: Path, target_language: str) -> str:
    source_stem = source_path.stem
    if source_stem.casefold().startswith("vn_"):
        batch_name = source_stem[3:]
    else:
        # Keep even a general vn.json source in a separate patch (for example en_vn.json).
        batch_name = source_stem
    return f"{target_language}_{batch_name}.json"


def main() -> int:
    run_started = time.monotonic()
    target_language = TARGET_LANGUAGE.strip().lower().replace("_", "-")
    if not LANGUAGE_CODE_PATTERN.fullmatch(target_language):
        raise SystemExit("Invalid language code. Examples: en, ja, pt-br.")
    if target_language in {"vn", "vi"}:
        raise SystemExit("vn/vi is the source language. Choose a different target language.")

    directory = Path(__file__).resolve().parent
    source_path = resolve_json_path(SOURCE_FILE, directory)
    output_name = OUTPUT_FILE.strip() or default_output_name(source_path, target_language)
    target_path = resolve_json_path(output_name, directory)
    if source_path == target_path:
        raise SystemExit("Source and output paths must be different.")
    if target_path.suffix.casefold() != ".json":
        raise SystemExit("The output file must have a .json extension.")

    source_data = read_flat_json(source_path, required=True)
    old_target_data = read_flat_json(target_path, required=False)

    pending = [
        (key, value)
        for key, value in source_data.items()
        if value.strip() and (
            OVERWRITE_EXISTING
            or not old_target_data.get(key, "").strip()
            or old_target_data.get(key) == value
        )
    ]

    if not pending:
        print("Không có dòng nào cần dịch; không có key mới để gửi.")

    translated_values: dict[str, str] = {}
    failed_keys: set[str] = set()
    not_attempted_keys: set[str] = set()
    if pending:
        try:
            from googletrans import Translator
        except (ImportError, AttributeError) as error:
            raise SystemExit(
                "googletrans is missing or incompatible. Upgrade it with "
                "'python -m pip install --upgrade googletrans==4.0.2'. "
                f"Details: {error}"
            ) from error
        translated_values, failed_keys, not_attempted_keys = asyncio.run(
            translate_pending(pending, Translator, target_language)
        )

    output_data: dict[str, str] = {}
    translated_count = 0
    preserved_count = 0
    failed_count = len(failed_keys)
    not_attempted_count = len(not_attempted_keys)

    for key, source_value in source_data.items():
        old_value = old_target_data.get(key, "")
        should_translate = OVERWRITE_EXISTING or not old_value.strip() or old_value == source_value

        if not should_translate or not source_value.strip():
            output_data[key] = old_value if old_value.strip() else source_value
            preserved_count += 1
            continue

        if key in translated_values:
            output_data[key] = translated_values[key]
            translated_count += 1
        elif key in failed_keys or key in not_attempted_keys:
            # Keep prior content; source-equal or missing values remain retryable next run.
            if old_value.strip():
                output_data[key] = old_value
        else:
            output_data[key] = old_value if old_value.strip() else source_value

    # Keep output-only entries so manual translations are not silently deleted.
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
    print(f"Output: {target_path.name} ({target_language})")
    print(f"Translated: {translated_count}; preserved: {preserved_count}; failed: {failed_count}; not sent: {not_attempted_count}")
    print(f"Hoàn tất sau {time.monotonic() - run_started:.1f}s.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())