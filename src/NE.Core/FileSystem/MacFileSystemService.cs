using System.IO;
using System.Text;
using System.Xml.Linq;

namespace NEManager.Core.FileSystem;

public static class MacFileSystemService
{
    // DMG 头部信息
    public class DmgInfo
    {
        public string Format { get; set; } = "";
        public long DataSize { get; set; }
        public string VolumeName { get; set; } = "";
        public string Error { get; set; } = "";
    }

    // 读取 DMG 信息
    public static DmgInfo ReadDmgInfo(string dmgPath)
    {
        var info = new DmgInfo();
        
        try
        {
            using var fs = File.OpenRead(dmgPath);
            using var reader = new BinaryReader(fs);
            
            // DMG 文件末尾有 koly block (512 字节)
            // 从文件末尾往前 512 字节
            fs.Seek(-512, SeekOrigin.End);
            
            var magic = Encoding.ASCII.GetString(reader.ReadBytes(4));
            if (magic == "koly")
            {
                info.Format = "DMG (koly)";
                
                // 跳过头部字段
                reader.ReadUInt32(); // version
                reader.ReadUInt32(); // headersize
                reader.ReadUInt32(); // flags
                reader.ReadUInt32(); // running_data_fork_offset
                var dataForkOffset = reader.ReadUInt64();
                var dataForkLength = reader.ReadUInt64();
                
                info.DataSize = (long)dataForkLength;
                
                // 继续读取更多元数据...
                // 实际 DMG 解析非常复杂，这里只读取基本信息
            }
            else
            {
                info.Format = "Unknown";
            }
            
            info.DataSize = new FileInfo(dmgPath).Length;
        }
        catch (Exception ex)
        {
            info.Error = ex.Message;
        }
        
        return info;
    }

    // Plist 文件读取
    public class PlistEntry
    {
        public string Key { get; set; } = "";
        public string Value { get; set; } = "";
        public string Type { get; set; } = "";
    }

    // 读取 XML Plist
    public static List<PlistEntry> ReadPlist(string plistPath)
    {
        var entries = new List<PlistEntry>();
        
        try
        {
            var doc = XDocument.Load(plistPath);
            var dict = doc.Root?.Element("dict");
            
            if (dict != null)
            {
                var elements = dict.Elements().ToList();
                for (int i = 0; i < elements.Count - 1; i += 2)
                {
                    if (elements[i].Name == "key")
                    {
                        var key = elements[i].Value;
                        var valueElement = elements[i + 1];
                        var value = valueElement.Value;
                        var type = valueElement.Name.LocalName;
                        
                        entries.Add(new PlistEntry
                        {
                            Key = key,
                            Value = value,
                            Type = type
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            throw new Exception($"Plist 读取失败: {ex.Message}");
        }
        
        return entries;
    }

    // 写入 XML Plist
    public static void WritePlist(string plistPath, List<PlistEntry> entries)
    {
        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("plist",
                new XAttribute("version", "1.0"),
                new XElement("dict",
                    entries.SelectMany(e => new XElement[]
                    {
                        new XElement("key", e.Key),
                        new XElement(e.Type, e.Value)
                    })
                )
            )
        );
        
        doc.Save(plistPath);
    }

    // 检测 HFS+ / APFS
    public static string DetectMacFileSystem(string imagePath)
    {
        try
        {
            using var fs = File.OpenRead(imagePath);
            using var reader = new BinaryReader(fs);
            
            // HFS+ signature at offset 0x400
            fs.Seek(0x400, SeekOrigin.Begin);
            var signature = Encoding.ASCII.GetString(reader.ReadBytes(2));
            if (signature == "H+" || signature == "HX")
                return "HFS+";
            
            // APFS magic at offset 0
            fs.Seek(0, SeekOrigin.Begin);
            var apfsMagic = reader.ReadUInt32();
            if (apfsMagic == 0x4253504E) // "NVSB" reversed
                return "APFS";
            
            return "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }
}
