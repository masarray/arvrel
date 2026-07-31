# P1 validation matrix

P1 is accepted only when the Windows CI workflow restores, builds, and tests ARVREL with a sibling checkout of `masarray/ARIEC61850`.

## Automated coverage

| Area | Evidence |
|---|---|
| Classic PCAP | Ethernet packet timestamps and frame bytes are reconstructed deterministically. |
| PCAPNG | Section, interface, enhanced-packet, timestamp-resolution, and Ethernet link-type handling are exercised. |
| Measurement context | CT primary/secondary conversion and nominal-frequency validation are deterministic. |
| SV runtime | Fixed-layout Sampled Values payloads populate two-cycle rings, RMS values, trust state, and protection input. |
| Protection regression | Existing 50P, 51P, 50N, 51N, trip-latch, reset, and trust-blocking tests remain green. |

## Manual laboratory acceptance

Manual field acceptance remains required for:

- adapter discovery on the target Windows computer;
- authorized isolated Npcap live capture;
- replay of representative PCAP and PCAPNG files;
- SCL-to-wire mapping review;
- known-current injection comparison after CT context is entered;
- confirmation that degraded stream conditions block virtual trip as configured.

ARVREL P1 produces virtual indications only. Passing this matrix does not constitute IEC 61850 conformance, calibrated measurement validation, or protection-IED certification.
