using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;

namespace FormulaFloat;
public partial class MainWindow : Window
{
    private ModelSettings settings = new();
    private readonly VisionClient vision = new();
    private FloatingWindow? floating;
    private Forms.NotifyIcon? tray;
    private CancellationTokenSource? request;
    private byte[]? image;
    private readonly Stack<string> undo = new();
    private bool capturing;
    private bool configuring;
    private bool exiting;
    private HwndSource? source;
    private IntPtr handle;
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

    public MainWindow() { InitializeComponent(); }
    public void InitializeDesktop()
    {
        try { settings = SettingsStore.Load(); }
        catch { MessageBox.Show("设置或密钥无法读取，请在设置中重新填写。", "FormulaFloat"); }
        floating = new FloatingWindow(this); floating.Show();
        handle = new WindowInteropHelper(this).EnsureHandle();
        source = HwndSource.FromHwnd(handle); source.AddHook(WindowMessage);
        if (!RegisterHotKey(handle, 1, 0x4000 | 0x0002 | 0x0001, 0x4D))
            ShortcutText.Text = "Ctrl + Alt + M 已被占用，请使用悬浮球截图。";
        tray = new Forms.NotifyIcon { Icon = System.Drawing.SystemIcons.Application, Text = "FormulaFloat · 截图提取公式", Visible = true };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("截取公式", null, async (_, _) => await CaptureAsync());
        menu.Items.Add("打开结果面板", null, (_, _) => OpenPanel());
        menu.Items.Add("模型设置", null, (_, _) => OpenSettings());
        menu.Items.Add("退出", null, (_, _) => ExitApp());
        tray.ContextMenuStrip = menu; tray.DoubleClick += (_, _) => OpenPanel();
        if (string.IsNullOrWhiteSpace(settings.Model)) OpenPanel();
    }
    private IntPtr WindowMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == 0x0312 && wParam.ToInt32() == 1) { handled = true; Dispatcher.InvokeAsync(async () => await CaptureAsync()); }
        return IntPtr.Zero;
    }
    public void OpenPanel() { Show(); WindowState = WindowState.Normal; Activate(); }
    public void OpenSettings()
    {
        if (request != null || capturing || configuring) return;
        OpenPanel();
        configuring = true;
        try
        {
            var dialog = new SettingsWindow(settings) { Owner = this };
            if (dialog.ShowDialog() == true)
            {
                settings = dialog.Result!;
                StatusText.Text = "模型设置已保存。点击「截取公式」开始。";
            }
        }
        finally { configuring = false; }
    }
    public async Task CaptureAsync()
    {
        if (capturing || configuring || request != null) return;
        try { settings.Validate(); }
        catch { OpenSettings(); return; }
        capturing = true;
        Hide(); floating?.Hide();
        try
        {
            await Task.Delay(180);
            var captured = await CaptureService.CaptureAsync();
            if (captured == null) return;
            image = captured; undo.Clear(); LatexBox.Clear(); InstructionBox.Clear();
            using var memory = new MemoryStream(image);
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.StreamSource = memory; bitmap.EndInit(); bitmap.Freeze();
            SourceImage.Source = bitmap; EmptyImage.Visibility = Visibility.Collapsed;
            OpenPanel();
            await RunAsync("");
        }
        catch (Exception ex) { OpenPanel(); StatusText.Text = "截图失败：" + ex.Message; }
        finally { capturing = false; if (!exiting) floating?.Show(); RefreshControls(); }
    }
    private async Task RunAsync(string instruction)
    {
        if (image == null || request != null) return;
        using var cancellation = new CancellationTokenSource();
        request = cancellation;
        var before = LatexBox.Text;
        StatusText.Text = instruction.Length == 0 ? "正在识别公式…" : "正在按你的要求调整…";
        CancelButton.Visibility = Visibility.Visible; RefreshControls();
        try
        {
            var result = await vision.ConvertAsync(settings, image, before, instruction, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (exiting) return;
            if (before.Length > 0) undo.Push(before);
            LatexBox.Text = result;
            if (instruction.Length > 0) InstructionBox.Clear();
            StatusText.Text = "已完成 · 请核对符号与上下标，可继续输入调整要求。";
        }
        catch (OperationCanceledException) { StatusText.Text = "已取消，当前公式保持不变。"; }
        catch (Exception ex) { StatusText.Text = ex.Message; }
        finally { request = null; CancelButton.Visibility = Visibility.Collapsed; RefreshControls(); }
    }
    private void RefreshControls()
    {
        if (CaptureButton == null) return;
        bool idle = request == null;
        CaptureButton.IsEnabled = idle && !capturing;
        LatexBox.IsReadOnly = !idle; InstructionBox.IsEnabled = idle;
        RefineButton.IsEnabled = idle && image != null && !string.IsNullOrWhiteSpace(LatexBox.Text);
        RetryButton.IsEnabled = idle && image != null;
        UndoButton.IsEnabled = idle && undo.Count > 0;
        CopyButton.IsEnabled = CopyMathButton.IsEnabled = !string.IsNullOrWhiteSpace(LatexBox.Text);
    }
    private async void CaptureClick(object sender, RoutedEventArgs e) => await CaptureAsync();
    private async void RetryClick(object sender, RoutedEventArgs e) => await RunAsync("");
    private async void RefineClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InstructionBox.Text)) { InstructionBox.Focus(); StatusText.Text = "请先输入调整要求。"; return; }
        await RunAsync(InstructionBox.Text.Trim());
    }
    private void InstructionKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter && RefineButton.IsEnabled) { e.Handled = true; RefineClick(sender, e); } }
    private void LatexChanged(object sender, System.Windows.Controls.TextChangedEventArgs e) => RefreshControls();
    private void CancelClick(object sender, RoutedEventArgs e) => request?.Cancel();
    private void SettingsClick(object sender, RoutedEventArgs e) => OpenSettings();
    private void UndoClick(object sender, RoutedEventArgs e) { if (undo.Count > 0) LatexBox.Text = undo.Pop(); RefreshControls(); StatusText.Text = "已恢复上一个公式版本。"; }
    private void Copy(bool math)
    {
        try { Clipboard.SetText(math ? "$$\n" + LatexBox.Text.Trim() + "\n$$" : LatexBox.Text); StatusText.Text = "已复制到剪贴板。"; }
        catch (ExternalException) { StatusText.Text = "剪贴板正被占用，请再试一次。"; }
    }
    private void CopyClick(object sender, RoutedEventArgs e) => Copy(false);
    private void CopyMathClick(object sender, RoutedEventArgs e) => Copy(true);
    private void CollapseClick(object sender, RoutedEventArgs e) => Hide();
    private void DragTitle(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void OnClosing(object? sender, CancelEventArgs e) { if (!exiting) { e.Cancel = true; Hide(); } }
    public void ExitApp()
    {
        exiting = true; request?.Cancel(); UnregisterHotKey(handle, 1); source?.RemoveHook(WindowMessage);
        if (tray != null) { tray.Visible = false; tray.ContextMenuStrip?.Dispose(); tray.Dispose(); }
        floating?.Close(); image = null; Application.Current.Shutdown();
    }
}
