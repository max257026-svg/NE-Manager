using System.Text;

namespace NEManager.Core.Memory;

/// <summary>
/// 内存搜索与锁定。
/// </summary>
public class MemorySearch
{
    public ProcessMemory Process { get; }
    public List<SearchResult> Results { get; private set; } = new();

    private readonly Dictionary<IntPtr, System.Timers.Timer> _lockTimers = new();

    public MemorySearch(ProcessMemory process)
    {
        Process = process;
    }

    public List<SearchResult> SearchExact(byte[] pattern)
    {
        Results.Clear();
        if (!Process.IsAttached || pattern.Length == 0) return Results;

        var regions = Process.GetMemoryRegions();
        foreach (var region in regions)
        {
            if (region.RegionSize <= 0 || region.RegionSize > 100 * 1024 * 1024)
                continue;
            if (region.State != 0x1000)
                continue;

            try
            {
                var data = Process.ReadBytes(region.BaseAddress, (int)region.RegionSize);
                for (int i = 0; i <= data.Length - pattern.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < pattern.Length; j++)
                    {
                        if (data[i + j] != pattern[j]) { match = false; break; }
                    }
                    if (match)
                    {
                        IntPtr addr = IntPtr.Add(region.BaseAddress, i);
                        var value = new byte[pattern.Length];
                        Array.Copy(pattern, value, pattern.Length);
                        Results.Add(new SearchResult { Address = addr, CurrentValue = value });
                    }
                }
            }
            catch { }
        }

        return Results;
    }

    public List<SearchResult> SearchInt32(int value)
    {
        return SearchExact(BitConverter.GetBytes(value));
    }

    public List<SearchResult> SearchInt64(long value)
    {
        return SearchExact(BitConverter.GetBytes(value));
    }

    public List<SearchResult> SearchFloat(float value, float tolerance)
    {
        Results.Clear();
        if (!Process.IsAttached) return Results;

        var regions = Process.GetMemoryRegions();
        foreach (var region in regions)
        {
            if (region.RegionSize < 4 || region.RegionSize > 100 * 1024 * 1024)
                continue;
            if (region.State != 0x1000)
                continue;

            try
            {
                var data = Process.ReadBytes(region.BaseAddress, (int)region.RegionSize);
                for (int i = 0; i <= data.Length - 4; i += 4)
                {
                    float f = BitConverter.ToSingle(data, i);
                    if (Math.Abs(f - value) <= tolerance)
                    {
                        IntPtr addr = IntPtr.Add(region.BaseAddress, i);
                        var valBytes = new byte[4];
                        Array.Copy(data, i, valBytes, 0, 4);
                        Results.Add(new SearchResult { Address = addr, CurrentValue = valBytes });
                    }
                }
            }
            catch { }
        }

        return Results;
    }

    public List<SearchResult> SearchString(string text, Encoding encoding)
    {
        var pattern = encoding.GetBytes(text);
        return SearchExact(pattern);
    }

    public List<SearchResult> RefineResults(Func<SearchResult, bool> filter)
    {
        Results = Results.Where(filter).ToList();
        return Results;
    }

    public void LockValue(IntPtr address, byte[] value, int intervalMs = 100)
    {
        if (_lockTimers.ContainsKey(address)) return;

        var timer = new System.Timers.Timer(intervalMs);
        timer.Elapsed += (_, _) =>
        {
            try { Process.WriteBytes(address, value); }
            catch { }
        };
        timer.AutoReset = true;
        timer.Start();
        _lockTimers[address] = timer;

        var result = Results.FirstOrDefault(r => r.Address == address);
        if (result != null) result.IsLocked = true;
    }

    public void UnlockValue(IntPtr address)
    {
        if (_lockTimers.TryGetValue(address, out var timer))
        {
            timer.Stop();
            timer.Dispose();
            _lockTimers.Remove(address);
        }

        var result = Results.FirstOrDefault(r => r.Address == address);
        if (result != null) result.IsLocked = false;
    }

    public void UnlockAll()
    {
        foreach (var kvp in _lockTimers)
        {
            kvp.Value.Stop();
            kvp.Value.Dispose();
        }
        _lockTimers.Clear();

        foreach (var r in Results)
            r.IsLocked = false;
    }
}

public class SearchResult
{
    public IntPtr Address { get; set; }
    public byte[] CurrentValue { get; set; } = Array.Empty<byte>();
    public bool IsLocked { get; set; }
}
