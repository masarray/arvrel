# P7.3 — CT persistence and independent validation

## Objective

P7.3 makes nonlinear CT studies reproducible across ARVREL sessions and establishes a second implementation path for numerical validation. Configuration persistence and transient runtime checkpoints remain deliberately separate.

## Versioned profile document

The public WPF Virtual Injection workspace can save and load `*.arvrel-injection.json` files. Schema 1 contains:

- document kind and schema version;
- UTC save timestamp and provenance;
- normalized virtual-injection profile;
- 4I+4V enable, RMS, angle, DC offset, and DC time constant;
- complete CT settings;
- SHA-256 configuration fingerprint.

The loader validates and normalizes the payload before it can replace the active profile. The stored fingerprint must match the normalized profile. Unknown fields, comments, trailing commas, malformed JSON, invalid engineering ranges, altered fingerprints, unsupported older envelopes, and future schema versions are rejected.

A legacy raw `VirtualInjectionProfile` JSON object without an envelope is accepted as schema 0 and migrated in memory. Saving it produces the current schema 1 document.

## Deliberately excluded state

A profile file never stores or restores:

- CT flux linkage;
- previous secondary current or voltage;
- processed-sample or source-sample indexes;
- coherent-window timers;
- protection timers, pickup state, or trip latch;
- UI window state.

Loading a profile therefore configures a new reproducible study condition rather than silently resuming a previous magnetic or relay event.

## Write and failure contract

Saving writes UTF-8 JSON to a unique temporary file in the destination directory, flushes it to disk, and then atomically moves it over the target. Temporary payloads are removed after failure.

Loading follows parse → strict schema validation → engineering validation → fingerprint verification → apply. Any failure occurs before active-laboratory mutation, preserving the last valid profile.

## Independent numerical reference

`validation/ct_reference_validate.py` is a standard-library-only CPython implementation of the documented CT equivalent-circuit algorithm. It does not load .NET, invoke ARVREL assemblies, call the production solver, or consume production-generated runtime output.

The committed vector set covers:

- disabled exact pass-through;
- below-knee 50 Hz operation;
- severe asymmetrical current with positive remanence;
- negative remanence and opposed DC polarity;
- 60 Hz input on the fixed 4 kHz grid with inductive burden;
- a carried runtime state with nonzero flux, previous current/voltage, and sample index.

For each case, the vector stores selected ideal, secondary, flux, and excitation checkpoints plus final state and diagnostics. CI independently recomputes them in Python. The .NET test suite reads the same immutable vectors and compares the production C# model against them.

This bilateral arrangement detects changes in either implementation. Updating expected values requires an explicit reviewed vector change; CI never regenerates golden outputs.

## Validation boundary

Matching an independent implementation increases confidence in equation translation, state handling, saturation onset, off-nominal timing, and diagnostics. It does not convert the engineering-study equivalent circuit into IEC 61869 type-test evidence or a manufacturer-calibrated magnetic digital twin.
