# P4.3-I — Relay surface fit and premium tactile buttons

Updated: 2026-08-05

## Objective

Correct the relay-body gloss geometry and replace the overly deep keypad interaction with a shallower, premium tactile response.

## Relay surface correction

- [x] Full-face gloss explicitly spans every row and column in the relay body grid
- [x] Remove the narrow left-side white strip caused by the default Grid cell
- [x] Reduce gloss alpha so it blends with the gray molded housing
- [x] Preserve the complete body corner radius and inner-edge margin
- [x] Keep gloss behind labels, LCD, LEDs, and controls
- [x] Keep overlay non-interactive

## Premium button correction

- [x] Override the earlier deep button templates after hardware initialization
- [x] Reduce physical base depth from 5 px to 3 px
- [x] Reduce pressed travel from 3 px to 1 px
- [x] Reduce shadow blur and depth
- [x] Use a restrained matte-plastic face gradient
- [x] Use a thinner specular highlight
- [x] Keep a visible but shallow lower lip
- [x] Apply a dedicated light template to RESET TRIP
- [x] Preserve all command handlers and keyboard focus behavior
- [x] Use bounded SystemIdle initialization after the P4.3-G shell

## Behavior boundary

- [x] No changes to relay state, LED state, pickup, trip, reset, latch, SMV trust, injection, DFT, phasor, process bus, or evidence
- [x] No periodic timer or animation clock added
- [x] P4.3-F phasor stability and P4.3-H LED consistency remain intact

## Automated validation

- [ ] Restore
- [ ] Windows application build
- [ ] Full-grid gloss span test
- [ ] Premium template XML validation
- [ ] Shallow travel source test
- [ ] Protection tests
- [ ] Process-bus tests
- [ ] Virtual-injection tests
- [ ] NuGet vulnerability audit
- [ ] Static-site validation
- [ ] CodeQL C# analysis
- [ ] Changed-file scope review
- [ ] Ready-for-review and squash merge

## Manual Windows QA

- [ ] No narrow vertical gloss strip remains on the left side
- [ ] Gloss follows the complete relay body edge
- [ ] Gray housing is not washed out by a white overlay
- [ ] Keypad looks premium and less glossy/overbuilt
- [ ] Key travel is shallow and realistic when pressed
- [ ] RESET TRIP remains clearly tactile but not deeply recessed
- [ ] Icons stay centered during press/release
- [ ] No clipping, input blocking, flicker, or material performance regression
