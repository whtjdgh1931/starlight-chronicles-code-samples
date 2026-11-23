using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;



public class Portal : MonoBehaviour
{
    /// <summary>
    /// 포탈 고유 번호
    /// </summary>
    public int PortalID;

    /// <summary>
    /// 공유할 이동 데이터 ID, 0이면 포탈 고유 번호 사용
    /// </summary>
    [SerializeField, Min(0)] private int _destinationDataId;

    private readonly HashSet<Collider2D> _localPlayerColliders = new();
    private bool _isPlayerInPortal = false;
    public bool IsPlayerInside => _isPlayerInPortal;
    public event System.Action<Portal> PlayerExited;

    /// <summary>
    /// 입장 확정 후 같은 접촉에서 재요청 방지
    /// </summary>
    public void CompleteEntry() => ClearPortalState();

    private async void Update()
    {
        if (_isPlayerInPortal && Keyboard.current != null && Keyboard.current.upArrowKey.wasPressedThisFrame)
        {
            Player player = GameManager.Instance.PlaySessionController.ActivePlayer;
            if (player == null || player.IsDead || player.IsNetworkJoinLocked)
            {
                return;
            }

            int destinationDataId = _destinationDataId > 0 ? _destinationDataId : PortalID;
            PortalData data = GameManager.Instance.DataManager.GetPortalData(destinationDataId);

            if (data != null)
            {
                if (player.IsNetworkControlled)
                {
                    try
                    {
                        bool started = await player.GetComponent<NetworkPlayerController>().TryStartBossEncounterFromPortalAsync(data.TargetScene);
                        // 씬 전환으로 제거된 포탈의 후속 접근 제외
                        if (started && this != null) { ClearPortalState(); }
                    }
                    catch (System.Exception exception)
                    {
                        if (this != null) { Debug.LogException(exception, this); }
                    }
                    return;
                }
                if (gameObject.scene.name == nameof(GameSceneId.HostScene) &&
                    data.TargetScene == nameof(GameSceneId.BossScene))
                {
                    GameManager.Instance.UIManager.OpenSoloBossEntry(this, player, data.TargetPortalID);
                    return;
                }
                if (data.TargetScene == nameof(GameSceneId.HuntScene))
                {
                    GameManager.Instance.UIManager.OpenHuntEntry(this, player, data.TargetPortalID);
                    return;
                }
                ClearPortalState();
                GameManager.Instance.GameSceneManager.ChangeMapScene(data.TargetScene, data.TargetPortalID);
            }
            else
            {
                Debug.LogError($"포탈 {PortalID}: 구글 시트에 {destinationDataId}번 이동 데이터가 없습니다!");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.TryGetComponent(out Player player) &&
            player == GameManager.Instance.PlaySessionController.ActivePlayer)
        {
            _localPlayerColliders.Add(collision);
            _isPlayerInPortal = true;
        }
    }

    /// <summary>
    /// 기록된 진입 Collider 제거와 남은 접촉 상태 반영
    /// 종료·씬 정리 시 GameManager 재조회 제외
    /// </summary>
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!_localPlayerColliders.Remove(collision)) { return; }
        _isPlayerInPortal = _localPlayerColliders.Count > 0;
        if (!_isPlayerInPortal) { PlayerExited?.Invoke(this); }
    }

    private void OnDisable()
    {
        ClearPortalState();
        PlayerExited?.Invoke(this);
    }

    /// <summary>
    /// 이동 요청 성공·비활성화 시 이전 접촉과 활성 상태 정리
    /// </summary>
    private void ClearPortalState()
    {
        _localPlayerColliders.Clear();
        _isPlayerInPortal = false;
    }
}
