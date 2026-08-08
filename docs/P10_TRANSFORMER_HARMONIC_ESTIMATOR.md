# P10 — Transformer H2/H5 Harmonic Estimator

## Objective

P10 closes the measurement-security gap between the paired HV/LV Sampled Values adapter and the transformer differential protection core.

The protection core introduced in P8 already supports second- and fifth-harmonic blocking/restraint, but until P10 those harmonic ratios were only deterministic test inputs. P10 adds a real waveform estimator and a process-bus enrichment layer so `TransformerMeasurementFrame` can carry measured H2/H5 evidence.

P10 still does **not** enable the live transformer trip runtime. The next layer can now do that without substituting zero harmonic content.

## Protection context

Public transformer-protection documentation consistently uses low-order current harmonics as security quantities:

- Schneider Electric MiCOM P64x documents second-harmonic inrush restraint and fifth-harmonic blocking; its published application material includes example settings `Ih(2)%> = 20%` and `Ih(5)%> = 35%`.
- SEL-787 documentation describes harmonic blocking/restraint for transformer inrush, using even harmonics for energization security and fifth harmonic for overexcitation security.
- ARVREL does not reproduce either vendor's proprietary internal filtering or decision logic. It implements a transparent, vendor-neutral measurement primitive and leaves blocking/restraint policy in `TransformerProtectionEngine`.

## Estimator definition

For an integer-cycle waveform window with `N` samples and `Ns` samples/cycle, ARVREL estimates harmonic order `h` using an orthogonal DFT:

```text
Xh = sqrt(2)/N * sum((x[k] - mean(x)) * exp(-j*2*pi*h*k/Ns))
```

`|Xh|` is the RMS magnitude of harmonic order `h`.

P10 calculates:

```text
H2 ratio = |X2| / |X1|
H5 ratio = |X5| / |X1|
```

The default window is one full cycle. This keeps latency bounded and matches the one-cycle measurement window already required by the ARVREL SV runtime. A multi-cycle window remains configurable for laboratory studies.

## Why coherent DFT

The estimator is intentionally simple and auditable:

- deterministic;
- phase-invariant for magnitude ratios;
- amplitude-invariant;
- exact for coherent integer harmonics in a stationary sampled waveform;
- independent H1/H2/H5 channels;
- explicit DC removal;
- no opaque adaptive state;
- no vendor-specific coefficient set.

This is a research/protection-engineering implementation, not a claim that a commercial MiCOM, SEL, Siemens, GE, ABB, or other relay uses the same internal filter.

## DC offset handling

The arithmetic mean of the selected integer-cycle window is removed before the DFT. This prevents a stationary DC component from leaking into the harmonic magnitude calculation in the coherent test domain.

Transient decaying DC is a different problem and remains part of later CT-saturation / transient-security validation.

## Ratio floor

Harmonic percentage becomes numerically meaningless when the fundamental is nearly zero. `TransformerHarmonicEstimatorSettings.MinimumFundamentalRms` therefore defines a ratio floor.

If `|X1|` is below that floor:

- H1/H2/H5 RMS values are still available in the estimate;
- `RatioReliable = false`;
- H2/H1 and H5/H1 are published as zero instead of an exploding numerical ratio.

This is safe for the current architecture because the 87T operating quantity is itself fundamental-current based. A phase with negligible fundamental current cannot independently create a meaningful restrained differential pickup.

## Sampling constraints

Default minimum sampling density is 16 samples/cycle.

H5 must remain below Nyquist, therefore fewer than 11 samples/cycle is categorically invalid. P10 uses a stricter default margin and rejects an insufficient waveform window rather than estimating from partial data.

## Process-bus integration

`TransformerHarmonicProcessBusAdapter` wraps the P9 alignment layer:

```text
HV SV snapshot ----\
                    -> P9 smpCnt/smpSynch alignment
LV SV snapshot ----/             |
                                  v
                         aligned fundamental frame
                                  |
                    H1/H2/H5 DFT on each winding
                                  |
                                  v
                    TransformerMeasurementFrame
                    + HV H2/H5 ratios per phase
                    + LV H2/H5 ratios per phase
```

The harmonic estimator runs only **after** paired-SV synchronization checks pass.

If harmonic estimation fails because the requested waveform window or sampling density is invalid, the adapter returns:

```text
PAIR_HARMONIC_ESTIMATE_INVALID
```

and removes the measurement frame. It never silently substitutes zero harmonic ratios for an invalid waveform.

## One-sample HV/LV skew

P9 may phase-correct a bounded one-sample fundamental phasor offset. P10 estimates **harmonic magnitudes** independently on each source waveform. A pure time shift changes harmonic phase but not harmonic magnitude, so the H2/H1 and H5/H1 ratios do not require the P9 phasor rotation.

## Current security behavior

The P8 protection core currently combines winding harmonic evidence conservatively per phase:

```text
H2 used by 87T = max(H2_HV, H2_LV)
H5 used by 87T = max(H5_HV, H5_LV)
```

That behavior is intentionally unchanged in P10. P10 is a measurement layer, not a redesign of the protection characteristic.

A later research track may compare winding-current harmonic security with a compensated differential-current harmonic estimator. That requires careful treatment of transformer connection compensation at harmonic orders and should not be hidden inside P10.

## Measured ratio validation

`TransformerHarmonicRatios` no longer imposes the previous arbitrary `<= 5` ceiling. It now accepts any finite non-negative measured ratio.

The reason is physical and numerical: a small but still valid fundamental can produce a harmonic ratio above 500%. Settings thresholds remain separately bounded by the protection-settings validation.

## Deterministic validation cases

P10 adds regression coverage for:

1. pure fundamental -> negligible H2/H5;
2. 20% H2 recovery;
3. 35% H5 recovery;
4. simultaneous independent H2/H5 recovery;
5. amplitude and phase invariance;
6. DC offset rejection;
7. configurable two-cycle window;
8. last-complete-window authority;
9. low-fundamental ratio floor;
10. per-phase three-phase ratios;
11. insufficient-window rejection;
12. H5 sampling-density rejection;
13. non-finite sample rejection;
14. paired-SV enrichment with measured H2/H5;
15. invalid multi-cycle harmonic window blocking;
16. measured H2 driving the existing 87T inrush block;
17. measured H5 driving the existing 87T overexcitation block.

## Intentional non-goals

P10 does not add:

- fourth-harmonic logic;
- cross-phase harmonic blocking;
- adaptive frequency tracking;
- decaying-DC compensation;
- CT-saturation harmonic classification;
- compensated differential-current harmonic synthesis;
- live/replay transformer trip runtime;
- practitioner UI;
- IEC 60255 type-test or vendor-equivalence claims.

## Next integration layer

With P8, P9, and P10 stacked, the next safe implementation is the live/replay transformer runtime:

```text
paired SV
-> alignment diagnostics
-> H1/H2/H5 measurement
-> transformer/CT/vector-group engineering
-> TransformerProtectionEngine
-> virtual trip latch
-> evidence snapshot
```

The runtime must preserve all existing trust gates. Any loss of pairing, synchronization, waveform validity, engineering configuration, or source trust must remove trip permission rather than reuse stale harmonic evidence.
