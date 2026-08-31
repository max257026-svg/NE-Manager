using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Native;

/// <summary>
/// Ntdll.dll —— 未公开但稳定的 NT 层接口：挂起/恢复进程、句柄枚举、对象信息查询。
/// </summary>
internal static partial class NtDll
{
    private const string Lib = "ntdll.dll";

    [DllImport(Lib)]
    internal static extern uint NtSuspendProcess(IntPtr processHandle);

    [DllImport(Lib)]
    internal static extern uint NtResumeProcess(IntPtr processHandle);

    [DllImport(Lib)]
    internal static extern uint NtTerminateProcess(IntPtr processHandle, uint exitStatus);

    [DllImport(Lib)]
    internal static extern uint NtQueryInformationProcess(
        IntPtr processHandle, int processInformationClass,
        IntPtr processInformation, uint processInformationLength, out uint returnLength);

    [DllImport(Lib)]
    internal static extern uint NtQuerySystemInformation(
        int systemInformationClass, IntPtr systemInformation,
        uint systemInformationLength, out uint returnLength);

    [DllImport(Lib)]
    internal static extern uint NtQueryObject(
        IntPtr objectHandle, int objectInformationClass,
        IntPtr objectInformation, uint objectInformationLength, out uint returnLength);

    [DllImport(Lib)]
    internal static extern uint NtQueryInformationFile(
        IntPtr fileHandle, IntPtr ioStatusBlock,
        IntPtr fileInformation, uint length, int fileInformationClass);

    [DllImport(Lib)]
    internal static extern uint NtDuplicateObject(
        IntPtr sourceProcessHandle, IntPtr sourceHandle,
        IntPtr targetProcessHandle, out IntPtr targetHandle,
        uint desiredAccess, uint handleAttributes, uint options);

    [DllImport(Lib)]
    internal static extern uint RtlNtStatusToDosError(uint status);

    [DllImport(Lib)]
    internal static extern uint NtOpenProcess(
        out IntPtr processHandle, uint desiredAccess,
        ref OBJECT_ATTRIBUTES objectAttributes, ref CLIENT_ID clientId);

    [StructLayout(LayoutKind.Sequential)]
    internal struct OBJECT_ATTRIBUTES
    {
        public uint Length;
        public IntPtr RootDirectory;
        public IntPtr ObjectName;
        public uint Attributes;
        public IntPtr SecurityDescriptor;
        public IntPtr SecurityQualityOfService;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct CLIENT_ID
    {
        public IntPtr UniqueProcess;
        public IntPtr UniqueThread;
    }

    // 系统信息类
    internal const int SystemHandleInformation = 16;
    internal const int SystemProcessInformation = 5;
    internal const int SystemExtendedHandleInformation = 64;

    // 进程信息类
    internal const int ProcessBasicInformation = 0;
    internal const int ProcessImageFileName = 27;
    internal const int ProcessCommandLineInformation = 60;
    internal const int ProcessWow64Information = 26;

    // 对象信息类
    internal const int ObjectNameInformation = 1;
    internal const int ObjectTypeInformation = 2;
    internal const int ObjectBasicInformation = 0;

    // NTSTATUS 成功
    internal static bool NtSuccess(uint status) => status <= 0x7FFFFFFF;
}

/// <summary>
/// 虚拟磁盘 (VHD/VHDX) 操作接口。
/// </summary>
internal static partial class VirtDisk
{
    private const string Lib = "virtdisk.dll";

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint OpenVirtualDisk(
        ref VIRTUAL_STORAGE_TYPE virtualStorageType,
        string path,
        uint virtualDiskAccessMask,
        uint flags,
        IntPtr parameters,
        out IntPtr handle);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint AttachVirtualDisk(
        IntPtr virtualDiskHandle,
        IntPtr securityDescriptor,
        uint attachFlags,
        uint providerSpecificFlags,
        IntPtr parameters,
        IntPtr overlapped);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint DetachVirtualDisk(
        IntPtr virtualDiskHandle, uint flags, uint providerSpecificFlags);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint GetVirtualDiskPhysicalPath(
        IntPtr virtualDiskHandle, ref uint diskPathSizeInBytes, StringBuilder diskPath);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint CreateVirtualDisk(
        ref VIRTUAL_STORAGE_TYPE virtualStorageType,
        string path,
        uint virtualDiskAccessMask,
        IntPtr securityDescriptor,
        uint createFlags,
        uint providerSpecificFlags,
        IntPtr parameters,
        IntPtr overlapped,
        out IntPtr handle);
}

/// <summary>
/// WinTrust —— 数字签名校验。
/// </summary>
internal static partial class WinTrust
{
    private const string Lib = "wintrust.dll";

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint WinVerifyTrust(
        IntPtr hwnd, ref Guid actionId, ref WINTRUST_DATA data);

    internal static readonly Guid WINTRUST_ACTION_GENERIC_VERIFY_V2 =
        new(0x00AAC56B, 0xCD44, 0x11D0, 0x8C, 0xC2, 0x00, 0xC0, 0x4F, 0xC2, 0x95, 0xEE);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WINTRUST_FILE_INFO
    {
        public uint cbStruct;
        public string pcwszFilePath;
        public IntPtr hFile;
        public IntPtr pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct WINTRUST_DATA
    {
        public uint cbStruct;
        public IntPtr pPolicyCallbackData;
        public IntPtr pSIPClientData;
        public uint dwUIChoice;
        public uint fdwRevocationChecks;
        public uint dwUnionChoice;
        public IntPtr pFile;
        public uint dwStateAction;
        public IntPtr hWVTStateData;
        public IntPtr pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
    }

    internal const uint WTD_CHOICE_FILE = 1;
    internal const uint WTD_UI_NONE = 2;
    internal const uint WTD_REVOKE_NONE = 0;
    internal const uint WTD_STATEACTION_VERIFY = 0x00000001;
    internal const uint WTD_STATEACTION_CLOSE = 0x00000002;
    internal const uint WTD_CACHE_ONLY_URL_RETRIEVAL = 0x00002000;
}
