using System.Runtime.InteropServices;
using System.Text;
using NEManager.Native;

namespace NEManager.Core.Memory;

/// <summary>
/// 进程内存读写封装。
/// </summary>
public class ProcessMemory
{
    public int ProcessId { get; private set; }
    public string ProcessName { get; private set; } = string.Empty;
    public IntPtr ProcessHandle { get; private set; }
    public bool IsAttached { get; private set; }

    public bool Attach(int processId)
    {
        try
        {
            ProcessId = processId;
            ProcessHandle = Memory32.OpenProcess(Memory32.PROCESS_ALL_ACCESS, false, (uint)processId);
            if (ProcessHandle == IntPtr.Zero)
                return false;

            try
            {
                var proc = System.Diagnostics.Process.GetProcessById(processId);
                ProcessName = proc.ProcessName;
            }
            catch
            {
                ProcessName = $"PID:{processId}";
            }

            IsAttached = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Detach()
    {
        if (IsAttached && ProcessHandle != IntPtr.Zero)
        {
            Memory32.CloseHandle(ProcessHandle);
            ProcessHandle = IntPtr.Zero;
        }
        IsAttached = false;
    }

    public byte[] ReadBytes(IntPtr address, int count)
    {
        var buffer = new byte[count];
        Memory32.ReadProcessMemory(ProcessHandle, address, buffer, count, out _);
        return buffer;
    }

    public bool WriteBytes(IntPtr address, byte[] data)
    {
        return Memory32.WriteProcessMemory(ProcessHandle, address, data, data.Length, out _);
    }

    public int ReadInt32(IntPtr address)
    {
        var buffer = ReadBytes(address, 4);
        return BitConverter.ToInt32(buffer, 0);
    }

    public bool WriteInt32(IntPtr address, int value)
    {
        return WriteBytes(address, BitConverter.GetBytes(value));
    }

    public long ReadInt64(IntPtr address)
    {
        var buffer = ReadBytes(address, 8);
        return BitConverter.ToInt64(buffer, 0);
    }

    public bool WriteInt64(IntPtr address, long value)
    {
        return WriteBytes(address, BitConverter.GetBytes(value));
    }

    public float ReadFloat(IntPtr address)
    {
        var buffer = ReadBytes(address, 4);
        return BitConverter.ToSingle(buffer, 0);
    }

    public bool WriteFloat(IntPtr address, float value)
    {
        return WriteBytes(address, BitConverter.GetBytes(value));
    }

    public string ReadString(IntPtr address, int maxLength, Encoding encoding)
    {
        var buffer = ReadBytes(address, maxLength);
        int nullIndex = Array.IndexOf<byte>(buffer, 0);
        if (nullIndex >= 0)
            Array.Resize(ref buffer, nullIndex);
        return encoding.GetString(buffer);
    }

    public List<MemoryRegion> GetMemoryRegions()
    {
        var regions = new List<MemoryRegion>();
        IntPtr address = IntPtr.Zero;
        int infoSize = Marshal.SizeOf<Memory32.MEMORY_BASIC_INFORMATION>();

        while (Memory32.VirtualQueryEx(ProcessHandle, address, out var mbi, infoSize) != 0)
        {
            regions.Add(new MemoryRegion
            {
                BaseAddress = mbi.BaseAddress,
                RegionSize = (long)mbi.RegionSize,
                Protect = mbi.Protect,
                State = mbi.State,
                Description = $"Base={mbi.BaseAddress:X}, Size={mbi.RegionSize:X}, Protect={mbi.Protect:X}, State={mbi.State:X}"
            });

            long next = (long)mbi.BaseAddress + (long)mbi.RegionSize;
            if (next <= (long)address) break;
            address = (IntPtr)next;
        }

        return regions;
    }

    public List<ProcessModule> GetModules()
    {
        var modules = new List<ProcessModule>();
        IntPtr snapshot = Memory32.CreateToolhelp32Snapshot(Memory32.TH32CS_SNAPMODULE, (uint)ProcessId);
        if (snapshot == IntPtr.Zero || snapshot == new IntPtr(-1))
            return modules;

        try
        {
            var entry = new Memory32.MEMORY_MODULEENTRY32();
            entry.dwSize = (uint)Marshal.SizeOf<Memory32.MEMORY_MODULEENTRY32>();

            if (Memory32.Module32First(snapshot, ref entry))
            {
                do
                {
                    modules.Add(new ProcessModule
                    {
                        BaseAddress = entry.modBaseAddr,
                        Size = (int)entry.modBaseSize,
                        Name = entry.szModule,
                        FilePath = entry.szExePath
                    });
                } while (Memory32.Module32Next(snapshot, ref entry));
            }
        }
        finally
        {
            Memory32.CloseHandle(snapshot);
        }

        return modules;
    }
}

public class MemoryRegion
{
    public IntPtr BaseAddress { get; set; }
    public long RegionSize { get; set; }
    public uint Protect { get; set; }
    public uint State { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class ProcessModule
{
    public IntPtr BaseAddress { get; set; }
    public int Size { get; set; }
    public string Name { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
}
