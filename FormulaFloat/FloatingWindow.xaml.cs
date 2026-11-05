using System;
using System.Windows;
using System.Windows.Input;

namespace FormulaFloat;
public partial class FloatingWindow : Window
{
    private readonly MainWindow host;
    private Point start;
    private Point windowOrigin;
    private bool pressed;
    private bool moved;
    public FloatingWindow(MainWindow host)
    {
        this.host = host; InitializeComponent();
        Left = SystemParameters.WorkArea.Right - 85; Top = SystemParameters.WorkArea.Top + 160;
    }
    private void PointerDown(object sender, MouseButtonEventArgs e)
    {
        start = PointToScreen(e.GetPosition(this)); windowOrigin = new(Left, Top); moved = false; pressed = true;
        ((UIElement)sender).CaptureMouse(); e.Handled = true;
    }
    private void PointerMove(object sender, MouseEventArgs e)
    {
        if (!pressed || e.LeftButton != MouseButtonState.Pressed) return;
        var point = PointToScreen(e.GetPosition(this)); var scale = System.Windows.Media.VisualTreeHelper.GetDpi(this);
        var dx = (point.X - start.X) / scale.DpiScaleX; var dy = (point.Y - start.Y) / scale.DpiScaleY;
        if (Math.Abs(dx) + Math.Abs(dy) > 5) moved = true;
        if (moved) { Left = windowOrigin.X + dx; Top = windowOrigin.Y + dy; }
    }
    private async void PointerUp(object sender, MouseButtonEventArgs e)
    {
        ((UIElement)sender).ReleaseMouseCapture(); bool click = pressed && !moved; pressed = false;
        if (click) await host.CaptureAsync();
    }
    private async void CaptureClick(object sender, RoutedEventArgs e) => await host.CaptureAsync();
    private void OpenClick(object sender, RoutedEventArgs e) => host.OpenPanel();
    private void SettingsClick(object sender, RoutedEventArgs e) => host.OpenSettings();
    private void ExitClick(object sender, RoutedEventArgs e) => host.ExitApp();
}
