using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NEManager.Core.Binary;

/// <summary>
/// 便携式可执行文件 (PE) 解析器 —— 纯 C# 实现，支持 EXE / DLL / SYS。
/// 覆盖：DOS/NT 头、节表、导入表、导出表、资源、重定位、校验和、数字签名。
/// </summary>
public sealed class PeParser
{
    // ==================== 模型 ====================

    public string FilePath { get; private set; } = string.Empty;
    public bool IsValid { get; private set; }
    public string Error { get; private set; } = string.Empty;

    // ---- 文件头 ----
    public ushort Machine { get; private set; }
    public string MachineName { get; private set; } = string.Empty;
    public ushort NumberOfSections { get; private set; }
    public uint TimeDateStamp { get; private set; }
    public DateTime TimeStamp => DateTimeOffset.FromUnixTimeSeconds(TimeDateStamp).LocalDateTime;
    public ushort Characteristics { get; private set; }
    public ushort SizeOfOptionalHeader { get; private set; }

    // ---- 可选头 ----
    public bool Is64Bit { get; private set; }
    public bool IsPe32Plus { get; private set; }
    public ushort Subsystem { get; private set; }
    public string SubsystemName { get; private set; } = string.Empty;
    public ushort DllCharacteristics { get; private set; }
    public ulong ImageBase { get; private set; }
    public uint EntryPoint { get; private set; }
    public uint SizeOfImage { get; private set; }
    public uint SizeOfHeaders { get; private set; }
    public uint SectionAlignment { get; private set; }
    public uint FileAlignment { get; private set; }
    public uint StoredCheckSum { get; private set; }
    public uint CalculatedCheckSum { get; private set; }
    public bool CheckSumValid => StoredCheckSum == CalculatedCheckSum;
    public ushort LinkerVersion { get; private set; }
    public string LinkerVersionText => $"{LinkerVersion >> 8}.{LinkerVersion & 0xFF}";

    // ---- 派生属性 ----
    public bool IsDll => (Characteristics & 0x2000) != 0;
    public bool IsExecutable => (Characteristics & 0x0002) != 0;
    public bool IsSystemFile => (Characteristics & 0x1000) != 0;
    public bool IsDriver => Subsystem == 1; // IMAGE_SUBSYSTEM_NATIVE
    public bool IsDotNet => DataDirectories.Count > 14 && DataDirectories[14].Size > 0;
    public bool IsSigned { get; private set; }

    public string FileKindText => IsDriver ? "内核驱动程序 (.sys)" : IsDll ? "动态链接库 (.dll)" : "可执行程序 (.exe)";

    public string CharacteristicsText
    {
        get
        {
            var flags = new List<string>();
            if ((Characteristics & 0x0001) != 0) flags.Add("无重定位");
            if ((Characteristics & 0x0002) != 0) flags.Add("可执行映像");
            if ((Characteristics & 0x0020) != 0) flags.Add(">2GB 地址感知");
            if ((Characteristics & 0x0100) != 0) flags.Add("32 位机器");
            if ((Characteristics & 0x0200) != 0) flags.Add("调试信息已剥离");
            if ((Characteristics & 0x1000) != 0) flags.Add("系统文件");
            if ((Characteristics & 0x2000) != 0) flags.Add("DLL");
            if ((Characteristics & 0x8000) != 0) flags.Add("字节序反转");
            return string.Join(", ", flags);
        }
    }

    public string DllCharacteristicsText
    {
        get
        {
            var flags = new List<string>();
            if ((DllCharacteristics & 0x0020) != 0) flags.Add("HIGH_ENTROPY_VA");
            if ((DllCharacteristics & 0x0040) != 0) flags.Add("动态基址 (ASLR)");
            if ((DllCharacteristics & 0x0080) != 0) flags.Add("强制完整性");
            if ((DllCharacteristics & 0x0100) != 0) flags.Add("NX 兼容 (DEP)");
            if ((DllCharacteristics & 0x0400) != 0) flags.Add("禁止 SEH");
            if ((DllCharacteristics & 0x1000) != 0) flags.Add("终端服务感知");
            if ((DllCharacteristics & 0x4000) != 0) flags.Add("控制流防护 (CFG)");
            return flags.Count == 0 ? "无" : string.Join(", ", flags);
        }
    }

    public class Section
    {
        public string Name { get; set; } = string.Empty;
        public uint VirtualSize { get; set; }
        public uint VirtualAddress { get; set; }
        public uint RawSize { get; set; }
        public uint RawAddress { get; set; }
        public uint Characteristics { get; set; }

        public double Entropy { get; set; }
        public string EntropyText => Entropy.ToString("F2");

        public string CharacteristicsText
        {
            get
            {
                var flags = new List<string>();
                if ((Characteristics & 0x00000020) != 0) flags.Add("代码");
                if ((Characteristics & 0x00000040) != 0) flags.Add("已初始化数据");
                if ((Characteristics & 0x00000080) != 0) flags.Add("未初始化数据");
                if ((Characteristics & 0x02000000) != 0) flags.Add("可丢弃");
                if ((Characteristics & 0x10000000) != 0) flags.Add("共享");
                if ((Characteristics & 0x20000000) != 0) flags.Add("可执行");
                if ((Characteristics & 0x40000000) != 0) flags.Add("可读");
                if ((Characteristics & 0x80000000) != 0) flags.Add("可写");
                return string.Join(", ", flags);
            }
        }

        /// <summary>节内容高熵通常意味着被加壳或加密。</summary>
        public bool IsPacked => Entropy > 7.2 && (Characteristics & 0x00000020) != 0;
        public string PackedText => IsPacked ? "是" : "否";
    }

    public class ImportInfo
    {
        public string DllName { get; set; } = string.Empty;
        public uint OriginalFirstThunk { get; set; }
        public uint FirstThunk { get; set; }
        public List<string> Functions { get; set; } = new();
        public int FunctionCount => Functions.Count;
    }

    public class ExportInfo
    {
        public string Name { get; set; } = string.Empty;
        public uint Ordinal { get; set; }
        public uint Address { get; set; }
        public string? Forwarder { get; set; }
    }

    public class ResourceEntry
    {
        public string Type { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public uint LanguageId { get; set; }
        public string LanguageName { get; set; } = string.Empty;
        public uint Rva { get; set; }
        public uint Size { get; set; }
        public uint CodePage { get; set; }
        public int Level { get; set; }

        public string SizeText => FileSystem.FileItem.FormatSize(Size);
    }

    public struct DataDirectory
    {
        public uint Rva { get; set; }
        public uint Size { get; set; }
        public string Name { get; set; }
        public string RvaText => $"0x{Rva:X8}";
        public string SizeText => Size.ToString();
    }

    // ---- 集合 ----
    public List<Section> Sections { get; } = new();
    public List<ImportInfo> Imports { get; } = new();
    public List<ExportInfo> Exports { get; } = new();
    public List<ResourceEntry> Resources { get; } = new();
    public List<DataDirectory> DataDirectories { get; } = new();
    public string ExportName { get; private set; } = string.Empty;
    public uint ExportOrdinalBase { get; private set; }

    private byte[] _data = Array.Empty<byte>();
    private uint _peOffset;
    private uint _optionalHeaderOffset;
    private uint _sectionTableOffset;

    private static readonly string[] DirectoryNames =
    {
        "导出表", "导入表", "资源表", "异常表", "证书表", "重定位表", "调试信息",
        "体系结构", "全局指针", "TLS 表", "加载配置", "绑定导入", "导入地址表",
        "延迟导入", "CLR 运行时头", "保留"
    };

    private static readonly Dictionary<uint, string> LanguageNames = new()
    {
        [0x0409] = "英语(美国)",
        [0x0804] = "中文(简体)",
        [0x0404] = "中文(繁体-台湾)",
        [0x0C04] = "中文(繁体-香港)",
        [0x1004] = "中文(简体-新加坡)",
        [0x0411] = "日语",
        [0x0412] = "韩语",
        [0x0407] = "德语",
        [0x040C] = "法语",
        [0x0410] = "意大利语",
        [0x0419] = "俄语",
        [0x0000] = "中性语言"
    };

    // ==================== 解析入口 ====================

    public static PeParser Parse(string filePath)
    {
        var parser = new PeParser();
        parser.Load(filePath);
        return parser;
    }

    private void Load(string filePath)
    {
        FilePath = filePath;
        try
        {
            _data = File.ReadAllBytes(filePath);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return;
        }

        if (_data.Length < 64)
        {
            Error = "文件太小，不是有效的 PE 文件。";
            return;
        }

        // DOS 头签名 "MZ"
        if (_data[0] != 'M' || _data[1] != 'Z')
        {
            Error = "缺少 MZ 签名，不是有效的 PE 文件。";
            return;
        }

        _peOffset = BitConverter.ToUInt32(_data, 0x3C);
        if (_peOffset + 24 >= _data.Length)
        {
            Error = "PE 头偏移越界。";
            return;
        }

        // PE 签名 "PE\0\0"
        if (_data[_peOffset] != 'P' || _data[_peOffset + 1] != 'E' ||
            _data[_peOffset + 2] != 0 || _data[_peOffset + 3] != 0)
        {
            Error = "缺少 PE 签名。";
            return;
        }

        IsValid = true; // 签名已校验通过，允许后续解析流程推进
        ParseFileHeader();
        if (!IsValid) return;

        ParseOptionalHeader();
        ParseSections();
        ParseDirectories();
        ParseExports();
        ParseImports();
        ParseResources();
        CalculateCheckSum();
        CheckSignature();
        IsValid = string.IsNullOrEmpty(Error);
    }

    private void ParseFileHeader()
    {
        uint offset = _peOffset + 4;
        Machine = BitConverter.ToUInt16(_data, (int)offset);
        NumberOfSections = BitConverter.ToUInt16(_data, (int)(offset + 2));
        TimeDateStamp = BitConverter.ToUInt32(_data, (int)(offset + 4));
        SizeOfOptionalHeader = BitConverter.ToUInt16(_data, (int)(offset + 16));
        Characteristics = BitConverter.ToUInt16(_data, (int)(offset + 18));

        _optionalHeaderOffset = offset + 20;
        _sectionTableOffset = _optionalHeaderOffset + SizeOfOptionalHeader;

        MachineName = Machine switch
        {
            0x014c => "x86 (i386)",
            0x8664 => "x64 (AMD64)",
            0x01c0 => "ARM",
            0xAA64 => "ARM64",
            0x0200 => "IA64 (Itanium)",
            0x01c4 => "ARMv7 Thumb-2",
            0x0166 => "MIPS",
            0x0EBC => "RISC-V 32",
            0x5064 => "RISC-V 64",
            _ => $"未知 (0x{Machine:X4})"
        };
    }

    private void ParseOptionalHeader()
    {
        ushort magic = BitConverter.ToUInt16(_data, (int)_optionalHeaderOffset);
        IsPe32Plus = magic == 0x20b;
        Is64Bit = IsPe32Plus;

        if (magic != 0x10b && magic != 0x20b && magic != 0x107)
        {
            Error = $"未知的可选头魔数：0x{magic:X4}";
            return;
        }

        int o = (int)_optionalHeaderOffset;
        LinkerVersion = _data[o + 3];      // MinorLinkerVersion
        LinkerVersion = (ushort)((_data[o + 2] << 8) | _data[o + 3]);

        if (IsPe32Plus)
        {
            ImageBase = BitConverter.ToUInt64(_data, o + 24);
            SectionAlignment = BitConverter.ToUInt32(_data, o + 32);
            FileAlignment = BitConverter.ToUInt32(_data, o + 36);
            SizeOfImage = BitConverter.ToUInt32(_data, o + 56);
            SizeOfHeaders = BitConverter.ToUInt32(_data, o + 60);
            StoredCheckSum = BitConverter.ToUInt32(_data, o + 64);
            Subsystem = BitConverter.ToUInt16(_data, o + 68);
            DllCharacteristics = BitConverter.ToUInt16(_data, o + 70);
            EntryPoint = BitConverter.ToUInt32(_data, o + 16);
        }
        else
        {
            ImageBase = BitConverter.ToUInt32(_data, o + 28);
            SectionAlignment = BitConverter.ToUInt32(_data, o + 32);
            FileAlignment = BitConverter.ToUInt32(_data, o + 36);
            SizeOfImage = BitConverter.ToUInt32(_data, o + 56);
            SizeOfHeaders = BitConverter.ToUInt32(_data, o + 60);
            StoredCheckSum = BitConverter.ToUInt32(_data, o + 64);
            Subsystem = BitConverter.ToUInt16(_data, o + 68);
            DllCharacteristics = BitConverter.ToUInt16(_data, o + 70);
            EntryPoint = BitConverter.ToUInt32(_data, o + 16);
        }

        SubsystemName = Subsystem switch
        {
            0 => "未知",
            1 => "原生（NT 驱动）",
            2 => "Windows 图形界面 (GUI)",
            3 => "Windows 控制台 (CUI)",
            5 => "OS/2 控制台",
            7 => "POSIX 控制台",
            9 => "Windows CE",
            10 => "EFI 应用程序",
            11 => "EFI 引导服务驱动",
            12 => "EFI 运行时驱动",
            13 => "EFI ROM 映像",
            14 => "Xbox",
            16 => "Windows 引导应用程序",
            _ => $"未知 ({Subsystem})"
        };
    }

    private void ParseSections()
    {
        for (int i = 0; i < NumberOfSections; i++)
        {
            int o = (int)_sectionTableOffset + i * 40;
            if (o + 40 > _data.Length) break;

            var nameBytes = new byte[8];
            Array.Copy(_data, o, nameBytes, 0, 8);
            var name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');

            var section = new Section
            {
                Name = name,
                VirtualSize = BitConverter.ToUInt32(_data, o + 8),
                VirtualAddress = BitConverter.ToUInt32(_data, o + 12),
                RawSize = BitConverter.ToUInt32(_data, o + 16),
                RawAddress = BitConverter.ToUInt32(_data, o + 20),
                Characteristics = BitConverter.ToUInt32(_data, o + 36)
            };

            section.Entropy = ComputeEntropy(section.RawAddress, section.RawSize);
            Sections.Add(section);
        }
    }

    private void ParseDirectories()
    {
        int dirOffset = IsPe32Plus ? (int)_optionalHeaderOffset + 112 : (int)_optionalHeaderOffset + 96;
        uint count = IsPe32Plus
            ? BitConverter.ToUInt32(_data, (int)_optionalHeaderOffset + 108)
            : BitConverter.ToUInt32(_data, (int)_optionalHeaderOffset + 92);

        count = Math.Min(count, 16);
        for (int i = 0; i < count; i++)
        {
            int o = dirOffset + i * 8;
            if (o + 8 > _data.Length) break;

            DataDirectories.Add(new DataDirectory
            {
                Rva = BitConverter.ToUInt32(_data, o),
                Size = BitConverter.ToUInt32(_data, o + 4),
                Name = i < DirectoryNames.Length ? DirectoryNames[i] : $"目录 {i}"
            });
        }
    }

    // ==================== RVA ↔ 文件偏移 ====================

    private uint RvaToOffset(uint rva)
    {
        foreach (var s in Sections)
        {
            if (rva >= s.VirtualAddress && rva < s.VirtualAddress + Math.Max(s.VirtualSize, s.RawSize))
            {
                long delta = rva - s.VirtualAddress;
                if (delta < s.RawSize)
                    return s.RawAddress + (uint)delta;
            }
        }
        // 头部区域
        if (rva < SizeOfHeaders) return rva;
        return 0;
    }

    // ==================== 导出表 ====================

    private void ParseExports()
    {
        if (DataDirectories.Count == 0 || DataDirectories[0].Rva == 0) return;

        uint offset = RvaToOffset(DataDirectories[0].Rva);
        if (offset == 0 || offset + 40 > _data.Length) return;

        ExportOrdinalBase = BitConverter.ToUInt32(_data, (int)(offset + 16));
        uint numberOfFunctions = BitConverter.ToUInt32(_data, (int)(offset + 20));
        uint numberOfNames = BitConverter.ToUInt32(_data, (int)(offset + 24));
        uint addressOfFunctions = BitConverter.ToUInt32(_data, (int)(offset + 28));
        uint addressOfNames = BitConverter.ToUInt32(_data, (int)(offset + 32));
        uint addressOfNameOrdinals = BitConverter.ToUInt32(_data, (int)(offset + 36));

        uint nameRva = BitConverter.ToUInt32(_data, (int)(offset + 12));
        ExportName = ReadStringAtRva(nameRva);

        var nameMap = new Dictionary<uint, string>();
        for (uint i = 0; i < Math.Min(numberOfNames, 10000); i++)
        {
            uint namePtrOffset = RvaToOffset(addressOfNames + i * 4);
            uint ordinalOffset = RvaToOffset(addressOfNameOrdinals + i * 2);
            if (namePtrOffset == 0 || ordinalOffset == 0) continue;

            uint nameRva2 = BitConverter.ToUInt32(_data, (int)namePtrOffset);
            ushort ordinal = BitConverter.ToUInt16(_data, (int)ordinalOffset);
            nameMap[ordinal] = ReadStringAtRva(nameRva2);
        }

        for (uint i = 0; i < Math.Min(numberOfFunctions, 10000); i++)
        {
            uint funcOffset = RvaToOffset(addressOfFunctions + i * 4);
            if (funcOffset == 0) continue;

            uint funcRva = BitConverter.ToUInt32(_data, (int)funcOffset);
            if (funcRva == 0) continue;

            // 转发函数：RVA 落在导出表目录范围内
            bool isForwarder = funcRva >= DataDirectories[0].Rva &&
                               funcRva < DataDirectories[0].Rva + DataDirectories[0].Size;

            string? forwarder = null;
            if (isForwarder)
            {
                uint fwdOffset = RvaToOffset(funcRva);
                forwarder = ReadAsciiString(fwdOffset);
            }

            Exports.Add(new ExportInfo
            {
                Ordinal = ExportOrdinalBase + i,
                Address = funcRva,
                Name = nameMap.TryGetValue(i, out var n) ? n : string.Empty,
                Forwarder = forwarder
            });
        }
    }

    // ==================== 导入表 ====================

    private void ParseImports()
    {
        if (DataDirectories.Count < 2 || DataDirectories[1].Rva == 0) return;

        uint descriptorOffset = RvaToOffset(DataDirectories[1].Rva);
        if (descriptorOffset == 0) return;

        for (uint i = 0; i < 512; i++)
        {
            uint o = descriptorOffset + i * 20;
            if (o + 20 > _data.Length) break;

            uint originalFirstThunk = BitConverter.ToUInt32(_data, (int)o);
            uint nameRva = BitConverter.ToUInt32(_data, (int)(o + 12));
            uint firstThunk = BitConverter.ToUInt32(_data, (int)(o + 16));

            // 全零描述符 = 结束
            if (originalFirstThunk == 0 && nameRva == 0 && firstThunk == 0) break;

            var import = new ImportInfo
            {
                DllName = ReadStringAtRva(nameRva),
                OriginalFirstThunk = originalFirstThunk,
                FirstThunk = firstThunk
            };

            if (string.IsNullOrEmpty(import.DllName)) continue;

            // 遍历 thunk 表读取函数名
            uint thunkRva = originalFirstThunk != 0 ? originalFirstThunk : firstThunk;
            uint thunkOffset = RvaToOffset(thunkRva);
            if (thunkOffset != 0)
            {
                for (uint j = 0; j < 4096; j++)
                {
                    uint entryOffset = thunkOffset + j * (IsPe32Plus ? 8u : 4u);
                    if (entryOffset + (IsPe32Plus ? 8 : 4) > _data.Length) break;

                    ulong value = IsPe32Plus
                        ? BitConverter.ToUInt64(_data, (int)entryOffset)
                        : BitConverter.ToUInt32(_data, (int)entryOffset);

                    if (value == 0) break;

                    if (IsPe32Plus && (value & 0x8000000000000000) != 0)
                    {
                        import.Functions.Add($"序号 {value & 0xFFFF}");
                    }
                    else if (!IsPe32Plus && (value & 0x80000000) != 0)
                    {
                        import.Functions.Add($"序号 {value & 0xFFFF}");
                    }
                    else
                    {
                        // Hint(2 bytes) + Name
                        uint hintOffset = RvaToOffset((uint)(value & 0xFFFFFFFF) + 2);
                        import.Functions.Add(ReadAsciiString(hintOffset));
                    }
                }
            }

            Imports.Add(import);
        }
    }

    // ==================== 资源 ====================

    private void ParseResources()
    {
        if (DataDirectories.Count < 3 || DataDirectories[2].Rva == 0) return;

        uint baseOffset = RvaToOffset(DataDirectories[2].Rva);
        if (baseOffset == 0) return;

        WalkResourceDirectory(baseOffset, baseOffset, 0, null, null, 0);
    }

    private void WalkResourceDirectory(uint baseOffset, uint dirOffset, int level,
        string? typeName, string? nameName, uint langId)
    {
        if (level > 3 || dirOffset + 16 > _data.Length) return;

        ushort namedCount = BitConverter.ToUInt16(_data, (int)(dirOffset + 12));
        ushort idCount = BitConverter.ToUInt16(_data, (int)(dirOffset + 14));
        int total = namedCount + idCount;

        for (int i = 0; i < Math.Min(total, 4096); i++)
        {
            uint entryOffset = dirOffset + 16 + (uint)(i * 8);
            if (entryOffset + 8 > _data.Length) break;

            uint nameOrId = BitConverter.ToUInt32(_data, (int)entryOffset);
            uint dataOffset = BitConverter.ToUInt32(_data, (int)(entryOffset + 4));

            string idText;
            if ((nameOrId & 0x80000000) != 0)
            {
                uint stringOffset = baseOffset + (nameOrId & 0x7FFFFFFF);
                idText = ReadUnicodeStringAtOffset(stringOffset);
            }
            else
            {
                uint id = nameOrId & 0xFFFF;
                idText = level == 0 ? GetResourceTypeName(id) : id.ToString();
            }

            if ((dataOffset & 0x80000000) != 0)
            {
                // 子目录
                uint subDirOffset = baseOffset + (dataOffset & 0x7FFFFFFF);
                switch (level)
                {
                    case 0:
                        WalkResourceDirectory(baseOffset, subDirOffset, 1, idText, null, 0);
                        break;
                    case 1:
                        WalkResourceDirectory(baseOffset, subDirOffset, 2, typeName, idText, 0);
                        break;
                    case 2:
                        WalkResourceDirectory(baseOffset, subDirOffset, 3, typeName, nameName,
                            uint.TryParse(idText, out var l) ? l : 0);
                        break;
                }
            }
            else
            {
                // 数据项
                uint entryRvaOffset = baseOffset + dataOffset;
                if (entryRvaOffset + 16 > _data.Length) continue;

                uint dataRva = BitConverter.ToUInt32(_data, (int)entryRvaOffset);
                uint size = BitConverter.ToUInt32(_data, (int)(entryRvaOffset + 4));
                uint codePage = BitConverter.ToUInt32(_data, (int)(entryRvaOffset + 8));

                Resources.Add(new ResourceEntry
                {
                    Type = typeName ?? "未知类型",
                    Name = nameName ?? idText,
                    LanguageId = langId,
                    LanguageName = LanguageNames.TryGetValue(langId, out var ln) ? ln : $"0x{langId:X4}",
                    Rva = dataRva,
                    Size = size,
                    CodePage = codePage,
                    Level = level
                });
            }
        }
    }

    private static string GetResourceTypeName(uint id) => id switch
    {
        1 => "光标 (RT_CURSOR)",
        2 => "位图 (RT_BITMAP)",
        3 => "图标 (RT_ICON)",
        4 => "菜单 (RT_MENU)",
        5 => "对话框 (RT_DIALOG)",
        6 => "字符串表 (RT_STRING)",
        7 => "字体目录 (RT_FONTDIR)",
        8 => "字体 (RT_FONT)",
        9 => "加速键 (RT_ACCELERATOR)",
        10 => "资源数据 (RT_RCDATA)",
        11 => "消息表 (RT_MESSAGETABLE)",
        12 => "光标组 (RT_GROUP_CURSOR)",
        14 => "图标组 (RT_GROUP_ICON)",
        16 => "版本信息 (RT_VERSION)",
        17 => "对话框链接 (RT_DLGINCLUDE)",
        19 => "即插即用 (RT_PLUGPLAY)",
        20 => "VXD",
        21 => "动画光标 (RT_ANICURSOR)",
        22 => "动画图标 (RT_ANIICON)",
        23 => "HTML (RT_HTML)",
        24 => "清单 (RT_MANIFEST)",
        _ => $"未知类型 ({id})"
    };

    // ==================== 校验和 ====================

    private void CalculateCheckSum()
    {
        // PE 校验和算法：按字累加，进位回卷，再加上文件长度
        int checksumOffset = (int)(_optionalHeaderOffset + 64);
        uint stored = BitConverter.ToUInt32(_data, checksumOffset);

        // 计算时把原校验和位置置零
        Array.Copy(BitConverter.GetBytes(0u), 0, _data, checksumOffset, 4);

        ulong sum = 0;
        int length = _data.Length;
        int i = 0;

        for (; i + 1 < length; i += 2)
        {
            sum += (ushort)(_data[i] | (_data[i + 1] << 8));
            sum = (sum >> 16) + (sum & 0xFFFF);
        }
        if (i < length)
        {
            sum += _data[i];
            sum = (sum >> 16) + (sum & 0xFFFF);
        }

        sum = (sum >> 16) + (sum & 0xFFFF);
        sum += (uint)length;

        CalculatedCheckSum = (uint)(sum & 0xFFFFFFFF);

        // 恢复原始值
        Array.Copy(BitConverter.GetBytes(stored), 0, _data, checksumOffset, 4);
    }

    // ==================== 数字签名 ====================

    private void CheckSignature()
    {
        try
        {
            using var cert = X509Certificate.CreateFromSignedFile(FilePath);
            IsSigned = cert != null;
        }
        catch
        {
            IsSigned = false;
        }
    }

    /// <summary>
    /// 读取文件的数字签名证书信息。
    /// </summary>
    public List<(string Field, string Value)> GetSignatureInfo()
    {
        var info = new List<(string, string)>();
        try
        {
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(FilePath));
            info.Add(("已签名", "是"));
            info.Add(("颁发给", cert.Subject));
            info.Add(("颁发者", cert.Issuer));
            info.Add(("序列号", cert.SerialNumber));
            info.Add(("有效期自", cert.NotBefore.ToString("yyyy-MM-dd HH:mm:ss")));
            info.Add(("有效期至", cert.NotAfter.ToString("yyyy-MM-dd HH:mm:ss")));
            info.Add(("指纹 (SHA1)", cert.Thumbprint));
            info.Add(("算法", cert.SignatureAlgorithm.FriendlyName ?? cert.SignatureAlgorithm.Value ?? ""));
            info.Add(("当前时间有效", cert.NotBefore <= DateTime.Now && DateTime.Now <= cert.NotAfter ? "是" : "否（已过期或未生效）"));

            // 证书目录大小
            if (DataDirectories.Count > 4)
                info.Add(("证书表大小", $"{DataDirectories[4].Size} 字节"));
        }
        catch (Exception ex)
        {
            info.Add(("已签名", "否"));
            info.Add(("说明", ex.Message));
        }
        return info;
    }

    // ==================== 工具方法 ====================

    private string ReadStringAtRva(uint rva)
    {
        if (rva == 0) return string.Empty;
        uint offset = RvaToOffset(rva);
        return offset == 0 ? string.Empty : ReadAsciiString(offset);
    }

    private string ReadAsciiString(uint offset)
    {
        if (offset >= _data.Length) return string.Empty;
        int end = (int)offset;
        while (end < _data.Length && _data[end] != 0) end++;
        int len = Math.Min(end - (int)offset, 512);
        return Encoding.ASCII.GetString(_data, (int)offset, len);
    }

    private string ReadUnicodeStringAtOffset(uint offset)
    {
        try
        {
            if (offset + 2 > _data.Length) return string.Empty;
            ushort len = BitConverter.ToUInt16(_data, (int)offset);
            if (offset + 2 + len * 2 > _data.Length) return string.Empty;
            return Encoding.Unicode.GetString(_data, (int)offset + 2, len * 2);
        }
        catch
        {
            return string.Empty;
        }
    }

    private double ComputeEntropy(uint rawAddress, uint rawSize)
    {
        if (rawSize == 0 || rawAddress + rawSize > _data.Length) return 0;

        var frequencies = new long[256];
        long total = 0;
        for (uint i = rawAddress; i < rawAddress + rawSize && i < _data.Length; i++)
        {
            frequencies[_data[i]]++;
            total++;
        }

        if (total == 0) return 0;

        double entropy = 0;
        foreach (long count in frequencies)
        {
            if (count == 0) continue;
            double p = count / (double)total;
            entropy -= p * Math.Log2(p);
        }
        return entropy;
    }

    /// <summary>
    /// 读取指定节的原始字节（用于十六进制编辑）。
    /// </summary>
    public byte[] ReadSectionData(string sectionName)
    {
        var section = Sections.FirstOrDefault(s => s.Name.Equals(sectionName, StringComparison.Ordinal));
        if (section == null) return Array.Empty<byte>();

        int len = (int)Math.Min(section.RawSize, _data.Length - section.RawAddress);
        var result = new byte[len];
        Array.Copy(_data, (int)section.RawAddress, result, 0, len);
        return result;
    }

    /// <summary>
    /// 生成一份人类可读的 PE 报告。
    /// </summary>
    public string GenerateReport()
    {
        if (!IsValid) return $"解析失败：{Error}";

        var sb = new StringBuilder();
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine("  NE 管理器 · PE 文件分析报告");
        sb.AppendLine("════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"文件路径      : {FilePath}");
        sb.AppendLine($"文件类型      : {FileKindText}");
        sb.AppendLine($"目标架构      : {MachineName}");
        sb.AppendLine($"位数          : {(Is64Bit ? "64 位" : "32 位")}");
        sb.AppendLine($"子系统        : {SubsystemName}");
        sb.AppendLine($"链接器版本    : {LinkerVersionText}");
        sb.AppendLine($"编译时间戳    : {TimeStamp:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"入口点 RVA    : 0x{EntryPoint:X8}");
        sb.AppendLine($"映像基址      : 0x{ImageBase:X}");
        sb.AppendLine($"映像大小      : {FileSystem.FileItem.FormatSize(SizeOfImage)}");
        sb.AppendLine($"数字签名      : {(IsSigned ? "已签名" : "未签名或签名无效")}");
        sb.AppendLine($"校验和        : 存储 0x{StoredCheckSum:X8} / 计算 0x{CalculatedCheckSum:X8} " +
                      $"{(CheckSumValid ? "✓ 一致" : "✗ 不一致（文件可能被修改）")}");
        if (IsDotNet) sb.AppendLine($"运行时        : .NET 托管程序集");
        sb.AppendLine();

        sb.AppendLine($"【文件头特征】{CharacteristicsText}");
        sb.AppendLine($"【安全特性】{DllCharacteristicsText}");
        sb.AppendLine();

        sb.AppendLine("【节表】");
        sb.AppendLine($"{"名称",-10}{"虚拟大小",-12}{"虚拟地址",-12}{"原始大小",-12}{"熵值",-8}特征");
        sb.AppendLine(new string('─', 90));
        foreach (var s in Sections)
        {
            sb.AppendLine($"{s.Name,-10}{s.VirtualSize,-12:X8}{s.VirtualAddress,-12:X8}{s.RawSize,-12:X8}{s.Entropy,-8:F2}{s.CharacteristicsText}");
            if (s.IsPacked) sb.AppendLine($"            ⚠ 熵值偏高，该节可能被加壳或加密");
        }
        sb.AppendLine();

        if (Imports.Count > 0)
        {
            sb.AppendLine($"【导入表】(共 {Imports.Count} 个依赖模块)");
            foreach (var imp in Imports)
            {
                sb.AppendLine($"  ▸ {imp.DllName} ({imp.FunctionCount} 个函数)");
                foreach (var f in imp.Functions.Take(12))
                    sb.AppendLine($"      {f}");
                if (imp.FunctionCount > 12)
                    sb.AppendLine($"      … 其余 {imp.FunctionCount - 12} 个");
            }
            sb.AppendLine();
        }

        if (Exports.Count > 0)
        {
            sb.AppendLine($"【导出表】(共 {Exports.Count} 项，模块名：{ExportName})");
            foreach (var exp in Exports.Take(50))
            {
                var name = string.IsNullOrEmpty(exp.Name) ? $"(序号 {exp.Ordinal})" : exp.Name;
                sb.AppendLine($"  序号 {exp.Ordinal,-6} {name,-45} 0x{exp.Address:X8}" +
                              (exp.Forwarder != null ? $" → 转发至 {exp.Forwarder}" : ""));
            }
            if (Exports.Count > 50) sb.AppendLine($"  … 其余 {Exports.Count - 50} 项");
            sb.AppendLine();
        }

        if (Resources.Count > 0)
        {
            sb.AppendLine($"【资源表】(共 {Resources.Count} 项)");
            foreach (var r in Resources.Take(40))
                sb.AppendLine($"  {r.Type,-28}{r.Name,-20}{r.LanguageName,-16}{r.SizeText}");
            if (Resources.Count > 40) sb.AppendLine($"  … 其余 {Resources.Count - 40} 项");
        }

        return sb.ToString();
    }
}
