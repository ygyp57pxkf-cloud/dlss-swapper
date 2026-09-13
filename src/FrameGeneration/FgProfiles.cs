using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.FrameGeneration;

public sealed record FgGpuProfile(string Name, string Router, string Recommendation)
{
    public override string ToString() => Name;
    public static readonly FgGpuProfile[] All = [
        new("RTX 3080 / 30 系列（待本机实测）", "SM86", "建议先用 2×、PTX、精确模式；3080 10GB 注意 4K 显存预算。基础帧率和刷新率合适后再试 4×。"),
        new("CMP 40HX 解锁版 / SM75（帧生成待实测）", "SM75", "仅作为 SM75 实验配置，不等同于已验证的 2060 SUPER。先确认驱动正常、NGX/CUDA 可用，从 1080p、2×、关闭光追开始；不改驱动或显卡名称。"),
        new("RTX 20 系列（实验）", "SM75", "物理 Turing 验证仍不足。使用 SM75 + PTX + 精确模式，先测 2×。")
    ];
    public static FgGpuProfile? Recommend(string gpu)
    {
        if (gpu.Contains("40HX", StringComparison.OrdinalIgnoreCase)) return All[1];
        if (gpu.Contains("RTX 20", StringComparison.OrdinalIgnoreCase)) return All[2];
        if (gpu.Contains("RTX 30", StringComparison.OrdinalIgnoreCase)) return All[0];
        return null;
    }
}

public sealed record FgGameProfile(string Name, string RelativeExe, string Guidance)
{
    // Directory adapters, not claims of hardware/gameplay certification.
    public static readonly FgGameProfile[] All = [
        new("黑神话：悟空", "b1/Binaries/Win64/b1-Win64-Shipping.exe", "目录已适配；上游有 3080 Ti 游戏反馈，本 Fork 的 3080 / 40HX 尚待测试。进入游戏显示/画质设置启用 DLSS 帧生成；若显示倍率选项，再选择与上限一致的倍率。"),
        new("明末：渊虚之羽", "Project_Plague/Binaries/Win64/Project_Plague-Win64-Shipping.exe", "目录适配预览；3080 / 40HX 及具体游戏版本尚待实测。进入游戏图形设置选择 DLSS 并尝试开启帧生成；没有倍率菜单时，不能用上限设置强制得到 4×。")
    ];
    public static FgGameProfile? Find(string root) => All.FirstOrDefault(p => File.Exists(Path.Combine(root, p.RelativeExe.Replace('/', Path.DirectorySeparatorChar))));
    public static FgGameProfile? FindExe(string exe) => All.FirstOrDefault(p => string.Equals(Path.GetFileName(exe), Path.GetFileName(p.RelativeExe), StringComparison.OrdinalIgnoreCase));
    public static string Ini(string router, int multiplier, bool approximate)
    {
        if (router != "SM75" && router != "SM86") throw new ArgumentException("请选择支持的 GPU 路由。");
        if (multiplier < 2 || multiplier > 4) throw new ArgumentException("支持 2× / 3× / 4× 上限。");
        if (approximate && router != "SM86") throw new ArgumentException("SM75 不支持近似采样。");
        return $"; DLSS Swapper FG Preview - cap only, actual multiplier requested by game\r\n[Compatibility]\r\nRouter={router}\r\nKernelImage=PTX\r\nHardwareBilinear={(approximate ? 1 : 0)}\r\n\r\n[FrameGeneration]\r\nMaxGeneratedFrames={multiplier - 1}\r\n\r\n[Logging]\r\nLevel=2\r\n";
    }
}
