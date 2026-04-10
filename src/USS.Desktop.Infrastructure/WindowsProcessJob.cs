using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace USS.Desktop.Infrastructure;

internal sealed class WindowsProcessJob : IDisposable
{
    private readonly SafeFileHandle? _handle;

    public WindowsProcessJob()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        _handle = CreateJobObject(IntPtr.Zero, null);
        if (_handle.IsInvalid)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to create Windows job object.");
        }

        var info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            BasicLimitInformation = new JOBOBJECT_BASIC_LIMIT_INFORMATION
            {
                LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
            }
        };

        var length = Marshal.SizeOf<JOBOBJECT_EXTENDED_LIMIT_INFORMATION>();
        var buffer = Marshal.AllocHGlobal(length);
        try
        {
            Marshal.StructureToPtr(info, buffer, false);
            if (!SetInformationJobObject(_handle, JobObjectInfoType.ExtendedLimitInformation, buffer, (uint)length))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to configure Windows job object.");
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public void AddProcess(IntPtr processHandle)
    {
        if (_handle is null || _handle.IsInvalid)
        {
            return;
        }

        if (!AssignProcessToJobObject(_handle, processHandle))
        {
            var error = Marshal.GetLastWin32Error();
            if (error == 5)
            {
                return;
            }

            throw new Win32Exception(error, "Failed to assign process to Windows job object.");
        }
    }

    public void Dispose()
    {
        _handle?.Dispose();
    }

    private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern SafeFileHandle CreateJobObject(IntPtr jobAttributes, string? name);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetInformationJobObject(
        SafeFileHandle job,
        JobObjectInfoType infoType,
        IntPtr jobObjectInfo,
        uint jobObjectInfoLength);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AssignProcessToJobObject(SafeFileHandle job, IntPtr process);

    private enum JobObjectInfoType
    {
        ExtendedLimitInformation = 9
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
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
    private struct IO_COUNTERS
    {
        public ulong ReadOperationCount;
        public ulong WriteOperationCount;
        public ulong OtherOperationCount;
        public ulong ReadTransferCount;
        public ulong WriteTransferCount;
        public ulong OtherTransferCount;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
    {
        public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
        public IO_COUNTERS IoInfo;
        public UIntPtr ProcessMemoryLimit;
        public UIntPtr JobMemoryLimit;
        public UIntPtr PeakProcessMemoryUsed;
        public UIntPtr PeakJobMemoryUsed;
    }
}
