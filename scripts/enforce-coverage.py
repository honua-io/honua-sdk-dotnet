#!/usr/bin/env python3
"""Fail closed when merged Cobertura coverage does not meet configured floors."""

from __future__ import annotations

import argparse
from decimal import Decimal, InvalidOperation
from pathlib import Path
import sys
import xml.etree.ElementTree as ET


def decimal_text(value: Decimal) -> str:
    """Return a non-rounded, non-scientific decimal for logs and summaries."""

    text = format(value, "f")
    if "." in text:
        text = text.rstrip("0").rstrip(".")
    return text or "0"


def parse_percentage(value: str | None, name: str) -> Decimal:
    if value is None:
        raise ValueError(f"Cobertura report is missing the required {name!r} attribute")

    try:
        percentage = Decimal(value) * Decimal(100)
    except InvalidOperation as error:
        raise ValueError(f"Cobertura {name!r} value is not a decimal: {value!r}") from error

    if not percentage.is_finite() or not Decimal(0) <= percentage <= Decimal(100):
        raise ValueError(
            f"Cobertura {name!r} value must be between 0 and 1: {value!r}"
        )
    return percentage


def parse_floor(value: str, name: str) -> Decimal:
    try:
        floor = Decimal(value)
    except InvalidOperation as error:
        raise ValueError(f"{name} must be a decimal percentage: {value!r}") from error

    if not floor.is_finite() or not Decimal(0) <= floor <= Decimal(100):
        raise ValueError(f"{name} must be between 0 and 100: {value!r}")
    return floor


def append_summary(
    summary_path: Path | None,
    line_rate: Decimal,
    line_floor: Decimal,
    branch_rate: Decimal,
    branch_floor: Decimal,
) -> None:
    if summary_path is None:
        return

    with summary_path.open("a", encoding="utf-8") as summary:
        summary.write(
            "\n**Coverage gate**: "
            f"lines {decimal_text(line_rate)}% / floor {decimal_text(line_floor)}%, "
            f"branches {decimal_text(branch_rate)}% / floor "
            f"{decimal_text(branch_floor)}%\n"
        )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--line-floor", required=True)
    parser.add_argument("--branch-floor", required=True)
    parser.add_argument("--summary", type=Path)
    args = parser.parse_args(argv)

    try:
        if not args.report.is_file():
            raise ValueError(f"Cobertura coverage report was not found at {args.report}")

        root = ET.parse(args.report).getroot()
        line_rate = parse_percentage(root.get("line-rate"), "line-rate")
        branch_rate = parse_percentage(root.get("branch-rate"), "branch-rate")
        line_floor = parse_floor(args.line_floor, "line floor")
        branch_floor = parse_floor(args.branch_floor, "branch floor")
    except (ET.ParseError, OSError, ValueError) as error:
        print(f"::error::{error}")
        return 2

    line_text = decimal_text(line_rate)
    branch_text = decimal_text(branch_rate)
    line_floor_text = decimal_text(line_floor)
    branch_floor_text = decimal_text(branch_floor)

    print(f"Line coverage: {line_text}% (floor {line_floor_text}%)")
    print(f"Branch coverage: {branch_text}% (floor {branch_floor_text}%)")
    append_summary(args.summary, line_rate, line_floor, branch_rate, branch_floor)

    failed = False
    if line_rate < line_floor:
        print(
            f"::error::Line coverage {line_text}% is below the floor of "
            f"{line_floor_text}%."
        )
        failed = True
    if branch_rate < branch_floor:
        print(
            f"::error::Branch coverage {branch_text}% is below the floor of "
            f"{branch_floor_text}%."
        )
        failed = True

    return 1 if failed else 0


if __name__ == "__main__":
    sys.exit(main())
