using System.Text;

namespace NEManager.Core.Pe;

/// <summary>PE 文件解析器（支持 PE32/PE32+）</summary>
public static class PeParser
{
    public record PeInfo(
        bool IsValid,
        bool Is64Bit,
        string Machine,
        DateTime LinkerTimestamp,
        int SectionCount,
        string Subsystem,
        bool IsDll,
        int EntryPointRva,
        long ImageSize,
        string[] Sections,
        string? ImportsFrom,   // DLL导入列表（简化版）
        string Error = ""
    );

    public static PeInfo Parse(string path)
    {
        try
        {
            using var br = new BinaryReader(File.OpenRead(path));

            // DOS 头 MZ
            if (br.ReadUInt16() != 0x5A4D) return new PeInfo(false, false, "", DateTime.MinValue, 0, "", false, 0, 0, Array.Empty<string>(), null, "不是有效的 PE 文件");

            br.BaseStream.Position = 0x3C;
            var peOffset = br.ReadInt32();
            br.BaseStream.Position = peOffset;

            // PE 签名
            if (br.ReadUInt32() != 0x4550) return new PeInfo(false, false, "", DateTime.MinValue, 0, "", false, 0, 0, Array.Empty<string>(), null, "PE 签名无效");

            var machine = br.ReadUInt16();
            var sectionCount = br.ReadUInt16();
            var timestamp = br.ReadUInt32();
            br.ReadUInt32(); // 指针符号表
            br.ReadUInt32(); // 符号数
            var optHeaderSize = br.ReadUInt16();
            var characteristics = br.ReadUInt16();

            var isDll = (characteristics & 0x2000) != 0;

            // 可选头
            var optStart = br.BaseStream.Position;
            var magic = br.ReadUInt16();
            bool is64 = magic == 0x20b; // PE32+
            var subsystemOffset = optStart + (is64 ? 68 : 68);
            br.BaseStream.Position = subsystemOffset;
            var subsystemId = br.ReadUInt16();

            string subsystem = subsystemId switch
            {
                1 => "Native", 2 => "Windows GUI", 3 => "Windows Console",
                5 => "OS/2 Console", 7 => "POSIX Console", 8 => "Native", 9 => "Windows CE GUI",
                10 => "EFI Application", 11 => "EFI Boot Service Driver", 12 => "EFI Runtime Driver",
                13 => "EFI ROM", 14 => "Xbox", 16 => "Windows Boot Application",
                _ => $"Unknown ({subsystemId})"
            };

            // Entry point & Image size from optional header
            br.BaseStream.Position = optStart + 16;
            var entryPoint = br.ReadUInt32();
            br.BaseStream.Position = optStart + (is64 ? 56 : 56);
            var imageSize = br.ReadUInt32();

            // 节表
            br.BaseStream.Position = optStart + optHeaderSize;
            var sections = new string[sectionCount];
            for (int i = 0; i < sectionCount; i++)
            {
                var nameBytes = br.ReadBytes(8);
                sections[i] = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0');
                br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32();
                br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt32(); br.ReadUInt16(); br.ReadUInt16();
            }

            string machineStr = machine switch
            {
                0x14c => "x86 (i386)", 0x8664 => "x64 (AMD64)", 0x1c0 => "ARM",
                0xaa64 => "ARM64", 0x200 => "Intel Itanium", _ => $"Unknown (0x{machine:X})"
            };

            var linkerTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).DateTime.ToLocalTime();

            return new PeInfo(true, is64, machineStr, linkerTime, sectionCount, subsystem,
                isDll, (int)entryPoint, imageSize, sections, null);
        }
        catch (Exception ex)
        {
            return new PeInfo(false, false, "", DateTime.MinValue, 0, "", false, 0, 0, Array.Empty<string>(), null, ex.Message);
        }
    }
}
