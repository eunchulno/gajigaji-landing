namespace SlimeTodo.Models;

public class AppData
{
    public const int CurrentSchemaVersion = 1;

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public List<TodoTask> Tasks { get; set; } = new();
    public List<Project> Projects { get; set; } = new();
    public List<HashTag> HashTags { get; set; } = new();
    public PetStatus PetStatus { get; set; } = new();
    public bool IsDarkMode { get; set; } = false;
    public Statistics Statistics { get; set; } = new();
}

public class Statistics
{
    public List<DailyStats> DailyHistory { get; set; } = [];
    public int CurrentStreak { get; set; }
    public int BestStreak { get; set; }
    public DateTime? LastCompletionDate { get; set; }
}

public class DailyStats
{
    public DateTime Date { get; set; }
    public int Completed { get; set; }
    public int Created { get; set; }
}
