using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>인벤토리와 장비 슬롯이 공유하는 읽기 전용 아이템 툴팁</summary>
internal sealed class ItemTooltip
{
    private const float Width = 300f;
    private const float Padding = 12f;
    private const float Gap = 5f;
    private static readonly Vector2 PointerOffset = new(16f, -16f);
    private static readonly Color UnmetLevelColor = new(1f, 0.34f, 0.34f);
    private static readonly (StatType Type, string Label)[] StatLabels =
    {
        (StatType.MaxHp, "최대 HP"), (StatType.MaxMp, "최대 MP"), (StatType.Atk, "공격력"), (StatType.Def, "방어력"),
        (StatType.Str, "힘"), (StatType.Dex, "민첩"), (StatType.Int, "지능")
    };

    private readonly Canvas _canvas;
    private readonly RectTransform _layer;
    private readonly RectTransform _panel;
    private readonly UnityEngine.UI.Image _icon;
    private readonly TextMeshProUGUI _title;
    private readonly TextMeshProUGUI _requirement;
    private readonly TextMeshProUGUI _body;
    private PlayerStat _stat;
    private PlayerEquipment _equipment;
    private MonoBehaviour _owner;

    internal ItemTooltip(Canvas canvas, TMP_FontAsset font)
    {
        _canvas = canvas;
        _layer = CreateRect("ItemTooltipLayer", canvas.transform);
        _layer.anchorMin = Vector2.zero;
        _layer.anchorMax = Vector2.one;
        _layer.offsetMin = _layer.offsetMax = Vector2.zero;
        Canvas overlay = _layer.gameObject.AddComponent<Canvas>();
        overlay.overrideSorting = true;
        overlay.sortingOrder = Constants.ItemTooltipSortingOrder;
        _panel = CreateRect("ItemTooltip", _layer);
        _panel.pivot = new Vector2(0f, 1f);
        _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
        AddImage(_panel, new Color(0.08f, 0.09f, 0.10f, 0.85f));
        AddBorder("Top", new Vector2(0f, 1f), Vector2.one, new Vector2(0f, -1f), Vector2.zero);
        AddBorder("Bottom", Vector2.zero, Vector2.right, Vector2.zero, new Vector2(0f, 1f));
        AddBorder("Left", Vector2.zero, Vector2.up, Vector2.zero, new Vector2(1f, 0f));
        AddBorder("Right", Vector2.right, Vector2.one, new Vector2(-1f, 0f), Vector2.zero);
        _icon = AddImage(CreateRect("Icon", _panel), Color.white);
        _icon.preserveAspect = true;
        _title = CreateText("Title", font, 19f);
        _title.fontStyle = FontStyles.Bold;
        _requirement = CreateText("RequiredLevel", font, 16f);
        _body = CreateText("Details", font, 16f);
        _panel.gameObject.SetActive(false);
    }

    internal void Bind(PlayerStat stat, PlayerEquipment equipment)
    {
        Hide();
        _stat = stat;
        _equipment = equipment;
    }

    internal void Show(MonoBehaviour owner, InventoryItemData entry, Sprite icon, Vector2 screenPoint, bool compare)
    {
        if (_owner == owner)
        {
            Move(screenPoint);
            return;
        }

        ItemData item = entry.ItemData;
        bool isEquipment = item.GetItemType() == GameItemType.Equipment;
        if (isEquipment && (_stat == null || _equipment == null))
        {
            Hide();
            return;
        }

        _owner = owner;
        _icon.sprite = icon;
        _icon.enabled = icon != null;
        _title.text = entry.EnhanceLevel > 0 ? $"{item.Name} +{entry.EnhanceLevel}" : item.Name;
        _requirement.text = isEquipment ? $"필요 레벨 {item.ReqLevel}" : string.Empty;
        _requirement.color = isEquipment && _stat.Level < item.ReqLevel ? UnmetLevelColor : Color.white;
        _body.text = isEquipment ? DescribeEquipment(entry, compare) : item.Description ?? string.Empty;
        Layout();
        Move(screenPoint);
        _panel.gameObject.SetActive(true);
    }

    internal void Hide(MonoBehaviour owner)
    {
        if (_owner == owner)
        {
            Hide();
        }
    }

    internal void Hide()
    {
        _owner = null;
        _panel.gameObject.SetActive(false);
    }

    private string DescribeEquipment(InventoryItemData entry, bool compare)
    {
        EquipSlot slot = entry.ItemData.GetEquipSlot();
        string slotLabel = slot switch
        {
            EquipSlot.Weapon => "무기",
            EquipSlot.Helmet => "투구",
            EquipSlot.Armor => "갑옷",
            _ => slot.ToString()
        };
        var text = new StringBuilder(slotLabel);
        _stat.GetEquipmentBonuses(entry, out var candidateBase, out var candidateEnhance);
        AppendStats(text, "기본 스탯", candidateBase);
        AppendStats(text, "강화 보너스", candidateEnhance);
        if (compare)
        {
            var difference = new Dictionary<StatType, float>();
            AddStats(difference, candidateBase, 1f);
            AddStats(difference, candidateEnhance, 1f);
            if (_equipment.TryGetEquippedItem(slot, out InventoryItemData equipped))
            {
                _stat.GetEquipmentBonuses(equipped, out var equippedBase, out var equippedEnhance);
                AddStats(difference, equippedBase, -1f);
                AddStats(difference, equippedEnhance, -1f);
            }

            AppendStats(text, "장착 시 변화", difference, includeZero: true);
        }

        return text.ToString();
    }

    private static void AddStats(Dictionary<StatType, float> total, IReadOnlyDictionary<StatType, float> stats, float sign)
    {
        foreach (var stat in stats)
        {
            if (stat.Value == 0f) { continue; }
            total.TryGetValue(stat.Key, out float current);
            total[stat.Key] = current + sign * stat.Value;
        }
    }

    private static void AppendStats(StringBuilder text, string heading, IReadOnlyDictionary<StatType, float> values, bool includeZero = false)
    {
        bool hasHeading = false;
        foreach (var (type, label) in StatLabels)
        {
            if (!values.TryGetValue(type, out float value) || (!includeZero && value == 0f))
            {
                continue;
            }

            if (!hasHeading)
            {
                text.AppendLine().AppendLine().Append(heading);
                hasHeading = true;
            }

            float displayedValue = Mathf.Round(value * 100f) / 100f;
            text.AppendLine().Append(label).Append("  ").Append(displayedValue == 0f ? "-" : displayedValue.ToString("+0.##;-0.##"));
        }
    }

    private void Layout()
    {
        float width = Mathf.Min(Width, Mathf.Max(Padding * 2f + 1f, _layer.rect.width));
        float innerWidth = width - Padding * 2f;
        float iconWidth = _icon.enabled ? 34f : 0f;
        Place(_icon.rectTransform, Padding, Padding, iconWidth, iconWidth);
        float titleOffset = _icon.enabled ? iconWidth + Gap : 0f;
        float titleHeight = PlaceText(_title, Padding + titleOffset, Padding, Mathf.Max(1f, innerWidth - titleOffset));
        float top = Padding + Mathf.Max(iconWidth, titleHeight) + Gap;
        if (_requirement.text.Length > 0)
        {
            top += PlaceText(_requirement, Padding, top, innerWidth) + Gap;
        }

        float bodyHeight = PlaceText(_body, Padding, top, innerWidth);
        float height = Mathf.Min(_layer.rect.height, top + bodyHeight + Padding);
        _body.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(0f, height - top - Padding));
        _panel.sizeDelta = new Vector2(width, height);
    }

    private void Move(Vector2 screenPoint)
    {
        Camera camera = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screenPoint, camera, out Vector2 pointer) ||
            !RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screenPoint + PointerOffset, camera, out Vector2 position))
        {
            return;
        }

        Rect bounds = _layer.rect;
        Vector2 size = _panel.sizeDelta;
        if (position.x + size.x > bounds.xMax) { position.x = pointer.x - (position.x - pointer.x) - size.x; }
        if (position.y - size.y < bounds.yMin) { position.y = pointer.y + (pointer.y - position.y) + size.y; }
        _panel.anchoredPosition = new Vector2(
            Mathf.Clamp(position.x, bounds.xMin, Mathf.Max(bounds.xMin, bounds.xMax - size.x)),
            Mathf.Clamp(position.y, Mathf.Min(bounds.yMax, bounds.yMin + size.y), bounds.yMax));
    }

    private static RectTransform CreateRect(string name, Transform parent)
    {
        var rect = (RectTransform)new GameObject(name, typeof(RectTransform)).transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private static UnityEngine.UI.Image AddImage(RectTransform rect, Color color)
    {
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private void AddBorder(string name, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
    {
        RectTransform rect = CreateRect(name, _panel);
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        AddImage(rect, Color.white);
    }

    private TextMeshProUGUI CreateText(string name, TMP_FontAsset font, float size)
    {
        var text = CreateRect(name, _panel).gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font;
        text.fontSize = size;
        text.color = Color.white;
        text.richText = false;
        text.overflowMode = TextOverflowModes.Truncate;
        text.raycastTarget = false;
        return text;
    }

    private static float PlaceText(TextMeshProUGUI text, float x, float y, float width)
    {
        float height = Mathf.Ceil(text.GetPreferredValues(text.text, width, Mathf.Infinity).y);
        Place(text.rectTransform, x, y, width, height);
        return height;
    }

    private static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.up;
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }
}
