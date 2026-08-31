using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Native;

/// <summary>
/// Rstrtmgr.dll —— 重启管理器，用于查询"哪些进程正在占用某个文件"。
/// 比手工遍历系统句柄表稳定得多。
/// </summary>
internal static partial class Rstrtmgr
{
    private const string Lib = "rstrtmgr.dll";

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RmStartSession(out uint sessionHandle, uint sessionFlags, StringBuilder strSessionKey);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RmEndSession(uint sessionHandle);

    [DllImport(Lib, SetLastError = true, CharSet = CharSet.Unicode)]
    internal static extern uint RmRegisterResources(
        uint sessionHandle,
        uint numFiles,
        string[] rgsFilenames,
        uint numApplications,
        IntPtr rgApplications,
        uint numServices,
        IntPtr rgsServiceNames);

    [DllImport(Lib, SetLastError = true)]
    internal static extern uint RmGetList(
        uint sessionHandle,
        out uint procInfoNeeded,
        ref uint procInfo,
        [In, Out] RM_PROCESS_INFO[] rgAffectedApps,
        ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;
        public uint AppLastWriteTime1;
        public uint AppLastWriteTime2;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
        public string strAppName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string strServiceShortName;
        public RM_APP_TYPE ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;
        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct RM_UNIQUE_PROCESS
    {
        public uint dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    internal enum RM_APP_TYPE
    {
        RmUnknownApp = 0,
        RmMainWindow = 1,
        RmOtherWindow = 2,
        RmService = 3,
        RmExplorer = 4,
        RmConsole = 5,
        RmCritical = 1000
    }

    internal const uint RmRebootReasonNone = 0;
    internal const int CCH_RM_SESSION_KEY = 32;
    internal const int ERROR_MORE_DATA = 234;
}
