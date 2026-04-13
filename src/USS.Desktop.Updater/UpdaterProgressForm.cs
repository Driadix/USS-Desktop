using System.Drawing;
using System.Windows.Forms;

namespace USS.Desktop.Updater;

internal sealed class UpdaterProgressForm : Form
{
    private readonly string[] _args;
    private readonly Label _titleLabel;
    private readonly Label _statusLabel;
    private readonly ProgressBar _progressBar;
    private readonly Button _closeButton;
    private bool _canClose;

    public UpdaterProgressForm(string[] args)
    {
        _args = args;
        ExitCode = 1;

        Text = "USS Desktop Update";
        ClientSize = new Size(500, 200);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = false;
        TopMost = true;

        _titleLabel = new Label
        {
            AutoSize = false,
            Font = new Font(Font.FontFamily, 11F, FontStyle.Bold),
            Location = new Point(20, 18),
            Size = new Size(460, 24),
            Text = "Updating USS Desktop"
        };

        _statusLabel = new Label
        {
            AutoSize = false,
            Location = new Point(20, 52),
            Size = new Size(460, 58),
            AutoEllipsis = true,
            Text = "Preparing update..."
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 124),
            Size = new Size(460, 18),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 30
        };

        _closeButton = new Button
        {
            Enabled = false,
            Location = new Point(380, 162),
            Size = new Size(100, 28),
            Text = "Close",
            Visible = false
        };
        _closeButton.Click += (_, _) =>
        {
            _canClose = true;
            Close();
        };

        Controls.AddRange(new Control[] { _titleLabel, _statusLabel, _progressBar, _closeButton });
    }

    public int ExitCode { get; private set; }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        var progress = new Progress<UpdateProgress>(ReportProgress);
        ExitCode = await Task.Run(() => UpdaterProgram.RunAsync(_args, progress, TimeSpan.FromSeconds(2)));
        if (ExitCode == 0)
        {
            _canClose = true;
            Close();
            return;
        }

        _canClose = true;
        _closeButton.Enabled = true;
        _closeButton.Visible = true;
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_canClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnFormClosing(e);
    }

    private void ReportProgress(UpdateProgress progress)
    {
        _statusLabel.Text = progress.Message;
        if (progress.IsError)
        {
            _titleLabel.Text = "Update failed";
            _progressBar.Style = ProgressBarStyle.Continuous;
            _progressBar.Value = 100;
            return;
        }

        if (progress.Percent is null)
        {
            _progressBar.Style = ProgressBarStyle.Marquee;
            _progressBar.MarqueeAnimationSpeed = 30;
            return;
        }

        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.MarqueeAnimationSpeed = 0;
        _progressBar.Value = Math.Clamp(progress.Percent.Value, _progressBar.Minimum, _progressBar.Maximum);
        if (progress.Percent.Value == _progressBar.Maximum)
        {
            _titleLabel.Text = "Update completed";
        }
    }
}
