using UnityEngine;

public class MagnetPickupScanner : MonoBehaviour
{

    public void Scan(Vector2 center, float radius, Transform flyTarget, IPickupReceiver receiver)
    {
        // 범위 내 DroppedItem 탐색
        // IsFlying이 false인 드롭만 선택
        // StartMagnetFly 호출
        if (radius <= 0f || flyTarget == null || receiver == null)
        {
            return;
        }

        Collider2D[] colliders = Physics2D.OverlapCircleAll(center, radius);


        foreach (Collider2D candidateCollider in colliders)
        {
            if (!candidateCollider.TryGetComponent(out DroppedItem droppedItem))
            {
                continue;
            }

            if (droppedItem.IsFlying)
            {
                continue;
            }

            droppedItem.StartMagnetFly(flyTarget, receiver);
        }
    }
}
