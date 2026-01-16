using SlimeTodo.Models;

namespace SlimeTodo.Services;

public enum UndoActionType
{
    TaskCompleted,
    TaskUncompleted,
    TaskDeleted,
    TaskDateChanged,
    TaskImportantToggled,
    TaskPinnedToggled,
    TaskCreated
}

public class UndoAction
{
    public UndoActionType Type { get; set; }
    public TodoTask? Task { get; set; }
    public object? PreviousValue { get; set; }
    public object? NewValue { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class UndoService
{
    private readonly Stack<UndoAction> _undoStack = new();
    private readonly Stack<UndoAction> _redoStack = new();
    private readonly TaskService _taskService;
    private const int MaxUndoCount = 50;

    public event Action<string, UndoAction>? UndoPerformed;
    public event Action<string, UndoAction>? RedoPerformed;

    public UndoService(TaskService taskService)
    {
        _taskService = taskService;
    }

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    public void RecordAction(UndoAction action)
    {
        _undoStack.Push(action);
        _redoStack.Clear();

        // 최대 개수 제한
        while (_undoStack.Count > MaxUndoCount)
        {
            var items = _undoStack.ToArray();
            _undoStack.Clear();
            for (int i = items.Length - 2; i >= 0; i--)
            {
                _undoStack.Push(items[i]);
            }
        }
    }

    public void RecordComplete(TodoTask task, bool wasCompleted)
    {
        RecordAction(new UndoAction
        {
            Type = wasCompleted ? UndoActionType.TaskUncompleted : UndoActionType.TaskCompleted,
            Task = task,
            PreviousValue = wasCompleted,
            NewValue = !wasCompleted,
            Description = wasCompleted ? "완료 취소" : "완료 표시"
        });
    }

    public void RecordDateChange(TodoTask task, DateTime? previousDate, DateTime? newDate)
    {
        RecordAction(new UndoAction
        {
            Type = UndoActionType.TaskDateChanged,
            Task = task,
            PreviousValue = previousDate,
            NewValue = newDate,
            Description = GetDateChangeDescription(newDate)
        });
    }

    public void RecordImportantToggle(TodoTask task, bool wasImportant)
    {
        RecordAction(new UndoAction
        {
            Type = UndoActionType.TaskImportantToggled,
            Task = task,
            PreviousValue = wasImportant,
            NewValue = !wasImportant,
            Description = wasImportant ? "중요 표시 해제" : "중요 표시"
        });
    }

    public void RecordPinnedToggle(TodoTask task, bool wasPinned)
    {
        RecordAction(new UndoAction
        {
            Type = UndoActionType.TaskPinnedToggled,
            Task = task,
            PreviousValue = wasPinned,
            NewValue = !wasPinned,
            Description = wasPinned ? "오늘 해제" : "오늘 추가"
        });
    }

    public void RecordDelete(TodoTask task)
    {
        RecordAction(new UndoAction
        {
            Type = UndoActionType.TaskDeleted,
            Task = task,
            Description = "삭제"
        });
    }

    public void RecordCreate(TodoTask task)
    {
        RecordAction(new UndoAction
        {
            Type = UndoActionType.TaskCreated,
            Task = task,
            Description = "생성"
        });
    }

    private static string GetDateChangeDescription(DateTime? newDate)
    {
        if (!newDate.HasValue) return "날짜 없음으로 변경";

        var date = newDate.Value.Date;
        var today = DateTime.Today;

        if (date == today) return "오늘로 변경";
        if (date == today.AddDays(1)) return "내일로 변경";
        if (date == today.AddDays(-1)) return "어제로 변경";

        var dayNames = new[] { "일", "월", "화", "수", "목", "금", "토" };
        var dayOfWeek = dayNames[(int)date.DayOfWeek];

        return $"{dayOfWeek}요일로 옮겼어요";
    }

    public (bool success, string message, UndoAction? action) Undo()
    {
        if (!CanUndo) return (false, string.Empty, null);

        var action = _undoStack.Pop();
        var task = action.Task;

        if (task == null) return (false, string.Empty, null);

        string message;

        switch (action.Type)
        {
            case UndoActionType.TaskCompleted:
            case UndoActionType.TaskUncompleted:
                // 완료 상태 되돌리기
                var previousCompleted = (bool)action.PreviousValue!;
                task.IsCompleted = previousCompleted;
                task.CompletedAt = previousCompleted ? DateTime.Now : null;
                _taskService.UpdateTask(task);
                message = previousCompleted ? "완료 표시 복원" : "완료 취소 복원";
                break;

            case UndoActionType.TaskDateChanged:
                var previousDate = action.PreviousValue as DateTime?;
                task.DueDate = previousDate;
                _taskService.UpdateTask(task);
                message = GetDateChangeDescription(previousDate);
                break;

            case UndoActionType.TaskImportantToggled:
                var previousImportant = (bool)action.PreviousValue!;
                task.IsImportant = previousImportant;
                _taskService.UpdateTask(task);
                message = previousImportant ? "중요 표시 복원" : "중요 해제 복원";
                break;

            case UndoActionType.TaskPinnedToggled:
                var previousPinned = (bool)action.PreviousValue!;
                task.IsPinnedToday = previousPinned;
                _taskService.UpdateTask(task);
                message = previousPinned ? "오늘 고정 복원" : "오늘 해제 복원";
                break;

            case UndoActionType.TaskDeleted:
                _taskService.RestoreTask(task);
                message = "삭제 복원";
                break;

            case UndoActionType.TaskCreated:
                _taskService.DeleteTask(task);
                message = "생성 취소";
                break;

            default:
                return (false, string.Empty, null);
        }

        _redoStack.Push(action);
        UndoPerformed?.Invoke(message, action);
        return (true, message, action);
    }

    public (bool success, string message) Redo()
    {
        if (!CanRedo) return (false, string.Empty);

        var action = _redoStack.Pop();
        var task = action.Task;

        if (task == null) return (false, string.Empty);

        string message;

        switch (action.Type)
        {
            case UndoActionType.TaskCompleted:
            case UndoActionType.TaskUncompleted:
                var newCompleted = (bool)action.NewValue!;
                task.IsCompleted = newCompleted;
                task.CompletedAt = newCompleted ? DateTime.Now : null;
                _taskService.UpdateTask(task);
                message = newCompleted ? "완료 표시" : "완료 취소";
                break;

            case UndoActionType.TaskDateChanged:
                var newDate = action.NewValue as DateTime?;
                task.DueDate = newDate;
                _taskService.UpdateTask(task);
                message = GetDateChangeDescription(newDate);
                break;

            case UndoActionType.TaskImportantToggled:
                var newImportant = (bool)action.NewValue!;
                task.IsImportant = newImportant;
                _taskService.UpdateTask(task);
                message = newImportant ? "중요 표시" : "중요 해제";
                break;

            case UndoActionType.TaskPinnedToggled:
                var newPinned = (bool)action.NewValue!;
                task.IsPinnedToday = newPinned;
                _taskService.UpdateTask(task);
                message = newPinned ? "오늘 고정" : "오늘 해제";
                break;

            case UndoActionType.TaskDeleted:
                _taskService.DeleteTask(task);
                message = "다시 삭제";
                break;

            case UndoActionType.TaskCreated:
                _taskService.RestoreTask(task);
                message = "다시 생성";
                break;

            default:
                return (false, string.Empty);
        }

        _undoStack.Push(action);
        RedoPerformed?.Invoke(message, action);
        return (true, message);
    }
}
