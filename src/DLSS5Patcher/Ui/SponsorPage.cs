using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 赞助支持页（对齐 GSX SupportView）：每次进入页面都重新从分发服务器拉取并解密赞助码；
/// 加载中 / 失败可重试，金额完全自愿。
/// </summary>
public sealed class SponsorPage : Theme.AmbientPage
{
    private enum State { Loading, Ready, Error, Empty }

    private readonly PictureBox _pic = new();
    private readonly Label _lblState = new();
    private readonly Theme.GlassButton _btnRetry = new();
    private readonly Panel _card;
    private bool _loading;

    public SponsorPage()
    {
        Size = new Size(862, 800);
        Controls.Add(Theme.MakePageHeader(L.S("OPTIONAL SUPPORT", "OPTIONAL SUPPORT"), L.S("赞助支持", "Sponsor")));

        var heart = new Label
        {
            Text = "♥",
            AutoSize = true,
            ForeColor = Theme.Signal,
            Font = new Font(Theme.FontUi, 20f),
            BackColor = Color.Transparent,
        };
        heart.Location = new Point(862 - 36 - heart.PreferredWidth, 34);
        Controls.Add(heart);

        _card = Theme.MakeCard(790, 560);
        _card.Location = new Point(36, 100);
        Controls.Add(_card);

        var slogan = Theme.MakeLabel(L.S("免费制作更新不易，还请各位大佬支持！", "This tool is free to make and update — your support is appreciated!"),
            Theme.Text, 10.5f, bold: true);
        slogan.Location = new Point(16, 18);
        _card.Controls.Add(slogan);

        _pic.Size = new Size(380, 380);
        _pic.Location = new Point((790 - 380) / 2, 66);
        _pic.BackColor = Color.White;
        _pic.SizeMode = PictureBoxSizeMode.Zoom;
        _pic.Visible = false;
        _card.Controls.Add(_pic);

        _lblState.AutoSize = false;
        _lblState.Size = new Size(758, 30);
        _lblState.Location = new Point(16, 300);
        _lblState.TextAlign = ContentAlignment.MiddleCenter;
        _lblState.ForeColor = Theme.TextSecondary;
        _lblState.Font = new Font(Theme.FontUi, 9.5f);
        _card.Controls.Add(_lblState);

        _btnRetry.Text = L.S("重新加载", "Reload");
        _btnRetry.Size = new Size(140, 38);
        _btnRetry.Location = new Point((790 - 140) / 2, 344);
        _btnRetry.Visible = false;
        _btnRetry.Click += (_, _) => _ = LoadAsync();
        _card.Controls.Add(_btnRetry);

        // 作者署名（点击访问 B 空间主页）
        var author = new Label
        {
            Text = L.S($"作者：{AgreementContent.AuthorName}（点击访问 B 站主页）", $"Author: {AgreementContent.AuthorName} (click to visit Bilibili)"),
            AutoSize = true,
            ForeColor = Theme.Signal,
            Font = new Font(Theme.FontUi, 9f, FontStyle.Bold),
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
        };
        author.Location = new Point(_card.Width - 16 - author.PreferredWidth, 528);
        author.Click += (_, _) =>
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = AgreementContent.AuthorUrl, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, L.S("提示", "Notice"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        };
        _card.Controls.Add(author);
    }

    /// <summary>主窗体在进入本页时调用：每次都重新拉取（与服务端换码即时生效一致）。</summary>
    public Task LoadAsync()
    {
        if (_loading) return Task.CompletedTask;
        _loading = true;
        return Task.Run(async () =>
        {
            var result = await SponsorQrClient.FetchAsync();
            try
            {
                BeginInvoke(() =>
                {
                    _loading = false;
                    if (result.Ok && result.Image != null)
                    {
                        using var ms = new MemoryStream(result.Image);
                        var old = _pic.Image;
                        _pic.Image = Image.FromStream(ms);
                        old?.Dispose();
                        Show(State.Ready);
                    }
                    else
                    {
                        // 404 = 服务器尚未配置赞助码资产
                        Show(result.Error == "http-404" ? State.Empty : State.Error);
                    }
                });
            }
            catch { _loading = false; }   // 窗体关闭时的竞态，忽略
        });
    }

    private void Show(State state)
    {
        _pic.Visible = state == State.Ready;
        _btnRetry.Visible = state == State.Error;
        _lblState.Visible = state != State.Ready;
        _lblState.ForeColor = state == State.Error ? Theme.Danger : Theme.TextSecondary;
        _lblState.Text = state switch
        {
            State.Loading => L.S("正在加载赞助码…", "Loading sponsor QR..."),
            State.Error => L.S("赞助码暂时无法加载，请检查网络后重试。", "Sponsor QR failed to load — check your network and retry."),
            State.Empty => L.S("赞助码暂未配置，请稍后再来。", "Sponsor QR is not configured yet — check back later."),
            _ => "",
        };
    }
}
