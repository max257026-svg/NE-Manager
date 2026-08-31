namespace NEManager.Native;

/// <summary>
/// Windows 原生常量集合。
/// </summary>
internal static class WinConst
{
    // ---- 通用 ----
    public const int INVALID_HANDLE_VALUE = -1;
    public const uint MAX_PATH = 260;
    public const uint ERROR_SUCCESS = 0;
    public const uint ERROR_INSUFFICIENT_BUFFER = 122;
    public const uint ERROR_MORE_DATA = 234;
    public const uint ERROR_NO_MORE_ITEMS = 259;
    public const uint ERROR_ACCESS_DENIED = 5;
    public const uint ERROR_FILE_NOT_FOUND = 2;
    public const uint ERROR_SHARING_VIOLATION = 32;

    // ---- 进程访问 ----
    public const uint PROCESS_TERMINATE = 0x0001;
    public const uint PROCESS_CREATE_THREAD = 0x0002;
    public const uint PROCESS_VM_OPERATION = 0x0008;
    public const uint PROCESS_VM_READ = 0x0010;
    public const uint PROCESS_VM_WRITE = 0x0020;
    public const uint PROCESS_DUP_HANDLE = 0x0040;
    public const uint PROCESS_QUERY_INFORMATION = 0x0400;
    public const uint PROCESS_SUSPEND_RESUME = 0x0800;
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    public const uint PROCESS_ALL_ACCESS = 0x1F0FFF;

    // ---- 令牌访问 ----
    public const uint TOKEN_QUERY = 0x0008;
    public const uint TOKEN_ADJUST_PRIVILEGES = 0x0020;
    public const uint TOKEN_ADJUST_GROUPS = 0x0040;
    public const uint TOKEN_ADJUST_DEFAULT = 0x0080;
    public const uint TOKEN_DUPLICATE = 0x0002;
    public const uint TOKEN_IMPERSONATE = 0x0004;
    public const uint TOKEN_QUERY_SOURCE = 0x0010;
    public const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
    public const uint TOKEN_ALL_ACCESS = 0xF01FF;

    // ---- 特权属性 ----
    public const uint SE_PRIVILEGE_ENABLED = 0x00000002;
    public const uint SE_PRIVILEGE_REMOVED = 0x00000004;
    public const uint SE_PRIVILEGE_ENABLED_BY_DEFAULT = 0x00000001;

    // ---- 安全信息 ----
    public const uint OWNER_SECURITY_INFORMATION = 0x00000001;
    public const uint GROUP_SECURITY_INFORMATION = 0x00000002;
    public const uint DACL_SECURITY_INFORMATION = 0x00000004;
    public const uint SACL_SECURITY_INFORMATION = 0x00000008;
    public const uint LABEL_SECURITY_INFORMATION = 0x00000010;
    public const uint PROTECTED_DACL_SECURITY_INFORMATION = 0x80000000;
    public const uint PROTECTED_SACL_SECURITY_INFORMATION = 0x40000000;
    public const uint UNPROTECTED_DACL_SECURITY_INFORMATION = 0x20000000;
    public const uint UNPROTECTED_SACL_SECURITY_INFORMATION = 0x10000000;

    // ---- ACE ----
    public const byte ACCESS_ALLOWED_ACE_TYPE = 0x00;
    public const byte ACCESS_DENIED_ACE_TYPE = 0x01;
    public const byte SYSTEM_AUDIT_ACE_TYPE = 0x02;
    public const byte ACCESS_ALLOWED_OBJECT_ACE_TYPE = 0x05;
    public const byte ACCESS_DENIED_OBJECT_ACE_TYPE = 0x06;

    public const byte OBJECT_INHERIT_ACE = 0x01;
    public const byte CONTAINER_INHERIT_ACE = 0x02;
    public const byte NO_PROPAGATE_INHERIT_ACE = 0x04;
    public const byte INHERIT_ONLY_ACE = 0x08;
    public const byte SUCCESSFUL_ACCESS_ACE_FLAG = 0x40;
    public const byte FAILED_ACCESS_ACE_FLAG = 0x80;

    // ---- 通用访问权限 ----
    public const uint GENERIC_READ = 0x80000000;
    public const uint GENERIC_WRITE = 0x40000000;
    public const uint GENERIC_EXECUTE = 0x20000000;
    public const uint GENERIC_ALL = 0x10000000;
    public const uint DELETE = 0x00010000;
    public const uint READ_CONTROL = 0x00020000;
    public const uint WRITE_DAC = 0x00040000;
    public const uint WRITE_OWNER = 0x00080000;
    public const uint SYNCHRONIZE = 0x00100000;
    public const uint MAXIMUM_ALLOWED = 0x02000000;

    // ---- 文件/目录权限位 ----
    public const uint FILE_READ_DATA = 0x0001;
    public const uint FILE_WRITE_DATA = 0x0002;
    public const uint FILE_APPEND_DATA = 0x0004;
    public const uint FILE_READ_EA = 0x0008;
    public const uint FILE_WRITE_EA = 0x0010;
    public const uint FILE_EXECUTE = 0x0020;
    public const uint FILE_DELETE_CHILD = 0x0040;
    public const uint FILE_READ_ATTRIBUTES = 0x0080;
    public const uint FILE_WRITE_ATTRIBUTES = 0x0100;

    // ---- 注册表 ----
    public const uint KEY_QUERY_VALUE = 0x0001;
    public const uint KEY_SET_VALUE = 0x0002;
    public const uint KEY_CREATE_SUB_KEY = 0x0004;
    public const uint KEY_ENUMERATE_SUB_KEYS = 0x0008;
    public const uint KEY_NOTIFY = 0x0010;
    public const uint KEY_CREATE_LINK = 0x0020;
    public const uint KEY_WOW64_64KEY = 0x0100;
    public const uint KEY_WOW64_32KEY = 0x0200;
    public const uint KEY_READ = 0x20019;
    public const uint KEY_WRITE = 0x20006;
    public const uint KEY_ALL_ACCESS = 0xF003F;

    public const uint REG_OPTION_NON_VOLATILE = 0x00000000;
    public const uint REG_OPTION_VOLATILE = 0x00000001;
    public const uint REG_OPTION_BACKUP_RESTORE = 0x00000004;
    public const uint REG_OPTION_OPEN_LINK = 0x00000008;

    // 注册表值类型
    public const uint REG_NONE = 0;
    public const uint REG_SZ = 1;
    public const uint REG_EXPAND_SZ = 2;
    public const uint REG_BINARY = 3;
    public const uint REG_DWORD = 4;
    public const uint REG_DWORD_LITTLE_ENDIAN = 4;
    public const uint REG_DWORD_BIG_ENDIAN = 5;
    public const uint REG_LINK = 6;
    public const uint REG_MULTI_SZ = 7;
    public const uint REG_RESOURCE_LIST = 8;
    public const uint REG_FULL_RESOURCE_DESCRIPTOR = 9;
    public const uint REG_RESOURCE_REQUIREMENTS_LIST = 10;
    public const uint REG_QWORD = 11;
    public const uint REG_QWORD_LITTLE_ENDIAN = 11;

    // ---- 服务控制管理器 ----
    public const uint SC_MANAGER_CONNECT = 0x0001;
    public const uint SC_MANAGER_CREATE_SERVICE = 0x0002;
    public const uint SC_MANAGER_ENUMERATE_SERVICE = 0x0004;
    public const uint SC_MANAGER_LOCK = 0x0008;
    public const uint SC_MANAGER_QUERY_LOCK_STATUS = 0x0010;
    public const uint SC_MANAGER_MODIFY_BOOT_CONFIG = 0x0020;
    public const uint SC_MANAGER_ALL_ACCESS = 0xF003F;

    public const uint SERVICE_QUERY_CONFIG = 0x0001;
    public const uint SERVICE_CHANGE_CONFIG = 0x0002;
    public const uint SERVICE_QUERY_STATUS = 0x0004;
    public const uint SERVICE_ENUMERATE_DEPENDENTS = 0x0008;
    public const uint SERVICE_START = 0x0010;
    public const uint SERVICE_STOP = 0x0020;
    public const uint SERVICE_PAUSE_CONTINUE = 0x0040;
    public const uint SERVICE_INTERROGATE = 0x0080;
    public const uint SERVICE_USER_DEFINED_CONTROL = 0x0100;
    public const uint SERVICE_ALL_ACCESS = 0xF01FF;

    public const uint SERVICE_CONTROL_STOP = 0x00000001;
    public const uint SERVICE_CONTROL_PAUSE = 0x00000002;
    public const uint SERVICE_CONTROL_CONTINUE = 0x00000003;
    public const uint SERVICE_CONTROL_INTERROGATE = 0x00000004;

    // 服务类型
    public const uint SERVICE_KERNEL_DRIVER = 0x00000001;
    public const uint SERVICE_FILE_SYSTEM_DRIVER = 0x00000002;
    public const uint SERVICE_WIN32_OWN_PROCESS = 0x00000010;
    public const uint SERVICE_WIN32_SHARE_PROCESS = 0x00000020;
    public const uint SERVICE_INTERACTIVE_PROCESS = 0x00000100;

    // 启动类型
    public const uint SERVICE_BOOT_START = 0x00000000;
    public const uint SERVICE_SYSTEM_START = 0x00000001;
    public const uint SERVICE_AUTO_START = 0x00000002;
    public const uint SERVICE_DEMAND_START = 0x00000003;
    public const uint SERVICE_DISABLED = 0x00000004;
    public const uint SERVICE_NO_CHANGE = 0xFFFFFFFF;

    // ---- 令牌类型 / 模拟级别 ----
    public const int TokenUser = 1;
    public const int TokenGroups = 2;
    public const int TokenPrivileges = 3;
    public const int TokenOwner = 4;
    public const int TokenPrimaryGroup = 5;
    public const int TokenDefaultDacl = 6;
    public const int TokenSource = 7;
    public const int TokenType = 8;
    public const int TokenImpersonationLevel = 9;
    public const int TokenStatistics = 10;
    public const int TokenElevationType = 18;
    public const int TokenElevation = 20;
    public const int TokenIntegrityLevel = 25;

    public const int SecurityAnonymous = 0;
    public const int SecurityIdentification = 1;
    public const int SecurityImpersonation = 2;
    public const int SecurityDelegation = 3;

    public const int TokenPrimary = 1;
    public const int TokenImpersonation = 2;

    // ---- 令牌完整性 ----
    public const uint SECURITY_MANDATORY_UNTRUSTED_RID = 0x00000000;
    public const uint SECURITY_MANDATORY_LOW_RID = 0x00001000;
    public const uint SECURITY_MANDATORY_MEDIUM_RID = 0x00002000;
    public const uint SECURITY_MANDATORY_MEDIUM_PLUS_RID = 0x00002100;
    public const uint SECURITY_MANDATORY_HIGH_RID = 0x00003000;
    public const uint SECURITY_MANDATORY_SYSTEM_RID = 0x00004000;
    public const uint SECURITY_MANDATORY_PROTECTED_PROCESS_RID = 0x00005000;

    // ---- 文件属性 ----
    public const uint FILE_ATTRIBUTE_READONLY = 0x00000001;
    public const uint FILE_ATTRIBUTE_HIDDEN = 0x00000002;
    public const uint FILE_ATTRIBUTE_SYSTEM = 0x00000004;
    public const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
    public const uint FILE_ATTRIBUTE_ARCHIVE = 0x00000020;
    public const uint FILE_ATTRIBUTE_DEVICE = 0x00000040;
    public const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
    public const uint FILE_ATTRIBUTE_TEMPORARY = 0x00000100;
    public const uint FILE_ATTRIBUTE_SPARSE_FILE = 0x00000200;
    public const uint FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;
    public const uint FILE_ATTRIBUTE_COMPRESSED = 0x00000800;
    public const uint FILE_ATTRIBUTE_OFFLINE = 0x00001000;
    public const uint FILE_ATTRIBUTE_NOT_CONTENT_INDEXED = 0x00002000;
    public const uint FILE_ATTRIBUTE_ENCRYPTED = 0x00004000;
    public const uint FILE_ATTRIBUTE_INTEGRITY_STREAM = 0x00008000;
    public const uint FILE_ATTRIBUTE_VIRTUAL = 0x00010000;
    public const uint FILE_ATTRIBUTE_NO_SCRUB_DATA = 0x00020000;

    // ---- CreateFile ----
    public const uint FILE_SHARE_READ = 0x00000001;
    public const uint FILE_SHARE_WRITE = 0x00000002;
    public const uint FILE_SHARE_DELETE = 0x00000004;
    public const uint OPEN_EXISTING = 3;
    public const uint FILE_FLAG_BACKUP_SEMANTICS = 0x02000000;
    public const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
    public const uint FILE_FLAG_NO_BUFFERING = 0x20000000;
    public const uint FILE_FLAG_WRITE_THROUGH = 0x80000000;

    // ---- MoveFileEx ----
    public const uint MOVEFILE_REPLACE_EXISTING = 0x00000001;
    public const uint MOVEFILE_COPY_ALLOWED = 0x00000002;
    public const uint MOVEFILE_DELAY_UNTIL_REBOOT = 0x00000004;
    public const uint MOVEFILE_WRITE_THROUGH = 0x00000008;

    // ---- ToolHelp ----
    public const uint TH32CS_SNAPPROCESS = 0x00000002;
    public const uint TH32CS_SNAPMODULE = 0x00000008;
    public const uint TH32CS_SNAPMODULE32 = 0x00000010;

    // ---- IOCTL ----
    public const uint IOCTL_DISK_GET_DRIVE_GEOMETRY = 0x00070000;
    public const uint IOCTL_DISK_GET_PARTITION_INFO = 0x00074004;
    public const uint IOCTL_VOLUME_GET_VOLUME_DISK_EXTENTS = 0x56000000;
    public const uint FSCTL_GET_NTFS_VOLUME_DATA = 0x90064;
    public const uint FSCTL_GET_RETRIEVAL_POINTERS = 0x90073;

    // ---- 虚拟磁盘 ----
    public const uint VIRTUAL_DISK_ACCESS_ATTACH_RO = 0x00010000;
    public const uint VIRTUAL_DISK_ACCESS_ATTACH_RW = 0x00020000;
    public const uint VIRTUAL_DISK_ACCESS_DETACH = 0x00040000;
    public const uint VIRTUAL_DISK_ACCESS_GET_INFO = 0x00080000;
    public const uint VIRTUAL_DISK_ACCESS_CREATE = 0x00100000;
    public const uint VIRTUAL_DISK_ACCESS_METAOPS = 0x00200000;
    public const uint VIRTUAL_DISK_ACCESS_READ = 0x000d0000;
    public const uint VIRTUAL_DISK_ACCESS_ALL = 0x003f0000;

    public const uint ATTACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000;
    public const uint ATTACH_VIRTUAL_DISK_FLAG_READ_ONLY = 0x00000001;
    public const uint ATTACH_VIRTUAL_DISK_FLAG_NO_DRIVE_LETTER = 0x00000002;
    public const uint ATTACH_VIRTUAL_DISK_FLAG_PERMANENT_LIFETIME = 0x00000004;
    public const uint ATTACH_VIRTUAL_DISK_FLAG_NO_LOCAL_HOST = 0x00000008;

    public const uint DETACH_VIRTUAL_DISK_FLAG_NONE = 0x00000000;

    public const uint OPEN_VIRTUAL_DISK_FLAG_NONE = 0x00000000;
    public const uint OPEN_VIRTUAL_DISK_FLAG_NO_PARENTS = 0x00000001;
    public const uint OPEN_VIRTUAL_DISK_FLAG_BLANK_FILE = 0x00000002;
    public const uint OPEN_VIRTUAL_DISK_FLAG_BOOT_VOLUME = 0x00000004;

    // ---- 特权名 ----
    public const string SE_TAKE_OWNERSHIP_NAME = "SeTakeOwnershipPrivilege";
    public const string SE_RESTORE_NAME = "SeRestorePrivilege";
    public const string SE_BACKUP_NAME = "SeBackupPrivilege";
    public const string SE_DEBUG_NAME = "SeDebugPrivilege";
    public const string SE_SECURITY_NAME = "SeSecurityPrivilege";
    public const string SE_LOAD_DRIVER_NAME = "SeLoadDriverPrivilege";
    public const string SE_SYSTEM_ENVIRONMENT_NAME = "SeSystemEnvironmentPrivilege";
    public const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
    public const string SE_IMPERSONATE_NAME = "SeImpersonatePrivilege";
    public const string SE_CREATE_TOKEN_NAME = "SeCreateTokenPrivilege";
    public const string SE_ASSIGNPRIMARYTOKEN_NAME = "SeAssignPrimaryTokenPrivilege";
    public const string SE_INCREASE_QUOTA_NAME = "SeIncreaseQuotaPrivilege";
    public const string SE_PROFILE_SINGLE_PROCESS_NAME = "SeProfileSingleProcessPrivilege";
    public const string SE_SYSTEMTIME_NAME = "SeSystemtimePrivilege";
    public const string SE_TCB_NAME = "SeTcbPrivilege";
    public const string SE_MANAGE_VOLUME_NAME = "SeManageVolumePrivilege";
    public const string SE_CREATE_SYMBOLIC_LINK_NAME = "SeCreateSymbolicLinkPrivilege";
    public const string SE_TRUSTED_CREDMAN_ACCESS_NAME = "SeTrustedCredManAccessPrivilege";
    public const string SE_RELABEL_NAME = "SeRelabelPrivilege";
    public const string SE_TIME_ZONE_NAME = "SeTimeZonePrivilege";
    public const string SE_DELEGATE_SESSION_USER_IMPERSONATE_NAME = "SeDelegateSessionUserImpersonatePrivilege";
    public const string SE_INC_BASE_PRIORITY_NAME = "SeIncreaseBasePriorityPrivilege";
    public const string SE_INCREASE_WORKING_SET_NAME = "SeIncreaseWorkingSetPrivilege";
    public const string SE_LOCK_MEMORY_NAME = "SeLockMemoryPrivilege";
    public const string SE_AUDIT_NAME = "SeAuditPrivilege";
    public const string SE_SYNC_AGENT_NAME = "SeSyncAgentPrivilege";
    public const string SE_ENABLE_DELEGATION_NAME = "SeEnableDelegationPrivilege";
    public const string SE_MACHINE_ACCOUNT_NAME = "SeMachineAccountPrivilege";
    public const string SE_REMOTE_SHUTDOWN_NAME = "SeRemoteShutdownPrivilege";
    public const string SE_UNDOCK_NAME = "SeUndockPrivilege";
    public const string SE_BATCH_LOGON_NAME = "SeBatchLogonRight";
    public const string SE_SERVICE_LOGON_NAME = "SeServiceLogonRight";
    public const string SE_NETWORK_LOGON_NAME = "SeNetworkLogonRight";
    public const string SE_INTERACTIVE_LOGON_NAME = "SeInteractiveLogonRight";
    public const string SE_DENY_BATCH_LOGON_NAME = "SeDenyBatchLogonRight";
    public const string SE_DENY_SERVICE_LOGON_NAME = "SeDenyServiceLogonRight";
    public const string SE_DENY_NETWORK_LOGON_NAME = "SeDenyNetworkLogonRight";
    public const string SE_DENY_INTERACTIVE_LOGON_NAME = "SeDenyInteractiveLogonRight";
    public const string SE_CREATE_GLOBAL_NAME = "SeCreateGlobalPrivilege";
    public const string SE_CREATE_PAGEFILE_NAME = "SeCreatePagefilePrivilege";
    public const string SE_CREATE_PERMANENT_NAME = "SeCreatePermanentPrivilege";
    public const string SE_DRIVER = "SeLoadDriverPrivilege";

    // ---- 已知的 TrustedInstaller 相关 SID ----
    public const string TRUSTED_INSTALLER_SID = "S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464";
    public const string LOCAL_SYSTEM_SID = "S-1-5-18";

    // ---- 注册表预定义键 ----
    public const ulong HKEY_CLASSES_ROOT = 0x80000000;
    public const ulong HKEY_CURRENT_USER = 0x80000001;
    public const ulong HKEY_LOCAL_MACHINE = 0x80000002;
    public const ulong HKEY_USERS = 0x80000003;
    public const ulong HKEY_CURRENT_CONFIG = 0x80000005;
}
