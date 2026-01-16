using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Windows.Input;

namespace SlimeTodo.ViewModels;

public class ProjectViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly TaskService _taskService;
    private readonly Action<string>? _onSelect;
    private readonly Action<string>? _onDelete;
    private readonly Action<string, string>? _onRename;
    private bool _isEditing;
    private string _editName;
    private bool _isSelected;

    public ProjectViewModel(Project project, TaskService taskService,
        Action<string>? onSelect = null, Action<string>? onDelete = null, Action<string, string>? onRename = null)
    {
        _project = project;
        _taskService = taskService;
        _onSelect = onSelect;
        _onDelete = onDelete;
        _onRename = onRename;
        _editName = project.Name;

        SelectCommand = new RelayCommand(() => _onSelect?.Invoke(Id));
        DeleteCommand = new RelayCommand(Delete);
        StartEditCommand = new RelayCommand(() => { IsEditing = true; EditName = Name; });
        SaveEditCommand = new RelayCommand(SaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
    }

    public string Id => _project.Id;

    public string Name
    {
        get => _project.Name;
        private set
        {
            _project.Name = value;
            OnPropertyChanged();
        }
    }

    public int TaskCount => _taskService.GetProjectTaskCount(_project.Id);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsEditing
    {
        get => _isEditing;
        set => SetProperty(ref _isEditing, value);
    }

    public string EditName
    {
        get => _editName;
        set => SetProperty(ref _editName, value);
    }

    public ICommand SelectCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand StartEditCommand { get; }
    public ICommand SaveEditCommand { get; }
    public ICommand CancelEditCommand { get; }

    private void Delete()
    {
        _onDelete?.Invoke(_project.Id);
    }

    private void SaveEdit()
    {
        if (!string.IsNullOrWhiteSpace(EditName) && EditName.Trim() != Name)
        {
            _onRename?.Invoke(_project.Id, EditName.Trim());
        }
        IsEditing = false;
    }

    private void CancelEdit()
    {
        EditName = Name;
        IsEditing = false;
    }
}
