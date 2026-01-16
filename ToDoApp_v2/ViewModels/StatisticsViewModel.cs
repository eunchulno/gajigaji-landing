using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Collections.ObjectModel;
using System.Windows.Media;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using MediaBrushes = System.Windows.Media.Brushes;

namespace SlimeTodo.ViewModels;

public class StatisticsViewModel : ViewModelBase
{
    private readonly TaskService _taskService;

    public StatisticsViewModel(TaskService taskService)
    {
        _taskService = taskService;
        WeeklyData = new ObservableCollection<DayData>();
        MonthlyData = new ObservableCollection<DayCell>();
        LoadData();
    }

    public int TotalCompleted => _taskService.GetPetStatus().TotalCompleted;

    public int CurrentStreak => _taskService.GetStatistics().CurrentStreak;

    public int BestStreak => _taskService.GetStatistics().BestStreak;

    public ObservableCollection<DayData> WeeklyData { get; }

    public ObservableCollection<DayCell> MonthlyData { get; }

    public void LoadData()
    {
        LoadWeeklyData();
        LoadMonthlyData();
        OnPropertyChanged(nameof(TotalCompleted));
        OnPropertyChanged(nameof(CurrentStreak));
        OnPropertyChanged(nameof(BestStreak));
    }

    private void LoadWeeklyData()
    {
        WeeklyData.Clear();
        var stats = _taskService.GetStatistics();
        var today = DateTime.Today;

        int maxCompleted = 1;
        var weekData = new List<(DateTime Date, int Completed)>();

        for (int i = 6; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayStats = stats.DailyHistory.FirstOrDefault(s => s.Date.Date == date);
            var completed = dayStats?.Completed ?? 0;
            weekData.Add((date, completed));
            if (completed > maxCompleted) maxCompleted = completed;
        }

        foreach (var (date, completed) in weekData)
        {
            WeeklyData.Add(new DayData
            {
                Date = date,
                DayName = GetDayName(date.DayOfWeek),
                Completed = completed,
                BarHeight = Math.Max(4, (completed * 60.0) / maxCompleted)
            });
        }
    }

    private void LoadMonthlyData()
    {
        MonthlyData.Clear();
        var stats = _taskService.GetStatistics();
        var today = DateTime.Today;

        for (int i = 29; i >= 0; i--)
        {
            var date = today.AddDays(-i);
            var dayStats = stats.DailyHistory.FirstOrDefault(s => s.Date.Date == date);
            var completed = dayStats?.Completed ?? 0;

            MonthlyData.Add(new DayCell
            {
                Date = date,
                Completed = completed,
                Color = GetHeatmapColor(completed),
                Tooltip = $"{date:MM/dd}: {completed}개 완료"
            });
        }
    }

    private static string GetDayName(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "월",
            DayOfWeek.Tuesday => "화",
            DayOfWeek.Wednesday => "수",
            DayOfWeek.Thursday => "목",
            DayOfWeek.Friday => "금",
            DayOfWeek.Saturday => "토",
            DayOfWeek.Sunday => "일",
            _ => ""
        };
    }

    private static MediaBrush GetHeatmapColor(int completed)
    {
        return completed switch
        {
            0 => new SolidColorBrush(MediaColor.FromRgb(232, 232, 232)),
            1 => new SolidColorBrush(MediaColor.FromRgb(198, 228, 139)),
            2 => new SolidColorBrush(MediaColor.FromRgb(123, 201, 111)),
            3 or 4 => new SolidColorBrush(MediaColor.FromRgb(35, 154, 59)),
            _ => new SolidColorBrush(MediaColor.FromRgb(25, 97, 39))
        };
    }
}

public class DayData
{
    public DateTime Date { get; set; }
    public string DayName { get; set; } = string.Empty;
    public int Completed { get; set; }
    public double BarHeight { get; set; }
}

public class DayCell
{
    public DateTime Date { get; set; }
    public int Completed { get; set; }
    public MediaBrush Color { get; set; } = MediaBrushes.Gray;
    public string Tooltip { get; set; } = string.Empty;
}
