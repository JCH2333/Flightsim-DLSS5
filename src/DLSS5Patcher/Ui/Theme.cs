using DLSS5Patcher.Core;
using System.Runtime.InteropServices;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 深色玻璃拟态设计令牌与控件工厂（对齐 GSX 汉化 2.0.0 视觉语言）。
/// 底色 #121310 + 三团环境光晕 + 白色低透明玻璃表面（GDI 无 backdrop-filter，用预混色还原）
/// + 12px 圆角卡片 + Bahnschrift 品牌字 + 绿色信号色。所有布局按 96 DPI 设计，主窗体启动时整体缩放。
/// </summary>
public static class Theme
{
    // ── 基底与表面（玻璃预混色） ──
    public static readonly Color Bg            = FromHex("#121310");
    public static readonly Color TitleBar      = FromHex("#181916");   // rgba(255,255,255,.025) 叠加底色
    public static readonly Color Surface       = FromHex("#1c1d1a");   // rgba(255,255,255,.042)
    public static readonly Color SurfaceRaised = FromHex("#212220");   // .065
    public static readonly Color SurfaceHover  = FromHex("#272826");   // .09
    public static readonly Color SurfaceInset  = FromHex("#151613");   // 编辑器 / 日志内衬
    public static readonly Color GlassBorder   = FromHex("#262724");   // rgba(255,255,255,.085)
    public static readonly Color Border        = FromHex("#232420");   // rgba(255,255,255,.07)
    public static readonly Color BorderStrong  = FromHex("#383936");   // .16
    public static readonly Color ChipBorder    = FromHex("#3b3c36");

    // ── 文本 ──
    public static readonly Color Text          = FromHex("#f2f4ed");
    public static readonly Color TextSecondary = FromHex("#b9bcae");
    public static readonly Color TextMuted     = FromHex("#82857a");

    // ── 信号色（绿） ──
    public static readonly Color Signal        = FromHex("#6adfae");
    public static readonly Color SignalStrong  = FromHex("#46cd96");
    public static readonly Color SignalBg      = FromHex("#1b2720");   // rgba(106,223,174,.10)
    public static readonly Color SignalBorder  = FromHex("#2e5443");   // rgba(106,223,174,.32)
    public static readonly Color PrimaryBg     = FromHex("#283a30");   // rgba(106,223,174,.15) 叠玻璃
    public static readonly Color PrimaryBorder = FromHex("#3d6e58");   // rgba(106,223,174,.42)
    public static readonly Color PrimaryText   = FromHex("#c2f6df");
    public static readonly Color PrimaryHover  = FromHex("#2e4a3c");
    public static readonly Color PrimaryHoverBorder = FromHex("#4b9173");

    // ── 语义色 ──
    public static readonly Color Warning       = FromHex("#e5b65c");
    public static readonly Color WarningBg     = FromHex("#2b261c");
    public static readonly Color Danger        = FromHex("#e9766c");
    public static readonly Color DangerHover   = FromHex("#c9473e");

    public const string FontUi = "Microsoft YaHei UI";
    public const string FontBrand = "Bahnschrift";

    public const int RadiusCard = 12;
    public const int RadiusNav = 11;
    public const int RadiusButton = 10;

    public static Color FromHex(string hex)
    {
        var h = hex.TrimStart('#');
        return Color.FromArgb(Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
    }

    // ───────────────────────────── 环境光晕 ─────────────────────────────

    /// <summary>绘制 GSX 2.0 同款三团径向环境光（绿 / 蓝 / 琥珀）。</summary>
    public static void PaintAmbient(Graphics g, Rectangle r)
    {
        DrawGlow(g, r, 0.10f, -0.08f, 620, 460, FromHex("#6adfae"), 0.12f);
        DrawGlow(g, r, 1.06f, 1.14f, 760, 540, FromHex("#5888eb"), 0.07f);
        DrawGlow(g, r, 0.92f, -0.20f, 860, 640, FromHex("#e5b65c"), 0.05f);
    }

    private static void DrawGlow(Graphics g, Rectangle r, float cx, float cy, int w, int h, Color c, float alpha)
    {
        var center = new PointF(r.X + r.Width * cx, r.Y + r.Height * cy);
        using var path = new System.Drawing.Drawing2D.GraphicsPath();
        path.AddEllipse(center.X - w / 2f, center.Y - h / 2f, w, h);
        using var brush = new System.Drawing.Drawing2D.PathGradientBrush(path);
        brush.CenterColor = Color.FromArgb((int)(255 * alpha), c);
        brush.SurroundColors = new[] { Color.FromArgb(0, c) };
        brush.FocusScales = new PointF(0.55f, 0.55f);
        g.FillPath(brush, path);
    }

    /// <summary>圆角路径。</summary>
    public static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int radius)
    {
        var d = radius * 2;
        var path = new System.Drawing.Drawing2D.GraphicsPath();
        if (d <= 0 || r.Width < d || r.Height < d)
        {
            path.AddRectangle(r);
            return path;
        }
        path.AddArc(r.X, r.Y, d, d, 180, 90);
        path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        path.CloseFigure();
        return path;
    }

    // ───────────────────────────── 环境底板 ─────────────────────────────

    /// <summary>带环境光晕的页面底板。</summary>
    public class AmbientPage : UserControl
    {
        public AmbientPage()
        {
            BackColor = Bg;
            DoubleBuffered = true;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Bg);
            PaintAmbient(e.Graphics, ClientRectangle);
        }
    }

    /// <summary>玻璃卡片：圆角填充 + 1px 玻璃描边（填充画在背景擦除阶段，子控件透明底可正确回放）。</summary>
    public sealed class GlassCard : Panel
    {
        public int Radius { get; set; } = RadiusCard;
        public Color Fill { get; set; } = Surface;
        public Color BorderColor { get; set; } = GlassBorder;

        public GlassCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var path = RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius);
            using var fill = new SolidBrush(Fill);
            e.Graphics.FillPath(fill, path);
            using var pen = new Pen(BorderColor);
            e.Graphics.DrawPath(pen, path);
        }

        protected override void OnPaint(PaintEventArgs e) { }
    }

    // ───────────────────────────── 按钮 ─────────────────────────────

    /// <summary>玻璃按钮（圆角 10 / 高 40 / 悬停微浮），primary 为绿色信号样式。</summary>
    public sealed class GlassButton : Control
    {
        private bool _hover;
        private bool _down;

        public bool Primary { get; set; }

        public GlassButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font(FontUi, 9f, FontStyle.Bold);
            Height = 40;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPath(rect, RadiusButton);

            Color fill, border, text;
            if (Primary)
            {
                fill = !_hover ? PrimaryBg : PrimaryHover;
                border = !_hover ? PrimaryBorder : PrimaryHoverBorder;
                text = PrimaryText;
            }
            else
            {
                fill = _hover ? SurfaceHover : Surface;
                border = _hover ? BorderStrong : GlassBorder;
                text = _hover ? Theme.Text : TextSecondary;
            }
            if (!Enabled)
            {
                fill = Color.FromArgb(120, fill);
                border = Color.FromArgb(120, border);
                text = Color.FromArgb(110, text);
            }
            else if (_down)
            {
                fill = _hover ? ControlPaint.Dark(fill, 0.06f) : fill;
            }

            using (var b = new SolidBrush(fill)) e.Graphics.FillPath(b, path);
            using (var p = new Pen(border)) e.Graphics.DrawPath(p, path);

            TextRenderer.DrawText(e.Graphics, Text, Font, rect, text,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    public static GlassButton MakeButton(string text, bool primary = false, int height = 40)
    {
        return new GlassButton { Text = text, Primary = primary, Height = height };
    }

    /// <summary>侧栏导航按钮（高 44 / 圆角 11 / 激活为绿色信号态）。</summary>
    public sealed class NavButton : Control
    {
        private bool _hover;
        public bool Active { get; set; }

        public NavButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font(FontUi, 9.75f, FontStyle.Bold);
            Height = 44;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var rect = new Rectangle(0, 0, Width - 1, Height - 1);
            using var path = RoundedPath(rect, RadiusNav);

            Color fill, border, text;
            if (Active)
            {
                fill = _hover ? FromHex("#20302a") : SignalBg;
                border = SignalBorder;
                text = Signal;
            }
            else if (_hover)
            {
                fill = Surface;
                border = GlassBorder;
                text = Theme.Text;
            }
            else
            {
                fill = Color.Transparent;
                border = Color.Transparent;
                text = TextSecondary;
            }
            if (fill != Color.Transparent)
            {
                using var b = new SolidBrush(fill);
                e.Graphics.FillPath(b, path);
            }
            if (border != Color.Transparent)
            {
                using var p = new Pen(border);
                e.Graphics.DrawPath(p, path);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(rect.X + 13, rect.Y, rect.Width - 13, rect.Height),
                text, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    // ───────────────────────────── 标签 / 徽章 / 页头 ─────────────────────────────

    public static Label MakeLabel(string text, Color? color = null, float size = 9f, bool bold = false, string? fontFamily = null)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = color ?? TextSecondary,
            Font = new Font(fontFamily ?? FontUi, size, bold ? FontStyle.Bold : FontStyle.Regular),
            BackColor = Color.Transparent,
        };
    }

    /// <summary>绿色小眉题（Bahnschrift）。</summary>
    public static Label MakeEyebrow(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = Signal,
            Font = new Font(FontBrand, 9.75f, FontStyle.Bold),
            BackColor = Color.Transparent,
        };
    }

    /// <summary>状态徽章（success / warning / muted）。</summary>
    public static Label MakeBadge(string text, string tone = "muted")
    {
        var (bg, fg) = tone switch
        {
            "success" => (SignalBg, Signal),
            "warning" => (WarningBg, Warning),
            _ => (FromHex("#353630"), TextMuted),
        };
        return new Label
        {
            Text = text,
            AutoSize = true,
            BackColor = bg,
            ForeColor = fg,
            Font = new Font(FontUi, 8f),
            Padding = new Padding(6, 3, 6, 3),
        };
    }

    /// <summary>元信息小片（细边框）。</summary>
    public static Label MakeChip(string text)
    {
        var lbl = new Label
        {
            Text = text,
            AutoSize = true,
            AutoEllipsis = true,
            BackColor = Color.Transparent,
            ForeColor = TextMuted,
            Font = new Font(FontUi, 8f),
            Padding = new Padding(6, 3, 6, 3),
        };
        lbl.Paint += (_, e) =>
        {
            using var pen = new Pen(ChipBorder);
            e.Graphics.DrawRectangle(pen, 0, 0, lbl.Width - 1, lbl.Height - 1);
        };
        return lbl;
    }

    /// <summary>页头：绿色眉题 + 大标题（GSX view-header）。</summary>
    public static Panel MakePageHeader(string eyebrow, string title, string? titleSuffix = null)
    {
        var host = new Panel { Location = new Point(36, 30), Size = new Size(790, 56), BackColor = Color.Transparent };
        var eb = MakeEyebrow(eyebrow);
        eb.Location = new Point(1, 0);
        var h1 = MakeLabel(title, Text, 18f, bold: true);
        h1.Location = new Point(0, 18);
        host.Controls.Add(eb);
        host.Controls.Add(h1);
        if (titleSuffix != null)
        {
            var suf = MakeLabel(titleSuffix, TextMuted, 10f);
            suf.Location = new Point(h1.Right + 10, 30);
            host.Controls.Add(suf);
        }
        return host;
    }

    // ───────────────────────────── DWM（系统圆角 + 深色） ─────────────────────────────

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Win11 圆角窗口 + 深色标题栏属性（Win10 静默降级）。</summary>
    public static void ApplyWindowChrome(IWin32Window window)
    {
        try
        {
            var handle = window.Handle;
            var on = 1;
            DwmSetWindowAttribute(handle, 20, ref on, 4);      // DWMWA_USE_IMMERSIVE_DARK_MODE
            var round = 2;                                     // DWMWCP_ROUND
            DwmSetWindowAttribute(handle, 33, ref round, 4);   // DWMWA_WINDOW_CORNER_PREFERENCE
        }
        catch { /* Win10 无圆角，忽略 */ }
    }

    /// <summary>玻璃复选框（自绘，替代系统白色勾选框）：绿底白勾选中态。</summary>
    public sealed class GlassCheck : Control
    {
        private bool _checked;
        private bool _hover;

        public bool Checked
        {
            get => _checked;
            set { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        public event EventHandler? CheckedChanged;

        public GlassCheck()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font(FontUi, 9.5f);
            Size = new Size(120, 24);   // 自绘控件不用 AutoSize（preferred size 为 0 会消失），由调用方按文本设宽
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e)
        {
            Checked = !Checked;
            base.OnClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var box = new Rectangle(0, Height / 2 - 9, 18, 18);
            using (var path = RoundedPath(box, 5))
            {
                using var fill = new SolidBrush(Checked ? SignalBg : _hover ? SurfaceHover : SurfaceRaised);
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(Checked ? SignalBorder : _hover ? BorderStrong : Theme.FromHex("#4a4b44"));
                e.Graphics.DrawPath(pen, path);
            }
            if (Checked)
            {
                using var pen = new Pen(Signal, 2f);
                pen.StartCap = System.Drawing.Drawing2D.LineCap.Round;
                pen.EndCap = System.Drawing.Drawing2D.LineCap.Round;
                e.Graphics.DrawLine(pen, box.X + 4, box.Y + 9, box.X + 8, box.Y + 13);
                e.Graphics.DrawLine(pen, box.X + 8, box.Y + 13, box.X + 14, box.Y + 5);
            }
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(26, 0, Width - 26, Height),
                ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
        }
    }

    /// <summary>
    /// 拨动开关（iOS 风格轨道 + 圆形滑块）：开 = 信号绿，关 = 深底灰边。
    /// 文字绘制在轨道左侧；点击整块切换。
    /// </summary>
    public sealed class GlassSwitch : Control
    {
        private bool _checked;
        private bool _hover;

        public bool Checked
        {
            get => _checked;
            set { _checked = value; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); }
        }

        /// <summary>编程式设置（不触发 CheckedChanged，供刷新状态用）。</summary>
        public void SetCheckedSilent(bool value)
        {
            if (_checked == value) return;
            _checked = value;
            Invalidate();
        }

        public event EventHandler? CheckedChanged;

        public GlassSwitch()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            Font = new Font(FontUi, 9f);
            Size = new Size(120, 22);
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { _checked = !_checked; Invalidate(); CheckedChanged?.Invoke(this, EventArgs.Empty); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 文字（轨道左侧）
            var textW = string.IsNullOrEmpty(Text) ? 0 : TextRenderer.MeasureText(Text, Font).Width + 6;
            if (textW > 0)
                TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(0, 0, textW, Height),
                    Checked ? Signal : TextSecondary,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);

            // 轨道
            int tw = 40, th = 20;
            var track = new Rectangle(Width - tw, Height / 2 - th / 2, tw, th);
            using (var path = RoundedPath(track, th / 2))
            {
                using var fill = new SolidBrush(Checked ? SignalBg : _hover ? SurfaceHover : Surface);
                e.Graphics.FillPath(fill, path);
                using var pen = new Pen(Checked ? SignalBorder : _hover ? BorderStrong : FromHex("#4a4b44"));
                e.Graphics.DrawPath(pen, path);
            }

            // 滑块
            int d = th - 6;
            var thumb = new Rectangle(Checked ? track.Right - d - 3 : track.X + 3, Height / 2 - d / 2, d, d);
            using (var path = RoundedPath(thumb, d / 2))
            {
                using var fill = new SolidBrush(Checked ? Signal : FromHex("#82857a"));
                e.Graphics.FillPath(fill, path);
            }
        }
    }

    /// <summary>下拉框黑绿化：扁平 + 深底 + 自绘项（选中项绿色信号底）。</summary>
    public static void StyleCombo(ComboBox cb)
    {
        cb.FlatStyle = FlatStyle.Flat;
        cb.ForeColor = Text;
        cb.BackColor = SurfaceRaised;
        cb.DrawMode = DrawMode.OwnerDrawFixed;
        cb.DrawItem += (_, e) =>
        {
            if (e.Index < 0) return;
            var selected = (e.State & DrawItemState.Selected) != 0;
            var hovered = (e.State & DrawItemState.HotLight) != 0;
            using (var b = new SolidBrush(selected && !hovered ? SignalBg : hovered ? SurfaceHover : SurfaceRaised))
                e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, cb.Items[e.Index].ToString() ?? "", e.Font, e.Bounds,
                selected && !hovered ? Signal : Text,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        };
    }

    /// <summary>
    /// 自绘滚动条（贴在 RichTextBox 右缘的细轨 + 可拖动滑块，替代系统白色滚动条视觉）。
    /// RichTextBox 自身需设 ScrollBars.None；滚轮由 WheelRouter 全局路由（悬停即滚，无需焦点）。
    /// 定位用 EM_GETFIRSTVISIBLELINE/EM_GETLINECOUNT（行单位）——GetScrollInfo 在
    /// ScrollBars.None 的 RichEdit 上返回的数值不可靠，且 EM_LINESCROLL 不触发 VScroll 事件，
    /// 所以每次滚动后都显式调用 UpdateThumb。
    /// </summary>
    /// <summary>滚轮路由的滚动目标：正数向下滚（ScrollIndicator 按行、页面级滚动条按像素）。
    /// 返回 true 表示确实滚动了（消息被消费）；false 时路由继续尝试下一个目标。</summary>
    public interface IWheelScrollTarget
    {
        bool ScrollLines(int lines);
    }

    public sealed class ScrollIndicator : Panel, IWheelScrollTarget
    {
        private readonly RichTextBox _rtb;
        private readonly Panel _fill = new();
        private bool _dragging;
        private bool _userScrolled;
        private float _lineHeight;   // 惰性实测：必须与 ClientSize 同为物理像素，否则 AtBottom 误判

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int EM_LINESCROLL = 0x00B6;
        private const int EM_GETLINECOUNT = 0x00BA;
        private const int EM_GETFIRSTVISIBLELINE = 0x00CE;

        public ScrollIndicator(RichTextBox rtb)
        {
            _rtb = rtb;
            Size = new Size(12, rtb.Height);
            BackColor = Color.Transparent;
            Cursor = Cursors.Hand;
            SetStyle(ControlStyles.SupportsTransparentBackColor | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);

            _fill.BackColor = Signal;
            _fill.Size = new Size(4, 12);
            _fill.Visible = false;
            Controls.Add(_fill);

            rtb.TextChanged += (_, _) => UpdateThumb();
            rtb.VScroll += (_, _) => UpdateThumb();
            rtb.Resize += (_, _) => UpdateThumb();
            rtb.FontChanged += (_, _) => { _lineHeight = 0; UpdateThumb(); };
            // 注意：用 rtb 的 BeginInvoke（此时 ScrollIndicator 自身句柄还未创建，对它调用会抛异常）
            rtb.HandleCreated += (_, _) => rtb.BeginInvoke(new Action(UpdateThumb));

            MouseDown += (_, e) => { if (e.Button == MouseButtons.Left) { _dragging = true; DragTo(e.Y); } };
            MouseMove += (_, e) => { if (_dragging) DragTo(e.Y); };
            MouseUp += (_, e) => { if (e.Button == MouseButtons.Left) _dragging = false; };
            MouseLeave += (_, _) => { _dragging = false; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            // 4px 细轨画在条带最左侧，其余为透明命中区（方便拖拽）
            using var track = new SolidBrush(FromHex("#353630"));
            e.Graphics.FillRectangle(track, 0, 0, 4, Height);
            base.OnPaint(e);
        }

        private int LineCount => _rtb.IsHandleCreated ? SendMessage(_rtb.Handle, EM_GETLINECOUNT, IntPtr.Zero, IntPtr.Zero).ToInt32() : 0;

        private int FirstVisible => _rtb.IsHandleCreated ? SendMessage(_rtb.Handle, EM_GETFIRSTVISIBLELINE, IntPtr.Zero, IntPtr.Zero).ToInt32() : 0;

        private int VisibleLines
        {
            get
            {
                if (_lineHeight <= 0)
                {
                    // 用 rtb 自己的 Graphics 实测行高（与 ClientSize 同为物理像素，DPI 一致）
                    using var g = _rtb.CreateGraphics();
                    _lineHeight = _rtb.Font.GetHeight(g) + 2f;
                }
                return Math.Max(1, (int)(_rtb.ClientSize.Height / _lineHeight));
            }
        }

        /// <summary>滚轮路由调用：按行滚动并立即刷新滑块。返回是否真的滚动了。</summary>
        public bool ScrollLines(int lines)
        {
            if (!_rtb.IsHandleCreated || lines == 0 || LineCount <= VisibleLines) return false;
            SendMessage(_rtb.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)lines);
            _userScrolled = true;
            UpdateThumb();
            return true;
        }

        private void DragTo(int y)
        {
            int total = LineCount;
            int scrollable = total - VisibleLines;
            if (scrollable <= 0) return;
            int thumbH = ThumbHeight(total);
            int rail = Height - thumbH;
            if (rail <= 0) return;
            double ratio = Math.Clamp((y - thumbH / 2.0) / rail, 0.0, 1.0);
            int delta = (int)Math.Round(ratio * scrollable) - FirstVisible;
            if (delta == 0) return;
            SendMessage(_rtb.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)delta);
            _userScrolled = true;
            UpdateThumb();
        }

        private int ThumbHeight(int total)
        {
            int visible = Math.Min(total, VisibleLines);
            return Math.Max(24, (int)(Height * (float)visible / Math.Max(1, total)));
        }

        /// <summary>内容是否已滚动到末尾（内容不足一屏时视为 true）。协议弹窗用它判定"已完整阅读"。</summary>
        public bool AtBottom { get; private set; }

        /// <summary>自最近一次 ResetReadState 后用户是否真的滚动过（滚轮或拖动）。用于协议强制阅读。</summary>
        public bool HasUserScrolled => _userScrolled;

        /// <summary>滚动到末尾时触发。</summary>
        public event Action? ReachedBottom;

        private void UpdateThumb()
        {
            if (!_rtb.IsHandleCreated) return;
            int total = LineCount;
            int scrollable = total - VisibleLines;
            if (scrollable <= 0)
            {
                _fill.Visible = false;
                if (!AtBottom) { AtBottom = true; ReachedBottom?.Invoke(); }
                return;
            }
            int thumbH = ThumbHeight(total);
            int top = (int)((Height - thumbH) * ((float)Math.Min(FirstVisible, scrollable) / scrollable));
            _fill.Visible = true;
            _fill.SetBounds(0, Math.Clamp(top, 0, Height - thumbH), 4, thumbH);

            bool atBottom = FirstVisible >= scrollable;
            if (atBottom && !AtBottom) ReachedBottom?.Invoke();
            AtBottom = atBottom;
        }

        /// <summary>重置已读状态（切换文档内容时调用）。</summary>
        public void ResetReadState()
        {
            AtBottom = false;
            _userScrolled = false;
        }
    }

    /// <summary>
    /// 全局滚轮路由：鼠标悬停在已注册宿主区域内时，滚轮直接滚动其 RichTextBox——
    /// 不需要先点击取得焦点（TextBoxBase 没焦点时收不到 WM_MOUSEWHEEL，这是教程页滚不动的原因）。
    /// 在主窗体构造时调用 Install() 安装一次。
    /// </summary>
    public sealed class WheelRouter : IMessageFilter
    {
        public static readonly WheelRouter Default = new();
        private static bool _installed;

        private readonly List<(Control Host, IWheelScrollTarget Target)> _pairs = new();
        private WheelRouter() { }

        public static void Install()
        {
            if (_installed) return;
            Application.AddMessageFilter(Default);
            _installed = true;
        }

        public void Register(Control host, IWheelScrollTarget target) => _pairs.Add((host, target));

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL) return false;

            // 有模态弹窗时（ActiveForm=弹窗），跳过被遮挡页面上的宿主，
            // 否则弹窗内容区收不到滚轮（会被背后页面的宿主吞掉）。
            var active = Form.ActiveForm;

            var pos = Cursor.Position;
            foreach (var (host, target) in _pairs)
            {
                if (!host.Visible || !host.IsHandleCreated) continue;
                if (active != null && host.FindForm() != active) continue;
                if (!host.RectangleToScreen(host.ClientRectangle).Contains(pos)) continue;
                // 各页面靠 z 序叠放（都 Visible），必须确认宿主在光标处确实处于最上层，
                // 否则滚轮会被背后页面的宿主抢先吞掉（表现为目标页面滚不动）。
                if (!IsTopmostAt(host, pos)) continue;

                int raw = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);   // 滚轮刻度（有符号短整型）
                if (target.ScrollLines(-Math.Sign(raw) * 5))              // 上滚为正 → 向上滚
                    return true;   // 消费滚轮消息，避免它落到焦点控件上
                // 该目标已到边界/不可滚 → 继续尝试下一个目标（如页面级滚动）
            }
            return false;
        }

        /// <summary>宿主在光标处是否未被任何更高 z 序的兄弟子树遮挡（逐层向上检查，直到窗体顶层）。</summary>
        private static bool IsTopmostAt(Control host, Point screen)
        {
            Control node = host;
            while (node.Parent != null)
            {
                var parent = node.Parent;
                if (!node.Visible) return false;
                if (!node.RectangleToScreen(node.ClientRectangle).Contains(screen)) return false;
                foreach (Control sib in parent.Controls)   // Controls 集合按 z 序：索引 0 在最上
                {
                    if (ReferenceEquals(sib, node)) break;   // 本层通过；继续向上验证父级链
                    if (sib.Visible && sib.RectangleToScreen(sib.ClientRectangle).Contains(screen)) return false;
                }
                node = parent;
            }
            return true;
        }
    }

    /// <summary>
    /// 96-DPI 设计坐标窗体基类（PerMonitorV2）。
    /// 几何缩放策略：
    ///   • 构造期只按 96-DPI 设计像素搭建，句柄随 Show/ShowDialog 在目标显示器上创建；
    ///   • OnLoad 用 GetDpiForWindow 取窗口真实 DPI 做一次性整体缩放并重新居中
    ///     （取代旧 CreateGraphics 方案——GDI+ 的 DpiX 在多屏下会返回主屏 DPI，
    ///      窗口落在低 DPI 副屏时会按主屏比例被放大）；
    ///   • 之后的跨屏拖动/系统缩放变更（WM_DPICHANGED）由 OnDpiChanged 接管：
    ///     框架对 AutoScaleMode=None 的窗口不缩放子控件几何，这里自行等比缩放并一次成型。
    /// 字体不要自己动：GDI 字体的像素换算固定按主屏 DPI（进程启动时的主显示器），
    /// 框架在 DPI 变化时把字号 pt 按 newDpi/oldDpi 缩放正是为此做的正确补偿
    /// （例如 9.75pt 在 100% 副屏上会调成 4.875pt，渲染出来才是设计上的 13px）。
    /// 任何"恢复字号"的操作都会让文字在非主屏 DPI 下翻倍/减半。
    /// </summary>
    public abstract class DpiScaledForm : Form
    {
        private readonly int _designW;
        private readonly int _designH;
        private bool _laid;

        protected DpiScaledForm(int designW, int designH)
        {
            _designW = designW;
            _designH = designH;
            AutoScaleMode = AutoScaleMode.None;   // 布局缩放全部由本类接管，禁用框架自动缩放
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            int dpi = GetDpiForWindow(Handle);
            float factor = dpi / 96f;
            if (factor > 1.001f) Scale(new SizeF(factor, factor));
            ClientSize = new Size(_designW * dpi / 96, _designH * dpi / 96);

            // CenterScreen/CenterParent 是按设计尺寸居中的，缩放后重新对齐；
            // 工作区居中同时避免窗口下缘压进任务栏
            if (StartPosition == FormStartPosition.CenterScreen) CenterIn(null);
            else if (StartPosition == FormStartPosition.CenterParent && Owner != null) CenterIn(Owner);

            _laid = true;
        }

        protected override void OnDpiChanged(DpiChangedEventArgs e)
        {
            e.Cancel = true;        // 不采用框架建议矩形：几何在下面一次成型
            base.OnDpiChanged(e);   // 框架完成字体 pt 的 DPI 补偿（对 GDI 按主屏换算的修正）
            if (!_laid) return;     // 句柄创建期的 DPI 切换：几何仍是设计像素，留给 OnLoad 统一缩放

            // 冻结整树绘制：几何重排与其后字体补偿波次产生的全部中间态都不上屏。
            // 上一轮"更闪"的原因：逐控件 Scale → 190 个控件的中间状态逐个重绘上屏；
            // 冻结后只在一切落定时一次性重绘。
            SetRedrawDeep(this, false);
            try
            {
                float f = (float)e.DeviceDpiNew / e.DeviceDpiOld;
                SuspendLayout();
                try
                {
                    if (Math.Abs(f - 1f) > 0.001f) Scale(new SizeF(f, f));
                    Bounds = new Rectangle(e.SuggestedRectangle.Location,   // 位置沿用系统建议（拖动中跟随光标）
                        new Size(_designW * e.DeviceDpiNew / 96, _designH * e.DeviceDpiNew / 96));
                }
                finally { ResumeLayout(true); }
            }
            catch
            {
                Unfreeze();   // 兜底：任何异常都不能把窗口留在冻结状态
                throw;
            }
            // 字体补偿的 AFTER_PARENT 波次以消息形式排在本处理之后，解冻任务在其后执行：
            // 几何 + 字体全部落定后才做唯一一次完整重绘
            _ = BeginInvoke(new Action(Unfreeze));
        }

        private void Unfreeze()
        {
            if (IsDisposed || !IsHandleCreated) return;
            SetRedrawDeep(this, true);
            RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero,
                RDW_ERASE | RDW_FRAME | RDW_INVALIDATE | RDW_ALLCHILDREN | RDW_UPDATENOW);
        }

        private static void SetRedrawDeep(Control root, bool on)
        {
            if (root.IsHandleCreated)
                SendMessage(root.Handle, WM_SETREDRAW, (IntPtr)(on ? 1 : 0), IntPtr.Zero);
            foreach (Control child in root.Controls) SetRedrawDeep(child, on);
        }

        private void CenterIn(Control? anchor)
        {
            Rectangle area;
            Point center;
            if (anchor != null)
            {
                area = Screen.FromControl(anchor).WorkingArea;
                center = new Point(anchor.Left + anchor.Width / 2, anchor.Top + anchor.Height / 2);
            }
            else
            {
                area = Screen.FromControl(this).WorkingArea;
                center = new Point(area.Left + area.Width / 2, area.Top + area.Height / 2);
            }
            int x = Math.Clamp(center.X - Width / 2, area.Left, Math.Max(area.Left, area.Right - Width));
            int y = Math.Clamp(center.Y - Height / 2, area.Top, Math.Max(area.Top, area.Bottom - Height));
            Location = new Point(x, y);
        }

        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool RedrawWindow(IntPtr hWnd, IntPtr rectUpdate, IntPtr hrgnUpdate, uint flags);

        private const int WM_SETREDRAW = 0x000B;
        private const uint RDW_ERASE = 0x4, RDW_FRAME = 0x400, RDW_INVALIDATE = 0x1,
                           RDW_ALLCHILDREN = 0x80, RDW_UPDATENOW = 0x100;
    }

    /// <summary>
    /// 跨显示器拖动窗口（PerMonitorV2 DPI 变化）时，RichEdit 会丢失全部逐字符颜色——
    /// 教程/公告文字因此变成黑色。本控件在内容构建完成后调用 SaveSnapshot() 留一份干净的 RTF，
    /// 之后每逢 DPI/字体变化（事件后延迟执行）把快照原样还原。RTF 字号是逻辑磅值，新 DPI 下渲染依旧正确。
    /// </summary>
    public sealed class DpiSafeRichTextBox : RichTextBox
    {
        private string? _snapshot;
        private bool _restoring;

        /// <summary>内容排版完成后调用：以当前内容作为"标准版本"，DPI 变化丢失颜色时用它还原。</summary>
        public void SaveSnapshot()
        {
            try { if (TextLength > 0) _snapshot = Rtf; } catch { }
        }

        private void RestoreSnapshot()
        {
            if (_restoring || _snapshot == null || !IsHandleCreated) return;
            _restoring = true;
            try
            {
                if (Rtf != _snapshot)
                {
                    Rtf = _snapshot;
                    SelectionStart = 0;
                    SelectionLength = 0;
                }
            }
            catch { }
            finally { _restoring = false; }
        }

        private void RestoreLater()
        {
            if (_snapshot == null || !IsHandleCreated) return;
            _ = BeginInvoke(new Action(RestoreSnapshot));   // 等 WinForms 的缩放/重排全部落定后再还原
        }

        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            RestoreLater();
        }

        protected override void OnDpiChangedAfterParent(EventArgs e)
        {
            base.OnDpiChangedAfterParent(e);
            RestoreLater();
        }
    }

    /// <summary>给多行 RichTextBox 挂上细滚动条（置于指定宿主的右上内侧），并注册滚轮路由。
    /// 构造期调用传默认 dpiScale=1（坐标随后由窗体整体缩放）；OnShown/运行时等
    /// 宿主已缩放后的调用须传 dpiScale = host.DeviceDpi / 96f。</summary>
    public static ScrollIndicator AttachScrollIndicator(RichTextBox rtb, Control host, int rightInset, int topInset, int height, float dpiScale = 1f)
    {
        var indicator = new ScrollIndicator(rtb)
        {
            Size = new Size((int)(12 * dpiScale), (int)(height * dpiScale)),
            Location = new Point(host.ClientSize.Width - (int)(rightInset * dpiScale) - (int)(12 * dpiScale), (int)(topInset * dpiScale)),
        };
        host.Controls.Add(indicator);
        indicator.BringToFront();
        WheelRouter.Default.Register(host, indicator);
        return indicator;
    }

    // ───────────────────────────── 兼容旧 API ─────────────────────────────

    public static GlassCard MakeCard(int w, int h)
    {
        return new GlassCard { Size = new Size(w, h) };
    }

    public static void EnableBorder(Control c, Color color)
    {
        c.Paint += (_, e) =>
        {
            using var pen = new Pen(color);
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        };
    }
}
