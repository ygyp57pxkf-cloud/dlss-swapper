using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;

namespace DLSS_Swapper.EnhancementTools;

public sealed record GameTarget(string Exe, string Architecture, string[] GraphicsImports, string[] ExistingMods, string Guidance);

/// <summary>Read-only PE inspection. Static imports are clues, not a runtime API detection.</summary>
public static class GameTargetInspector
{
    static readonly string[] GraphicsDlls = ["d3d8.dll", "d3d9.dll", "ddraw.dll", "d3d11.dll", "d3d12.dll", "dxgi.dll", "opengl32.dll", "vulkan-1.dll"];
    static readonly string[] ModNames = ["version.dll", "dxgi.dll", "d3d9.dll", "d3d11.dll", "dinput8.dll", "winmm.dll", "winhttp.dll", "ReShade.ini", "reshade-shaders", ".dlss-swapper-fg"];

    public static GameTarget Inspect(string exe)
    {
        exe = Path.GetFullPath(exe);
        if (!string.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("请选择实际游戏 EXE。");
        using var stream = File.OpenRead(exe);
        using var pe = new PEReader(stream);
        var header = pe.PEHeaders.PEHeader ?? throw new IOException("不是有效的 Windows 游戏程序。");
        var architecture = pe.PEHeaders.CoffHeader.Machine switch
        {
            Machine.I386 => "32 位 x86",
            Machine.Amd64 => "64 位 x64",
            Machine.Arm64 => "ARM64（本工具链未验证）",
            _ => "未知架构",
        };
        var imports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var table = header.ImportTableDirectory;
        if (table.RelativeVirtualAddress != 0 && table.Size >= 20)
        {
            var reader = pe.GetSectionData(table.RelativeVirtualAddress).GetReader();
            int limit = Math.Min(Math.Min(table.Size / 20, reader.Length / 20), 4096);
            for (int i = 0; i < limit; i++)
            {
                var lookup = reader.ReadInt32();
                var timestamp = reader.ReadInt32();
                var forwarder = reader.ReadInt32();
                var nameRva = reader.ReadInt32();
                var address = reader.ReadInt32();
                if ((lookup | timestamp | forwarder | nameRva | address) == 0) break;
                if (nameRva <= 0) throw new IOException("EXE 导入表不完整。");
                var nameReader = pe.GetSectionData(nameRva).GetReader();
                var bytes = new List<byte>();
                bool terminated = false;
                for (int j = 0; j < Math.Min(512, nameReader.Length); j++)
                {
                    var value = nameReader.ReadByte();
                    if (value == 0) { terminated = true; break; }
                    bytes.Add(value);
                }
                if (!terminated) throw new IOException("EXE 导入名称不完整。");
                var name = Encoding.ASCII.GetString(bytes.ToArray()).ToLowerInvariant();
                if (GraphicsDlls.Contains(name)) imports.Add(name);
            }
        }
        var directory = Path.GetDirectoryName(exe)!;
        var mods = Directory.EnumerateFileSystemEntries(directory)
            .Select(p => Path.GetFileName(p)).Where(n => ModNames.Contains(n, StringComparer.OrdinalIgnoreCase)).Order().ToArray();
        string guidance = "静态导入不能确认实际渲染接口；动态加载、启动器和包装器可能隐藏接口。请结合游戏版本确认。";
        if (imports.Overlaps(["ddraw.dll", "d3d8.dll", "d3d9.dll"]))
            guidance += "\n旧接口先核对外部管理器的 dgVoodoo / Feeder 路线；32 位游戏需匹配 32 位 Feeder，NR host 使用 64 位。";
        if (imports.Contains("d3d11.dll") || imports.Contains("d3d12.dll"))
            guidance += "\n可从 ReShade / Feeder 路线试起，按游戏位数选择组件；不能据此认定 RenoDX 或 NR 已兼容。";
        if (mods.Length > 0)
            guidance += "\n发现同名代理/Mod 线索，不一定冲突。安装前在原管理器中核对归属和恢复方式，保留备份。";
        return new(exe, architecture, imports.Order().ToArray(), mods, guidance);
    }
}
