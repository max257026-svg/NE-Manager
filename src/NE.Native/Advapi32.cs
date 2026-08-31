using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Native;

/// <summary>
/// Advapi32.dll —— 令牌特权、安全描述符、服务控制、注册表原生接口。
/// </summary>
internal static partial class Advapi32
{
    private const string Lib = "advapi32.dll";

    // ==================== 令牌与特权 ====================

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetTokenInformation(
        IntPtr tokenHandle,
        int tokenInformationClass,
        IntPtr tokenInformation,
        int tokenInformationLength,
        out int returnLength);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeValue(
        string? lpSystemName, string lpName, out LUID lpLuid);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AdjustTokenPrivileges(
        IntPtr tokenHandle,
        [MarshalAs(UnmanagedType.Bool)] bool disableAllPrivileges,
        IntPtr newState,
        uint bufferLength,
        IntPtr previousState,
        IntPtr returnLength);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DuplicateTokenEx(
        IntPtr hExistingToken,
        uint dwDesiredAccess,
        IntPtr lpTokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr phNewToken);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ImpersonateLoggedOnUser(IntPtr hToken);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool RevertToSelf();

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetThreadToken(IntPtr thread, IntPtr token);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string? lpApplicationName,
        StringBuilder? lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref Kernel32.STARTUPINFO lpStartupInfo,
        out Kernel32.PROCESS_INFORMATION lpProcessInformation);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateProcessWithTokenW(
        IntPtr hToken,
        uint dwLogonFlags,
        string? lpApplicationName,
        StringBuilder? lpCommandLine,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string? lpCurrentDirectory,
        ref Kernel32.STARTUPINFO lpStartupInfo,
        out Kernel32.PROCESS_INFORMATION lpProcessInformation);

    // ==================== SID / 账户 ====================

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ConvertSidToStringSid(IntPtr sid, out IntPtr stringSid);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ConvertStringSidToSid(string stringSid, out IntPtr sid);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupAccountSid(
        string? lpSystemName,
        IntPtr sid,
        StringBuilder? name,
        ref uint cchName,
        StringBuilder? domainName,
        ref uint cchDomainName,
        out int sidNameUse);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupAccountName(
        string? lpSystemName,
        string lpAccountName,
        IntPtr sid,
        ref uint cbSid,
        StringBuilder? domainName,
        ref uint cchDomainName,
        out int sidNameUse);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool IsValidSid(IntPtr sid);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetLengthSid(IntPtr sid);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CreateWellKnownSid(int wellKnownSidType, IntPtr domainSid, IntPtr sid, ref uint cbSid);

    // ==================== 安全描述符 ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint GetNamedSecurityInfo(
        string objectName,
        SE_OBJECT_TYPE objectType,
        uint securityInfo,
        out IntPtr ownerSid,
        out IntPtr groupSid,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint SetNamedSecurityInfo(
        string objectName,
        SE_OBJECT_TYPE objectType,
        uint securityInfo,
        IntPtr ownerSid,
        IntPtr groupSid,
        IntPtr dacl,
        IntPtr sacl);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetSecurityInfo(
        IntPtr handle,
        SE_OBJECT_TYPE objectType,
        uint securityInfo,
        out IntPtr ownerSid,
        out IntPtr groupSid,
        out IntPtr dacl,
        out IntPtr sacl,
        out IntPtr securityDescriptor);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint SetSecurityInfo(
        IntPtr handle,
        SE_OBJECT_TYPE objectType,
        uint securityInfo,
        IntPtr ownerSid,
        IntPtr groupSid,
        IntPtr dacl,
        IntPtr sacl);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSecurityDescriptorOwner(IntPtr sd, out IntPtr owner, [MarshalAs(UnmanagedType.Bool)] out bool ownerDefaulted);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetSecurityDescriptorOwner(IntPtr sd, IntPtr owner, [MarshalAs(UnmanagedType.Bool)] bool ownerDefaulted);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSecurityDescriptorDacl(
        IntPtr sd, [MarshalAs(UnmanagedType.Bool)] out bool daclPresent,
        out IntPtr dacl, [MarshalAs(UnmanagedType.Bool)] out bool daclDefaulted);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetSecurityDescriptorDacl(
        IntPtr sd, [MarshalAs(UnmanagedType.Bool)] bool daclPresent, IntPtr dacl, [MarshalAs(UnmanagedType.Bool)] bool daclDefaulted);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSecurityDescriptorSacl(
        IntPtr sd, [MarshalAs(UnmanagedType.Bool)] out bool saclPresent,
        out IntPtr sacl, [MarshalAs(UnmanagedType.Bool)] out bool saclDefaulted);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetSecurityDescriptorControl(IntPtr sd, out ushort control, out uint revision);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetAce(IntPtr acl, uint aceIndex, out IntPtr ace);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetAclInformation(IntPtr acl, IntPtr info, uint infoLength, int infoClass);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint SetEntriesInAcl(uint countOfExplicitEntries, IntPtr explicitEntries, IntPtr oldAcl, out IntPtr newAcl);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetExplicitEntriesFromAcl(IntPtr acl, out uint count, out IntPtr entries);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeSecurityDescriptor(IntPtr sd, uint revision);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool InitializeAcl(IntPtr acl, uint aclSize, uint aclRevision);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool AddAce(IntPtr acl, uint aclRevision, uint startingAceIndex, IntPtr aceList, uint aceListLength);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteAce(IntPtr acl, uint aceIndex);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint GetSecurityDescriptorLength(IntPtr sd);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MakeAbsoluteSD(
        IntPtr selfRelativeSD, IntPtr absoluteSD, ref uint absoluteSDSize,
        IntPtr dacl, ref uint daclSize, IntPtr sacl, ref uint saclSize,
        IntPtr owner, ref uint ownerSize, IntPtr group, ref uint groupSize);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MakeSelfRelativeSD(IntPtr absoluteSD, IntPtr selfRelativeSD, ref uint bufferSize);

    // ==================== 服务控制管理器 ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr OpenSCManager(string? machineName, string? databaseName, uint desiredAccess);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr OpenService(IntPtr scManager, string serviceName, uint desiredAccess);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern IntPtr CreateService(
        IntPtr scManager, string serviceName, string? displayName,
        uint desiredAccess, uint serviceType, uint startType, uint errorControl,
        string binaryPathName, string? loadOrderGroup, IntPtr tagId,
        string? dependencies, string? serviceStartName, string? password);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteService(IntPtr service);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool CloseServiceHandle(IntPtr handle);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ControlService(IntPtr service, uint control, out SERVICE_STATUS status);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool StartService(IntPtr service, uint numArgs, IntPtr args);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ChangeServiceConfig(
        IntPtr service, uint serviceType, uint startType, uint errorControl,
        string? binaryPathName, string? loadOrderGroup, IntPtr tagId,
        string? dependencies, string? serviceStartName, string? password, string? displayName);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceConfig(IntPtr service, IntPtr config, uint cbBufSize, out uint bytesNeeded);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool QueryServiceStatusEx(
        IntPtr service, int infoLevel, IntPtr buffer, uint cbBufSize, out uint bytesNeeded);

    [DllImport(Lib, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool EnumServicesStatusEx(
        IntPtr scManager, int infoLevel, uint serviceType, uint serviceState,
        IntPtr services, uint cbBufSize, out uint bytesNeeded,
        out uint servicesReturned, ref uint resumeHandle, string? groupName);

    // ==================== 注册表（原生） ====================

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegOpenKeyEx(
        IntPtr hKey, string? subKey, uint options, uint samDesired, out IntPtr result);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegCreateKeyEx(
        IntPtr hKey, string subKey, uint reserved, string? lpClass, uint options,
        uint samDesired, IntPtr securityAttributes, out IntPtr result, out uint disposition);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RegCloseKey(IntPtr hKey);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegEnumKeyEx(
        IntPtr hKey, uint index, StringBuilder lpName, ref uint cchName,
        IntPtr reserved, StringBuilder? lpClass, IntPtr cchClass, out long lastWriteTime);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegEnumValue(
        IntPtr hKey, uint index, StringBuilder lpValueName, ref uint cchValueName,
        IntPtr reserved, IntPtr type, IntPtr data, IntPtr cbData);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegQueryValueEx(
        IntPtr hKey, string? valueName, IntPtr reserved, IntPtr type, IntPtr data, ref uint cbData);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegSetValueEx(
        IntPtr hKey, string? valueName, uint reserved, uint type, IntPtr data, uint cbData);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegDeleteValue(IntPtr hKey, string valueName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegDeleteKeyEx(IntPtr hKey, string subKey, uint samDesired, uint reserved);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegDeleteKey(IntPtr hKey, string subKey);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegQueryInfoKey(
        IntPtr hKey, StringBuilder? lpClass, IntPtr cchClass, IntPtr reserved,
        out uint subKeys, out uint maxSubKeyLen, out uint maxClassLen,
        out uint values, out uint maxValueNameLen, out uint maxValueLen,
        IntPtr securityDescriptor, IntPtr lastWriteTime);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegSaveKey(IntPtr hKey, string lpFile, IntPtr securityAttributes);

    // ---- 离线 Hive 加载/卸载 ----

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegLoadKey(IntPtr hKey, string? subKey, string lpFile);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegUnLoadKey(IntPtr hKey, string? subKey);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RegLoadAppKey(string lpFile, out IntPtr result, uint samDesired, uint options, uint reserved);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RegFlushKey(IntPtr hKey);

    // ---- 注册表项安全描述符 ----

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RegGetKeySecurity(IntPtr hKey, uint securityInformation, IntPtr securityDescriptor, ref uint lpcbSecurityDescriptor);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RegSetKeySecurity(IntPtr hKey, uint securityInformation, IntPtr securityDescriptor);

    // ---- 特权枚举 ----

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool PrivilegeCheck(IntPtr token, IntPtr privileges, [MarshalAs(UnmanagedType.Bool)] out bool result);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeName(
        string? systemName, in LUID luid, StringBuilder? name, ref uint cchName);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool LookupPrivilegeDisplayName(
        string? systemName, string name, StringBuilder? displayName, ref uint cchDisplayName, out uint languageId);
}
