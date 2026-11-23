using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 자동 생성 발판의 원본 Tilemap 그룹 식별 정보
/// 런타임 동작 없이 에디터 재생성 대상만 구분
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("")]
public sealed class GeneratedPlatformCollider : MonoBehaviour
{
    [SerializeField] private Tilemap _sourceTilemap;
    [SerializeField] private Tilemap[] _sourceTilemaps = System.Array.Empty<Tilemap>();

    public Tilemap SourceTilemap => _sourceTilemap;
    public IEnumerable<Tilemap> Sources => _sourceTilemaps.Length > 0
        ? _sourceTilemaps : new[] { _sourceTilemap };

    /// <summary>
    /// 기존 단일 Tilemap 저장 정보와 그룹 저장 정보 모두 지원
    /// </summary>
    public bool ContainsSource(Tilemap tilemap)
    {
        foreach (Tilemap source in Sources)
        {
            if (source == tilemap)
            {
                return true;
            }
        }
        return false;
    }

    public void Initialize(Tilemap sourceTilemap)
    {
        Initialize(new[] { sourceTilemap });
    }

    /// <summary>
    /// 재생성 시 부분 선택 여부를 판별하기 위한 전체 원본 그룹 연결
    /// </summary>
    public void Initialize(Tilemap[] sourceTilemaps)
    {
        _sourceTilemaps = sourceTilemaps;
        _sourceTilemap = sourceTilemaps[0];
    }
}