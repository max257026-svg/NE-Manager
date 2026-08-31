using System.Windows.Threading;

namespace NEManager.Core.Memory;

/// <summary>
/// 后台定时器，每隔一段时间把冻结地址的值写回目标进程。
/// 就是 RH Editor 里那个「锁定」按钮的底层。
/// </summary>
public class FreezeManager : IDisposable
{
    private readonly MemoryModService _mem;
    private readonly DispatcherTimer _timer;
    private readonly HashSet<AddressEntry> _frozen = new();
    private readonly object _lock = new();

    public IReadOnlyCollection<AddressEntry> Frozen
    {
        get { lock (_lock) return _frozen.ToList(); }
    }

    public FreezeManager(MemoryModService mem, int intervalMs = 100)
    {
        _mem = mem;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(intervalMs) };
        _timer.Tick += (_, _) => Flush();
    }

    public void Start() => _timer.Start();
    public void Stop() => _timer.Stop();
    public bool IsRunning => _timer.IsEnabled;

    public void Add(AddressEntry entry)
    {
        lock (_lock) { _frozen.Add(entry); }
        if (!IsRunning) Start();
    }

    public void Remove(AddressEntry entry)
    {
        lock (_lock) { _frozen.Remove(entry); }
        if (Frozen.Count == 0) Stop();
    }

    public void Clear()
    {
        lock (_lock) _frozen.Clear();
        Stop();
    }

    private void Flush()
    {
        List<AddressEntry> snapshot;
        lock (_lock) snapshot = _frozen.ToList();

        foreach (var entry in snapshot)
        {
            try
            {
                var bytes = ParseToBytes(entry.Type, entry.FrozenValue);
                if (bytes != null && bytes.Length > 0)
                    _mem.WriteBytes(entry.Address, bytes);
            }
            catch { /* 进程可能已退出 */ }
        }
    }

    private static byte[]? ParseToBytes(ValueType type, string val)
    {
        try
        {
            return type switch
            {
                ValueType.Int32 => BitConverter.GetBytes(int.Parse(val)),
                ValueType.Int64 => BitConverter.GetBytes(long.Parse(val)),
                ValueType.Float => BitConverter.GetBytes(float.Parse(val)),
                ValueType.Double => BitConverter.GetBytes(double.Parse(val)),
                ValueType.String => System.Text.Encoding.Unicode.GetBytes(val),
                ValueType.ByteArray => val.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p =>
                    {
                        var s = p.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? p[2..] : p;
                        return Convert.ToByte(s, 16);
                    }).ToArray(),
                _ => null
            };
        }
        catch { return null; }
    }

    public void Dispose()
    {
        Stop();
        Clear();
    }
}
