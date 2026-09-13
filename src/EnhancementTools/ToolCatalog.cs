using System;

namespace DLSS_Swapper.EnhancementTools;

public sealed record EnhancementTool(string Id, string Name, string Version, string FileName,
    Uri DownloadUrl, string Sha256, long DownloadBytes, bool IsZip, string LauncherName,
    Uri ReleaseUrl, string Description, string Guide, string[]? RequiredFiles = null);

public static class ToolCatalog
{
    public static readonly EnhancementTool Games = new(
        "dlss5-swapper", "DLSS 5 游戏增强", "2.2.7", "DLSS5-Swapper-2.2.7-portable.exe",
        new("https://github.com/rakanki911/DLSS5-Swapper/releases/download/v2.2.7/DLSS5-Swapper-2.2.7-portable.exe"),
        "b2385736d88e9b301b088c0ce8cc1add29851cba611474b9f02d9bf6e43d63d0", 232893049, false,
        "DLSS5-Swapper-2.2.7-portable.exe", new("https://github.com/rakanki911/DLSS5-Swapper/releases/tag/v2.2.7"),
        "准备并打开 DLSS5-Swapper。它在自己的界面中安装 ReShade / Feeder，支持兼容的 2D、2.5D、3D 游戏及模拟器。",
        "1. 先退出游戏，保留能正常运行的副本。\n2. 在外部管理器中 Add a game，选实际游戏 EXE。没有原生 DLSS 的游戏先选 ReShade / Feeder；核对接口与 32/64 位后再 Install。\n3. Home 打开 ReShade；部分 64 位 DX11/DX12 游戏可用 F8 面板。先用低强度、单 Pass。\n4. 同时检查 Feeder 送帧和成功 NR 帧数；安装完成不代表效果生效。\n5. 恢复游戏请用 DLSS5-Swapper 的 Restore originals；卸载管理器不会自动恢复游戏。\n\n老滚五 SE/AE：64 位 DX11。原版/传奇版：32 位 DX9，需要 dgVoodoo 和 host64。战地3：32 位 DX11；仅测试允许 Mod 的单人环境。孤胆枪手：尚无可靠实测，先确认实际接口。\n\nNR 会增加 GPU 负载。616.64 等驱动与部分 RenoDX 版本有组合故障；保留 40HX 已稳定的驱动，不据此自动更换驱动。" );

    public static readonly EnhancementTool Media = new(
        "visual-enhancer", "图片 / 视频增强", "8.0", "DLSS.5.Visual.Enhancer.v8.0.zip",
        new("https://github.com/Merserk/dlss5-visual-enhancer/releases/download/v8.0/DLSS.5.Visual.Enhancer.v8.0.zip"),
        "5a06420e772a9edd6a0d5039841871d5b95232ab95b53451b9a8313f6daa2c53", 498439132, true,
        "start.bat", new("https://github.com/Merserk/dlss5-visual-enhancer/releases/tag/v8.0"),
        "准备并打开 DLSS 5 Visual Enhancer。通过它的本地网页处理图片、导出视频或使用带缓冲的 Live 播放。",
        "1. 打开工具后，在终端提示的本地网页中操作。需要 Windows 11 / Direct3D 12。\n2. Neural Rendering → Image / Video：选文件，先用单 Pass、低强度和预览，再保存到独立输出目录。Detail-Only 可尽量保留原色调。\n3. Live：选 Local video，建议先用 720p、原帧率、单 Pass，Open in MPV → Start Live。Live 有缓冲，并非低延迟游戏滤镜。\n4. Upscale 用于放大/清晰度增强；Neural Rendering 会改变细节、材质和光照，两者分开选择。\n5. 建议先用 RTX 3080，编码先选 H.264/H.265 (NVIDIA NVENC)。v8.0 的视频插帧标注支持 RTX 40/50。\n\nv8.0 根据设备名是否包含 RTX 标记 AI 兼容性。若驱动返回 CMP 40HX，可能先被标记不兼容；双解锁/NVENC 成功不代表 NR 已验证。\n\n输出和配置保存在外部工具目录中。搬迁 Swapper 时保留整个 StoredData-FG-Preview；不要删除工具目录来清缓存。", ["app.py", "bin/python-3.13.15-embed-amd64/python.exe"]);

    public static readonly EnhancementTool[] All = [Games, Media];

}
