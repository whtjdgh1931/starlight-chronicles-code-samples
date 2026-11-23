using System.Collections.Generic;

/// <summary>
/// 에디터 직접 실행과 Google Sheets 목업 업로드에 사용할 로컬 게임 데이터를 생성합니다.
/// </summary>
public sealed class LocalGameDataProvider
{
    private const float DefaultMonsterTurnDelay = 0.25f;
    private const string TelegraphTargetModeNone = "None";
    private const string TelegraphTargetModeFollowTarget = "FollowTarget";
    private const string TelegraphTargetModeLockedPosition = "LockedPosition";

    /// <summary>
    /// 플레이어 이동 기본값을 생성합니다.
    /// </summary>
    public PlayerMoveData CreatePlayerMoveData()
    {
        return new PlayerMoveData
        {
            MoveSpeed = 5.0f,
            JumpCnt = 2,
            NormalJumpPower = 8.282512f,
            HighJumpPower = 9.465727f,
            DoubleJumpForwardPower = 5.0f,
            DoubleJumpUpPower = 9.465727f,
            DownJumpPower = 3.0f
        };
    }

    /// <summary>
    /// 플레이어 스탯 기본값을 생성합니다.
    /// </summary>
    public PlayerStatData CreatePlayerStatData()
    {
        return new PlayerStatData
        {
            BaseHp = 1000f,
            BaseMp = 4000f,
            BaseAtk = 10f,
            BaseDef = 5f,
            BaseStat = 5f,
            MainStatCoefficient = 1f
        };
    }

    /// <summary>
    /// Google Sheets Monster 탭과 동일한 몬스터 데이터를 생성합니다.
    /// </summary>
    public Dictionary<int, MonsterData> CreateMonsterTable()
    {
        var table = new Dictionary<int, MonsterData>
        {
            { 1001, CreateMonster(1001, "달팽이", 180f, 50f, 1f, 1f, 14f, 2f, 10, 20, 0.6f, "3001,6001", "0.05,0.30") },
            { 1002, CreateMonster(1002, "파란 달팽이", 240f, 87f, 1.2f, 2f, 15f, 2f, 15, 30, 0.65f, "4001,6001", "0.05,0.35") },
            { 1003, CreateMonster(1003, "빨간 달팽이", 480f, 105f, 1.4f, 8f, 43f, 2f, 20, 40, 0.7f, "6001", "0.4") },
            { 1004, CreateMonster(1004, "슬라임", 480f, 123f, 1.8f, 6f, 30f, 1.5f, 25, 50, 0.75f, "3001,6002", "0.08,0.40") },
            { 1005, CreateMonster(1005, "주황버섯", 260f, 65f, 1.15f, 3f, 21f, 1.5f, 30, 60, 0.8f, "3002,5001", "0.04,0.06") },
            { 1006, CreateMonster(1006, "리본돼지", 285f, 72f, 1.65f, 4f, 26f, 1f, 40, 80, 0.85f, "5001", "0.1") },
            { 1007, CreateMonster(1007, "초록버섯", 850f, 175f, 1.8f, 15f, 150f, 1.5f, 50, 100, 0.9f, "3002", "0.08") },
            { 1008, CreateMonster(1008, "뿔버섯", 620f, 140f, 1.5f, 11f, 69f, 1.5f, 70, 130, 0.9f, "", "") },
            { 1009, CreateMonster(1009, "좀비버섯", 920f, 195f, 1.55f, 17f, 215f, 2f, 80, 150, 0.9f, "", "") },
            { 1010, CreateWildBoar() },
            { 1011, CreateMonster(1011, "파이어보어", 1100f, 235f, 3.6f, 20f, 370f, 1f, 120, 250, 0.9f, "", "") },
            { 1012, CreateMonster(1012, "스텀프", 700f, 145f, 1.3f, 12f, 88f, 3f, 18, 35, 0.6f, "6002", "1") },
            { 1013, CreateArcherMonster() },
            { 1014, CreateSwordMonster() },
            { 9001, CreateTestBossMonster() }
        };
        AppendForestMonsters(table);
        return table;
    }

    /// <summary>
    /// Google Sheets MonsterAttack 탭과 동일한 몬스터 공격 데이터를 생성
    /// </summary>
    public Dictionary<int, MonsterAttackData> CreateMonsterAttackTable()
    {
        var table = new Dictionary<int, MonsterAttackData>
        {
            { 101001, CreateMonsterAttack(101001, 1010, "Charge", 1.2f, 4f, 2f, 0.6f, 1f, 6f, 16f, 2f, 6f, 3f, 0.3f, 0f, 0f, TelegraphTargetModeNone) },
            { 101301, CreateArcherAttack() },
            { 101401, CreateSwordAttack() },
            { 900101, CreateMonsterAttack(900101, 9001, "Melee", 1f, 3f, 4f, 0.5f, 0.4f, 2.5f, 0f, 0f, 4f, 2f, 0.2f, 0f, 0f, TelegraphTargetModeLockedPosition) },
            { 900102, CreateMonsterAttack(900102, 9001, "Projectile", 0.9f, 12f, 6f, 1f, 0.6f, 5f, 0f, 0f, 0f, 0f, 0f, 7f, 6f, TelegraphTargetModeFollowTarget) },
            { 900103, CreateMonsterAttack(900103, 9001, "Charge", 1.2f, 8f, 4f, 1f, 1f, 14f, 20f, 2f, 8f, 4f, 0.4f, 0f, 0f, TelegraphTargetModeLockedPosition) },
            { 900104, CreateMonsterAttack(900104, 9001, "Melee", 1.2f, 6f, 4f, 0.7f, 0.6f, 5f, 0f, 0f, 6f, 3f, 0.3f, 0f, 0f, TelegraphTargetModeLockedPosition) },
            { 900105, CreateMonsterAttack(900105, 9001, "Projectile", 0.9f, 14f, 8f, 1.2f, 0.6f, 8f, 0f, 0f, 0f, 0f, 0f, 5f, 8f, TelegraphTargetModeFollowTarget) }
        };
        AppendForestAttacks(table);
        return table;
    }

    /// <summary>
    /// Google Sheets Boss 탭과 동일한 테스트 보스 데이터 생성
    /// </summary>
    public Dictionary<int, BossData> CreateBossTable() => new Dictionary<int, BossData> { { 9001, new BossData { BossId = 9001, PhaseChangeHpRatio = 0.5f, PhaseTransitionDuration = 2f } } };

    /// <summary>시트와 캐시가 없을 때의 사냥터 입장 규칙</summary>
    public Dictionary<int, HuntDifficultyData> CreateHuntDifficultyTable()
    {
        return new Dictionary<int, HuntDifficultyData>
        {
            [(int)HuntDifficulty.Beginner] = new() { Difficulty = HuntDifficulty.Beginner, Name = "초급", RequiredLevel = 1 },
            [(int)HuntDifficulty.Intermediate] = new() { Difficulty = HuntDifficulty.Intermediate, Name = "중급", RequiredLevel = 8 },
            [(int)HuntDifficulty.Advanced] = new() { Difficulty = HuntDifficulty.Advanced, Name = "상급", RequiredLevel = 15 }
        };
    }

    /// <summary>
    /// 시트와 캐시가 없을 때의 사냥터 몬스터 배치
    /// </summary>
    public List<HuntSpawnData> CreateHuntSpawnRows()
    {
        return new List<HuntSpawnData>
        {
            new() { ID = 101, Difficulty = HuntDifficulty.Beginner, ZoneId = HuntSpawnZone.WestCenter, MonsterID = 1001, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0f },
            new() { ID = 102, Difficulty = HuntDifficulty.Beginner, ZoneId = HuntSpawnZone.EastCenter, MonsterID = 1005, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0.6f },
            new() { ID = 103, Difficulty = HuntDifficulty.Beginner, ZoneId = HuntSpawnZone.Lower, MonsterID = 1006, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.2f },
            new() { ID = 104, Difficulty = HuntDifficulty.Beginner, ZoneId = HuntSpawnZone.WestCanopy, MonsterID = 1021, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.8f },
            new() { ID = 105, Difficulty = HuntDifficulty.Beginner, ZoneId = HuntSpawnZone.EastCanopy, MonsterID = 1014, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 2.4f },
            new() { ID = 201, Difficulty = HuntDifficulty.Intermediate, ZoneId = HuntSpawnZone.WestCenter, MonsterID = 1003, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0f },
            new() { ID = 202, Difficulty = HuntDifficulty.Intermediate, ZoneId = HuntSpawnZone.EastCenter, MonsterID = 1022, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0.6f },
            new() { ID = 203, Difficulty = HuntDifficulty.Intermediate, ZoneId = HuntSpawnZone.Lower, MonsterID = 1008, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.2f },
            new() { ID = 204, Difficulty = HuntDifficulty.Intermediate, ZoneId = HuntSpawnZone.WestCanopy, MonsterID = 1012, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.8f },
            new() { ID = 205, Difficulty = HuntDifficulty.Intermediate, ZoneId = HuntSpawnZone.EastCanopy, MonsterID = 1023, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 2.4f },
            new() { ID = 301, Difficulty = HuntDifficulty.Advanced, ZoneId = HuntSpawnZone.WestCenter, MonsterID = 1007, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0f },
            new() { ID = 302, Difficulty = HuntDifficulty.Advanced, ZoneId = HuntSpawnZone.EastCenter, MonsterID = 1013, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 0.6f },
            new() { ID = 303, Difficulty = HuntDifficulty.Advanced, ZoneId = HuntSpawnZone.Lower, MonsterID = 1009, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.2f },
            new() { ID = 304, Difficulty = HuntDifficulty.Advanced, ZoneId = HuntSpawnZone.WestCanopy, MonsterID = 1010, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 1.8f },
            new() { ID = 305, Difficulty = HuntDifficulty.Advanced, ZoneId = HuntSpawnZone.EastCanopy, MonsterID = 1011, MaxAlive = 7, BatchCount = 4, SpawnInterval = 5f, InitialDelay = 2.4f }
        };
    }

    /// <summary>
    /// Google Sheets BossPhaseAttack 탭과 동일한 페이즈별 공격 데이터 생성
    /// </summary>
    public Dictionary<int, BossPhaseAttackData> CreateBossPhaseAttackTable()
    {
        return new Dictionary<int, BossPhaseAttackData>
        {
            { 9001101, new BossPhaseAttackData { PhaseAttackId = 9001101, BossID = 9001, Phase = 1, PatternID = 900101, Weight = 5f } },
            { 9001102, new BossPhaseAttackData { PhaseAttackId = 9001102, BossID = 9001, Phase = 1, PatternID = 900102, Weight = 3f } },
            { 9001104, new BossPhaseAttackData { PhaseAttackId = 9001104, BossID = 9001, Phase = 1, PatternID = 900104, Weight = 2f } },
            { 9001201, new BossPhaseAttackData { PhaseAttackId = 9001201, BossID = 9001, Phase = 2, PatternID = 900101, Weight = 2f } },
            { 9001202, new BossPhaseAttackData { PhaseAttackId = 9001202, BossID = 9001, Phase = 2, PatternID = 900102, Weight = 3f } },
            { 9001203, new BossPhaseAttackData { PhaseAttackId = 9001203, BossID = 9001, Phase = 2, PatternID = 900103, Weight = 4f } },
            { 9001204, new BossPhaseAttackData { PhaseAttackId = 9001204, BossID = 9001, Phase = 2, PatternID = 900104, Weight = 3f } },
            { 9001205, new BossPhaseAttackData { PhaseAttackId = 9001205, BossID = 9001, Phase = 2, PatternID = 900105, Weight = 2f } },
        };
    }

    /// <summary>
    /// Google Sheets Item 탭과 동일한 아이템 데이터를 생성합니다.
    /// </summary>
    /// <summary>
    /// 여행 상인의 퀘스트 5종 기본 정의
    /// 수락·처치 집계·수집품 소모·보상 지급은 퀘스트 실행 계층의 책임
    /// </summary>
    public Dictionary<int, QuestData> CreateQuestTable()
    {
        var quests = new Dictionary<int, QuestData>
        {
            { 1001, new QuestData
                {
                    ID = 1001,
                    Name = "숲길의 첫걸음",
                    Description = "초급 사냥터 중앙 왼쪽에서 달팽이 8마리를 처치하고 마을 상인에게 돌아오세요. 보상 골드로 포션과 첫 강화를 준비하세요.",
                    ObjectiveType = QuestObjectiveType.KillMonster,
                    TargetID = 1001,
                    TargetCount = 8,
                    RequiredQuestID = 0,
                    RewardGold = 300,
                    RewardExp = 300,
                    RewardItemID = 2001,
                    RewardItemCount = 5
                }
            },
            { 1002, new QuestData
                {
                    ID = 1002,
                    Name = "초급 사냥터 졸업",
                    Description = "초급 사냥터 상층 왼쪽에서 숲 슬라임 10마리를 처치하세요. 보상 수령 후 8레벨부터 중급에 입장할 수 있어요. 받은 무기를 장착하고 포션을 준비하세요.",
                    ObjectiveType = QuestObjectiveType.KillMonster,
                    TargetID = 1021,
                    TargetCount = 10,
                    RequiredQuestID = 1001,
                    RewardGold = 700,
                    RewardExp = 1000,
                    RewardItemID = 3002,
                    RewardItemCount = 1
                }
            },
            { 1003, new QuestData
                {
                    ID = 1003,
                    Name = "위험한 가시 제거",
                    Description = "중급 사냥터 중앙 오른쪽에서 가시 버섯 10마리를 처치하세요. 투사체를 피하며 싸우고 보상 골드로 장비를 구매하거나 강화하세요.",
                    ObjectiveType = QuestObjectiveType.KillMonster,
                    TargetID = 1022,
                    TargetCount = 10,
                    RequiredQuestID = 1002,
                    RewardGold = 1500,
                    RewardExp = 3000,
                    RewardItemID = 2002,
                    RewardItemCount = 5
                }
            },
            { 1004, new QuestData
                {
                    ID = 1004,
                    Name = "보스 도전 준비",
                    Description = "중급 사냥터 상층 왼쪽의 스텀프가 확정으로 떨어뜨리는 나뭇가지 8개를 상인에게 전달하세요. 보상 수령 후 상급 사냥과 장비 강화를 거쳐 보스전을 준비하세요.",
                    ObjectiveType = QuestObjectiveType.CollectItem,
                    TargetID = 6002,
                    TargetCount = 8,
                    RequiredQuestID = 1003,
                    RewardGold = 2500,
                    RewardExp = 3000,
                    RewardItemID = 2005,
                    RewardItemCount = 5
                }
            },
            { 1005, new QuestData
                {
                    ID = 1005,
                    Name = "달빛 수호자에게 도전",
                    Description = "파티 대기장에서 방을 만들거나 참가한 뒤 달빛 수호 골렘 1마리를 처치하세요. 20레벨과 강화 장비를 권장해요. 전투 종료 후 마을 상인에게 돌아와 의뢰 보상을 받으세요.",
                    ObjectiveType = QuestObjectiveType.KillMonster,
                    TargetID = 9001,
                    TargetCount = 1,
                    RequiredQuestID = 1004,
                    RewardGold = 3000,
                    RewardExp = 1000,
                    RewardItemID = 2003,
                    RewardItemCount = 5
                }
            }
        };
#if UNITY_EDITOR
        // 선행 조건 없는 독립 검증 정의, 일반 빌드의 퀘스트 목록에서는 제외
        var validationQuests = new[]
        {
            new QuestData
            {
                ID = 91001, Name = "[검증] 슬라임 공동 집계 A", Description = "숲 슬라임 3마리 처치. 공동 집계 B와 함께 수락해 상한 검증",
                ObjectiveType = QuestObjectiveType.KillMonster, TargetID = 1021, TargetCount = 3,
                RequiredQuestID = 0, RewardItemID = 2001, RewardItemCount = 1
            },
            new QuestData
            {
                ID = 91002, Name = "[검증] 슬라임 공동 집계 B", Description = "숲 슬라임 5마리 처치. 공동 집계 A와 같은 처치 동시 반영 검증",
                ObjectiveType = QuestObjectiveType.KillMonster, TargetID = 1021, TargetCount = 5,
                RequiredQuestID = 0, RewardItemID = 2001, RewardItemCount = 1
            },
            new QuestData
            {
                ID = 91003, Name = "[검증] 나뭇가지 공유 수집 A", Description = "나뭇가지 3개 전달. 복합 수집 B와 수집품 공유 및 소모 후 진행도 검증",
                ObjectiveType = QuestObjectiveType.CollectItem, TargetID = 6002, TargetCount = 3,
                RequiredQuestID = 0, RewardItemID = 2001, RewardItemCount = 1
            },
            new QuestData
            {
                ID = 91004, Name = "[검증] 나뭇가지·껍질 복합 수집 B", Description = "나뭇가지 5개와 달팽이의 껍질 2개 전달. 공유 수집 A와 겹치는 목표 및 아이템별 진단 검증",
                ObjectiveType = QuestObjectiveType.CollectItem,
                RequiredItems = new List<ItemRequirement>
                {
                    new ItemRequirement { ItemId = 6002, Count = 5 },
                    new ItemRequirement { ItemId = 6001, Count = 2 }
                },
                RequiredQuestID = 0, RewardItemID = 2001, RewardItemCount = 1
            }
        };
        foreach (QuestData quest in validationQuests) { quests.Add(quest.ID, quest); }
#endif
        return quests;
    }

    public Dictionary<int, ItemData> CreateItemTable()
    {
        return new Dictionary<int, ItemData>
        {
            { 2001, new ItemData { ID = 2001, Cooldown = 1f, Name = "빨간포션", Description = "체력을 50 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.RedPotionS", Price = 20, RestoreHp = 50 } },
            { 2002, new ItemData { ID = 2002, Cooldown = 1.5f, Name = "주황포션", Description = "체력을 150 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.OrangePotionL", Price = 60, RestoreHp = 150 } },
            { 2003, new ItemData { ID = 2003, Cooldown = 2f, Name = "하얀포션", Description = "체력을 300 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "Starlight.Generated.Supplies.WhitePotion", Price = 120, RestoreHp = 300 } },
            { 2004, new ItemData { ID = 2004, Cooldown = 1f, Name = "파란포션", Description = "마나를 100 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.BluePotionS", Price = 30, RestoreMp = 100 } },
            { 2005, new ItemData { ID = 2005, Cooldown = 1.5f, Name = "마나엘릭서", Description = "마나를 300 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.BluePotionL", Price = 90, RestoreMp = 300 } },
            { 2006, new ItemData { ID = 2006, Cooldown = 5f, Name = "엘릭서", Description = "체력과 마나를 50% 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.PurplePotionS", Price = 650, RestoreHpRatio = 0.5f, RestoreMpRatio = 0.5f } },
            { 2007, new ItemData { ID = 2007, Cooldown = 8f, Name = "파워엘릭서", Description = "체력과 마나를 모두 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.WinnerPotion", Price = 1500, RestoreHpRatio = 1, RestoreMpRatio = 1 } },
            { 2008, new ItemData { ID = 2008, Cooldown = 2f, Name = "맑은물", Description = "마나를 800 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "Starlight.Generated.Supplies.ClearWater", Price = 240, RestoreMp = 800 } },
            { 2009, new ItemData { ID = 2009, Cooldown = 4f, Name = "수박", Description = "체력을 1000 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "Starlight.Generated.Supplies.Watermelon", Price = 400, RestoreHp = 1000 } },
            { 2010, new ItemData { ID = 2010, Cooldown = 3f, Name = "통닭", Description = "체력을 500 회복시켜 줍니다.", ItemType = "Consumable", EquipSlot = "None", IconId = "Common.Christmas.Supplies.ChristmasTurkey", Price = 200, RestoreHp = 500 } },
            { 3001, new ItemData { ID = 3001, Cooldown = 0f, Name = "초보자의 검", Description = "기본 무기입니다.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 10, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.AdvancedShortSword", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.AdvancedShortSword", Price = 500, ReqLevel = 1 } },
            { 3002, new ItemData { ID = 3002, Cooldown = 0f, Name = "강철검", Description = "더 강력한 무기입니다.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 25, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.AssaultSword", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.AssaultSword", Price = 1800, ReqLevel = 5 } },
            { 4001, new ItemData { ID = 4001, Cooldown = 0f, Name = "갈색 두건", Description = "기본 투구입니다.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 10, AddDef = 5, SpriteId = "FantasyHeroes.Basic.Armor.HardLeatherArmor", IconId = "FantasyHeroes.Basic.Helmet.HardLeatherArmor", Price = 300, ReqLevel = 1 } },
            { 5001, new ItemData { ID = 5001, Cooldown = 0f, Name = "수련복", Description = "기본 갑옷입니다.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 20, AddDef = 10, SpriteId = "FantasyHeroes.Basic.Armor.HardLeatherArmor", IconId = "FantasyHeroes.Basic.Armor.HardLeatherArmor", Price = 700, ReqLevel = 1 } },
            { 3003, new ItemData { ID = 3003, Cooldown = 0f, Name = "토벌자의 검", Description = "보스 토벌 보상 무기. 공격력 +40.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 40, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.AssaultSword", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.AssaultSword", Price = 4000, ReqLevel = 10 } },
            { 5002, new ItemData { ID = 5002, Cooldown = 0f, Name = "토벌자의 갑옷", Description = "보스 토벌 보상 방어구. 체력 +100, 방어력 +15.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 100, AddDef = 15, SpriteId = "FantasyHeroes.Basic.Armor.HardLeatherArmor", IconId = "FantasyHeroes.Basic.Armor.HardLeatherArmor", Price = 3000, ReqLevel = 10 } },
            { 6001, new ItemData { ID = 6001, Cooldown = 0f, Name = "달팽이의 껍질", Description = "달팽이에게서 얻은 둥근 껍질입니다.", ItemType = "Etc", EquipSlot = "None", IconId = "Starlight.Generated.Supplies.SnailShell", Price = 10 } },
            { 6002, new ItemData { ID = 6002, Cooldown = 0f, Name = "나뭇가지", Description = "몬스터들이 떨어뜨린 작은 나뭇가지입니다.", ItemType = "Etc", EquipSlot = "None", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.WoodenStick", Price = 5 } },
            { 2011, new ItemData { ID = 2011, Cooldown = 2f, Name = "숲사과", Description = "체력을 100 회복하는 싱그러운 숲사과.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.GreenApple", Price = 40, RestoreHp = 100 } },
            { 2012, new ItemData { ID = 2012, Cooldown = 3f, Name = "여행자의 빵", Description = "체력 200과 마나 50을 회복하는 든든한 빵.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.Bread", Price = 100, RestoreHp = 200, RestoreMp = 50 } },
            { 2013, new ItemData { ID = 2013, Cooldown = 4f, Name = "구운 생선", Description = "체력 400과 마나 80을 회복하는 구운 생선.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.RoastFish", Price = 200, RestoreHp = 400, RestoreMp = 80 } },
            { 2014, new ItemData { ID = 2014, Cooldown = 4f, Name = "버섯 수프", Description = "체력 300과 마나 180을 회복하는 따뜻한 수프.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.MushroomSoup", Price = 220, RestoreHp = 300, RestoreMp = 180 } },
            { 2015, new ItemData { ID = 2015, Cooldown = 3f, Name = "별빛 회복약", Description = "체력 600과 마나 300을 회복하는 별빛 회복약.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.RecoveryPotion", Price = 360, RestoreHp = 600, RestoreMp = 300 } },
            { 2016, new ItemData { ID = 2016, Cooldown = 4f, Name = "작은 별빛 엘릭서", Description = "최대 체력과 최대 마나의 25%를 회복하는 엘릭서.", ItemType = "Consumable", EquipSlot = "None", IconId = "FantasyHeroes.Basic.Supplies.PurplePotionS", Price = 300, RestoreHpRatio = 0.25f, RestoreMpRatio = 0.25f } },
            { 3004, new ItemData { ID = 3004, Cooldown = 0f, Name = "수습 검사의 단검", Description = "레벨 3 모험가를 위한 한손 무기. 공격력 +18.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 18, AddStat = 1, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.BronzeDagger", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.BronzeDagger", Price = 1000, ReqLevel = 3 } },
            { 3005, new ItemData { ID = 3005, Cooldown = 0f, Name = "숲지기의 손도끼", Description = "레벨 5 모험가를 위한 한손 무기. 공격력 +28.", ItemType = "Equipment", EquipSlot = "Weapon", AddHp = 10, AddAtk = 28, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.BalancedAxe", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.BalancedAxe", Price = 2200, ReqLevel = 5 } },
            { 3006, new ItemData { ID = 3006, Cooldown = 0f, Name = "대장장이의 망치", Description = "레벨 8 모험가를 위한 한손 무기. 공격력 +34.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 34, AddDef = 2, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.BlacksmithHammer", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.BlacksmithHammer", Price = 3200, ReqLevel = 8 } },
            { 3007, new ItemData { ID = 3007, Cooldown = 0f, Name = "수호자의 장검", Description = "레벨 10 모험가를 위한 한손 무기. 공격력 +38.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 38, AddDef = 3, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.GatekeeperSword", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.GatekeeperSword", Price = 4400, ReqLevel = 10 } },
            { 3008, new ItemData { ID = 3008, Cooldown = 0f, Name = "모험가의 곡도", Description = "레벨 12 모험가를 위한 한손 무기. 공격력 +43.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 43, AddStat = 1, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.Cutlus", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.Cutlus", Price = 5800, ReqLevel = 12 } },
            { 3009, new ItemData { ID = 3009, Cooldown = 0f, Name = "서리늑대 도끼", Description = "레벨 15 모험가를 위한 한손 무기. 공격력 +48.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 48, AddStat = 2, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.BlueWolfAxe", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.BlueWolfAxe", Price = 7500, ReqLevel = 15 } },
            { 3010, new ItemData { ID = 3010, Cooldown = 0f, Name = "유적의 정복검", Description = "레벨 18 모험가를 위한 한손 무기. 공격력 +55.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 55, AddStat = 3, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.ConquerorSword", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.ConquerorSword", Price = 9500, ReqLevel = 18 } },
            { 3011, new ItemData { ID = 3011, Cooldown = 0f, Name = "흑강 카타나", Description = "레벨 20 모험가를 위한 한손 무기. 공격력 +62.", ItemType = "Equipment", EquipSlot = "Weapon", AddAtk = 62, AddStat = 4, SpriteId = "FantasyHeroes.Basic.MeleeWeapon1H.DarkSteelKatana", IconId = "FantasyHeroes.Basic.MeleeWeapon1H.DarkSteelKatana", Price = 12000, ReqLevel = 20 } },
            { 4002, new ItemData { ID = 4002, Cooldown = 0f, Name = "여행자의 후드", Description = "레벨 3 투구. 체력 +20, 방어력 +6.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 20, AddDef = 6, SpriteId = "FantasyHeroes.Basic.Armor.BanditLightArmor", IconId = "FantasyHeroes.Basic.Helmet.BanditLightArmor", Price = 650, ReqLevel = 3 } },
            { 5003, new ItemData { ID = 5003, Cooldown = 0f, Name = "여행자의 가죽옷", Description = "레벨 3 옷. 체력 +40, 방어력 +11.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 40, AddDef = 11, SpriteId = "FantasyHeroes.Basic.Armor.BanditLightArmor", IconId = "FantasyHeroes.Basic.Armor.BanditLightArmor", Price = 1300, ReqLevel = 3 } },
            { 4003, new ItemData { ID = 4003, Cooldown = 0f, Name = "숲지기의 모자", Description = "레벨 5 투구. 체력 +30, 방어력 +8.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 30, AddDef = 8, AddStat = 1, SpriteId = "FantasyHeroes.Basic.Armor.HunterClothes", IconId = "FantasyHeroes.Basic.Helmet.HunterClothes", Price = 1100, ReqLevel = 5 } },
            { 5004, new ItemData { ID = 5004, Cooldown = 0f, Name = "숲지기의 사냥복", Description = "레벨 5 옷. 체력 +60, 방어력 +13.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 60, AddDef = 13, AddStat = 1, SpriteId = "FantasyHeroes.Basic.Armor.HunterClothes", IconId = "FantasyHeroes.Basic.Armor.HunterClothes", Price = 2000, ReqLevel = 5 } },
            { 4004, new ItemData { ID = 4004, Cooldown = 0f, Name = "경비대 투구", Description = "레벨 8 투구. 체력 +45, 방어력 +10.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 45, AddDef = 10, AddStat = 1, SpriteId = "FantasyHeroes.Basic.Armor.GuardArmor", IconId = "FantasyHeroes.Basic.Helmet.GuardArmor", Price = 1800, ReqLevel = 8 } },
            { 5005, new ItemData { ID = 5005, Cooldown = 0f, Name = "경비대 제복", Description = "레벨 8 옷. 체력 +80, 방어력 +15.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 80, AddDef = 15, AddStat = 1, SpriteId = "FantasyHeroes.Basic.Armor.GuardArmor", IconId = "FantasyHeroes.Basic.Armor.GuardArmor", Price = 2800, ReqLevel = 8 } },
            { 4005, new ItemData { ID = 4005, Cooldown = 0f, Name = "철제 투구", Description = "레벨 12 투구. 체력 +70, 방어력 +14.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 70, AddDef = 14, AddStat = 2, SpriteId = "FantasyHeroes.Basic.Armor.IronArmor", IconId = "FantasyHeroes.Basic.Helmet.IronArmor", Price = 2800, ReqLevel = 12 } },
            { 5006, new ItemData { ID = 5006, Cooldown = 0f, Name = "철제 흉갑", Description = "레벨 12 옷. 체력 +120, 방어력 +18.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 120, AddDef = 18, AddStat = 2, SpriteId = "FantasyHeroes.Basic.Armor.IronArmor", IconId = "FantasyHeroes.Basic.Armor.IronArmor", Price = 4000, ReqLevel = 12 } },
            { 4006, new ItemData { ID = 4006, Cooldown = 0f, Name = "기사단 투구", Description = "레벨 16 투구. 체력 +100, 방어력 +18.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 100, AddDef = 18, AddStat = 2, SpriteId = "FantasyHeroes.Basic.Armor.KnightArmor", IconId = "FantasyHeroes.Basic.Helmet.KnightArmor", Price = 4200, ReqLevel = 16 } },
            { 5007, new ItemData { ID = 5007, Cooldown = 0f, Name = "기사단 갑옷", Description = "레벨 16 옷. 체력 +170, 방어력 +24.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 170, AddDef = 24, AddStat = 2, SpriteId = "FantasyHeroes.Basic.Armor.KnightArmor", IconId = "FantasyHeroes.Basic.Armor.KnightArmor", Price = 5800, ReqLevel = 16 } },
            { 4007, new ItemData { ID = 4007, Cooldown = 0f, Name = "별빛기사 투구", Description = "레벨 20 투구. 체력 +130, 방어력 +22.", ItemType = "Equipment", EquipSlot = "Helmet", AddHp = 130, AddDef = 22, AddStat = 3, SpriteId = "FantasyHeroes.Basic.Armor.EliteKnightArmor", IconId = "FantasyHeroes.Basic.Helmet.EliteKnightArmor", Price = 6000, ReqLevel = 20 } },
            { 5008, new ItemData { ID = 5008, Cooldown = 0f, Name = "별빛기사 갑옷", Description = "레벨 20 옷. 체력 +230, 방어력 +30.", ItemType = "Equipment", EquipSlot = "Armor", AddHp = 230, AddDef = 30, AddStat = 3, SpriteId = "FantasyHeroes.Basic.Armor.EliteKnightArmor", IconId = "FantasyHeroes.Basic.Armor.EliteKnightArmor", Price = 8000, ReqLevel = 20 } }
        };
    }


    /// <summary>
    /// 기존 시트·캐시 항목을 보존하며 누락 아이템 기본값 보완
    /// 구형 캐시에 회복 수치가 없는 소비 아이템만 회복 기본값 복원
    /// </summary>
    public void AppendMissingItemData(Dictionary<int, ItemData> items, bool restoreLegacyRecovery)
    {
        foreach (var entry in CreateItemTable())
        {
            if (items.TryAdd(entry.Key, entry.Value))
            {
                continue;
            }

            ItemData item = items[entry.Key];
            if (item.Cooldown < 0f) { item.Cooldown = entry.Value.Cooldown; }
            if (!restoreLegacyRecovery || item.GetItemType() != GameItemType.Consumable ||
                item.RestoreHp != 0f || item.RestoreMp != 0f ||
                item.RestoreHpRatio != 0f || item.RestoreMpRatio != 0f)
            {
                continue;
            }

            item.RestoreHp = entry.Value.RestoreHp;
            item.RestoreMp = entry.Value.RestoreMp;
            item.RestoreHpRatio = entry.Value.RestoreHpRatio;
            item.RestoreMpRatio = entry.Value.RestoreMpRatio;
        }
    }

    /// <summary>
    /// Google Sheets EnhanceData 탭과 동일한 강화 확률 데이터를 생성합니다.
    /// </summary>
    public Dictionary<int, EnhanceData> CreateEnhanceProbabilityTable()
    {
        return new Dictionary<int, EnhanceData>
        {
            { 0, CreateEnhanceData(0, 0.95f, 0f, 0f, 100) },
            { 1, CreateEnhanceData(1, 0.9f, 0f, 0f, 150) },
            { 2, CreateEnhanceData(2, 0.85f, 0f, 0f, 200) },
            { 3, CreateEnhanceData(3, 0.85f, 0f, 0f, 250) },
            { 4, CreateEnhanceData(4, 0.8f, 0f, 0f, 300) },
            { 5, CreateEnhanceData(5, 0.75f, 0f, 0f, 350) },
            { 6, CreateEnhanceData(6, 0.7f, 0f, 0f, 400) },
            { 7, CreateEnhanceData(7, 0.65f, 0f, 0f, 500) },
            { 8, CreateEnhanceData(8, 0.6f, 0f, 0f, 600) },
            { 9, CreateEnhanceData(9, 0.55f, 0f, 0f, 800) },
            { 10, CreateEnhanceData(10, 0.5f, 0f, 0f, 1000) },
            { 11, CreateEnhanceData(11, 0.45f, 0.55f, 0f, 1500) },
            { 12, CreateEnhanceData(12, 0.4f, 0.6f, 0f, 2000) },
            { 13, CreateEnhanceData(13, 0.35f, 0.65f, 0f, 2500) },
            { 14, CreateEnhanceData(14, 0.3f, 0.7f, 0f, 3500) },
            { 15, CreateEnhanceData(15, 0.3f, 0.68f, 0.02f, 5000) },
            { 16, CreateEnhanceData(16, 0.3f, 0.68f, 0.02f, 7000) },
            { 17, CreateEnhanceData(17, 0.3f, 0.68f, 0.02f, 10000) },
            { 18, CreateEnhanceData(18, 0.25f, 0.72f, 0.03f, 15000) },
            { 19, CreateEnhanceData(19, 0.2f, 0.75f, 0.05f, 20000) }
        };
    }

    /// <summary>
    /// 장비 요구 레벨을 내림차순으로 정렬한 강화 스탯 데이터를 생성합니다.
    /// </summary>
    public List<EnhanceStatData> CreateEnhanceStatList()
    {
        return new List<EnhanceStatData>
        {
            new EnhanceStatData { ReqLevel = 16, BaseBonusAtk = 5, BaseBonusDef = 5, BaseBonusHp = 100, BaseBonusStat = 2 },
            new EnhanceStatData { ReqLevel = 13, BaseBonusAtk = 4, BaseBonusDef = 4, BaseBonusHp = 50, BaseBonusStat = 2 },
            new EnhanceStatData { ReqLevel = 10, BaseBonusAtk = 3, BaseBonusDef = 3, BaseBonusHp = 30, BaseBonusStat = 1 },
            new EnhanceStatData { ReqLevel = 5, BaseBonusAtk = 2, BaseBonusDef = 2, BaseBonusHp = 20, BaseBonusStat = 1 },
            new EnhanceStatData { ReqLevel = 1, BaseBonusAtk = 1, BaseBonusDef = 1, BaseBonusHp = 10, BaseBonusStat = 1 }
        };
    }

    /// <summary>
    /// Google Sheets StringData 탭과 동일한 문자열 데이터를 생성합니다.
    /// </summary>
    public Dictionary<string, string> CreateStringTable()
    {
        return new Dictionary<string, string>
        {
            { Constants.STR_Enhance_Success, "<color=green>[성공!] +{0}강이 되었습니다!</color>" },
            { Constants.STR_Enhance_Drop, "<color=orange>[하락...] 수치가 떨어졌습니다. 현재: +{0}강</color>" },
            { Constants.STR_Enhance_Destroy, "<color=red>[파괴!!!] 아이템이 견디지 못하고 파괴되어 흔적만 남았습니다.</color>" },
            { Constants.STR_Enhance_Fail, "<color=yellow>[실패] 다행히 장비 수치가 유지되었습니다.</color>" },
            { Constants.STR_Enhance_NotEnoughGold, "골드가 부족합니다!" },
            { Constants.STR_Enhance_MaxLevel, "최대 강화 수치 도달 (MAX)" },
            { Constants.STR_Enhance_MaxCost, "- Gold" },
            { Constants.STR_Enhance_CostFormat, "필요 골드: {0} G" },
            { Constants.STR_Enhance_ProbFormat, "성공 확률: {0}%\n하락 확률: {1}%\n파괴 확률: {2}%" },
            { Constants.STR_Enhance_InvalidItem, "장비 아이템만 강화할 수 있습니다." },
            { Constants.STR_Enhance_StatPreview, "강화 {0} +{1:0.##} → +{2:0.##}" },
            { Constants.STR_Enhance_StatCurrent, "강화 {0} +{1:0.##}" },
            { Constants.STR_Boss_SoloEntryTitle, "보스에게 도전" },
            { Constants.STR_Boss_SoloEntryPrompt, "보스에게 혼자 도전할까요?" },
            { Constants.STR_Boss_SoloEntryConfirm, "혼자 도전" },
            { Constants.STR_Boss_SoloEntryCancel, "취소" },
            { Constants.STR_Boss_RemainingRevivesFormat, "남은 부활 기회: {0}" },
            { Constants.STR_Boss_SoloRevivePreparing, "쓰러졌어요." },
            { Constants.STR_Boss_SoloReviveReady, "다시 일어날 수 있어요." },
            { Constants.STR_Boss_SoloDefeated, "패배 · 남은 부활 기회를 모두 사용했어요." },
            { Constants.STR_Boss_SoloReviveAction, "부활하기" },
            { Constants.STR_Boss_SoloReturnToLobby, "로비로 돌아가기" },
            { Constants.STR_Boss_OnlineReviveAction, "부활" },
            { Constants.STR_Boss_OnlineRevivePreparing, "부활 준비 중" },
            { Constants.STR_Boss_OnlineCleared, "BOSS CLEAR" },
            { Constants.STR_Boss_OnlineDefeated, "GAME OVER" },
            { Constants.STR_Boss_OnlineReturn, "귀환" },
            { Constants.STR_Boss_OnlineResultReceiving, "결과 수신 중" },
            { Constants.STR_Boss_OnlineResultSaving, "저장 중" },
            { Constants.STR_Boss_OnlineResultSaved, "저장 완료 / HP/MP 전량 회복" },
            { Constants.STR_Boss_OnlineResultWaitingFormat, "저장 완료 / 참가자 {0}명 결과 수신 대기" },
            { Constants.STR_Boss_OnlineResultSummaryFormat, "경험치 {0:0} / 골드 {1:N0} G\n개인 아이템 {2}개 / 미수령 {3}개" },
            { Constants.STR_Boss_OnlineResultRetrySave, "저장 재시도" },
            { Constants.STR_Boss_OnlineResultRequestAgain, "결과 재요청" },
            { Constants.STR_Boss_OnlineReturnForfeit, "포기 후 귀환" },
            { Constants.STR_Boss_OnlineReturnConfirm, "확인 후 귀환" },
            { Constants.STR_Boss_OnlineRewardInventoryFull, "인벤토리 공간이 부족해 아이템을 수령할 수 없습니다." },
            { Constants.STR_Boss_OnlineRewardForfeitDone, "미수령 보상을 포기했습니다." },
            { Constants.STR_Boss_OnlineRewardForfeitButtonFormat, "남은 보상 포기 ({0})" },
            { Constants.STR_Boss_OnlineRewardForfeitConfirmButton, "포기 확인" },
            { Constants.STR_Boss_OnlineRewardClaimed, "수령 완료" },
            { Constants.STR_Boss_OnlineRewardForfeited, "수령 포기" },
            { Constants.STR_Boss_OnlineRewardClaimHint, "더블클릭으로 수령" },
            { Constants.STR_Boss_OnlineRewardRowFormat, "{0} ×{1}   ·   {2}" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.Atk), "공격력" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.Def), "방어력" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.MaxHp), "최대 HP" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.Str), "힘" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.Dex), "민첩" },
            { Constants.STR_Stat_NamePrefix + nameof(StatType.Int), "지능" }
        };
    }

    /// <summary>
    /// Google Sheets PortalData 탭과 동일한 포탈 데이터를 생성합니다.
    /// </summary>
    public Dictionary<int, PortalData> CreatePortalTable()
    {
        return new Dictionary<int, PortalData>
        {
            { 1, new PortalData { ID = 1, TargetScene = "HuntScene", TargetPortalID = 2 } },
            { 2, new PortalData { ID = 2, TargetScene = "HomeScene", TargetPortalID = 1 } },
            { 3, new PortalData { ID = 3, TargetScene = "HostScene", TargetPortalID = 4 } },
            { 4, new PortalData { ID = 4, TargetScene = "HuntScene", TargetPortalID = 3 } },
            { 5, new PortalData { ID = 5, TargetScene = "BossScene", TargetPortalID = 6 } },
            { 6, new PortalData { ID = 6, TargetScene = "HostScene", TargetPortalID = 5 } }
        };
    }

    /// <summary>
    /// 시트·캐시의 기존 값을 보존하며 신규 숲 몬스터와 공격 기본값 보완
    /// 시트에 동일 ID 추가 시 해당 시트 데이터 우선
    /// </summary>
    public void AppendForestCreatureData(Dictionary<int, MonsterData> monsters, Dictionary<int, MonsterAttackData> attacks)
    {
        AppendForestMonsters(monsters);
        AppendForestAttacks(attacks);

        // 이전 시트와 캐시에서 사냥터에 배치된 궁수·검사 데이터 보완
        monsters.TryAdd(1013, CreateArcherMonster());
        monsters.TryAdd(1014, CreateSwordMonster());
        attacks.TryAdd(101301, CreateArcherAttack());
        attacks.TryAdd(101401, CreateSwordAttack());
    }

    // 온라인·로컬 경로에서 공유하는 신규 몬스터 기본값
    private static void AppendForestMonsters(Dictionary<int, MonsterData> monsters)
    {
        monsters.TryAdd(1021, CreateMonster(1021, "숲 슬라임", 250f, 60f, 1.25f, 4f, 24f, 1.8f, 18, 35, 0.75f, "6001", "0.25"));
        monsters.TryAdd(1022, CreateMonster(1022, "가시 버섯", 560f, 130f, 0.95f, 9f, 54f, 2f, 25, 50, 0.8f, "6002", "0.25"));
        monsters.TryAdd(1023, CreateMonster(1023, "숲 늑대", 760f, 160f, 2.45f, 14f, 120f, 1f, 40, 75, 0.85f, "6002", "0.30"));
    }

    // 온라인·로컬 경로에서 공유하는 신규 공격 기본값
    private static void AppendForestAttacks(Dictionary<int, MonsterAttackData> attacks)
    {
        attacks.TryAdd(102101, CreateMonsterAttack(102101, 1021, "Melee", 1f, 1.3f, 1.5f, 0.35f, 0.5f, 2f, 0f, 0f, 2f, 1f, 0.15f, 0f, 0f, TelegraphTargetModeNone));
        attacks.TryAdd(102201, CreateMonsterAttack(102201, 1022, "Projectile", 1f, 7f, 3f, 0.45f, 0.7f, 3f, 0f, 0f, 2f, 1f, 0.15f, 5f, 4f, TelegraphTargetModeNone));
        attacks.TryAdd(102301, CreateMonsterAttack(102301, 1023, "Charge", 1.2f, 4f, 2f, 0.6f, 0.5f, 3.5f, 6.5f, 0.5f, 4f, 2f, 0.2f, 0f, 0f, TelegraphTargetModeNone));
    }

    /// <summary>
    /// Google Sheets Skill 탭과 동일한 전사 스킬 데이터 생성
    /// </summary>
    public Dictionary<int, SkillData> CreateSkillTable()
    {
        return new Dictionary<int, SkillData>
    {
        { 100101, CreateSkill(100101, "검기", "Projectile", "1.3", "5", "(1.5,0.8)", "(1.0,0.4)", 0f, 4f, 10f, 12f, 1.2f, 0f, 0f) },
        { 100102, CreateSkill(100102, "트리플 슬래시", "Area", "0.8*3", "4*3", "(2.8,1.8)*3", "(1.4,0.0)*3", 0.12f, 5f, 15f, 0f, 0f, 0f, 0f) },
        { 100103, CreateSkill(100103, "돌진 공격", "Dash", "1.5", "5", "(2.0,1.5)", "(1.0,0.0)", 0f, 6f, 20f, 0f, 0f, 12f, 0.45f) }
    };
    }

    private static MonsterData CreateMonster(
        int id,
        string name,
        float hp,
        float atk,
        float speed,
        float level,
        float exp,
        float idleDuration,
        int minGold,
        int maxGold,
        float goldDropChance,
        string dropItemIds = "",
        string dropItemChances = "")
    {
        return new MonsterData
        {
            ID = id,
            Name = name,
            Hp = hp,
            Atk = atk,
            Speed = speed,
            Level = level,
            Exp = exp,
            IdleDuration = idleDuration,
            PatrolDuration = Constants.FLOAT_PatrolDuration,
            ChaseDuration = 5f,
            TurnDelay = DefaultMonsterTurnDelay,
            DetectionRangeX = 5f,
            DetectionRangeY = 3f,
            LoseTargetRangeX = 8f,
            LoseTargetRangeY = 4f,
            MinGold = minGold,
            MaxGold = maxGold,
            GoldDropChance = goldDropChance,
            DropItemIDs = dropItemIds,
            DropItemChances = dropItemChances,
            ContactDamageEnabled = true
        };
    }

    /// <summary>
    /// Google Sheets 와일드보어 돌진 데이터 생성
    /// </summary>
    private static MonsterData CreateWildBoar()
    {
        return CreateMonster(1010, "와일드보어", 1000f, 210f, 3.1f, 18f, 265f, 1f, 100, 200, 0.9f, "", "");
    }

    /// <summary>
    /// Google Sheets 궁수 몬스터 투사체 데이터 생성
    /// </summary>
    private static MonsterData CreateArcherMonster()
    {
        MonsterData data = CreateMonster(1013, "궁수 몬스터", 760f, 160f, 1.45f, 16f, 175f, 3f, 18, 35, 0.6f, "6002", "0.3");
        data.ContactDamageEnabled = false;
        return data;
    }

    /// <summary>
    /// Google Sheets 검사 몬스터 근접 공격 데이터 생성
    /// </summary>
    private static MonsterData CreateSwordMonster()
    {
        MonsterData data = CreateMonster(1014, "검사 몬스터", 320f, 75f, 1.35f, 6f, 33f, 3f, 18, 35, 0.6f, "6002", "0.3");
        data.ContactDamageEnabled = false;
        return data;
    }

    /// <summary>
    /// Google Sheets 테스트 보스 공통 데이터 생성
    /// </summary>
    private static MonsterData CreateTestBossMonster()
    {
        MonsterData data = CreateMonster(9001, "달빛 수호 골렘", 50000f, 250f, 2f, 20f, 3200f, 1f, 500, 1000, 1f, "3003,5002", "1,0.5");
        data.PatrolDuration = 0f;
        data.ChaseDuration = 10f;
        data.DetectionRangeX = 30f;
        data.DetectionRangeY = 8f;
        data.LoseTargetRangeX = 35f;
        data.LoseTargetRangeY = 10f;
        data.ContactDamageEnabled = false;
        return data;
    }

    private static MonsterAttackData CreateArcherAttack()
    {
        return CreateMonsterAttack(101301, 1013, "Projectile", 1f, 6f, 2f, 0.6f, 0.5f, 2f, 0f, 0f, 0f, 0f, 0f, 8f, 5f, TelegraphTargetModeNone);
    }

    private static MonsterAttackData CreateSwordAttack()
    {
        return CreateMonsterAttack(101401, 1014, "Melee", 1.2f, 2f, 1.5f, 0.4f, 0.4f, 1.5f, 0f, 0f, 4f, 2f, 0.2f, 0f, 0f, TelegraphTargetModeNone);
    }

    private static MonsterAttackData CreateMonsterAttack(
        int patternId,
        int monsterId,
        string attackType,
        float damageMultiplier,
        float rangeX,
        float rangeY,
        float preparationDuration,
        float recoveryDuration,
        float cooldown,
        float chargeSpeed,
        float chargeDuration,
        float knockbackForceX,
        float knockbackForceY,
        float knockbackDuration,
        float projectileSpeed,
        float projectileLifetime,
        string telegraphTargetMode)
    {
        return new MonsterAttackData
        {
            PatternId = patternId,
            MonsterID = monsterId,
            AttackType = attackType,
            DamageMultiplier = damageMultiplier,
            RangeX = rangeX,
            RangeY = rangeY,
            PreparationDuration = preparationDuration,
            RecoveryDuration = recoveryDuration,
            Cooldown = cooldown,
            ChargeSpeed = chargeSpeed,
            ChargeDuration = chargeDuration,
            KnockbackForceX = knockbackForceX,
            KnockbackForceY = knockbackForceY,
            KnockbackDuration = knockbackDuration,
            ProjectileSpeed = projectileSpeed,
            ProjectileLifetime = projectileLifetime,
            TelegraphTargetMode = telegraphTargetMode
        };
    }

    private static EnhanceData CreateEnhanceData(
        int enhanceLevel,
        float successRate,
        float dropRate,
        float destroyRate,
        int cost)
    {
        return new EnhanceData
        {
            EnhanceLevel = enhanceLevel,
            SuccessRate = successRate,
            DropRate = dropRate,
            DestroyRate = destroyRate,
            Cost = cost
        };
    }

    private static SkillData CreateSkill(
        int id,
        string skillName,
        string executionType,
        string damageFormat,
        string targetFormat,
        string rangeFormat,
        string offsetFormat,
        float hitInterval,
        float cooldown,
        float mpCost,
        float projectileSpeed,
        float projectileLifetime,
        float moveSpeed,
        float moveDuration)
    {
        return new SkillData
        {
            ID = id,
            SkillName = skillName,
            PlayerClass = nameof(PlayerClass.Warrior),
            ExecutionType = executionType,
            DamageFormat = damageFormat,
            TargetFormat = targetFormat,
            RangeFormat = rangeFormat,
            OffsetFormat = offsetFormat,
            HitInterval = hitInterval,
            Cooldown = cooldown,
            MpCost = mpCost,
            ProjectileSpeed = projectileSpeed,
            ProjectileLifetime = projectileLifetime,
            MoveSpeed = moveSpeed,
            MoveDuration = moveDuration
        };
    }
}
