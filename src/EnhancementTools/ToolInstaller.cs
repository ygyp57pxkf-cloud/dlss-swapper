using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace DLSS_Swapper.EnhancementTools;

public sealed record ToolProgress(string Stage, long Bytes = 0, long Total = 0);
public sealed record PreparedTool(string Directory, string Launcher);
public sealed record ToolReceipt(string Id, string Version, string PackageHash, string Launcher, string LauncherHash);

/// <summary>Prepares external tools in their own version directories; never installs into a game.</summary>
public static class ToolInstaller
{
    const string ReceiptName = "tool-install.json";
    const long MaxExpandedBytes = 4L * 1024 * 1024 * 1024;

    static void Segment(string value)
    {
        if (string.IsNullOrEmpty(value) || value is "." or ".." || value.Any(c => !(char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')))
            throw new IOException("工具目录标识无效。");
    }

    static void PlainPath(string path)
    {
        for (var current = Path.GetFullPath(path); !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("工具目录包含符号链接或重解析点，请使用真实目录。");
    }

    public static string ToolDirectory(string root, EnhancementTool tool)
    {
        Segment(tool.Id); Segment(tool.Version); Segment(tool.FileName); Segment(tool.LauncherName);
        var path = Path.Combine(Path.GetFullPath(root), tool.Id, tool.Version);
        PlainPath(path);
        return path;
    }

    static string Child(string root, string relative)
    {
        var parts = relative.Replace('\\', '/').Split('/');
        if (parts.Any(p => p.Length == 0 || p is "." or ".." || p.IndexOfAny([':', '<', '>', '"', '|', '?', '*']) >= 0 || p.EndsWith(' ') || p.EndsWith('.')))
            throw new IOException("工具包包含无效或越界路径。");
        var target = Path.GetFullPath(Path.Combine(root, Path.Combine(parts)));
        if (!target.StartsWith(Path.GetFullPath(root) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new IOException("工具包路径超出准备目录。");
        PlainPath(target);
        return target;
    }

    public static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    public static PreparedTool? GetPrepared(string root, EnhancementTool tool)
    {
        var directory = ToolDirectory(root, tool);
        var receiptPath = Path.Combine(directory, ReceiptName);
        PlainPath(receiptPath);
        if (!File.Exists(receiptPath)) return null;
        var receipt = JsonSerializer.Deserialize<ToolReceipt>(File.ReadAllText(receiptPath));
        if (receipt is null || receipt.Id != tool.Id || receipt.Version != tool.Version || receipt.PackageHash != tool.Sha256)
            throw new IOException("工具准备记录不匹配，请保留原目录与输出文件后检查。");
        var launcher = Child(directory, receipt.Launcher);
        if (Path.GetFileName(launcher) != tool.LauncherName || !File.Exists(launcher) || Hash(launcher) != receipt.LauncherHash)
            throw new IOException("工具启动文件缺失或已修改。请先备份整个工具目录；不会覆盖其中的输出和配置。");
        VerifyRequiredFiles(Path.GetDirectoryName(launcher)!, tool);
        return new(directory, launcher);
    }

    static void VerifyRequiredFiles(string directory, EnhancementTool tool)
    {
        foreach (var relative in tool.RequiredFiles ?? [])
            if (!File.Exists(Child(directory, relative))) throw new IOException("工具运行文件缺失：" + relative + "。请保留输出和配置后重新准备。");
    }

    static async Task VerifyAsync(string file, EnhancementTool tool, CancellationToken token)
    {
        if (new FileInfo(file).Length != tool.DownloadBytes) throw new IOException("安装包大小与所选版本不匹配。");
        await using var stream = File.OpenRead(file);
        var hash = Convert.ToHexString(await SHA256.HashDataAsync(stream, token)).ToLowerInvariant();
        if (!string.Equals(hash, tool.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("安装包校验失败，请从此版本的作者发布页重新获取。");
    }

    static async Task DownloadAsync(string destination, EnhancementTool tool, HttpClient client, IProgress<ToolProgress>? progress, CancellationToken token)
    {
        using var response = await client.GetAsync(tool.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength is long length && length != tool.DownloadBytes)
            throw new IOException("下载响应大小异常，请使用本地导入或作者发布页。");
        await using var source = await response.Content.ReadAsStreamAsync(token);
        await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true);
        var watch = Stopwatch.StartNew();
        byte[] buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, token)) > 0)
        {
            total += read;
            if (total > tool.DownloadBytes) throw new IOException("下载数据超出所选安装包大小。");
            await output.WriteAsync(buffer.AsMemory(0, read), token);
            if (watch.ElapsedMilliseconds >= 200)
            {
                progress?.Report(new("正在下载", total, tool.DownloadBytes));
                watch.Restart();
            }
        }
        progress?.Report(new("下载结束，正在校验", total, tool.DownloadBytes));
    }

    static void Extract(string archive, string stage, CancellationToken token)
    {
        using var zip = ZipFile.OpenRead(archive);
        if (zip.Entries.Count > 100000) throw new IOException("工具包文件数异常。");
        long total = 0;
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in zip.Entries)
        {
            token.ThrowIfCancellationRequested();
            total = checked(total + entry.Length);
            if (total > MaxExpandedBytes) throw new IOException("工具包解压大小超出限制。");
            if (((entry.ExternalAttributes >> 16) & 0xf000) == 0xa000 || (entry.ExternalAttributes & (int)FileAttributes.ReparsePoint) != 0)
                throw new IOException("工具包包含链接文件。");
            var directoryEntry = entry.FullName.EndsWith('/') || entry.FullName.EndsWith('\\');
            var relative = directoryEntry ? entry.FullName.TrimEnd('/', '\\') : entry.FullName;
            var destination = Child(stage, relative);
            if (!paths.Add(destination)) throw new IOException("工具包包含重复路径。");
            if (directoryEntry) { Directory.CreateDirectory(destination); continue; }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            using var source = entry.Open();
            using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write);
            byte[] buffer = new byte[81920];
            long written = 0;
            int read;
            while ((read = source.Read(buffer)) > 0)
            {
                token.ThrowIfCancellationRequested();
                written += read;
                if (written > entry.Length) throw new IOException("工具包文件长度异常。");
                output.Write(buffer, 0, read);
            }
            if (written != entry.Length) throw new IOException("工具包文件不完整。");
        }
    }

    public static async Task<PreparedTool> PrepareAsync(string root, EnhancementTool tool, string? localPackage,
        HttpClient client, IProgress<ToolProgress>? progress, CancellationToken token)
    {
        // Called on a worker task: hashing and archive extraction never run on the UI thread.
        var directory = ToolDirectory(root, tool);
        Directory.CreateDirectory(Path.GetDirectoryName(directory)!);
        var lockPath = directory + ".prepare.lock";
        PlainPath(lockPath);
        using var operationLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var existing = GetPrepared(root, tool);
        if (existing is not null) return existing;
        if (Directory.Exists(directory)) throw new IOException("已有同版本目录但准备记录不完整。请先将它移到备份位置，保留输出和配置，再重试。");
        var cache = Path.Combine(root, "_downloads");
        PlainPath(cache);
        Directory.CreateDirectory(cache);
        var package = Path.Combine(cache, tool.FileName);
        PlainPath(package);
        var temporary = Path.Combine(cache, Guid.NewGuid() + ".part");
        var stage = directory + ".staging-" + Guid.NewGuid();
        try
        {
            if (!string.IsNullOrWhiteSpace(localPackage))
            {
                progress?.Report(new("正在校验本地安装包"));
                await VerifyAsync(localPackage, tool, token);
                package = Path.GetFullPath(localPackage);
            }
            else
            {
                if (File.Exists(package))
                {
                    progress?.Report(new("正在校验已下载的安装包"));
                    try { await VerifyAsync(package, tool, token); }
                    catch (IOException)
                    {
                        // Only this version's download cache is disposable, never tool outputs.
                        File.Delete(package);
                    }
                }
                if (!File.Exists(package))
                {
                    await DownloadAsync(temporary, tool, client, progress, token);
                    await VerifyAsync(temporary, tool, token);
                    File.Move(temporary, package);
                }
            }
            token.ThrowIfCancellationRequested();
            progress?.Report(new(tool.IsZip ? "正在解压，保留原工具目录" : "正在准备便携程序"));
            Directory.CreateDirectory(stage);
            if (tool.IsZip) Extract(package, stage, token);
            else File.Copy(package, Path.Combine(stage, tool.LauncherName));
            var launchers = Directory.EnumerateFiles(stage, tool.LauncherName, SearchOption.AllDirectories).ToArray();
            if (launchers.Length != 1) throw new IOException("工具包内没有唯一的预期启动文件。");
            VerifyRequiredFiles(Path.GetDirectoryName(launchers[0])!, tool);
            var relative = Path.GetRelativePath(stage, launchers[0]);
            var receipt = new ToolReceipt(tool.Id, tool.Version, tool.Sha256, relative, Hash(launchers[0]));
            File.WriteAllText(Path.Combine(stage, ReceiptName), JsonSerializer.Serialize(receipt));
            token.ThrowIfCancellationRequested();
            Directory.Move(stage, directory);
            progress?.Report(new("工具已准备，可点击打开"));
            return new(directory, Child(directory, relative));
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
            if (Directory.Exists(stage)) Directory.Delete(stage, true);
        }
    }

    public static ProcessStartInfo LaunchInfo(PreparedTool prepared) => new(prepared.Launcher)
    {
        UseShellExecute = true,
        WorkingDirectory = Path.GetDirectoryName(prepared.Launcher)!,
    };
}
