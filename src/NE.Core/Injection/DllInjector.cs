using System.Runtime.InteropServices;
using System.Text;

namespace NEManager.Core.Injection;

/// <summary>CreateRemoteThread + LoadLibraryW DLL 注入器</summary>
public static class DllInjector
{
    [DllImport("kernel32.dll")] private static extern IntPtr OpenProcess(int access, bool inherit, int pid);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr h);
    [DllImport("kernel32.dll")] private static extern IntPtr VirtualAllocEx(IntPtr h, IntPtr addr, uint size, uint alloc, uint protect);
    [DllImport("kernel32.dll")] private static extern bool WriteProcessMemory(IntPtr h, IntPtr addr, byte[] buf, uint size, out uint written);
    [DllImport("kernel32.dll")] private static extern bool VirtualFreeEx(IntPtr h, IntPtr addr, uint size, uint free);
    [DllImport("kernel32.dll")] private static extern IntPtr CreateRemoteThread(IntPtr h, IntPtr attr, uint stack, IntPtr start, IntPtr param, uint flag, out uint id);
    [DllImport("kernel32.dll")] private static extern bool WaitForSingleObject(IntPtr h, uint ms);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandleW(string name);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)] private static extern IntPtr GetProcAddress(IntPtr h, string name);

    private const int PROCESS_ALL_ACCESS = 0x1F0FFF;
    private const uint MEM_COMMIT = 0x1000;
    private const uint MEM_RESERVE = 0x2000;
    private const uint MEM_RELEASE = 0x8000;
    private const uint PAGE_READWRITE = 0x04;
    private const uint PAGE_EXECUTE_READ = 0x20;

    public record InjectResult(bool Success, string Message);

    public static InjectResult Inject(int pid, string dllPath)
    {
        if (!File.Exists(dllPath)) return new InjectResult(false, $"DLL 不存在：{dllPath}");

        var hProc = OpenProcess(PROCESS_ALL_ACCESS, false, pid);
        if (hProc == IntPtr.Zero)
            return new InjectResult(false, $"OpenProcess 失败（PID {pid}）。需要管理员权限或 SeDebug 特权。");

        IntPtr remoteMem = IntPtr.Zero;
        try
        {
            var pathBytes = Encoding.Unicode.GetBytes(dllPath);
            remoteMem = VirtualAllocEx(hProc, IntPtr.Zero, (uint)pathBytes.Length + 2,
                MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (remoteMem == IntPtr.Zero)
                return new InjectResult(false, "VirtualAllocEx 失败。");

            if (!WriteProcessMemory(hProc, remoteMem, pathBytes, (uint)pathBytes.Length, out _))
                return new InjectResult(false, "WriteProcessMemory 失败。");

            var hKernel = GetModuleHandleW("kernel32.dll");
            if (hKernel == IntPtr.Zero) return new InjectResult(false, "GetModuleHandle 失败。");
            var loadLibrary = GetProcAddress(hKernel, "LoadLibraryW");
            if (loadLibrary == IntPtr.Zero) return new InjectResult(false, "GetProcAddress(LoadLibraryW) 失败。");

            var hThread = CreateRemoteThread(hProc, IntPtr.Zero, 0, loadLibrary, remoteMem, 0, out _);
            if (hThread == IntPtr.Zero) return new InjectResult(false, "CreateRemoteThread 失败。");

            WaitForSingleObject(hThread, 5000);
            CloseHandle(hThread);

            return new InjectResult(true, $"注入成功！PID {pid} 已加载 {Path.GetFileName(dllPath)}");
        }
        catch (Exception ex)
        {
            return new InjectResult(false, ex.Message);
        }
        finally
        {
            // 失败也必须释放远程内存 + 关闭句柄，防泄漏
            if (remoteMem != IntPtr.Zero) VirtualFreeEx(hProc, remoteMem, 0, MEM_RELEASE);
            CloseHandle(hProc);
        }
    }
}
