# ARVREL Product Requirements Document

## Product definition

ARVREL is a Windows engineering laboratory that behaves as a vendor-neutral numerical protection relay driven by IEC 61850 Sampled Values. It is not a decorative dashboard and not merely an SV analyzer.

The product makes the full decision chain visible in one laptop screen:

```text
SMV reception
→ stream trust assessment
→ channel mapping and scaling
→ two-cycle waveform
→ current measurement
→ 50/51 protection
→ pickup / timing / trip or block
→ event and disturbance evidence
```

## Locked UX direction

- one-screen 62/38 waveform-to-relay layout;
- no long vertical scrolling on 1366×768 and above;
- no oversized typography, bulky cards, glass effects or decorative animation;
- thin dividers, compact controls, engineering typography and restrained status color;
- waveform is stationary like an oscilloscope evidence window;
- pickup and trip markers remain visible;
- relay faceplate is original and vendor-neutral;
- green is reserved for healthy, amber for warning/block, red for trip/error;
- the right-side virtual relay remains fully visible while the user observes waveform causality.

## P0 foundation

- deterministic internal measurement source;
- 50P, 51P, 50N and 51N;
- trip latch and reset;
- phase and earth-fault indications;
- SMV trust contract with separate measurement, pickup and trip permission;
- typed Algorithm Editor policy validation;
- virtual trip only.

## Sibling architecture

`arvrel` is an application repository. `ARIEC61850` remains the source of truth for reusable IEC 61850, SCL, Sampled Values, PCAP and Npcap behavior.

```text
Git/
├── ARIEC61850/
└── arvrel/
```

The application references the sibling core projects when present. Protection logic remains in `Arvrel.Protection` so it is deterministic, UI-independent and directly testable.

## P1 process-bus integration

P1 implements live Npcap capture, classic PCAP and PCAPNG replay, dynamic stream discovery, SCL-assisted binding, fixed-layout fallback, circular samples, RMS measurement, CT context, trust evidence, and JSON export. The adapter translates ARIEC61850 observations into the canonical input:

```text
MeasurementFrame
  Timestamp
  IA / IB / IC
  residual or measured IN
  SmvTrustState
```

Before a trip is permitted, the trust guard evaluates:

- Ethernet and SV decode validity;
- selected stream identity;
- SCL or reviewed manual channel mapping;
- scaling provenance;
- sample rate and timebase stability;
- sample-counter continuity;
- freshness;
- quality;
- configuration revision and expected-versus-observed evidence;
- protection-processing backlog.

Uncertain data must produce a visible reasoned block, never a silent trip.

## Algorithm laboratory

Each element has its own typed DSL definition for input, filtering, pickup, dropout, timing, reset and trip. The safe-laboratory policy requires `smv.allowsTrip` in the final trip expression and forbids file, network, process, reflection, unmanaged and unbounded behavior.

Activation workflow:

```text
Edit → validate → static policy analysis → deterministic tests → stage → shadow compare → activate
```

P1 keeps validation and immutable shadow staging only; compilation, A/B evaluation, and activation remain P2. Arbitrary C# compilation is intentionally not supported.

## Acceptance criteria

- normal load never trips;
- 50P operates after its definite delay;
- 51P integrates IEC standard-inverse time;
- 50N/51N use an independently visible earth operating quantity;
- trip latch survives fault removal until reset;
- phase LEDs identify pickup phases;
- degraded SMV can expose pickup but blocks a new trip request;
- UI refresh never drives the protection timer;
- main workspace needs no page scrolling at the supported minimum size;
- live Npcap and PCAP/PCAPNG sources feed the same protection engine as the deterministic source;
- SCL-unbound, unscaled, stale, discontinuous, or invalid-quality streams expose explicit trust reasons;
- no active GOOSE, MMS control or physical output exists in P1;
- every algorithm staging action records element, time and content hash.

## Safety and public claims

ARVREL is for education, research, approved FAT/SAT preparation and isolated laboratory work. It does not claim IEC 61850 conformance, protection certification, calibrated measurement, functional safety, deterministic real-time performance, or permission to operate an energised power system.
