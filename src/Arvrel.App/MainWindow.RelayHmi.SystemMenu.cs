namespace Arvrel.App;

/// <summary>
/// P1.2 operator-language contract for the OCR virtual relay.
///
/// The core P1 HMI keeps one tested page/navigation engine. The Diagnostics and
/// DeviceInfo host pages are routed by P1.2 into the richer operator system pages
/// for binary I/O, alarms, access, setting groups and programmable LEDs.
/// </summary>
public partial class MainWindow
{
    private readonly bool _relayP12SystemMenuContract = ApplyRelayP12SystemMenuContract();

    private static bool ApplyRelayP12SystemMenuContract()
    {
        RelayRootMenu[0] = new("MEASUREMENTS", Submenu: RelayMenuLevel.Measurements);
        RelayRootMenu[1] = new("PROTECTION", Submenu: RelayMenuLevel.Protection);
        RelayRootMenu[2] = new("EVENT LOG / RECORDS", Submenu: RelayMenuLevel.Records);
        RelayRootMenu[3] = new("SETTINGS & CONFIG", Page: RelayLcdPage.Settings);
        RelayRootMenu[4] = new("SYSTEM / MONITOR I/O", Submenu: RelayMenuLevel.Diagnostics);

        RelayDiagnosticsMenu[0] = new("MONITOR I/O / ALARMS", Page: RelayLcdPage.Diagnostics);
        RelayDiagnosticsMenu[1] = new("DEVICE / ACCESS / LED", Page: RelayLcdPage.DeviceInfo);

        return true;
    }
}
