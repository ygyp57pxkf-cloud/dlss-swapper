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
    Console.WriteLine($"{count} checks passed; fixture tests only, no GPU or gameplay claim.");
}
finally { Directory.Delete(root, true); }
