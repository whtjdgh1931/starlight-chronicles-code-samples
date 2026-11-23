public static class Constants
{
    /// <summary>플레이어 성장과 저장·접속 데이터의 최대 레벨</summary>
    public const int MaxPlayerLevel = 99;

    // 전환 페이드 아래의 툴팁과 강화 결과 표시 순서
    internal const int ItemTooltipSortingOrder = 997;
    internal const int EnhancementResultSortingOrder = 998;
    internal const int SceneFadeSortingOrder = 999;

    #region float
    /// <summary>
    /// 텔레포트 이동 최소 거리
    /// </summary>
    public const float FLOAT_TeleportMinRange = 0.5f;

    /// <summary>
    /// 최대 낙하 속도
    /// </summary>
    public const float FLOAT_MaxFallSpeed = -14.142136f;

    /// <summary>
    /// 플롯 입력 최소값
    /// </summary>
    public const float FLOAT_CheckFloat = 0.1f;

    /// <summary>
    /// 몬스터 사망 딜레이
    /// </summary>
    public const float FLOAT_DeadDelay = 1.5f;

    /// <summary>
    /// 몬스터 땅 끝 체크 거리
    /// </summary>
    public const float FLOAT_WallCheck = 0.5f;

    /// <summary>
    /// 몬스터가 캐릭터를 지난 후 방향 전환을 할 때 까지의 거리
    /// </summary>
    public const float FLOAT_CharacterCheck = 0.1f;

    /// <summary>
    /// 몬스터 소환 간격
    /// </summary>
    public const float FLOAT_SpawnInterval = 5.0f;

    /// <summary>
    /// 몬스터 생성 시 Y축 보정
    /// </summary>
    public const float FLOAT_OffsetY = 0.05f;

    /// <summary>
    /// 몬스터 대기 시간 예비값
    /// </summary>
    public const float FLOAT_IdleDuration = 2f;

    /// <summary>
    /// ray 체크 거리
    /// </summary>
    public const float FLOAT_CheckDistance = 15f;

    /// <summary>
    /// 패트롤 유지 확률
    /// </summary>
    public const float FLOAT_CheckPatrol = 0.7f;

    /// <summary>
    /// 몬스터 patrol 유지 시간
    /// </summary>
    public const float FLOAT_PatrolDuration = 3f;

    /// <summary>
    /// 무적 시간
    /// </summary>
    public const float FLOAT_InvincibleDuration = 1.5f;

    public const float FLOAT_SliderAnimateDuration = 1f;


    public const float FLOAT_ExpMultiplier = 1.2f;
    public const float FLOAT_ExpPenalty = 0.9f;
    #endregion

    #region string


    public const string STR_Table = "Table";

    public const string STR_IconCollection = "IconCollection";

    public const string STR_Blank = "";

    public const string STR_FadeCanvas = "FadeCanvas";
    public const string STR_FadeImage = "FadeImage";

    public const string Prefab_DropItem = "Prefabs/DroppedItem";
    public const string Sprite_GoldSprite = "Icons/GoldIcon";


    #endregion

    #region tag

    /// <summary>
    /// 그라운드 타일맵 태그
    /// </summary>
    public const string TAG_Ground = "Ground";

    public const string TAG_Player = "Player";

    /// <summary>
    /// 몬스터 태그
    /// </summary>
    public const string TAG_Monster = "Monster";

    #endregion

    #region layer
    public const int LAYER_Ground = 3;
    public const int LAYER_UI = 5;
    public const int LAYER_Player = 7;
    public const int LAYER_Wall = 8;
    #endregion

    #region SheetName
    //공통,Sheet 이름
    public const string ID = "ID";
    public const string Name = "Name";
    public const string FistRow = "1";


    public const string PlayerMoveSheet = "PlayerMove";
    public const string MonsterSheet = "Monster";
    public const string QuestSheet = "Quest";
    public const string MonsterAttackSheet = "MonsterAttack";
    public const string BossSheet = "Boss";
    public const string HuntDifficultySheet = "HuntDifficulty";
    public const string HuntSpawnSheet = "HuntSpawn";
    public const string BossPhaseAttackSheet = "BossPhaseAttack";
    public const string ItemSheet = "Item";
    public const string PortalSheet = "PortalData";
    public const string SkillSheet = "Skill";

    //Player
    public const string Player_MoveSpeed = "_moveSpeed";
    public const string Player_Stat = "PlayerStat";

    //Monster


    //Item
    public const string Str_EnhanceLevel = "EnhanceLevel";
    public const string Str_ReqLevel = "ReqLevel";
    public const string Str_Key = "Key";



    #endregion


    #region StringTable Keys (로컬라이즈 대사 키 모음)
    public const string STR_Enhance_Success = "Enhance_Success";
    public const string STR_Enhance_Drop = "Enhance_Drop";
    public const string STR_Enhance_Destroy = "Enhance_Destroy";
    public const string STR_Enhance_Fail = "Enhance_Fail";
    public const string STR_Enhance_NotEnoughGold = "Enhance_NotEnoughGold";
    public const string STR_Enhance_MaxLevel = "Enhance_MaxLevel";
    public const string STR_Enhance_MaxCost = "Enhance_MaxCost";
    public const string STR_Enhance_CostFormat = "Enhance_CostFormat";
    public const string STR_Enhance_ProbFormat = "Enhance_ProbFormat";
    public const string STR_Enhance_InvalidItem = "Enhance_InvalidItem";
    public const string STR_Enhance_StatPreview = "Enhance_StatPreview";
    public const string STR_Enhance_StatCurrent = "Enhance_StatCurrent";
    public const string STR_Stat_NamePrefix = "Stat_Name_";
    public const string STR_Boss_SoloEntryTitle = "Boss_SoloEntryTitle";
    public const string STR_Boss_SoloEntryPrompt = "Boss_SoloEntryPrompt";
    public const string STR_Boss_SoloEntryConfirm = "Boss_SoloEntryConfirm";
    public const string STR_Boss_SoloEntryCancel = "Boss_SoloEntryCancel";
    public const string STR_Boss_RemainingRevivesFormat = "Boss_RemainingRevivesFormat";
    public const string STR_Boss_SoloRevivePreparing = "Boss_SoloRevivePreparing";
    public const string STR_Boss_SoloReviveReady = "Boss_SoloReviveReady";
    public const string STR_Boss_SoloDefeated = "Boss_SoloDefeated";
    public const string STR_Boss_SoloReviveAction = "Boss_SoloReviveAction";
    public const string STR_Boss_SoloReturnToLobby = "Boss_SoloReturnToLobby";
    public const string STR_Boss_OnlineReviveAction = "Boss_OnlineReviveAction";
    public const string STR_Boss_OnlineRevivePreparing = "Boss_OnlineRevivePreparing";
    public const string STR_Boss_OnlineCleared = "Boss_OnlineCleared";
    public const string STR_Boss_OnlineDefeated = "Boss_OnlineDefeated";
    public const string STR_Boss_OnlineReturn = "Boss_OnlineReturn";
    public const string STR_Boss_OnlineResultReceiving = "Boss_OnlineResultReceiving";
    public const string STR_Boss_OnlineResultSaving = "Boss_OnlineResultSaving";
    public const string STR_Boss_OnlineResultSaved = "Boss_OnlineResultSaved";
    public const string STR_Boss_OnlineResultWaitingFormat = "Boss_OnlineResultWaitingFormat";
    public const string STR_Boss_OnlineResultSummaryFormat = "Boss_OnlineResultSummaryFormat";
    public const string STR_Boss_OnlineResultRetrySave = "Boss_OnlineResultRetrySave";
    public const string STR_Boss_OnlineResultRequestAgain = "Boss_OnlineResultRequestAgain";
    public const string STR_Boss_OnlineReturnForfeit = "Boss_OnlineReturnForfeit";
    public const string STR_Boss_OnlineReturnConfirm = "Boss_OnlineReturnConfirm";
    public const string STR_Boss_OnlineRewardInventoryFull = "Boss_OnlineRewardInventoryFull";
    public const string STR_Boss_OnlineRewardForfeitDone = "Boss_OnlineRewardForfeitDone";
    public const string STR_Boss_OnlineRewardForfeitButtonFormat = "Boss_OnlineRewardForfeitButtonFormat";
    public const string STR_Boss_OnlineRewardForfeitConfirmButton = "Boss_OnlineRewardForfeitConfirmButton";
    public const string STR_Boss_OnlineRewardClaimed = "Boss_OnlineRewardClaimed";
    public const string STR_Boss_OnlineRewardForfeited = "Boss_OnlineRewardForfeited";
    public const string STR_Boss_OnlineRewardClaimHint = "Boss_OnlineRewardClaimHint";
    public const string STR_Boss_OnlineRewardRowFormat = "Boss_OnlineRewardRowFormat";
    #endregion


    #region Monster Animator

    public const string ANIM_MonsterIdle = "Idle";
    public const string ANIM_MonsterMove = "Move";
    public const string ANIM_MonsterAttack = "Attack";
    public const string ANIM_MonsterHit = "Hit";
    public const string ANIM_MonsterDie = "Die";
    public const string ANIM_MonsterShot = "Shot";
    #endregion
}

public struct KnockbackData
{
    public float ForceX;
    public float ForceY;
    public float Duration;

    public KnockbackData(float forceX, float forceY, float duration)
    {
        ForceX = forceX;
        ForceY = forceY;
        Duration = duration;
    }

    public static KnockbackData Default => new KnockbackData(3f, 2.5f, 0.25f);
}
