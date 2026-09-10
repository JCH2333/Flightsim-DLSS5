namespace DLSS5Patcher.Ui;

/// <summary>
/// 深色主题设计令牌与控件工厂。配色取自 GSX 汉化安装器的视觉语言（深色 + 主题绿 + 圆角卡片）。
/// 所有布局按 96 DPI 设计，由 MainForm 在启动时按真实 DPI 整体缩放。
/// </summary>
public static class Theme
{
    public static readonly Color Bg            = FromHex("#171816");
    public static readonly Color Sidebar       = FromHex("#1b1c19");
    public static readonly Color Surface       = FromHex("#20211e");
    public static readonly Color SurfaceRaised = FromHex("#292a26");
    public static readonly Color SurfaceHover  = FromHex("#30312c");
    public static readonly Color Border        = FromHex("#3a3b35");
    public static readonly Color BorderStrong  = FromHex("#515249");
    public static readonly Color Text          = FromHex("#f1f2ec");
    public static readonly Color TextSecondary = FromHex("#b8baaf");
    public static readonly Color TextMuted     = FromHex("#85877e");
    public static readonly Color Accent        = FromHex("#62d6a3");
    public static readonly Color AccentStrong  = FromHex("#39bd87");
    public static readonly Color AccentText    = FromHex("#102019");
    public static readonly Color Warning       = FromHex("#e3b253");
    public static readonly Color Danger        = FromHex("#e66c62");

    public static Color FromHex(string hex)
    {
        var h = hex.TrimStart('#');
        return Color.FromArgb(Convert.ToInt32(h[..2], 16), Convert.ToInt32(h[2..4], 16), Convert.ToInt32(h[4..6], 16));
    }

    public static Button MakeButton(string text, bool primary = false)
    {
        var b = new Button
        {
            Text = text,
            FlatStyle = FlatStyle.Flat,
            BackColor = primary ? AccentStrong : Surface,
            ForeColor = primary ? AccentText : TextSecondary,
            Font = new Font("Microsoft YaHei UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Height = 32,
            UseVisualStyleBackColor = false,
        };
        b.FlatAppearance.BorderSize = primary ? 0 : 1;
        b.FlatAppearance.BorderColor = Border;
        b.FlatAppearance.MouseOverBackColor = primary ? Accent : SurfaceHover;
        b.FlatAppearance.MouseDownBackColor = primary ? AccentStrong : SurfaceRaised;
        b.FlatAppearance.CheckedBackColor = primary ? AccentStrong : SurfaceRaised;
        b.EnabledChanged += (_, _) =>
        {
            b.BackColor = b.Enabled ? (primary ? AccentStrong : Surface) : Surface;
            b.ForeColor = b.Enabled ? (primary ? AccentText : TextSecondary) : TextMuted;
        };
        return b;
    }

    public static Label MakeLabel(string text, Color? color = null, float size = 9f, bool bold = false)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            ForeColor = color ?? TextSecondary,
            Font = new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular),
            BackColor = Color.Transparent,
        };
    }

    public static Panel MakeCard(int w, int h)
    {
        var p = new Panel { Size = new Size(w, h), BackColor = Surface };
        EnableBorder(p, Border);
        return p;
    }

    /// <summary>给面板画 1px 描边（WinForms Panel 无原生边框色）。</summary>
    public static void EnableBorder(Control c, Color color)
    {
        c.Paint += (_, e) =>
        {
            using var pen = new Pen(color);
            e.Graphics.DrawRectangle(pen, 0, 0, c.Width - 1, c.Height - 1);
        };
    }

    /// <summary>页面主标题（大号加粗 + 绿色竖条）。返回的宿主面板需由调用方定位。</summary>
    public static Panel MakePageTitle(string text, int x, int y)
    {
        var host = new Panel { Location = new Point(x, y), Size = new Size(420, 30), BackColor = Bg };
        var bar = new Panel { Location = new Point(0, 5), Size = new Size(4, 20), BackColor = AccentStrong };
        var lbl = MakeLabel(text, Text, 14f, bold: true);
        lbl.Location = new Point(14, 1);
        host.Controls.Add(bar);
        host.Controls.Add(lbl);
        return host;
    }
}
