using System.Runtime.InteropServices;
using NEManager.Native;

namespace NEManager.Core.Memory;

public enum ValueType { Int32, Int64, Float, Double, String, ByteArray }
public enum ScanType { Exact, GreaterThan, LessThan, Between, Changed, Unchanged, Increased, Decreased }

public record ScanResult(IntPtr Address, byte[] RawBytes, string Display, int Size, ValueType ValueType);

/// <summary>内存修改核心服务 —— 内存搜索 + 读写。</summary>
public class MemoryModService : IDisposable
{
    private IntPtr _hProcess;
    private readonly int _pid;
    private bool _disposed;

    public int Pid => _pid;
    public bool IsAttached => _hProcess != IntPtr.Zero;
    public string Error { get; private set; } = "";

    public MemoryModService(int pid)
    {
        _pid = pid;
        _hProcess = Memory32.OpenProcess(Memory32.PROCESS_ALL_ACCESS, false, (uint)pid);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_hProcess != IntPtr.Zero) Memory32.CloseHandle(_hProcess);
            _hProcess = IntPtr.Zero;
            _disposed = true;
        }
    }

    // ==================== 读内存 ====================

    public byte[] ReadBytes(IntPtr address, int size)
    {
        if (!IsAttached) return Array.Empty<byte>();
        var buf = new byte[size];
        int read = 0;
        Memory32.ReadProcessMemory(_hProcess, address, buf, size, out read);
        if (read < size) { var trimmed = new byte[read]; Array.Copy(buf, trimmed, read); return trimmed; }
        return buf;
    }

    public T? Read<T>(IntPtr address) where T : struct
    {
        var bytes = ReadBytes(address, Marshal.SizeOf<T>());
        if (bytes.Length < Marshal.SizeOf<T>()) return null;
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject()); }
        finally { handle.Free(); }
    }

    // ==================== 写内存 ====================

    public bool WriteBytes(IntPtr address, byte[] data)
    {
        if (!IsAttached) return false;
        return Memory32.WriteProcessMemory(_hProcess, address, data, data.Length, out _);
    }

    public bool Write<T>(IntPtr address, T value) where T : struct
    {
        var bytes = new byte[Marshal.SizeOf<T>()];
        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try { Marshal.StructureToPtr(value, handle.AddrOfPinnedObject(), false); }
        finally { handle.Free(); }
        return WriteBytes(address, bytes);
    }

    // ==================== 内存扫描 ====================

    /// <summary>
    /// 扫描进程所有可读内存区域，找匹配值的地址。
    /// maxResults 限制返回数量（防止结果爆内存）。
    /// </summary>
    public List<ScanResult> Scan(ValueType type, ScanType scanType, string valueStr, int maxResults = 5000)
    {
        var results = new List<ScanResult>();
        if (!IsAttached) { Error = "未附加到进程"; return results; }

        byte[] targetBytes = ParseValue(type, valueStr, out int valueSize);
        if (targetBytes.Length == 0) { Error = "值解析失败"; return results; }

        var addr = IntPtr.Zero;
        while (true)
        {
            if (Memory32.VirtualQueryEx(_hProcess, addr, out var mbi, Marshal.SizeOf<Memory32.MEMORY_BASIC_INFORMATION>()) == 0)
                break;

            // 只扫已提交且可读的内存
            if (mbi.State == Memory32.MEM_COMMIT && (mbi.Protect & 0x04) != 0) // PAGE_READWRITE = 0x04
            {
                try
                {
                    long regionSize = mbi.RegionSize;
                    if (regionSize > 200 * 1024 * 1024) // 跳过过大区域（>200MB）
                        regionSize = 200 * 1024 * 1024;

                    var regionBytes = ReadBytes(mbi.BaseAddress, (int)regionSize);
                    if (regionBytes.Length >= valueSize)
                    {
                        ScanRegion(regionBytes, mbi.BaseAddress, targetBytes, valueSize, type, results, maxResults);
                    }
                }
                catch { /* 跳过不可读区域 */ }
            }

            long next = mbi.BaseAddress.ToInt64() + mbi.RegionSize;
            if (next <= addr.ToInt64()) break;
            addr = new IntPtr(next);
            if (results.Count >= maxResults) break;
        }

        Error = "";
        return results;
    }

    private static void ScanRegion(byte[] region, IntPtr baseAddr, byte[] target, int valueSize,
        ValueType type, List<ScanResult> results, int maxResults)
    {
        for (int i = 0; i <= region.Length - valueSize && results.Count < maxResults; i++)
        {
            bool match = true;
            for (int j = 0; j < target.Length; j++)
            {
                if (region[i + j] != target[j]) { match = false; break; }
            }
            if (match)
            {
                var addr = new IntPtr(baseAddr.ToInt64() + i);
                var copy = new byte[target.Length];
                Array.Copy(target, copy, target.Length);
                results.Add(new ScanResult(addr, copy, ConvertBytesToDisplay(copy, type), valueSize, type));
            }
        }
    }

    private static byte[] ParseValue(ValueType type, string input, out int size)
    {
        size = 0;
        try
        {
            byte[] result;
            switch (type)
            {
                case ValueType.Int32:
                    size = 4;
                    result = BitConverter.GetBytes(int.Parse(input));
                    break;
                case ValueType.Int64:
                    size = 8;
                    result = BitConverter.GetBytes(long.Parse(input));
                    break;
                case ValueType.Float:
                    size = 4;
                    result = BitConverter.GetBytes(float.Parse(input));
                    break;
                case ValueType.Double:
                    size = 8;
                    result = BitConverter.GetBytes(double.Parse(input));
                    break;
                case ValueType.String:
                    size = input.Length * 2;
                    result = System.Text.Encoding.Unicode.GetBytes(input);
                    break;
                case ValueType.ByteArray:
                    var parts = input.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    var bytes = parts.Select(p =>
                    {
                        var s = p.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? p[2..] : p;
                        return Convert.ToByte(s, 16);
                    }).ToArray();
                    size = bytes.Length;
                    result = bytes;
                    break;
                default:
                    result = Array.Empty<byte>();
                    break;
            }
            return result;
        }
        catch { return Array.Empty<byte>(); }
    }

    private static string ConvertBytesToDisplay(byte[] bytes, ValueType type)
    {
        try
        {
            return type switch
            {
                ValueType.Int32 => BitConverter.ToInt32(bytes, 0).ToString(),
                ValueType.Int64 => BitConverter.ToInt64(bytes, 0).ToString(),
                ValueType.Float => BitConverter.ToSingle(bytes, 0).ToString("F2"),
                ValueType.Double => BitConverter.ToDouble(bytes, 0).ToString("F2"),
                ValueType.String => System.Text.Encoding.Unicode.GetString(bytes),
                _ => BitConverter.ToString(bytes).Replace("-", " ")
            };
        }
        catch { return BitConverter.ToString(bytes).Replace("-", " "); }
    }

    // ==================== 枚举模块 ====================

    public List<(string Name, IntPtr Base, uint Size)> EnumModules()
    {
        var list = new List<(string, IntPtr, uint)>();
        var snap = Memory32.CreateToolhelp32Snapshot(
            Memory32.TH32CS_SNAPMODULE | 0x00000010, (uint)_pid);
        if (snap == IntPtr.Zero || snap == new IntPtr(-1)) return list;

        try
        {
            var entry = new Memory32.MEMORY_MODULEENTRY32 { dwSize = (uint)Marshal.SizeOf<Memory32.MEMORY_MODULEENTRY32>() };
            if (Memory32.Module32First(snap, ref entry))
            {
                do
                {
                    list.Add((entry.szModule, entry.modBaseAddr, entry.modBaseSize));
                } while (Memory32.Module32Next(snap, ref entry));
            }
        }
        finally { Memory32.CloseHandle(snap); }
        return list;
    }
}
