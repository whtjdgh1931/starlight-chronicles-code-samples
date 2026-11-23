using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버의 범위·돌진 연출을 클라이언트에 전달
/// 호스트는 기존 실행기 이벤트에서 연출 재생
/// </summary>
public sealed class NetworkSkillPresentation : NetworkBehaviour
{
    [SerializeField] private PlayerAreaSkillExecutor _areaExecutor;
    [SerializeField] private PlayerDashSkillExecutor _dashExecutor;
    [SerializeField] private AreaSkillPresentation _areaPresentation;
    [SerializeField] private DashSkillPresentation _dashPresentation;

    // 객체 제거·세션 종료 중 발생한 공격 취소 알림의 전송 제외
    private bool CanSend => IsSpawned && IsServer && !NetworkManager.ShutdownInProgress;

    public override void OnNetworkSpawn()
    {
        if (_areaExecutor == null || _dashExecutor == null ||
            _areaPresentation == null || _dashPresentation == null)
        {
            throw new MissingReferenceException($"{name}: 스킬 실행기와 연출 참조 필요");
        }

        _areaPresentation.SetSoundOwnership(IsOwner);
        _dashPresentation.SetSoundOwnership(IsOwner);

        if (IsServer)
        {
            _areaExecutor.AttackPerformed += HandleAreaAttack;
            _dashExecutor.DashStarted += HandleDashStarted;
            _dashExecutor.DashEnded += HandleDashEnded;
        }
    }

    private void HandleAreaAttack(Vector3 center, Vector2 range, int hitIndex, int directionX)
    {
        if (CanSend)
        {
            PlayAreaRpc(center, range, hitIndex, directionX);
        }
    }

    private void HandleDashStarted(Vector3 position, int directionX)
    {
        if (CanSend)
        {
            PlayDashStartRpc(position, directionX);
        }
    }

    private void HandleDashEnded()
    {
        if (CanSend)
        {
            PlayDashEndRpc();
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayAreaRpc(Vector3 center, Vector2 range, int hitIndex, int directionX)
    {
        if (!IsServer)
        {
            _areaPresentation.PlayAttack(center, range, hitIndex, directionX);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayDashStartRpc(Vector3 position, int directionX)
    {
        if (!IsServer)
        {
            _dashPresentation.PlayStart(position, directionX);
        }
    }

    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    private void PlayDashEndRpc()
    {
        if (!IsServer)
        {
            _dashPresentation.PlayEnd();
        }
    }

    public override void OnNetworkDespawn()
    {
        _areaExecutor.AttackPerformed -= HandleAreaAttack;
        _dashExecutor.DashStarted -= HandleDashStarted;
        _dashExecutor.DashEnded -= HandleDashEnded;
        _dashPresentation.Clear();
        _areaPresentation.SetSoundOwnership(true);
        _dashPresentation.SetSoundOwnership(true);
    }
}
