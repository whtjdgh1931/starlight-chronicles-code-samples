using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 그룹 윤곽 분석과 자동 생성 발판의 Undo 지원 교체
/// </summary>
internal static class PlatformPlacementBuilder
{
    private const string UndoName = "타일맵 발판 콜라이더 갱신";

    internal sealed class Plan
    {
        internal List<PlatformSurfaceAnalyzer.Surface> Surfaces { get; }
        internal List<GeneratedPlatformCollider> Previous { get; }

        internal Plan(List<PlatformSurfaceAnalyzer.Surface> surfaces, List<GeneratedPlatformCollider> previous)
        {
            Surfaces = surfaces;
            Previous = previous;
        }
    }

    /// <summary>
    /// 명시적으로 선택한 Tilemap 그룹만 분석
    /// 기존 생성 그룹 일부만 선택한 경우 잔여 충돌선 손실 방지를 위해 중단
    /// </summary>
    internal static Plan BuildPlan(PlatformPlacementSettings settings)
    {
        ValidateConfiguration(settings);
        List<Tilemap> tilemaps = settings.GetTilemaps();
        var previous = new List<GeneratedPlatformCollider>();
        foreach (GameObject root in settings.Tilemap.gameObject.scene.GetRootGameObjects())
        {
            foreach (GeneratedPlatformCollider generated in root.GetComponentsInChildren<GeneratedPlatformCollider>(true))
            {
                if (!tilemaps.Any(generated.ContainsSource))
                {
                    continue;
                }
                if (generated.Sources.Any(source => !tilemaps.Contains(source)))
                {
                    throw new InvalidOperationException("기존 생성 그룹의 모든 Tilemap을 함께 선택하세요.");
                }
                ValidateGeneratedObject(generated);
                previous.Add(generated);
            }
        }
        return new Plan(PlatformSurfaceAnalyzer.Analyze(settings), previous);
    }

    private static void ValidateConfiguration(PlatformPlacementSettings settings)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            throw new InvalidOperationException("플레이 종료 후 사용하세요.");
        }
        if (settings.Tilemap == null || settings.PhysicsParent == null)
        {
            throw new InvalidOperationException("시각용 Tilemap과 발판 부모를 지정하세요.");
        }
        Scene scene = settings.Tilemap.gameObject.scene;
        Transform parent = settings.PhysicsParent;
        if (!scene.IsValid() || !scene.isLoaded || EditorSceneManager.IsPreviewScene(scene) ||
            EditorUtility.IsPersistent(parent) || scene != parent.gameObject.scene ||
            !parent.gameObject.activeInHierarchy || parent.GetComponentInParent<Rigidbody2D>() != null ||
            parent.GetComponentInParent<GeneratedPlatformCollider>() != null)
        {
            throw new InvalidOperationException("같은 일반 씬의 활성화된 정적 발판 부모를 지정하세요.");
        }
        foreach (Tilemap tilemap in settings.GetTilemaps())
        {
            if (tilemap.gameObject.scene != scene || !tilemap.gameObject.activeInHierarchy ||
                !tilemap.TryGetComponent(out TilemapRenderer renderer) || !renderer.enabled ||
                tilemap.GetComponent<Collider2D>() != null || tilemap.GetComponentInParent<Rigidbody2D>() != null)
            {
                throw new InvalidOperationException($"{tilemap.name}: 같은 씬의 콜라이더 없는 활성 시각용 Tilemap 필요");
            }
            Grid grid = tilemap.layoutGrid;
            if (grid == null || grid.cellLayout != GridLayout.CellLayout.Rectangle ||
                tilemap.orientation != Tilemap.Orientation.XY)
            {
                throw new InvalidOperationException("직사각형 XY Grid 필요");
            }
        }
        if (settings.ScanCellLimit < 1 || settings.PlatformLimit < 1 ||
            !PlatformSurfaceAnalyzer.IsFinite(settings.SurfaceOffset) ||
            !PlatformSurfaceAnalyzer.IsFinite(settings.SurfaceArc) ||
            settings.SurfaceArc <= 0f || settings.SurfaceArc > 180f ||
            !PlatformSurfaceAnalyzer.IsFinite(settings.AlphaThreshold) ||
            settings.AlphaThreshold <= 0f || settings.AlphaThreshold > 1f)
        {
            throw new InvalidOperationException("검사 제한·높이 보정·충돌 각도·알파 기준 확인 필요");
        }
    }

    /// <summary>
    /// 연결된 외곽선마다 하나의 EdgeCollider 생성
    /// 떨어진 층은 별도 Collider로 유지하여 밑점프 시 아래 발판 충돌 보존
    /// </summary>
    internal static int Rebuild(PlatformPlacementSettings settings)
    {
        Plan plan = BuildPlan(settings);
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName(UndoName);
        try
        {
            foreach (GeneratedPlatformCollider generated in plan.Previous)
            {
                Undo.DestroyObjectImmediate(generated.gameObject);
            }
            foreach (PlatformSurfaceAnalyzer.Surface surface in plan.Surfaces)
            {
                CreatePlatform(settings, surface);
            }
            EditorSceneManager.MarkSceneDirty(settings.Tilemap.gameObject.scene);
            Undo.FlushUndoRecordObjects();
            Undo.CollapseUndoOperations(group);
            return plan.Surfaces.Count;
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

    private static void CreatePlatform(PlatformPlacementSettings settings, PlatformSurfaceAnalyzer.Surface surface)
    {
        var platform = new GameObject(
            GameObjectUtility.GetUniqueNameForSibling(settings.PhysicsParent, "GeneratedPlatform"));
        Undo.RegisterCreatedObjectUndo(platform, UndoName);
        SceneManager.MoveGameObjectToScene(platform, settings.Tilemap.gameObject.scene);
        Undo.SetTransformParent(platform.transform, settings.PhysicsParent, UndoName);
        Undo.RecordObject(platform.transform, UndoName);
        platform.layer = Constants.LAYER_Ground;
        platform.tag = Constants.TAG_Ground;
        platform.transform.rotation = Quaternion.identity;
        platform.transform.localScale = Vector3.one;
        platform.transform.position = surface.Points[0];

        GeneratedPlatformCollider owner = Undo.AddComponent<GeneratedPlatformCollider>(platform);
        Undo.RecordObject(owner, UndoName);
        owner.Initialize(settings.GetTilemaps().ToArray());
        EdgeCollider2D edge = Undo.AddComponent<EdgeCollider2D>(platform);
        Undo.RecordObject(edge, UndoName);
        edge.points = surface.Points.Select(point => (Vector2)platform.transform.InverseTransformPoint(point)).ToArray();
        // 닫힌 선의 시작·끝에도 이웃 정점을 제공하여 접점 법선의 불연속 방지
        edge.useAdjacentStartPoint = true;
        edge.useAdjacentEndPoint = true;
        edge.adjacentStartPoint = edge.points[edge.pointCount - 2];
        edge.adjacentEndPoint = edge.points[1];
        if (settings.UseOneWay)
        {
            PlatformEffector2D effector = Undo.AddComponent<PlatformEffector2D>(platform);
            Undo.RecordObject(effector, UndoName);
            effector.useColliderMask = false;
            effector.useOneWay = true;
            effector.useOneWayGrouping = true;
            effector.surfaceArc = settings.SurfaceArc;
            effector.useSideFriction = false;
            effector.useSideBounce = false;
            edge.usedByEffector = true;
        }
    }

    private static void ValidateGeneratedObject(GeneratedPlatformCollider generated)
    {
        if (PrefabUtility.IsPartOfPrefabInstance(generated) || generated.transform.childCount > 0 ||
            !generated.TryGetComponent(out EdgeCollider2D _))
        {
            throw new InvalidOperationException($"{generated.name}: 수정된 생성 발판의 수동 구성 확인 필요");
        }
        foreach (Component component in generated.GetComponents<Component>())
        {
            if (!(component is Transform) && !(component is GeneratedPlatformCollider) &&
                !(component is EdgeCollider2D) && !(component is PlatformEffector2D))
            {
                throw new InvalidOperationException($"{generated.name}: 추가 컴포넌트 보존을 위해 갱신 중단");
            }
        }
    }
}