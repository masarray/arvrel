# P16 — Unified OCR / AVR / Transformer IED Selector

P16 closes the shell-integration gap between the already merged OCR/AVR laboratory and the P8–P15 Transformer Differential stack.

## User-facing result

The existing top-level **IED** selector becomes the single public entry point for:

1. `Protection Relay · OCR`
2. `AVR · OLTC Controller`
3. `Transformer Differential · 87T / REF`

The transformer function is no longer discoverable only through an unlabeled toolbar icon.

## Integration boundary

P16 does not add or copy any protection/control algorithm.

- OCR continues through the existing feeder protection workspace.
- AVR continues through the existing `AvrWorkspaceControl` and AVR simulation/MMS runtime.
- Transformer Differential opens the existing P12–P15 `TransformerIedWindow` against the same `SmvProcessBusController` owned by `MainWindow`.

The transformer window still initializes P14 external-fault/CT-saturation practitioner controls and the P15 deterministic public self-test before it is shown.

## Source lifecycle

Selecting AVR retains the existing PR #89 behavior: internal injection is stopped and an active process-bus source is stopped before entering the AVR laboratory.

Selecting Transformer Differential does **not** stop an active process-bus source because paired live SV is a valid transformer input. If AVR injection is active, it is stopped before entering the transformer workspace.

Selecting OCR restores the existing OCR workspace and hides the transformer landing surface.

## Transformer landing surface

When Transformer Differential is selected, the main shell keeps process-bus source controls available, hides the single-stream OCR body, and shows a compact transformer landing surface with:

- 87T / 87T-HS / REF HV/LV identity;
- the virtual-output safety boundary;
- an explicit `Open 87T / REF workspace` action;
- first-test guidance for `RUN 10-SCENARIO SELF-TEST`;
- reminder that Live/Replay protection requires two distinct HV/LV SV streams.

Selecting Transformer Differential also opens the practitioner window immediately. Closing it leaves the transformer choice selected and the landing surface visible, so the shell state remains truthful and the workspace can be reopened without changing IED selection.

## Compatibility

`InitializeTransformerIedEntryPoint()` is retained as the startup method name so P12–P15 application lifecycle wiring remains compatible. Its responsibility changes from injecting a hidden icon to upgrading the already-created PR #89 selector.

The original PR #89 selection handler is detached only after the OCR/AVR selector exists. P16 then owns the unified three-choice handler while continuing to call the existing `SelectIed(...)` path for OCR and AVR.

## Safety

ARVREL remains a virtual laboratory. P16 adds no physical trip contact, GOOSE trip, MMS breaker control, OLTC motor authority, or autonomous switching path.
