using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using NEManager.Core.SystemTools;

namespace NEManager.App.Views;

public partial class ProcessTreePage : UserControl
{
    public ProcessTreePage() { InitializeComponent(); Loaded += (_, _) => Refresh(); }

    private void Refresh()
    {
        try
        {
            var procs = ProcessManager.Enumerate().ToList();
            var byId = new Dictionary<int, TreeNode>();
            foreach (var p in procs)
                byId[p.Id] = new TreeNode { Pid = p.Id, Label = $"{p.Name} (PID:{p.Id})", ParentId = p.ParentId };

            foreach (var node in byId.Values)
            {
                if (node.ParentId > 0 && byId.TryGetValue(node.ParentId, out var parent))
                    parent.Children.Add(node);
            }

            var roots = byId.Values
                .Where(n => n.ParentId == 0 || !byId.ContainsKey(n.ParentId))
                .OrderBy(n => n.Pid)
                .ToList();

            ProcTree.ItemsSource = roots;
        }
        catch (Exception ex) { MessageBox.Show("枚举进程失败: " + ex.Message); }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => Refresh();
}

public class TreeNode
{
    public string Label { get; set; } = "";
    public int Pid { get; set; }
    public int ParentId { get; set; }
    public List<TreeNode> Children { get; } = new();
}
