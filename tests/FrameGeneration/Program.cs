using DLSS_Swapper.FrameGeneration;
using System.Security.Cryptography;

var root = Path.Combine(Environment.CurrentDirectory, ".fg-tests-" + Guid.NewGuid());
Directory.CreateDirectory(root);
int count = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception(name); Console.WriteLine("PASS " + name); count++; }
void Throws(Action action, string name) { try { action(); } catch (Exception) { Check(true, name); return; } throw new Exception("Expected rejection: " + name); }
string Dir(string name) { var d = Path.Combine(root, name); Directory.CreateDirectory(d); return d; }
try
{
    var source = Path.Combine(root, "fixture.dll");
    File.WriteAllBytes(source, new byte[] { 1, 2, 3, 4 });
    var asset = new FgAsset("version.dll", "fixture", FgPackage.Hash(source));
    var ini2 = FgGameProfile.Ini("SM86", 2, false);
    var ini4 = FgGameProfile.Ini("SM75", 4, false);
    Check(ini2.Contains("MaxGeneratedFrames=1") && ini4.Contains("MaxGeneratedFrames=3"), "2x and 4x caps map to generated counts");
    Check(FgGameProfile.Ini("SM75", 3, false).Contains("MaxGeneratedFrames=2"), "3x is two generated frames");
    Throws(() => FgGameProfile.Ini("SM86", 5, false), "reject unsupported multiplier");
    Throws(() => FgGameProfile.Ini("SM75", 2, true), "reject SM75 approximate mode");
    Check(FgGpuProfile.Recommend("NVIDIA CMP 40HX")!.Router == "SM75", "CMP profile is SM75 experiment");
    Check(FgGpuProfile.Recommend("NVIDIA GeForce RTX 3080")!.Router == "SM86", "3080 uses SM86");
    Check(FgGpuProfile.Recommend("NVIDIA RTX 4090") is null, "unknown route not silently assumed");
    var game = Dir("install");
    File.WriteAllText(Path.Combine(game, FgInstaller.IniName), "original user config");
    FgInstaller.Install(game, "game.exe", source, asset, ini2);
    Check(File.ReadAllText(Path.Combine(game, FgInstaller.StateDirectory, "original.ini")) == "original user config", "original INI preserved");
    FgInstaller.Install(game, "game.exe", source, asset, ini4);
    Check(File.ReadAllText(Path.Combine(game, FgInstaller.IniName)) == ini4, "reapply changes cap and GPU route");
    FgInstaller.Restore(game);
    Check(!File.Exists(Path.Combine(game, "version.dll")) && File.ReadAllText(Path.Combine(game, FgInstaller.IniName)) == "original user config", "restore original after repeated apply");
    var clean = Dir("clean");
    FgInstaller.Install(clean, "game.exe", source, asset, ini2);
    FgInstaller.Restore(clean);
    Check(!File.Exists(Path.Combine(clean, FgInstaller.IniName)), "uninstall removes newly created INI");
    var conflict = Dir("conflict");
    File.WriteAllText(Path.Combine(conflict, "version.dll"), "other mod");
    Throws(() => FgInstaller.Install(conflict, "game.exe", source, asset, ini2), "foreign proxy collision rejected");
    Check(File.ReadAllText(Path.Combine(conflict, "version.dll")) == "other mod", "foreign proxy intact");
    var tamper = Dir("tamper");
    FgInstaller.Install(tamper, "game.exe", source, asset, ini2);
    File.WriteAllText(Path.Combine(tamper, FgInstaller.IniName), "manual edit");
    Throws(() => FgInstaller.Restore(tamper), "manual edits preserved on uninstall");
    Throws(() => FgInstaller.Install(tamper, "game.exe", source, asset, ini4), "manual edits preserved on reapply");
    var bad = asset with { Sha256 = new string('0', 64) };
    Throws(() => FgInstaller.Install(Dir("bad"), "game.exe", source, bad, ini2), "reject untrusted package before game writes");
    var interrupted = Dir("interrupted");
    FgInstaller.Install(interrupted, "game.exe", source, asset, ini2);
    File.Delete(Path.Combine(interrupted, "version.dll"));
    FgInstaller.Restore(interrupted);
    Check(!File.Exists(Path.Combine(interrupted, FgInstaller.IniName)), "recovery tolerates missing installed DLL");
    var switched = Dir("switch");
    FgInstaller.Install(switched, "game.exe", source, asset, ini2);
    Throws(() => FgInstaller.Install(switched, "game.exe", source, asset with { Name = "winmm.dll" }, ini2), "requires uninstall before switching proxy");
    var backup = Dir("backup");
    File.WriteAllText(Path.Combine(backup, FgInstaller.IniName), "original");
    FgInstaller.Install(backup, "game.exe", source, asset, ini2);
    File.WriteAllText(Path.Combine(backup, FgInstaller.StateDirectory, "original.ini"), "changed");
    Throws(() => FgInstaller.Restore(backup), "damaged original backup blocks destructive restore");
    Throws(() => FgInstaller.ValidateExe(root, Path.Combine(root, "..", "outside.exe")), "reject EXE outside game root");
    Throws(() => FgInstaller.ValidateExe(root, source), "reject non EXE rendering target");
    var wukong = Dir("wukong");
    var expected = Path.Combine(wukong, FgGameProfile.All[0].RelativeExe);
    Directory.CreateDirectory(Path.GetDirectoryName(expected)!); File.WriteAllBytes(expected, []);
    Check(FgGameProfile.Find(wukong)?.Name == "黑神话：悟空", "Wukong directory adapter");
    Check(FgGameProfile.Find(Dir("unknown")) is null, "unknown game remains experimental");
    var wuchang = Dir("wuchang");
    var wuchangExe = Path.Combine(wuchang, FgGameProfile.All[1].RelativeExe);
    Directory.CreateDirectory(Path.GetDirectoryName(wuchangExe)!); File.WriteAllBytes(wuchangExe, []);
    Check(FgGameProfile.Find(wuchang)?.Name == "明末：渊虚之羽", "Wuchang directory adapter");
    var preferences = Path.Combine(root, "prefs", "targets.json");
    FgPreferences.SaveExe(preferences, wuchang, wuchangExe);
    Check(FgPreferences.GetExe(preferences, wuchang) == Path.GetFullPath(wuchangExe), "manual target survives reopening");
    Check(FgPreferences.GetExe(preferences, wukong) is null, "game targets remain separate");
    var partial = Dir("partial");
    File.WriteAllText(Path.Combine(partial, FgInstaller.IniName), "before partial install");
    File.WriteAllText(Path.Combine(partial, "version.dll.fg-stage"), "interrupted stage");
    Throws(() => FgInstaller.Install(partial, "game.exe", source, asset, ini2), "partial write keeps recovery journal");
    FgInstaller.Restore(partial);
    Check(File.ReadAllText(Path.Combine(partial, FgInstaller.IniName)) == "before partial install", "partial install restores original");
    var upgrade = Dir("upgrade");
    var newSource = Path.Combine(root, "new-fixture.dll");
    File.WriteAllBytes(newSource, new byte[] { 8, 7, 6, 5 });
    var newAsset = new FgAsset("version.dll", "fixture", FgPackage.Hash(newSource), "Native 0.2.4", FgPackage.CandidateCommit);
    File.WriteAllText(Path.Combine(upgrade, FgInstaller.IniName), "original before version switch");
    FgInstaller.Install(upgrade, "game.exe", source, asset, ini2);
    FgInstaller.Install(upgrade, "game.exe", newSource, newAsset, ini4);
    Check(FgInstaller.ReadState(upgrade)!.Version == "Native 0.2.4" && FgPackage.Hash(Path.Combine(upgrade, "version.dll")) == newAsset.Sha256, "upgrade records selected backend and DLL");
    FgInstaller.Install(upgrade, "game.exe", source, asset, ini2);
    Check(FgInstaller.ReadState(upgrade)!.Version == FgPackage.Version, "downgrade restores prior backend selection");
    FgInstaller.Restore(upgrade);
    Check(File.ReadAllText(Path.Combine(upgrade, FgInstaller.IniName)) == "original before version switch", "original survives upgrade and downgrade");
    var interruptedUpgrade = Dir("interrupted-upgrade");
    FgInstaller.Install(interruptedUpgrade, "game.exe", source, asset, ini2);
    File.WriteAllText(Path.Combine(interruptedUpgrade, "version.dll.fg-stage"), "staged file");
    Throws(() => FgInstaller.Install(interruptedUpgrade, "game.exe", newSource, newAsset, ini4), "interrupted backend upgrade rejected");
    Throws(() => FgInstaller.Install(interruptedUpgrade, "game.exe", newSource, newAsset, ini4), "retrying interrupted upgrade preserves recoverable original DLL hash");
    FgInstaller.Restore(interruptedUpgrade);
    Check(!File.Exists(Path.Combine(interruptedUpgrade, "version.dll")), "recovery accepts old DLL after new version journal was written");
    var logFile = Path.Combine(root, "native_fixture.jsonl");
    File.WriteAllText(logFile, "{\"event\":\"runtime_redirect\"}\n{\"event\":\"feature_created\"}\n{\"event\":\"evaluate\",\"generated_count\":2}\n{\"event\":\"ngx_error\"}\n{unfinished\n");
    var report = FgLogReport.Read(logFile, null);
    Check(report.HasErrors && report.Message.Contains("最多生成 2 帧") && report.Message.Contains("错误事件 1 条"), "log reports generation evidence alongside errors");
    Check(report.Message.Contains("无效 JSON 1 行"), "incomplete live log line is tolerated");
    var stale = FgLogReport.Read(logFile, DateTime.UtcNow.AddMinutes(1));
    Check(stale.HasErrors && stale.Message.Contains("早于本次安装") && !stale.Message.Contains("最多生成"), "old logs never validate a newly installed backend");
    File.WriteAllText(logFile, "{\"event\":\"configuration\"}\n");
    Check(FgLogReport.Read(logFile, null).Message.Contains("未读到有效生成帧数"), "configuration log alone does not imply frame generation");
    // Simulate a CDN that does not return: scheduling artwork must not block local readiness.
    var stalledCover = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var coverStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var refresh = new DLSS_Swapper.Helpers.BackgroundCoverRefresh();
    int coverRuns = 0;
    refresh.Start(async () => { Interlocked.Increment(ref coverRuns); coverStarted.SetResult(); await stalledCover.Task; }, _ => { });
    await coverStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Check(!refresh.Completion.IsCompleted, "unreachable cover remains pending independently of local readiness");
    refresh.Start(() => { Interlocked.Increment(ref coverRuns); return Task.CompletedTask; }, _ => { });
    Check(coverRuns == 1, "repeated scans do not duplicate an active cover refresh");
    stalledCover.SetResult();
    await refresh.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    bool coverFailureReported = false;
    refresh.Start(() => throw new IOException("CDN failure"), _ => coverFailureReported = true);
    await refresh.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    Check(coverFailureReported, "cover failure is observed without failing game processing");
    refresh.Start(() => { Interlocked.Increment(ref coverRuns); return Task.CompletedTask; }, _ => { });
    await refresh.Completion.WaitAsync(TimeSpan.FromSeconds(5));
    Check(coverRuns == 2, "cover refresh can retry after failure");
    var allCoverRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    var fourStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    int activeCovers = 0, peakCovers = 0;
    var manyCovers = Enumerable.Range(0, 8).Select(_ => new DLSS_Swapper.Helpers.BackgroundCoverRefresh()).ToArray();
    foreach (var cover in manyCovers) cover.Start(async () => {
        var active = Interlocked.Increment(ref activeCovers);
        InterlockedExtensionsMax(ref peakCovers, active);
        if (active == 4) fourStarted.TrySetResult();
        await allCoverRelease.Task;
        Interlocked.Decrement(ref activeCovers);
    }, _ => { });
    await fourStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
    Check(peakCovers == 4, "artwork requests are bounded separately from local DLL scans");
    allCoverRelease.SetResult();
    await Task.WhenAll(manyCovers.Select(c => c.Completion)).WaitAsync(TimeSpan.FromSeconds(5));
    Check(activeCovers == 0, "cover queue releases all slots");
    if (args.Contains("--package-smoke"))
    {
        var realAsset = FgPackage.Assets[0];
        var realSource = await FgPackage.AcquireAsync(Dir("download"), realAsset, null, CancellationToken.None);
        Check(FgPackage.Hash(realSource) == realAsset.Sha256, "real upstream download verifies against pinned package");
        var realGame = Dir("real-package-install");
        FgInstaller.Install(realGame, "fixture-game.exe", realSource, realAsset, ini2);
        Check(FgPackage.Hash(Path.Combine(realGame, realAsset.Name)) == realAsset.Sha256, "real package installed without loading DLL");
        FgInstaller.Restore(realGame);
        Check(!File.Exists(Path.Combine(realGame, realAsset.Name)), "real package removed by ownership-aware restore");
        var candidate = FgPackage.CandidateAssets[0];
        var candidateSource = await FgPackage.AcquireAsync(Dir("download-candidate"), candidate, null, CancellationToken.None);
        FgInstaller.Install(realGame, "fixture-game.exe", realSource, realAsset, ini2);
        FgInstaller.Install(realGame, "fixture-game.exe", candidateSource, candidate, ini4);
        Check(FgPackage.Hash(Path.Combine(realGame, candidate.Name)) == candidate.Sha256, "real 0.2.4 package upgrades from 0.2.3 without loading DLL");
        FgInstaller.Install(realGame, "fixture-game.exe", realSource, realAsset, ini2);
        Check(FgPackage.Hash(Path.Combine(realGame, realAsset.Name)) == realAsset.Sha256, "real 0.2.4 package downgrades to 0.2.3");
        FgInstaller.Restore(realGame);
    }
    Console.WriteLine($"{count} checks passed; fixture tests only, no GPU or gameplay claim.");
}
finally { Directory.Delete(root, true); }

static void InterlockedExtensionsMax(ref int target, int value)
{
    int previous;
    do { previous = Volatile.Read(ref target); if (previous >= value) return; }
    while (Interlocked.CompareExchange(ref target, value, previous) != previous);
}
