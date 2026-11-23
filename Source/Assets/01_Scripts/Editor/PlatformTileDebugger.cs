using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 시각용 타일의 위치 검색과 셀 속성을 보존하는 Undo 지원 교체
/// </summary>
internal static class PlatformTileDebugger
{
    private const string UndoName = "타일맵 이미지 교체";

    internal readonly struct Cell
    {
        internal Tilemap Map { get; }
        internal Vector3Int Position { get; }

        internal Cell(Tilemap map, Vector3Int position)
        {
            Map = map;
            Position = position;
        }
    }

    /// <summary>
    /// 지정 범위의 같은 TileBase 참조를 가진 셀 검색
    /// </summary>
    internal static List<Cell> FindMatches(List<Tilemap> maps, TileBase source, int scanCellLimit)
    {
        if (source == null || scanCellLimit < 1)
        {
            throw new InvalidOperationException("찾을 타일과 1 이상의 검사 셀 수 제한 필요");
        }
        long scanned = 0;
        foreach (Tilemap map in maps)
        {
            ValidateMap(map);
            Vector3Int size = map.cellBounds.size;
            scanned += (long)size.x * size.y * size.z;
            if (scanned > scanCellLimit)
            {
                throw new InvalidOperationException("검사 셀 수 제한 초과. 검색할 Tilemap 범위를 줄이거나 제한을 조정하세요.");
            }
        }

        var matches = new List<Cell>();
        foreach (Tilemap map in maps)
        {
            foreach (Vector3Int position in map.cellBounds.allPositionsWithin)
            {
                if (map.GetTile(position) == source) { matches.Add(new Cell(map, position)); }
            }
        }
        return matches;
    }

    /// <summary>
    /// 실행 직전 원본 확인 후 지정 셀의 타일 참조만 교체
    /// </summary>
    internal static int Replace(List<Cell> cells, TileBase source, TileBase replacement)
    {
        if (source == null || replacement == null || source == replacement)
        {
            throw new InvalidOperationException("서로 다른 원본 타일과 교체 타일을 지정하세요.");
        }
        if (!EditorUtility.IsPersistent(replacement))
        {
            throw new InvalidOperationException("Project의 기존 타일 에셋을 지정하세요.");
        }
        var grouped = new Dictionary<Tilemap, List<Vector3Int>>();
        foreach (Cell cell in cells)
        {
            if (!grouped.TryGetValue(cell.Map, out List<Vector3Int> positions))
            {
                ValidateMap(cell.Map);
                positions = new List<Vector3Int>();
                grouped.Add(cell.Map, positions);
            }
            if (cell.Map.GetTile(cell.Position) != source)
            {
                throw new InvalidOperationException("선택한 셀의 타일이 바뀌었습니다. 다시 선택하거나 검색하세요.");
            }
            positions.Add(cell.Position);
        }
        if (cells.Count == 0) { return 0; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        int replaced = 0;
        try
        {
            foreach (var entry in grouped)
            {
                Tilemap map = entry.Key;
                var changes = new TileChangeData[entry.Value.Count];
                var flags = new TileFlags[entry.Value.Count];
                for (int i = 0; i < entry.Value.Count; i++)
                {
                    Vector3Int position = entry.Value[i];
                    changes[i] = new TileChangeData(position, replacement, map.GetColor(position), map.GetTransformMatrix(position));
                    flags[i] = map.GetTileFlags(position);
                }
                Undo.RegisterCompleteObjectUndo(map, UndoName);
                map.SetTiles(changes, true);
                for (int i = 0; i < changes.Length; i++) { map.SetTileFlags(changes[i].position, flags[i]); }
                PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                EditorSceneManager.MarkSceneDirty(map.gameObject.scene);
                replaced += changes.Length;
            }
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return replaced;
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally
        {
            Undo.IncrementCurrentGroup();
        }
    }

    /// <summary>
    /// 타일 에셋과 프리팹 원본을 변경하지 않는 일반 씬의 XY 타일맵 확인
    /// </summary>
    internal static void ValidateMap(Tilemap map)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 사용하세요.");
        }
        if (map == null || EditorUtility.IsPersistent(map) || !map.gameObject.scene.IsValid() ||
            !map.gameObject.scene.isLoaded || EditorSceneManager.IsPreviewScene(map.gameObject.scene) ||
            PrefabStageUtility.GetPrefabStage(map.gameObject) != null)
        {
            throw new InvalidOperationException("일반 씬의 Tilemap을 지정하세요. 프리팹 편집 모드는 지원하지 않습니다.");
        }
        if (map.layoutGrid == null || map.layoutGrid.cellLayout != GridLayout.CellLayout.Rectangle ||
            map.orientation != Tilemap.Orientation.XY)
        {
            throw new InvalidOperationException("직사각형 XY Grid의 Tilemap 필요");
        }
    }

    /// <summary>
    /// 명시한 부모 하위에서 같은 Sprite 참조를 가진 렌더러 검색
    /// </summary>
    internal static List<SpriteRenderer> FindSprites(Transform root, Sprite source, bool includeInactive, int rendererLimit)
    {
        ValidateSpriteRoot(root);
        if (source == null || rendererLimit < 1)
        {
            throw new InvalidOperationException("찾을 Sprite와 1 이상의 검사 개수 제한 필요");
        }
        SpriteRenderer[] renderers = root.GetComponentsInChildren<SpriteRenderer>(includeInactive);
        if (renderers.Length > rendererLimit)
        {
            throw new InvalidOperationException("렌더러 검사 개수 제한 초과. 검색 부모 범위를 줄이세요.");
        }
        var matches = new List<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer.sprite == source && (includeInactive || renderer.enabled)) { matches.Add(renderer); }
        }
        return matches;
    }

    /// <summary>
    /// 원본과 검색 범위 재확인 후 Sprite 참조만 교체
    /// Transform·크기·색상·콜라이더와 프리팹 원본 유지
    /// </summary>
    internal static int ReplaceSprites(Transform root, List<SpriteRenderer> renderers, Sprite source, Sprite replacement)
    {
        ValidateSpriteRoot(root);
        if (source == null || replacement == null || source == replacement || !EditorUtility.IsPersistent(replacement))
        {
            throw new InvalidOperationException("서로 다른 원본 Sprite와 Project의 교체 Sprite를 지정하세요.");
        }
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.transform.IsChildOf(root) || renderer.sprite != source)
            {
                throw new InvalidOperationException("검색 범위 또는 원본 이미지가 바뀌었습니다. 다시 선택하거나 검색하세요.");
            }
        }
        if (renderers.Count == 0) { return 0; }

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        try
        {
            foreach (SpriteRenderer renderer in renderers)
            {
                Undo.RecordObject(renderer, UndoName);
                renderer.sprite = replacement;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return renderers.Count;
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally
        {
            Undo.IncrementCurrentGroup();
        }
    }

    /// <summary>
    /// 같은 Sprite와 부모 범위를 확인한 뒤 Renderer 색상·투명도만 적용
    /// </summary>
    internal static int ApplySpriteColor(Transform root, List<SpriteRenderer> renderers, Sprite source, Color color)
    {
        ValidateSpriteRoot(root);
        if (source == null) { throw new InvalidOperationException("찾을 Sprite를 지정하세요."); }
        var changes = new List<SpriteRenderer>();
        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null || !renderer.transform.IsChildOf(root) || renderer.sprite != source)
            {
                throw new InvalidOperationException("검색 범위 또는 원본 이미지가 바뀌었습니다. 다시 선택하거나 검색하세요.");
            }
            if (renderer.color != color) { changes.Add(renderer); }
        }
        if (changes.Count == 0) { return 0; }

        const string undoName = "스프라이트 색상 변경";
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(undoName);
        try
        {
            foreach (SpriteRenderer renderer in changes)
            {
                Undo.RecordObject(renderer, undoName);
                renderer.color = color;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorUtility.SetDirty(renderer);
            }
            EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return changes.Count;
        }
        catch
        {
            Undo.FlushUndoRecordObjects();
            Undo.RevertAllDownToGroup(group);
            throw;
        }
        finally
        {
            Undo.IncrementCurrentGroup();
        }
    }

    private static void ValidateSpriteRoot(Transform root)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 사용하세요.");
        }
        if (root == null || EditorUtility.IsPersistent(root) || !root.gameObject.scene.IsValid() ||
            !root.gameObject.scene.isLoaded || EditorSceneManager.IsPreviewScene(root.gameObject.scene) ||
            PrefabStageUtility.GetPrefabStage(root.gameObject) != null)
        {
            throw new InvalidOperationException("일반 씬의 검색 부모를 지정하세요. 프리팹 편집 모드는 지원하지 않습니다.");
        }
    }

    internal static Vector3[] GetCellCorners(Cell cell)
    {
        return new[]
        {
            cell.Map.CellToWorld(cell.Position),
            cell.Map.CellToWorld(cell.Position + Vector3Int.right),
            cell.Map.CellToWorld(cell.Position + Vector3Int.right + Vector3Int.up),
            cell.Map.CellToWorld(cell.Position + Vector3Int.up)
        };
    }
}
