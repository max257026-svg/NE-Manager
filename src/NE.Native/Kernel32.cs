using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Native;

/// <summary>
/// Kernel32.dll —— 文件/卷/进程/内存/设备 IO 原生接口。
/// </summary>
internal static partial class Kernel32
{
    private const string Lib = "kernel32.dll";

    // ==================== 文件 ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateFile(
        string lpFileName,
        uint dwDesiredAccess,
        uint dwShareMode,
        IntPtr lpSecurityAttributes,
        uint dwCreationDisposition,
        uint dwFlagsAndAttributes,
        IntPtr hTemplateFile);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadFile(
        IntPtr hFile, IntPtr lpBuffer, uint numberOfBytesToRead,
        out uint numberOfBytesRead, IntPtr overlapped);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteFile(
        IntPtr hFile, IntPtr lpBuffer, uint numberOfBytesToWrite,
        out uint numberOfBytesWritten, IntPtr overlapped);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetFilePointerEx(
        IntPtr hFile, long liDistanceToMove, out long newFilePointer, uint dwMoveMethod);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetFileSizeEx(IntPtr hFile, out long fileSize);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FlushFileBuffers(IntPtr hFile);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr hObject);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeviceIoControl(
        IntPtr hDevice, uint dwIoControlCode, IntPtr lpInBuffer, uint nInBufferSize,
        IntPtr lpOutBuffer, uint nOutBufferSize, out uint lpBytesReturned, IntPtr lpOverlapped);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MoveFileEx(
        string lpExistingFileName, string? lpNewFileName, uint dwFlags);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteFile(string lpFileName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RemoveDirectory(string lpPathName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateDirectory(string lpPathName, IntPtr lpSecurityAttributes);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint GetFileAttributes(string lpFileName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetFileAttributes(string lpFileName, uint dwFileAttributes);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetFileTime(
        IntPtr hFile, out long creationTime, out long lastAccessTime, out long lastWriteTime);

    // ---- 备用数据流 (NTFS ADS) 枚举 ----

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindFirstStreamW(
        string lpFileName, int infoLevel, out WIN32_FIND_STREAM_DATA lpFindStreamData, uint dwFlags);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindNextStreamW(
        IntPtr hFindStream, out WIN32_FIND_STREAM_DATA lpFindStreamData);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindClose(IntPtr hFindFile);

    // ---- 硬链接 ----

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    // ==================== 卷与磁盘 ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVolumeInformation(
        string rootPathName, StringBuilder volumeNameBuffer, uint volumeNameSize,
        out uint volumeSerialNumber, out uint maxComponentLength, out uint fileSystemFlags,
        StringBuilder fileSystemNameBuffer, uint fileSystemNameSize);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetDiskFreeSpaceEx(
        string directoryName, out ulong freeBytesAvailable, out ulong totalBytes, out ulong totalFreeBytes);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetLogicalDrives();

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint GetDriveType(string lpRootPathName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVolumeNameForVolumeMountPoint(
        string volumeMountPoint, StringBuilder volumeName, uint bufferLength);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetVolumePathNamesForVolumeName(
        string volumeName, StringBuilder volumePathNames, uint bufferLength, out uint returnLength);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr FindFirstVolume(StringBuilder volumeName, uint bufferLength);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindNextVolume(IntPtr findVolume, StringBuilder volumeName, uint bufferLength);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool FindVolumeClose(IntPtr findVolume);

    // ==================== 进程 ====================

    [DllImport(Lib, SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool TerminateProcess(IntPtr process, uint exitCode);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        IntPtr process, IntPtr baseAddress, IntPtr buffer, nuint size, out nuint bytesRead);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetExitCodeProcess(IntPtr process, out uint exitCode);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetCurrentProcessId();

    [DllImport(Lib, SetLastError = true)]
    internal static extern IntPtr GetCurrentProcess();

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsWow64Process(IntPtr process, [MarshalAs(UnmanagedType.Bool)] out bool wow64);

    [DllImport(Lib, SetLastError = true)]
    internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32First(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32Next(IntPtr snapshot, ref PROCESSENTRY32 entry);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32First(IntPtr snapshot, ref MODULEENTRY32 entry);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32Next(IntPtr snapshot, ref MODULEENTRY32 entry);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint QueryDosDevice(string? deviceName, StringBuilder targetPath, uint max);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcess(
        string? lpApplicationName,
        StringBuilder lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct STARTUPINFO
    {
        public uint cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public uint dwX;
        public uint dwY;
        public uint dwXSize;
        public uint dwYSize;
        public uint dwXCountChars;
        public uint dwYCountChars;
        public uint dwFillAttribute;
        public uint dwFlags;
        public ushort wShowWindow;
        public ushort cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PROCESS_INFORMATION
    {
        public IntPtr hProcess;
        public IntPtr hThread;
        public uint dwProcessId;
        public uint dwThreadId;
    }

    // ==================== 系统信息 ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern void GetNativeSystemInfo(out SYSTEM_INFO lpSystemInfo);

    [StructLayout(LayoutKind.Sequential)]
    internal struct SYSTEM_INFO
    {
        public ushort wProcessorArchitecture;
        public ushort wReserved;
        public uint dwPageSize;
        public IntPtr lpMinimumApplicationAddress;
        public IntPtr lpMaximumApplicationAddress;
        public IntPtr dwActiveProcessorMask;
        public uint dwNumberOfProcessors;
        public uint dwProcessorType;
        public uint dwAllocationGranularity;
        public ushort wProcessorLevel;
        public ushort wProcessorRevision;
    }

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORYSTATUSEX
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
}
