using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

/// <summary>스탯 창에서 현재 캐릭터의 착용 외형 표시</summary>
[DisallowMultipleComponent]
public sealed class EquipmentPreview : MonoBehaviour
{
    private const string PreviewLayerName = "EquipmentPreview";
    private const int TextureWidth = 320;
    private const int TextureHeight = 288;
    private const float CameraDistance = 10f;
    private const float FramingPadding = 1.08f;
    private const float BorderWidth = 1f;
    private static readonly Vector3 StagePosition = new(0f, -4096f, 0f);

    [SerializeField] private UnityEngine.UI.RawImage _image;
    [SerializeField] private Character4D _modelPrefab;
    [SerializeField] private Shader _previewShader;

    private Player _owner;
    private PlayerVisual _source;
    private GameObject _stage;
    private Character4D _model;
    private Camera _camera;
    private RenderTexture _texture;
    private Material _material;
    private SpriteRenderer[] _renderers;
    private GameObject _border;
    private bool _windowVisible;

    /// <summary>미리보기 표시 참조 확인과 초기 숨김</summary>
    internal void Initialize()
    {
        if (_image == null || _modelPrefab == null || _previewShader == null)
        {
            throw new MissingReferenceException($"{name}: 미리보기 이미지·외형 프리팹·셰이더 참조 필요");
        }

        _image.raycastTarget = false;
        if (_border == null) { CreateBorder(); }
        SetWindowVisible(false);
    }

    /// <summary>현재 로컬 캐릭터의 외형과 장비 변경 연결</summary>
    internal void Bind(Player owner, PlayerVisual source)
    {
        DisconnectOwner();
        _owner = owner;
        _source = source;
        _owner.EquipmentChanged += HandleEquipmentChanged;
        SetWindowVisible(_windowVisible);
    }

    /// <summary>현재 미리보기 소유자의 갱신 연결 해제</summary>
    internal void Unbind(Player owner)
    {
        if (_owner != owner) { return; }
        DisconnectOwner();
        SetWindowVisible(_windowVisible);
    }

    /// <summary>스탯 창 표시 중에만 외형과 카메라 활성화</summary>
    internal void SetWindowVisible(bool visible)
    {
        _windowVisible = visible;
        if (!visible || _source == null)
        {
            _image.enabled = false;
            _border.SetActive(false);
            if (_stage != null) { _stage.SetActive(false); }
            return;
        }

        if (_stage == null) { CreateStage(); }
        _stage.SetActive(true);
        RefreshAppearance();
        _image.enabled = true;
        _border.SetActive(true);
    }

    private void CreateBorder()
    {
        _border = new GameObject("PreviewBorder", typeof(RectTransform));
        _border.layer = _image.gameObject.layer;
        var rect = (RectTransform)_border.transform;
        rect.SetParent(_image.rectTransform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        AddBorderEdge(rect, "Top", Vector2.up, Vector2.one, Vector2.zero, new Vector2(0f, BorderWidth));
        AddBorderEdge(rect, "Bottom", Vector2.zero, Vector2.right, new Vector2(0f, -BorderWidth), Vector2.zero);
        AddBorderEdge(rect, "Left", Vector2.zero, Vector2.up, new Vector2(-BorderWidth, -BorderWidth), new Vector2(0f, BorderWidth));
        AddBorderEdge(rect, "Right", Vector2.right, Vector2.one, new Vector2(0f, -BorderWidth), new Vector2(BorderWidth, BorderWidth));
    }

    private static void AddBorderEdge(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        var edge = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        edge.layer = parent.gameObject.layer;
        var rect = (RectTransform)edge.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        var image = edge.GetComponent<UnityEngine.UI.Image>();
        image.color = Color.white;
        image.raycastTarget = false;
    }

    private void CreateStage()
    {
        int layer = LayerMask.NameToLayer(PreviewLayerName);
        if (layer < 0)
        {
            throw new MissingReferenceException($"{name}: {PreviewLayerName} 레이어 필요");
        }

        _stage = new GameObject("EquipmentPreviewStage");
        _stage.transform.SetParent(transform.root, false);
        _stage.transform.SetPositionAndRotation(StagePosition, Quaternion.identity);
        _stage.SetActive(false);
        _model = Instantiate(_modelPrefab, _stage.transform, false);
        _model.transform.localPosition = Vector3.zero;
        _model.transform.localRotation = Quaternion.identity;
        _model.transform.localScale = Vector3.one;
        foreach (Transform part in _stage.GetComponentsInChildren<Transform>(true))
        {
            part.gameObject.layer = layer;
        }
        foreach (Collider2D collider in _model.GetComponentsInChildren<Collider2D>(true))
        {
            collider.enabled = false;
            Destroy(collider);
        }

        _material = new Material(_previewShader);
        _renderers = _model.GetComponentsInChildren<SpriteRenderer>(true);
        _model.Initialize();
        _model.Animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        _texture = new RenderTexture(TextureWidth, TextureHeight, 16, RenderTextureFormat.ARGB32)
        {
            name = "EquipmentPreviewTexture",
            filterMode = FilterMode.Bilinear
        };
        _image.texture = _texture;
        var cameraObject = new GameObject("EquipmentPreviewCamera");
        cameraObject.transform.SetParent(_stage.transform, false);
        _camera = cameraObject.AddComponent<Camera>();
        _camera.orthographic = true;
        _camera.aspect = (float)TextureWidth / TextureHeight;
        _camera.clearFlags = CameraClearFlags.SolidColor;
        _camera.backgroundColor = Color.black;
        _camera.cullingMask = 1 << layer;
        _camera.allowHDR = false;
        _camera.allowMSAA = false;
        _camera.nearClipPlane = 0.1f;
        _camera.farClipPlane = CameraDistance * 2f;
        _camera.targetTexture = _texture;
    }

    private void RefreshAppearance()
    {
        _source.CopyAppearanceTo(_model);
        // HeroEditor 초기화가 복원한 기본 재질을 미리보기용 재질로 교체
        foreach (SpriteRenderer renderer in _renderers)
        {
            renderer.sharedMaterial = _material;
        }
        _model.SetDirection(Vector2.right);
        foreach (GameObject shadow in _model.Shadows)
        {
            shadow.SetActive(false);
        }
        _model.AnimationManager.SetState(CharacterState.Idle);
        FitCamera();
    }

    private void FitCamera()
    {
        var bounds = new Bounds(_model.transform.position, Vector3.zero);
        bool hasVisibleSprite = false;
        foreach (SpriteRenderer renderer in _renderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null)
            {
                continue;
            }

            if (hasVisibleSprite) { bounds.Encapsulate(renderer.bounds); }
            else { bounds = renderer.bounds; }
            hasVisibleSprite = true;
        }

        Vector3 center = bounds.center;
        float bodyMinX = float.PositiveInfinity;
        float bodyMaxX = float.NegativeInfinity;
        foreach (SpriteRenderer renderer in _model.Active.BodyRenderers)
        {
            if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || renderer.sprite == null) { continue; }
            Bounds body = renderer.bounds;
            bodyMinX = Mathf.Min(bodyMinX, body.min.x);
            bodyMaxX = Mathf.Max(bodyMaxX, body.max.x);
        }

        if (bodyMinX <= bodyMaxX)
        {
            center.x = (bodyMinX + bodyMaxX) * 0.5f;
        }

        // 몸체를 중앙에 두고 양쪽 장비 끝까지 포함하는 카메라 너비
        float halfWidth = Mathf.Max(center.x - bounds.min.x, bounds.max.x - center.x);
        _camera.transform.position = center - Vector3.forward * CameraDistance;
        _camera.orthographicSize = Mathf.Max(0.5f, Mathf.Max(bounds.extents.y, halfWidth / _camera.aspect) * FramingPadding);
    }

    private void HandleEquipmentChanged(EquipSlot slot, InventoryItemData item)
    {
        if (_windowVisible && _stage != null && _stage.activeSelf)
        {
            RefreshAppearance();
        }
    }

    private void DisconnectOwner()
    {
        if (_owner != null) { _owner.EquipmentChanged -= HandleEquipmentChanged; }
        _owner = null;
        _source = null;
    }

    private void OnDisable()
    {
        if (_border != null) { SetWindowVisible(false); }
    }

    private void OnDestroy()
    {
        DisconnectOwner();
        if (_camera != null) { _camera.targetTexture = null; }
        if (_stage != null) { Destroy(_stage); }
        if (_texture != null)
        {
            _texture.Release();
            Destroy(_texture);
        }
        if (_material != null) { Destroy(_material); }
    }
}
