using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class DashboardPage : UserControl, IRefreshable
{
    private SystemMonitorService? _svc;

    public DashboardPage()
    {
        InitializeComponent();
        Loaded += (_, _) => OnEnter();
        Unloaded += (_, _) => OnLeave();
    }

    public void OnEnter()
    {
        if (_svc != null) return;
        _svc = new SystemMonitorService(intervalMs: 800);
        _svc.SampleTaken += OnSample;
        // 立即预热一次
        OnSample(_svc.CpuUsage, _svc.MemoryUsage);
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
            var used = _svc!.MemoryUsedMb;
            var total = _svc.MemoryTotalMb;
            MemDetailLabel.Text = $"{used:F0} / {total:F0} MB";
            StatusLabel.Text = $"上次采样 {DateTime.Now:HH:mm:ss}";
            DrawChart(CpuChart, _svc.GetHistoryOrdered().cpu, "#58A6FF");
            DrawChart(MemChart, _svc.GetHistoryOrdered().mem, "#3FB950");
        });
    }

    private static void DrawChart(Canvas canvas, double[] values, string hexColor)
    {
        canvas.Children.Clear();

        double W = canvas.ActualWidth > 0 ? canvas.ActualWidth : 600;
        double H = canvas.ActualHeight > 0 ? canvas.ActualHeight : 200;

        var gridBrush = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
        for (int i = 1; i <= 4; i++)
        {
            var line = new Line { X1 = 0, X2 = W, Y1 = H * i / 4, Y2 = H * i / 4, Stroke = gridBrush, StrokeThickness = 1 };
            canvas.Children.Add(line);
        }

        if (values.Length == 0) return;

        var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(hexColor)!;
        var poly = new Polyline
        {
            Stroke = brush, StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round,
            StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round
        };

        double step = W / (values.Length - 1);
        for (int i = 0; i < values.Length; i++)
        {
            double x = i * step;
            double y = H - (Math.Clamp(values[i], 0, 100) / 100.0) * H;
            poly.Points.Add(new Point(x, y));
        }
        canvas.Children.Add(poly);

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
