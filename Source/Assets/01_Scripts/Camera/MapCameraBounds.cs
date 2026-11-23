using System;
using Cinemachine;
using UnityEngine;

/// <summary>
/// Cinemachine 추적 결과를 화면 크기만큼 안쪽의 맵 경계로 제한
/// </summary>
public sealed class MapCameraBounds : CinemachineExtension
{
    [SerializeField] private Camera _outputCamera;
    [SerializeField] private Rect _mapBounds;
    [SerializeField] private bool _limitVertically;

    /// <summary>
    /// 맵 추적 연결 전 필수 화면과 경계 크기 검증
    /// </summary>
    public void ValidateConfiguration()
    {
        if (_outputCamera == null)
        {
            throw new MissingReferenceException($"{name}: 출력 카메라 참조 필요");
        }
        if (!_outputCamera.orthographic || _mapBounds.width <= 0f || _mapBounds.height <= 0f)
        {
            throw new InvalidOperationException($"{name}: 직교 카메라와 양수 맵 경계 크기 필요");
        }
    }

    protected override void PostPipelineStageCallback(
        CinemachineVirtualCameraBase vcam, CinemachineCore.Stage stage,
        ref CameraState state, float deltaTime)
    {
        // 편집기에서 참조 연결 이전에도 실행되는 확장 콜백 경계
        if (stage != CinemachineCore.Stage.Finalize || _outputCamera == null ||
            _mapBounds.width <= 0f || _mapBounds.height <= 0f)
        {
            return;
        }

        float aspect = _outputCamera.aspect;
        LensSettings lens = state.Lens;
        float maximumHalfHeight = _mapBounds.width / (2f * aspect);
        if (_limitVertically)
        {
            maximumHalfHeight = Mathf.Min(maximumHalfHeight, _mapBounds.height * 0.5f);
        }

        // 화면보다 작은 맵과 넓은 화면비에서도 경계 밖 노출 방지
        lens.OrthographicSize = Mathf.Min(lens.OrthographicSize, maximumHalfHeight);
        state.Lens = lens;

        float halfHeight = lens.OrthographicSize;
        float halfWidth = halfHeight * aspect;
        Vector3 position = state.FinalPosition;
        Vector3 constrained = position;

        constrained.x = Mathf.Clamp(position.x,
            _mapBounds.xMin + halfWidth, _mapBounds.xMax - halfWidth);
        if (_limitVertically)
        {
            constrained.y = Mathf.Clamp(position.y,
                _mapBounds.yMin + halfHeight, _mapBounds.yMax - halfHeight);
        }
        state.PositionCorrection += constrained - position;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(
            new Vector3(_mapBounds.center.x, _mapBounds.center.y, 0f),
            new Vector3(_mapBounds.width, _mapBounds.height, 0f));
    }
}
