using System;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 현재 열린 씬의 데이터 설정을 이용한 Google Sheets 로컬 캐시 갱신 메뉴
/// </summary>
public static class DownloadGoogleSheetsToLocalCache
{
    private const string MenuPath = "Window/GSTU/Download Google Sheets To Local Cache";

    private static bool _isSynchronizing;

    [MenuItem(MenuPath)]
    private static void Synchronize()
    {
        DataManager dataManager = UnityEngine.Object.FindAnyObjectByType<DataManager>(FindObjectsInactive.Include);

        if (dataManager == null)
        {
            EditorUtility.DisplayDialog(
                "Google Sheets 동기화 실패",
                "DataManager가 있는 TitleScene 또는 HomeScene을 연 뒤 다시 실행해 주세요.",
                "확인");
            return;
        }

        _isSynchronizing = true;

        try
        {
            dataManager.SynchronizeGoogleSheetsToLocalCache(OnSynchronizationCompleted);
            Debug.Log("[GameData] Google Sheets 로컬 캐시 동기화 시작");
        }
        catch (Exception exception)
        {
            _isSynchronizing = false;
            Debug.LogException(exception);
            EditorUtility.DisplayDialog("Google Sheets 동기화 실패", exception.Message, "확인");
        }
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateSynchronize()
    {
        return !_isSynchronizing && !Application.isPlaying;
    }

    private static void OnSynchronizationCompleted(bool succeeded, string message)
    {
        _isSynchronizing = false;

        string title = succeeded ? "Google Sheets 동기화 완료" : "Google Sheets 동기화 실패";
        Debug.Log($"[GameData] {title}: {message}");
        EditorUtility.DisplayDialog(title, message, "확인");
    }
}
