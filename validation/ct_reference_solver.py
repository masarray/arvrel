"""Independent CT equivalent-circuit solver used only by validation tooling."""
from __future__ import annotations

import math
from typing import Any


def excitation_current(flux: float, knee_flux: float, settings: dict[str, Any]) -> float:
    normalized = abs(flux) / knee_flux
    if normalized <= 1.0:
        magnitude = settings["excitationCurrentAtKneeA"] * normalized**3
    else:
        magnitude = settings["excitationCurrentAtKneeA"] + (
            settings["excitationCurrentAtTwiceKneeA"]
            - settings["excitationCurrentAtKneeA"]
        ) * (normalized - 1.0) ** settings["saturationExponent"]
    magnitude = min(magnitude, settings["maximumExcitationCurrentA"])
    return math.copysign(magnitude, flux)


def generate_source(case: dict[str, Any]) -> list[float]:
    source = case["source"]
    sample_rate = case["sampleRateHz"]
    frequency = case["frequencyHz"]
    peak = source["rmsA"] * math.sqrt(2.0)
    phase = math.radians(source["phaseDegrees"])
    dc_fraction = source["dcOffsetPercent"] / 100.0
    time_constant = source["dcTimeConstantMilliseconds"] / 1000.0
    start = source["startSampleIndex"]
    return [
        peak * math.cos(2.0 * math.pi * frequency * (start + index) / sample_rate + phase)
        + peak
        * dc_fraction
        * math.exp(-((start + index) / sample_rate) / time_constant)
        for index in range(source["sampleCount"])
    ]


def solve(case: dict[str, Any], iterations: int, relaxation: float) -> dict[str, Any]:
    ideal = generate_source(case)
    settings = case["settings"]
    initial = case.get("initialState")
    if not settings["enabled"] or not ideal:
        state = initial or {
            "initialized": False,
            "fluxLinkageVoltSeconds": 0.0,
            "previousSecondaryCurrentA": 0.0,
            "previousSecondaryVoltageV": 0.0,
            "processedSampleCount": 0,
        }
        return {
            "ideal": ideal,
            "secondary": ideal.copy(),
            "fluxPerUnit": [0.0] * len(ideal),
            "excitationCurrentA": [0.0] * len(ideal),
            "finalState": state,
            "diagnostics": {
                "enabled": False,
                "saturated": False,
                "saturatedSampleCount": 0,
                "firstSaturatedSample": -1,
                "firstSaturationMilliseconds": None,
                "maximumAbsoluteFluxPerUnit": 0.0,
                "maximumExcitationCurrentA": 0.0,
                "maximumSecondaryVoltageV": 0.0,
                "idealRmsA": 0.0,
                "secondaryRmsA": 0.0,
                "rmsMagnitudeErrorPercent": 0.0,
                "waveformErrorPercent": 0.0,
                "minimumMagnitudeRatio": 1.0,
                "initialFluxPerUnit": 0.0,
                "finalFluxPerUnit": 0.0,
                "stateWasCarried": False,
                "initialProcessedSampleCount": 0,
                "finalProcessedSampleCount": state["processedSampleCount"],
                "firstSaturationAbsoluteSample": -1,
            },
        }

    sample_rate = case["sampleRateHz"]
    frequency = case["frequencyHz"]
    interval = 1.0 / sample_rate
    knee_flux = math.sqrt(2.0) * settings["kneePointVoltageRms"] / (
        2.0 * math.pi * frequency
    )
    total_resistance = (
        settings["secondaryWindingResistanceOhm"] + settings["burdenResistanceOhm"]
    )
    inductance = settings["burdenInductanceMilliHenries"] / 1000.0
    maximum_flux = settings["maximumFluxPerUnit"] * knee_flux
    carried = bool(initial and initial["initialized"])
    state = initial or {
        "initialized": True,
        "fluxLinkageVoltSeconds": settings["remanencePercent"] / 100.0 * knee_flux,
        "previousSecondaryCurrentA": 0.0,
        "previousSecondaryVoltageV": 0.0,
        "processedSampleCount": 0,
    }
    previous_flux = max(
        -maximum_flux, min(maximum_flux, state["fluxLinkageVoltSeconds"])
    )
    previous_secondary = state["previousSecondaryCurrentA"]
    previous_voltage = state["previousSecondaryVoltageV"]
    initial_count = state["processedSampleCount"]
    initial_flux_pu = previous_flux / knee_flux

    secondary: list[float] = []
    flux_per_unit: list[float] = []
    excitation: list[float] = []
    saturated_count = 0
    first_saturated = -1
    maximum_absolute_flux = abs(initial_flux_pu)
    maximum_excitation = 0.0
    maximum_voltage = 0.0
    minimum_ratio = 1.0
    ratio_measured = False
    ideal_square = secondary_square = error_square = 0.0

    for index, ideal_sample in enumerate(ideal):
        candidate_secondary = ideal_sample - excitation_current(
            previous_flux, knee_flux, settings
        )
        candidate_flux = previous_flux
        candidate_voltage = previous_voltage
        for _ in range(iterations):
            derivative = (candidate_secondary - previous_secondary) / interval
            candidate_voltage = total_resistance * candidate_secondary + inductance * derivative
            candidate_flux = previous_flux + 0.5 * (
                previous_voltage + candidate_voltage
            ) * interval
            candidate_flux = max(-maximum_flux, min(maximum_flux, candidate_flux))
            candidate_excitation = excitation_current(candidate_flux, knee_flux, settings)
            target_secondary = ideal_sample - candidate_excitation
            candidate_secondary += relaxation * (
                target_secondary - candidate_secondary
            )

        candidate_excitation = excitation_current(candidate_flux, knee_flux, settings)
        candidate_secondary = ideal_sample - candidate_excitation
        candidate_voltage = total_resistance * candidate_secondary + inductance * (
            candidate_secondary - previous_secondary
        ) / interval

        secondary.append(candidate_secondary)
        flux_per_unit.append(candidate_flux / knee_flux)
        excitation.append(candidate_excitation)
        absolute_flux = abs(candidate_flux / knee_flux)
        maximum_absolute_flux = max(maximum_absolute_flux, absolute_flux)
        maximum_excitation = max(maximum_excitation, abs(candidate_excitation))
        maximum_voltage = max(maximum_voltage, abs(candidate_voltage))
        if absolute_flux >= 1.0:
            saturated_count += 1
            if first_saturated < 0:
                first_saturated = index
        if abs(ideal_sample) >= settings["ratedSecondaryCurrentA"] * 0.1:
            ratio = abs(candidate_secondary) / abs(ideal_sample)
            minimum_ratio = min(minimum_ratio, ratio) if ratio_measured else ratio
            ratio_measured = True

        ideal_square += ideal_sample * ideal_sample
        secondary_square += candidate_secondary * candidate_secondary
        error_square += (candidate_secondary - ideal_sample) ** 2
        previous_secondary = candidate_secondary
        previous_voltage = candidate_voltage
        previous_flux = candidate_flux

    count = len(ideal)
    ideal_rms = math.sqrt(ideal_square / count)
    secondary_rms = math.sqrt(secondary_square / count)
    final_count = initial_count + count
    return {
        "ideal": ideal,
        "secondary": secondary,
        "fluxPerUnit": flux_per_unit,
        "excitationCurrentA": excitation,
        "finalState": {
            "initialized": True,
            "fluxLinkageVoltSeconds": previous_flux,
            "previousSecondaryCurrentA": previous_secondary,
            "previousSecondaryVoltageV": previous_voltage,
            "processedSampleCount": final_count,
        },
        "diagnostics": {
            "enabled": True,
            "saturated": saturated_count > 0,
            "saturatedSampleCount": saturated_count,
            "firstSaturatedSample": first_saturated,
            "firstSaturationMilliseconds": (
                first_saturated * 1000.0 / sample_rate
                if first_saturated >= 0
                else None
            ),
            "maximumAbsoluteFluxPerUnit": maximum_absolute_flux,
            "maximumExcitationCurrentA": maximum_excitation,
            "maximumSecondaryVoltageV": maximum_voltage,
            "idealRmsA": ideal_rms,
            "secondaryRmsA": secondary_rms,
            "rmsMagnitudeErrorPercent": (
                100.0 * (secondary_rms - ideal_rms) / ideal_rms
                if ideal_rms > 1e-12
                else 0.0
            ),
            "waveformErrorPercent": (
                100.0 * math.sqrt(error_square / count) / ideal_rms
                if ideal_rms > 1e-12
                else 0.0
            ),
            "minimumMagnitudeRatio": minimum_ratio if ratio_measured else 1.0,
            "initialFluxPerUnit": initial_flux_pu,
            "finalFluxPerUnit": previous_flux / knee_flux,
            "stateWasCarried": carried and initial_count > 0,
            "initialProcessedSampleCount": initial_count,
            "finalProcessedSampleCount": final_count,
            "firstSaturationAbsoluteSample": (
                initial_count + first_saturated if first_saturated >= 0 else -1
            ),
        },
    }
