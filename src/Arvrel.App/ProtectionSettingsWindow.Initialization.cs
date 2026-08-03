namespace Arvrel.App;

public partial class ProtectionSettingsWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        ApplyFamiliarOvercurrentUx();
        UpdatePreviews();
    }
}
