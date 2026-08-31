using System.Runtime.InteropServices;

namespace NEManager.Native;

[StructLayout(LayoutKind.Sequential)]
internal struct LUID
{
    public uint LowPart;
    public int HighPart;

    public long Value => ((long)HighPart << 32) | LowPart;
}

[StructLayout(LayoutKind.Sequential)]
internal struct LUID_AND_ATTRIBUTES
{
    public LUID Luid;
    public uint Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_PRIVILEGES
{
    public uint PrivilegeCount;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
    public LUID_AND_ATTRIBUTES[] Privileges;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_ELEVATION
{
    public int TokenIsElevated;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_USER
{
    public SID_AND_ATTRIBUTES User;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TOKEN_STATISTICS
{
    public LUID TokenId;
    public LUID AuthenticationId;
    public long ExpirationTime;
    public int TokenType;
    public int ImpersonationLevel;
    public uint DynamicCharged;
    public uint DynamicAvailable;
    public uint GroupCount;
    public uint PrivilegeCount;
    public LUID ModifiedId;
}

[StructLayout(LayoutKind.Sequential)]
internal struct TOKEN_MANDATORY_LABEL
{
    public SID_AND_ATTRIBUTES Label;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SID_AND_ATTRIBUTES
{
    public IntPtr Sid;
    public uint Attributes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct ACL
{
    public byte AclRevision;
    public byte Sbz1;
    public ushort AclSize;
    public ushort AceCount;
    public ushort Sbz2;
}

/// <summary>GetNamedSecurityInfo / BuildExplicitAccessWithName 使用的对象类型。</summary>
internal enum SE_OBJECT_TYPE
{
    SE_UNKNOWN_OBJECT_TYPE = 0,
    SE_FILE_OBJECT,
    SE_SERVICE,
    SE_PRINTER,
    SE_REGISTRY_KEY,
    SE_LMSHARE,
    SE_KERNEL_OBJECT,
    SE_WINDOW_OBJECT,
    SE_DS_OBJECT,
    SE_DS_OBJECT_ALL,
    SE_PROVIDER_DEFINED_OBJECT,
    SE_WMIGUID_OBJECT,
    SE_REGISTRY_WOW64_32KEY,
    SE_REGISTRY_WOW64_64KEY
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct EXPLICIT_ACCESS
{
    public uint grfAccessPermissions;
    public uint grfAccessMode;
    public uint grfInheritance;
    public TRUSTEE Trustee;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct TRUSTEE
{
    public IntPtr pMultipleTrustee;
    public int MultipleTrusteeOperation;
    public int TrusteeForm;
    public int TrusteeType;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string ptstrName;
}

internal enum TRUSTEE_FORM
{
    TRUSTEE_IS_SID = 0,
    TRUSTEE_IS_NAME = 1
}

internal enum TRUSTEE_TYPE
{
    TRUSTEE_IS_UNKNOWN = 0,
    TRUSTEE_IS_USER = 1,
    TRUSTEE_IS_GROUP = 2,
    TRUSTEE_IS_DOMAIN = 3,
    TRUSTEE_IS_ALIAS = 4,
    TRUSTEE_IS_WELL_KNOWN_GROUP = 5,
    TRUSTEE_IS_DELETED = 6,
    TRUSTEE_IS_INVALID = 7,
    TRUSTEE_IS_COMPUTER = 8
}

internal enum ACCESS_MODE
{
    NOT_USED_ACCESS = 0,
    GRANT_ACCESS,
    SET_ACCESS,
    DENY_ACCESS,
    REVOKE_ACCESS,
    SET_AUDIT_SUCCESS,
    SET_AUDIT_FAILURE
}

/// <summary>ACL 中的 ACE 头，遍历 ACL 时使用。</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct ACE_HEADER
{
    public byte AceType;
    public byte AceFlags;
    public ushort AceSize;
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROCESSENTRY32
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
internal struct MODULEENTRY32
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

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct WIN32_FIND_STREAM_DATA
{
    public long StreamSize;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 296)]
    public string cStreamName;
}

[StructLayout(LayoutKind.Sequential)]
internal struct DISK_GEOMETRY
{
    public long Cylinders;
    public uint MediaType;
    public uint TracksPerCylinder;
    public uint SectorsPerTrack;
    public uint BytesPerSector;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SERVICE_STATUS
{
    public uint dwServiceType;
    public uint dwCurrentState;
    public uint dwControlsAccepted;
    public uint dwWin32ExitCode;
    public uint dwServiceSpecificExitCode;
    public uint dwCheckPoint;
    public uint dwWaitHint;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct QUERY_SERVICE_CONFIG
{
    public uint dwServiceType;
    public uint dwStartType;
    public uint dwErrorControl;
    public string lpBinaryPathName;
    public string lpLoadOrderGroup;
    public uint dwTagId;
    public string lpDependencies;
    public string lpServiceStartName;
    public string lpDisplayName;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ENUM_SERVICE_STATUS_PROCESS
{
    public string lpServiceName;
    public string lpDisplayName;
    public SERVICE_STATUS_PROCESS ServiceStatusProcess;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SERVICE_STATUS_PROCESS
{
    public uint dwServiceType;
    public uint dwCurrentState;
    public uint dwControlsAccepted;
    public uint dwWin32ExitCode;
    public uint dwServiceSpecificExitCode;
    public uint dwCheckPoint;
    public uint dwWaitHint;
    public uint dwProcessId;
    public uint dwServiceFlags;
}

[StructLayout(LayoutKind.Sequential)]
internal struct VIRTUAL_STORAGE_TYPE
{
    public uint DeviceId;
    public Guid VendorId;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct OPEN_VIRTUAL_DISK_PARAMETERS
{
    public uint Version;
    public uint RWDepth;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct ATTACH_VIRTUAL_DISK_PARAMETERS
{
    public uint Version;
    public uint Reserved;
}

[StructLayout(LayoutKind.Sequential)]
internal struct SYSTEM_HANDLE_INFORMATION
{
    public uint ProcessId;
    public byte ObjectTypeNumber;
    public byte Flags;
    public ushort Handle;
    public IntPtr Object;
    public uint GrantedAccess;
}
