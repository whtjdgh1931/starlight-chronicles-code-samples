using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>혼자 도전 확인과 취소 표시</summary>
public sealed class SoloBossEntryView : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _message;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private TMP_Text _cancelLabel;

    private readonly PopupSelection _selection = new();
    public event Action Confirmed;
    public event Action Cancelled;
    public bool IsOpen => _root.activeSelf;

    public void Initialize()
    {
        if (_root == null || _confirmButton == null || _cancelButton == null ||
            _title == null || _message == null || _confirmLabel == null || _cancelLabel == null)
        {
            throw new MissingReferenceException($"{name}: 혼자 도전 팝업·버튼·문구 참조 필요");
        }

        _confirmButton.onClick.AddListener(HandleConfirm);
        _cancelButton.onClick.AddListener(HandleCancel);
        Hide();
    }

    /// <summary>로드된 문구 데이터로 입장 확인 팝업 표시</summary>
    public void Show(DataManager dataManager)
    {
        _title.text = dataManager.GetString(Constants.STR_Boss_SoloEntryTitle);
        _message.text = dataManager.GetString(Constants.STR_Boss_SoloEntryPrompt);
        _confirmLabel.text = dataManager.GetString(Constants.STR_Boss_SoloEntryConfirm);
        _cancelLabel.text = dataManager.GetString(Constants.STR_Boss_SoloEntryCancel);
        _root.SetActive(true);
        _root.transform.SetAsLastSibling();
        _selection.Open(_root, _confirmButton);
    }

    public void Hide()
    {
        _selection.Close(_root);
        _root.SetActive(false);
    }

    private void HandleConfirm() => Confirmed?.Invoke();

    private void HandleCancel() => Cancelled?.Invoke();

    private void OnDestroy()
    {
        _selection.Close(_root);
        if (_confirmButton != null) { _confirmButton.onClick.RemoveListener(HandleConfirm); }
        if (_cancelButton != null) { _cancelButton.onClick.RemoveListener(HandleCancel); }
    }
}
