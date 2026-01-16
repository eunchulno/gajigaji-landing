using System.IO;
using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace SlimeTodo.ViewModels;

public class MainViewModel : ViewModelBase, IDisposable
{
    private readonly StorageService _storageService;
    private readonly TaskService _taskService;
    private readonly PetService _petService;
    private readonly UndoService _undoService;
    private readonly DispatcherTimer _excitedTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Random _random = new();
    private bool _disposed;

    private ViewType _currentView = ViewType.Today;
    private string _quickAddText = string.Empty;
    private PetMood _petMood = PetMood.Normal;
    private string _petMessage = string.Empty;
    private bool _showMessage;
    private int _previousLevel;
    private bool _showCompleted;
    private int _completionStreak;
    private DateTime _lastCompletionTime = DateTime.MinValue;
    private readonly TimeSpan _streakTimeout = TimeSpan.FromMinutes(2);
    private bool _isSearchMode;
    private string _searchText = string.Empty;

    // Projects
    private string? _selectedProjectId;
    private bool _isAddingProject;
    private string _newProjectName = string.Empty;

    // HashTag Filter
    private string? _selectedHashTagId;

    // Toast
    private bool _showToast;
    private string _toastMessage = string.Empty;
    private UndoAction? _lastUndoAction;

    // 서브할일 확장 상태 저장 (Task ID 기반)
    private readonly HashSet<string> _expandedTaskIds = new();

    // Help Modal
    private bool _showHelpModal;

    // Quick Add Inline
    private bool _isQuickAddVisible;

    // HashTag Autocomplete
    private ObservableCollection<HashTagSuggestion> _hashTagSuggestions = new();
    private int _selectedSuggestionIndex = -1;
    private bool _showHashTagSuggestions;

    // Section Collapse
    private bool _isProjectsExpanded = true;
    private bool _isHashTagsExpanded = true;

    // Hidden HashTags
    private bool _showHiddenHashTags;

    // Delete Confirmation
    private bool _showDeleteHashTagConfirm;
    private string? _pendingDeleteHashTagId;
    private string _pendingDeleteHashTagName = string.Empty;

    // Shortcut Hints (Ctrl 키 누름 상태)
    private bool _showShortcuts;

    public MainViewModel()
    {
        _storageService = new StorageService();
        _taskService = new TaskService(_storageService);
        _petService = new PetService(_taskService);
        _undoService = new UndoService(_taskService);

        _taskService.DataChanged += OnDataChanged;
        _undoService.UndoPerformed += OnUndoPerformed;

        Tasks = new ObservableCollection<TaskItemViewModel>();
        Projects = new ObservableCollection<ProjectViewModel>();
        HashTags = new ObservableCollection<HashTagViewModel>();

        // Commands
        NavigateCommand = new RelayCommand(Navigate);
        AddTaskCommand = new RelayCommand(AddTask, () => !string.IsNullOrWhiteSpace(QuickAddText));
        ClearQuickAddCommand = new RelayCommand(() => QuickAddText = string.Empty);
        ToggleShowCompletedCommand = new RelayCommand(ToggleShowCompleted);
        CloseSearchCommand = new RelayCommand(CloseSearch);
        PetClickCommand = new RelayCommand(OnPetClicked);
        UndoCommand = new RelayCommand(PerformUndo, () => _undoService.CanUndo);
        RedoCommand = new RelayCommand(PerformRedo, () => _undoService.CanRedo);
        ShowHelpCommand = new RelayCommand(() => ShowHelpModal = true);
        CloseHelpCommand = new RelayCommand(() => ShowHelpModal = false);
        ToggleQuickAddCommand = new RelayCommand(ToggleQuickAdd);
        HideToastCommand = new RelayCommand(() => ShowToast = false);
        ExportCommand = new RelayCommand(ExportData);
        ImportCommand = new RelayCommand(ImportData);
        OpenBackupFolderCommand = new RelayCommand(OpenBackupFolder);
        ResetDataCommand = new RelayCommand(ResetData);

        // Project Commands
        StartAddProjectCommand = new RelayCommand(() => { IsAddingProject = true; NewProjectName = string.Empty; });
        AddProjectCommand = new RelayCommand(AddProject, () => !string.IsNullOrWhiteSpace(NewProjectName));
        CancelAddProjectCommand = new RelayCommand(() => { IsAddingProject = false; NewProjectName = string.Empty; });
        SelectProjectCommand = new RelayCommand<string>(id => SelectProject(id));

        // Section Toggle Commands
        ToggleProjectsExpandedCommand = new RelayCommand(() => { IsProjectsExpanded = !IsProjectsExpanded; SaveUISettings(); });
        ToggleHashTagsExpandedCommand = new RelayCommand(() => { IsHashTagsExpanded = !IsHashTagsExpanded; SaveUISettings(); });

        // Hidden HashTag Commands
        ToggleShowHiddenHashTagsCommand = new RelayCommand(() => { ShowHiddenHashTags = !ShowHiddenHashTags; LoadHashTags(); });
        ConfirmDeleteHashTagCommand = new RelayCommand(ExecuteDeleteHashTag);
        CancelDeleteHashTagCommand = new RelayCommand(() => { ShowDeleteHashTagConfirm = false; _pendingDeleteHashTagId = null; });

        // Timer for returning from Excited mood
        _excitedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _excitedTimer.Tick += (s, e) =>
        {
            _excitedTimer.Stop();
            ShowMessage = false;
            PetMood = PetMood.Normal;
            UpdatePetMood();
        };

        // Toast timer (2초 후 자동 숨김)
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _toastTimer.Tick += (s, e) =>
        {
            _toastTimer.Stop();
            ShowToast = false;
        };

        _previousLevel = _petService.GetStatus().Level;

        // Initial load
        LoadUISettings();
        LoadTasks();
        LoadProjects();
        LoadHashTags();

        // 앱 시작 인사 (기억 기반)
        ShowAppStartGreeting();
    }

    private void ShowAppStartGreeting()
    {
        var (message, shouldShow) = _petService.GetAppStartGreeting();
        if (shouldShow && message != null)
        {
            PetMessage = message;
            PetMood = PetMood.Normal;
            ShowMessage = true;

            _excitedTimer.Stop();
            _excitedTimer.Start();
        }
        else
        {
            UpdatePetMood();
            if (PetMood != PetMood.Worried)
            {
                ShowMessage = false;
            }
        }
    }

    public ObservableCollection<TaskItemViewModel> Tasks { get; }
    public ObservableCollection<ProjectViewModel> Projects { get; }
    public ObservableCollection<HashTagViewModel> HashTags { get; }

    public ViewType CurrentView
    {
        get => _currentView;
        set
        {
            if (SetProperty(ref _currentView, value))
            {
                LoadTasks();
                OnPropertyChanged(nameof(ViewTitle));
                OnPropertyChanged(nameof(IsInboxSelected));
                OnPropertyChanged(nameof(IsTodaySelected));
                OnPropertyChanged(nameof(IsWeekSelected));
                OnPropertyChanged(nameof(IsUpcomingSelected));
            }
        }
    }

    public string ViewTitle
    {
        get
        {
            if (IsSearchMode) return "검색 결과";
            if (SelectedProjectId != null)
            {
                var project = _taskService.GetProjectById(SelectedProjectId);
                return project?.Name ?? "프로젝트";
            }
            if (SelectedHashTagId != null)
            {
                var hashTag = _taskService.GetHashTagById(SelectedHashTagId);
                return hashTag != null ? $"#{hashTag.Name}" : "해시태그";
            }
            return CurrentView switch
            {
                ViewType.Inbox => "할일",
                ViewType.Today => "오늘",
                ViewType.Week => "주간달력",
                ViewType.Upcoming => "다음",
                _ => "오늘"
            };
        }
    }

    // HashTag Filter
    public string? SelectedHashTagId
    {
        get => _selectedHashTagId;
        set
        {
            if (SetProperty(ref _selectedHashTagId, value))
            {
                // 해시태그 선택 시 Week 뷰가 아닌 일반 뷰로 전환
                if (value != null && _currentView == ViewType.Week)
                {
                    _currentView = ViewType.Inbox;
                    OnPropertyChanged(nameof(CurrentView));
                }
                OnPropertyChanged(nameof(IsInboxSelected));
                OnPropertyChanged(nameof(IsTodaySelected));
                OnPropertyChanged(nameof(IsWeekSelected));
                OnPropertyChanged(nameof(IsUpcomingSelected));
                OnPropertyChanged(nameof(ViewTitle));
                UpdateHashTagSelection();
                LoadTasks();
            }
        }
    }

    public bool IsHashTagSelected => SelectedHashTagId != null;

    public bool IsInboxSelected => CurrentView == ViewType.Inbox && SelectedProjectId == null && SelectedHashTagId == null;
    public bool IsTodaySelected => CurrentView == ViewType.Today && SelectedProjectId == null && SelectedHashTagId == null;
    public bool IsWeekSelected => CurrentView == ViewType.Week && SelectedProjectId == null && SelectedHashTagId == null;
    public bool IsUpcomingSelected => CurrentView == ViewType.Upcoming && SelectedProjectId == null && SelectedHashTagId == null;
    public bool IsProjectSelected => SelectedProjectId != null;

    // Project Properties
    public string? SelectedProjectId
    {
        get => _selectedProjectId;
        set
        {
            if (SetProperty(ref _selectedProjectId, value))
            {
                // 프로젝트 선택 시 해시태그 선택 해제
                if (value != null)
                {
                    _selectedHashTagId = null;
                    OnPropertyChanged(nameof(SelectedHashTagId));
                    OnPropertyChanged(nameof(IsHashTagSelected));
                    UpdateHashTagSelection();
                }
                // 프로젝트 선택 시 Week 뷰가 아닌 일반 뷰로 전환 (TaskListView 표시를 위해)
                if (value != null && _currentView == ViewType.Week)
                {
                    _currentView = ViewType.Inbox;
                    OnPropertyChanged(nameof(CurrentView));
                }
                OnPropertyChanged(nameof(IsProjectSelected));
                OnPropertyChanged(nameof(IsInboxSelected));
                OnPropertyChanged(nameof(IsTodaySelected));
                OnPropertyChanged(nameof(IsWeekSelected));
                OnPropertyChanged(nameof(IsUpcomingSelected));
                OnPropertyChanged(nameof(ViewTitle));
                UpdateProjectSelection();
                LoadTasks();
            }
        }
    }

    public bool IsAddingProject
    {
        get => _isAddingProject;
        set => SetProperty(ref _isAddingProject, value);
    }

    public string NewProjectName
    {
        get => _newProjectName;
        set
        {
            if (SetProperty(ref _newProjectName, value))
            {
                ((RelayCommand)AddProjectCommand).RaiseCanExecuteChanged();
            }
        }
    }

    public bool ShowCompleted
    {
        get => _showCompleted;
        set
        {
            if (SetProperty(ref _showCompleted, value))
            {
                LoadTasks();
            }
        }
    }

    public string ShowCompletedText => ShowCompleted ? "완료 숨기기" : "완료 보기";

    public bool IsSearchMode
    {
        get => _isSearchMode;
        set
        {
            if (SetProperty(ref _isSearchMode, value))
            {
                if (!value)
                {
                    SearchText = string.Empty;
                }
                OnPropertyChanged(nameof(ViewTitle));
            }
        }
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                LoadTasks();
            }
        }
    }

    public string QuickAddText
    {
        get => _quickAddText;
        set
        {
            if (SetProperty(ref _quickAddText, value))
            {
                ((RelayCommand)AddTaskCommand).RaiseCanExecuteChanged();
                UpdateHashTagSuggestions(value);
            }
        }
    }

    public PetMood PetMood
    {
        get => _petMood;
        set => SetProperty(ref _petMood, value);
    }

    public string PetMessage
    {
        get => _petMessage;
        set => SetProperty(ref _petMessage, value);
    }

    public bool ShowMessage
    {
        get => _showMessage;
        set => SetProperty(ref _showMessage, value);
    }

    // Toast Properties
    public bool ShowToast
    {
        get => _showToast;
        set => SetProperty(ref _showToast, value);
    }

    public string ToastMessage
    {
        get => _toastMessage;
        set => SetProperty(ref _toastMessage, value);
    }

    // Help Modal
    public bool ShowHelpModal
    {
        get => _showHelpModal;
        set => SetProperty(ref _showHelpModal, value);
    }

    // Shortcut Hints (Ctrl 키 누름 상태)
    public bool ShowShortcuts
    {
        get => _showShortcuts;
        set => SetProperty(ref _showShortcuts, value);
    }

    // Quick Add Inline
    public bool IsQuickAddVisible
    {
        get => _isQuickAddVisible;
        set => SetProperty(ref _isQuickAddVisible, value);
    }

    // HashTag Autocomplete Properties
    public ObservableCollection<HashTagSuggestion> HashTagSuggestions
    {
        get => _hashTagSuggestions;
        set => SetProperty(ref _hashTagSuggestions, value);
    }

    public int SelectedSuggestionIndex
    {
        get => _selectedSuggestionIndex;
        set => SetProperty(ref _selectedSuggestionIndex, value);
    }

    public bool ShowHashTagSuggestions
    {
        get => _showHashTagSuggestions;
        set => SetProperty(ref _showHashTagSuggestions, value);
    }

    // Section Collapse Properties
    public bool IsProjectsExpanded
    {
        get => _isProjectsExpanded;
        set => SetProperty(ref _isProjectsExpanded, value);
    }

    public bool IsHashTagsExpanded
    {
        get => _isHashTagsExpanded;
        set => SetProperty(ref _isHashTagsExpanded, value);
    }

    // Hidden HashTags Properties
    public bool ShowHiddenHashTags
    {
        get => _showHiddenHashTags;
        set => SetProperty(ref _showHiddenHashTags, value);
    }

    public bool HasHiddenHashTags => _taskService.GetHashTags().Any(h => h.IsHidden);

    // Delete Confirmation Properties
    public bool ShowDeleteHashTagConfirm
    {
        get => _showDeleteHashTagConfirm;
        set => SetProperty(ref _showDeleteHashTagConfirm, value);
    }

    public string PendingDeleteHashTagName
    {
        get => _pendingDeleteHashTagName;
        set => SetProperty(ref _pendingDeleteHashTagName, value);
    }

    public int PetLevel => _petService.GetStatus().Level;

    public int TodayCompleted => _petService.GetStatus().TodayCompleted;

    public int TodayTotal
    {
        get
        {
            var incompleteTodayCount = _taskService.GetTodayTasks().Count();
            return incompleteTodayCount + TodayCompleted;
        }
    }

    public string TodayProgressText => $"오늘: {TodayCompleted}/{TodayTotal}";

    public string TodayProgressDots
    {
        get
        {
            int total = TodayTotal;
            int completed = TodayCompleted;
            if (total == 0) return string.Empty;

            var filled = new string('●', Math.Min(completed, 5));
            var empty = new string('○', Math.Min(Math.Max(0, total - completed), 5 - filled.Length));
            return filled + empty;
        }
    }

    public string CurrentDate => DateTime.Today.ToString("yyyy.MM.dd");

    // 이번 주 평일 (월~금) 날짜 정보
    public WeekDayInfo[] WeekDays
    {
        get
        {
            var today = DateTime.Today;
            var dayOfWeek = today.DayOfWeek;
            var daysToMonday = dayOfWeek == DayOfWeek.Sunday ? -6 : -(int)dayOfWeek + 1;
            var monday = today.AddDays(daysToMonday);

            var days = new WeekDayInfo[5];
            for (int i = 0; i < 5; i++)
            {
                var date = monday.AddDays(i);
                days[i] = new WeekDayInfo
                {
                    Day = date.Day,
                    IsToday = date.Date == today
                };
            }
            return days;
        }
    }

    public ICommand NavigateCommand { get; }
    public ICommand AddTaskCommand { get; }
    public ICommand ClearQuickAddCommand { get; }
    public ICommand ToggleShowCompletedCommand { get; }
    public ICommand CloseSearchCommand { get; }
    public ICommand PetClickCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ShowHelpCommand { get; }
    public ICommand CloseHelpCommand { get; }
    public ICommand ToggleQuickAddCommand { get; }
    public ICommand HideToastCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand ImportCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand ResetDataCommand { get; }

    // Project Commands
    public ICommand StartAddProjectCommand { get; }
    public ICommand AddProjectCommand { get; }
    public ICommand CancelAddProjectCommand { get; }
    public ICommand SelectProjectCommand { get; }

    // Section Toggle Commands
    public ICommand ToggleProjectsExpandedCommand { get; }
    public ICommand ToggleShowHiddenHashTagsCommand { get; }
    public ICommand ConfirmDeleteHashTagCommand { get; }
    public ICommand CancelDeleteHashTagCommand { get; }
    public ICommand ToggleHashTagsExpandedCommand { get; }

    public event Action? RequestQuickAddFocus;
    public event Action? RequestSearchFocus;

    public void FocusQuickAdd()
    {
        RequestQuickAddFocus?.Invoke();
    }

    public void OpenSearch()
    {
        // QuickAdd가 열려있으면 닫기
        if (IsQuickAddVisible)
        {
            IsQuickAddVisible = false;
            QuickAddText = string.Empty;
        }
        IsSearchMode = true;
        RequestSearchFocus?.Invoke();
    }

    public void ToggleSearch()
    {
        if (IsSearchMode)
        {
            CloseSearch();
        }
        else
        {
            OpenSearch();
        }
    }

    private void CloseSearch()
    {
        IsSearchMode = false;
        LoadTasks();
    }

    private void Navigate(object? parameter)
    {
        if (parameter is string viewName)
        {
            // 일반 뷰로 이동할 때는 프로젝트/해시태그 선택 해제
            _selectedProjectId = null;
            _selectedHashTagId = null;
            OnPropertyChanged(nameof(SelectedProjectId));
            OnPropertyChanged(nameof(SelectedHashTagId));
            OnPropertyChanged(nameof(IsProjectSelected));
            OnPropertyChanged(nameof(IsHashTagSelected));
            UpdateProjectSelection();
            UpdateHashTagSelection();
            CurrentView = viewName switch
            {
                "Inbox" => ViewType.Inbox,
                "Today" => ViewType.Today,
                "Week" => ViewType.Week,
                "Upcoming" => ViewType.Upcoming,
                _ => ViewType.Today
            };
        }
    }

    public TaskService GetTaskService() => _taskService;
    public UndoService GetUndoService() => _undoService;

    private void AddTask()
    {
        if (string.IsNullOrWhiteSpace(QuickAddText)) return;

        // 프로젝트 선택 중이면 해당 프로젝트에 추가
        var task = _taskService.AddTask(QuickAddText, SelectedProjectId);
        _undoService.RecordCreate(task);

        // 오늘 탭에서 생성 시 오늘 날짜로 설정 (프로젝트 선택 중이 아닐 때만)
        if (CurrentView == ViewType.Today && SelectedProjectId == null && !task.DueDate.HasValue)
        {
            _taskService.SetTaskDueDate(task, DateTime.Today);
        }

        // 해시태그 뷰에서 생성 시 해당 해시태그 자동 추가
        if (SelectedHashTagId != null && !task.HashTagIds.Contains(SelectedHashTagId))
        {
            _taskService.AddHashTagToTask(task, SelectedHashTagId);
        }

        QuickAddText = string.Empty;
        LoadTasks();
        LoadHashTags(); // 새 해시태그가 생성되었을 수 있음

        // 프로젝트에 할일 추가 시 카운트 갱신
        if (SelectedProjectId != null)
        {
            LoadProjects();
        }
        // 연속 입력을 위해 입력창은 유지
    }

    private void ToggleQuickAdd()
    {
        // 검색 모드가 열려있으면 닫기
        if (IsSearchMode)
        {
            IsSearchMode = false;
            LoadTasks();
        }

        IsQuickAddVisible = !IsQuickAddVisible;
        if (!IsQuickAddVisible)
        {
            QuickAddText = string.Empty;
            HideHashTagSuggestions();
        }
    }

    // HashTag Autocomplete Methods
    private void UpdateHashTagSuggestions(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            HideHashTagSuggestions();
            return;
        }

        // #으로 시작하는 해시태그 찾기
        var hashIndex = text.LastIndexOf('#');
        if (hashIndex < 0)
        {
            HideHashTagSuggestions();
            return;
        }

        // # 뒤의 텍스트 추출 (스페이스가 나오면 태그 종료로 간주)
        var afterHash = text.Substring(hashIndex + 1);
        var spaceIndex = afterHash.IndexOf(' ');
        if (spaceIndex >= 0)
        {
            // 스페이스가 있으면 태그가 이미 종료됨
            HideHashTagSuggestions();
            return;
        }

        var query = afterHash.Trim();

        // 해시태그 검색 (prefix match)
        var hashTags = _taskService.GetHashTags();
        var matches = hashTags
            .Where(h => h.Name.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .OrderBy(h => h.Name)
            .Take(5)
            .Select(h => new HashTagSuggestion { Id = h.Id, Name = h.Name, Color = h.Color })
            .ToList();

        if (matches.Count == 0 && !string.IsNullOrEmpty(query))
        {
            // prefix가 없으면 contains로 검색
            matches = hashTags
                .Where(h => h.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .OrderBy(h => h.Name)
                .Take(5)
                .Select(h => new HashTagSuggestion { Id = h.Id, Name = h.Name, Color = h.Color })
                .ToList();
        }

        if (matches.Count > 0)
        {
            HashTagSuggestions.Clear();
            foreach (var match in matches)
            {
                HashTagSuggestions.Add(match);
            }
            SelectedSuggestionIndex = 0;
            ShowHashTagSuggestions = true;
        }
        else
        {
            HideHashTagSuggestions();
        }
    }

    public void HideHashTagSuggestions()
    {
        ShowHashTagSuggestions = false;
        SelectedSuggestionIndex = -1;
    }

    public void SelectNextSuggestion()
    {
        if (!ShowHashTagSuggestions || HashTagSuggestions.Count == 0) return;
        SelectedSuggestionIndex = (SelectedSuggestionIndex + 1) % HashTagSuggestions.Count;
    }

    public void SelectPreviousSuggestion()
    {
        if (!ShowHashTagSuggestions || HashTagSuggestions.Count == 0) return;
        SelectedSuggestionIndex = SelectedSuggestionIndex <= 0
            ? HashTagSuggestions.Count - 1
            : SelectedSuggestionIndex - 1;
    }

    public bool ApplySelectedSuggestion()
    {
        if (!ShowHashTagSuggestions || SelectedSuggestionIndex < 0 || SelectedSuggestionIndex >= HashTagSuggestions.Count)
            return false;

        var selected = HashTagSuggestions[SelectedSuggestionIndex];
        ApplyHashTagSuggestion(selected.Name);
        return true;
    }

    public void ApplyHashTagSuggestion(string hashTagName)
    {
        // # 이후의 텍스트를 선택한 해시태그 이름으로 교체
        var hashIndex = QuickAddText.LastIndexOf('#');
        if (hashIndex >= 0)
        {
            var beforeHash = QuickAddText.Substring(0, hashIndex);
            // 해시태그 이름에 공백이 있으면 따옴표로 감싸기
            var formattedName = hashTagName.Contains(' ') ? $"\"{hashTagName}\"" : hashTagName;
            QuickAddText = beforeHash + "#" + formattedName + " ";
        }
        HideHashTagSuggestions();
    }

    private void LoadTasks()
    {
        Tasks.Clear();

        IEnumerable<TodoTask> tasks;

        if (IsSearchMode)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                tasks = _taskService.GetAllTasks(ShowCompleted);
            }
            else
            {
                tasks = _taskService.SearchTasks(SearchText, ShowCompleted);
            }
        }
        else if (SelectedProjectId != null)
        {
            // 프로젝트 선택 시 해당 프로젝트의 할일만 표시
            tasks = _taskService.GetProjectTasks(SelectedProjectId, ShowCompleted);
        }
        else if (SelectedHashTagId != null)
        {
            // 해시태그 선택 시 해당 해시태그의 할일만 표시
            tasks = _taskService.GetTasksByHashTag(SelectedHashTagId, ShowCompleted);
        }
        else
        {
            tasks = CurrentView switch
            {
                ViewType.Inbox => _taskService.GetInboxTasks(ShowCompleted),
                ViewType.Today => _taskService.GetTodayTasks(ShowCompleted),
                ViewType.Upcoming => _taskService.GetUpcomingTasks(ShowCompleted),
                _ => _taskService.GetTodayTasks(ShowCompleted)
            };
        }

        foreach (var task in tasks)
        {
            var taskVm = new TaskItemViewModel(task, _taskService, _undoService, OnTaskCompleted, OnTaskDataChanged, OnTaskExpandedChanged);
            // 저장된 확장 상태 복원
            if (_expandedTaskIds.Contains(task.Id))
            {
                taskVm.IsExpanded = true;
            }
            Tasks.Add(taskVm);
        }

        UpdateProgress();
    }

    private void OnTaskExpandedChanged(string taskId, bool isExpanded)
    {
        if (isExpanded)
        {
            _expandedTaskIds.Add(taskId);
        }
        else
        {
            _expandedTaskIds.Remove(taskId);
        }
    }

    private void ToggleShowCompleted()
    {
        ShowCompleted = !ShowCompleted;
        OnPropertyChanged(nameof(ShowCompletedText));
    }

    private void OnTaskDataChanged()
    {
        LoadTasks();
    }

    private void OnTaskCompleted()
    {
        var petStatus = _petService.GetStatus();
        bool isFirstToday = petStatus.TodayCompleted == 1;
        bool isAllDone = !_taskService.GetTodayTasks().Any();

        var now = DateTime.Now;
        if (now - _lastCompletionTime < _streakTimeout)
        {
            _completionStreak++;
        }
        else
        {
            _completionStreak = 1;
        }
        _lastCompletionTime = now;

        bool shouldReact = false;
        string? message = null;

        if (petStatus.Level > _previousLevel)
        {
            message = _petService.GetLevelUpMessage(petStatus.Level);
            _previousLevel = petStatus.Level;
            shouldReact = true;
        }
        else if (isFirstToday)
        {
            message = _petService.GetCompletionMessage(true, false);
            shouldReact = true;
        }
        else if (isAllDone)
        {
            message = _petService.GetCompletionMessage(false, true);
            shouldReact = true;
        }
        else if (_completionStreak >= 3 && _random.Next(100) < 50)
        {
            message = _petService.GetStreakMessage(_completionStreak);
            shouldReact = true;
        }
        else if (_petService.ShouldShowNormalCompletionReaction())
        {
            message = _petService.GetCompletionMessage(false, false);
            shouldReact = true;
        }

        if (shouldReact && message != null)
        {
            PetMessage = message;
            PetMood = PetMood.Excited;
            ShowMessage = true;

            _excitedTimer.Stop();
            _excitedTimer.Start();
        }

        LoadTasks();
        OnPropertyChanged(nameof(PetLevel));
    }

    private void OnDataChanged()
    {
        UpdateProgress();
    }

    private void UpdateProgress()
    {
        OnPropertyChanged(nameof(TodayCompleted));
        OnPropertyChanged(nameof(TodayTotal));
        OnPropertyChanged(nameof(TodayProgressText));
        OnPropertyChanged(nameof(TodayProgressDots));
    }

    private void UpdatePetMood()
    {
        if (PetMood != PetMood.Excited)
        {
            PetMood = _petService.GetCurrentMood();

            if (PetMood == PetMood.Worried)
            {
                var overdueMsg = _petService.GetOverdueMessage();
                if (overdueMsg != null)
                {
                    PetMessage = overdueMsg;
                    ShowMessage = true;
                }
            }
            else if (PetMood == PetMood.Resting)
            {
                PetMessage = _petService.GetRestingMessage();
                ShowMessage = true;
            }
            else
            {
                // Normal 상태에서는 말풍선 숨김
                ShowMessage = false;
            }
        }
    }

    private void OnPetClicked()
    {
        PetMessage = _petService.GetPokeMessage();
        PetMood = PetMood.Excited;
        ShowMessage = true;

        _excitedTimer.Stop();
        _excitedTimer.Start();

        PetPoked?.Invoke();
    }

    // Undo/Redo
    public void PerformUndo()
    {
        var (success, message, action) = _undoService.Undo();
        if (success)
        {
            _lastUndoAction = action;
            ToastMessage = message;
            ShowToast = true;
            _toastTimer.Stop();
            _toastTimer.Start();
            LoadTasks();
            ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
        }
    }

    public void PerformRedo()
    {
        var (success, message) = _undoService.Redo();
        if (success)
        {
            _lastUndoAction = null;
            ToastMessage = message;
            ShowToast = true;
            _toastTimer.Stop();
            _toastTimer.Start();
            LoadTasks();
            ((RelayCommand)UndoCommand).RaiseCanExecuteChanged();
            ((RelayCommand)RedoCommand).RaiseCanExecuteChanged();
        }
    }

    private void OnUndoPerformed(string message, UndoAction action)
    {
        // Undo 수행 후 처리 (이벤트 기반)
    }

    public event Action? PetPoked;
    public event Action<string>? GoodbyeRequested;

    public void ShowGoodbye()
    {
        var message = _petService.GetGoodbyeMessage();
        GoodbyeRequested?.Invoke(message);
    }

    public void ToggleSelectedTaskComplete(TaskItemViewModel? selectedTask)
    {
        selectedTask?.ToggleCompleteCommand.Execute(null);
    }

    public void ToggleSelectedTaskImportant(TaskItemViewModel? selectedTask)
    {
        selectedTask?.ToggleImportantCommand.Execute(null);
    }

    public void ToggleSelectedTaskPinnedToday(TaskItemViewModel? selectedTask)
    {
        selectedTask?.TogglePinnedTodayCommand.Execute(null);
    }

    public void ReorderTask(TaskItemViewModel taskViewModel, int newIndex)
    {
        _taskService.ReorderTask(taskViewModel.Task, newIndex);
        LoadTasks();
    }

    // Project Methods
    private void LoadProjects()
    {
        Projects.Clear();
        foreach (var project in _taskService.GetProjects())
        {
            var vm = new ProjectViewModel(project, _taskService, SelectProject, DeleteProject, RenameProject);
            Projects.Add(vm);
        }
    }

    private void AddProject()
    {
        if (string.IsNullOrWhiteSpace(NewProjectName)) return;

        var project = _taskService.AddProject(NewProjectName.Trim());
        LoadProjects();
        IsAddingProject = false;
        NewProjectName = string.Empty;

        // 새 프로젝트 선택
        SelectProject(project.Id);
    }

    private void SelectProject(string? projectId)
    {
        SelectedProjectId = projectId;
    }

    private void UpdateProjectSelection()
    {
        foreach (var project in Projects)
        {
            project.IsSelected = project.Id == SelectedProjectId;
        }
    }

    private void DeleteProject(string projectId)
    {
        _taskService.DeleteProject(projectId);
        if (SelectedProjectId == projectId)
        {
            SelectedProjectId = null;
            CurrentView = ViewType.Inbox;
        }
        LoadProjects();
    }

    private void RenameProject(string projectId, string newName)
    {
        _taskService.RenameProject(projectId, newName);
        LoadProjects();
        OnPropertyChanged(nameof(ViewTitle));
    }

    // HashTag Methods
    private void LoadHashTags()
    {
        HashTags.Clear();
        var hashTags = _taskService.GetHashTags();

        // 숨김 표시 모드가 아니면 숨겨진 해시태그 제외
        if (!ShowHiddenHashTags)
        {
            hashTags = hashTags.Where(h => !h.IsHidden).ToList();
        }

        foreach (var hashTag in hashTags)
        {
            var vm = new HashTagViewModel(hashTag, _taskService, SelectHashTag, RequestDeleteHashTag, UpdateHashTagColor, HideHashTag, RestoreHashTag);
            HashTags.Add(vm);
        }

        OnPropertyChanged(nameof(HasHiddenHashTags));
    }

    private void SelectHashTag(string? hashTagId)
    {
        // 해시태그 선택 시 프로젝트 선택 해제
        if (hashTagId != null)
        {
            _selectedProjectId = null;
            OnPropertyChanged(nameof(SelectedProjectId));
            OnPropertyChanged(nameof(IsProjectSelected));
            UpdateProjectSelection();
        }
        SelectedHashTagId = hashTagId;
    }

    private void UpdateHashTagSelection()
    {
        foreach (var hashTag in HashTags)
        {
            hashTag.IsSelected = hashTag.Id == SelectedHashTagId;
        }
    }

    private void RequestDeleteHashTag(string hashTagId)
    {
        // 삭제 확인 팝업 표시
        var hashTag = _taskService.GetHashTagById(hashTagId);
        if (hashTag != null)
        {
            _pendingDeleteHashTagId = hashTagId;
            PendingDeleteHashTagName = hashTag.Name;
            ShowDeleteHashTagConfirm = true;
        }
    }

    private void ExecuteDeleteHashTag()
    {
        if (_pendingDeleteHashTagId != null)
        {
            _taskService.DeleteHashTag(_pendingDeleteHashTagId);
            LoadHashTags();
            LoadTasks(); // 할일 목록도 갱신 (해시태그 표시 갱신)
        }
        ShowDeleteHashTagConfirm = false;
        _pendingDeleteHashTagId = null;
    }

    private void HideHashTag(string hashTagId)
    {
        _taskService.HideHashTag(hashTagId);
        LoadHashTags();
    }

    private void RestoreHashTag(string hashTagId)
    {
        _taskService.RestoreHashTag(hashTagId);
        LoadHashTags();
    }

    private void UpdateHashTagColor(string hashTagId, string color)
    {
        _taskService.UpdateHashTagColor(hashTagId, color);
        LoadHashTags();
        LoadTasks(); // 할일 목록도 갱신 (색상 변경 반영)
    }

    // Drag and Drop - Move task to/from project
    public void MoveTaskToProject(TaskItemViewModel taskVm, string? projectId)
    {
        _taskService.MoveTaskToProject(taskVm.Task, projectId);
        LoadTasks();
        LoadProjects(); // Update task counts
    }

    public void MoveTaskFromProject(TaskItemViewModel taskVm)
    {
        // Move task out of project (set ProjectId to null)
        _taskService.MoveTaskToProject(taskVm.Task, null);
        LoadTasks();
        LoadProjects(); // Update task counts
    }

    // Project Reorder
    public void ReorderProject(string draggedProjectId, string targetProjectId)
    {
        _taskService.ReorderProject(draggedProjectId, targetProjectId);
        LoadProjects();
    }

    // Export/Import Methods
    private void ExportData()
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = "데이터 내보내기",
            Filter = "JSON 파일 (*.json)|*.json",
            DefaultExt = "json",
            FileName = $"gajigaji_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json"
        };

        if (dialog.ShowDialog() == true)
        {
            var data = _taskService.GetAppData();
            if (_storageService.ExportToFile(data, dialog.FileName))
            {
                ToastMessage = "데이터를 내보냈습니다";
                ShowToast = true;
                _toastTimer.Stop();
                _toastTimer.Start();
            }
        }
    }

    private void ImportData()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "데이터 가져오기",
            Filter = "JSON 파일 (*.json)|*.json",
            DefaultExt = "json"
        };

        if (dialog.ShowDialog() == true)
        {
            var result = System.Windows.MessageBox.Show(
                "현재 데이터가 가져온 데이터로 대체됩니다.\n계속하시겠습니까?",
                "GajiGaji - 데이터 가져오기",
                System.Windows.MessageBoxButton.YesNo,
                System.Windows.MessageBoxImage.Warning);

            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var data = _storageService.ImportFromFile(dialog.FileName);
                if (data != null)
                {
                    _taskService.ReplaceData(data);
                    LoadTasks();
                    LoadProjects();
                    OnPropertyChanged(nameof(PetLevel));
                    UpdateProgress();

                    ToastMessage = "데이터를 가져왔습니다";
                    ShowToast = true;
                    _toastTimer.Stop();
                    _toastTimer.Start();
                }
                else
                {
                    System.Windows.MessageBox.Show(
                        "파일을 읽을 수 없습니다.",
                        "GajiGaji",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                }
            }
        }
    }

    private void OpenBackupFolder()
    {
        var folder = _storageService.GetAppFolder();
        try
        {
            if (!Directory.Exists(folder))
            {
                System.Windows.MessageBox.Show(
                    "백업 폴더가 존재하지 않습니다.",
                    "GajiGaji",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = folder,
                UseShellExecute = true,
                Verb = "open"
            };
            System.Diagnostics.Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[MainViewModel] OpenBackupFolder failed: {ex.Message}");
            System.Windows.MessageBox.Show(
                $"폴더를 열 수 없습니다.\n{ex.Message}",
                "GajiGaji",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
        }
    }

    private void ResetData()
    {
        // 1차 확인
        var result1 = System.Windows.MessageBox.Show(
            "정말로 모든 데이터를 초기화하시겠습니까?\n\n" +
            "• 모든 할일\n" +
            "• 모든 프로젝트\n" +
            "• 캐릭터 레벨 및 상태\n\n" +
            "모두 삭제됩니다.",
            "GajiGaji - 데이터 초기화",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result1 != System.Windows.MessageBoxResult.Yes)
            return;

        // 2차 확인
        var result2 = System.Windows.MessageBox.Show(
            "⚠️ 마지막 확인 ⚠️\n\n" +
            "이 작업은 되돌릴 수 없습니다.\n" +
            "정말 초기화하시겠습니까?",
            "GajiGaji - 최종 확인",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        if (result2 != System.Windows.MessageBoxResult.Yes)
            return;

        // 초기화 실행
        var newData = new Models.AppData();
        _taskService.ReplaceData(newData);

        // 백업도 삭제 (자동 복구 방지)
        _taskService.DeleteAllBackups();

        // UI 갱신
        LoadTasks();
        LoadProjects();
        OnPropertyChanged(nameof(PetLevel));

        ShowHelpModal = false;

        System.Windows.MessageBox.Show(
            "데이터가 초기화되었습니다.",
            "GajiGaji",
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information);
    }

    // UI Settings
    private void LoadUISettings()
    {
        var settings = _storageService.LoadUISettings();
        _isProjectsExpanded = settings.IsProjectsExpanded;
        _isHashTagsExpanded = settings.IsHashTagsExpanded;
        OnPropertyChanged(nameof(IsProjectsExpanded));
        OnPropertyChanged(nameof(IsHashTagsExpanded));
    }

    private void SaveUISettings()
    {
        var settings = new UISettings
        {
            IsProjectsExpanded = IsProjectsExpanded,
            IsHashTagsExpanded = IsHashTagsExpanded
        };
        _storageService.SaveUISettings(settings);
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
            _excitedTimer?.Stop();
            _toastTimer?.Stop();
            _storageService?.Dispose();
        }

        _disposed = true;
    }
}

public class WeekDayInfo
{
    public int Day { get; set; }
    public bool IsToday { get; set; }
}

public class HashTagSuggestion
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#9B59B6";
}
