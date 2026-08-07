using System.Windows;
using System.Windows.Controls;

namespace Arvrel.App;

public partial class TransformerIedWindow
{
    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        EnsureCtEngineeringRows();
    }

    private void EnsureCtEngineeringRows()
    {
        if (HvCtPrimaryText.Parent is not Grid ctGrid || ctGrid.RowDefinitions.Count >= 5)
            return;

        for (var index = 0; index < 5; index++)
            ctGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
    }
}
