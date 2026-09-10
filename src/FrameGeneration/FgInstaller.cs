using System;
using System.IO;
using System.Linq;
using System.Reflection.PortableExecutable;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;

namespace DLSS_Swapper.FrameGeneration;

public sealed class FgInstallState
{
    public string Proxy { get; set; } = "";
    public string DllHash { get; set; } = "";
    public string IniHash { get; set; } = "";
    public string PreviousIniHash { get; set; } = "";
    public string? OriginalIniHash { get; set; }
    public string Version { get; set; } = FgPackage.Version;
    public string ExeName { get; set; } = "";
}

public static class FgInstaller
{
    public const string StateDirectory = ".dlss-swapper-fg";
    public const string IniName = "dlssg_sm86.ini";
    static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    static void PlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("目标包含符号链接/重解析点，请选择游戏的真实目录。");
    }
    public static string ValidateExe(string root, string exe)
    {
        var rootPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        exe = Path.GetFullPath(exe.Trim().Trim('"'));
        if (!exe.StartsWith(rootPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) || !File.Exists(exe) || !string.Equals(Path.GetExtension(exe), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new IOException("请选择此游戏安装目录内实际渲染的 EXE。");
        PlainPath(exe);
        using var stream = File.OpenRead(exe);
        using var pe = new PEReader(stream);
        if (pe.PEHeaders.CoffHeader.Machine != Machine.Amd64) throw new IOException("帧生成后端仅支持 Windows x64 游戏。");
        return Path.GetDirectoryName(exe)!;
    }
    static string StatePath(string directory) => Path.Combine(directory, StateDirectory, "state.json");
    public static FgInstallState? ReadState(string directory)
    {
        var file = StatePath(directory);
        PlainPath(file);
        if (!File.Exists(file)) return null;
        var state = JsonSerializer.Deserialize<FgInstallState>(File.ReadAllText(file)) ?? throw new IOException("安装记录为空，请按手动恢复指引处理。");
        if (!FgPackage.Assets.Any(a => a.Name == state.Proxy) || string.IsNullOrWhiteSpace(state.DllHash) || string.IsNullOrWhiteSpace(state.IniHash))
            throw new IOException("安装记录无效，请保留备份并手动恢复。");
        return state;
    }
    static void WriteState(string directory, FgInstallState state) => AtomicWrite(StatePath(directory), Encoding.UTF8.GetBytes(JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true })));
    static void AtomicWrite(string file, byte[] bytes)
    {
        PlainPath(file);
        var temp = file + ".fg-stage";
        if (File.Exists(temp)) throw new IOException("发现上次中断留下的 .fg-stage 文件，请先按手动恢复指引检查。");
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes);
                stream.Flush(true);
            }
            File.Move(temp, file, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    static bool IsExpected(string file, params string?[] hashes) => !File.Exists(file) || hashes.Contains(FgPackage.Hash(file), StringComparer.OrdinalIgnoreCase);
    static void EnsureOwned(string directory, FgInstallState state)
    {
        var dll = Path.Combine(directory, state.Proxy);
        var ini = Path.Combine(directory, IniName);
        PlainPath(dll); PlainPath(ini);
        if (!IsExpected(dll, state.DllHash) || !IsExpected(ini, state.IniHash, state.PreviousIniHash, state.OriginalIniHash))
            throw new IOException("安装后的 DLL/INI 被其他程序或手动修改。为保留你的改动，停止自动覆盖；请查看手动恢复指引。");
        if (state.OriginalIniHash is not null)
        {
            var backup = Path.Combine(directory, StateDirectory, "original.ini");
            PlainPath(backup);
            if (!File.Exists(backup) || FgPackage.Hash(backup) != state.OriginalIniHash) throw new IOException("原 INI 备份缺失或变化，停止自动恢复。");
        }
    }
    public static void Install(string directory, string exeName, string source, FgAsset asset, string ini)
    {
        PlainPath(directory);
        FgPackage.Verify(source, asset);
        var backupDirectory = Path.Combine(directory, StateDirectory);
        PlainPath(backupDirectory);
        Directory.CreateDirectory(backupDirectory);
        using var operationLock = new FileStream(Path.Combine(backupDirectory, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var old = ReadState(directory);
        if (old is not null)
        {
            if (old.Proxy != asset.Name) throw new IOException("切换代理入口前，请先点击卸载并恢复，再选新入口安装。");
            EnsureOwned(directory, old);
        }
        else
        {
            foreach (var known in FgPackage.Assets)
            {
                var existing = Path.Combine(directory, known.Name);
                PlainPath(existing);
                if (File.Exists(existing) && FgPackage.Hash(existing) == known.Sha256)
                    throw new IOException("发现此前手动安装的本 Mod，请先备份并移走它，避免同时加载两个代理。");
            }
            if (File.Exists(Path.Combine(directory, asset.Name))) throw new IOException($"{asset.Name} 已被游戏或其他 Mod 占用。原件未改动，请选择另一个实际可加载的入口。");
            if (Directory.EnumerateFiles(backupDirectory).Any(p => Path.GetFileName(p) != "operation.lock"))
                throw new IOException("发现无完整安装记录的旧备份，请先手动检查，防止覆盖原件。");
        }
        var iniPath = Path.Combine(directory, IniName);
        PlainPath(iniPath);
        var originalHash = old?.OriginalIniHash;
        if (old is null && File.Exists(iniPath))
        {
            var original = File.ReadAllBytes(iniPath);
            originalHash = Digest(original);
            File.WriteAllBytes(Path.Combine(backupDirectory, "original.ini"), original);
        }
        byte[] iniBytes = Encoding.UTF8.GetBytes(ini);
        var state = new FgInstallState { Proxy = asset.Name, DllHash = asset.Sha256, IniHash = Digest(iniBytes), PreviousIniHash = old?.IniHash ?? "", OriginalIniHash = originalHash, ExeName = exeName };
        // Journal before touching game files, so interrupted installs can be restored.
        WriteState(directory, state);
        try
        {
            AtomicWrite(Path.Combine(directory, asset.Name), File.ReadAllBytes(source));
            AtomicWrite(iniPath, iniBytes);
            FgPackage.Verify(Path.Combine(directory, asset.Name), asset);
            if (FgPackage.Hash(iniPath) != state.IniHash) throw new IOException("INI 写入校验失败。");
        }
        catch
        {
            // Keep journal and original.ini for explicit recovery; never destroy foreign changes.
            throw new IOException("安装未完成，原件备份与安装记录已保留。请先点击卸载并恢复；若失败，使用手动恢复指引。");
        }
    }
    public static void Restore(string directory)
    {
        PlainPath(directory);
        var state = ReadState(directory) ?? throw new IOException("此目录没有本工具的安装记录；不会删除手动安装的文件。");
        var backupDirectory = Path.Combine(directory, StateDirectory);
        using (var operationLock = new FileStream(Path.Combine(backupDirectory, "operation.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
        {
            state = ReadState(directory) ?? throw new IOException("安装记录已变化。");
            EnsureOwned(directory, state);
            var ini = Path.Combine(directory, IniName);
            if (state.OriginalIniHash is not null)
                AtomicWrite(ini, File.ReadAllBytes(Path.Combine(backupDirectory, "original.ini")));
            else if (File.Exists(ini)) File.Delete(ini);
            var dll = Path.Combine(directory, state.Proxy);
            if (File.Exists(dll)) File.Delete(dll);
            // Keep original.ini until restore fully succeeds.
            File.Delete(StatePath(directory));
            var backup = Path.Combine(backupDirectory, "original.ini");
            if (File.Exists(backup)) File.Delete(backup);
        }
        File.Delete(Path.Combine(backupDirectory, "operation.lock"));
        if (!Directory.EnumerateFileSystemEntries(backupDirectory).Any()) Directory.Delete(backupDirectory);
    }
}
