using System.Globalization;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FormulaFloat;

static class LayoutRender
{
    public static void Run()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var app = new App(); app.InitializeComponent();
                var main = new MainWindow();
                ((TextBox)main.FindName("LatexBox")).Text = @"E = mc^{2}";
                ((TextBlock)main.FindName("StatusText")).Text = "布局示例 · 示例内容并非模型识别结果。";
                ((TextBox)main.FindName("InstructionBox")).Text = "将质量 m 改为带下标的 m₀";
                ((TextBlock)main.FindName("EmptyImage")).Visibility = Visibility.Collapsed;
                var visual = new DrawingVisual();
                using (var g = visual.RenderOpen())
                {
                    g.DrawRectangle(Brushes.White, null, new Rect(0, 0, 400, 84));
                    g.DrawText(new FormattedText("E = mc²", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface("Cambria Math"), 40, Brushes.Black, 1), new Point(110, 18));
                }
                var source = new RenderTargetBitmap(400, 84, 96, 96, PixelFormats.Pbgra32); source.Render(visual);
                ((Image)main.FindName("SourceImage")).Source = source;
                Render(main, 600, 730, "artifacts/layout-main.png");
                var settings = new SettingsWindow(new ModelSettings { Endpoint = "https://your-provider.example/v1", Model = "your-vision-model" });
                Render(settings, 484, 570, "artifacts/layout-settings.png");
                Console.WriteLine("Rendered WPF layouts to artifacts/ (sample content only).");
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.Start(); thread.Join();
        if (failure != null) throw failure;
    }
    private static void Render(Window window, int width, int height, string path)
    {
        var content = (UIElement)window.Content; window.Content = null;
        var root = new Border { Background = window.Background, Child = content, Width = width, Height = height };
        TextElement.SetFontFamily(root, window.FontFamily); TextElement.SetFontSize(root, window.FontSize); TextElement.SetForeground(root, window.Foreground);
        root.Measure(new Size(width, height)); root.Arrange(new Rect(0, 0, width, height)); root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32); bitmap.Render(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }
}
