using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Windows.Input;

namespace SlimeTodo.ViewModels;

public class DeletedProjectViewModel : ViewModelBase
{
    private readonly Project _project;
    private readonly TaskService _taskService;
    private readonly Action? _onChanged;

    public DeletedProjectViewModel(Project project, TaskService taskService, Action? onChanged)
    {
        _project = project;
        _taskService = taskService;
        _onChanged = onChanged;

        RestoreCommand = new RelayCommand(Restore);
        PermanentDeleteCommand = new RelayCommand(PermanentDelete);
    }

    public string Id => _project.Id;
    public string Name => _project.Name;
    public DateTime? DeletedAt => _project.DeletedAt;

    public string DeletedAtText => DeletedAt.HasValue
        ? DeletedAt.Value.ToString("yyyy-MM-dd HH:mm")
        : "";

    public ICommand RestoreCommand { get; }
    public ICommand PermanentDeleteCommand { get; }

    private void Restore()
    {
        _taskService.RestoreProject(_project.Id);
        _onChanged?.Invoke();
    }

    private void PermanentDelete()
    {
        _taskService.PermanentlyDeleteProject(_project.Id);
        _onChanged?.Invoke();
    }
}
