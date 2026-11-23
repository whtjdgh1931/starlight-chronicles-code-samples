using System;
using System.IO;
using System.Text;

/// <summary>
/// 로컬 파일 확인, 읽기, 쓰기 담당
/// </summary>
public sealed class SaveFileStorage : ISaveWriter
{
    public enum ReadResult
    {
        Success,
        NotFound,
        Failed
    }

    private const string BackupSuffix = ".bak";
    private const string TemporarySuffix = ".tmp";

    private static readonly UTF8Encoding Utf8Encoding = new(false);


    private readonly string _directoryPath;

    private readonly string _filePath;

    private readonly string _backupFilePath;

    private readonly string _temporaryFilePath;
    private readonly object _writeLock = new();
    private bool _preserveBackup;



    public SaveFileStorage(string directoryPath, string fileName)
    {
        _directoryPath = directoryPath;
        _filePath = Path.Combine(directoryPath, fileName);
        _backupFilePath = _filePath + BackupSuffix;
        _temporaryFilePath = _filePath + TemporarySuffix;
    }

    public ReadResult Read(out string json, out string errorMessage)
    {
        return ReadFile(_filePath, "저장 파일", out json, out errorMessage);
    }

    public ReadResult ReadBackup(out string json, out string errorMessage)
    {
        return ReadFile(_backupFilePath, "백업 저장 파일", out json, out errorMessage);
    }

    public bool TryWrite(string json, out string errorMessage)
    {
        lock (_writeLock)
        {
            return TryWriteCore(json, out errorMessage);
        }
    }

    /// <summary>검증된 백업 복원 후 다음 저장에서 기존 백업 보존</summary>
    public void PreserveBackupOnNextWrite()
    {
        lock (_writeLock) { _preserveBackup = true; }
    }

    private bool TryWriteCore(string json, out string errorMessage)
    {
        try
        {
            Directory.CreateDirectory(_directoryPath);
            WriteTemporaryFile(json);

            if (File.Exists(_filePath))
            {
                File.Replace(_temporaryFilePath, _filePath, _preserveBackup ? null : _backupFilePath);
            }
            else
            {
                File.Move(_temporaryFilePath, _filePath);
            }

            _preserveBackup = false;
            errorMessage = string.Empty;
            return true;
        }
        catch (IOException exception)
        {
            errorMessage = $"저장 파일을 쓰지 못했습니다. {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            errorMessage = $"저장 파일에 접근할 권한이 없습니다. {exception.Message}";
            return false;
        }
        catch (NotSupportedException exception)
        {
            errorMessage = $"현재 플랫폼에서 저장 파일 교체를 지원하지 않습니다. {exception.Message}";
            return false;
        }
    }

    /// <summary>
    /// 기본, 백업, 임시 저장 파일 삭제
    /// </summary>
    public bool TryDelete(out string errorMessage)
    {
        lock (_writeLock)
        {
            return TryDeleteCore(out errorMessage);
        }
    }

    private bool TryDeleteCore(out string errorMessage)
    {
        try
        {
            File.Delete(_temporaryFilePath);
            File.Delete(_filePath);
            File.Delete(_backupFilePath);
            _preserveBackup = false;
            errorMessage = string.Empty;
            return true;
        }
        catch (IOException exception)
        {
            errorMessage = $"저장 파일을 삭제하지 못했습니다. {exception.Message}";
            return false;
        }
        catch (UnauthorizedAccessException exception)
        {
            errorMessage = $"저장 파일을 삭제할 권한이 없습니다. {exception.Message}";
            return false;
        }
        catch (NotSupportedException exception)
        {
            errorMessage = $"현재 플랫폼에서 저장 파일 삭제를 지원하지 않습니다. {exception.Message}";
            return false;
        }
    }

    private static ReadResult ReadFile(
            string filePath,
            string fileDescription,
            out string json,
            out string errorMessage)
    {
        json = string.Empty;

        if (!File.Exists(filePath))
        {
            errorMessage = string.Empty;
            return ReadResult.NotFound;
        }

        try
        {
            json = File.ReadAllText(filePath, Utf8Encoding);
            errorMessage = string.Empty;
            return ReadResult.Success;
        }
        catch (IOException exception)
        {
            errorMessage = $"{fileDescription}을 읽지 못했습니다. {exception.Message}";
            return ReadResult.Failed;
        }
        catch (UnauthorizedAccessException exception)
        {
            errorMessage = $"{fileDescription}에 접근할 권한이 없습니다. {exception.Message}";
            return ReadResult.Failed;
        }
    }

    private void WriteTemporaryFile(string json)
    {
        byte[] bytes = Utf8Encoding.GetBytes(json);

        using FileStream stream = new(
                _temporaryFilePath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None);

        stream.Write(bytes, 0, bytes.Length);
        stream.Flush(true);
    }
}
