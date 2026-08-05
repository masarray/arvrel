# P4.3-G — True 3D relay hardware styling

Updated: 2026-08-05

## Objective

Replace the previous shadow-only relay treatment with layered physical-depth cues: a raised housing, beveled faces, recessed modules, tactile button thickness, specular highlights, and a visible pressed-state displacement.

## Root cause

The P4.3-F presentation added static shadows and gradients, but the controls still had a single flat surface. A shadow alone communicates distance from the panel; it does not communicate the molded face, chamfer, base thickness, lip, or mechanical travel needed for a convincing three-dimensional control.

## True 3D keypad contract

- [x] Key base/depth is a separate visual layer
- [x] Key face uses a multi-stop vertical material gradient
- [x] Outer edge uses a diagonal light-to-dark bevel
- [x] Upper face includes a restrained specular highlight strip
- [x] Lower edge includes a darker physical lip
- [x] Hover adds a small surface-light response
- [x] Press translates the face by 3 px
- [x] Press reduces the base, lower lip, gloss, and external shadow
- [x] Keyboard focus remains visible
- [x] Disabled state remains legible

## RESET TRIP contract

- [x] Footer RESET TRIP uses a dedicated light molded-button template
- [x] Light face includes its own bevel, gloss, base thickness, and press travel
- [x] Existing icon, label, click handler, and relay-only reset behavior are retained
- [x] Keypad reset remains part of the dark tactile-key family

## Relay housing contract

- [x] Mounting well uses a deeper industrial-panel gradient
- [x] Relay body uses a four-stop matte-plastic face gradient
- [x] Body has a diagonal outer bevel rather than a flat border
- [x] Body has an inner bevel overlay
- [x] Body has a restrained upper sheen
- [x] Body has a visible lower casing lip
- [x] Static body shadow remains only as one component of the depth model

## Recessed module contract

- [x] LCD is wrapped in a dedicated dark recess well
- [x] LCD face remains the existing readable green-grey surface
- [x] Indicator bay is wrapped in the same recess system with a neutral face
- [x] Recesses use top and left inner shade plus bottom highlight
- [x] Original content, padding intent, LED ownership, and LCD presenter remain intact

## Performance and behavior boundary

- [x] Templates and brushes are created once and reused
- [x] Brushes and body effect are frozen
- [x] No animation clock or periodic timer is added
- [x] No changes to protection, trip, pickup, reset, injection, SMV, DFT, phasor, evidence, or process-bus behavior
- [x] P4.3-F LCD phasor stabilization remains unchanged

## Automated validation

- [x] Restore
- [x] Windows application build
- [x] Embedded button-template XML validation
- [x] Layer/source coverage validation
- [x] Protection tests
- [x] Process-bus tests
- [x] Virtual-injection tests
- [x] NuGet vulnerability audit
- [x] Static-site validation
- [x] Changed-file scope review: one WPF presentation partial, one source-validation test, and this document
- [x] Ready-for-review and squash merge through PR #59

## Manual Windows QA

- [ ] Keypad clearly shows face, bevel, base thickness, and lower lip
- [ ] Key press visibly travels downward and returns on release
- [ ] RESET TRIP has the same physical language in a light material
- [ ] Relay body looks molded/raised rather than merely shadowed
- [ ] LCD and indicator bay look recessed into the body
- [ ] Text, LED, and LCD contrast remain readable
- [ ] No overlay blocks mouse input
- [ ] No clipping at 1520×900 or minimum size
- [ ] No flicker or material UI performance regression
