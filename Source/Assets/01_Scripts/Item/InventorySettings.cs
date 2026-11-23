using UnityEngine;

[CreateAssetMenu(fileName = "InventorySettings", menuName = "Scriptable Objects/InventorySettings")]
public class InventorySettings : ScriptableObject
{
    [SerializeField, Min(1)] private int _maxSlotCnt = 128;
    public int MaxSlotCnt => _maxSlotCnt;

    [SerializeField, Min(1)] private int _defaultSlotCnt = 24;
    public int DefaultSlotCnt => _defaultSlotCnt;

    [SerializeField, Min(0)] private int _initialGold;
    public int InitialGold => _initialGold;

    private void OnValidate()
    {
        _maxSlotCnt = Mathf.Max(1, _maxSlotCnt);
        _defaultSlotCnt = Mathf.Clamp(
            _defaultSlotCnt,
            1,
            _maxSlotCnt);

        _initialGold = Mathf.Max(0, _initialGold);
    }
}
