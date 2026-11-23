using DG.Tweening;
using UnityEngine;

public class DroppedItem : MonoBehaviour
{
    /// <summary>
    /// 골드인지 여부
    /// </summary>
    private bool _isGold;
    public bool IsGold => _isGold;

    /// <summary>
    /// 골드일 경우 드랍된 골드의 양
    /// </summary>
    private int _goldAmount;
    public int GoldAmount => _goldAmount;

    /// <summary>
    /// 아이템일 경우 아이템의 데이타
    /// </summary>
    private ItemData _itemData;
    public ItemData ItemData => _itemData;

    [SerializeField] private SpriteRenderer _spriteRenderer;
    [SerializeField] private Rigidbody2D _rb;
    [SerializeField] private Collider2D _colldier;

    /// <summary>
    /// 이동 트윈
    /// </summary>
    private Tween _magnetTween;
    private PoolableObject _poolableObject;
    [SerializeField] private float _magnetDuration = 0.4f;

    /// <summary>
    /// 드롭 아이템 스프라이트 길이
    /// </summary>
    [SerializeField] private float _targetVisualSize;

    private bool _isFlying;
    public bool IsFlying => _isFlying;

    public void Initialize(int goldAmount, Sprite goldIcon)
    {
        _poolableObject ??= GetComponent<PoolableObject>();
        RestoreToField();

        _isGold = true;
        _goldAmount = goldAmount;
        _itemData = null;
        SetVisual(goldIcon);

    }

    public void Initialize(ItemData itemData, Sprite itemIcon)
    {
        _poolableObject ??= GetComponent<PoolableObject>();
        RestoreToField();

        _isGold = false;
        _goldAmount = 0;
        _itemData = itemData;
        SetVisual(itemIcon);

    }

    public void SetVisual(Sprite sprite)
    {
        if (sprite == null)
        {
            _spriteRenderer.sprite = null;
            return;
        }

        _spriteRenderer.sprite = sprite;

        Vector2 spriteSize = sprite.bounds.size;
        float longestSize = Mathf.Max(spriteSize.x, spriteSize.y);
        float normalizedSize = _targetVisualSize / longestSize;

        _spriteRenderer.transform.localScale = Vector3.one * normalizedSize;
    }

    public void Launch(Vector2 force)
    {
        _rb.linearVelocity = Vector2.zero;
        _rb.AddForce(force, ForceMode2D.Impulse);
    }

    public bool StartMagnetFly(Transform flyTarget, IPickupReceiver receiver)
    {
        if (_isFlying || flyTarget == null || receiver == null || !receiver.CanReceive(this))
        {
            return false;
        }

        _magnetTween?.Kill();
        _magnetTween = null;

        _isFlying = true;
        DisablePhysics();

        Vector3 startPosition = transform.position;

        _magnetTween = DOVirtual.Float(
            0f,
            1f,
            _magnetDuration,
            progress =>
            {
                if (flyTarget == null)
                {
                    CancelMagnetFly();
                    return;
                }

                float easedProgress = progress * progress;

                transform.position = Vector3.LerpUnclamped(
                    startPosition,
                    flyTarget.position,
                    easedProgress);
            })
            .SetEase(Ease.Linear)
            .SetLink(gameObject, LinkBehaviour.KillOnDisable)
            .OnComplete(() =>
            {
                _magnetTween = null;
                CompleteMagnetPickup(receiver);
            });

        return true;
    }

    public bool TryPickup(IPickupReceiver receiver)
    {
        if (_isFlying || receiver == null)
        {
            return false;
        }

        if (!receiver.TryReceive(this))
        {
            return false;
        }

        ReturnToPool();
        return true;
    }

    public void CompleteMagnetPickup(IPickupReceiver receiver)
    {
        if (receiver != null && receiver.TryReceive(this))
        {
            ReturnToPool();
            return;
        }

        RestoreToField();
    }

    public void RestoreToField()
    {
        _magnetTween?.Kill();
        _magnetTween = null;

        _isFlying = false;

        _rb.gravityScale = 1f;
        _rb.linearVelocity = Vector2.zero;
        _rb.angularVelocity = 0f;

        _colldier.enabled = true;
    }

    private void DisablePhysics()
    {
        _rb.gravityScale = 0f;
        _rb.linearVelocity = Vector2.zero;
        _colldier.enabled = false;
    }

    private void ReturnToPool()
    {
        _magnetTween?.Kill();
        _magnetTween = null;

        _isFlying = false;
        _poolableObject.Release();
    }

    private void CancelMagnetFly()
    {
        Tween tween = _magnetTween;
        _magnetTween = null;

        tween?.Kill();

        RestoreToField();
    }
}


