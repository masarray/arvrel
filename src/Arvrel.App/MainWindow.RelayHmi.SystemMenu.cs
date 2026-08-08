namespace Arvrel.App;

/// <summary>
/// P1.1 operator-language contract for the OCR virtual relay.
///
/// The core P1 HMI keeps its tested page/state model; this partial maps that model
/// to the vocabulary a protection-relay user expects on a real front panel.
/// No protection, process-bus, injection or reset authority is changed here.
/// </summary>
public partial class MainWindow
{
    // RelayRootMenu / RelayDiagnosticsMenu are readonly arrays by design, but their
    // entries are presentation metadata. Updating the labels here avoids creating
    // a second navigation engine while making the production menu explicit for
    // first-time users coming from physical protection IEDs.
    private readonly bool _relayP11SystemMenuContract = ApplyRelayP11SystemMenuContract();

    private static bool ApplyRelayP11SystemMenuContract()
    {
        RelayRootMenu[0] = new("MEASUREMENTS", Submenu: RelayMenuLevel.Measurements);
        RelayRootMenu[1] = new("PROTECTION", Submenu: RelayMenuLevel.Protection);
        RelayRootMenu[2] = new("EVENT LOG / RECORDS", Submenu: RelayMenuLevel.Records);
        RelayRootMenu[3] = new("SETTINGS & CONFIG", Page: RelayLcdPage.Settings);
        RelayRootMenu[4] = new("SYSTEM / MONITOR I/O", Submenu: RelayMenuLevel.Diagnostics);

        // The existing diagnostics page already exposes the live source plus the
        // MEASURE / PICKUP / TRIP permission states. Present it as the relay's
        // virtual I/O monitor rather than hiding it behind engineering terminology.
        RelayDiagnosticsMenu[0] = new("MONITOR I/O / PROCESS BUS", Page: RelayLcdPage.Diagnostics);
        RelayDiagnosticsMenu[1] = new("DEVICE / SYSTEM INFO", Page: RelayLcdPage.DeviceInfo);

        return true;
    }
}
