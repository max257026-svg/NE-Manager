using System.Diagnostics;

namespace NEManager.Core.SystemTools;

/// <summary>
/// 轻量系统资源监控服务：CPU 总使用率 + 物理内存已用百分比。
/// 使用 PerformanceCounter，采样后缓存最近 N 帧供 WPF 绑定。
/// </summary>
public class SystemMonitorService : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memoryCounter;
    private readonly Timer _timer;

    public double CpuUsage { get; private set; }
    public double MemoryUsage { get; private set; }
    public double MemoryUsedMb { get; private set; }
    public double MemoryTotalMb { get; private set; }

    /// <summary>最近 60 帧 CPU 采样（每帧 1s，最多回看 1 分钟）。</summary>
    public double[] CpuHistory { get; private set; } = new double[60];
    /// <summary>最近 60 帧内存采样。</summary>
    public double[] MemoryHistory { get; private set; } = new double[60];

    private int _historyIdx;

    public event Action<double, double>? SampleTaken; // CPU, MEM

    public SystemMonitorService(int intervalMs = 1000)
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");

        // 首次调用返回 0，必须先 Read 一次让计数器就绪
        _cpuCounter.NextValue();
        _memoryCounter.NextValue();

        _timer = new Timer(_ => TakeSample(), null, intervalMs, intervalMs);
    }

    private void TakeSample()
    {
        try
        {
            CpuUsage = _cpuCounter.NextValue();
            MemoryUsage = _memoryCounter.NextValue();

            var totalGb = GC.GetTotalMemory(false); // 不准，只给 UI 个趋势
            var gcUsedMb = totalGb / 1024.0 / 1024.0;

            // 历史环形缓冲
            CpuHistory[_historyIdx] = CpuUsage;
            MemoryHistory[_historyIdx] = MemoryUsage;
            _historyIdx = (_historyIdx + 1) % CpuHistory.Length;

            SampleTaken?.Invoke(CpuUsage, MemoryUsage);
        }
        catch { /* 后台计数器偶发异常忽略 */ }
    }

    /// <summary>获取环形缓冲中"最旧在前"的完整历史快照（给折线图绑定用）。</summary>
    public (double[] cpu, double[] mem) GetHistoryOrdered()
    {
        int n = CpuHistory.Length;
        var cpu = new double[n];
        var mem = new double[n];
        for (int i = 0; i < n; i++)
        {
            int src = (_historyIdx + i) % n;
            cpu[i] = CpuHistory[src];
            mem[i] = MemoryHistory[src];
        }
        return (cpu, mem);
    }

    public void Dispose()
    {
        _timer.Dispose();
        _cpuCounter.Dispose();
        _memoryCounter.Dispose();
    }
}
