# P4.3-H — Relay LED consistency and full-face gloss correction

Updated: 2026-08-05

## Objective

Make HEALTHY and SMV BLOCK use the same physical LED lens language as PICKUP, TRIP, and phase indicators, and replace the small upper gloss patch with a full-face relay-housing reflection aligned to the body edge.

## LED consistency contract

- [x] All faceplate LEDs retain one 12 × 12 circular lens geometry
- [x] All active LEDs share one neutral bezel stroke
- [x] HEALTHY, WARNING, TRIP, and PHASE glows share one blur radius
- [x] HEALTHY, WARNING, TRIP, and PHASE glows share one opacity
- [x] Only emitted-light hue changes by engineering state
- [x] HEALTHY remains green
- [x] SMV BLOCK remains amber
- [x] TRIP and operated phases remain red where commanded by the existing renderer
- [x] OFF state keeps the same dark inactive lens
- [x] No timer or new LED-state owner is introduced

## Full-face gloss contract

- [x] Replace the fixed 62 px upper strip with a stretch-aligned face reflection
- [x] Gloss follows the relay body's inner edge and corner radius
- [x] Gloss covers the complete molded faceplate
- [x] Gloss is rendered behind labels, LCD, LEDs, and controls
- [x] Gloss never receives pointer input
- [x] Inner bevel and lower casing lip remain above the gloss
- [x] Initialization retries are bounded and ordered after the P4.3-G hardware shell

## Behavior boundary

- [x] No changes to LED state logic
- [x] No changes to pickup, trip, reset, latch, SMV trust, injection, DFT, phasor, evidence, or process bus
- [x] P4.3-F LCD stability and P4.3-G button/housing templates remain intact

## Automated validation

- [ ] Restore
- [ ] Windows application build
- [ ] LED consistency source test
- [ ] Full-face gloss source test
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] Virtual-injection tests
- [ ] NuGet vulnerability audit
- [ ] Static-site validation
- [ ] Changed-file scope review
- [ ] Ready-for-review and squash merge

## Manual Windows QA

- [ ] HEALTHY green has the same lens diameter, bezel, and halo size as active red LEDs
- [ ] SMV BLOCK amber has the same lens diameter, bezel, and halo size as active red LEDs
- [ ] No active LED appears physically larger because of a different glow geometry
- [ ] Full-face gloss reaches the relay body's inner edge
- [ ] Gloss does not wash out ARVREL branding, LCD text, LED labels, or buttons
- [ ] Inner bevel and bottom lip remain visible
- [ ] No overlay blocks mouse input
- [ ] No new flicker or material performance regression
