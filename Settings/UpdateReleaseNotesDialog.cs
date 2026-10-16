using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;

namespace HyperXBatteryTray.Settings;

/// <summary>
/// Displays only the release body returned by the trusted GitHub Releases API.
/// The Markdown is treated as untrusted plain text: never render remote HTML,
/// script, images, or active links inside an embedded browser.
/// </summary>
internal sealed class UpdateReleaseNotesDialog : Form
{
    private static readonly Regex MarkdownLink = new(
        @"\[([^\]\r\n]+)\]\(https?://[^)\r\n]+\)",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(200));

    internal UpdateReleaseNotesDialog(
        string tag, string? releaseNotes, Uri releasePageUrl,
        AppLanguage language, bool dark)
    {
        string L(string key) => Localization.Get(key, language);
        Color background = dark ? Color.FromArgb(31, 34, 37) : Color.FromArgb(250, 251, 253);
        Color foreground = dark ? Color.WhiteSmoke : Color.FromArgb(24, 32, 45);
        Color border = dark ? Color.FromArgb(66, 71, 76) : Color.FromArgb(222, 227, 234);
        Color accent = dark ? Color.FromArgb(95, 174, 255) : Color.FromArgb(0, 102, 204);

        Text = string.Format(L("UpdatesReleaseNotesTitle"), tag);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.Sizable;
        ClientSize = new Size(650, 530);
        MinimumSize = new Size(440, 340);
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ShowIcon = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 9f);
        BackColor = background;
        ForeColor = foreground;

        TableLayoutPanel layout = new()
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(18, 14, 18, 14),
            BackColor = background
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        Controls.Add(layout);

        Label header = new()
        {
            Text = Text,
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 13f),
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = foreground,
            BackColor = background,
            AutoEllipsis = true
        };
        layout.Controls.Add(header, 0, 0);

        RichTextBox body = new()
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            DetectUrls = false,
            TabStop = true,
            BackColor = background,
            ForeColor = foreground,
            Font = new Font("Segoe UI", 9.5f),
            Margin = new Padding(2, 8, 2, 8)
        };
        layout.Controls.Add(body, 0, 1);
        RenderMarkdown(body, string.IsNullOrWhiteSpace(releaseNotes)
            ? L("UpdatesReleaseNotesUnavailable") : releaseNotes, foreground, accent);

        Panel footer = new() { Dock = DockStyle.Fill, BackColor = background };
        footer.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 1, BackColor = border });
        FlowLayoutPanel actions = new()
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0),
            BackColor = background
        };
        footer.Controls.Add(actions);
        layout.Controls.Add(footer, 0, 2);

        Button close = new()
        {
            Text = L("UpdatesClose"),
            AutoSize = true,
            MinimumSize = new Size(96, 32),
            DialogResult = DialogResult.OK,
            FlatStyle = FlatStyle.Flat,
            BackColor = dark ? Color.FromArgb(52, 56, 62) : Color.White,
            ForeColor = foreground,
            Margin = new Padding(8, 0, 0, 0)
        };
        Button viewOnline = new()
        {
            Text = L("UpdatesViewOnGitHub"),
            AutoSize = true,
            MinimumSize = new Size(145, 32),
            FlatStyle = FlatStyle.Flat,
            BackColor = accent,
            ForeColor = Color.White,
            Margin = new Padding(8, 0, 0, 0)
        };
        // The URL is already restricted to the project's GitHub release pages
        // by GitHubReleaseChecker.TryGetTrustedUrl.
        viewOnline.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(releasePageUrl.AbsoluteUri)
                {
                    UseShellExecute = true
                });
            }
            catch (System.ComponentModel.Win32Exception) { }
            catch (InvalidOperationException) { }
        };
        close.FlatAppearance.BorderColor = border;
        viewOnline.FlatAppearance.BorderSize = 0;
        actions.Controls.Add(close);
        actions.Controls.Add(viewOnline);
        AcceptButton = close;
        CancelButton = close;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        Rectangle area = Screen.FromControl(this).WorkingArea;
        // Keep the dialog entirely visible at lower resolutions and high DPI.
        Width = Math.Min(Width, Math.Max(MinimumSize.Width, area.Width - 24));
        Height = Math.Min(Height, Math.Max(MinimumSize.Height, area.Height - 24));
        CenterToParent();
    }

    private static void RenderMarkdown(RichTextBox box, string markdown,
        Color foreground, Color accent)
    {
        using Font normal = new("Segoe UI", 9.5f);
        using Font strong = new("Segoe UI Semibold", 9.5f);
        using Font heading = new("Segoe UI Semibold", 11f);
        using Font code = new("Consolas", 9f);

        box.SuspendLayout();
        foreach (string line in markdown.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
        {
            string text = line.TrimEnd();
            bool isHeading = false;
            int hashes = 0;
            while (hashes < text.Length && hashes < 6 && text[hashes] == '#') hashes++;
            if (hashes > 0 && hashes < text.Length && text[hashes] == ' ')
            {
                isHeading = true;
                text = text[(hashes + 1)..];
            }
            else if (text.StartsWith("- ", StringComparison.Ordinal) ||
                     text.StartsWith("* ", StringComparison.Ordinal))
                text = "  • " + text[2..];
            else if (text is "---" or "***")
                text = "────────────────────";

            // Show readable labels without treating GitHub-provided links as active content.
            try { text = MarkdownLink.Replace(text, "$1"); }
            catch (RegexMatchTimeoutException) { }

            int index = 0;
            bool bold = false;
            bool mono = false;
            while (index < text.Length)
            {
                if (index + 1 < text.Length && text.AsSpan(index).StartsWith("**"))
                {
                    bold = !bold;
                    index += 2;
                    continue;
                }
                if (text[index] == '`')
                {
                    mono = !mono;
                    index++;
                    continue;
                }
                int next = index + 1;
                while (next < text.Length && text[next] != '`' &&
                       !(next + 1 < text.Length && text.AsSpan(next).StartsWith("**"))) next++;
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;
                box.SelectionFont = mono ? code : isHeading ? heading : bold ? strong : normal;
                box.SelectionColor = isHeading ? accent : foreground;
                box.AppendText(text[index..next]);
                index = next;
            }
            box.SelectionStart = box.TextLength;
            box.SelectionLength = 0;
            box.SelectionFont = normal;
            box.AppendText("\n");
        }
        box.SelectionStart = 0;
        box.SelectionLength = 0;
        box.ResumeLayout();
    }
}
