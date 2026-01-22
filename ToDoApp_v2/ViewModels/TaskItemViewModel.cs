using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace SlimeTodo.ViewModels;

public class TaskItemViewModel : ViewModelBase
{
    private readonly TodoTask _task;
    private readonly TaskService _taskService;
    private readonly UndoService? _undoService;
    private readonly Action? _onCompleted;
    private readonly Action? _onDataChanged;
    private readonly Action<string, bool>? _onExpandedChanged;
    private bool _isEditing;
    private string _editingTitle = string.Empty;
    private bool _isExpanded;
    private bool _isAddingSubTask;
    private string _newSubTaskTitle = string.Empty;
    private bool _isEditingDueDate;

    public TaskItemViewModel(TodoTask task, TaskService taskService, UndoService? undoService = null, Action? onCompleted = null, Action? onDataChanged = null, Action<string, bool>? onExpandedChanged = null)
    {
        _task = task;
        _taskService = taskService;
        _undoService = undoService;
        _onCompleted = onCompleted;
        _onDataChanged = onDataChanged;
        _onExpandedChanged = onExpandedChanged;

        SubTasks = new ObservableCollection<SubTaskViewModel>(
            _task.SubTasks.OrderBy(s => s.Order).Select(s => new SubTaskViewModel(s, this)));

        ToggleCompleteCommand = new RelayCommand(ToggleComplete);
        ToggleImportantCommand = new RelayCommand(ToggleImportant);
        TogglePinnedTodayCommand = new RelayCommand(TogglePinnedToday);
        DeleteCommand = new RelayCommand(Delete);
        StartEditCommand = new RelayCommand(StartEdit);
        SaveEditCommand = new RelayCommand(SaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        ToggleExpandCommand = new RelayCommand(ToggleExpand);
        StartAddSubTaskCommand = new RelayCommand(StartAddSubTask);
        AddSubTaskCommand = new RelayCommand(AddSubTask, () => !string.IsNullOrWhiteSpace(NewSubTaskTitle));
        CancelAddSubTaskCommand = new RelayCommand(CancelAddSubTask);
        SetRecurrenceCommand = new RelayCommand<RecurrenceType>(SetRecurrence);
        SetReminderCommand = new RelayCommand<string>(SetReminder);
        ClearReminderCommand = new RelayCommand(ClearReminder);
        StartEditDueDateCommand = new RelayCommand(StartEditDueDate);
        ClearDueDateCommand = new RelayCommand(ClearDueDate);
    }

    public string Id => _task.Id;

    public string Title
    {
        get => _task.Title;
        set
        {
            if (_task.Title != value)
            {
                _task.Title = value;
                _taskService.UpdateTask(_task);
                OnPropertyChanged();
                // 제목 변경 시에는 전체 목록 새로고침 불필요 (메모 패널 닫힘 방지)
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditingTitle
    {
        get => _editingTitle;
        set => SetProperty(ref _editingTitle, value);
    }

    public bool IsCompleted
    {
        get => _task.IsCompleted;
        set
        {
            if (_task.IsCompleted != value)
            {
                _task.IsCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsImportant
    {
        get => _task.IsImportant;
        set
        {
            if (_task.IsImportant != value)
            {
                _task.IsImportant = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsPinnedToday
    {
        get => _task.IsPinnedToday;
        set
        {
            if (_task.IsPinnedToday != value)
            {
                _task.IsPinnedToday = value;
                OnPropertyChanged();
            }
        }
    }

    // 오늘 할일인지 여부 (오늘 마감일 또는 연체)
    public bool IsDueToday => _task.DueDate.HasValue && _task.DueDate.Value.Date <= DateTime.Today && !_task.IsCompleted;

    public DateTime? DueDate
    {
        get => _task.DueDate;
        set
        {
            if (_task.DueDate != value)
            {
                _task.DueDate = value;
                _taskService.UpdateTask(_task);
                OnPropertyChanged();
                OnPropertyChanged(nameof(DueDateText));
                OnPropertyChanged(nameof(IsOverdue));
                OnPropertyChanged(nameof(IsDueToday));
                OnPropertyChanged(nameof(RecurrenceText));
                _onDataChanged?.Invoke();
            }
        }
    }

    public bool IsEditingDueDate
    {
        get => _isEditingDueDate;
        set => SetProperty(ref _isEditingDueDate, value);
    }

    public string DueDateText
    {
        get
        {
            if (!_task.DueDate.HasValue) return string.Empty;

            var date = _task.DueDate.Value.Date;
            var today = DateTime.Today;

            if (date == today) return "오늘";
            if (date == today.AddDays(1)) return "내일";
            if (date < today) return $"연체 ({(today - date).Days}일)";

            return date.ToString("MM/dd");
        }
    }

    public bool IsOverdue => _task.DueDate.HasValue && _task.DueDate.Value.Date < DateTime.Today && !_task.IsCompleted;

    public RecurrenceType Recurrence
    {
        get => _task.Recurrence;
        set
        {
            if (_task.Recurrence != value)
            {
                _taskService.SetRecurrence(_task, value);
                OnPropertyChanged();
                OnPropertyChanged(nameof(RecurrenceText));
                OnPropertyChanged(nameof(HasRecurrence));
            }
        }
    }

    public bool HasRecurrence => _task.Recurrence != RecurrenceType.None;

    public bool HasReminder => _task.ReminderTime.HasValue && !_task.ReminderNotified;

    // Notes
    public string Notes
    {
        get => _task.Notes;
        set
        {
            if (_task.Notes != value)
            {
                _task.Notes = value;
                _task.NotesModifiedAt = DateTime.Now;
                _taskService.UpdateTask(_task);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasNotes));
                // 메모 변경 시에는 전체 목록 새로고침 불필요
            }
        }
    }

    public bool HasNotes => !string.IsNullOrWhiteSpace(_task.Notes);

    public string ReminderText
    {
        get
        {
            if (!_task.ReminderTime.HasValue) return string.Empty;
            var reminder = _task.ReminderTime.Value;
            if (reminder.Date == DateTime.Today)
                return $"오늘 {reminder:HH:mm}";
            if (reminder.Date == DateTime.Today.AddDays(1))
                return $"내일 {reminder:HH:mm}";
            return reminder.ToString("MM/dd HH:mm");
        }
    }

    public string RecurrenceText
    {
        get
        {
            if (_task.Recurrence == RecurrenceType.None) return string.Empty;

            var recurrenceLabel = _task.Recurrence switch
            {
                RecurrenceType.Daily => "매일",
                RecurrenceType.Weekly => "매주",
                RecurrenceType.Monthly => "매월",
                RecurrenceType.Yearly => "매년",
                _ => string.Empty
            };

            if (_task.DueDate.HasValue)
            {
                var date = _task.DueDate.Value;
                var dateStr = date.Date == DateTime.Today ? "Today" :
                              date.Date == DateTime.Today.AddDays(1) ? "Tomorrow" :
                              date.ToString("MM/dd");
                return $"{dateStr}({recurrenceLabel})";
            }

            return recurrenceLabel;
        }
    }

    public TodoTask Task => _task;

    public ObservableCollection<SubTaskViewModel> SubTasks { get; }

    public bool HasSubTasks => _task.SubTasks.Count > 0;

    // HashTag Properties
    public bool HasHashTags => _task.HashTagIds.Count > 0;

    public string HashTagsText
    {
        get
        {
            if (_task.HashTagIds.Count == 0) return string.Empty;
            var tags = _task.HashTagIds
                .Select(id => _taskService.GetHashTagById(id))
                .Where(h => h != null)
                .Select(h => $"#{h!.Name}");
            return string.Join(" ", tags);
        }
    }

    public List<TaskHashTagInfo> HashTagList
    {
        get
        {
            return _task.HashTagIds
                .Select(id => _taskService.GetHashTagById(id))
                .Where(h => h != null)
                .Select(h => new TaskHashTagInfo
                {
                    Id = h!.Id,
                    Name = h.Name,
                    Color = h.Color,
                    RemoveCommand = new RelayCommand(() => RemoveHashTag(h.Id))
                })
                .ToList();
        }
    }

    public void RemoveHashTag(string hashTagId)
    {
        _taskService.RemoveHashTagFromTask(_task, hashTagId);
        OnPropertyChanged(nameof(HasHashTags));
        OnPropertyChanged(nameof(HashTagsText));
        OnPropertyChanged(nameof(HashTagList));
        OnPropertyChanged(nameof(FirstHashTagColor));
        _onDataChanged?.Invoke();
    }

    public string FirstHashTagColor
    {
        get
        {
            var color = _taskService.GetFirstHashTagColor(_task);
            return color ?? "#9B59B6"; // 기본 보라색
        }
    }

    public string SubTaskProgress => HasSubTasks
        ? $"{_task.SubTasks.Count(s => s.IsCompleted)}/{_task.SubTasks.Count}"
        : string.Empty;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (SetProperty(ref _isExpanded, value))
            {
                _onExpandedChanged?.Invoke(_task.Id, value);
            }
        }
    }

    public bool IsAddingSubTask
    {
        get => _isAddingSubTask;
        set => SetProperty(ref _isAddingSubTask, value);
    }

    public string NewSubTaskTitle
    {
        get => _newSubTaskTitle;
        set
        {
            if (SetProperty(ref _newSubTaskTitle, value))
            {
                ((RelayCommand)AddSubTaskCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand ToggleCompleteCommand { get; }
    public ICommand ToggleImportantCommand { get; }
    public ICommand TogglePinnedTodayCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }
    public ICommand ToggleExpandCommand { get; }
    public ICommand StartAddSubTaskCommand { get; }
    public ICommand AddSubTaskCommand { get; }
    public ICommand CancelAddSubTaskCommand { get; }
    public ICommand SetRecurrenceCommand { get; }
    public ICommand SetReminderCommand { get; }
    public ICommand ClearReminderCommand { get; }
    public ICommand StartEditDueDateCommand { get; }
    public ICommand ClearDueDateCommand { get; }

    public void SetRecurrence(RecurrenceType type)
    {
        Recurrence = type;
        _onDataChanged?.Invoke();
    }

    private void SetReminder(string? parameter)
    {
        if (string.IsNullOrEmpty(parameter)) return;

        DateTime reminderTime;
        if (parameter == "tomorrow")
        {
            reminderTime = DateTime.Today.AddDays(1).AddHours(9);
        }
        else if (int.TryParse(parameter, out int minutes))
        {
            reminderTime = DateTime.Now.AddMinutes(minutes);
        }
        else
        {
            return;
        }

        _task.ReminderTime = reminderTime;
        _task.ReminderNotified = false;
        _taskService.UpdateTask(_task);
        OnPropertyChanged(nameof(HasReminder));
        OnPropertyChanged(nameof(ReminderText));
        _onDataChanged?.Invoke();
    }

    private void ClearReminder()
    {
        _task.ReminderTime = null;
        _task.ReminderNotified = false;
        _taskService.UpdateTask(_task);
        OnPropertyChanged(nameof(HasReminder));
        OnPropertyChanged(nameof(ReminderText));
        _onDataChanged?.Invoke();
    }

    private void StartEditDueDate()
    {
        IsEditingDueDate = true;
    }

    public void FinishEditDueDate()
    {
        IsEditingDueDate = false;
    }

    private void ClearDueDate()
    {
        DueDate = null;
        IsEditingDueDate = false;
    }

    private void ToggleComplete()
    {
        var wasCompleted = _task.IsCompleted;
        _undoService?.RecordComplete(_task, wasCompleted);

        _taskService.ToggleComplete(_task);
        IsCompleted = _task.IsCompleted;

        if (_task.IsCompleted)
        {
            _onCompleted?.Invoke();
        }
        else
        {
            _onDataChanged?.Invoke();
        }
    }

    private void ToggleImportant()
    {
        var wasImportant = _task.IsImportant;
        _undoService?.RecordImportantToggle(_task, wasImportant);

        _taskService.ToggleImportant(_task);
        IsImportant = _task.IsImportant;
        _onDataChanged?.Invoke();
    }

    private void TogglePinnedToday()
    {
        var wasPinned = _task.IsPinnedToday;
        _undoService?.RecordPinnedToggle(_task, wasPinned);

        _taskService.TogglePinnedToday(_task);
        IsPinnedToday = _task.IsPinnedToday;
        _onDataChanged?.Invoke();
    }

    private void Delete()
    {
        _undoService?.RecordDelete(_task);
        _taskService.DeleteTask(_task);
        _onDataChanged?.Invoke();
    }

    private void StartEdit()
    {
        EditingTitle = Title;
        IsEditing = true;
    }

    private void SaveEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditingTitle))
        {
            // 해시태그 추출 및 처리
            var (cleanTitle, hashTags) = ExtractHashTags(EditingTitle.Trim());

            if (!string.IsNullOrWhiteSpace(cleanTitle) && cleanTitle != Title)
            {
                Title = cleanTitle;
            }

            // 해시태그 추가
            foreach (var tagName in hashTags)
            {
                var existingTag = _taskService.GetHashTagByName(tagName);
                if (existingTag != null)
                {
                    _taskService.AddHashTagToTask(_task, existingTag.Id);
                }
                else
                {
                    // 새 해시태그 생성
                    var newTag = _taskService.AddHashTag(tagName);
                    _taskService.AddHashTagToTask(_task, newTag.Id);
                }
            }

            _taskService.UpdateTask(_task);
            OnPropertyChanged(nameof(HasHashTags));
            OnPropertyChanged(nameof(HashTagsText));
            OnPropertyChanged(nameof(HashTagList));
            OnPropertyChanged(nameof(FirstHashTagColor));
        }
        IsEditing = false;
        _onDataChanged?.Invoke();
    }

    private (string cleanTitle, List<string> hashTags) ExtractHashTags(string input)
    {
        var hashTags = new List<string>();
        var regex = new System.Text.RegularExpressions.Regex(@"#(\S+)");
        var matches = regex.Matches(input);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var tagName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tagName) && !hashTags.Contains(tagName))
            {
                hashTags.Add(tagName);
            }
        }

        var cleanTitle = regex.Replace(input, "").Trim();
        // 연속 공백 제거
        cleanTitle = System.Text.RegularExpressions.Regex.Replace(cleanTitle, @"\s+", " ");

        return (cleanTitle, hashTags);
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditingTitle = Title;
    }

    // 해시태그 추가 (메뉴/버튼용)
    public void AddHashTagByName(string tagName)
    {
        if (string.IsNullOrWhiteSpace(tagName)) return;

        var existingTag = _taskService.GetHashTagByName(tagName);
        if (existingTag != null)
        {
            _taskService.AddHashTagToTask(_task, existingTag.Id);
        }
        else
        {
            var newTag = _taskService.AddHashTag(tagName);
            _taskService.AddHashTagToTask(_task, newTag.Id);
        }

        OnPropertyChanged(nameof(HasHashTags));
        OnPropertyChanged(nameof(HashTagsText));
        OnPropertyChanged(nameof(HashTagList));
        OnPropertyChanged(nameof(FirstHashTagColor));
        OnPropertyChanged(nameof(AvailableHashTags));
        _onDataChanged?.Invoke();
    }

    public List<AvailableHashTag> AvailableHashTags
    {
        get
        {
            return _taskService.GetHashTags()
                .Where(h => !h.IsHidden && !_task.HashTagIds.Contains(h.Id))
                .Select(h => new AvailableHashTag
                {
                    Id = h.Id,
                    Name = h.Name,
                    Color = h.Color,
                    AddCommand = new RelayCommand(() =>
                    {
                        _taskService.AddHashTagToTask(_task, h.Id);
                        OnPropertyChanged(nameof(HasHashTags));
                        OnPropertyChanged(nameof(HashTagsText));
                        OnPropertyChanged(nameof(HashTagList));
                        OnPropertyChanged(nameof(FirstHashTagColor));
                        OnPropertyChanged(nameof(AvailableHashTags));
                        _onDataChanged?.Invoke();
                    })
                })
                .ToList();
        }
    }

    private void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    private void StartAddSubTask()
    {
        IsExpanded = true;
        IsAddingSubTask = true;
        NewSubTaskTitle = string.Empty;
    }

    private void AddSubTask()
    {
        if (string.IsNullOrWhiteSpace(NewSubTaskTitle)) return;

        var subTask = new SubTask
        {
            Title = NewSubTaskTitle.Trim(),
            Order = _task.SubTasks.Count
        };

        _task.SubTasks.Add(subTask);
        SubTasks.Add(new SubTaskViewModel(subTask, this));
        _taskService.UpdateTask(_task);

        NewSubTaskTitle = string.Empty;
        IsAddingSubTask = false;

        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    private void CancelAddSubTask()
    {
        IsAddingSubTask = false;
        NewSubTaskTitle = string.Empty;
    }

    public void DeleteSubTask(SubTaskViewModel subTaskVm)
    {
        _task.SubTasks.Remove(subTaskVm.SubTask);
        SubTasks.Remove(subTaskVm);
        _taskService.UpdateTask(_task);

        OnPropertyChanged(nameof(HasSubTasks));
        OnPropertyChanged(nameof(SubTaskProgress));
    }

    public void UpdateSubTaskCompletion()
    {
        _taskService.UpdateTask(_task);
        OnPropertyChanged(nameof(SubTaskProgress));
    }
}

public class SubTaskViewModel : ViewModelBase
{
    private readonly SubTask _subTask;
    private readonly TaskItemViewModel _parent;
    private bool _isEditing;
    private string _editingTitle = string.Empty;

    public SubTaskViewModel(SubTask subTask, TaskItemViewModel parent)
    {
        _subTask = subTask;
        _parent = parent;

        ToggleCompleteCommand = new RelayCommand(ToggleComplete);
        DeleteCommand = new RelayCommand(Delete);
        StartEditCommand = new RelayCommand(StartEdit);
        SaveEditCommand = new RelayCommand(SaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
    }

    public SubTask SubTask => _subTask;

    public string Title
    {
        get => _subTask.Title;
        private set
        {
            if (_subTask.Title != value)
            {
                _subTask.Title = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsCompleted
    {
        get => _subTask.IsCompleted;
        set
        {
            if (_subTask.IsCompleted != value)
            {
                _subTask.IsCompleted = value;
                OnPropertyChanged();
            }
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditingTitle
    {
        get => _editingTitle;
        set => SetProperty(ref _editingTitle, value);
    }

    public ICommand ToggleCompleteCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    private void ToggleComplete()
    {
        IsCompleted = !IsCompleted;
        _parent.UpdateSubTaskCompletion();
    }

    private void Delete()
    {
        _parent.DeleteSubTask(this);
    }

    private void StartEdit()
    {
        EditingTitle = Title;
        IsEditing = true;
    }

    private void SaveEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditingTitle))
        {
            Title = EditingTitle.Trim();
            _parent.UpdateSubTaskCompletion(); // 저장
        }
        IsEditing = false;
    }

    private void CancelEdit()
    {
        IsEditing = false;
        EditingTitle = string.Empty;
    }
}

public class TaskHashTagInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#9B59B6";
    public ICommand? RemoveCommand { get; set; }
}

public class AvailableHashTag
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#9B59B6";
    public ICommand? AddCommand { get; set; }
}
