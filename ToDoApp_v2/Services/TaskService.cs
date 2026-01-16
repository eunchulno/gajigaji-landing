using SlimeTodo.Models;

namespace SlimeTodo.Services;

public class TaskService
{
    private readonly StorageService _storageService;
    private readonly NaturalLanguageParser _parser;
    private AppData _data;

    public event Action? DataChanged;

    public TaskService(StorageService storageService)
    {
        _storageService = storageService;
        _parser = new NaturalLanguageParser();
        _data = _storageService.Load();
    }

    public IReadOnlyList<TodoTask> GetAllTasks() => _data.Tasks.AsReadOnly();

    public IEnumerable<TodoTask> GetAllTasks(bool includeCompleted)
    {
        var tasks = includeCompleted
            ? _data.Tasks
            : _data.Tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenBy(t => t.Order);
    }

    public IEnumerable<TodoTask> GetInboxTasks(bool includeCompleted = false)
    {
        // Inbox = ProjectId가 null인 작업들
        var tasks = _data.Tasks.Where(t => t.ProjectId == null);

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.Order);
    }

    public IEnumerable<TodoTask> GetTodayTasks(bool includeCompleted = false)
    {
        var today = DateTime.Today;
        // 프로젝트에 속한 할일은 제외
        var tasks = _data.Tasks.Where(t =>
            t.ProjectId == null &&
            (t.IsPinnedToday ||
            (t.DueDate.HasValue && t.DueDate.Value.Date <= today)));

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.Order);
    }

    public IEnumerable<TodoTask> GetUpcomingTasks(bool includeCompleted = false)
    {
        var today = DateTime.Today;
        // 프로젝트에 속한 할일은 제외
        var tasks = _data.Tasks.Where(t =>
            t.ProjectId == null &&
            t.DueDate.HasValue && t.DueDate.Value.Date > today);

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenBy(t => t.Order);
    }

    public IEnumerable<TodoTask> GetWeekTasks(DateTime weekStart, DateTime weekEnd, bool includeCompleted = false)
    {
        // 프로젝트에 속한 할일은 제외
        var tasks = _data.Tasks.Where(t =>
            t.ProjectId == null &&
            t.DueDate.HasValue &&
            t.DueDate.Value.Date >= weekStart.Date &&
            t.DueDate.Value.Date <= weekEnd.Date);

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenBy(t => t.DueDate)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.Order);
    }

    public IEnumerable<TodoTask> GetUnscheduledTasks(bool includeCompleted = false)
    {
        // 프로젝트에 속한 할일은 제외
        var tasks = _data.Tasks.Where(t => t.ProjectId == null && !t.DueDate.HasValue);

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.Order);
    }

    public void SetTaskDueDate(TodoTask task, DateTime? dueDate)
    {
        task.DueDate = dueDate;
        Save();
    }

    public TodoTask AddTask(string input, string? projectId = null)
    {
        var parseResult = _parser.Parse(input);

        var task = new TodoTask
        {
            Title = parseResult.Title,
            DueDate = parseResult.DueDate,
            ProjectId = projectId,
            Order = _data.Tasks.Count
        };

        // #태그명 파싱된 경우, 해시태그 생성/연결
        if (parseResult.HashTags.Count > 0)
        {
            foreach (var tagName in parseResult.HashTags)
            {
                var cleanName = tagName.Trim('"');
                var hashTag = GetHashTagByName(cleanName) ?? AddHashTag(cleanName);
                task.HashTagIds.Add(hashTag.Id);
            }
        }

        _data.Tasks.Add(task);
        UpdateStatistics(created: 1);
        Save();
        return task;
    }

    public void SetRecurrence(TodoTask task, RecurrenceType type, int interval = 1)
    {
        task.Recurrence = type;
        task.RecurrenceInterval = interval;
        Save();
    }

    public void SetReminder(TodoTask task, DateTime? reminderTime)
    {
        task.ReminderTime = reminderTime;
        task.ReminderNotified = false;
        Save();
    }

    public IEnumerable<TodoTask> GetTasksWithPendingReminders()
    {
        var now = DateTime.Now;
        return _data.Tasks.Where(t =>
            !t.IsCompleted &&
            t.ReminderTime.HasValue &&
            t.ReminderTime.Value <= now &&
            !t.ReminderNotified);
    }

    public void MarkReminderNotified(TodoTask task)
    {
        task.ReminderNotified = true;
        Save();
    }

    public Statistics GetStatistics() => _data.Statistics;

    public void ToggleComplete(TodoTask task)
    {
        task.IsCompleted = !task.IsCompleted;

        if (task.IsCompleted)
        {
            task.CompletedAt = DateTime.Now;
            _data.PetStatus.TotalCompleted++;

            // 오늘 날짜 확인하여 TodayCompleted 업데이트
            if (_data.PetStatus.LastActiveDate.Date != DateTime.Today)
            {
                _data.PetStatus.LastActiveDate = DateTime.Today;
                _data.PetStatus.TodayCompleted = 0;
            }
            _data.PetStatus.TodayCompleted++;

            // 레벨업 체크 (10개당 1레벨, 최대 999)
            int newLevel = Math.Min(999, 1 + _data.PetStatus.TotalCompleted / 10);
            _data.PetStatus.Level = newLevel;

            // 통계 업데이트
            UpdateStatistics(completed: 1);

            // 반복 작업 처리
            if (task.Recurrence != RecurrenceType.None)
            {
                CreateNextRecurrence(task);
            }
        }
        else
        {
            task.CompletedAt = null;
        }

        Save();
    }

    private void CreateNextRecurrence(TodoTask completedTask)
    {
        var nextDueDate = CalculateNextDueDate(completedTask.DueDate ?? DateTime.Today,
            completedTask.Recurrence, completedTask.RecurrenceInterval);

        var newTask = new TodoTask
        {
            Title = completedTask.Title,
            DueDate = nextDueDate,
            IsImportant = completedTask.IsImportant,
            Recurrence = completedTask.Recurrence,
            RecurrenceInterval = completedTask.RecurrenceInterval,
            Order = _data.Tasks.Count
        };

        // 하위 작업도 복사 (미완료 상태로)
        foreach (var subTask in completedTask.SubTasks)
        {
            newTask.SubTasks.Add(new SubTask
            {
                Title = subTask.Title,
                Order = subTask.Order,
                IsCompleted = false
            });
        }

        _data.Tasks.Add(newTask);
    }

    private static DateTime CalculateNextDueDate(DateTime baseDate, RecurrenceType type, int interval)
    {
        return type switch
        {
            RecurrenceType.Daily => baseDate.AddDays(interval),
            RecurrenceType.Weekly => baseDate.AddDays(7 * interval),
            RecurrenceType.Monthly => baseDate.AddMonths(interval),
            RecurrenceType.Yearly => baseDate.AddYears(interval),
            _ => baseDate
        };
    }

    private void UpdateStatistics(int completed = 0, int created = 0)
    {
        var today = DateTime.Today;
        var todayStats = _data.Statistics.DailyHistory
            .FirstOrDefault(s => s.Date.Date == today);

        if (todayStats == null)
        {
            todayStats = new DailyStats { Date = today };
            _data.Statistics.DailyHistory.Add(todayStats);
        }

        todayStats.Completed += completed;
        todayStats.Created += created;

        // 연속 기록 업데이트
        if (completed > 0)
        {
            var lastDate = _data.Statistics.LastCompletionDate;
            if (lastDate == null || lastDate.Value.Date < today.AddDays(-1))
            {
                _data.Statistics.CurrentStreak = 1;
            }
            else if (lastDate.Value.Date == today.AddDays(-1))
            {
                _data.Statistics.CurrentStreak++;
            }
            // 같은 날이면 유지

            _data.Statistics.LastCompletionDate = today;
            _data.Statistics.BestStreak = Math.Max(_data.Statistics.BestStreak, _data.Statistics.CurrentStreak);
        }

        // 최근 30일만 유지
        var cutoff = today.AddDays(-30);
        _data.Statistics.DailyHistory.RemoveAll(s => s.Date < cutoff);
    }

    public void ToggleImportant(TodoTask task)
    {
        task.IsImportant = !task.IsImportant;
        Save();
    }

    public void TogglePinnedToday(TodoTask task)
    {
        task.IsPinnedToday = !task.IsPinnedToday;
        Save();
    }

    public void DeleteTask(TodoTask task)
    {
        _data.Tasks.Remove(task);
        Save();
    }

    public void RestoreTask(TodoTask task)
    {
        if (!_data.Tasks.Contains(task))
        {
            _data.Tasks.Add(task);
            Save();
        }
    }

    public void UpdateTask(TodoTask task)
    {
        Save();
    }

    public void ReorderTask(TodoTask task, int newIndex)
    {
        var tasks = _data.Tasks;
        var oldIndex = tasks.IndexOf(task);
        if (oldIndex < 0 || oldIndex == newIndex) return;

        tasks.RemoveAt(oldIndex);
        if (newIndex > tasks.Count) newIndex = tasks.Count;
        tasks.Insert(newIndex, task);

        // Order 값 재정렬
        for (int i = 0; i < tasks.Count; i++)
        {
            tasks[i].Order = i;
        }

        Save();
    }

    public PetStatus GetPetStatus()
    {
        // 날짜 변경 시 TodayCompleted 리셋
        if (_data.PetStatus.LastActiveDate.Date != DateTime.Today)
        {
            _data.PetStatus.LastActiveDate = DateTime.Today;
            _data.PetStatus.TodayCompleted = 0;
            _data.PetStatus.HasShownGreetingToday = false;
            Save();
        }
        return _data.PetStatus;
    }

    public void UpdatePetStatus()
    {
        Save();
    }

    public int GetTodayTaskCount()
    {
        return GetTodayTasks().Count();
    }

    public bool HasOverdueTasks()
    {
        var today = DateTime.Today;
        return _data.Tasks.Any(t => !t.IsCompleted &&
                                    t.DueDate.HasValue &&
                                    t.DueDate.Value.Date < today);
    }

    public IEnumerable<TodoTask> SearchTasks(string query, bool includeCompleted = false)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Enumerable.Empty<TodoTask>();

        var searchTerms = query.ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var tasks = _data.Tasks.Where(t =>
            searchTerms.All(term => t.Title.ToLower().Contains(term)));

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.DueDate ?? DateTime.MaxValue)
            .ThenBy(t => t.Order);
    }

    // Project Methods
    public IReadOnlyList<Project> GetProjects() => _data.Projects.Where(p => !p.IsDeleted).ToList().AsReadOnly();

    public IReadOnlyList<Project> GetDeletedProjects() => _data.Projects.Where(p => p.IsDeleted).ToList().AsReadOnly();

    public IEnumerable<TodoTask> GetProjectTasks(string projectId, bool includeCompleted = false)
    {
        var tasks = _data.Tasks.Where(t => t.ProjectId == projectId);

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.Order);
    }

    public Project AddProject(string name)
    {
        var project = new Project
        {
            Name = name,
            Order = _data.Projects.Count
        };
        _data.Projects.Add(project);
        Save();
        return project;
    }

    public void UpdateProject(Project project)
    {
        Save();
    }

    public void DeleteProject(string projectId)
    {
        var project = _data.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return;

        // Soft delete - 프로젝트 보관
        project.IsDeleted = true;
        project.DeletedAt = DateTime.Now;

        // 해당 프로젝트의 작업들도 모두 삭제
        _data.Tasks.RemoveAll(t => t.ProjectId == projectId);

        Save();
    }

    public void RestoreProject(string projectId)
    {
        var project = _data.Projects.FirstOrDefault(p => p.Id == projectId && p.IsDeleted);
        if (project == null) return;

        project.IsDeleted = false;
        project.DeletedAt = null;
        Save();
    }

    public void PermanentlyDeleteProject(string projectId)
    {
        var project = _data.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return;

        _data.Projects.Remove(project);
        Save();
    }

    public void RenameProject(string projectId, string newName)
    {
        var project = _data.Projects.FirstOrDefault(p => p.Id == projectId);
        if (project == null) return;

        project.Name = newName;
        Save();
    }

    public void ReorderProject(string draggedProjectId, string targetProjectId)
    {
        var projects = _data.Projects.Where(p => !p.IsDeleted).OrderBy(p => p.Order).ToList();
        var draggedProject = projects.FirstOrDefault(p => p.Id == draggedProjectId);
        var targetProject = projects.FirstOrDefault(p => p.Id == targetProjectId);

        if (draggedProject == null || targetProject == null) return;
        if (draggedProjectId == targetProjectId) return;

        var oldIndex = projects.IndexOf(draggedProject);
        var newIndex = projects.IndexOf(targetProject);

        projects.RemoveAt(oldIndex);
        projects.Insert(newIndex, draggedProject);

        // Order 값 재정렬
        for (int i = 0; i < projects.Count; i++)
        {
            projects[i].Order = i;
        }

        Save();
    }

    public void MoveTaskToProject(TodoTask task, string? projectId)
    {
        task.ProjectId = projectId;
        Save();
    }

    public Project? GetProjectById(string? projectId)
    {
        if (projectId == null) return null;
        return _data.Projects.FirstOrDefault(p => p.Id == projectId);
    }

    public Project? GetProjectByName(string name)
    {
        return _data.Projects.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public int GetProjectTaskCount(string projectId)
    {
        return _data.Tasks.Count(t => t.ProjectId == projectId && !t.IsCompleted);
    }

    // HashTag Methods
    public IReadOnlyList<HashTag> GetHashTags() => _data.HashTags.OrderBy(h => h.Order).ToList().AsReadOnly();

    public HashTag? GetHashTagById(string id)
    {
        return _data.HashTags.FirstOrDefault(h => h.Id == id);
    }

    public HashTag? GetHashTagByName(string name)
    {
        return _data.HashTags.FirstOrDefault(h =>
            h.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    public HashTag AddHashTag(string name, string? color = null)
    {
        var existingTag = GetHashTagByName(name);
        if (existingTag != null) return existingTag;

        var tag = new HashTag
        {
            Name = name,
            Color = color ?? HashTagColors.Palette[_data.HashTags.Count % HashTagColors.Palette.Length],
            Order = _data.HashTags.Count
        };
        _data.HashTags.Add(tag);
        Save();
        return tag;
    }

    public void UpdateHashTagColor(string hashTagId, string color)
    {
        var tag = GetHashTagById(hashTagId);
        if (tag == null) return;

        tag.Color = color;
        Save();
    }

    public void RenameHashTag(string hashTagId, string newName)
    {
        var tag = GetHashTagById(hashTagId);
        if (tag == null) return;

        tag.Name = newName;
        Save();
    }

    public void DeleteHashTag(string hashTagId)
    {
        var tag = GetHashTagById(hashTagId);
        if (tag == null) return;

        // 모든 할일에서 해당 태그 제거
        foreach (var task in _data.Tasks)
        {
            task.HashTagIds.Remove(hashTagId);
        }

        _data.HashTags.Remove(tag);
        Save();
    }

    public void HideHashTag(string hashTagId)
    {
        var tag = GetHashTagById(hashTagId);
        if (tag == null) return;

        tag.IsHidden = true;
        Save();
    }

    public void RestoreHashTag(string hashTagId)
    {
        var tag = GetHashTagById(hashTagId);
        if (tag == null) return;

        tag.IsHidden = false;
        Save();
    }

    public void AddHashTagToTask(TodoTask task, string hashTagId)
    {
        if (!task.HashTagIds.Contains(hashTagId))
        {
            task.HashTagIds.Add(hashTagId);
            Save();
        }
    }

    public void RemoveHashTagFromTask(TodoTask task, string hashTagId)
    {
        if (task.HashTagIds.Remove(hashTagId))
        {
            Save();
        }
    }

    public IEnumerable<TodoTask> GetTasksByHashTag(string hashTagId, bool includeCompleted = false)
    {
        var tasks = _data.Tasks.Where(t => t.HashTagIds.Contains(hashTagId));

        if (!includeCompleted)
            tasks = tasks.Where(t => !t.IsCompleted);

        return tasks
            .OrderBy(t => t.IsCompleted)
            .ThenByDescending(t => t.IsImportant)
            .ThenBy(t => t.Order);
    }

    public int GetHashTagTaskCount(string hashTagId)
    {
        return _data.Tasks.Count(t => !t.IsCompleted && t.HashTagIds.Contains(hashTagId));
    }

    public string? GetFirstHashTagColor(TodoTask task)
    {
        if (task.HashTagIds.Count == 0) return null;
        var tag = GetHashTagById(task.HashTagIds[0]);
        return tag?.Color;
    }

    public AppData GetAppData() => _data;

    public void ReplaceData(AppData newData)
    {
        _data = newData;
        Save();
    }

    public void DeleteAllBackups()
    {
        _storageService.DeleteAllBackups();
    }

    private void Save()
    {
        _storageService.Save(_data);
        DataChanged?.Invoke();
    }
}
