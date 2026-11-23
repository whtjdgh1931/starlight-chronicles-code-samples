using UnityEngine;

public enum DamageNumberStyle
{
    Dealt,
    Received
}

public readonly struct DamageNumberRequest
{
    public DamageNumberRequest(int amount, Vector3 position, DamageNumberStyle style, UnityEngine.EntityId targetId)
    {
        Amount = amount;
        Position = position;
        Style = style;
        EntityId = targetId;
    }

    public int Amount { get; }
    public Vector3 Position { get; }
    public DamageNumberStyle Style { get; }
    /// <summary>로컬 표시에서 피격 대상을 구분하는 인스턴스 ID</summary>
    public UnityEngine.EntityId EntityId { get; }
}
