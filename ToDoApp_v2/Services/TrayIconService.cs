using System.Drawing;
using System.Windows.Forms;

namespace SlimeTodo.Services;

public class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private bool _disposed;

    public event Action? ShowWindowRequested;
    public event Action? ExitRequested;

    public TrayIconService()
    {
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("열기", null, (s, e) => ShowWindowRequested?.Invoke());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("종료", null, (s, e) => ExitRequested?.Invoke());

        _notifyIcon = new NotifyIcon
        {
            Text = "GajiGaji",
            Icon = CreateDefaultIcon(),
            Visible = false,
            ContextMenuStrip = _contextMenu
        };

        _notifyIcon.DoubleClick += (s, e) => ShowWindowRequested?.Invoke();
    }

    public void Show()
    {
        _notifyIcon.Visible = true;
    }

    public void Hide()
    {
        _notifyIcon.Visible = false;
    }

    public void UpdateTooltip(int todayCount, int completedCount)
    {
        _notifyIcon.Text = $"GajiGaji - 오늘: {completedCount}/{todayCount}";
    }

    public void ShowBalloonTip(string title, string text, ToolTipIcon icon = ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, text, icon);
    }

    private static Icon CreateDefaultIcon()
    {
        // 가지 모양 아이콘 생성 (16x16)
        var bitmap = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.Clear(Color.Transparent);
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // 가지 꼭지 (초록색)
            using var stemBrush = new SolidBrush(Color.FromArgb(76, 153, 76));
            g.FillEllipse(stemBrush, 5, 0, 6, 5);

            // 가지 몸통 (보라색)
            using var bodyBrush = new SolidBrush(Color.FromArgb(102, 51, 153));
            g.FillEllipse(bodyBrush, 2, 3, 12, 12);

            // 눈 (밝은 색)
            using var eyeBrush = new SolidBrush(Color.FromArgb(245, 245, 245));
            g.FillEllipse(eyeBrush, 4, 7, 3, 3);
            g.FillEllipse(eyeBrush, 9, 7, 3, 3);
        }

        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
        }

        _disposed = true;
    }
}
