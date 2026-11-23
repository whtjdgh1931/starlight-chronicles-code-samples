using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>로컬 보스전 횟수와 사망 상태 표시</summary>
public sealed class LocalBossBattleView : MonoBehaviour
{
    public enum Prompt { None, Preparing, Revive, Defeat }

    [SerializeField] private GameObject _counterRoot;
    [SerializeField] private TMP_Text _remainingText;
    [SerializeField] private GameObject _promptRoot;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private Button _actionButton;
    [SerializeField] private TMP_Text _actionLabel;

    private readonly PopupSelection _selection = new();
    private DataManager _dataManager;
    private Prompt _prompt;
    private bool _initialized;
    public event Action ReviveRequested;
    public event Action ReturnRequested;

    /// <summary>로드된 문구 데이터 연결과 버튼 이벤트 등록</summary>
    public void Initialize(DataManager dataManager)
    {
        _dataManager = dataManager;
        if (_initialized) { return; }
        if (_counterRoot == null || _remainingText == null || _promptRoot == null || _message == null ||
            _actionButton == null || _actionLabel == null)
        {
            throw new MissingReferenceException($"{name}: 로컬 보스 HUD 참조 필요");
        }

        _actionButton.onClick.AddListener(HandleAction);
        _initialized = true;
        Hide();
    }

    public void Render(int remaining, Prompt prompt)
    {
        _counterRoot.SetActive(true);
        _remainingText.text = _dataManager.GetString(Constants.STR_Boss_RemainingRevivesFormat, remaining);
        if (_prompt == prompt) { return; }

        _selection.Close(_promptRoot);
        _prompt = prompt;
        _promptRoot.SetActive(prompt != Prompt.None);
        _actionButton.gameObject.SetActive(prompt is Prompt.Revive or Prompt.Defeat);

        _message.text = prompt switch
        {
            Prompt.Preparing => _dataManager.GetString(Constants.STR_Boss_SoloRevivePreparing),
            Prompt.Revive => _dataManager.GetString(Constants.STR_Boss_SoloReviveReady),
            Prompt.Defeat => _dataManager.GetString(Constants.STR_Boss_SoloDefeated),
            _ => string.Empty
        };

        if (prompt is Prompt.Revive or Prompt.Defeat)
        {
            _actionLabel.text = _dataManager.GetString(
                prompt == Prompt.Revive ? Constants.STR_Boss_SoloReviveAction : Constants.STR_Boss_SoloReturnToLobby);
            _selection.Open(_promptRoot, _actionButton);
        }
    }

    public void ShowReturnError(string message) => _message.text = message;

    public void Hide()
    {
        _selection.Close(_promptRoot);
        _prompt = Prompt.None;
        _promptRoot.SetActive(false);
        _counterRoot.SetActive(false);
    }

    private void HandleAction()
    {
        if (_prompt == Prompt.Revive) { ReviveRequested?.Invoke(); }
        if (_prompt == Prompt.Defeat) { ReturnRequested?.Invoke(); }
    }

    private void OnDestroy()
    {
        _selection.Close(_promptRoot);
        if (_actionButton != null) { _actionButton.onClick.RemoveListener(HandleAction); }
    }
}
