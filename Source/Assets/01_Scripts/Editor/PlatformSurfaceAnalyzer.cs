using System;
using System.Collections.Generic;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일 그림의 불투명 영역 추출과 그룹 전체 외곽선 병합
/// 임시 Preview Scene에서만 물리 형상 생성
/// </summary>
internal static class PlatformSurfaceAnalyzer
{
    internal const float GeometryTolerance = 0.0001f;

    internal readonly struct Surface
    {
        internal Vector3[] Points { get; }

        internal Surface(Vector3[] points)
        {
            Points = points;
        }
    }

    /// <summary>
    /// 픽셀 윤곽에 타일 앵커·개별 변환·높이 보정을 적용한 닫힌 충돌선 계산
    /// 맞닿은 타일과 다른 Tilemap의 겹친 영역까지 하나의 외곽선으로 병합
    /// </summary>
    internal static List<Surface> Analyze(PlatformPlacementSettings settings)
    {
        var offsets = new Dictionary<TileBase, float>();
        foreach (PlatformPlacementSettings.TileHeightOverride entry in settings.HeightOverrides)
        {
            if (entry.Tile == null || !IsFinite(entry.SurfaceOffset) ||
                !offsets.TryAdd(entry.Tile, entry.SurfaceOffset))
            {
                throw new InvalidOperationException("타일별 보정의 빈 타일·중복·높이 값을 확인하세요.");
            }
        }

        var spriteShapes = new Dictionary<Sprite, List<Vector2[]>>();
        var textures = new Dictionary<Texture2D, Texture2D>();
        var pixelCache = new Dictionary<Texture2D, Color32[]>();
        Scene preview = EditorSceneManager.NewPreviewScene();
        try
        {
            var root = new GameObject("PlatformGeometryPreview");
            SceneManager.MoveGameObjectToScene(root, preview);
            Rigidbody2D body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Static;
            CompositeCollider2D composite = root.AddComponent<CompositeCollider2D>();
            composite.geometryType = CompositeCollider2D.GeometryType.Outlines;
            composite.generationType = CompositeCollider2D.GenerationType.Manual;
            composite.vertexDistance = GeometryTolerance;
            composite.offsetDistance = 0f;
            PolygonCollider2D polygon = root.AddComponent<PolygonCollider2D>();
            polygon.compositeOperation = Collider2D.CompositeOperation.Merge;
            var paths = new List<Vector2[]>();
            long scanned = 0;

            foreach (Tilemap tilemap in settings.GetTilemaps())
            {
                BoundsInt bounds = tilemap.cellBounds;
                scanned += (long)bounds.size.x * bounds.size.y * bounds.size.z;
                if (scanned > settings.ScanCellLimit || bounds.size.z > 1)
                {
                    throw new InvalidOperationException("그룹 검사 셀 제한 초과 또는 여러 Z층 포함");
                }
                foreach (Vector3Int cell in bounds.allPositionsWithin)
                {
                    Sprite sprite = tilemap.GetSprite(cell);
                    if (sprite == null)
                    {
                        continue;
                    }
                    if (!spriteShapes.TryGetValue(sprite, out List<Vector2[]> shapes))
                    {
                        shapes = ExtractShapes(sprite, settings, textures, pixelCache);
                        spriteShapes.Add(sprite, shapes);
                    }

                    TileBase tile = tilemap.GetTile(cell);
                    float offset = offsets.TryGetValue(tile, out float customOffset)
                        ? customOffset : settings.SurfaceOffset;
                    Vector3 anchor = tilemap.CellToLocalInterpolated((Vector3)cell + tilemap.tileAnchor);
                    Matrix4x4 matrix = tilemap.transform.localToWorldMatrix *
                        Matrix4x4.Translate(anchor + Vector3.up * offset) *
                        tilemap.orientationMatrix * tilemap.GetTransformMatrix(cell);
                    foreach (Vector2[] shape in shapes)
                    {
                        var points = new Vector2[shape.Length];
                        for (int i = 0; i < shape.Length; i++)
                        {
                            points[i] = matrix.MultiplyPoint3x4(shape[i]);
                        }
                        paths.Add(points);
                    }
                }
            }

            if (paths.Count == 0)
            {
                return new List<Surface>();
            }
            polygon.pathCount = paths.Count;
            for (int i = 0; i < paths.Count; i++)
            {
                polygon.SetPath(i, paths[i]);
            }
            composite.GenerateGeometry();
            if (composite.pathCount > settings.PlatformLimit)
            {
                throw new InvalidOperationException("생성 발판 수 제한 초과. 이미지의 작은 장식이나 알파 기준 확인 필요");
            }

            var surfaces = new List<Surface>();
            for (int i = 0; i < composite.pathCount; i++)
            {
                var path = new Vector2[composite.GetPathPointCount(i)];
                composite.GetPath(i, path);
                var points = new Vector3[path.Length + 1];
                for (int j = 0; j < path.Length; j++)
                {
                    points[j] = path[j];
                }
                points[path.Length] = points[0];
                surfaces.Add(new Surface(points));
            }
            return surfaces;
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
            foreach (Texture2D texture in textures.Values)
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }

    /// <summary>
    /// 불투명 픽셀의 수평 구간을 세로로 병합한 직사각형 목록
    /// 원본 텍스처의 Read/Write와 Sprite Editor 설정 변경 없음
    /// </summary>
    private static List<Vector2[]> ExtractShapes(Sprite sprite, PlatformPlacementSettings settings,
        Dictionary<Texture2D, Texture2D> textures, Dictionary<Texture2D, Color32[]> pixelCache)
    {
        var shapes = new List<Vector2[]>();
        if (settings.UseSpritePhysicsShape)
        {
            for (int i = 0; i < sprite.GetPhysicsShapeCount(); i++)
            {
                var points = new List<Vector2>();
                sprite.GetPhysicsShape(i, points);
                shapes.Add(points.ToArray());
            }
            if (shapes.Count == 0)
            {
                throw new InvalidOperationException($"{sprite.name}: Sprite Editor의 Physics Shape 설정 필요");
            }
            return shapes;
        }

        if (sprite.packed)
        {
            throw new InvalidOperationException($"{sprite.name}: 패킹 전 원본 스프라이트로 분석 필요");
        }
        if (!textures.TryGetValue(sprite.texture, out Texture2D texture))
        {
            texture = CopyReadableTexture(sprite.texture);
            textures.Add(sprite.texture, texture);
            pixelCache.Add(sprite.texture, texture.GetPixels32());
        }
        Rect rect = sprite.rect;
        Color32[] pixels = pixelCache[sprite.texture];
        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        int originX = Mathf.RoundToInt(rect.x);
        int originY = Mathf.RoundToInt(rect.y);
        var rectangles = new List<RectInt>();
        var active = new Dictionary<Vector2Int, int>();
        byte threshold = (byte)Mathf.Clamp(Mathf.CeilToInt(settings.AlphaThreshold * 255f), 1, 255);
        for (int y = 0; y < height; y++)
        {
            var next = new Dictionary<Vector2Int, int>();
            int x = 0;
            while (x < width)
            {
                if (pixels[(originY + y) * texture.width + originX + x].a < threshold)
                {
                    x++;
                    continue;
                }
                int start = x++;
                while (x < width && pixels[(originY + y) * texture.width + originX + x].a >= threshold)
                {
                    x++;
                }
                var key = new Vector2Int(start, x);
                if (active.TryGetValue(key, out int index))
                {
                    RectInt rectangle = rectangles[index];
                    rectangle.height++;
                    rectangles[index] = rectangle;
                }
                else
                {
                    index = rectangles.Count;
                    rectangles.Add(new RectInt(start, y, x - start, 1));
                }
                next.Add(key, index);
            }
            active = next;
        }
        foreach (RectInt rectangle in rectangles)
        {
            Vector2 min = ((Vector2)rectangle.min - sprite.pivot) / sprite.pixelsPerUnit;
            Vector2 max = ((Vector2)rectangle.max - sprite.pivot) / sprite.pixelsPerUnit;
            shapes.Add(new[] { min, new Vector2(max.x, min.y), max, new Vector2(min.x, max.y) });
        }
        return shapes;
    }

    private static Texture2D CopyReadableTexture(Texture2D source)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0,
            RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        var copy = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        try
        {
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            copy.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            copy.Apply();
            return copy;
        }
        catch
        {
            UnityEngine.Object.DestroyImmediate(copy);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}