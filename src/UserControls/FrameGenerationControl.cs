using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.Data;
using DLSS_Swapper.FrameGeneration;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace DLSS_Swapper.UserControls;

/// <summary>Experimental mod management; writing a cap is never reported as active frame generation.</summary>
public sealed class FrameGenerationControl : StackPanel
{
    readonly Game _game;
    readonly EasyContentDialog _dialog;
    readonly TextBox _exe = new() { Header = "实际渲染 EXE（必须位于此游戏目录内）" };
    readonly ComboBox _gpu = new() { Header = "显卡配置（请确认游戏实际使用的显卡）", ItemsSource = FgGpuProfile.All, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox _multiplier = new() { Header = "帧生成倍率上限", ItemsSource = new[] { "2×：最多额外生成 1 帧（推荐起点）", "4×：最多额外生成 3 帧（实验）" }, SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly ComboBox _proxy = new() { Header = "代理入口（只安装一个；替代入口必须能被游戏加载）", ItemsSource = FgPackage.Assets.Select(a => a.Name).ToArray(), SelectedIndex = 0, HorizontalAlignment = HorizontalAlignment.Stretch };
    readonly CheckBox _approximate = new() { Content = "近似采样（仅 SM86；可能影响画质，默认关闭）" };
    readonly TextBox _local = new() { Header = "本地 DLL（可选，留空则从上游固定提交下载）", PlaceholderText = "只接受此预览版已核对的 Native 0.2.3 文件" };
    readonly TextBlock _recommendation = Text("");
    readonly TextBlock _compatibility = Text("");
    readonly TextBlock _detected = Text("正在读取 NVIDIA 驱动提供的显卡信息……");
    readonly InfoBar _status = new() { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational, Message = "未执行安装。普通 DLSS 文件替换仍使用原界面。" };
    readonly CheckBox _consent = new() { Content = Text("我已退出游戏，确认这是可使用 Mod 的单机环境，并了解未适配/实验配置可能无效或崩溃。") };
    readonly Button _apply = new() { Content = "解锁 / 应用帧生成配置", IsEnabled = false };
    readonly Button _restore = new() { Content = "卸载并恢复原件" };
    readonly Button _cancel = new() { Content = "取消下载", Visibility = Visibility.Collapsed };
    readonly StackPanel _settings = new() { Spacing = 8 };
    readonly ContentControl _settingsHost = new() { HorizontalContentAlignment = HorizontalAlignment.Stretch };
    CancellationTokenSource? _cancellation;
    bool _busy;
    string PreferencesPath => Path.Combine(Storage.StoragePath, "FrameGeneration", "targets.json");

    static TextBlock Text(string text) => new() { Text = text, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

    public FrameGenerationControl(Game game, EasyContentDialog dialog)
    {
        _game = game;
        _dialog = dialog;
        Spacing = 10;
        var profile = FgGameProfile.Find(game.InstallPath);
        if (profile is not null) _exe.Text = Path.Combine(game.InstallPath, profile.RelativeExe.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            var savedExe = FgPreferences.GetExe(PreferencesPath, game.InstallPath);
            if (savedExe is not null && File.Exists(savedExe)) _exe.Text = savedExe;
        }
        catch { /* A bad preference file must not prevent manually selecting the game. */ }
        Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Warning, Message = "非官方帧生成实验功能，仅 Windows x64 / D3D12。2×/4×是上限，实际倍率由游戏决定；文件安装成功不等于已解锁。" });
        Children.Add(_compatibility);
        Children.Add(_detected);
        _settings.Children.Add(_exe);
        var browseExe = new Button { Content = "选择渲染 EXE…" };
        browseExe.Click += (_, _) => Browse(_exe, "游戏程序", "*.exe");
        _settings.Children.Add(browseExe);
        _settings.Children.Add(_gpu);
        _settings.Children.Add(_recommendation);
        _settings.Children.Add(_multiplier);
        _settings.Children.Add(_approximate);
        _settings.Children.Add(_proxy);
        _settings.Children.Add(_local);
        var browseDll = new Button { Content = "选择已下载的 DLL…" };
        browseDll.Click += (_, _) => Browse(_local, "帧生成代理", "*.dll");
        _settings.Children.Add(browseDll);
        _settings.Children.Add(_consent);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        actions.Children.Add(_apply);
        actions.Children.Add(_restore);
        _settings.Children.Add(actions);
        _settingsHost.Content = _settings;
        Children.Add(_settingsHost);
        Children.Add(_cancel);
        Children.Add(_status);
        var logs = new Button { Content = "检查 / 打开后端日志" };
        logs.Click += (_, _) => OpenLogs();
        Children.Add(logs);
        Children.Add(new Expander {
            Header = "自动解锁失败？手动解锁与恢复指引",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Content = Text("1. 退出游戏，确认选的是 Binaries\\Win64 内实际渲染 EXE，不是启动器。\n2. 此工具只装代理和 INI；保留游戏原有 nvngx_dlssg.dll。使用 D3D12，进入游戏图形设置启用 DLSS 帧生成。若有倍率菜单再选 2×/4×。\n3. 无日志：检查目录；先卸载本工具安装，再试另一个入口，例如 winmm.dll。必须选上游同名文件，不能把 version.dll 简单改名；其他 Mod 的 DLL 不可覆盖。\n4. 有日志但 FG 灰色/无效果：检查日志错误、游戏/驱动版本和实际 GPU；MaxGeneratedFrames=3 不能替游戏强制请求 4×。不要据此修改驱动、伪装显卡、关闭安全软件或绕过反作弊。\n5. 更新游戏或出现崩溃时先卸载恢复再重试。恢复按钮仅移除本工具拥有且未被修改的代理/INI；原 INI 从 .dlss-swapper-fg\\original.ini 恢复。\n6. 若自动恢复拒绝：先复制整个 .dlss-swapper-fg 文件夹和当前 DLL/INI 到安全位置，读取 state.json 确认 Proxy，再手动移走该代理；有 original.ini 则恢复为 dlssg_sm86.ini，无原件则移走本工具生成的 INI。保留其他 Mod，检查后再移走本工具记录目录。\n7. .fg-stage 是中断留下的暂存文件：保留备份，核对对应目标后仅移走本工具的暂存文件，再尝试恢复。\n8. 40HX 的驱动状态和 NGX/CUDA 能力需单独验证；本工具不会修复 Code 43，也不保证等同于 2060 SUPER。")
        });
        Children.Add(new HyperlinkButton { Content = "完整 README / 测试步骤 / 上游来源", NavigateUri = new Uri("https://github.com/ygyp57pxkf-cloud/dlss-swapper/blob/feature/frame-generation-preview/README.md") });
        _gpu.SelectionChanged += (_, _) => {
            if (_gpu.SelectedItem is FgGpuProfile selected)
            {
                _recommendation.Text = selected.Recommendation;
                _approximate.IsEnabled = selected.Router == "SM86";
                if (!_approximate.IsEnabled) _approximate.IsChecked = false;
            }
            UpdateApply();
        };
        _consent.Checked += (_, _) => UpdateApply();
        _consent.Unchecked += (_, _) => UpdateApply();
        _exe.TextChanged += (_, _) => UpdateCompatibility();
        _apply.Click += async (_, _) => await ApplyAsync();
        _restore.Click += async (_, _) => await RestoreAsync();
        _cancel.Click += (_, _) => _cancellation?.Cancel();
        dialog.Closing += (_, args) => { if (_busy) args.Cancel = true; };
        UpdateCompatibility();
        LoadInstalledSettings();
        Loaded += async (_, _) => await DetectGpuAsync();
    }
    void UpdateApply() => _apply.IsEnabled = !_busy && _consent.IsChecked == true && _gpu.SelectedItem is FgGpuProfile;
    void UpdateCompatibility()
    {
        var profile = FgGameProfile.FindExe(_exe.Text.Trim().Trim('"'));
        _compatibility.Text = profile is null
            ? "未适配游戏：默认仅使用原有 DLSS 文件替换。若选择尝试解锁，需自行确认实际 EXE、D3D12 和原有帧生成接入；安装可能无效。"
            : profile.Name + "：" + profile.Guidance;
    }
    void LoadInstalledSettings()
    {
        try
        {
            if (!File.Exists(_exe.Text)) return;
            var directory = FgInstaller.ValidateExe(_game.InstallPath, _exe.Text);
            var state = FgInstaller.ReadState(directory);
            if (state is null) return;
            _proxy.SelectedItem = state.Proxy;
            var iniPath = Path.Combine(directory, FgInstaller.IniName);
            if (File.Exists(iniPath))
            {
                var ini = File.ReadAllText(iniPath);
                if (ini.Contains("Router=SM75")) _gpu.SelectedItem = FgGpuProfile.All[1];
                else if (ini.Contains("Router=SM86")) _gpu.SelectedItem = FgGpuProfile.All[0];
                _multiplier.SelectedIndex = ini.Contains("MaxGeneratedFrames=3") ? 1 : 0;
                _approximate.IsChecked = ini.Contains("HardwareBilinear=1") && (_gpu.SelectedItem as FgGpuProfile)?.Router == "SM86";
            }
            Status("已有安装记录：" + state.Version + " / " + state.Proxy + "。实际加载与倍率尚待游戏验证。", InfoBarSeverity.Informational);
        }
        catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Warning); }
    }
    void Browse(TextBox box, string label, string filter)
    {
        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow);
            var file = FileSystemHelper.OpenFile(hwnd, [new FileSystemHelper.FileFilter(label, filter)], _game.InstallPath);
            if (!string.IsNullOrWhiteSpace(file))
            {
                box.Text = file;
                if (box == _exe)
                {
                    FgInstaller.ValidateExe(_game.InstallPath, file);
                    FgPreferences.SaveExe(PreferencesPath, _game.InstallPath, file);
                    LoadInstalledSettings();
                }
            }
        }
        catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Error); }
    }
    async Task DetectGpuAsync()
    {
        try
        {
            var smi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
            using var process = Process.Start(new ProcessStartInfo(smi, "--query-gpu=name,memory.total,driver_version --format=csv,noheader") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true });
            if (process is null) throw new IOException("无法运行 nvidia-smi。");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(8));
            var outputTask = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errorTask = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            catch { if (!process.HasExited) process.Kill(); throw; }
            var output = (await outputTask).Trim();
            await errorTask;
            if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(output)) throw new IOException("驱动未返回可用 GPU 信息。");
            _detected.Text = "检测信息（型号 / 显存 / 驱动）：\n" + output + "\n这不是性能实测，也不证明游戏使用此 GPU。";
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 1 && _gpu.SelectedItem is null) _gpu.SelectedItem = FgGpuProfile.Recommend(lines[0]);
        }
        catch { _detected.Text = "未能自动读取 GPU。请手动选择 3080 或 40HX 实验配置，并在设备管理器确认驱动正常。"; }
    }
    static void EnsureGameStopped(string exe)
    {
        var processes = Process.GetProcessesByName(Path.GetFileNameWithoutExtension(exe));
        try { if (processes.Length != 0) throw new IOException("检测到同名游戏进程正在运行，请完全退出游戏后重试。"); }
        finally { foreach (var process in processes) process.Dispose(); }
    }
    void Busy(bool busy)
    {
        _busy = busy;
        _settingsHost.IsEnabled = !busy;
        _dialog.IsEnabled = true;
        _cancel.Visibility = busy ? Visibility.Visible : Visibility.Collapsed;
        UpdateApply();
    }
    void Status(string message, InfoBarSeverity severity) { _status.Message = message; _status.Severity = severity; }
    async Task ApplyAsync()
    {
        if (_busy || _consent.IsChecked != true || _gpu.SelectedItem is not FgGpuProfile profile) return;
        Busy(true);
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        try
        {
            var exe = Path.GetFullPath(_exe.Text.Trim().Trim('"'));
            var directory = FgInstaller.ValidateExe(_game.InstallPath, exe);
            EnsureGameStopped(exe);
            FgPreferences.SaveExe(PreferencesPath, _game.InstallPath, exe);
            var asset = FgPackage.Assets[_proxy.SelectedIndex];
            var ini = FgGameProfile.Ini(profile.Router, _multiplier.SelectedIndex == 1 ? 4 : 2, _approximate.IsChecked == true);
            Status("正在获取固定版本并核对 DLL；下载完成前不会修改游戏文件。", InfoBarSeverity.Informational);
            var source = await FgPackage.AcquireAsync(Path.Combine(Storage.StoragePath, "FrameGeneration", FgPackage.Commit), asset, _local.Text.Trim().Trim('"'), cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            EnsureGameStopped(exe);
            Status("正在备份并写入帧生成配置……", InfoBarSeverity.Informational);
            _cancel.Visibility = Visibility.Collapsed;
            await Task.Run(() => FgInstaller.Install(directory, Path.GetFileName(exe), source, asset, ini));
            Status("文件已安装，配置已写入（" + (_multiplier.SelectedIndex == 1 ? "4×" : "2×") + "上限）。请启动游戏开启 DLSS 帧生成，再检查画面、日志与实际倍率；尚未确认解锁成功。", InfoBarSeverity.Success);
        }
        catch (OperationCanceledException) { Status("下载已取消；未进入游戏文件安装步骤。", InfoBarSeverity.Informational); }
        catch (Exception ex) { Status("自动应用失败：" + ex.Message + " 展开下方手动指引。", InfoBarSeverity.Error); }
        finally { _cancellation = null; Busy(false); }
    }
    async Task RestoreAsync()
    {
        if (_busy) return;
        Busy(true);
        _cancel.Visibility = Visibility.Collapsed;
        try
        {
            var exe = Path.GetFullPath(_exe.Text.Trim().Trim('"'));
            var directory = FgInstaller.ValidateExe(_game.InstallPath, exe);
            EnsureGameStopped(exe);
            await Task.Run(() => FgInstaller.Restore(directory));
            Status("本工具安装的帧生成文件已移除，原 INI（如有）已恢复。DLSS 版本恢复仍使用原界面的 Reset。", InfoBarSeverity.Success);
        }
        catch (Exception ex) { Status("恢复未完成：" + ex.Message + " 请展开手动恢复指引。", InfoBarSeverity.Error); }
        finally { Busy(false); }
    }
    void OpenLogs()
    {
        if (_busy) return;
        try
        {
            var directory = FgInstaller.ValidateExe(_game.InstallPath, _exe.Text);
            var logs = Path.Combine(directory, "dlssg_sm86", "logs");
            if (!Directory.Exists(logs)) { Status("未发现后端日志。可能尚未启动游戏、目录错误或代理未加载；请按手动指引检查。", InfoBarSeverity.Warning); return; }
            var latest = new DirectoryInfo(logs).GetFiles("native_*.jsonl").OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
            Status(latest is null ? "日志目录存在，但未找到 native_*.jsonl；尚不能判断加载成功。" : "最新日志：" + latest.Name + " / " + latest.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss") + "。旧日志不代表本次成功；日志存在也不证明实际输出倍率。", InfoBarSeverity.Informational);
            Process.Start(new ProcessStartInfo("explorer.exe", '"' + logs + '"') { UseShellExecute = true });
        }
        catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Error); }
    }
}
