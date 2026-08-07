#!/usr/bin/env python3
"""Independent stdlib-only validation of ARVREL CT golden vectors."""
from __future__ import annotations

import argparse
import json
from pathlib import Path
from typing import Any

from ct_reference_solver import solve


def reject_duplicate_keys(pairs: list[tuple[str, Any]]) -> dict[str, Any]:
    result: dict[str, Any] = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f"duplicate JSON key: {key}")
        result[key] = value
    return result


def load_json(path: Path) -> dict[str, Any]:
    def reject_constant(value: str) -> None:
        raise ValueError(f"non-finite JSON number: {value}")

    with path.open("r", encoding="utf-8") as stream:
        return json.load(
            stream,
            object_pairs_hook=reject_duplicate_keys,
            parse_constant=reject_constant,
        )


def close(expected: float, actual: float, absolute: float, relative: float, label: str) -> None:
    tolerance = absolute + relative * max(abs(expected), abs(actual))
    if abs(expected - actual) > tolerance:
        raise AssertionError(
            f"{label}: expected {expected:.17g}, actual {actual:.17g}, tolerance {tolerance:.3g}"
        )


def validate_case(case: dict[str, Any], document: dict[str, Any]) -> None:
    solved = solve(
        case,
        document["solverContract"]["iterations"],
        document["solverContract"]["relaxation"],
    )
    expected = case["expected"]
    absolute = document["absoluteTolerance"]
    relative = document["relativeTolerance"]
    for output_name in ("ideal", "secondary", "fluxPerUnit", "excitationCurrentA"):
        for offset, sample in enumerate(case["checkpoints"]):
            close(
                expected[output_name][offset],
                solved[output_name][sample],
                absolute,
                relative,
                f"{case['name']} {output_name}[{sample}]",
            )

    for name, value in expected["finalState"].items():
        actual = solved["finalState"][name]
        if isinstance(value, bool) or isinstance(value, int):
            if value != actual:
                raise AssertionError(f"{case['name']} finalState.{name}: {value} != {actual}")
        else:
            close(value, actual, absolute, relative, f"{case['name']} finalState.{name}")

    for name, value in expected["diagnostics"].items():
        actual = solved["diagnostics"][name]
        if value is None:
            if actual is not None:
                raise AssertionError(f"{case['name']} diagnostics.{name}: expected null")
        elif isinstance(value, bool) or isinstance(value, int):
            if value != actual:
                raise AssertionError(f"{case['name']} diagnostics.{name}: {value} != {actual}")
        else:
            close(value, actual, absolute, relative, f"{case['name']} diagnostics.{name}")


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--manifest",
        type=Path,
        default=Path(__file__).parent / "vectors" / "ct_reference_manifest.json",
    )
    args = parser.parse_args()
    document = load_json(args.manifest)
    if document["schemaVersion"] != 1:
        raise ValueError("unsupported CT vector schema")
    if document["solverContract"] != {"iterations": 6, "relaxation": 0.45}:
        raise ValueError("unexpected solver contract")
    case_files = document["caseFiles"]
    if len(case_files) < 6 or len(case_files) != len(set(case_files)):
        raise ValueError("reference manifest must contain at least six unique case files")

    cases = []
    for relative_path in case_files:
        case_path = (args.manifest.parent / relative_path).resolve()
        if args.manifest.parent.resolve() not in case_path.parents:
            raise ValueError(f"case path escapes vector directory: {relative_path}")
        payload = load_json(case_path)
        if payload != {"schemaVersion": 1, "case": payload.get("case")}:
            raise ValueError(f"invalid case envelope: {relative_path}")
        cases.append(payload["case"])

    for case in cases:
        validate_case(case, document)
        diagnostics = case["expected"]["diagnostics"]
        print(
            f"PASS {case['name']}: saturated={diagnostics['saturated']} "
            f"maxFlux={diagnostics['maximumAbsoluteFluxPerUnit']:.6f} pu "
            f"ratioError={diagnostics['rmsMagnitudeErrorPercent']:.6f}%"
        )
    print(f"Validated {len(cases)} CT reference cases with stdlib CPython only.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
