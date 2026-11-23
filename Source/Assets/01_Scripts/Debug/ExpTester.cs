using UnityEngine;

public class ExpTester : MonoBehaviour
{
    [Header("지급할 경험치 양")]
    public float ExpToAdd = 100f;

    public void GiveExp()
    {
        GameObject playerObj = GameObject.FindWithTag(Constants.TAG_Player);
        if (playerObj != null && playerObj.TryGetComponent<Player>(out var player))
        {
            player.PlayerStat.AddExp(ExpToAdd);
            Debug.Log($"[ExpTester] 경험치 {ExpToAdd} 지급 완료!");
        }
    }

    public void ForceLevelUp()
    {
        GameObject playerObj = GameObject.FindWithTag(Constants.TAG_Player);
        if (playerObj != null && playerObj.TryGetComponent<Player>(out var player))
        {
            player.PlayerStat.ForceLevelUp();
            Debug.Log($"[ExpTester] 강제 레벨업 작동!");
        }
    }
}
