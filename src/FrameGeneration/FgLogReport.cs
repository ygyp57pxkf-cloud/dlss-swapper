using System;
using System.IO;
using System.Globalization;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace DLSS_Swapper.FrameGeneration;

public sealed record FgLogReport(string Message, bool HasErrors)
{
    public static FgLogReport ReadMany(IEnumerable<string> files, DateTime? installedAtUtc)
    {
        var reports = files.Select(file => Read(file, installedAtUtc)).ToArray();
        return new(string.Join("\n\n", reports.Select(r => r.Message)), reports.Any(r => r.HasErrors));
    }

    public static FgLogReport Read(string file, DateTime? installedAtUtc)
    {
        var modified = File.GetLastWriteTimeUtc(file);
        var label = Path.GetFileName(file) + " / " + modified.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        if (installedAtUtc.HasValue && modified < installedAtUtc.Value)
            return new(label + "：日志早于本次安装，不能用于验证所选后端。请重新启动游戏后检查。", true);

        bool redirected = false, feature = false, routeActive = false, backendInstalled = false;
        int generated = 0, errors = 0, malformed = 0;
        const int limit = 8 * 1024 * 1024;
        using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var truncated = stream.Length > limit;
        if (truncated) stream.Seek(-limit, SeekOrigin.End);
        using var reader = new StreamReader(stream);
        if (truncated) reader.ReadLine(); // Discard a partial JSON line at the seek boundary.
        while (reader.ReadLine() is { } line)
        {
            try
            {
                using var document = JsonDocument.Parse(line);
                var row = document.RootElement;
                if (row.ValueKind != JsonValueKind.Object || !row.TryGetProperty("event", out var value) || value.ValueKind != JsonValueKind.String) continue;
                var kind = value.GetString() ?? "";
                redirected |= kind == "runtime_redirect";
                feature |= kind == "feature_created";
                if (kind == "fg_gate_create_feature" && row.TryGetProperty("succeeded", out var succeeded) && succeeded.ValueKind == JsonValueKind.True) feature = true;
                if (kind == "install" && row.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.True) routeActive = true;
                if (kind == "backend_install" && row.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.Number && status.TryGetInt32(out var code))
                {
                    backendInstalled |= code == 0;
                    if (code != 0) errors++;
                }
                if (kind == "error" || kind.EndsWith("_error", StringComparison.Ordinal) || kind == "backend_unusable") errors++;
                if (kind == "evaluate" && row.TryGetProperty("generated_count", out var count) && count.ValueKind == JsonValueKind.Number && count.TryGetInt32(out var number))
                    generated = Math.Max(generated, number);
            }
            catch (JsonException) { malformed++; } // A running game may still be writing the last line.
        }
        var activity = generated > 0 ? $"日志报告每组最多生成 {generated} 帧" : "未读到有效生成帧数";
        var scope = truncated ? "仅检查文件末尾 8 MiB。" : "";
        return new($"{label}\n代理重定向：{(redirected ? "有记录" : "未发现")}；后端安装：{(backendInstalled ? "成功记录" : "未发现")}；路由启用：{(routeActive ? "有记录" : "未发现")}；功能创建：{(feature ? "有记录" : "未发现")}；{activity}；错误事件 {errors} 条。\n{scope}跳过不完整/无效 JSON {malformed} 行。以上仅是该日志的记录，实际呈现倍率、画质和稳定性仍需游戏内验证。", errors > 0);
    }
}
