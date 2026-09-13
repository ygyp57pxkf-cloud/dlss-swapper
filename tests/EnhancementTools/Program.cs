using DLSS_Swapper.EnhancementTools;
using System.IO.Compression;
using System.Net;
using System.Text;

var root = Path.Combine(Environment.CurrentDirectory, ".enhancement-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
async Task Reject(Func<Task> action, string name)
{
    try { await action(); }
    catch (IOException) { Check(true, name); return; }
    catch (BadImageFormatException) { Check(true, name); return; }
    catch (OperationCanceledException) { Check(true, name); return; }
    throw new Exception("Expected rejection: " + name);
}
string Dir(string name) { var d = Path.Combine(root, name); Directory.CreateDirectory(d); return d; }
EnhancementTool Tool(string file, string id = "fixture", bool zip = false) => new(id, id, "1.0", Path.GetFileName(file),
    new Uri("https://example.invalid/tool"), ToolInstaller.Hash(file), new FileInfo(file).Length, zip, zip ? "start.bat" : Path.GetFileName(file),
    new Uri("https://example.invalid/release"), "fixture", "fixture");
string Zip(string name, params (string path, int attrs)[] entries)
{
    var file = Path.Combine(root, name + ".zip");
    using var zip = ZipFile.Open(file, ZipArchiveMode.Create);
    foreach (var item in entries)
    {
        var entry = zip.CreateEntry(item.path); entry.ExternalAttributes = item.attrs;
        using var writer = new StreamWriter(entry.Open()); writer.Write("fixture only");
    }
    return file;
}
string Pe(string name, bool x64, string? import)
{
    var bytes = new byte[1024];
    using var writer = new BinaryWriter(new MemoryStream(bytes));
    void W16(int offset, ushort value) { writer.BaseStream.Position = offset; writer.Write(value); }
    void W32(int offset, uint value) { writer.BaseStream.Position = offset; writer.Write(value); }
    W16(0, 0x5a4d); W32(0x3c, 0x80); W32(0x80, 0x4550);
    W16(0x84, x64 ? (ushort)0x8664 : (ushort)0x14c); W16(0x86, 1);
    int opt = 0x98, optSize = x64 ? 240 : 224;
    W16(0x94, (ushort)optSize); W16(0x96, 0x102); W16(opt, x64 ? (ushort)0x20b : (ushort)0x10b);
    W32(opt + 32, 0x1000); W32(opt + 36, 0x200); W32(opt + 56, 0x2000); W32(opt + 60, 0x200);
    int directories = opt + (x64 ? 112 : 96); W32(directories - 4, 16);
    if (import is not null) { W32(directories + 8, 0x1000); W32(directories + 12, 40); }
    int section = opt + optSize;
    W32(section + 8, 0x200); W32(section + 12, 0x1000); W32(section + 16, 0x200); W32(section + 20, 0x200);
    if (import is not null)
    {
        W32(0x200 + 12, 0x1060); W32(0x200 + 16, 0x1080);
        Encoding.ASCII.GetBytes(import + "\0").CopyTo(bytes, 0x260);
    }
    var file = Path.Combine(Dir(name), name + ".exe"); File.WriteAllBytes(file, bytes); return file;
}

try
{
    var executable = Path.Combine(root, "portable.exe"); File.WriteAllBytes(executable, [1, 2, 3, 4]);
    var tool = Tool(executable);
    using var client = new HttpClient(new PayloadHandler(File.ReadAllBytes(executable)));
    var importedRoot = Dir("path with spaces");
    var prepared = await ToolInstaller.PrepareAsync(importedRoot, tool, executable, client, null, default);
    Check(ToolInstaller.GetPrepared(importedRoot, tool) == prepared, "local import persists verified ready state");
    File.WriteAllText(Path.Combine(prepared.Directory, "user-output.mp4"), "preserve me");
    await ToolInstaller.PrepareAsync(importedRoot, tool, null, client, null, default);
    Check(File.ReadAllText(Path.Combine(prepared.Directory, "user-output.mp4")) == "preserve me", "repeated preparation preserves outputs");
    var launch = ToolInstaller.LaunchInfo(prepared);
    Check(launch.FileName == prepared.Launcher && launch.WorkingDirectory == prepared.Directory && launch.UseShellExecute && launch.Arguments == "" && launch.Verb == "", "launch handles spaces without injected shell arguments or elevation");
    File.AppendAllText(prepared.Launcher, "changed");
    await Reject(() => Task.Run(() => ToolInstaller.GetPrepared(importedRoot, tool)), "modified launcher cannot launch as ready");
    var wrong = Path.Combine(root, "wrong.exe"); File.WriteAllBytes(wrong, [4, 3, 2, 1]);
    await Reject(() => ToolInstaller.PrepareAsync(Dir("bad-hash"), tool, wrong, client, null, default), "bad hash rejected");
    var shortFile = Path.Combine(root, "short.exe"); File.WriteAllBytes(shortFile, [1]);
    await Reject(() => ToolInstaller.PrepareAsync(Dir("bad-size"), tool, shortFile, client, null, default), "bad size rejected");
    var partialRoot = Dir("partial"); var partial = ToolInstaller.ToolDirectory(partialRoot, tool); Directory.CreateDirectory(partial);
    File.WriteAllText(Path.Combine(partial, "output.png"), "original");
    await Reject(() => ToolInstaller.PrepareAsync(partialRoot, tool, executable, client, null, default), "incomplete existing tool directory is not overwritten");
    Check(File.ReadAllText(Path.Combine(partial, "output.png")) == "original", "incomplete tool output remains intact");
    var netRoot = Dir("download");
    var downloaded = await ToolInstaller.PrepareAsync(netRoot, tool, null, client, null, default);
    Check(ToolInstaller.Hash(downloaded.Launcher) == tool.Sha256, "network preparation verifies downloaded bytes");
    var corruptRoot = Dir("bad-cache"); Directory.CreateDirectory(Path.Combine(corruptRoot, "_downloads"));
    File.WriteAllText(Path.Combine(corruptRoot, "_downloads", tool.FileName), "bad cache");
    await ToolInstaller.PrepareAsync(corruptRoot, tool, null, client, null, default);
    Check(ToolInstaller.GetPrepared(corruptRoot, tool) is not null, "corrupt download cache can be retried");
    using var wrongClient = new HttpClient(new PayloadHandler([5, 6, 7, 8]));
    var failedRoot = Dir("failed-network");
    await Reject(() => ToolInstaller.PrepareAsync(failedRoot, tool, null, wrongClient, null, default), "network hash mismatch rejected");
    Check(!Directory.EnumerateFiles(failedRoot, "*.part", SearchOption.AllDirectories).Any(), "failed download removes partial file");
    var cancelRoot = Dir("cancel-download"); using var cancellation = new CancellationTokenSource();
    await Reject(() => ToolInstaller.PrepareAsync(cancelRoot, tool, null, client,
        new InlineProgress(p => { if (p.Bytes > 0) cancellation.Cancel(); }), cancellation.Token), "cancellation during download verification is honored");
    Check(!Directory.EnumerateFiles(cancelRoot, "*.part", SearchOption.AllDirectories).Any(), "cancelled download cleans owned partial");
    var validZip = Zip("valid", ("start.bat", 0), ("app.py", 0), ("bin/python.exe", 0));
    var zipTool = Tool(validZip, "zip-tool", true) with { RequiredFiles = ["app.py", "bin/python.exe"] };
    var zipRoot = Dir("valid-zip");
    var unpacked = await ToolInstaller.PrepareAsync(zipRoot, zipTool, validZip, client, null, default);
    Check(File.Exists(Path.Combine(unpacked.Directory, "bin", "python.exe")), "archive extracts runtime beside root launcher");
    File.Delete(Path.Combine(unpacked.Directory, "app.py"));
    await Reject(() => Task.Run(() => ToolInstaller.GetPrepared(zipRoot, zipTool)), "missing required runtime prevents ready state");
    var cancelZipRoot = Dir("cancel-zip"); using var zipCancellation = new CancellationTokenSource();
    await Reject(() => ToolInstaller.PrepareAsync(cancelZipRoot, zipTool, validZip, client,
        new InlineProgress(p => { if (p.Stage.StartsWith("正在解压")) zipCancellation.Cancel(); }), zipCancellation.Token), "cancelled extraction is not committed");
    Check(!Directory.EnumerateDirectories(cancelZipRoot, "*.staging-*", SearchOption.AllDirectories).Any(), "cancelled extraction cleans staging");
    foreach (var bad in new[] {
        Zip("traversal", ("../escape.txt", 0), ("start.bat", 0)),
        Zip("absolute", ("/escape.txt", 0), ("start.bat", 0)),
        Zip("windows-traversal", ("..\\escape.txt", 0), ("start.bat", 0)),
        Zip("stream", ("start.bat:stream", 0)),
        Zip("link", ("start.bat", unchecked((int)0xa1ff0000))),
        Zip("duplicate", ("start.bat", 0), ("START.BAT", 0)),
        Zip("missing-launcher", ("app.py", 0)),
        Zip("multiple-launchers", ("a/start.bat", 0), ("b/start.bat", 0)) })
    {
        var destination = Dir("reject-" + Path.GetFileNameWithoutExtension(bad));
        await Reject(() => ToolInstaller.PrepareAsync(destination, Tool(bad, "bad-zip", true), bad, client, null, default), Path.GetFileNameWithoutExtension(bad) + " archive rejected");
        Check(!Directory.EnumerateDirectories(destination, "*.staging-*", SearchOption.AllDirectories).Any(), "rejected archive cleans staging");
    }
    Check(!File.Exists(Path.Combine(root, "escape.txt")), "archive traversal cannot create outside file");
    var x86 = Pe("legacy", false, "D3D9.dll");
    File.WriteAllText(Path.Combine(Path.GetDirectoryName(x86)!, "version.dll"), "other mod");
    var before = ToolInstaller.Hash(x86); var inspected = GameTargetInspector.Inspect(x86);
    Check(inspected.Architecture == "32 位 x86" && inspected.GraphicsImports.SequenceEqual(["d3d9.dll"]), "legacy x86 DX9 imports detected");
    Check(inspected.ExistingMods.Contains("version.dll") && ToolInstaller.Hash(x86) == before, "read-only inspection reports existing mod");
    var x64 = GameTargetInspector.Inspect(Pe("modern", true, "d3d11.dll"));
    Check(x64.Architecture == "64 位 x64" && x64.GraphicsImports.Contains("d3d11.dll"), "x64 DX11 imports detected");
    Check(GameTargetInspector.Inspect(Pe("dynamic", true, null)).GraphicsImports.Length == 0, "dynamic or absent imports stay unknown");
    await Reject(() => Task.Run(() => GameTargetInspector.Inspect(executable)), "invalid EXE is rejected");
    var packageArg = Array.IndexOf(args, "--packages");
    if (packageArg >= 0)
    {
        var packages = args[packageArg + 1];
        foreach (var real in ToolCatalog.All)
        {
            var actualRoot = Dir("real-" + real.Id);
            var result = await ToolInstaller.PrepareAsync(actualRoot, real, Path.Combine(packages, real.FileName), client, null, default);
            Check(ToolInstaller.GetPrepared(actualRoot, real) == result, real.Name + " actual pinned package prepares without execution");
            Check(new FileInfo(result.Launcher).Length > 0, real.Name + " actual launcher present");
        }
    }
    Console.WriteLine($"{count} checks passed; no third-party executable or game was run.");
}
finally { Directory.Delete(root, true); }

sealed class PayloadHandler(byte[] bytes) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
}
sealed class InlineProgress(Action<ToolProgress> action) : IProgress<ToolProgress>
{
    public void Report(ToolProgress value) => action(value);
}
