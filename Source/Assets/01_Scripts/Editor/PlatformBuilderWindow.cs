using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 타일 이미지 디버그·교체와 완성된 타일맵의 발판 콜라이더 생성 창
/// </summary>
public sealed class PlatformBuilderWindow : EditorWindow
{
    private enum ImageTarget
    {
        SpriteRenderer,
        Tilemap
    }

    private const string MenuPath = "Tools/Map/Platform Builder";
    private const float PreviewLineWidth = 4f;

    [SerializeField] private PlatformPlacementSettings _settings = new PlatformPlacementSettings();
    private PlatformPlacementBuilder.Plan _plan;
    private string _error = string.Empty;
    private Vector2 _scroll;

    [SerializeField] private ImageTarget _imageTarget;
    [SerializeField] private bool _showColliderBuilder;
    [SerializeField] private Transform _spriteRoot;
    [SerializeField] private SpriteRenderer _selectedRenderer;
    [SerializeField] private Sprite _sourceSprite;
    [SerializeField] private Sprite _replacementSprite;
    [SerializeField] private Color _spriteColor = Color.white;
    [SerializeField] private bool _includeInactiveSprites;
    [SerializeField, Min(1)] private int _spriteScanLimit = 10000;
    private readonly List<SpriteRenderer> _spriteMatches = new List<SpriteRenderer>();
    private int _spriteMatchIndex = -1;
    private string _spriteMessage = string.Empty;

    [SerializeField] private bool _showTileDebug = true;
    [SerializeField] private bool _pickTile;
    [SerializeField] private bool _searchGroup;
    [SerializeField] private TileBase _sourceTile;
    [SerializeField] private TileBase _replacementTile;
    [SerializeField] private Tilemap _selectedMap;
    [SerializeField] private Vector3Int _selectedCell;
    [SerializeField, Min(1)] private int _highlightLimit = 200;
    private readonly List<PlatformTileDebugger.Cell> _matches = new List<PlatformTileDebugger.Cell>();
    private int _matchIndex;
    private string _tileDebugMessage = string.Empty;

    [MenuItem(MenuPath)]
    private static void Open()
    {
        GetWindow<PlatformBuilderWindow>("플랫폼 빌더");
    }

    private void OnEnable()
    {
        minSize = new Vector2(400f, 480f);
        SceneView.duringSceneGui += OnSceneGUI;
        Selection.selectionChanged += HandleSelectionChanged;
        HandleSelectionChanged();
        Undo.undoRedoPerformed += HandleUndoRedo;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        RefreshPreview();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
        Selection.selectionChanged -= HandleSelectionChanged;
        Undo.undoRedoPerformed -= HandleUndoRedo;
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        _plan = null;
        SceneView.RepaintAll();
    }

    private void OnFocus()
    {
        HandleSelectionChanged();
        RefreshPreview();
    }

    private void OnGUI()
    {
        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawImageDebug();
        }
        EditorGUILayout.Space();
        _showColliderBuilder = EditorGUILayout.Foldout(_showColliderBuilder, "타일맵 발판 콜라이더 생성", true);
        if (!_showColliderBuilder)
        {
            EditorGUILayout.EndScrollView();
            return;
        }
        EditorGUILayout.LabelField("이미지 구성 후 콜라이더 생성", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "1. 기존 Tile Palette로 여러 이미지를 자유롭게 배치하세요.\n" +
            "2. Tilemap과 같은 충돌 그룹의 추가 Tilemap을 지정하세요.\n" +
            "3. 분석 후 콜라이더 생성/갱신 버튼을 누르세요.", MessageType.Info);

        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUI.BeginChangeCheck();
            _settings.Tilemap = (Tilemap)EditorGUILayout.ObjectField(
                "시각용 Tilemap", _settings.Tilemap, typeof(Tilemap), true);
            _settings.PhysicsParent = (Transform)EditorGUILayout.ObjectField(
                "발판 부모", _settings.PhysicsParent, typeof(Transform), true);
            DrawTilemapGroup();
            _settings.UseOneWay = EditorGUILayout.Toggle("단방향 발판", _settings.UseOneWay);
            _settings.UseSpritePhysicsShape = EditorGUILayout.Toggle("Sprite Physics Shape 사용", _settings.UseSpritePhysicsShape);
            if (!_settings.UseSpritePhysicsShape)
            {
                _settings.AlphaThreshold = EditorGUILayout.Slider("불투명 알파 기준", _settings.AlphaThreshold, 0.01f, 1f);
            }
            _settings.SurfaceOffset = EditorGUILayout.FloatField(
                new GUIContent("기본 높이 보정", "그림 윤곽 기준 Tilemap 로컬 Y 단위"), _settings.SurfaceOffset);
            _settings.SurfaceArc = EditorGUILayout.Slider("단방향 충돌 각도", _settings.SurfaceArc, 1f, 180f);
            _settings.ScanCellLimit = EditorGUILayout.IntField("검사 셀 수 제한", _settings.ScanCellLimit);
            _settings.PlatformLimit = EditorGUILayout.IntField("생성 발판 수 제한", _settings.PlatformLimit);
            DrawHeightOverrides();
            if (EditorGUI.EndChangeCheck())
            {
                _matches.Clear();
                RefreshPreview();
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("타일맵 분석 / 미리보기 갱신"))
            {
                RefreshPreview();
            }
            if (_error.Length > 0)
            {
                EditorGUILayout.HelpBox(_error, MessageType.Warning);
            }
            if (_plan != null)
            {
                EditorGUILayout.LabelField($"생성 구간 {_plan.Surfaces.Count}개 / 교체할 자동 발판 {_plan.Previous.Count}개");
                if (_plan.Surfaces.Count == 0 && _plan.Previous.Count > 0)
                {
                    EditorGUILayout.HelpBox("윗면이 없어 기존 자동 생성 발판을 정리합니다.", MessageType.Info);
                }
            }
            using (new EditorGUI.DisabledScope(_plan == null))
            {
                if (GUILayout.Button("발판 콜라이더 생성 / 갱신", GUILayout.Height(32f)))
                {
                    Rebuild();
                }
            }

            EditorGUILayout.HelpBox(
                "그림의 불투명 윤곽과 타일 변환을 반영하고 그룹의 접한 경계를 합칩니다.\n" +
                "타일 편집 후 미리보기를 갱신하세요. 생성 버튼은 항상 다시 분석합니다.\n" +
                "재생성 시 선택한 그룹의 자동 발판만 교체합니다. 장식은 그룹에서 제외하세요.\n" +
                "기존 수동 발판은 보존됩니다. 같은 위치의 수동 충돌체는 비활성화하세요.\n" +
                "Ctrl+Z로 콜라이더 갱신을 되돌릴 수 있으며 씬은 직접 저장해야 합니다.",
                MessageType.None);
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTilemapGroup()
    {
        EditorGUILayout.LabelField("같은 충돌 그룹의 추가 Tilemap", EditorStyles.boldLabel);
        for (int i = 0; i < _settings.GroupTilemaps.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            _settings.GroupTilemaps[i] = (Tilemap)EditorGUILayout.ObjectField(
                _settings.GroupTilemaps[i], typeof(Tilemap), true);
            bool remove = GUILayout.Button("삭제", GUILayout.Width(45f));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                _settings.GroupTilemaps.RemoveAt(i);
                GUI.changed = true;
                break;
            }
        }
        if (GUILayout.Button("그룹 Tilemap 추가"))
        {
            _settings.GroupTilemaps.Add(null);
            GUI.changed = true;
        }
    }

    private void DrawHeightOverrides()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("타일별 높이 보정", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("기본값에 더하지 않고 해당 타일의 보정값으로 사용",
            EditorStyles.wordWrappedMiniLabel);
        for (int i = 0; i < _settings.HeightOverrides.Count; i++)
        {
            PlatformPlacementSettings.TileHeightOverride entry = _settings.HeightOverrides[i];
            EditorGUILayout.BeginHorizontal();
            entry.Tile = (TileBase)EditorGUILayout.ObjectField(entry.Tile, typeof(TileBase), false);
            entry.SurfaceOffset = EditorGUILayout.FloatField(entry.SurfaceOffset, GUILayout.Width(70f));
            bool remove = GUILayout.Button("삭제", GUILayout.Width(45f));
            EditorGUILayout.EndHorizontal();
            if (remove)
            {
                _settings.HeightOverrides.RemoveAt(i);
                GUI.changed = true;
                break;
            }
        }
        if (GUILayout.Button("타일별 보정 추가"))
        {
            _settings.HeightOverrides.Add(new PlatformPlacementSettings.TileHeightOverride());
            GUI.changed = true;
        }
    }

    /// <summary>
    /// 타일·씬 데이터를 변경하지 않는 충돌선 분석 미리보기
    /// </summary>
    private void RefreshPreview()
    {
        _plan = null;
        _error = string.Empty;
        try
        {
            _plan = PlatformPlacementBuilder.BuildPlan(_settings);
        }
        catch (InvalidOperationException exception)
        {
            _error = exception.Message;
        }
        Repaint();
        SceneView.RepaintAll();
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        HandleTilePicking();
        DrawTileHighlights();
        if (Event.current.type != EventType.Repaint || _plan == null ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            _settings.Tilemap == null || _settings.PhysicsParent == null)
        {
            return;
        }
        using (new Handles.DrawingScope(Color.green))
        {
            foreach (PlatformSurfaceAnalyzer.Surface surface in _plan.Surfaces)
            {
                Handles.DrawAAPolyLine(PreviewLineWidth, surface.Points);
            }
        }
    }

    /// <summary>
    /// 충돌 생성 설정과 독립적인 타일 이미지 확인·교체 UI
    /// </summary>
    private void DrawTileDebug()
    {
        EditorGUI.BeginChangeCheck();
        _settings.Tilemap = (Tilemap)EditorGUILayout.ObjectField("대상 Tilemap", _settings.Tilemap, typeof(Tilemap), true);
        if (EditorGUI.EndChangeCheck()) { _matches.Clear(); RefreshPreview(); }
        EditorGUILayout.HelpBox("Scene 선택 모드에서 대표 Tilemap의 셀을 클릭하세요. Alt 이동과 우클릭은 유지됩니다.\n" +
            "교체는 기존 타일 에셋 참조만 바꿉니다. Physics Parent 없이도 사용할 수 있습니다.", MessageType.Info);
        _pickTile = EditorGUILayout.Toggle("Scene에서 타일 선택", _pickTile);
        _selectedCell = EditorGUILayout.Vector3IntField("선택 셀 / 클릭할 Z층", _selectedCell);
        if (GUILayout.Button("대표 Tilemap의 해당 셀 확인"))
        {
            SelectDebugCell(_settings.Tilemap, _selectedCell);
        }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("선택 Tilemap", _selectedMap, typeof(Tilemap), true);
            Sprite sprite = _selectedMap != null ? _selectedMap.GetSprite(_selectedCell) : null;
            EditorGUILayout.ObjectField("현재 표시 Sprite", sprite, typeof(Sprite), false);
            EditorGUILayout.TextField("Sprite 원본 경로", AssetDatabase.GetAssetPath(sprite));
        }
        EditorGUI.BeginChangeCheck();
        _sourceTile = (TileBase)EditorGUILayout.ObjectField("찾을 타일", _sourceTile, typeof(TileBase), false);
        _searchGroup = EditorGUILayout.Toggle("추가 그룹까지 검색·교체", _searchGroup);
        _highlightLimit = Mathf.Max(1, EditorGUILayout.IntField("최대 강조 셀 수", _highlightLimit));
        if (EditorGUI.EndChangeCheck()) { _matches.Clear(); SceneView.RepaintAll(); }
        EditorGUILayout.TextField("타일 에셋 경로", AssetDatabase.GetAssetPath(_sourceTile));
        _replacementTile = (TileBase)EditorGUILayout.ObjectField("교체할 타일", _replacementTile, typeof(TileBase), false);
        if (GUILayout.Button("일치 타일 검색 / 위치 강조")) { FindDebugTiles(); }
        EditorGUILayout.LabelField($"검색 결과 {_matches.Count}개 / 최대 {_highlightLimit}개 강조");
        using (new EditorGUI.DisabledScope(_matches.Count == 0))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("이전 위치")) { FocusDebugMatch(-1); }
            if (GUILayout.Button("다음 위치")) { FocusDebugMatch(1); }
            EditorGUILayout.EndHorizontal();
        }
        using (new EditorGUI.DisabledScope(_sourceTile == null))
        {
            if (GUILayout.Button("원본 타일 에셋 찾기")) { EditorGUIUtility.PingObject(_sourceTile); }
        }
        using (new EditorGUI.DisabledScope(_sourceTile == null || _replacementTile == null || _sourceTile == _replacementTile))
        {
            if (GUILayout.Button("선택 셀만 교체")) { ReplaceDebugTiles(false); }
            if (GUILayout.Button("검색 범위의 같은 타일 전체 교체")) { ReplaceDebugTiles(true); }
        }
        if (_tileDebugMessage.Length > 0)
        {
            EditorGUILayout.HelpBox(_tileDebugMessage, MessageType.Info);
        }
        EditorGUILayout.HelpBox("전체 교체는 실행 직전에 범위를 다시 검색합니다. 같은 Sprite를 쓰는 다른 타일 에셋은 포함하지 않습니다.\n" +
            "교체 후 콜라이더 생성/갱신과 씬 저장은 직접 실행하세요. Ctrl+Z로 교체를 되돌릴 수 있습니다.\n" +
            "RuleTile 등은 교체 타일의 규칙에 따라 표시 이미지와 변환이 달라질 수 있습니다.", MessageType.None);
    }

    /// <summary>
    /// 이미지 저장 방식에 따른 선택·검색·교체 UI 분기
    /// </summary>
    private void DrawImageDebug()
    {
        _showTileDebug = EditorGUILayout.Foldout(_showTileDebug, "이미지 디버그 / 교체", true);
        if (!_showTileDebug) { return; }
        EditorGUI.BeginChangeCheck();
        _imageTarget = (ImageTarget)EditorGUILayout.EnumPopup("이미지 대상", _imageTarget);
        if (EditorGUI.EndChangeCheck())
        {
            _matches.Clear();
            _spriteMatches.Clear();
            HandleSelectionChanged();
            SceneView.RepaintAll();
        }
        if (_imageTarget == ImageTarget.SpriteRenderer) { DrawSpriteDebug(); }
        else { DrawTileDebug(); }
    }

    private void DrawSpriteDebug()
    {
        EditorGUILayout.HelpBox("Scene 또는 Hierarchy에서 이미지 오브젝트를 선택하세요. 기본 Unity 선택 기능을 사용합니다.\n" +
            "검색 부모 아래의 같은 Sprite만 교체하며 Tilemap·발판 부모 설정은 필요하지 않습니다.", MessageType.Info);
        if (GUILayout.Button("선택 오브젝트에서 이미지 가져오기"))
        {
            SelectDebugSprite(Selection.activeGameObject);
        }
        EditorGUI.BeginChangeCheck();
        _spriteRoot = (Transform)EditorGUILayout.ObjectField("검색 부모", _spriteRoot, typeof(Transform), true);
        _sourceSprite = (Sprite)EditorGUILayout.ObjectField("찾을 Sprite", _sourceSprite, typeof(Sprite), false);
        _includeInactiveSprites = EditorGUILayout.Toggle("비활성·숨긴 렌더러 포함", _includeInactiveSprites);
        _spriteScanLimit = Mathf.Max(1, EditorGUILayout.IntField("검사 렌더러 수 제한", _spriteScanLimit));
        _highlightLimit = Mathf.Max(1, EditorGUILayout.IntField("최대 강조 수", _highlightLimit));
        if (EditorGUI.EndChangeCheck()) { _spriteMatches.Clear(); SceneView.RepaintAll(); }
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("선택 Renderer", _selectedRenderer, typeof(SpriteRenderer), true);
        }
        EditorGUILayout.LabelField("Sprite 원본 경로", EditorStyles.boldLabel);
        EditorGUILayout.SelectableLabel(AssetDatabase.GetAssetPath(_sourceSprite), EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        _replacementSprite = (Sprite)EditorGUILayout.ObjectField("교체할 Sprite", _replacementSprite, typeof(Sprite), false);
        using (new EditorGUI.DisabledScope(_spriteRoot == null || _sourceSprite == null))
        {
            if (GUILayout.Button("같은 Sprite 검색 / 위치 강조")) { FindDebugSprites(); }
        }
        EditorGUILayout.LabelField($"검색 결과 {_spriteMatches.Count}개 / 최대 {_highlightLimit}개 강조");
        using (new EditorGUI.DisabledScope(_spriteMatches.Count == 0))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("이전 이미지")) { FocusDebugSprite(-1); }
            if (GUILayout.Button("다음 이미지")) { FocusDebugSprite(1); }
            EditorGUILayout.EndHorizontal();
        }
        using (new EditorGUI.DisabledScope(_sourceSprite == null))
        {
            if (GUILayout.Button("원본 Sprite 에셋 찾기")) { EditorGUIUtility.PingObject(_sourceSprite); }
        }
        using (new EditorGUI.DisabledScope(_spriteRoot == null || _sourceSprite == null || _replacementSprite == null || _sourceSprite == _replacementSprite))
        {
            using (new EditorGUI.DisabledScope(_selectedRenderer == null))
            {
                if (GUILayout.Button("선택 이미지 하나만 교체")) { ReplaceDebugSprites(false); }
            }
            if (GUILayout.Button("검색 부모의 같은 Sprite 전체 교체")) { ReplaceDebugSprites(true); }
        }
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("색상과 투명도", EditorStyles.boldLabel);
        _spriteColor = EditorGUILayout.ColorField(new GUIContent("적용할 색상 (RGBA)"), _spriteColor, true, true, false);
        using (new EditorGUI.DisabledScope(_spriteRoot == null || _sourceSprite == null))
        {
            using (new EditorGUI.DisabledScope(_selectedRenderer == null))
            {
                if (GUILayout.Button("선택 이미지 하나에 색상 적용")) { ApplyDebugSpriteColor(false); }
            }
            if (GUILayout.Button("검색 부모의 같은 Sprite 전체에 색상 적용")) { ApplyDebugSpriteColor(true); }
        }
        EditorGUILayout.LabelField("색상은 SpriteRenderer에 곱해지며 A는 투명도입니다. 이미지 교체와 별도로 적용됩니다.", EditorStyles.wordWrappedMiniLabel);
        if (_spriteMessage.Length > 0) { EditorGUILayout.HelpBox(_spriteMessage, MessageType.Info); }
        EditorGUILayout.HelpBox("처음 이미지를 가져오면 그 이미지의 바로 위 부모가 검색 범위가 됩니다. 필요하면 부모를 직접 바꾸세요.\n" +
            "전체 교체 직전에 다시 검색합니다. 위치·스케일·Renderer 크기·기존 콜라이더는 유지되며 씬은 직접 저장하세요.\n" +
            "새 Sprite의 원본 크기나 피벗이 다르면 보이는 크기와 위치는 달라질 수 있습니다. Ctrl+Z로 교체를 되돌릴 수 있습니다.", MessageType.None);
    }

    private void HandleSelectionChanged()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !_showTileDebug || _imageTarget != ImageTarget.SpriteRenderer) { return; }
        GameObject selected = Selection.activeGameObject;
        if (selected != null && selected.TryGetComponent(out SpriteRenderer renderer) &&
            (_selectedRenderer != renderer || _sourceSprite != renderer.sprite))
        {
            SelectDebugSprite(selected);
        }
    }

    private void SelectDebugSprite(GameObject selected)
    {
        if (selected == null || EditorUtility.IsPersistent(selected) || !selected.TryGetComponent(out SpriteRenderer renderer))
        {
            _spriteMessage = "Scene 또는 Hierarchy에서 SpriteRenderer가 있는 이미지 오브젝트를 선택하세요.";
            return;
        }
        _selectedRenderer = renderer;
        _spriteColor = renderer.color;
        _sourceSprite = renderer.sprite;
        if (_spriteRoot == null) { _spriteRoot = renderer.transform.parent != null ? renderer.transform.parent : renderer.transform; }
        _spriteMatches.Clear();
        _spriteMatchIndex = -1;
        _spriteMessage = renderer.sprite != null ? $"{renderer.name}: {renderer.sprite.name}" : $"{renderer.name}: Sprite 미지정";
        Repaint();
        SceneView.RepaintAll();
    }

    private void FindDebugSprites()
    {
        _spriteMatches.Clear();
        try
        {
            _spriteMatches.AddRange(PlatformTileDebugger.FindSprites(_spriteRoot, _sourceSprite, _includeInactiveSprites, _spriteScanLimit));
            _spriteMatchIndex = -1;
            _spriteMessage = $"같은 Sprite {_spriteMatches.Count}개 발견. 전체 교체는 검색 부모 아래에만 적용됩니다.";
        }
        catch (InvalidOperationException exception) { _spriteMessage = exception.Message; }
        SceneView.RepaintAll();
    }

    private void FocusDebugSprite(int direction)
    {
        _spriteMatchIndex = _spriteMatchIndex < 0
            ? (direction > 0 ? 0 : _spriteMatches.Count - 1)
            : (_spriteMatchIndex + direction + _spriteMatches.Count) % _spriteMatches.Count;
        SpriteRenderer renderer = _spriteMatches[_spriteMatchIndex];
        if (renderer == null || renderer.sprite != _sourceSprite || _spriteRoot == null || !renderer.transform.IsChildOf(_spriteRoot))
        {
            _spriteMatches.Clear();
            _spriteMessage = "이미지 구성이 바뀌었습니다. 다시 검색하세요.";
            return;
        }
        _selectedRenderer = renderer;
        _spriteColor = renderer.color;
        if (SceneView.lastActiveSceneView != null) { SceneView.lastActiveSceneView.Frame(renderer.bounds, false); }
        EditorGUIUtility.PingObject(renderer.gameObject);
        _spriteMessage = $"검색 결과 {_spriteMatchIndex + 1}/{_spriteMatches.Count}: {renderer.name}";
        SceneView.RepaintAll();
    }

    private void ReplaceDebugSprites(bool replaceAll)
    {
        try
        {
            List<SpriteRenderer> renderers = replaceAll
                ? PlatformTileDebugger.FindSprites(_spriteRoot, _sourceSprite, _includeInactiveSprites, _spriteScanLimit)
                : new List<SpriteRenderer> { _selectedRenderer };
            int count = PlatformTileDebugger.ReplaceSprites(_spriteRoot, renderers, _sourceSprite, _replacementSprite);
            _spriteMatches.Clear();
            _spriteMessage = $"이미지 {count}개 교체 완료. 기존 콜라이더 유지, 씬 저장 필요.";
        }
        catch (InvalidOperationException exception) { _spriteMessage = exception.Message; }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _spriteMessage = $"교체 실패: {exception.Message}";
        }
        SceneView.RepaintAll();
    }

    /// <summary>
    /// 현재 검색 범위의 선택 이미지 또는 같은 Sprite에 색상 적용
    /// </summary>
    private void ApplyDebugSpriteColor(bool applyAll)
    {
        try
        {
            List<SpriteRenderer> renderers = applyAll
                ? PlatformTileDebugger.FindSprites(_spriteRoot, _sourceSprite, _includeInactiveSprites, _spriteScanLimit)
                : new List<SpriteRenderer> { _selectedRenderer };
            int count = PlatformTileDebugger.ApplySpriteColor(_spriteRoot, renderers, _sourceSprite, _spriteColor);
            _spriteMessage = $"이미지 {count}개 색상 변경 완료. 씬 저장 필요.";
        }
        catch (InvalidOperationException exception) { _spriteMessage = exception.Message; }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _spriteMessage = $"색상 변경 실패: {exception.Message}";
        }
        SceneView.RepaintAll();
    }

    private List<Tilemap> GetDebugMaps()
    {
        List<Tilemap> maps = _searchGroup ? _settings.GetTilemaps() : new List<Tilemap> { _settings.Tilemap };
        foreach (Tilemap map in maps)
        {
            PlatformTileDebugger.ValidateMap(map);
            if (map.gameObject.scene != _settings.Tilemap.gameObject.scene)
            {
                throw new InvalidOperationException("같은 씬의 Tilemap만 검색 그룹에 포함하세요.");
            }
        }
        return maps;
    }

    private void SelectDebugCell(Tilemap map, Vector3Int cell)
    {
        try
        {
            PlatformTileDebugger.ValidateMap(map);
            _selectedMap = map;
            _selectedCell = cell;
            _sourceTile = map.GetTile(cell);
            _matches.Clear();
            _tileDebugMessage = _sourceTile != null ? $"{map.name} {cell}: {_sourceTile.name}" : $"{map.name} {cell}: 빈 셀";
        }
        catch (InvalidOperationException exception) { _tileDebugMessage = exception.Message; }
        Repaint();
        SceneView.RepaintAll();
    }

    private void FindDebugTiles()
    {
        _matches.Clear();
        try
        {
            _matches.AddRange(PlatformTileDebugger.FindMatches(GetDebugMaps(), _sourceTile, _settings.ScanCellLimit));
            _matchIndex = -1;
            _tileDebugMessage = $"같은 타일 {_matches.Count}개 발견. 전체 교체는 이 검색 범위에만 적용됩니다.";
        }
        catch (InvalidOperationException exception) { _tileDebugMessage = exception.Message; }
        SceneView.RepaintAll();
    }

    private void FocusDebugMatch(int direction)
    {
        _matchIndex = _matchIndex < 0
            ? (direction > 0 ? 0 : _matches.Count - 1)
            : (_matchIndex + direction + _matches.Count) % _matches.Count;
        PlatformTileDebugger.Cell cell = _matches[_matchIndex];
        if (cell.Map == null || cell.Map.GetTile(cell.Position) != _sourceTile)
        {
            _matches.Clear();
            _tileDebugMessage = "타일 구성이 바뀌었습니다. 다시 검색하세요.";
            return;
        }
        _selectedMap = cell.Map;
        _selectedCell = cell.Position;
        Vector3[] corners = PlatformTileDebugger.GetCellCorners(cell);
        var bounds = new Bounds(corners[0], Vector3.zero);
        foreach (Vector3 corner in corners) { bounds.Encapsulate(corner); }
        if (SceneView.lastActiveSceneView != null) { SceneView.lastActiveSceneView.Frame(bounds, false); }
        _tileDebugMessage = $"검색 결과 {_matchIndex + 1}/{_matches.Count}: {cell.Map.name} {cell.Position}";
        SceneView.RepaintAll();
    }

    private void ReplaceDebugTiles(bool replaceAll)
    {
        try
        {
            List<Tilemap> maps = GetDebugMaps();
            if (!replaceAll && !maps.Contains(_selectedMap))
            {
                throw new InvalidOperationException("현재 검색 범위의 셀을 다시 선택하세요.");
            }
            List<PlatformTileDebugger.Cell> cells = replaceAll
                ? PlatformTileDebugger.FindMatches(maps, _sourceTile, _settings.ScanCellLimit)
                : new List<PlatformTileDebugger.Cell> { new PlatformTileDebugger.Cell(_selectedMap, _selectedCell) };
            int count = PlatformTileDebugger.Replace(cells, _sourceTile, _replacementTile);
            _matches.Clear();
            _tileDebugMessage = $"타일 {count}개 교체 완료. 필요한 콜라이더 갱신과 씬 저장을 실행하세요.";
            RefreshPreview();
        }
        catch (InvalidOperationException exception) { _tileDebugMessage = exception.Message; }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            _tileDebugMessage = $"교체 실패: {exception.Message}";
        }
    }

    private void HandleTilePicking()
    {
        if (_imageTarget != ImageTarget.Tilemap || !_showTileDebug || !_pickTile || EditorApplication.isPlayingOrWillChangePlaymode || _settings.Tilemap == null)
        {
            return;
        }
        Event current = Event.current;
        if (current.alt || Tools.viewToolActive) { return; }
        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        if (current.type == EventType.Layout) { HandleUtility.AddDefaultControl(controlId); }
        if (current.type != EventType.MouseDown || current.button != 0 || HandleUtility.nearestControl != controlId) { return; }
        Tilemap map = _settings.Tilemap;
        Ray ray = HandleUtility.GUIPointToWorldRay(current.mousePosition);
        var plane = new Plane(map.transform.forward, map.CellToWorld(new Vector3Int(0, 0, _selectedCell.z)));
        if (!plane.Raycast(ray, out float distance)) { return; }
        Vector3Int cell = map.WorldToCell(ray.GetPoint(distance));
        cell.z = _selectedCell.z;
        SelectDebugCell(map, cell);
        current.Use();
    }

    private void DrawTileHighlights()
    {
        if (!_showTileDebug || Event.current.type != EventType.Repaint || EditorApplication.isPlayingOrWillChangePlaymode) { return; }
        if (_imageTarget == ImageTarget.SpriteRenderer)
        {
            for (int i = 0; i < Mathf.Min(_spriteMatches.Count, _highlightLimit); i++)
            {
                SpriteRenderer renderer = _spriteMatches[i];
                if (renderer != null && renderer.sprite == _sourceSprite)
                {
                    using (new Handles.DrawingScope(Color.yellow)) { Handles.DrawWireCube(renderer.bounds.center, renderer.bounds.size); }
                }
            }
            if (_selectedRenderer != null)
            {
                using (new Handles.DrawingScope(Color.cyan)) { Handles.DrawWireCube(_selectedRenderer.bounds.center, _selectedRenderer.bounds.size); }
            }
            return;
        }
        using (new Handles.DrawingScope(Color.yellow))
        {
            for (int i = 0; i < Mathf.Min(_matches.Count, _highlightLimit); i++)
            {
                PlatformTileDebugger.Cell cell = _matches[i];
                if (cell.Map != null && cell.Map.GetTile(cell.Position) == _sourceTile)
                {
                    Handles.DrawSolidRectangleWithOutline(PlatformTileDebugger.GetCellCorners(cell), Color.clear, Color.yellow);
                }
            }
        }
        if (_selectedMap != null)
        {
            var cell = new PlatformTileDebugger.Cell(_selectedMap, _selectedCell);
            Handles.DrawSolidRectangleWithOutline(PlatformTileDebugger.GetCellCorners(cell), Color.clear, Color.cyan);
        }
    }

    private void HandleUndoRedo()
    {
        _matches.Clear();
        _spriteMatches.Clear();
        _spriteMessage = "Undo/Redo 반영 완료. 현재 이미지로 다시 검색하세요.";
        _tileDebugMessage = "Undo/Redo 반영 완료. 필요한 경우 타일 검색과 콜라이더 미리보기를 갱신하세요.";
        RefreshPreview();
    }

    private void Rebuild()
    {
        try
        {
            int count = PlatformPlacementBuilder.Rebuild(_settings);
            ShowNotification(new GUIContent($"발판 {count}개 갱신 완료 · 씬 저장 필요"));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            ShowNotification(new GUIContent($"생성 실패: {exception.Message}"));
        }
        RefreshPreview();
    }

    private void OnPlayModeChanged(PlayModeStateChange state)
    {
        _plan = null;
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            RefreshPreview();
        }
        else
        {
            Repaint();
            SceneView.RepaintAll();
        }
    }
}
