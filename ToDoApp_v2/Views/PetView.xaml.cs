using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using SlimeTodo.ViewModels;

namespace SlimeTodo.Views;

public partial class PetView : UserControl
{
    private readonly DispatcherTimer _blinkTimer;
    private readonly DispatcherTimer _lookTimer;
    private readonly DispatcherTimer _yawnTimer;
    private readonly Random _random = new();
    private bool _isYawning;

    public PetView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        // 눈 깜빡임 타이머 (3~8초 랜덤 간격, 5% 확률)
        _blinkTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_random.Next(3, 8))
        };
        _blinkTimer.Tick += OnBlinkTimerTick;

        // 시선 변화 타이머 (10~20초 간격)
        _lookTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_random.Next(10, 20))
        };
        _lookTimer.Tick += OnLookTimerTick;

        // 하품 타이머 (45~90초 간격)
        _yawnTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_random.Next(45, 90))
        };
        _yawnTimer.Tick += OnYawnTimerTick;

        Loaded += (_, _) =>
        {
            _blinkTimer.Start();
            _lookTimer.Start();
            _yawnTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _blinkTimer.Stop();
            _lookTimer.Stop();
            _yawnTimer.Stop();
        };
    }

    private void OnBlinkTimerTick(object? sender, EventArgs e)
    {
        // 하품 중에는 깜빡임 스킵
        if (_isYawning) return;

        // 5% 확률로 눈 깜빡임
        if (_random.Next(100) < 5)
        {
            PlayBlinkAnimation();
        }

        // 다음 타이머 간격 랜덤 설정 (3~8초)
        _blinkTimer.Interval = TimeSpan.FromSeconds(_random.Next(3, 8));
    }

    private void OnLookTimerTick(object? sender, EventArgs e)
    {
        // 하품 중에는 시선 변화 스킵
        if (_isYawning) return;

        // 랜덤으로 왼쪽 또는 오른쪽 보기
        var lookLeft = _random.Next(2) == 0;
        var animationKey = lookLeft ? "LookLeftAnimation" : "LookRightAnimation";

        if (Resources[animationKey] is Storyboard lookStoryboard)
        {
            lookStoryboard.Begin();
        }

        // 다음 타이머 간격 랜덤 설정 (10~20초)
        _lookTimer.Interval = TimeSpan.FromSeconds(_random.Next(10, 20));
    }

    private void OnYawnTimerTick(object? sender, EventArgs e)
    {
        // Normal 상태일 때만 하품 (Excited, Resting, Worried 상태에서는 하품하지 않음)
        if (DataContext is MainViewModel vm && vm.PetMood == Models.PetMood.Normal && !_isYawning)
        {
            PlayYawnAnimation();
        }

        // 다음 타이머 간격 랜덤 설정 (45~90초)
        _yawnTimer.Interval = TimeSpan.FromSeconds(_random.Next(45, 90));
    }

    private void PlayYawnAnimation()
    {
        if (Resources["YawnAnimation"] is Storyboard yawnStoryboard)
        {
            _isYawning = true;
            yawnStoryboard.Completed += OnYawnCompleted;
            yawnStoryboard.Begin();
        }
    }

    private void OnYawnCompleted(object? sender, EventArgs e)
    {
        _isYawning = false;
        if (sender is Storyboard storyboard)
        {
            storyboard.Completed -= OnYawnCompleted;
        }
    }

    private void PlayBlinkAnimation()
    {
        if (Resources["BlinkAnimation"] is Storyboard blinkStoryboard)
        {
            blinkStoryboard.Begin();
        }
    }

    private void OnDataContextChanged(object sender, System.Windows.DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
        {
            oldVm.PetPoked -= PlayPokeAnimation;
        }

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PetPoked += PlayPokeAnimation;
        }
    }

    private void EggplantBody_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is MainViewModel vm && vm.PetClickCommand.CanExecute(null))
        {
            vm.PetClickCommand.Execute(null);
        }
    }

    private void PlayPokeAnimation()
    {
        if (Resources["PokeAnimation"] is Storyboard pokeStoryboard)
        {
            pokeStoryboard.Begin();
        }
    }
}
