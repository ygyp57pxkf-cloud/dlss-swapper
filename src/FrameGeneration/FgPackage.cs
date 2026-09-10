using System;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace DLSS_Swapper.FrameGeneration;

public sealed record FgAsset(string Name, string RepositoryPath, string Sha256);

public static class FgPackage
{
    public const string Commit = "1fb9ecbd980b1f191c092dce1b072b3cb9bd8984";
    public const string Version = "Native 0.2.3";
    public static readonly FgAsset[] Assets = [
        new("version.dll", "version.dll", "92bb81b9f0fc52711d7147ef6a4bfb8af2c06299f77a6e1e28effb8b887a5503"),
        new("winmm.dll", "altnative/winmm.dll", "2983e0ee0a6fe84e0a9860d9fecf5661ca1ee5db5650e3f3cd4c6cb60ad5b6c7"),
        new("dinput8.dll", "altnative/dinput8.dll", "93ec6fc4e27982b3e202a88ac58ddc48bcd52fb8e64953daaf6d5a530f1928f2"),
        new("winhttp.dll", "altnative/winhttp.dll", "8f2516982246e3baa46d1d3926a94dc9aadfb8a248c17fe9ff811527676525f9"),
        new("dxgi.dll", "altnative/dxgi.dll", "fe69f30bf6a5050267053d6596827a031abb8654a4704b89ec1aa38f5f2fe16b")
    ];
    public static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static void Verify(string file, FgAsset asset)
    {
        if (!File.Exists(file) || !string.Equals(Hash(file), asset.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new IOException("DLL 与此预览版固定的上游文件不匹配。请使用 README 指定提交，不要重命名其他代理或关闭安全软件。");
    }
    public static async Task<string> AcquireAsync(string cache, FgAsset asset, string? localFile, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(localFile))
        {
            Verify(localFile, asset);
            return localFile;
        }
        Directory.CreateDirectory(cache);
        var destination = Path.Combine(cache, asset.Name);
        if (File.Exists(destination))
        {
            try { Verify(destination, asset); return destination; }
            catch (IOException) { /* Download a clean copy into a separate temporary file. */ }
        }
        var temporary = Path.Combine(cache, Guid.NewGuid() + ".download");
        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            using var response = await client.GetAsync($"https://raw.githubusercontent.com/sdli1995/dlssg_for_sm86/{Commit}/{asset.RepositoryPath}", HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength > 256L * 1024 * 1024) throw new IOException("下载文件过大。");
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(temporary))
            {
                byte[] buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > 256L * 1024 * 1024) throw new IOException("下载文件过大。");
                    await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }
            Verify(temporary, asset);
            File.Move(temporary, destination, true);
            return destination;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
