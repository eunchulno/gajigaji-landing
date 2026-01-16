namespace SlimeTodo.Models;

public class PetStatus
{
    public int Level { get; set; } = 1;
    public int TotalCompleted { get; set; }
    public int TodayCompleted { get; set; }
    public DateTime LastActiveDate { get; set; } = DateTime.Today;

    // 기억 시스템용 상태
    public DateTime LastAppOpenDate { get; set; } = DateTime.MinValue;
    public bool YesterdayWasAllDone { get; set; }
    public bool HasShownGreetingToday { get; set; }
}
