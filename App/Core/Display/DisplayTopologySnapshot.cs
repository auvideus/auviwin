namespace App.Core.Display;

public sealed record DisplayTopologySnapshot
{
    public required byte[] PathData { get; init; }
    public required byte[] ModeData { get; init; }
    public required DisplaySourceInfo[] ActiveSources { get; init; }
    public required DisplayTargetInfo[] ActiveTargets { get; init; }

    public bool Matches(DisplayTopologySnapshot other)
    {
        if (ActiveSources.Length != other.ActiveSources.Length) return false;
        if (ActiveTargets.Length != other.ActiveTargets.Length) return false;
        var otherSources = new HashSet<DisplaySourceInfo>(other.ActiveSources);
        if (!ActiveSources.All(s => otherSources.Contains(s))) return false;
        var otherTargets = new HashSet<DisplayTargetInfo>(other.ActiveTargets);
        return ActiveTargets.All(t => otherTargets.Contains(t));
    }
}

public sealed record DisplaySourceInfo(uint AdapterLuidLow, int AdapterLuidHigh, uint SourceId);
public sealed record DisplayTargetInfo(uint AdapterLuidLow, int AdapterLuidHigh, uint TargetId);

