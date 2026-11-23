using UnityEngine;
using UnityEngine.UI;

public class TitleSceneController : MonoBehaviour
{
    [Header("UI 버튼")]
    [SerializeField] private Button _startButton;

    [SerializeField]
    private string _characterSelectSceneName = "SelectScene";

    private void Start()
    {
        // GameManager가 정상 초기화되었고 데이터 로딩이 가능한 상태인지 체크
        if (_startButton != null)
        {
            _startButton.onClick.AddListener(OnStartButtonClick);
        }
    }

    public void OpenOptions() => GameManager.Instance.UIManager.OpenGameOptions();

    public void QuitGame() => GameManager.Instance.UIManager.QuitFromMenu();

    private void OnStartButtonClick()
    {
        GameManager.Instance.GameSceneManager.ChangeScene(_characterSelectSceneName);
    }
}
