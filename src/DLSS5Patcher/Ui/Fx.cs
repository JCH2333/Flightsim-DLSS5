using System.Diagnostics;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 丝滑动效（Apple 风格：ease-out cubic 缓动）。
/// - AnimatePageSwitch：页面切换 = 旧页滑出 + 新页整页推入（不透明位图平移，帧率稳定）
/// - FadeIn：顶层窗口/弹窗透明度渐显 + 轻微上浮
/// </summary>
public static class Fx
{
    private static Panel? _overlay;
    private static System.Windows.Forms.Timer? _timer;
    private static Bitmap? _oldBmp;
    private static Bitmap? _newBmp;

    private sealed class BufferPanel : Panel
    {
        public BufferPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    /// <summary>
    /// 页面切换动画（iOS push 风格）：旧页向反方向滑出、新页整页推入，ease-out cubic。
    /// 用不透明位图平移（无逐帧 alpha 混合）保证帧率；两页位图都在盖板出现前截好、
    /// 盖板先盖住旧页、真实新页再移到盖板之下——避免新页在动画开始前“闪现”造成撕裂感。
    /// </summary>
    /// <param name="host">承载页面的容器</param>
    /// <param name="oldPage">当前可见页</param>
    /// <param name="newPage">目标页</param>
    /// <param name="dir">1 = 新页从右侧推入；-1 = 从左侧</param>
    public static void AnimatePageSwitch(Control host, Control? oldPage, Control newPage, int dir)
    {
        Finish();
        if (oldPage == null || ReferenceEquals(oldPage, newPage) || host.Width < 50)
        {
            newPage.BringToFront();
            return;
        }

        // 1. 先截两页位图（此时屏幕视觉尚未变化，衔接无缝）
        var oldBmp = Capture(oldPage, host);
        var newBmp = Capture(newPage, host);

        // 2. 盖板首帧 = 旧页截图（与用户当前所见完全一致）；真实新页随后才移到盖板之下
        _overlay = new BufferPanel
        {
            Location = Point.Empty,
            Size = host.ClientSize,
            BackColor = Theme.Bg,
        };
        host.Controls.Add(_overlay);
        _overlay.BringToFront();
        newPage.BringToFront();
        _overlay.BringToFront();

        var sw = Stopwatch.StartNew();
        const int durationMs = 260;
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) =>
        {
            if (sw.ElapsedMilliseconds >= durationMs)
            {
                Finish();
                return;
            }
            _overlay?.Invalidate();
        };
        _overlay.Paint += (_, e) =>
        {
            double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
            double ease = 1 - Math.Pow(1 - t, 3);                       // ease-out cubic
            int shift = (int)(dir * ease * host.Width);                 // dir=+1: 旧页左退、新页右入

            var g = e.Graphics;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(oldBmp, -shift, 0);
            g.DrawImageUnscaled(newBmp, host.Width - shift, 0);
        };
        _timer.Start();
    }

    /// <summary>立即结束当前动画（若有）。快速连点导航时保证状态一致。</summary>
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
}
