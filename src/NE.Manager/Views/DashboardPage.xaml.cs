using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class DashboardPage : UserControl, IRefreshable
{
    private SystemMonitorService? _svc;

    public DashboardPage() => InitializeComponent();

    protected override void OnVisualChildrenChanged(DependencyObject? visualAdded, DependencyObject? visualRemoved)
    {
        base.OnVisualChildrenChanged(visualAdded, visualRemoved);
        if (visualAdded != null) OnEnter();
        if (visualRemoved != null) OnLeave();
    }

    public void OnEnter()
    {
        if (_svc != null) return;
        _svc = new SystemMonitorService(intervalMs: 800);
        _svc.SampleTaken += OnSample;
        // 立即触发一次采样（计数器刚创建时 NextValue 返回 0，先热启动）
        Dispatcher.InvokeAsync(() => OnSample(_svc.CpuUsage, _svc.MemoryUsage),
            System.Windows.Threading.DispatcherPriority.Background);
    }

    public void OnLeave()
    {
        if (_svc == null) return;
        _svc.SampleTaken -= OnSample;
        _svc.Dispose();
        _svc = null;
    }

    private void OnSample(double cpu, double mem)
    {
        Dispatcher.Invoke(() =>
        {
            CpuLabel.Text = $"{cpu:F1}%";
            MemLabel.Text = $"{mem:F1}%";
            StatusLabel.Text = $"上次采样 {DateTime.Now:HH:mm:ss}";
            DrawChart(CpuChart, _svc!.GetHistoryOrdered().cpu, "#58A6FF");
            DrawChart(MemChart, _svc.GetHistoryOrdered().mem, "#3FB950");
        });
    }

    /// <summary>
    /// 把 0..100 折线画到 Canvas，用 WPF 原生 Polyline，不引入外部图表库。
    /// </summary>
    private static void DrawChart(Canvas canvas, double[] values, string hexColor)
    {
        canvas.Children.Clear();

        double W = canvas.ActualWidth > 0 ? canvas.ActualWidth : 600;
        double H = canvas.ActualHeight > 0 ? canvas.ActualHeight : 200;

        // 背景网格线 4 条
        var gridBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        for (int i = 1; i <= 4; i++)
        {
            var line = new Line
            {
                X1 = 0, X2 = W,
                Y1 = H * i / 4, Y2 = H * i / 4,
                Stroke = gridBrush,
                StrokeThickness = 1
            };
            canvas.Children.Add(line);
        }

        if (values.Length == 0) return;

        // 主折线
        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor)!;
        var poly = new Polyline
        {
            Stroke = brush,
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round
        };

        double step = W / (values.Length - 1);
        for (int i = 0; i < values.Length; i++)
        {
            double clamped = Math.Clamp(values[i], 0, 100);
            double x = i * step;
            double y = H - (clamped / 100.0) * H;
            poly.Points.Add(new Point(x, y));
        }
        canvas.Children.Add(poly);

        // 填充区域半透明
        if (poly.Points.Count >= 2)
        {
            var fill = new Polygon
            {
                Fill = new SolidColorBrush(Color.FromArgb(40, brush.Color.R, brush.Color.G, brush.Color.B)),
                Points = new PointCollection(poly.Points)
            };
            fill.Points.Add(new Point(W, H));
            fill.Points.Add(new Point(0, H));
            fill.Points.Add(poly.Points[0]);
            canvas.Children.Insert(0, fill);
        }
    }
}
