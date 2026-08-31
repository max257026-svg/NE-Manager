using System.Runtime.InteropServices;

namespace NEManager.Native;

/// <summary>
/// 进程内存操作 P/Invoke 声明。
/// </summary>
internal static class Memory32
{
    private const string KernelLib = "kernel32.dll";

    // ==================== 常量 ====================

    internal const uint PROCESS_ALL_ACCESS = 0x1F0FFF;
    internal const uint MEM_COMMIT = 0x1000;
    internal const uint MEM_RESERVE = 0x2000;
    internal const uint MEM_RELEASE = 0x8000;
    internal const uint PAGE_READWRITE = 0x04;
    internal const uint PAGE_EXECUTE_READWRITE = 0x40;
    internal const uint TH32CS_SNAPPROCESS = 0x02;
    internal const uint TH32CS_SNAPMODULE = 0x08;

    // ==================== 结构体 ====================

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_BASIC_INFORMATION
    {
        public IntPtr BaseAddress;
        public IntPtr AllocationBase;
        public uint AllocationProtect;
        public nint RegionSize;
        public uint State;
        public uint Protect;
        public uint Type;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MEMORY_PROCESSENTRY32
    {
        public uint dwSize;
        public uint cntUsage;
        public uint th32ProcessID;
        public IntPtr th32DefaultHeapID;
        public uint th32ModuleID;
        public uint cntThreads;
        public uint th32ParentProcessID;
        public int pcPriClassBase;
        public uint dwFlags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExeFile;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct MEMORY_MODULEENTRY32
    {
        public uint dwSize;
        public uint th32ModuleID;
        public uint th32ProcessID;
        public uint GlblcntUsage;
        public uint ProccntUsage;
        public IntPtr modBaseAddr;
        public uint modBaseSize;
        public IntPtr hModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string szModule;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szExePath;
    }

    // ==================== P/Invoke ====================

    [DllImport(KernelLib, SetLastError = true)]
    internal static extern IntPtr OpenProcess(uint desiredAccess, [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ReadProcessMemory(
        IntPtr process, IntPtr baseAddress, byte[] buffer, int size, out int bytesRead);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool WriteProcessMemory(
        IntPtr process, IntPtr baseAddress, byte[] buffer, int size, out int bytesWritten);

    [DllImport(KernelLib, SetLastError = true)]
    internal static extern int VirtualQueryEx(IntPtr process, IntPtr address, out MEMORY_BASIC_INFORMATION buffer, int length);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualProtectEx(IntPtr process, IntPtr address, int size, uint newProtect, out uint oldProtect);

    [DllImport(KernelLib, SetLastError = true)]
    internal static extern IntPtr VirtualAllocEx(IntPtr process, IntPtr address, int size, uint allocType, uint protect);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool VirtualFreeEx(IntPtr process, IntPtr address, int size, uint freeType);

    [DllImport(KernelLib, SetLastError = true)]
    internal static extern IntPtr CreateToolhelp32Snapshot(uint flags, uint processId);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32First(IntPtr snapshot, ref MEMORY_PROCESSENTRY32 entry);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Process32Next(IntPtr snapshot, ref MEMORY_PROCESSENTRY32 entry);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32First(IntPtr snapshot, ref MEMORY_MODULEENTRY32 entry);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool Module32Next(IntPtr snapshot, ref MEMORY_MODULEENTRY32 entry);

    [DllImport(KernelLib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseHandle(IntPtr handle);
}
