# AVR IEC 61850 SAS Validation

This document is the commissioning contract for the ARVREL AVR-230 virtual IED in PR #89.
The network stack is supplied by the sibling `ARIEC61850` project. All OLTC outputs remain virtual.

## Source layout

For source execution use sibling folders:

```text
Git/
├─ ARIEC61850/
└─ arvrel/
```

ARVREL PR #89 currently pins the ARIEC61850 process-control runtime commit
`0e1e2de693bc3f5427f6b1f23fcc6c3e6fe1f60e` (draft ARIEC61850 PR #52).
When running from source, check out both revisions before building.

## Start the virtual IED

1. Start ARVREL and select **AVR · OLTC Controller**.
2. On the virtual device select **REMOTE**.
3. Open **MENU → Communication / IEC 61850**.
4. Bind `0.0.0.0`, TCP port `102`, and start the server.
5. Connect the SAS/IEC 61850 client to the Windows PC IPv4 address on TCP/102.

The IED logical device is `ARVAVR1`.

## Logical model

| Logical node | Purpose |
| --- | --- |
| `LLN0` | DataSets / BRCB / URCB |
| `LPHD1` | Physical-device shell |
| `ATCC1` | Automatic tap-changer controller |
| `YLTC1` | Tap changer / OLTC feedback and control |
| `MMXU1` | Voltage, current, frequency, PF |
| `GGIO1` | Bench source / actuator auxiliary state |

### Main live values

- `ATCC1.CtlV.mag.f` — control voltage
- `ATCC1.LodA.mag.f` — load current
- `ATCC1.Loc.stVal` — LOCAL indication
- `ATCC1.Auto.stVal` — automatic mode
- `ATCC1.LTCBlk.stVal` — remote LTC blocking
- `ATCC1.TapOpR.stVal` / `TapOpL.stVal` — RAISE / LOWER command outputs
- `YLTC1.TapPos.stVal` — tap-position feedback
- `YLTC1.TapChg.valWTr.posVal` — stop/lower/higher state
- `YLTC1.TapChg.valWTr.transInd` — tap transition / motor travel
- `YLTC1.EndPosR.stVal` / `EndPosL.stVal` — end positions
- `YLTC1.OpCnt.stVal` — operation count
- `MMXU1.Vol.mag.f`, `A.mag.f`, `Hz.mag.f`, `PF.mag.f`

## Reports

- `ARVAVR1/LLN0.dsMeas`
- `ARVAVR1/LLN0.dsStatus`
- `ARVAVR1/LLN0.dsSettings`
- BRCB `ARVAVR1/LLN0.BR.rptMeas01`
- URCB `ARVAVR1/LLN0.RP.rptStatus01`

GI and integrity reporting are supported by the ARIEC61850 reporting runtime.

## Control model

The AVR advertises SBO-enhanced style control metadata (`ctlModel = 4`) with a 5 s SBO timeout.
Selection state is per MMS association.

### Tap control — `YLTC1.TapChg`

`ctlVal` mapping:

| ctlVal | Meaning | ARVREL action |
| ---: | --- | --- |
| 0 | stop | cancel current virtual motor travel, keep completed tap feedback |
| 1 | lower | command one tap lower |
| 2 | higher | command one tap higher / RAISE |

RAISE/LOWER requires **REMOTE + MANUAL**, no active LTC block, no motor travel, and available tap range.

Recommended SAS sequence:

1. Select `YLTC1.TapChg` using `SBO` or `SBOw`.
2. Operate within 5 s using the same `ctlVal`/`ctlNum` for enhanced selection.
3. Observe RAISE/LOWER output contact.
4. Observe `transInd = true` during virtual motor travel.
5. Observe `TapPos` change only when motor travel completes.

`Cancel` releases an outstanding selection. `ctlVal=0` is the operational STOP command.

### AVR mode — `ATCC1.Auto`

- `ctlVal = true` → AUTO
- `ctlVal = false` → MANUAL
- requires REMOTE authority and SBO/SBOw → Oper

### External regulator block — `ATCC1.LTCBlk`

- `ctlVal = true` → block tap operation
- `ctlVal = false` → release block
- requires REMOTE authority and SBO/SBOw → Oper
- applying block cancels pending virtual motor travel without changing completed tap feedback

## Writable settings

Ordinary MMS setting writes are accepted only in REMOTE and while the OLTC is not moving.

| Setting | IEC reference | Unit / meaning |
| --- | --- | --- |
| Voltage setpoint | `ATCC1.BndCtr.setMag.f` | V secondary |
| Total bandwidth | `ATCC1.BndWid.setMag.f` | V total width |
| T1 delay | `ATCC1.CtlDlTmms.setVal` | ms |

Setting changes are validated by the AVR application model, then applied on the WPF/AVR authority thread and immediately reflected on the HMI and MMS model.

## Positive test sequence

1. Inject 100 V / 1 A and start the IEC server.
2. Put the front panel in REMOTE.
3. From SAS, select + operate `ATCC1.Auto = false`; verify HMI `REMOTE · MANUAL`.
4. Select + operate `YLTC1.TapChg = 2`; verify RAISE pulse, motor travel, then TapPos +1.
5. Select + operate `YLTC1.TapChg = 1`; verify LOWER pulse, motor travel, then TapPos -1.
6. Select + operate `ATCC1.LTCBlk = true`; verify BLOCK and tap commands are inhibited.
7. Release `LTCBlk`.
8. Write `BndCtr`, `BndWid`, and `CtlDlTmms`; verify HMI configuration and online reads.
9. Set `Auto = true`, inject voltage outside band, and verify automatic T1/tap response plus BRCB/URCB reporting.

## Negative / interlock tests

Expected rejection cases:

- process control while front panel is LOCAL
- tap RAISE/LOWER while AUTO
- tap command while LTCBlk is active
- second tap operation while motor travel is active
- RAISE at maximum tap / LOWER at minimum tap
- Oper without a valid selection
- Oper after the 5 s SBO timeout
- enhanced Oper whose `ctlVal` or `ctlNum` differs from SBOw
- setting write while LOCAL
- setting write while tap changer is moving
- out-of-range setpoint, bandwidth, or T1

The HMI Communication page exposes accepted/rejected control counters and the latest control audit message.

## Safety boundary

This feature creates a real IEC 61850 TCP/MMS endpoint for SAS commissioning, but every OLTC command ends at the virtual AVR simulator. ARVREL does not provide physical motor-drive or field switching authority.
