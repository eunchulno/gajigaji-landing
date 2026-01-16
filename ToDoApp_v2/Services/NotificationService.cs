using System.Diagnostics;
using System.Windows.Threading;
using SlimeTodo.Models;
using Microsoft.Toolkit.Uwp.Notifications;

namespace SlimeTodo.Services;

public class NotificationService : IDisposable
{
    private readonly TaskService _taskService;
    private readonly DispatcherTimer _checkTimer;
    private const string AppId = "SlimeTodo";

    public NotificationService(TaskService taskService)
    {
        _taskService = taskService;

        _checkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _checkTimer.Tick += CheckReminders;
    }

    public void Start()
    {
        _checkTimer.Start();
        CheckReminders(null, EventArgs.Empty);
    }

    public void Stop()
    {
        _checkTimer.Stop();
    }

    private void CheckReminders(object? sender, EventArgs e)
    {
        var pendingTasks = _taskService.GetTasksWithPendingReminders().ToList();

        foreach (var task in pendingTasks)
        {
            ShowNotification(task);
            _taskService.MarkReminderNotified(task);
        }
    }

    private void ShowNotification(TodoTask task)
    {
        try
        {
            var builder = new ToastContentBuilder()
                .AddText("할일 알림")
                .AddText(task.Title);

            if (task.DueDate.HasValue)
            {
                builder.AddText($"마감: {task.DueDate.Value:MM/dd}");
            }

            builder.Show();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[NotificationService] ShowNotification failed: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
    }
}
