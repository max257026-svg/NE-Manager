namespace NEManager.Core.Pe;

/// <summary>
/// PE 资源提取辅助 —— 托管程序集的嵌入资源枚举。
/// 原生 PE 资源需要完整的资源树解析，此处仅返回托管资源名列表。
/// </summary>
public static class PeResourceExtractor
{
    public record ManagedResource(string Name, long Length, byte[] Data);

    /// <summary>
    /// 枚举 .NET 程序集的嵌入资源名称。
    /// 原生 PE 资源需要完整的资源树解析，此处仅返回托管资源名列表。
    /// </summary>
    public static List<ManagedResource> Extract(string pePath)
    {
        var list = new List<ManagedResource>();
        try
        {
            var asm = System.Reflection.Assembly.LoadFile(Path.GetFullPath(pePath));
            foreach (var name in asm.GetManifestResourceNames())
            {
                list.Add(new ManagedResource(name, 0, Array.Empty<byte>()));
            }
        }
        catch { /* 不是托管程序集或无法加载，返回空 */ }
        return list;
    }
}
