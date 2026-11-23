using System;
using UnityEngine;

/// <summary>
/// 플레이어 스킬의 실제 판정 범위 표시
/// </summary>
public sealed class PlayerSkillRangeDebugger : MonoBehaviour
{
    [SerializeField] private Player _player;
    [SerializeField] private PlayerSkillController _skillController;
    [SerializeField] private bool _showSkillRanges = true;
    [SerializeField] private Color _areaColor = Color.red;
    [SerializeField] private Color _projectileColor = Color.yellow;
    [SerializeField] private Color _dashColor = Color.cyan;

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || !_showSkillRanges || _player == null || _skillController == null)
        {
            return;
        }

        int directionX = _player.ForcedMovement.SightDirection >= 0 ? 1 : -1;

        for (int slotIndex = 0; slotIndex < _skillController.SkillSlotCount; slotIndex++)
        {
            if (!_skillController.TryGetSlotSkill(slotIndex, out SkillData skillData) || !Enum.TryParse(skillData.ExecutionType, true, out PlayerSkillExecutionType executionType))
            {
                continue;
            }

            DrawSkillRange(skillData, executionType, directionX);
        }
#endif
    }

    private void DrawSkillRange(SkillData skillData, PlayerSkillExecutionType executionType, int directionX)
    {
        switch (executionType)
        {
            case PlayerSkillExecutionType.Area:
                DrawAreaRanges(skillData, directionX);
                break;

            case PlayerSkillExecutionType.Projectile:
                DrawProjectileRange(skillData, directionX);
                break;

            case PlayerSkillExecutionType.Dash:
                DrawDashRange(skillData, directionX);
                break;
        }
    }

    private void DrawAreaRanges(SkillData skillData, int directionX)
    {
        Gizmos.color = _areaColor;

        for (int hitIndex = 0; hitIndex < skillData.AttackRanges.Length; hitIndex++)
        {
            Gizmos.DrawWireCube(GetAttackCenter(skillData, hitIndex, directionX), skillData.AttackRanges[hitIndex]);
        }
    }

    private void DrawProjectileRange(SkillData skillData, int directionX)
    {
        Vector2 start = GetAttackCenter(skillData, 0, directionX);
        Vector2 end = start + Vector2.right * directionX * skillData.ProjectileSpeed * skillData.ProjectileLifetime;

        Gizmos.color = _projectileColor;
        Gizmos.DrawLine(start, end);
        Gizmos.DrawWireSphere(end, 0.1f);
    }

    private void DrawDashRange(SkillData skillData, int directionX)
    {
        Vector2 start = GetAttackCenter(skillData, 0, directionX);
        float distance = skillData.MoveSpeed * skillData.MoveDuration;
        Vector2 end = start + Vector2.right * directionX * distance;
        Vector2 center = (start + end) * 0.5f;
        Vector2 range = skillData.AttackRanges[0];

        Gizmos.color = _dashColor;
        Gizmos.DrawWireCube(center, new Vector3(range.x + distance, range.y));
    }

    private Vector2 GetAttackCenter(SkillData skillData, int hitIndex, int directionX)
    {
        Vector2 offset = skillData.AttackOffsets[hitIndex];
        return (Vector2)_player.transform.position + new Vector2(offset.x * directionX, offset.y);
    }
}