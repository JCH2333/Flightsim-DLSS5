using System.Diagnostics;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 动效（对齐 GSX 管理端的 view 过渡：淡出/淡入 + 轻微上浮）。
/// - AnimatePageSwitch：页面切换 = 旧页截图以分层窗口淡出上浮，新页全程为真实控件
///   在幕下升入原位（合成器逐像素混色，无位图/真机切换，不会闪烁）
/// - FadeIn：顶层窗口/弹窗透明度渐显 + 轻微上浮
/// </summary>
public static class Fx
{
    private static FadeVeil? _veil;
    private static System.Windows.Forms.Timer? _timer;
    private static Bitmap? _oldBmp;
    private static Control? _risingPage;
    private static int _risePx;    // 新页起始下沉量（GSX enter: translateY(10px)）
    private static int _driftPx;   // 旧页上浮量（GSX leave: translateY(-6px)）

    /// <summary>
    /// 页面切换动画：GSX `view` 过渡（.view-leave-to: 淡出+上浮；.view-enter-from: 自下方淡入归位）。
    /// 旧页只截一张图交给顶层分层窗口做透明度渐隐（DWM 合成器混色，帧率稳定）；
    /// 新页自始至终是活的真页面，只是从下方 10px 升入——露出的像素永远是真实控件。
    /// </summary>
    public static void AnimatePageSwitch(Control host, Control? oldPage, Control newPage)
    {
        Finish();
        if (oldPage == null || ReferenceEquals(oldPage, newPage) || host.Width < 50)
        {
            newPage.Visible = true;
            newPage.BringToFront();
            return;
        }

        try
        {
            float dpi;
            using (var g = host.CreateGraphics()) dpi = g.DpiX / 96f;
            _risePx = (int)Math.Round(10 * dpi);
            _driftPx = (int)Math.Round(6 * dpi);

            var bmp = Capture(oldPage, host);   // 只截旧页子树（与新页 z 序无关）
            newPage.Visible = true;
            newPage.BringToFront();
            oldPage.Visible = false;            // 之后由幕布代替它；下次切回时 Visible 会被置回
            newPage.Location = new Point(0, _risePx);
            newPage.Location = new Point(0, _risePx);

            var veil = new FadeVeil(bmp)
            {
                Location = host.PointToScreen(Point.Empty),
                Size = host.ClientSize,
            };
            _veil = veil; _oldBmp = bmp; _risingPage = newPage;

            bool started = false;
            veil.Paint += (_, _) =>
            {
                if (started) return;   // 首帧绘制完成后才开始渐隐，避免出现未合成的空档
                started = true;
                BeginFade(veil, host);
            };
            veil.Show(host.FindForm());
            veil.Invalidate();
        }
        catch
        {
            // 动效失败不影响功能：直接呈现新页
            Finish();
        }
    }

    private static void BeginFade(FadeVeil veil, Control host)
    {
        var sw = Stopwatch.StartNew();
        const int durationMs = 200;   // GSX 为 170ms ease；单段合并版取 200ms 观感最接近
        _timer = new System.Windows.Forms.Timer { Interval = 15 };
        _timer.Tick += (_, _) =>
        {
            if (veil.IsDisposed || host.IsDisposed || host.FindForm()?.Visible != true) { Finish(); return; }
            double t = Math.Min(1.0, sw.ElapsedMilliseconds / (double)durationMs);
            double ease = 1 - Math.Pow(1 - t, 3);   // ease-out cubic
            veil.Opacity = (float)(1 - ease);
            veil.SetOffset((int)(ease * _driftPx));
            if (_risingPage != null) _risingPage.Location = new Point(0, (int)Math.Round(_risePx * (1 - ease)));
            if (t >= 1) Finish();
        };
        _timer.Start();
    }

    /// <summary>立即结束当前动画（若有）。快速连点导航时保证状态一致。</summary>
    public static void Finish()
    {
        _timer?.Stop();
        _timer?.Dispose();
        _timer = null;
        if (_veil != null)
        {
            var v = _veil; _veil = null;
            v.Close();
            v.Dispose();
        }
        _oldBmp?.Dispose(); _oldBmp = null;
        if (_risingPage != null)
        {
            _risingPage.Location = new Point(0, 0);
            _risingPage = null;
        }
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

    /// <summary>
    /// 淡出幕布：置顶于主窗内容区之上的无边框分层窗口，绘制旧页截图并整体渐隐。
    /// 不激活、不接收鼠标（点击穿透），从任务栏与 Alt+Tab 隐藏。
    /// </summary>
    private sealed class FadeVeil : Form
    {
        private readonly Bitmap _bmp;
        private int _offsetY;

        public FadeVeil(Bitmap bmp)
        {
            _bmp = bmp;
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            ShowIcon = false;
            MinimizeBox = MaximizeBox = false;
            Enabled = false;
            BackColor = Theme.Bg;
            Opacity = 0.999f;   // 直接进入分层窗口状态，避免中途切换窗口样式造成闪帧
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw, true);
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TRANSPARENT = 0x00000020;
                const int WS_EX_TOOLWINDOW = 0x00000080;
                var cp = base.CreateParams;
                cp.ExStyle |= WS_EX_NOACTIVATE | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        protected override bool ShowWithoutActivation => true;

        public void SetOffset(int y)
        {
            if (_offsetY == y) return;
            _offsetY = y;
            Invalidate();
            Update();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(Theme.Bg);
            g.DrawImageUnscaled(_bmp, 0, -_offsetY);
            base.OnPaint(e);
        }
    }
}
