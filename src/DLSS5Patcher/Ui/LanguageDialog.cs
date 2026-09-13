using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>首次启动的语言选择对话框（玻璃深色 + 双列大按钮），选择后写入 AppConfig。</summary>
public sealed class LanguageDialog : Form
{
    public LanguageDialog()
    {
        Text = "选择语言 / Select Language";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 220);
        BackColor = Theme.FromHex("#1a1c18");
        Font = new Font(Theme.FontUi, 9F);
        Shown += (_, _) => Theme.ApplyWindowChrome(this);

        var title = Theme.MakeLabel("请选择界面语言", Theme.Text, 12f, bold: true);
        title.Location = new Point(30, 22);
        Controls.Add(title);

        var sub = Theme.MakeLabel("Please choose the interface language (changeable later in Settings).", Theme.TextMuted, 9f);
        sub.Location = new Point(30, 56);
        Controls.Add(sub);

        var btnZh = Theme.MakeButton("简体中文", primary: true);
        btnZh.Size = new Size(200, 56);
        btnZh.Location = new Point(30, 100);
        btnZh.Font = new Font(Theme.FontUi, 11f, FontStyle.Bold);
        btnZh.Click += (_, _) => Choose("zh");
        Controls.Add(btnZh);

        var btnEn = Theme.MakeButton("English");
        btnEn.Size = new Size(200, 56);
        btnEn.Location = new Point(250, 100);
        btnEn.Font = new Font(Theme.FontUi, 11f, FontStyle.Bold);
        btnEn.Click += (_, _) => Choose("en");
        Controls.Add(btnEn);

        // 高 DPI 整体缩放（与主窗体同一方案）
        float dpi;
        using (var g = CreateGraphics()) dpi = g.DpiX / 96f;
        if (dpi > 1.01f)
        {
            Scale(new SizeF(dpi, dpi));
            ClientSize = new Size((int)(480 * dpi), (int)(220 * dpi));
        }
    }

    private void Choose(string lang)
    {
        AppConfig.Lang = lang;
        AppConfig.Save();
        DialogResult = DialogResult.OK;
        Close();
    }
}
