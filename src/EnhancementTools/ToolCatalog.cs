using System;

namespace DLSS_Swapper.EnhancementTools;

public sealed record EnhancementTool(string Id, string Name, string Version, string FileName,
    Uri DownloadUrl, string Sha256, long DownloadBytes, bool IsZip, string LauncherName,
    Uri ReleaseUrl, string Description, string Guide, string[]? RequiredFiles = null);

public static class ToolCatalog
{
    public static readonly EnhancementTool Games = new(
        "dlss5-swapper", "DLSS 5 游戏增强", "2.2.9", "DLSS5-Swapper-2.2.9-portable.exe",
        new("https://github.com/rakanki911/DLSS5-Swapper/releases/download/v2.2.9/DLSS5-Swapper-2.2.9-portable.exe"),
        "6b064ecba6e487a87302c1d75c3fd2d8189e78fdf3c91e705eee3d4539ae23c8", 258133136, false,
        "DLSS5-Swapper-2.2.9-portable.exe", new("https://github.com/rakanki911/DLSS5-Swapper/releases/tag/v2.2.9"),
        "准备并打开 DLSS5-Swapper。它在自己的界面中安装 ReShade / Feeder，支持兼容的 2D、2.5D、3D 游戏及模拟器。",
        "1. 先退出游戏，保留能正常运行的副本。\n2. 在外部管理器中 Add a game，选实际游戏 EXE。没有原生 DLSS 的游戏先选 ReShade / Feeder；核对接口与 32/64 位后再 Install。\n3. Home 打开 ReShade；部分 64 位 DX11/DX12 游戏可用 F8 面板。先用低强度、单 Pass。\n4. 同时检查 Feeder 送帧和成功 NR 帧数；安装完成不代表效果生效。\n5. 恢复游戏请用 DLSS5-Swapper 的 Restore originals；卸载管理器不会自动恢复游戏。\n\n老滚五 SE/AE：64 位 DX11。原版/传奇版：32 位 DX9，需要 dgVoodoo 和 host64。战地3：32 位 DX11；仅测试允许 Mod 的单人环境。孤胆枪手：尚无可靠实测，先确认实际接口。\n\nNR 会增加 GPU 负载。616.64 等驱动与部分 RenoDX 版本有组合故障；保留 40HX 已稳定的驱动，不据此自动更换驱动。" );

    public static readonly EnhancementTool Media = new(
        "visual-enhancer", "图片 / 视频增强", "13.2", "Visual.Enhancer.v13.2.zip",
        new("https://github.com/Merserk/dlss5-visual-enhancer/releases/download/v13.2/Visual.Enhancer.v13.2.zip"),
        "656850c8ab2f529415c271c58d1a23eaf9f971f860ea58290715db4a6b27117b", 724867853, true,
        "Visual Enhancer.exe", new("https://github.com/Merserk/dlss5-visual-enhancer/releases/tag/v13.2"),
        "准备并打开 Visual Enhancer 13.2 的桌面应用。图片、视频与 Live 功能在外部工具中操作。",
        "1. 准备完成后点击打开工具，进入 Visual Enhancer 桌面界面；v13.2 不再使用旧版 start.bat 和本地网页流程。\n2. 先用一张图片或短视频、低强度参数预览，保存到独立输出目录；不要覆盖源文件。\n3. 如使用 Live，先用低分辨率和短片段测试延迟、画质与稳定性。\n4. Neural Rendering 与 Upscale 分开比较；视频编码先试 H.264/H.265 (NVIDIA NVENC)。\n\n建议先在 RTX 3080 验证。40HX 的 AI 兼容性与界面检测仍待真机核实，NVENC 可用不等于 NR 已验证。输出与配置保存在外部工具目录，搬迁 Swapper 时保留整个 StoredData-FG-Preview。", ["app.py", "bin/python-3.14.7-embed-amd64/python.exe"]);

    public static readonly EnhancementTool[] All = [Games, Media];

}
