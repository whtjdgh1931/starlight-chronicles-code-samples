using Assets.HeroEditor4D.Common.Scripts.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 아이템 드랍을 관리하는 클래스
/// </summary>
public class ItemDropSpawner : MonoBehaviour
{
    [Header("Drop Assets")]
    [SerializeField] private DroppedItem _droppedItemPrefab;
    [SerializeField] private Sprite _goldIcon;
    [SerializeField] private IconCollection _iconCollection;

    [Header("Launch Settings")]
    [SerializeField]
    private Vector2 _horizontalLaunchForceRange =
        new Vector2(-2f, 2f);

    [SerializeField]
    private Vector2 _verticalLaunchForceRange =
        new Vector2(4f, 6f);

    private ObjectPool _objectPool;
    private IReadOnlyDictionary<int, ItemData> _itemTable;
    private bool _isInitialized;

    public void Initialize(
         ObjectPool objectPool,
         IReadOnlyDictionary<int, ItemData> itemTable)
    {
        CheckComponent();

        if (objectPool == null)
        {
            throw new ArgumentNullException(nameof(objectPool));
        }

        if (itemTable == null)
        {
            throw new ArgumentNullException(nameof(itemTable));
        }

        _objectPool = objectPool;
        _itemTable = itemTable;
        _isInitialized = true;
    }

    private void CheckComponent()
    {
        if (_droppedItemPrefab == null)
        {
            throw new MissingReferenceException(
                $"{name}: DroppedItem Prefab이 연결되지 않았습니다.");
        }

        if (_goldIcon == null)
        {
            throw new MissingReferenceException(
                $"{name}: Gold Icon이 연결되지 않았습니다.");
        }

        if (_iconCollection == null)
        {
            throw new MissingReferenceException(
                $"{name}: IconCollection이 연결되지 않았습니다.");
        }

        if (_horizontalLaunchForceRange.x >
            _horizontalLaunchForceRange.y)
        {
            throw new InvalidOperationException(
                $"{name}: 수평 발사 힘 범위가 올바르지 않습니다.");
        }

        if (_verticalLaunchForceRange.x >
            _verticalLaunchForceRange.y)
        {
            throw new InvalidOperationException(
                $"{name}: 수직 발사 힘 범위가 올바르지 않습니다.");
        }
    }

    public void Spawn(MonsterData monsterData, Vector3 spawnPosition)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException(
                $"{name}: ItemDropSpawner가 초기화되지 않았습니다.");
        }

        if (monsterData == null)
        {
            throw new ArgumentNullException(nameof(monsterData));
        }

        SpawnGold(monsterData, spawnPosition);
        SpawnItems(monsterData, spawnPosition);
    }

    private void SpawnGold(
        MonsterData monsterData,
        Vector3 spawnPosition)
    {
        if (!Roll(monsterData.GoldDropChance))
        {
            return;
        }

        int goldAmount = UnityEngine.Random.Range(
            monsterData.MinGold,
            monsterData.MaxGold + 1);

        DroppedItem droppedItem = Pop(spawnPosition);
        droppedItem.Initialize(goldAmount, _goldIcon);
        droppedItem.Launch(CreateLaunchForce());
    }

    private void SpawnItems(
        MonsterData monsterData,
        Vector3 spawnPosition)
    {
        IReadOnlyList<int> itemIds = monsterData.GetDropItemIDs();
        IReadOnlyList<float> itemChances = monsterData.GetDropItemChances();

        for (int index = 0; index < itemIds.Count; index++)
        {
            if (!Roll(itemChances[index]))
            {
                continue;
            }

            int itemId = itemIds[index];

            if (!_itemTable.TryGetValue(itemId, out ItemData itemData))
            {
                Debug.LogWarning(
                    $"{name}: 몬스터 {monsterData.ID}의 드롭 아이템 " +
                    $"{itemId}를 찾을 수 없습니다.");

                continue;
            }

            Sprite itemIcon = _iconCollection.GetIcon(itemData.IconId);

            DroppedItem droppedItem = Pop(spawnPosition);
            droppedItem.Initialize(itemData, itemIcon);
            droppedItem.Launch(CreateLaunchForce());
        }
    }

    private DroppedItem Pop(Vector3 spawnPosition)
    {
        GameObject dropObject = _objectPool.Pop(
            _droppedItemPrefab.gameObject);

        dropObject.transform.position = spawnPosition;
        return dropObject.GetComponent<DroppedItem>();
    }

    private Vector2 CreateLaunchForce()
    {
        float forceX = UnityEngine.Random.Range(
            _horizontalLaunchForceRange.x,
            _horizontalLaunchForceRange.y);

        float forceY = UnityEngine.Random.Range(
            _verticalLaunchForceRange.x,
            _verticalLaunchForceRange.y);

        return new Vector2(forceX, forceY);
    }

    private static bool Roll(float chance)
    {
        float normalizedChance = Mathf.Clamp01(chance);

        return normalizedChance >= 1f ||
            UnityEngine.Random.value < normalizedChance;
    }
}
