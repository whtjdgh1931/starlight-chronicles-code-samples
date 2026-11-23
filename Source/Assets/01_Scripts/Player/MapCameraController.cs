using Cinemachine;
using System;
using UnityEngine;

/// <summary>
/// 현재 플레이어와 Cinemachine 추적 대상 연결
/// </summary>
public sealed class MapCameraController : MonoBehaviour
{
    [SerializeField] private CinemachineVirtualCamera _cinemachineCamera;
    [SerializeField] private MapCameraBounds _cameraBounds;

    /// <summary>
    /// 현재 플레이 세션의 카메라 추적 대상 연결
    /// </summary>
    public void Initialize(Player player)
    {
        if (player == null)
        {
            throw new ArgumentNullException(nameof(player));
        }

        _cameraBounds.ValidateConfiguration();
        _cinemachineCamera.Follow = player.transform;
    }
}
