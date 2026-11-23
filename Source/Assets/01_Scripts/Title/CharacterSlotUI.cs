using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 캐릭터 슬롯 하나의 표시 상태 관리
/// </summary>
public sealed class CharacterSlotUI : MonoBehaviour
{
    [SerializeField] private GameObject _emptyState;
    [SerializeField] private GameObject _characterState;

    [Header("캐릭터 정보")]
    [SerializeField] private Image _classImage;
    [SerializeField] private TMP_Text _classText;
    [SerializeField] private TMP_Text _levelText;

    [Header("슬롯 삭제")]
    [SerializeField] private GameObject _deleteButton;


    public void Render(CharacterSlotEntry entry, Sprite classImage)
    {
        bool isEmpty = entry.State == CharacterSlotState.Empty;
        bool isCorrupted = entry.State == CharacterSlotState.Corrupted;
        bool hasCharacter = !isEmpty && !isCorrupted;

        _emptyState.SetActive(isEmpty);
        _characterState.SetActive(hasCharacter);
        _deleteButton.SetActive(!isEmpty);

        if (hasCharacter)
        {
            _classImage.sprite = classImage;
            _classImage.preserveAspect = true;
            _classText.text = GetClassName(entry.Summary.Class);
            _levelText.text = $"Lv. {entry.Summary.Level}";
        }


    }

    private static string GetClassName(PlayerClass playerClass)
    {
        return playerClass switch
        {
            PlayerClass.Warrior => "전사",
            PlayerClass.Archer => "궁수",
            PlayerClass.Magician => "마법사",
            _ => "알 수 없음"
        };
    }
}
