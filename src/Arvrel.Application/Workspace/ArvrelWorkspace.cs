using Arvrel.Application.Laboratory;
using Arvrel.Protection;

namespace Arvrel.Application.Workspace;

public enum WorkspaceSourceMode
{
    InternalLaboratory,
    LiveProcessBus,
    CaptureReplay
}

/// <summary>
/// Platform-neutral workspace state shared by current and future presentation layers.
/// Platform-specific capture implementations remain outside this project.
/// </summary>
public sealed class ArvrelWorkspace
{
    public ArvrelWorkspace(ProtectionSettings settings)
    {
        InternalLab = new InternalLabSession(settings);
    }

    public InternalLabSession InternalLab { get; }
    public WorkspaceSourceMode SourceMode { get; private set; } = WorkspaceSourceMode.InternalLaboratory;
    public bool ExternalSourceRunning { get; private set; }

    public void SelectSource(WorkspaceSourceMode sourceMode)
    {
        SourceMode = sourceMode;
        if (sourceMode != WorkspaceSourceMode.InternalLaboratory)
            InternalLab.SetRunning(false);
        else
            ExternalSourceRunning = false;
    }

    public void SetExternalSourceRunning(bool running)
    {
        if (SourceMode == WorkspaceSourceMode.InternalLaboratory && running)
            throw new InvalidOperationException("An external source cannot run while the internal laboratory is selected.");

        ExternalSourceRunning = running;
        if (running)
            InternalLab.SetRunning(false);
    }

    public void StopAll()
    {
        InternalLab.SetRunning(false);
        ExternalSourceRunning = false;
    }
}
