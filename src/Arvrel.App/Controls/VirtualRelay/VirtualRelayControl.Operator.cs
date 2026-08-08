using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Arvrel.App.Controls.VirtualRelay;

public partial class VirtualRelayControl
{
    private TextBlock?[]? _programmableLampLabelBlocks;

    public void SetProgrammableLampLabels(IReadOnlyList<string> labels)
    {
        ArgumentNullException.ThrowIfNull(labels);
        EnsureProgrammableLampLabelBlocks();
        if (_programmableLampLabelBlocks is null)
            return;

        for (var index = 0; index < _programmableLampLabelBlocks.Length; index++)
        {
            if (_programmableLampLabelBlocks[index] is not { } text)
                continue;
            text.Text = index < labels.Count
                ? labels[index].ToUpperInvariant()
                : $"LED {index + 1}";
        }
    }

    private void EnsureProgrammableLampLabelBlocks()
    {
        if (_programmableLampLabelBlocks is not null)
            return;

        var defaults = new[] { "PHASE A", "PHASE B", "PHASE C", "EARTH" };
        var all = LogicalDescendants<TextBlock>(this).ToArray();
        _programmableLampLabelBlocks = new TextBlock?[defaults.Length];

        for (var index = 0; index < defaults.Length; index++)
        {
            _programmableLampLabelBlocks[index] = all.FirstOrDefault(
                text => string.Equals(text.Text, defaults[index], StringComparison.Ordinal));
        }
    }
}
