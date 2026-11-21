using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MockRtsp;

/// <summary>
/// Ties processes the app starts to the app itself (Windows job object with "kill on close"): when
/// MockRtsp.exe ends in any way (closed, crashed, or stopped from the debugger), Windows ends them
/// too, so no MediaMTX is left behind holding the RTSP port.
/// </summary>
internal static class ChildProcesses
{
    private const int JobObjectExtendedLimitInformation = 9;
    private const uint JobObjectLimitKillOnJobClose = 0x2000;

    private static readonly object Gate = new();
    private static IntPtr _job;             // kept open for the app's lifetime; Windows closes it when the app ends

    /// <summary>
    /// Ends <paramref name="process"/> when the app ends. Returns null when done, else why not (it then
    /// lives on after a crash, and the next start stops it as a leftover). Does nothing off Windows.
    /// </summary>
    public static string? EndWithThisApp(Process process)
    {
        if (!OperatingSystem.IsWindows())
            return null;

        try
        {
            lock (Gate)
            {
                if (_job == IntPtr.Zero)
                {
                    var job = CreateJobObject(IntPtr.Zero, null);
                    if (job == IntPtr.Zero)
                        return new Win32Exception(Marshal.GetLastWin32Error()).Message;

                    var info = new JobObjectExtendedLimit { Basic = { LimitFlags = JobObjectLimitKillOnJobClose } };
                    if (!SetInformationJobObject(job, JobObjectExtendedLimitInformation, ref info, (uint)Marshal.SizeOf<JobObjectExtendedLimit>()))
                    {
                        string why = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                        CloseHandle(job);
                        return why;
                    }

                    _job = job;
                }

                return AssignProcessToJobObject(_job, process.Handle) ? null : new Win32Exception(Marshal.GetLastWin32Error()).Message;
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception)
        {
            return ex.Message;      // the process already ended
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectBasicLimit
    {
        public long PerProcessUserTimeLimit;
        public long PerJobUserTimeLimit;
        public uint LimitFlags;
        public UIntPtr MinimumWorkingSetSize;
        public UIntPtr MaximumWorkingSetSize;
        public uint ActiveProcessLimit;
        public UIntPtr Affinity;
        public uint PriorityClass;
        public uint SchedulingClass;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JobObjectExtendedLimit
    {
        public JobObjectBasicLimit Basic;
        public IoCounters Io;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr CreateJobObject(IntPtr attributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(IntPtr job, int infoClass, ref JobObjectExtendedLimit info, uint length);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
