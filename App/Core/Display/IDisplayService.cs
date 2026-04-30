namespace AuviWin.Core.Display;

public interface IDisplayService
{
    DisplayTopologySnapshot CaptureCurrentTopology();
    void ApplyTopology(DisplayTopologySnapshot snapshot);
}
