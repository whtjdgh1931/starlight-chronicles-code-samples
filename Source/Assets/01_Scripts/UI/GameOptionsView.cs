using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 옵션 창 버튼·저장 결과 표시와 열기·닫기
/// </summary>
public sealed class GameOptionsView : MonoBehaviour
{
    [SerializeField] private Button _openButton;
    [SerializeField] private Button _saveButton;
    [SerializeField] private Button _saveAndQuitButton;
    [SerializeField] private Button _quitButton;
    [SerializeField] private Button _closeButton;
    [SerializeField] private TMP_Text _contextText;
    [SerializeField] private TMP_Text _resultText;
    [SerializeField] private GameObject _controlsRoot;
    [SerializeField, Min(0.1f)] private float _refreshInterval = 0.25f;
    private readonly PopupSelection _selection = new();
    private GameOptionsController _controller;
    private float _nextRefresh;

    /// <summary>
    /// Inspector에 연결된 버튼·텍스트와 옵션 동작 연결
    /// </summary>
    public void Initialize(GameOptionsController controller)
    {
        if (_controller != null) { throw new InvalidOperationException("옵션 창이 이미 초기화되었습니다."); }
        if (_openButton == null || _saveButton == null || _saveAndQuitButton == null || _quitButton == null ||
            _closeButton == null || _contextText == null || _resultText == null || _controlsRoot == null)
        {
            throw new MissingReferenceException("옵션 창 버튼·결과 텍스트·조작 안내 참조가 필요합니다.");
        }
        _controller = controller ?? throw new ArgumentNullException(nameof(controller));
        _controlsRoot.SetActive(false);
        _openButton.onClick.AddListener(Open);
        _saveButton.onClick.AddListener(HandleSave);
        _saveAndQuitButton.onClick.AddListener(_controller.SaveAndQuit);
        _quitButton.onClick.AddListener(_controller.QuitWithoutSaving);
        _closeButton.onClick.AddListener(Close);
        _controller.Changed += Render;
        _controller.Refresh();
        Render();
        gameObject.SetActive(false);
    }

    /// <summary>
    /// 현재 대상과 마지막 저장 결과를 표시한 옵션 창 열기
    /// </summary>
    public void Open()
    {
        _controlsRoot.SetActive(false);
        gameObject.SetActive(true);
        // 열기 버튼과 창을 함께 묶은 UI 루트의 표시 순서 갱신
        transform.parent.SetAsLastSibling();
        _controller.Refresh();
        Render();
        _selection.Open(gameObject, _closeButton);
    }

    /// <summary>
    /// 조작 안내 우선 닫기와 옵션 창 숨김, 마지막 결과 유지
    /// </summary>
    public void Close()
    {
        if (!_controller.CanQuit) { return; }
        if (_controlsRoot.activeSelf) { _controlsRoot.SetActive(false); }
        else { gameObject.SetActive(false); }
    }

    public void Toggle()
    {
        if (gameObject.activeSelf) { Close(); }
        else { Open(); }
    }

    public void SetOpenButtonVisible(bool visible) => _openButton.gameObject.SetActive(visible);

    public void QuitWithoutSaving() => _controller.QuitWithoutSaving();

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh) { return; }
        _nextRefresh = Time.unscaledTime + _refreshInterval;
        _controller.Refresh();
    }

    private void HandleSave() => _controller.Save();

    private void OnDisable() => _selection.Close(gameObject);

    private void Render()
    {
        _saveButton.interactable = _controller.CanSave;
        _saveAndQuitButton.interactable = _controller.CanSave;
        _quitButton.interactable = _controller.CanQuit;
        _closeButton.interactable = _controller.CanQuit;
        _openButton.interactable = _controller.CanQuit;
        _contextText.text = _controller.Context;
        _resultText.transform.parent.gameObject.SetActive(_controller.LastResult.Length > 0);
        if (_resultText.text != _controller.LastResult)
        {
            _resultText.text = _controller.LastResult;
            _resultText.rectTransform.anchoredPosition = Vector2.zero;
        }
        _resultText.color = _controller.State == GameOptionsController.OperationState.Failed
            ? new Color(0.65f, 0.12f, 0.08f) : _controller.State == GameOptionsController.OperationState.Saved
                ? new Color(0.15f, 0.35f, 0.12f) : new Color(0.22f, 0.13f, 0.07f);
    }

    private void OnDestroy()
    {
        if (_controller == null) { return; }
        _controller.Changed -= Render;
        if (_openButton != null) { _openButton.onClick.RemoveListener(Open); }
        if (_saveButton != null) { _saveButton.onClick.RemoveListener(HandleSave); }
        if (_saveAndQuitButton != null) { _saveAndQuitButton.onClick.RemoveListener(_controller.SaveAndQuit); }
        if (_quitButton != null) { _quitButton.onClick.RemoveListener(_controller.QuitWithoutSaving); }
        if (_closeButton != null) { _closeButton.onClick.RemoveListener(Close); }
    }
}
