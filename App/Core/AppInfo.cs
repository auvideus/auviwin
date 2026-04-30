using System.Reflection;

namespace AuviWin.Core;

/// <summary>
/// Central source of truth for the application name.
/// The value comes from the assembly name, which is set via &lt;AppDisplayName&gt; in the project file.
/// To rename the app, change &lt;AppDisplayName&gt; in App.csproj — everything else updates automatically.
/// </summary>
public static class AppInfo
{
    public static readonly string Name =
        Assembly.GetExecutingAssembly().GetName().Name ?? "App";
}
