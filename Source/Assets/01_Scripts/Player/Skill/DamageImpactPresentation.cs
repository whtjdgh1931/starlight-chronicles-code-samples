using UnityEngine;

public sealed class DamageImpactPresentation : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField] private SwordWaveBurst _playerImpactPrefab;
    [SerializeField] private SwordWaveBurst _monsterImpactPrefab;

    private ObjectPool _objectPool;

    public void SetObjectPool(ObjectPool objectPool)
    {
        _objectPool = objectPool;
    }

    public static Vector3 GetImpactPosition(Bounds body, bool fromLeft, float depth)
    {
        float x = fromLeft ? body.min.x : body.max.x;
        return new Vector3(x, body.center.y, depth);
    }

    public void PlayPlayerImpact(Vector3 position, bool fromLeft, bool isLocalSound)
    {
        Play(_playerImpactPrefab, position, fromLeft, isLocalSound);
    }

    public void PlayMonsterImpact(Vector3 position, bool fromLeft, bool isLocalSound)
    {
        Play(_monsterImpactPrefab, position, fromLeft, isLocalSound);
    }

    private void Play(SwordWaveBurst prefab, Vector3 position, bool fromLeft, bool isLocalSound)
    {
        Quaternion rotation = fromLeft ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
        GameObject effect = _objectPool.Pop(prefab.gameObject);
        effect.GetComponent<SwordWaveBurst>().PlayCombat(
            position, rotation, isLocalSound, isImpact: true);
    }
}
