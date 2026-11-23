using UnityEngine;

public class MonsterContactAttack : MonoBehaviour
{
    private float _damage;
    [SerializeField] private Collider2D _attackCollider;

    public void Initialize(float damage, bool enabled)
    {
        if (_attackCollider == null)
        {
            throw new MissingReferenceException($"{name}: 접촉 공격 Collider2D 참조 필요");
        }

        _damage = damage;
        SetEnabled(enabled);
    }

    /// <summary>
    /// 접촉 공격 판정 활성 상태 변경
    /// </summary>
    public void SetEnabled(bool enabled)
    {
        _attackCollider.enabled = enabled;
    }

    private void TryApplyDamage(Collider2D collision)
    {
        if (!collision.CompareTag(Constants.TAG_Player))
        {
            return;
        }

        if (collision.TryGetComponent(out Player player) && player.CanBeMonsterTarget)
        {
            player.TakeDamage(_damage, transform.position);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        TryApplyDamage(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        TryApplyDamage(collision);
    }
}
