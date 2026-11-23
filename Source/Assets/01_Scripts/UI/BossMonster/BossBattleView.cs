using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 공용 기회·본인 부활 요청·파티 게임 오버 표시
/// </summary>
public sealed class BossBattleView : MonoBehaviour
{
    [SerializeField] private GameObject _counterRoot;
    [SerializeField] private TextMeshProUGUI _deathCountText;
    [SerializeField] private Button _reviveButton;
    [SerializeField] private TextMeshProUGUI _reviveButtonText;
    [SerializeField] private GameObject _gameOverRoot;
    [SerializeField] private AudioSource _resultAudioSource;
    [SerializeField] private AudioClip _victoryClip;
    [SerializeField] private AudioClip _defeatClip;
    [SerializeField] private TextMeshProUGUI _resultTitle;
    [SerializeField] private TextMeshProUGUI _resultDetails;
    [SerializeField] private Button _returnButton;
    [SerializeField] private Button _retrySaveButton;
    private NetworkBattleResultSession _results;
    private bool _returnConfirmed;
    private bool _forfeitConfirmed;
    private string _rewardMessageKey;
    private string _displayedResultId;
    private BossRewardSelectionView _rewards;
    private TMP_Text _returnLabel;
    private TMP_Text _retryLabel;
    private NetworkBossBattleController.BattleStage _presentedStage;
    private NetworkBossBattleController _battle;
    private NetworkPlayerLifeController _player;
    private DataManager _dataManager;

    /// <summary>협동 보스전 상태와 로드된 문구 데이터 연결</summary>
    public void Initialize(NetworkBossBattleController battle, DataManager dataManager)
    {
        if (_resultAudioSource == null || _victoryClip == null || _defeatClip == null)
        {
            throw new MissingReferenceException($"{name}: 보스전 결과 효과음 참조 필요");
        }
        Shutdown();
        _battle = battle;
        _dataManager = dataManager;
        _results = battle.NetworkManager.GetComponent<NetworkSessionController>().BattleResults;
        _returnConfirmed = false;
        _forfeitConfirmed = false;
        _rewardMessageKey = string.Empty;
        _displayedResultId = null;
        _returnLabel = _returnButton.GetComponentInChildren<TMP_Text>();
        _retryLabel = _retrySaveButton.GetComponentInChildren<TMP_Text>();
        _rewards ??= new BossRewardSelectionView(_gameOverRoot.transform, _resultDetails.font, HandleClaimReward, HandleForfeitRewards);
        FitResultLayout();
        _results.Changed += Refresh;
        _returnButton.onClick.AddListener(HandleReturnClicked);
        _retrySaveButton.onClick.AddListener(HandleRetrySaveClicked);
        _presentedStage = battle.Stage;
        _battle.Changed += Refresh;
        _reviveButton.onClick.AddListener(HandleReviveClicked);
        _counterRoot.SetActive(true);
        Refresh();
    }

    public void BindPlayer(NetworkPlayerLifeController player)
    {
        if (_player != null)
        {
            _player.Changed -= Refresh;
        }
        _player = player;
        _player.Changed += Refresh;
        Refresh();
    }

    public void UnbindPlayer(NetworkPlayerLifeController player)
    {
        if (_player != player)
        {
            return;
        }
        _player.Changed -= Refresh;
        _player = null;
        Refresh();
    }

    private void Refresh()
    {
        if (_battle == null)
        {
            return;
        }
        _deathCountText.text = _dataManager.GetString(Constants.STR_Boss_RemainingRevivesFormat, _battle.RemainingDeaths);
        bool isDead = _player != null && !_player.IsAlive;
        bool canRevive = _player != null && _player.CanRevive;
        _reviveButton.gameObject.SetActive(_battle.IsFighting && isDead);
        _reviveButton.interactable = canRevive;
        _reviveButtonText.text = _dataManager.GetString(
            canRevive ? Constants.STR_Boss_OnlineReviveAction : Constants.STR_Boss_OnlineRevivePreparing);
        bool finished = _battle.Stage == NetworkBossBattleController.BattleStage.GameOver ||
            _battle.Stage == NetworkBossBattleController.BattleStage.Cleared;
        _gameOverRoot.SetActive(finished);
        if (finished)
        {
            _results.BeginReceiving();
            _resultTitle.text = _dataManager.GetString(_battle.Stage == NetworkBossBattleController.BattleStage.Cleared
                ? Constants.STR_Boss_OnlineCleared : Constants.STR_Boss_OnlineDefeated);
            NetworkBattleResultSession.Result result = _results.Current;
            if (_displayedResultId != result?.Id)
            {
                _displayedResultId = result?.Id;
                _returnConfirmed = false;
                _forfeitConfirmed = false;
                _rewardMessageKey = string.Empty;
            }
            string status = _results.Error.Length > 0
                ? _results.Error
                : !_results.IsSaved ? _dataManager.GetString(Constants.STR_Boss_OnlineResultSaving)
                : _results.CanReturn ? _dataManager.GetString(Constants.STR_Boss_OnlineResultSaved)
                : _dataManager.GetString(Constants.STR_Boss_OnlineResultWaitingFormat, _results.WaitingReceiptCount);
            if (result == null)
            {
                _resultDetails.text = _results.Error.Length > 0 ? _results.Error : _dataManager.GetString(Constants.STR_Boss_OnlineResultReceiving);
            }
            else
            {
                string summary = _dataManager.GetString(Constants.STR_Boss_OnlineResultSummaryFormat,
                    result.Experience, result.Gold, result.ItemCount, _results.UnclaimedRewardCount);
                _resultDetails.text = summary + "\n" + status;
            }
            _rewards.Refresh(_results, _forfeitConfirmed, _dataManager);
            _returnButton.interactable = _results.CanReturn;
            _retrySaveButton.gameObject.SetActive(!_results.IsSaved || _results.WaitingReceiptCount > 0);
            _retryLabel.text = _dataManager.GetString(result != null && !_results.IsSaved
                ? Constants.STR_Boss_OnlineResultRetrySave : Constants.STR_Boss_OnlineResultRequestAgain);
            string returnKey = !_returnConfirmed ? Constants.STR_Boss_OnlineReturn
                : _results.UnclaimedRewardCount > 0 ? Constants.STR_Boss_OnlineReturnForfeit : Constants.STR_Boss_OnlineReturnConfirm;
            _returnLabel.text = _dataManager.GetString(returnKey);
            if (_returnConfirmed)
            {
                _resultDetails.text += _results.UnclaimedRewardCount > 0
                    ? $"\n다시 누르면 미수령 아이템 {_results.UnclaimedRewardCount}개를 포기합니다."
                    : "\n미수신 결과는 유실될 수 있습니다.\n다시 눌러 귀환";
            }
            else if (_forfeitConfirmed)
            {
                _resultDetails.text += $"\n미수령 아이템 {_results.UnclaimedRewardCount}개를 포기할까요?";
            }
            else if (_rewardMessageKey.Length > 0)
            {
                _resultDetails.text += "\n" + _dataManager.GetString(_rewardMessageKey);
            }
        }
        PlayResultSound();
    }

    private void PlayResultSound()
    {
        NetworkBossBattleController.BattleStage stage = _battle.Stage;
        if (_presentedStage == stage)
        {
            return;
        }
        _presentedStage = stage;
        if (stage == NetworkBossBattleController.BattleStage.Cleared)
        {
            _resultAudioSource.PlayOneShot(_victoryClip);
        }
        else if (stage == NetworkBossBattleController.BattleStage.GameOver)
        {
            _resultAudioSource.PlayOneShot(_defeatClip);
        }
    }

    private void HandleReviveClicked() => _player.RequestRevive();

    private void HandleClaimReward(int index)
    {
        if (_results == null || index < 0 || index >= _results.Rewards.Count) { return; }
        LocalBattleResult.Reward reward = _results.Rewards[index];
        if (reward.Status != LocalBattleResult.RewardStatus.Pending) { return; }
        _returnConfirmed = false;
        _forfeitConfirmed = false;
        _rewardMessageKey = string.Empty;
        if (_results.ClaimReward(reward) == InventoryResult.InventoryFull)
        {
            _rewardMessageKey = Constants.STR_Boss_OnlineRewardInventoryFull;
        }
        Refresh();
    }

    private void HandleForfeitRewards()
    {
        if (_results.UnclaimedRewardCount == 0) { return; }
        _returnConfirmed = false;
        if (_forfeitConfirmed)
        {
            _forfeitConfirmed = false;
            _rewardMessageKey = Constants.STR_Boss_OnlineRewardForfeitDone;
            _results.ForfeitRemainingRewards();
        }
        else
        {
            _forfeitConfirmed = true;
        }
        Refresh();
    }

    private void HandleReturnClicked()
    {
        if (!_results.CanReturn) { return; }
        _forfeitConfirmed = false;
        if (!_returnConfirmed && (_results.UnclaimedRewardCount > 0 || !_results.IsSaved || _results.WaitingReceiptCount > 0))
        {
            _returnConfirmed = true;
            Refresh();
            return;
        }
        if (_results.UnclaimedRewardCount > 0)
        {
            _returnConfirmed = false;
            _rewardMessageKey = Constants.STR_Boss_OnlineRewardForfeitDone;
            _results.ForfeitRemainingRewards();
            if (!_results.IsSaved || _results.WaitingReceiptCount > 0)
            {
                Refresh();
                return;
            }
        }
        _results.TryReturn();
    }

    private void HandleRetrySaveClicked()
    {
        _returnConfirmed = false;
        _forfeitConfirmed = false;
        if (_results.Current != null && !_results.IsSaved) { _results.TrySave(); }
        else { _results.RequestAgain(); }
        Refresh();
    }

    private void FitResultLayout()
    {
        _resultTitle.rectTransform.anchorMin = new Vector2(0.1f, 0.71f);
        _resultTitle.rectTransform.anchorMax = new Vector2(0.9f, 0.88f);
        _resultDetails.rectTransform.anchorMin = new Vector2(0.1f, 0.51f);
        _resultDetails.rectTransform.anchorMax = new Vector2(0.9f, 0.70f);
    }

    public void Shutdown()
    {
        // 이미 종료된 연결의 중복 정리 제외
        if (_battle == null && _player == null)
        {
            return;
        }
        if (_battle != null)
        {
            _battle.Changed -= Refresh;
        }
        if (_player != null)
        {
            _player.Changed -= Refresh;
        }
        _results.Changed -= Refresh;
        _returnButton.onClick.RemoveListener(HandleReturnClicked);
        _retrySaveButton.onClick.RemoveListener(HandleRetrySaveClicked);
        _reviveButton.onClick.RemoveListener(HandleReviveClicked);
        _counterRoot.SetActive(false);
        _reviveButton.gameObject.SetActive(false);
        _gameOverRoot.SetActive(false);
        _rewards?.Hide();
        if (_resultAudioSource != null)
        {
            _resultAudioSource.Stop();
        }
        _battle = null;
        _player = null;
        _results = null;
        _dataManager = null;
    }

    private void OnDisable() => Shutdown();
}
