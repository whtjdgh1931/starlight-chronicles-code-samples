using UnityEngine;

/// <summary>
/// Inspector 퀘스트 검증 도구의 대상과 실행 설정
/// 실행 기능은 Editor 전용 검사기에 배치
/// </summary>
[AddComponentMenu("Debug/Quest Tester")]
public sealed class QuestTester : MonoBehaviour
{
    public const int MaximumSimulatedKillsPerRequest = 1000;
    [Header("대상 · 비워 두면 현재 로컬 캐릭터 사용")]
    [SerializeField] private Player _player;
    [SerializeField] private int _questId;
    [SerializeField] private MonsterBase _monster;

    [Header("아이템 지급")]
    [SerializeField] private int _itemId;
    [SerializeField, Min(1)] private int _itemCount = 1;

    [Header("반복·실제 처치")]
    [SerializeField, Range(2, 20)] private int _repeatCount = 2;
    [SerializeField, Min(1f)] private float _damageMultiplier = 10f;

    [Header("처치 집계 모의 실행")]
    [SerializeField, Range(1, MaximumSimulatedKillsPerRequest)] private int _simulatedKillCount = 1;

    public int SimulatedKillCount => _simulatedKillCount;
    public Player TargetPlayer => _player;
    public int QuestId => _questId;
    public MonsterBase TargetMonster => _monster;
    public int ItemId => _itemId;
    public int ItemCount => _itemCount;
    public int RepeatCount => _repeatCount;
    public float DamageMultiplier => _damageMultiplier;
}
