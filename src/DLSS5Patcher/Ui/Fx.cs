using System.Diagnostics;
using System.Drawing.Imaging;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 丝滑动效（Apple 宣传片风格：ease-out cubic 缓动）。
/// - AnimatePageSwitch：页面切换时旧页淡出、新页带轻微位移淡入（截图位图交叉混合，60fps 计时器驱动）
/// - FadeIn：顶层窗口/弹窗透明度渐显 + 轻微上浮
/// </summary>
public static class Fx
{
    private sealed class BufferPanel : Panel
    {
        public BufferPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    private static Panel? _overlay;
    private static System.Windows.Forms.Timer? _timer;
    private static Bitmap? _oldBmp;
    private static Bitmap? _newBmp;

    /// <summary>页面切换动画。host = 承载页面的容器；dir = 1 新页从右侧推入，-1 从左侧。</summary>
    public static void AnimatePageSwitch(Control host, Control? oldPage, Control newPage, int dir)
    {
        Finish();
        newPage.BringToFront();
        if (oldPage == null || ReferenceEquals(oldPage, newPage) || host.Width < 50) return;

        _oldBmp = Capture(oldPage, host);
        _newBmp = Capture(newPage, host);

        _overlay = new BufferPanel
        {
            Location = Point.Empty,
            Size = host.ClientSize,
            BackColor = Theme.Bg,
        };
        host.Controls.Add(_overlay);
        _overlay.BringToFront();

        var sw = Stopwatch.StartNew();
        const int durationMs = 300;
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) =>
        {
            if (sw.ElapsedMilliseconds >= durationMs) Finish();
            else _overlay?.Invalidate();
        };
        _overlay.Paint += (_, e) =>
        {
            double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
            double ease = 1 - Math.Pow(1 - t, 3);                       // ease-out cubic
            int shift = (int)(dir * (1 - ease) * host.Width / 26f);      // 轻推位移
            var g = e.Graphics;

            if (_oldBmp != null) g.DrawImage(_oldBmp, 0, 0);
            if (_newBmp != null) DrawWithAlpha(g, _newBmp, shift, (float)ease);
        };
        _timer.Start();
    }

    /// <summary>立即结束当前动画（若有）。切换被快速连点时保证状态一致。</summary>
    public static void Finish()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        if (_overlay != null)
        {
            var parent = _overlay.Parent;
            parent?.Controls.Remove(_overlay);
            _overlay.Dispose();
            _overlay = null;
        }
        _oldBmp?.Dispose(); _oldBmp = null;
        _newBmp?.Dispose(); _newBmp = null;
    }

    /// <summary>窗口/弹窗渐显 + 轻微上浮。</summary>
    public static void FadeIn(Form form, int durationMs = 240, int drift = 12)
    {
        var target = form.Location;
        form.Opacity = 0;
        form.Location = new Point(target.X, target.Y + drift);
        var sw = Stopwatch.StartNew();
        var timer = new System.Windows.Forms.Timer { Interval = 15 };
        timer.Tick += (_, _) =>
        {
            double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
            double ease = 1 - Math.Pow(1 - t, 3);
            form.Opacity = ease;
            form.Location = new Point(target.X, target.Y + (int)(drift * (1 - ease)));
            if (t >= 1)
            {
                form.Opacity = 1;
                timer.Stop();
                timer.Dispose();
            }
        };
        timer.Start();
    }

    private static Bitmap Capture(Control page, Control host)
    {
        var bmp = new Bitmap(host.ClientSize.Width, host.ClientSize.Height);
        page.DrawToBitmap(bmp, new Rectangle(Point.Empty, host.ClientSize));
        return bmp;
    }

    private static void DrawWithAlpha(Graphics g, Bitmap bmp, int shiftX, float alpha)
    {
        alpha = Math.Clamp(alpha, 0f, 1f);
        using var ia = new ImageAttributes();
        ia.SetColorMatrix(new ColorMatrix { Matrix33 = alpha });
        var dest = new Rectangle(shiftX, 0, bmp.Width, bmp.Height);
        g.DrawImage(bmp, dest, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, ia);
    }
}
