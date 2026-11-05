using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Forms = System.Windows.Forms;

namespace FormulaFloat;

public static class CaptureService
{
    [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X; public int Y; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
    private static Point CursorPosition() { GetCursorPos(out var p); return new(p.X, p.Y); }

    public static Task<byte[]?> CaptureAsync()
    {
        var completion = new TaskCompletionSource<byte[]?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var bounds = Forms.SystemInformation.VirtualScreen;
        var desktop = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
        try { using var g = Graphics.FromImage(desktop); g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size); }
        catch { desktop.Dispose(); throw; }
        var overlays = new List<SelectionOverlay>();
        Point? start = null;
        Rectangle selection = Rectangle.Empty;
        bool finished = false;
        void Finish(bool accept)
        {
            if (finished) return;
            finished = true;
            byte[]? result = null;
            try
            {
                var area = Rectangle.Intersect(selection, bounds);
                if (accept && area.Width >= 5 && area.Height >= 5)
                {
                    area.Offset(-bounds.Left, -bounds.Top);
                    using var crop = desktop.Clone(area, PixelFormat.Format32bppArgb);
                    using var memory = new MemoryStream(); crop.Save(memory, ImageFormat.Png); result = memory.ToArray();
                }
                completion.TrySetResult(result);
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { foreach (var overlay in overlays) { overlay.Close(); overlay.Dispose(); } desktop.Dispose(); }
        }
        try
        {
            foreach (var screen in Forms.Screen.AllScreens)
            {
                var overlay = new SelectionOverlay(screen.Bounds, desktop, bounds, () => selection, () => Finish(false));
                overlay.MouseDown += (_, e) =>
                {
                    if (e.Button == Forms.MouseButtons.Right) { Finish(false); return; }
                    if (e.Button != Forms.MouseButtons.Left) return;
                    start = CursorPosition(); overlay.Capture = true;
                };
                overlay.MouseMove += (_, _) =>
                {
                    if (start is not Point origin) return;
                    var end = CursorPosition();
                    selection = Rectangle.FromLTRB(Math.Min(origin.X, end.X), Math.Min(origin.Y, end.Y), Math.Max(origin.X, end.X), Math.Max(origin.Y, end.Y));
                    foreach (var item in overlays) item.Invalidate();
                };
                overlay.MouseUp += (_, e) => { if (e.Button == Forms.MouseButtons.Left && start.HasValue) Finish(true); };
                overlays.Add(overlay);
            }
            foreach (var overlay in overlays) overlay.Show();
            overlays.Find(x => x.Bounds.Contains(CursorPosition()))?.Activate();
        }
        catch { Finish(false); throw; }
        return completion.Task;
    }

    private sealed class SelectionOverlay : Forms.Form
    {
        private readonly Bitmap desktop;
        private readonly Rectangle virtualBounds;
        private readonly Func<Rectangle> getSelection;
        private readonly Action cancel;
        public SelectionOverlay(Rectangle bounds, Bitmap image, Rectangle virtualBounds, Func<Rectangle> selection, Action cancel)
        {
            this.desktop = image; this.virtualBounds = virtualBounds; getSelection = selection; this.cancel = cancel;
            FormBorderStyle = Forms.FormBorderStyle.None; StartPosition = Forms.FormStartPosition.Manual;
            AutoScaleMode = Forms.AutoScaleMode.None; Bounds = bounds; TopMost = true; ShowInTaskbar = false;
            Cursor = Forms.Cursors.Cross; DoubleBuffered = true; KeyPreview = true;
        }
        protected override bool ProcessCmdKey(ref Forms.Message msg, Forms.Keys keyData)
        {
            if (keyData == Forms.Keys.Escape) { cancel(); return true; }
            return base.ProcessCmdKey(ref msg, keyData);
        }
        protected override void OnPaint(Forms.PaintEventArgs e)
        {
            var source = new Rectangle(Left - virtualBounds.Left, Top - virtualBounds.Top, Width, Height);
            e.Graphics.DrawImage(desktop, ClientRectangle, source, GraphicsUnit.Pixel);
            var selected = getSelection(); selected.Offset(-Left, -Top);
            using var dim = new SolidBrush(Color.FromArgb(105, 20, 18, 35));
            using var region = new Region(ClientRectangle);
            if (!selected.IsEmpty) region.Exclude(selected);
            e.Graphics.FillRegion(dim, region);
            if (!selected.IsEmpty)
            {
                using var pen = new Pen(Color.FromArgb(175, 153, 255), 2);
                e.Graphics.DrawRectangle(pen, selected);
            }
            using var font = new Font("Microsoft YaHei UI", 11);
            var label = selected.IsEmpty ? "拖拽框选公式 · Esc / 右键取消" : $"{selected.Width} × {selected.Height} px · 松开识别 · Esc 取消";
            var size = e.Graphics.MeasureString(label, font);
            using var background = new SolidBrush(Color.FromArgb(235, 36, 32, 52));
            e.Graphics.FillRectangle(background, 24, 24, size.Width + 28, 42);
            e.Graphics.DrawString(label, font, Brushes.White, 38, 34);
        }
    }
}
