using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DLSS_Swapper.EnhancementTools;
using DLSS_Swapper.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;

namespace DLSS_Swapper.Pages;

public sealed class EnhancementToolsPage : Page
{
    public static string PageTag { get; } = "PageTag_EnhancementTools";
    readonly TextBox _target = new() { Header = "游戏目录 / 实际游戏 EXE", IsReadOnly = true, PlaceholderText = "从游戏详情进入，或选择游戏 EXE" };
    readonly TextBlock _inspection = Text("选择 EXE 后读取位数与静态图形接口线索，不写入游戏目录。");
    readonly List<ToolCard> _cards = [];
    readonly ToolCard _games;
    readonly Button _browse = new() { Content = "选择实际游戏 EXE…" };

    internal static TextBlock Text(string value) => new() { Text = value, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true };

    public EnhancementToolsPage()
    {
        var body = new StackPanel { Spacing = 16, MaxWidth = 960, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(24) };
        body.Children.Add(new TextBlock { Text = "画质增强 · FG Preview 0.3", FontSize = 28, TextWrapping = TextWrapping.Wrap });
        body.Children.Add(Text("按需准备独立的游戏与媒体工具，再打开它们的配置界面。普通 DLSS / 光线重构 DLL 替换仍在游戏库中操作。"));
        body.Children.Add(new InfoBar { IsOpen = true, IsClosable = false, Severity = InfoBarSeverity.Informational,
            Message = "社区 DLSS 5 / Neural Rendering 工具入口，非 NVIDIA 官方通用升级包。安装完成不表示增强已生效；NR、超分、光线重构和插帧是不同功能。" });
        body.Children.Add(_target);
        _browse.Click += async (_, _) => await BrowseAsync();
        body.Children.Add(_browse);
        body.Children.Add(_inspection);
        _games = new ToolCard(ToolCatalog.Games, () => _target.Text);
        _cards.Add(_games);
        _cards.Add(new ToolCard(ToolCatalog.Media));
        foreach (var card in _cards) body.Children.Add(card);
        body.Children.Add(Text("建议先在 2 号机 RTX 3080 试用。3 号机 40HX 的双解锁 / NVENC 记录不能替代这些工具的 NR 验收；保留当前稳定驱动。"));
        Content = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Content = body };
        Loaded += async (_, _) => { foreach (var card in _cards) await card.RefreshAsync(); };
    }

    public void SetGameRoot(string root)
    {
        _target.Text = root;
        _inspection.Text = "已带入游戏目录。请选择实际游戏 EXE，以读取位数和接口线索；外部管理器仍需 Add a game。";
    }

    async Task BrowseAsync()
    {
        try
        {
            var path = _target.Text;
            var initial = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            var exe = FileSystemHelper.OpenFile(WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow),
                [new FileSystemHelper.FileFilter("游戏程序", "*.exe")], initial);
            if (string.IsNullOrWhiteSpace(exe)) return;
            _browse.IsEnabled = false;
            _target.Text = exe;
            _inspection.Text = "正在读取 EXE…";
            var result = await Task.Run(() => GameTargetInspector.Inspect(exe));
            _inspection.Text = result.Architecture + "\n静态图形接口线索：" + (result.GraphicsImports.Length == 0 ? "未确认" : string.Join("、", result.GraphicsImports))
                + "\n同目录 Mod 线索：" + (result.ExistingMods.Length == 0 ? "未发现已知文件名（不代表没有 Mod）" : string.Join("、", result.ExistingMods)) + "\n" + result.Guidance;
        }
        catch (Exception ex) { _inspection.Text = "未能读取游戏信息：" + ex.Message; }
        finally { _browse.IsEnabled = true; }
    }

    sealed class ToolCard : ContentControl
    {
        readonly EnhancementTool _tool;
        readonly Func<string>? _gamePath;
        readonly Button _download = new() { Content = "下载并准备" };
        readonly Button _import = new() { Content = "导入已下载的包…" };
        readonly Button _open = new() { Content = "打开工具", IsEnabled = false };
        readonly Button _folder = new() { Content = "打开工具目录", IsEnabled = false };
        readonly Button _refresh = new() { Content = "重新检查" };
        readonly Button _cancel = new() { Content = "取消", Visibility = Visibility.Collapsed };
        readonly ProgressBar _progress = new() { Visibility = Visibility.Collapsed, Maximum = 100 };
        readonly InfoBar _status = new() { IsOpen = true, IsClosable = false, Message = "尚未准备；点击下载或导入本地包。" };
        readonly Button? _copyOpen;
        CancellationTokenSource? _cancellation;
        bool _busy;
        bool _ready;
        string Root => Path.Combine(Storage.StoragePath, "EnhancementTools");

        public ToolCard(EnhancementTool tool, Func<string>? gamePath = null)
        {
            _tool = tool;
            _gamePath = gamePath;
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            var body = new StackPanel { Spacing = 10 };
            body.Children.Add(new TextBlock { Text = tool.Name + " · " + tool.Version, FontSize = 22, TextWrapping = TextWrapping.Wrap });
            body.Children.Add(Text(tool.Description));
            body.Children.Add(Text($"下载约 {tool.DownloadBytes / 1048576d:F0} MiB · " + (tool.IsZip ? "建议预留 2 GiB 空间 · 已含便携 Python" : "独立便携程序") + " · 首次使用需手动准备"));
            // Short rows fit the existing compact navigation layout; text wraps at narrow widths.
            var prepare = new StackPanel { Spacing = 8 };
            prepare.Children.Add(_download); prepare.Children.Add(_import);
            body.Children.Add(prepare);
            body.Children.Add(_progress); body.Children.Add(_cancel); body.Children.Add(_status);
            body.Children.Add(_open);
            if (gamePath is not null)
            {
                _copyOpen = new Button { Content = new TextBlock { Text = "复制游戏路径并打开管理器", TextWrapping = TextWrapping.Wrap }, IsEnabled = false };
                _copyOpen.Click += async (_, _) => await OpenAsync(true);
                body.Children.Add(_copyOpen);
            }
            body.Children.Add(_folder); body.Children.Add(_refresh);
            body.Children.Add(new HyperlinkButton { Content = "作者发布页 / 手动下载安装包", NavigateUri = tool.ReleaseUrl });
            body.Children.Add(new Expander { Header = "使用步骤、兼容性与恢复", HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch, Content = Text(tool.Guide) });
            Content = new Border { Padding = new Thickness(16), CornerRadius = new CornerRadius(8),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"], Child = body };
            _download.Click += async (_, _) => await PrepareAsync(null);
            _import.Click += async (_, _) => await ImportAsync();
            _open.Click += async (_, _) => await OpenAsync(false);
            _folder.Click += (_, _) => {
                try { Process.Start(new ProcessStartInfo(ToolInstaller.ToolDirectory(Root, _tool)) { UseShellExecute = true }); }
                catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Error); }
            };
            _refresh.Click += async (_, _) => await RefreshAsync();
            _cancel.Click += (_, _) => _cancellation?.Cancel();
        }

        void Status(string text, InfoBarSeverity severity = InfoBarSeverity.Informational) { _status.Message = text; _status.Severity = severity; }
        void Busy(bool value)
        {
            _busy = value;
            _download.IsEnabled = _import.IsEnabled = _refresh.IsEnabled = !value;
            _open.IsEnabled = !value && _ready;
            if (_copyOpen is not null) _copyOpen.IsEnabled = !value && _ready;
            try { _folder.IsEnabled = !value && Directory.Exists(ToolInstaller.ToolDirectory(Root, _tool)); }
            catch { _folder.IsEnabled = false; }
        }
        public async Task RefreshAsync()
        {
            if (_busy) return;
            try
            {
                Busy(true);
                var prepared = await Task.Run(() => ToolInstaller.GetPrepared(Root, _tool));
                _ready = prepared is not null;
                Status(_ready ? "工具已准备，可以打开。GPU / 游戏 / 视频效果待实测。" : "尚未准备。下载或导入不会自动启动工具或修改游戏。");
            }
            catch (Exception ex) { _ready = false; Status(ex.Message, InfoBarSeverity.Error); }
            finally { Busy(false); }
        }
        async Task ImportAsync()
        {
            try
            {
                var file = FileSystemHelper.OpenFile(WinRT.Interop.WindowNative.GetWindowHandle(App.CurrentApp.MainWindow),
                    [new FileSystemHelper.FileFilter(_tool.FileName, _tool.IsZip ? "*.zip" : "*.exe")]);
                if (!string.IsNullOrWhiteSpace(file)) await PrepareAsync(file);
            }
            catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Error); }
        }
        async Task PrepareAsync(string? file)
        {
            if (_busy) return;
            try
            {
                Busy(true);
                _cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(30));
                _cancel.Visibility = _progress.Visibility = Visibility.Visible;
                _progress.IsIndeterminate = true;
                Status("正在准备 " + _tool.Version + "…");
                var progress = new Progress<ToolProgress>(p => {
                    if (!_busy || _cancellation is null) return;
                    _progress.IsIndeterminate = p.Total == 0;
                    if (p.Total > 0) _progress.Value = Math.Clamp(100d * p.Bytes / p.Total, 0, 100);
                    Status(p.Stage + (p.Total > 0 ? $" · {p.Bytes / 1048576d:F0} / {p.Total / 1048576d:F0} MiB" : ""));
                });
                var token = _cancellation.Token;
                await Task.Run(() => ToolInstaller.PrepareAsync(Root, _tool, file, App.CurrentApp.HttpClient, progress, token));
                _ready = true;
                Status("工具已准备。点击打开后，在它的界面里配置；增强效果待实测。", InfoBarSeverity.Success);
            }
            catch (OperationCanceledException) { Status("已取消或下载超时；已清理本次未完成文件。可重试或导入本地包。"); }
            catch (Exception ex) { Status(ex.Message + " 可重试，或从作者发布页下载相同版本后导入。", InfoBarSeverity.Error); }
            finally
            {
                _cancellation?.Dispose(); _cancellation = null;
                _cancel.Visibility = _progress.Visibility = Visibility.Collapsed;
                Busy(false);
            }
        }
        async Task OpenAsync(bool copyPath)
        {
            if (_busy) return;
            try
            {
                Busy(true);
                var prepared = await Task.Run(() => ToolInstaller.GetPrepared(Root, _tool)) ?? throw new IOException("工具尚未准备，请下载或导入。");
                if (copyPath)
                {
                    var path = _gamePath?.Invoke();
                    if (string.IsNullOrWhiteSpace(path)) throw new IOException("请先选择游戏 EXE，或从游戏详情进入。");
                    var data = new DataPackage(); data.SetText(path); Clipboard.SetContent(data);
                }
                using var process = Process.Start(ToolInstaller.LaunchInfo(prepared));
                Status((copyPath ? "路径已复制，请在外部管理器 Add a game 中选择。" : "已请求打开工具。")
                    + (_tool.IsZip ? " 首次启动请等待终端提示，打开其中的本地网页地址。" : " 请在外部管理器中完成游戏配置。") + " 启动请求不代表 NR 已运行。");
            }
            catch (Exception ex) { Status(ex.Message, InfoBarSeverity.Error); }
            finally { Busy(false); }
        }
    }
}
