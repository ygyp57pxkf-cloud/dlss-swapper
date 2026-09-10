using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace DLSS_Swapper.FrameGeneration;

public static class FgPreferences
{
    public static string? GetExe(string file, string gameRoot)
    {
        if (!File.Exists(file)) return null;
        var entries = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
        return entries is not null && entries.TryGetValue(Path.GetFullPath(gameRoot), out var exe) ? exe : null;
    }
    public static void SaveExe(string file, string gameRoot, string exe)
    {
        var entries = File.Exists(file) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file)) ?? new() : new Dictionary<string, string>();
        entries[Path.GetFullPath(gameRoot)] = Path.GetFullPath(exe);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var temp = file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries));
        File.Move(temp, file, true);
    }
}
