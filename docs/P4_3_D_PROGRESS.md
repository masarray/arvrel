# P4.3-D — Angle context menu

Updated: 2026-08-05

## Objective

Provide Quick CMC-style right-click engineering operations on the Direct injection `Angle (°)` column without bypassing the visible editor, validation, waveform generation, coherent-cycle rebuild, protection, or evidence paths.

## Context-menu contract

- [x] Menu opens only from an `Angle (°)` DataGrid cell
- [x] Right-click selects and focuses the clicked angle cell
- [x] `Zero` sets only the selected signal angle to 0°
- [x] `Line Angle` sets only the selected signal to L1=0°, L2=-120°, L3=120°; neutral=0°
- [x] `Balanced Angles` operates on the selected voltage or current phase group
- [x] Balanced operation retains the clicked phase angle as the anchor
- [x] `Reverse Rotation` keeps L1 and swaps L2/L3 angles
- [x] Balanced/Reverse are disabled for VN and IN
- [x] `Copy Table` exports frequency and complete 4I+4V table as versioned TSV
- [x] `Paste Table` accepts ARVREL TSV or compatible spreadsheet headers
- [x] Paste requires all eight signals and rejects duplicates or invalid values atomically

## Runtime and editor behavior

- [x] Bulk edits suppress per-cell debounce churn
- [x] One validated profile apply occurs after each menu operation
- [x] Running output follows the existing coherent-cycle rebuild
- [x] Last valid source remains active when an operation or paste is invalid
- [x] Preset selection clears and the pending profile name becomes `Custom injection` after a successful manual operation
- [x] No direct writes to waveform, measurement, phasor, protection, process-bus, or evidence state
- [x] P4.3-A relay/source separation remains unchanged
- [x] P4.3-B Start/Stop authority remains unchanged
- [x] P4.3-C compact modeless layout remains unchanged

## Deterministic validation

- [x] Standard line-angle mapping tests
- [x] Selected-phase anchor balancing tests
- [x] Angle normalization boundary test
- [x] Arbitrary-angle reverse-rotation test
- [x] Neutral rejection test
- [x] TSV serialize/parse round-trip test
- [x] Spreadsheet alias test
- [x] Incomplete-table rejection test
- [x] Duplicate-signal rejection test
- [x] Frequency-range rejection test

## Automated gates

- [x] Restore
- [x] Windows application build
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] NuGet vulnerability audit
- [x] Static-site validation
- [x] Changed-file scope review: one Direct-grid hook, one WPF context-menu partial, two deterministic domain helpers, two test files, and this progress document
- [x] Ready-for-review and squash merge through PR #51
- [x] Main commit `22c35e1755975dbaced6daab57e70574e093cf45`

## Manual Windows QA

- [ ] Right-click outside Angle column does not open the menu
- [ ] Right-click an Angle cell selects the correct row
- [ ] Zero changes only the selected angle
- [ ] Line Angle applies the expected phase-specific angle
- [ ] Balanced Angles preserves the clicked phase as anchor
- [ ] Reverse Rotation swaps L2/L3 without moving L1
- [ ] VN/IN disable Balanced and Reverse Rotation
- [ ] Copy Table pastes cleanly into a spreadsheet
- [ ] Paste Table restores frequency and all eight channels
- [ ] Malformed clipboard text leaves the current editor/source untouched
- [ ] Operations while RUNNING trigger coherent rebuild without stopping injection
- [ ] Context menu works in both Main INJECT workspace and modeless Advanced Injection Window
