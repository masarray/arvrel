# AVR IEC 61850 SAS Laboratory Validation

> Current public package: **ARVREL v0.1.0-beta.6**. This is a laboratory interoperability/behavior guide for the virtual AVR/OLTC IED, not a commissioning acceptance contract. All OLTC outputs remain virtual. See [`CURRENT_STATUS.md`](CURRENT_STATUS.md) for the canonical overall product status.

The reusable IEC 61850 network stack is supplied by the sibling `ARIEC61850` project. The beta.6 release workflow pins:

```text
masarray/ARIEC61850
0d0aa4e31c17f9e5a10901ad52fa75e9c4581daf
```

## Source layout

For source execution use sibling folders:

```text
Git/
├─ ARIEC61850/
└─ arvrel/
```

Check out the pinned engine revision when reproducing the selected release from source.

## Start the virtual IED

1. Start ARVREL v0.1.0-beta.6 and select **AVR · OLTC Controller**.
2. On the virtual device select **REMOTE**.
3. Open **MENU → Communication / IEC 61850**.
4. Bind the intended laboratory interface/port (TCP 102 is the normal MMS service port) and start the server according to local permissions.
5. Connect the authorized SAS/IEC 61850 client to the Windows PC laboratory IPv4 address.

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

GI and integrity reporting are supported by the pinned ARIEC61850 reporting runtime.

## Control model

The AVR advertises SBO-enhanced style control metadata (`ctlModel = 4`) with a 5 s SBO timeout. Selection state is per MMS association.

### Tap control — `YLTC1.TapChg`

`ctlVal` mapping:

| ctlVal | Meaning | ARVREL action |
| ---: | --- | --- |
| 0 | stop | cancel current virtual motor travel, keep completed tap feedback |
| 1 | lower | command one virtual tap lower |
| 2 | higher | command one virtual tap higher / RAISE |

RAISE/LOWER requires **REMOTE + MANUAL**, no active LTC block, no virtual motor travel, and available tap range.

Recommended laboratory sequence:

1. Select `YLTC1.TapChg` using `SBO` or `SBOw`.
2. Operate within 5 s using the matching `ctlVal`/`ctlNum` for enhanced selection.
3. Observe the virtual RAISE/LOWER output.
4. Observe `transInd = true` during virtual motor travel.
5. Observe `TapPos` change only when virtual travel completes.

`Cancel` releases an outstanding selection. `ctlVal=0` is the modeled STOP command.

### AVR mode — `ATCC1.Auto`

- `ctlVal = true` → AUTO
- `ctlVal = false` → MANUAL
- requires REMOTE authority and SBO/SBOw → Oper

### External regulator block — `ATCC1.LTCBlk`

- `ctlVal = true` → block virtual tap operation
- `ctlVal = false` → release block
- requires REMOTE authority and SBO/SBOw → Oper
- applying block cancels pending virtual motor travel without changing completed tap feedback

## Writable settings

Ordinary MMS setting writes are accepted only in REMOTE and while the virtual OLTC is not moving.

| Setting | IEC reference | Unit / meaning |
| --- | --- | --- |
| Voltage setpoint | `ATCC1.BndCtr.setMag.f` | V secondary |
| Total bandwidth | `ATCC1.BndWid.setMag.f` | V total width |
| T1 delay | `ATCC1.CtlDlTmms.setVal` | ms |

Setting changes are validated by the AVR application model, applied on the AVR/WPF authority path, and reflected by the HMI and MMS model.

## Positive laboratory sequence

1. Use the simulated transformer/bench input at a known voltage/current condition and start the IEC server.
2. Put the front panel in REMOTE.
3. From the laboratory client, select + operate `ATCC1.Auto = false`; verify HMI `REMOTE · MANUAL`.
4. Select + operate `YLTC1.TapChg = 2`; verify virtual RAISE, motor travel, then TapPos +1.
5. Select + operate `YLTC1.TapChg = 1`; verify virtual LOWER, motor travel, then TapPos -1.
6. Select + operate `ATCC1.LTCBlk = true`; verify BLOCK and tap commands are inhibited.
7. Release `LTCBlk`.
8. Write `BndCtr`, `BndWid`, and `CtlDlTmms`; verify HMI configuration and online reads.
9. Set `Auto = true`, move simulated voltage outside band, and verify automatic T1/tap response plus BRCB/URCB reporting.

## Negative / interlock tests

Expected rejection cases:

- process control while front panel is LOCAL;
- tap RAISE/LOWER while AUTO;
- tap command while `LTCBlk` is active;
- second tap operation while virtual motor travel is active;
- RAISE at maximum tap / LOWER at minimum tap;
- Oper without a valid selection;
- Oper after the 5 s SBO timeout;
- enhanced Oper whose `ctlVal` or `ctlNum` differs from SBOw;
- setting write while LOCAL;
- setting write while tap changer is moving;
- out-of-range setpoint, bandwidth, or T1.

The HMI Communication page exposes accepted/rejected control counters and the latest control audit message.

## Evidence to preserve

- ARVREL version and package/commit;
- pinned ARIEC61850 commit;
- virtual transformer/tap initial state;
- LOCAL/REMOTE and AUTO/MANUAL state;
- DataSet/report/control reference used;
- SBO/SBOw/Oper outcome and audit text;
- virtual tap transition/final feedback;
- accepted/rejected command reason;
- client/tool identity if relevant and non-sensitive.

## Safety boundary

This feature creates a real IEC 61850 TCP/MMS endpoint for authorized laboratory interoperability testing, but every OLTC command terminates at the virtual AVR simulator. ARVREL provides no physical motor-drive, switching, or primary-equipment authority.

This guide does not claim IEC 61850 conformance certification, formal SAS commissioning acceptance, IEC 60255 type testing, or permission to connect to an operational substation network.
