using System;
using System.IO;
using System.Globalization;
using System.Text.Json;

namespace DLSS_Swapper.FrameGeneration;

public sealed record FgLogReport(string Message, bool HasErrors)
{
    public static FgLogReport Read(string file, DateTime? installedAtUtc)
    {
        var modified = File.GetLastWriteTimeUtc(file);
        var label = Path.GetFileName(file) + " / " + modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (installedAtUtc.HasValue && modified < installedAtUtc.Value)
            return new(label + "：日志早于本次安装，不能用于验证所选后端。请重新启动游戏后检查。", true);

        bool redirected = false, feature = false;
        int generated = 0, errors = 0, malformed = 0;
        const int limit = 8 * 1024 * 1024;
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        while (stream.Position < limit && reader.ReadLine() is { } line)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var row = document.RootElement;
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("event", out var value) || value.ValueKind != JsonValueKind.String) continue;
                var kind = value.GetString() ?? "";
                redirected |= kind == "runtime_redirect";
                feature |= kind == "feature_created";
                if (kind == "error" || kind.EndsWith("_error", StringComparison.Ordinal)) errors++;
                if (kind == "evaluate" && row.TryGetProperty("generated_count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number))
                    generated = Math.Max(generated, number);
            }
            catch (JsonException) { malformed++; } // A running game may still be writing the last line.
        }
        var activity = generated > 0 ? $"日志报告每组最多生成 {generated} 帧" : "未读到有效生成帧数";
        var scope = stream.Length > limit ? "仅检查文件前 8 MiB。" : "";
        return new($"{label}\n代理重定向：{(redirected ? "有记录" : "未发现")}；功能创建：{(feature ? "有记录" : "未发现")}；{activity}；错误事件 {errors} 条。\n{scope}跳过不完整/无效 JSON {malformed} 行。以上仅是该日志的记录，实际呈现倍率、画质和稳定性仍需游戏内验证。", errors > 0);
    }
}
