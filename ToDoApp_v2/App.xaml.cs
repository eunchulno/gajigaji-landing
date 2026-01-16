using SlimeTodo.Services;
using System.Windows;

namespace SlimeTodo;

public partial class App : Application
{
    private StorageService? _storageService;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _storageService = new StorageService();

        // 크래시 복구는 더 이상 자동으로 수행하지 않음
        // 이유: 백업이 현재 데이터보다 오래된 경우 데이터 손실 발생
        // 대신 현재 data.json을 그대로 사용 (크래시 전 마지막 저장 상태)

        // 락 획득
        if (!_storageService.AcquireLock())
        {
            MessageBox.Show(
                "GajiGaji가 이미 실행 중입니다.",
                "GajiGaji",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            Shutdown();
            return;
        }

        // ThemeService 초기화 (저장된 테마 적용)
        _ = ThemeService.Instance;
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 락 해제
        _storageService?.ReleaseLock();
        base.OnExit(e);
    }
}
