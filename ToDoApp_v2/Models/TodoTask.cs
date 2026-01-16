namespace SlimeTodo.Models;

public class TodoTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsImportant { get; set; }
    public bool IsPinnedToday { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? CompletedAt { get; set; }
    public int Order { get; set; }
    public List<SubTask> SubTasks { get; set; } = [];

    // Project (null = Inbox)
    public string? ProjectId { get; set; }

    // HashTags
    public List<string> HashTagIds { get; set; } = [];

    // Recurrence
    public RecurrenceType Recurrence { get; set; } = RecurrenceType.None;
    public int RecurrenceInterval { get; set; } = 1; // 반복 간격 (예: 2주마다 = 2)

    // Reminder
    public DateTime? ReminderTime { get; set; }
    public bool ReminderNotified { get; set; }
}

public enum RecurrenceType
{
    None,
    Daily,
    Weekly,
    Monthly,
    Yearly
}

public class SubTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Title { get; set; } = string.Empty;
    public bool IsCompleted { get; set; }
    public int Order { get; set; }
}

public class HashTag
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#9B59B6"; // 기본 보라색
    public int Order { get; set; }
    public bool IsHidden { get; set; } = false; // 숨김 여부
}

public static class HashTagColors
{
    public static readonly string[] Palette =
    [
        "#E74C3C", // 빨강
        "#E67E22", // 주황
        "#F1C40F", // 노랑
        "#2ECC71", // 초록
        "#1ABC9C", // 청록
        "#3498DB", // 파랑
        "#9B59B6", // 보라
        "#E91E63", // 분홍
        "#95A5A6", // 회색
        "#34495E"  // 진회색
    ];
}
