using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;

namespace SlimeTodo.ViewModels;

public class WeekViewModel : ViewModelBase
{
    private readonly TaskService _taskService;
    private readonly UndoService? _undoService;
    private readonly Action? _onTaskCompleted;
    private readonly Action? _onDataChanged;
    private readonly Action<bool>? _onShowCompletedChanged;
    private DateTime _weekStart;
    private bool _isUnscheduledExpanded = true;
    private bool _showCompleted = false;
    private bool _isSyncingShowCompleted = false;

    public WeekViewModel(TaskService taskService, UndoService? undoService = null, Action? onTaskCompleted = null, Action? onDataChanged = null, Action<bool>? onShowCompletedChanged = null)
    {
        _taskService = taskService;
        _undoService = undoService;
        _onTaskCompleted = onTaskCompleted;
        _onDataChanged = onDataChanged;
        _onShowCompletedChanged = onShowCompletedChanged;

        Days = new ObservableCollection<DayColumnViewModel>();
        UnscheduledTasks = new ObservableCollection<TaskItemViewModel>();

        PreviousWeekCommand = new RelayCommand(GoToPreviousWeek);
        NextWeekCommand = new RelayCommand(GoToNextWeek);
        TodayWeekCommand = new RelayCommand(GoToTodayWeek);
        ToggleUnscheduledCommand = new RelayCommand(() => IsUnscheduledExpanded = !IsUnscheduledExpanded);
        ToggleShowCompletedCommand = new RelayCommand(ToggleShowCompleted);

        SetToCurrentWeek();
    }

    public ObservableCollection<DayColumnViewModel> Days { get; }
    public ObservableCollection<TaskItemViewModel> UnscheduledTasks { get; }

    public DateTime WeekStart
    {
        get => _weekStart;
        private set
        {
            if (SetProperty(ref _weekStart, value))
            {
                OnPropertyChanged(nameof(WeekRangeText));
                OnPropertyChanged(nameof(IsCurrentWeek));
                LoadWeekData();
            }
        }
    }

    public DateTime WeekEnd => _weekStart.AddDays(4);

    public string WeekRangeText
    {
        get
        {
            var start = _weekStart;
            var end = WeekEnd;
            if (start.Year == end.Year && start.Month == end.Month)
            {
                return $"{start:yyyy.MM.dd} ~ {end:dd}";
            }
            else if (start.Year == end.Year)
            {
                return $"{start:yyyy.MM.dd} ~ {end:MM.dd}";
            }
            return $"{start:yyyy.MM.dd} ~ {end:yyyy.MM.dd}";
        }
    }

    public bool IsCurrentWeek
    {
        get
        {
            var today = DateTime.Today;
            return today >= _weekStart && today <= WeekEnd;
        }
    }

    public bool IsUnscheduledExpanded
    {
        get => _isUnscheduledExpanded;
        set => SetProperty(ref _isUnscheduledExpanded, value);
    }

    public int UnscheduledCount => UnscheduledTasks.Count;

    public bool ShowCompleted
    {
        get => _showCompleted;
        set
        {
            if (SetProperty(ref _showCompleted, value))
            {
                OnPropertyChanged(nameof(ShowCompletedText));
                LoadWeekData();

                // MainViewModel에 동기화 (무한 루프 방지)
                if (!_isSyncingShowCompleted)
                {
                    _onShowCompletedChanged?.Invoke(value);
                }
            }
        }
    }

    // MainViewModel에서 호출하는 동기화 메서드
    public void SyncShowCompleted(bool value)
    {
        _isSyncingShowCompleted = true;
        ShowCompleted = value;
        _isSyncingShowCompleted = false;
    }

    public string ShowCompletedText => _showCompleted ? "완료 숨기기" : "완료 보기";

    public ICommand PreviousWeekCommand { get; }
    public ICommand NextWeekCommand { get; }
    public ICommand TodayWeekCommand { get; }
    public ICommand ToggleUnscheduledCommand { get; }
    public ICommand ToggleShowCompletedCommand { get; }

    private void SetToCurrentWeek()
    {
        var today = DateTime.Today;
        var dayOfWeek = today.DayOfWeek;
        var daysToMonday = dayOfWeek == DayOfWeek.Sunday ? -6 : -(int)dayOfWeek + 1;
        WeekStart = today.AddDays(daysToMonday);
    }

    private void GoToPreviousWeek()
    {
        WeekStart = _weekStart.AddDays(-7);
    }

    private void GoToNextWeek()
    {
        WeekStart = _weekStart.AddDays(7);
    }

    private void GoToTodayWeek()
    {
        SetToCurrentWeek();
    }

    private void ToggleShowCompleted()
    {
        ShowCompleted = !ShowCompleted;
    }

    public void LoadWeekData()
    {
        LoadDays();
        LoadUnscheduledTasks();
    }

    private void LoadDays()
    {
        Days.Clear();

        var weekTasks = _taskService.GetWeekTasks(_weekStart, WeekEnd, _showCompleted).ToList();

        for (int i = 0; i < 5; i++)
        {
            var date = _weekStart.AddDays(i);
            var dayTasks = weekTasks
                .Where(t => t.DueDate?.Date == date.Date)
                .Select(t => new TaskItemViewModel(t, _taskService, _undoService, _onTaskCompleted, _onDataChanged))
                .ToList();

            var dayVm = new DayColumnViewModel(date, dayTasks, _taskService, _undoService, _onTaskCompleted, _onDataChanged);
            Days.Add(dayVm);
        }
    }

    private void LoadUnscheduledTasks()
    {
        UnscheduledTasks.Clear();

        // TO-DO list는 항상 완료된 항목 숨김 (캘린더만 완료 보기 적용)
        foreach (var task in _taskService.GetUnscheduledTasks(false))
        {
            UnscheduledTasks.Add(new TaskItemViewModel(task, _taskService, _undoService, _onTaskCompleted, _onDataChanged));
        }

        OnPropertyChanged(nameof(UnscheduledCount));
    }

    public void AssignTaskToDate(TaskItemViewModel taskVm, DateTime date)
    {
        var previousDate = taskVm.Task.DueDate;
        _undoService?.RecordDateChange(taskVm.Task, previousDate, date);
        _taskService.SetTaskDueDate(taskVm.Task, date);
        LoadWeekData();
        _onDataChanged?.Invoke();
    }

    public void RemoveTaskDate(TaskItemViewModel taskVm)
    {
        var previousDate = taskVm.Task.DueDate;
        _undoService?.RecordDateChange(taskVm.Task, previousDate, null);
        _taskService.SetTaskDueDate(taskVm.Task, null);
        LoadWeekData();
        _onDataChanged?.Invoke();
    }

    public void AddTaskToDate(string title, DateTime date)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var task = _taskService.AddTask(title);
        _undoService?.RecordCreate(task);
        _taskService.SetTaskDueDate(task, date);
        LoadWeekData();
    }
}

public class DayColumnViewModel : ViewModelBase
{
    private readonly TaskService _taskService;
    private readonly UndoService? _undoService;
    private readonly Action? _onTaskCompleted;
    private readonly Action? _onDataChanged;
    private bool _isAddingTask;
    private string _newTaskTitle = string.Empty;

    public DayColumnViewModel(DateTime date, IEnumerable<TaskItemViewModel> tasks,
        TaskService taskService, UndoService? undoService, Action? onTaskCompleted, Action? onDataChanged)
    {
        Date = date;
        _taskService = taskService;
        _undoService = undoService;
        _onTaskCompleted = onTaskCompleted;
        _onDataChanged = onDataChanged;

        Tasks = new ObservableCollection<TaskItemViewModel>(tasks);

        StartAddTaskCommand = new RelayCommand(StartAddTask);
        AddTaskCommand = new RelayCommand(AddTask, () => !string.IsNullOrWhiteSpace(NewTaskTitle));
        CancelAddTaskCommand = new RelayCommand(CancelAddTask);
    }

    public DateTime Date { get; }

    public string DayName => Date.ToString("ddd", new CultureInfo("ko-KR"));

    public string DayNumber => Date.Day.ToString();

    public bool IsToday => Date.Date == DateTime.Today;

    public bool IsPast => Date.Date < DateTime.Today;

    public ObservableCollection<TaskItemViewModel> Tasks { get; }

    public int TaskCount => Tasks.Count;

    public bool IsAddingTask
    {
        get => _isAddingTask;
        set => SetProperty(ref _isAddingTask, value);
    }

    public string NewTaskTitle
    {
        get => _newTaskTitle;
        set
        {
            if (SetProperty(ref _newTaskTitle, value))
            {
                ((RelayCommand)AddTaskCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public ICommand StartAddTaskCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand CancelAddTaskCommand { get; }

    private void StartAddTask()
    {
        IsAddingTask = true;
        NewTaskTitle = string.Empty;
    }

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(NewTaskTitle)) return;

        var task = _taskService.AddTask(NewTaskTitle.Trim());
        _undoService?.RecordCreate(task);
        _taskService.SetTaskDueDate(task, Date);

        Tasks.Add(new TaskItemViewModel(task, _taskService, _undoService, _onTaskCompleted, _onDataChanged));

        NewTaskTitle = string.Empty;
        IsAddingTask = false;

        OnPropertyChanged(nameof(TaskCount));
        _onDataChanged?.Invoke();
    }

    private void CancelAddTask()
    {
        IsAddingTask = false;
        NewTaskTitle = string.Empty;
    }

    public void AddTaskFromDrop(TodoTask task)
    {
        _taskService.SetTaskDueDate(task, Date);
        Tasks.Add(new TaskItemViewModel(task, _taskService, _undoService, _onTaskCompleted, _onDataChanged));
        OnPropertyChanged(nameof(TaskCount));
    }

    public void RemoveTask(TaskItemViewModel taskVm)
    {
        Tasks.Remove(taskVm);
        OnPropertyChanged(nameof(TaskCount));
    }
}
