using System.Runtime.InteropServices;

namespace AuviWin.Core.Display;

/// <summary>
/// Captures and restores display topologies using QueryDisplayConfig / SetDisplayConfig.
/// Full path and mode data is persisted so inactive monitors can be re-activated on restore.
/// </summary>
public sealed class DisplayService : IDisplayService
{
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint SDC_APPLY = 0x00000080;
    private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
    private const uint SDC_SAVE_TO_DATABASE = 0x00000200;
    private const uint SDC_ALLOW_CHANGES = 0x00000400;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    public DisplayTopologySnapshot CaptureCurrentTopology()
    {
        const int maxRetries = 3;
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            GetBufferSizes(out var pathCount, out var modeCount);
            var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
            var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];

            int result = QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, nint.Zero);
            if (result == ERROR_INSUFFICIENT_BUFFER) continue;
            if (result != 0) throw new InvalidOperationException($"QueryDisplayConfig failed: {result}");

            var activeSources = paths
                .Take((int)pathCount)
                .Select(p => new DisplaySourceInfo(
                    p.sourceInfo.adapterId.LowPart,
                    p.sourceInfo.adapterId.HighPart,
                    p.sourceInfo.id))
                .Distinct()
                .ToArray();

            var activeTargets = paths
                .Take((int)pathCount)
                .Select(p => new DisplayTargetInfo(
                    p.targetInfo.adapterId.LowPart,
                    p.targetInfo.adapterId.HighPart,
                    p.targetInfo.id))
                .Distinct()
                .ToArray();

            return new DisplayTopologySnapshot
            {
                PathData = SerializePaths(paths, (int)pathCount),
                ModeData = SerializeModes(modes, (int)modeCount),
                ActiveSources = activeSources,
                ActiveTargets = activeTargets
            };
        }
        throw new InvalidOperationException("QueryDisplayConfig consistently returned ERROR_INSUFFICIENT_BUFFER.");
    }

    public void ApplyTopology(DisplayTopologySnapshot snapshot)
    {
        var paths = DeserializePaths(snapshot.PathData);
        var modes = DeserializeModes(snapshot.ModeData);

        int result = SetDisplayConfig(
            (uint)paths.Length, paths,
            (uint)modes.Length, modes,
            SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_SAVE_TO_DATABASE | SDC_ALLOW_CHANGES);

        if (result != 0)
            throw new InvalidOperationException($"SetDisplayConfig failed: {result}");
    }

    // ── Serialization ─────────────────────────────────────────────────────────

    private static byte[] SerializePaths(DISPLAYCONFIG_PATH_INFO[] paths, int count)
    {
        int sz = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>();
        var bytes = new byte[count * sz];
        for (int i = 0; i < count; i++)
        {
            nint ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(paths[i], ptr, false);
                Marshal.Copy(ptr, bytes, i * sz, sz);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        return bytes;
    }

    private static DISPLAYCONFIG_PATH_INFO[] DeserializePaths(byte[] bytes)
    {
        int sz = Marshal.SizeOf<DISPLAYCONFIG_PATH_INFO>();
        if (bytes.Length % sz != 0)
            throw new InvalidOperationException($"Display snapshot path data is corrupt: {bytes.Length} bytes is not a multiple of {sz}.");
        int count = bytes.Length / sz;
        var result = new DISPLAYCONFIG_PATH_INFO[count];
        for (int i = 0; i < count; i++)
        {
            nint ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.Copy(bytes, i * sz, ptr, sz);
                result[i] = Marshal.PtrToStructure<DISPLAYCONFIG_PATH_INFO>(ptr);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        return result;
    }

    private static byte[] SerializeModes(DISPLAYCONFIG_MODE_INFO[] modes, int count)
    {
        int sz = Marshal.SizeOf<DISPLAYCONFIG_MODE_INFO>();
        var bytes = new byte[count * sz];
        for (int i = 0; i < count; i++)
        {
            nint ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.StructureToPtr(modes[i], ptr, false);
                Marshal.Copy(ptr, bytes, i * sz, sz);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        return bytes;
    }

    private static DISPLAYCONFIG_MODE_INFO[] DeserializeModes(byte[] bytes)
    {
        int sz = Marshal.SizeOf<DISPLAYCONFIG_MODE_INFO>();
        if (bytes.Length % sz != 0)
            throw new InvalidOperationException($"Display snapshot mode data is corrupt: {bytes.Length} bytes is not a multiple of {sz}.");
        int count = bytes.Length / sz;
        var result = new DISPLAYCONFIG_MODE_INFO[count];
        for (int i = 0; i < count; i++)
        {
            nint ptr = Marshal.AllocHGlobal(sz);
            try
            {
                Marshal.Copy(bytes, i * sz, ptr, sz);
                result[i] = Marshal.PtrToStructure<DISPLAYCONFIG_MODE_INFO>(ptr);
            }
            finally { Marshal.FreeHGlobal(ptr); }
        }
        return result;
    }

    // ── Buffer size helper ────────────────────────────────────────────────────

    private static void GetBufferSizes(out uint pathCount, out uint modeCount)
    {
        pathCount = 0;
        modeCount = 0;
        int r = GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, ref pathCount, ref modeCount);
        if (r != 0) throw new InvalidOperationException($"GetDisplayConfigBufferSizes failed: {r}");
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("user32.dll")] private static extern int GetDisplayConfigBufferSizes(uint flags, ref uint numPathArrayElements, ref uint numModeInfoArrayElements);
    [DllImport("user32.dll")] private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, nint currentTopologyId);
    [DllImport("user32.dll")] private static extern int SetDisplayConfig(uint numPathArrayElements, DISPLAYCONFIG_PATH_INFO[] pathArray, uint numModeInfoArrayElements, DISPLAYCONFIG_MODE_INFO[] modeInfoArray, uint flags);

    // ── Structs ───────────────────────────────────────────────────────────────

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID { public uint LowPart; public int HighPart; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)] public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL { public uint Numerator; public uint Denominator; }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 48)]
        public byte[] modeInfoData;
    }
}

