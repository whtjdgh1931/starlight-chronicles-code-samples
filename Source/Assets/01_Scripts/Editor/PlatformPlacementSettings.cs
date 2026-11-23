using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일 그림과 명시적인 Tilemap 그룹 기준 발판 생성 설정
/// </summary>
[Serializable]
internal sealed class PlatformPlacementSettings
{
    [Serializable]
    internal sealed class TileHeightOverride
    {
        public TileBase Tile;
        public float SurfaceOffset;
    }

    public Tilemap Tilemap;
    public List<Tilemap> GroupTilemaps = new List<Tilemap>();
    public Transform PhysicsParent;
    public bool UseOneWay = true;
    public bool UseSpritePhysicsShape;
    public float AlphaThreshold = 0.1f;
    public float SurfaceOffset;
    public float SurfaceArc = 150f;
    public int ScanCellLimit = 1000000;
    public int PlatformLimit = 2048;
    public List<TileHeightOverride> HeightOverrides = new List<TileHeightOverride>();

    /// <summary>
    /// 대표 Tilemap과 추가 그룹의 중복 없는 목록
    /// 장식 타일맵은 선택하지 않아 충돌 생성에서 제외
    /// </summary>
    internal List<Tilemap> GetTilemaps()
    {
        var maps = new List<Tilemap> { Tilemap };
        foreach (Tilemap tilemap in GroupTilemaps)
        {
            if (tilemap == null)
            {
                throw new InvalidOperationException("그룹의 빈 Tilemap 항목을 지정하거나 삭제하세요.");
            }
            if (!maps.Contains(tilemap))
            {
                maps.Add(tilemap);
            }
        }
        return maps;
    }
}