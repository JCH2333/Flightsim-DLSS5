using System.Diagnostics;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 丝滑动效（Apple 风格：ease-out cubic 缓动）。
/// - AnimatePageSwitch：页面切换 = 旧页滑出 + 新页整页推入（不透明位图平移，帧率稳定）
/// - FadeIn：顶层窗口/弹窗透明度渐显 + 轻微上浮
/// </summary>
public static class Fx
{
    private static Panel? _curtain;
    private static System.Windows.Forms.Timer? _timer;
    private static Bitmap? _oldBmp;

    private sealed class BufferPanel : Panel
    {
        public BufferPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
        }
    }

    /// <summary>
    /// 页面切换动画（iOS push 风格）：新页保持“活的”真页面置于底层，一张旧页截图幕布从其上滑走
    /// 将新页逐步露出——露出的像素全程是真实控件（ClearType 文字、实时状态），不存在位图/真机
    /// 切换造成的闪烁；幕布为不透明位图平移，帧率稳定。260ms ease-out cubic。
    /// </summary>
    /// <param name=”host”>承载页面的容器</param>
    /// <param name=”oldPage”>当前可见页</param>
    /// <param name=”newPage”>目标页</param>
    /// <param name=”dir”>1 = 幕布向左滑出（新页自右揭露）；-1 = 向右</param>
    public static void AnimatePageSwitch(Control host, Control? oldPage, Control newPage, int dir)
    {
        Finish();
        newPage.BringToFront();   // 新页先就位（位于幕布之下，逐步揭露）
        if (oldPage == null || ReferenceEquals(oldPage, newPage) || host.Width < 50) return;

        var oldBmp = Capture(oldPage, host);
        var curtain = new BufferPanel
        {
            Location = Point.Empty,
            Size = host.ClientSize,
            BackColor = Theme.Bg,
        };
        curtain.Paint += (_, e) =>
        {
            var g = e.Graphics;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighSpeed;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
            g.DrawImageUnscaled(oldBmp, 0, 0);
        };
        host.Controls.Add(curtain);
        curtain.BringToFront();   // 幕布盖住新页：首帧 = 旧页截图，与用户所见无缝衔接

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
            double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
            double ease = 1 - Math.Pow(1 - t, 3);                       // ease-out cubic
            _curtain.Location = new Point((int)(-dir * ease * host.Width), 0);
        };
        _timer.Start();
        _curtain = curtain;
        _oldBmp = oldBmp;
    }

    /// <summary>立即结束当前动画（若有）。快速连点导航时保证状态一致。</summary>
    public static void Finish()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        if (_curtain != null)
        {
            var parent = _curtain.Parent;
            parent?.Controls.Remove(_curtain);
            _curtain.Dispose();
            _curtain = null;
        }
        _oldBmp?.Dispose(); _oldBmp = null;
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
