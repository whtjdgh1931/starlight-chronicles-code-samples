using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;


[Serializable]
public struct StatUIMapping
{
    public StatType StatType;
    public TextMeshProUGUI TextComponent;
    public string DisplayFormat;
}

public class StatUI : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private UIWindowMotion _windowMotion;
    [SerializeField] private EquipmentPreview _equipmentPreview;

    [SerializeField] private List<StatUIMapping> _statUIMappings = new List<StatUIMapping>();

    public void Initialize()
    {
        if (_windowMotion == null) { throw new MissingReferenceException($"{name}: 창 연출 참조 필요"); }
        _windowMotion.Initialize();
        // 미리보기 참조가 없는 씬의 기존 스탯 창 지원
        _equipmentPreview?.Initialize();
        EventBus.OnStatWindowInputEvent -= ToggleWindow;
        EventBus.OnStatWindowInputEvent += ToggleWindow;

        EventBus.OnPlayerStatsChanged -= RefreshUI;
        EventBus.OnPlayerStatsChanged += RefreshUI;


        gameObject.SetActive(true);
    }

    public void ToggleWindow()
    {
        bool nextState = !_windowMotion.IsVisible;
        _windowMotion.SetVisible(nextState);
        _equipmentPreview?.SetWindowVisible(nextState);
    }

    internal void BindEquipmentPreview(Player owner, PlayerVisual source) => _equipmentPreview?.Bind(owner, source);

    internal void UnbindEquipmentPreview(Player owner) => _equipmentPreview?.Unbind(owner);

    private void RefreshUI(Dictionary<StatType, float> stats)
    {
        foreach (StatUIMapping mapping in _statUIMappings)
        {
            if (stats.TryGetValue(mapping.StatType, out float val))
            {
                mapping.TextComponent.text = string.Format(mapping.DisplayFormat, val);
            }
        }
    }

    private void OnDestroy()
    {
        EventBus.OnPlayerStatsChanged -= RefreshUI;
        EventBus.OnStatWindowInputEvent -= ToggleWindow;
    }
}
