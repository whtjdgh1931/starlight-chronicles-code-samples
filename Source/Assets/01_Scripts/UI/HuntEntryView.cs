using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 사냥터 난이도별 해금 상태와 선택·취소 요청 표시
/// </summary>
public sealed class HuntEntryView : MonoBehaviour
{
    [Serializable]
    private sealed class DifficultyRow
    {
        public HuntDifficulty Difficulty;
        public Button Button;
        public TMP_Text Label;
    }

    [SerializeField] private DifficultyRow[] _rows;
    [SerializeField] private TMP_Text _levelText;
    [SerializeField] private TMP_Text _messageText;
    public bool IsOpen => gameObject.activeSelf;
    public event Action<HuntDifficulty> DifficultySelected;
    public event Action CloseRequested;

    /// <summary>
    /// 설정된 난이도와 화면 슬롯의 대응 확인
    /// </summary>
    public void Initialize()
    {
        var difficulties = (HuntDifficulty[])Enum.GetValues(typeof(HuntDifficulty));
        if (_rows.Length != difficulties.Length)
        {
            throw new InvalidOperationException($"{name}: 난이도 설정과 화면 슬롯 수 일치 필요");
        }
        foreach (HuntDifficulty difficulty in difficulties)
        {
            GetRow(difficulty);
        }
        Close();
    }

    public void Show(int playerLevel, IReadOnlyList<HuntDifficultyData> options)
    {
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        _levelText.text = $"현재 레벨 {playerLevel}\n원하는 사냥터 구성을 선택해 주세요.";
        _messageText.text = string.Empty;
        foreach (HuntDifficultyData option in options)
        {
            DifficultyRow row = GetRow(option.Difficulty);
            bool unlocked = option.AllowsEntry(playerLevel);
            row.Button.interactable = unlocked;
            row.Label.text = unlocked ? $"{option.Name}\n입장 가능" : $"{option.Name}\n{option.RequiredLevel}레벨부터";
        }
    }

    public void ShowMessage(string message) => _messageText.text = message;
    public void Close() => gameObject.SetActive(false);
    public void SelectDifficulty(int difficulty) => DifficultySelected?.Invoke((HuntDifficulty)difficulty);
    public void RequestClose() => CloseRequested?.Invoke();

    private DifficultyRow GetRow(HuntDifficulty difficulty)
    {
        foreach (DifficultyRow row in _rows)
        {
            if (row.Difficulty == difficulty) { return row; }
        }
        throw new InvalidOperationException($"{name}: {difficulty} 버튼 누락");
    }
}
