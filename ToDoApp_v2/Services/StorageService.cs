using System.Diagnostics;
using System.IO;
using System.Text.Json;
using SlimeTodo.Models;

namespace SlimeTodo.Services;

public class StorageService : IDisposable
{
    private bool _disposed;
    private static readonly string AppFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GajiGaji");

    private static readonly string DataFile = Path.Combine(AppFolder, "data.json");
    private static readonly string TempFile = Path.Combine(AppFolder, "data.json.tmp");
    private static readonly string BackupFolder = Path.Combine(AppFolder, "backups");
    private static readonly string LockFile = Path.Combine(AppFolder, ".lock");
    private static readonly string UISettingsFile = Path.Combine(AppFolder, "ui_settings.json");

    private const int BackupRetentionDays = 7;
    private FileStream? _lockStream;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public StorageService()
    {
        if (!Directory.Exists(AppFolder))
        {
            Directory.CreateDirectory(AppFolder);
        }
        if (!Directory.Exists(BackupFolder))
        {
            Directory.CreateDirectory(BackupFolder);
        }

        // 기존 SlimeTodo 폴더에서 데이터 마이그레이션
        MigrateFromOldFolder();
    }

    private void MigrateFromOldFolder()
    {
        var oldFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SlimeTodo");
        var oldDataFile = Path.Combine(oldFolder, "data.json");

        if (File.Exists(oldDataFile) && !File.Exists(DataFile))
        {
            try
            {
                File.Copy(oldDataFile, DataFile);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StorageService] Migration failed: {ex.Message}");
            }
        }
    }

    public bool AcquireLock()
    {
        try
        {
            _lockStream = new FileStream(LockFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] AcquireLock failed: {ex.Message}");
            return false;
        }
    }

    public void ReleaseLock()
    {
        _lockStream?.Dispose();
        _lockStream = null;
        try
        {
            if (File.Exists(LockFile))
                File.Delete(LockFile);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] ReleaseLock cleanup failed: {ex.Message}");
        }
    }

    public bool HasCrashRecovery()
    {
        // 락 파일이 있는데 획득 가능하면 = 이전 크래시
        if (!File.Exists(LockFile)) return false;

        try
        {
            using var test = new FileStream(LockFile, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return true; // 락 획득 성공 = 이전 세션이 정상 종료되지 않음
        }
        catch (IOException)
        {
            return false; // 다른 프로세스가 사용 중 (정상적인 상황)
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] HasCrashRecovery check failed: {ex.Message}");
            return false;
        }
    }

    public List<BackupInfo> GetAvailableBackups()
    {
        var backups = new List<BackupInfo>();

        if (!Directory.Exists(BackupFolder)) return backups;

        foreach (var file in Directory.GetFiles(BackupFolder, "backup_*.json"))
        {
            var info = new FileInfo(file);
            backups.Add(new BackupInfo
            {
                FilePath = file,
                Date = info.LastWriteTime,
                FileName = info.Name
            });
        }

        return backups.OrderByDescending(b => b.Date).ToList();
    }

    public AppData? LoadFromBackup(string backupPath)
    {
        try
        {
            var json = File.ReadAllText(backupPath);
            return JsonSerializer.Deserialize<AppData>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] LoadFromBackup failed ({backupPath}): {ex.Message}");
            return null;
        }
    }

    public AppData Load()
    {
        try
        {
            if (File.Exists(DataFile))
            {
                var json = File.ReadAllText(DataFile);
                var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
                if (data != null)
                {
                    data = MigrateIfNeeded(data);
                    return data;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] Load failed, attempting backup recovery: {ex.Message}");
            // 읽기 실패 시 최신 백업에서 복구 시도
            var backups = GetAvailableBackups();
            foreach (var backup in backups)
            {
                var data = LoadFromBackup(backup.FilePath);
                if (data != null)
                {
                    Debug.WriteLine($"[StorageService] Recovered from backup: {backup.FileName}");
                    return MigrateIfNeeded(data);
                }
            }
        }
        return new AppData();
    }

    private AppData MigrateIfNeeded(AppData data)
    {
        if (data.SchemaVersion < AppData.CurrentSchemaVersion)
        {
            // 향후 스키마 마이그레이션 로직 추가
            // 예: if (data.SchemaVersion == 1) MigrateV1ToV2(data);

            data.SchemaVersion = AppData.CurrentSchemaVersion;
        }
        return data;
    }

    public void Save(AppData data)
    {
        try
        {
            data.SchemaVersion = AppData.CurrentSchemaVersion;
            var json = JsonSerializer.Serialize(data, JsonOptions);

            // 1. 임시 파일에 먼저 작성
            File.WriteAllText(TempFile, json);

            // 2. 일간 롤링 백업 생성
            CreateDailyBackup();

            // 3. 임시 파일을 본 파일로 이동
            File.Move(TempFile, DataFile, overwrite: true);

            // 4. 오래된 백업 정리
            CleanupOldBackups();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Save failed: {ex.Message}");
        }
    }

    private void CreateDailyBackup()
    {
        if (!File.Exists(DataFile)) return;

        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var backupPath = Path.Combine(BackupFolder, $"backup_{today}.json");

        // 오늘 백업이 없으면 생성
        if (!File.Exists(backupPath))
        {
            try
            {
                File.Copy(DataFile, backupPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StorageService] Daily backup failed: {ex.Message}");
            }
        }
    }

    private void CleanupOldBackups()
    {
        if (!Directory.Exists(BackupFolder)) return;

        var cutoffDate = DateTime.Today.AddDays(-BackupRetentionDays);

        foreach (var file in Directory.GetFiles(BackupFolder, "backup_*.json"))
        {
            var info = new FileInfo(file);
            if (info.LastWriteTime.Date < cutoffDate)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[StorageService] Cleanup old backup failed ({info.Name}): {ex.Message}");
                }
            }
        }
    }

    /// <summary>
    /// 모든 백업 파일을 삭제합니다. 데이터 초기화 시 사용합니다.
    /// </summary>
    public void DeleteAllBackups()
    {
        if (!Directory.Exists(BackupFolder)) return;

        foreach (var file in Directory.GetFiles(BackupFolder, "backup_*.json"))
        {
            try
            {
                File.Delete(file);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[StorageService] DeleteAllBackups failed ({Path.GetFileName(file)}): {ex.Message}");
            }
        }
    }

    public string Export(AppData data)
    {
        return JsonSerializer.Serialize(data, JsonOptions);
    }

    public AppData? Import(string json)
    {
        try
        {
            // 입력 검증: 빈 문자열 체크
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.WriteLine("[StorageService] Import failed: Empty JSON");
                return null;
            }

            // 입력 검증: 최대 크기 제한 (10MB)
            if (json.Length > 10_000_000)
            {
                Debug.WriteLine("[StorageService] Import failed: JSON too large");
                return null;
            }

            var data = JsonSerializer.Deserialize<AppData>(json, JsonOptions);
            if (data != null)
            {
                // 입력 검증: 작업 개수 제한 (10,000개)
                if (data.Tasks.Count > 10_000)
                {
                    Debug.WriteLine($"[StorageService] Import failed: Too many tasks ({data.Tasks.Count})");
                    return null;
                }

                return MigrateIfNeeded(data);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] Import failed: {ex.Message}");
        }
        return null;
    }

    public bool ExportToFile(AppData data, string filePath)
    {
        try
        {
            var json = Export(data);
            File.WriteAllText(filePath, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] ExportToFile failed: {ex.Message}");
            return false;
        }
    }

    public AppData? ImportFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            return Import(json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] ImportFromFile failed: {ex.Message}");
            return null;
        }
    }

    public string GetAppFolder() => AppFolder;

    // UI Settings
    public UISettings LoadUISettings()
    {
        try
        {
            if (File.Exists(UISettingsFile))
            {
                var json = File.ReadAllText(UISettingsFile);
                var settings = JsonSerializer.Deserialize<UISettings>(json, JsonOptions);
                if (settings != null)
                    return settings;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] LoadUISettings failed: {ex.Message}");
        }
        return new UISettings();
    }

    public void SaveUISettings(UISettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(UISettingsFile, json);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[StorageService] SaveUISettings failed: {ex.Message}");
        }
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
            ReleaseLock();
        }

        _disposed = true;
    }

    ~StorageService()
    {
        Dispose(false);
    }
}

public class BackupInfo
{
    public string FilePath { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string FileName { get; set; } = string.Empty;
}

public class UISettings
{
    public bool IsProjectsExpanded { get; set; } = true;
    public bool IsHashTagsExpanded { get; set; } = true;
}
