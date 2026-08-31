using System.Diagnostics;

namespace NEManager.Core.SystemTools;

/// <summary>
/// 轻量系统资源监控服务：CPU 总使用率 + 物理内存已用百分比。
/// 使用 PerformanceCounter，采样后缓存最近 N 帧供 WPF 绑定。
/// </summary>
public class SystemMonitorService : IDisposable
{
    private readonly PerformanceCounter _cpuCounter;
    private readonly PerformanceCounter _memCommittedCounter;
    private readonly PerformanceCounter _memAvailableCounter;
    private readonly Timer _timer;

    public double CpuUsage { get; private set; }
    public double MemoryUsage { get; private set; }   // 已用百分比 0..100
    public double MemoryUsedMb { get; private set; }
    public double MemoryTotalMb { get; private set; }

    public double[] CpuHistory { get; private set; } = new double[60];
    public double[] MemoryHistory { get; private set; } = new double[60];

    private int _historyIdx;

    public event Action<double, double>? SampleTaken; // CPU, MEM

    public SystemMonitorService(int intervalMs = 1000)
    {
        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _memCommittedCounter = new PerformanceCounter("Memory", "Committed Bytes");
        _memAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");

        // 热启动：必须先 Read 一次计数器才能返回有效值
        _cpuCounter.NextValue();
        _memCommittedCounter.NextValue();
        _memAvailableCounter.NextValue();

        _timer = new Timer(_ => TakeSample(), null, intervalMs, intervalMs);
    }

    private void TakeSample()
    {
        try
        {
            CpuUsage = Math.Clamp(_cpuCounter.NextValue(), 0, 100);

            double committedMb = _memCommittedCounter.NextValue() / 1024.0 / 1024.0;
            double availableMb = _memAvailableCounter.NextValue();
            double totalMb = committedMb + availableMb;
            MemoryUsedMb = committedMb;
            MemoryTotalMb = totalMb;
            MemoryUsage = totalMb > 0 ? Math.Clamp(committedMb / totalMb * 100.0, 0, 100) : 0;

            CpuHistory[_historyIdx] = CpuUsage;
            MemoryHistory[_historyIdx] = MemoryUsage;
            _historyIdx = (_historyIdx + 1) % CpuHistory.Length;

            SampleTaken?.Invoke(CpuUsage, MemoryUsage);
        }
        catch { /* 后台计数器偶发异常忽略 */ }
    }

    /// <summary>环形缓冲最旧→最新顺序。</summary>
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
        _memCommittedCounter.Dispose();
        _memAvailableCounter.Dispose();
    }
}
