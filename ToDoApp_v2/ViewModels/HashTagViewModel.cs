using SlimeTodo.Models;
using SlimeTodo.Services;
using System.Windows.Input;

namespace SlimeTodo.ViewModels;

public class HashTagViewModel : ViewModelBase
{
    private readonly HashTag _hashTag;
    private readonly TaskService _taskService;
    private readonly Action<string>? _onSelect;
    private readonly Action<string>? _onDelete;
    private readonly Action<string, string>? _onColorChanged;
    private readonly Action<string>? _onHide;
    private readonly Action<string>? _onRestore;
    private bool _isSelected;

    public HashTagViewModel(HashTag hashTag, TaskService taskService,
        Action<string>? onSelect = null, Action<string>? onDelete = null,
        Action<string, string>? onColorChanged = null, Action<string>? onHide = null,
        Action<string>? onRestore = null)
    {
        _hashTag = hashTag;
        _taskService = taskService;
        _onSelect = onSelect;
        _onDelete = onDelete;
        _onColorChanged = onColorChanged;
        _onHide = onHide;
        _onRestore = onRestore;

        SelectCommand = new RelayCommand(() => _onSelect?.Invoke(Id));
        DeleteCommand = new RelayCommand(Delete);
        SetColorCommand = new RelayCommand<string>(SetColor);
        HideCommand = new RelayCommand(Hide);
        RestoreCommand = new RelayCommand(Restore);
    }

    public string Id => _hashTag.Id;

    public string Name
    {
        get => _hashTag.Name;
        private set
        {
            _hashTag.Name = value;
            OnPropertyChanged();
        }
    }

    public string Color
    {
        get => _hashTag.Color;
        private set
        {
            _hashTag.Color = value;
            OnPropertyChanged();
        }
    }

    public int TaskCount => _taskService.GetHashTagTaskCount(_hashTag.Id);

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public bool IsHidden
    {
        get => _hashTag.IsHidden;
        set
        {
            _hashTag.IsHidden = value;
            OnPropertyChanged();
        }
    }

    // 색상 팔레트
    public static string[] ColorPalette => HashTagColors.Palette;

    public ICommand SelectCommand { get; }
    public ICommand DeleteCommand { get; }
    public ICommand SetColorCommand { get; }
    public ICommand HideCommand { get; }
    public ICommand RestoreCommand { get; }

    private void Delete()
    {
        _onDelete?.Invoke(_hashTag.Id);
    }

    private void SetColor(string? color)
    {
        if (!string.IsNullOrEmpty(color))
        {
            Color = color;
            _onColorChanged?.Invoke(_hashTag.Id, color);
        }
    }

    private void Hide()
    {
        _onHide?.Invoke(_hashTag.Id);
    }

    private void Restore()
    {
        _onRestore?.Invoke(_hashTag.Id);
    }
}
