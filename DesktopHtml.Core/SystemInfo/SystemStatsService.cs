using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;

namespace DesktopHtml.Core.SystemInfo;

public sealed record MemoryStats(long UsedMb, long TotalMb, double Percent);
public sealed record DiskStats(string Drive, double FreeGb, double TotalGb);
public sealed record BatteryStats(bool HasBattery, int? Percent, bool OnAc);
public sealed record NetworkStats(long ReceivedBytesPerSec, long SentBytesPerSec);

public sealed record SystemStats(
    double CpuPercent,
    MemoryStats Memory,
    IReadOnlyList<DiskStats> Disks,
    BatteryStats Battery,
    NetworkStats Network);

/// <summary>
/// Cheap system statistics via Win32 counters — no PowerShell child processes,
/// no WMI. CPU and network are computed as deltas between calls, so the first
/// reading reports zero for those.
/// </summary>
public sealed class SystemStatsService
{
    private readonly object _gate = new();
    private readonly Stopwatch _sinceLastSample = Stopwatch.StartNew();
    private (long Idle, long Kernel, long User)? _lastCpuTimes;
    private (long Received, long Sent)? _lastNetworkTotals;
    private double _lastCpuPercent;
    private NetworkStats _lastNetworkRates = new(0, 0);

    public SystemStats GetStats()
    {
        lock (_gate)
        {
            var elapsedSeconds = _sinceLastSample.Elapsed.TotalSeconds;

            var cpu = SampleCpu(elapsedSeconds);
            var network = SampleNetwork(elapsedSeconds);
            if (elapsedSeconds >= 0.25)
            {
                _sinceLastSample.Restart();
            }

            return new SystemStats(cpu, SampleMemory(), SampleDisks(), SampleBattery(), network);
        }
    }

    private double SampleCpu(double elapsedSeconds)
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user))
        {
            return _lastCpuPercent;
        }

        var current = (ToLong(idle), ToLong(kernel), ToLong(user));

        if (_lastCpuTimes is { } last && elapsedSeconds >= 0.25)
        {
            var idleDelta = current.Item1 - last.Idle;
            var kernelDelta = current.Item2 - last.Kernel;
            var userDelta = current.Item3 - last.User;
            var total = kernelDelta + userDelta; // kernel time includes idle
            if (total > 0)
            {
                _lastCpuPercent = Math.Clamp(100.0 * (total - idleDelta) / total, 0, 100);
            }

            _lastCpuTimes = current;
        }
        else if (_lastCpuTimes is null)
        {
            _lastCpuTimes = current;
        }

        return Math.Round(_lastCpuPercent, 1);
    }

    private static MemoryStats SampleMemory()
    {
        var status = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        if (!GlobalMemoryStatusEx(ref status))
        {
            return new MemoryStats(0, 0, 0);
        }

        var totalMb = (long)(status.ullTotalPhys / (1024 * 1024));
        var usedMb = totalMb - (long)(status.ullAvailPhys / (1024 * 1024));
        var percent = totalMb > 0 ? Math.Round(100.0 * usedMb / totalMb, 1) : 0;
        return new MemoryStats(usedMb, totalMb, percent);
    }

    private static IReadOnlyList<DiskStats> SampleDisks()
    {
        var disks = new List<DiskStats>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady)
                {
                    continue;
                }

                disks.Add(new DiskStats(
                    drive.Name.TrimEnd('\\'),
                    Math.Round(drive.AvailableFreeSpace / 1_073_741_824.0, 1),
                    Math.Round(drive.TotalSize / 1_073_741_824.0, 1)));
            }
            catch
            {
            }
        }

        return disks;
    }

    private static BatteryStats SampleBattery()
    {
        if (!GetSystemPowerStatus(out var status))
        {
            return new BatteryStats(false, null, true);
        }

        var hasBattery = status.BatteryFlag != 128 && status.BatteryFlag != 255;
        int? percent = status.BatteryLifePercent <= 100 ? status.BatteryLifePercent : null;
        var onAc = status.ACLineStatus == 1 || !hasBattery;
        return new BatteryStats(hasBattery, hasBattery ? percent : null, onAc);
    }

    private NetworkStats SampleNetwork(double elapsedSeconds)
    {
        long received = 0;
        long sent = 0;
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up ||
                    nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                {
                    continue;
                }

                var stats = nic.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
        }
        catch
        {
            return _lastNetworkRates;
        }

        if (_lastNetworkTotals is { } last && elapsedSeconds >= 0.25)
        {
            _lastNetworkRates = new NetworkStats(
                Math.Max(0, (long)((received - last.Received) / elapsedSeconds)),
                Math.Max(0, (long)((sent - last.Sent) / elapsedSeconds)));
            _lastNetworkTotals = (received, sent);
        }
        else if (_lastNetworkTotals is null)
        {
            _lastNetworkTotals = (received, sent);
        }

        return _lastNetworkRates;
    }

    private static long ToLong(FILETIME time) =>
        ((long)time.dwHighDateTime << 32) | (uint)time.dwLowDateTime;

    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME
    {
        public uint dwLowDateTime;
        public int dwHighDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS lpSystemPowerStatus);
}
