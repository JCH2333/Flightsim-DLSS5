using DLSS5Patcher.Core;

namespace DLSS5Patcher.Ui;

/// <summary>
/// 公告列表页（对齐 GSX AnnouncementsView）：类别/置顶标签 + 时间 + 标题 + 内容，
/// 加载中 / 失败可重试 / 空列表三种状态。内容用 RichTextBox 排版并挂细滚动指示条。
/// </summary>
public sealed class AnnouncementsPage : Theme.AmbientPage
{
    private readonly RichTextBox _list = new();
    private readonly Label _lblState = new();
    private readonly Theme.GlassButton _btnReload = new();
    private readonly Panel _card;
    private List<AnnouncementsClient.Announcement> _items = new();

    /// <summary>加载成功并渲染后触发（参数 = 本次列表最大公告 id，主窗体据此推进已读水位）。</summary>
    public event Action<long>? Read;

    public AnnouncementsPage()
    {
        Size = new Size(862, 800);
        Controls.Add(Theme.MakePageHeader(L.S("ANNOUNCEMENTS", "ANNOUNCEMENTS"), L.S("公告", "Announcements")));

        var bell = new Label
        {
            Text = "🔔",
            AutoSize = true,
            Font = new Font("Segoe UI Emoji", 12f),
            BackColor = Color.Transparent,
        };
        bell.Location = new Point(862 - 36 - bell.PreferredWidth, 36);
        Controls.Add(bell);

        _card = Theme.MakeCard(790, 676);
        _card.Location = new Point(36, 100);
        Controls.Add(_card);

        _lblState.AutoSize = false;
        _lblState.Size = new Size(758, 36);
        _lblState.Location = new Point(16, 60);
        _lblState.TextAlign = ContentAlignment.MiddleCenter;
        _lblState.ForeColor = Theme.TextSecondary;
        _lblState.Font = new Font(Theme.FontUi, 9.5f);
        _card.Controls.Add(_lblState);

        _btnReload.Text = L.S("重新加载", "Reload");
        _btnReload.Size = new Size(140, 38);
        _btnReload.Location = new Point((790 - 140) / 2, 110);
        _btnReload.Visible = false;
        _btnReload.Click += (_, _) => _ = ReloadAsync();
        _card.Controls.Add(_btnReload);

        _list.ReadOnly = true;
        _list.BorderStyle = BorderStyle.None;
        _list.BackColor = Theme.Surface;   // RichTextBox 不支持透明底，用卡片填充色
        _list.ScrollBars = RichTextBoxScrollBars.None;
        _list.Font = new Font(Theme.FontUi, 9.25f);
        _list.Size = new Size(758, 644);
        _list.Location = new Point(16, 16);
        _list.Visible = false;
        _card.Controls.Add(_list);
        Theme.AttachScrollIndicator(_list, _card, rightInset: 12, topInset: 18, height: 640);
    }

    /// <summary>拉取公告并渲染；加载成功后触发 Read（主窗体清除未读点）。</summary>
    public async Task ReloadAsync()
    {
        ShowBusy();
        var result = await AnnouncementsClient.FetchListAsync();
        if (!IsHandleCreated) return;
        try
        {
            BeginInvoke(() =>
            {
                if (result.Ok)
                {
                    _items = result.Announcements;
                    Render();
                    Read?.Invoke(_items.Count > 0 ? _items.Max(a => a.Id) : 0);
                }
                else
                {
                    ShowError(result.Error);
                }
            });
        }
        catch { /* 窗体关闭竞态 */ }
    }

    private void ShowBusy()
    {
        _list.Visible = _btnReload.Visible = false;
        _lblState.Visible = true;
        _lblState.ForeColor = Theme.TextSecondary;
        _lblState.Text = L.S("正在获取公告…", "Fetching announcements...");
    }

    private void ShowError(string message)
    {
        _list.Visible = false;
        _lblState.Visible = _btnReload.Visible = true;
        _lblState.ForeColor = Theme.Danger;
        _lblState.Text = message;
    }

    private void Render()
    {
        if (_items.Count == 0)
        {
            _list.Visible = false;
            _lblState.Visible = true;
            _btnReload.Visible = false;
            _lblState.ForeColor = Theme.TextSecondary;
            _lblState.Text = L.S("暂无公告", "No announcements");
            return;
        }

        _lblState.Visible = _btnReload.Visible = false;
        _list.Visible = true;
        _list.Clear();

        void AppendRaw(string text, Color color, Font font)
        {
            _list.SelectionStart = _list.TextLength;
            _list.SelectionColor = color;
            _list.SelectionFont = font;
            _list.SelectedText = text;
        }

        var titleFont = new Font(Theme.FontUi, 11.5f, FontStyle.Bold);
        var bodyFont = new Font(Theme.FontUi, 9.25f);
        var tagFont = new Font(Theme.FontUi, 8.25f, FontStyle.Bold);
        var metaFont = new Font(Theme.FontUi, 8.25f);

        for (int i = 0; i < _items.Count; i++)
        {
            var a = _items[i];

            AppendRaw("● ", Theme.Signal, tagFont);
            var pinned = a.Pinned ? L.S(" · 置顶", " · Pinned") : "";
            AppendRaw($"{AnnouncementsClient.CategoryLabel(a.Category)}{pinned}", Theme.Signal, tagFont);
            AppendRaw($"      {AnnouncementsClient.FormatTime(a.CreatedAt)}\n", Theme.TextMuted, metaFont);

            AppendRaw(a.Title + "\n", Theme.Text, titleFont);
            AppendRaw(a.Content + "\n", Theme.TextSecondary, bodyFont);

            if (i < _items.Count - 1)
                AppendRaw("\n" + new string('─', 56) + "\n\n", Theme.GlassBorder, metaFont);
        }

        _list.SelectionStart = 0;
        _list.SelectionLength = 0;
    }
}
