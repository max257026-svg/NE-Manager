using System.Reflection.PortableExecutable;
using ICSharpCode.Decompiler;
using ICSharpCode.Decompiler.CSharp;
using ICSharpCode.Decompiler.Metadata;
using ICSharpCode.Decompiler.TypeSystem;

namespace NEManager.Core.Decompiler;

public static class DotNetDecompiler
{
    public record DecompileResult(bool Success, string Code, string Error, List<string> Types, string AssemblyName, string TargetFramework);

    /// <summary>判断文件是否为 .NET 程序集</summary>
    public static bool IsDotNetAssembly(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var peReader = new PEReader(stream);
            return peReader.HasMetadata;
        }
        catch { return false; }
    }

    /// <summary>反编译整个程序集</summary>
    public static DecompileResult Decompile(string assemblyPath)
    {
        try
        {
            var settings = new DecompilerSettings { ShowXmlDocumentation = false };
            var decompiler = new CSharpDecompiler(assemblyPath, settings);

            var types = decompiler.TypeSystem.MainModule.TypeDefinitions
                .Select(t => t.FullName)
                .ToList();

            var code = decompiler.DecompileWholeModuleAsString();

            return new DecompileResult(true, code, string.Empty, types,
                Path.GetFileNameWithoutExtension(assemblyPath), "IL/.NET");
        }
        catch (Exception ex)
        {
            return new DecompileResult(false, string.Empty, ex.Message, new List<string>(), string.Empty, string.Empty);
        }
    }

    /// <summary>反编译单个类型</summary>
    public static DecompileResult DecompileType(string assemblyPath, string typeFullName)
    {
        try
        {
            var settings = new DecompilerSettings();
            var decompiler = new CSharpDecompiler(assemblyPath, settings);
            var code = decompiler.DecompileTypeAsString(new FullTypeName(typeFullName));
            return new DecompileResult(true, code, string.Empty, new List<string> { typeFullName },
                Path.GetFileNameWithoutExtension(assemblyPath), string.Empty);
        }
        catch (Exception ex)
        {
            return new DecompileResult(false, string.Empty, ex.Message, new List<string>(), string.Empty, string.Empty);
        }
    }
}
