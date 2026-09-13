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
    public sealed class ScrollIndicator : Panel
    {
        private readonly RichTextBox _rtb;
        private readonly Panel _fill = new();
        private bool _dragging;
        private float _lineHeight = 19f;

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
            rtb.FontChanged += (_, _) => { _lineHeight = rtb.Font.Height + 2f; UpdateThumb(); };
            // 注意：用 rtb 的 BeginInvoke（此时 ScrollIndicator 自身句柄还未创建，对它调用会抛异常）
            rtb.HandleCreated += (_, _) => rtb.BeginInvoke(new Action(() => { _lineHeight = rtb.Font.Height + 2f; UpdateThumb(); }));

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

        private int VisibleLines => Math.Max(1, (int)(_rtb.ClientSize.Height / _lineHeight));

        /// <summary>滚轮路由调用：按行滚动并立即刷新滑块。</summary>
        public void ScrollLines(int lines)
        {
            if (!_rtb.IsHandleCreated || lines == 0 || LineCount <= VisibleLines) return;
            SendMessage(_rtb.Handle, EM_LINESCROLL, IntPtr.Zero, (IntPtr)lines);
            UpdateThumb();
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
            UpdateThumb();
        }

        private int ThumbHeight(int total)
        {
            int visible = Math.Min(total, VisibleLines);
            return Math.Max(24, (int)(Height * (float)visible / Math.Max(1, total)));
        }

        private void UpdateThumb()
        {
            if (!_rtb.IsHandleCreated) return;
            int total = LineCount;
            int scrollable = total - VisibleLines;
            if (scrollable <= 0) { _fill.Visible = false; return; }
            int thumbH = ThumbHeight(total);
            int top = (int)((Height - thumbH) * ((float)Math.Min(FirstVisible, scrollable) / scrollable));
            _fill.Visible = true;
            _fill.SetBounds(0, Math.Clamp(top, 0, Height - thumbH), 4, thumbH);
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

        private readonly List<(Control Host, ScrollIndicator Bar)> _pairs = new();
        private WheelRouter() { }

        public static void Install()
        {
            if (_installed) return;
            Application.AddMessageFilter(Default);
            _installed = true;
        }

        public void Register(Control host, ScrollIndicator bar) => _pairs.Add((host, bar));

        public bool PreFilterMessage(ref Message m)
        {
            const int WM_MOUSEWHEEL = 0x020A;
            if (m.Msg != WM_MOUSEWHEEL) return false;

            // 有模态弹窗时（ActiveForm=弹窗），跳过被遮挡页面上的宿主，
            // 否则弹窗内容区收不到滚轮（会被背后页面的宿主吞掉）。
            var active = Form.ActiveForm;

            var pos = Cursor.Position;
            foreach (var (host, bar) in _pairs)
            {
                if (!host.Visible || !host.IsHandleCreated) continue;
                if (active != null && host.FindForm() != active) continue;
                if (!host.RectangleToScreen(host.ClientRectangle).Contains(pos)) continue;

                int raw = (short)((m.WParam.ToInt64() >> 16) & 0xFFFF);   // 滚轮刻度（有符号短整型）
                bar.ScrollLines(-Math.Sign(raw) * 5);                     // 上滚为正 → 向上滚
                return true;   // 吞掉滚轮消息，避免它落到焦点控件上
            }
            return false;
        }
    }

    /// <summary>给多行 RichTextBox 挂上细滚动条（置于指定宿主的右上内侧），并注册滚轮路由。</summary>
    public static ScrollIndicator AttachScrollIndicator(RichTextBox rtb, Control host, int rightInset, int topInset, int height)
    {
        var indicator = new ScrollIndicator(rtb)
        {
            Size = new Size(12, height),
            Location = new Point(host.ClientSize.Width - rightInset - 12, topInset),
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
