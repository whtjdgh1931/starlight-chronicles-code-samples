using System;
using UnityEngine;

/// <summary>
/// HUD 표시용 아이콘 데이터
/// </summary>
[Serializable]
public class StatusEffectVisual
{
    [SerializeField] private StatusEffectId _effectId;
    [SerializeField] private Sprite _icon;
    [SerializeField] private Color _frameColor = Color.white;

    public StatusEffectId EffectId => _effectId;
    public Sprite Icon => _icon;
    public Color FrameColor => _frameColor;
}
