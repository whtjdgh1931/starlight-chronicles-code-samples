using Assets.HeroEditor4D.Common.Scripts.CharacterScripts;
using Assets.HeroEditor4D.Common.Scripts.Data;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using DG.Tweening;
using System.Collections;
using System.Linq;
using UnityEngine;


public class PlayerVisual : MonoBehaviour, IObjectPoolReceiver
{
    [SerializeField, Min(0.02f)] private float _invincibilityBlinkInterval = 0.1f;
    private SpriteRenderer[] _blinkRenderers = System.Array.Empty<SpriteRenderer>();
    private Coroutine _invincibilityBlink;
    private Character4D _character4D;
    private AnimationManager _animationManager;
    private const string DashClipName = "FastStab";
    private const string ActionSpeedParameter = "Speed";
    private const float WorldAppearanceScale = 1.2f;
    private const float BasicAttackWindupAngle = 5f;
    private const float BasicAttackWindupDuration = 0.14f;
    private Tween _basicAttackWindup;
    private Quaternion _basicAttackRestRotation;
    [SerializeField, Range(0f, 30f)] private float _dashLeanAngle = 14f;
    private Coroutine _dashMotion;
    private Quaternion _restRotation;
    private float _dashClipLength;
    private float _restActionSpeed;
    private int _sightDirection = 1;
    [Header("접지 연출")]
    [SerializeField] private SpriteRenderer _contactShadow;
    [SerializeField] private ParticleSystem _moveDustPrefab;
    [SerializeField, Range(0f, 0.15f)] private float _landingCompression = 0.06f;
    [SerializeField, Min(0.01f)] private float _landingDuration = 0.14f;
    [SerializeField, Min(0f)] private float _minimumLandingAirTime = 0.12f;
    private readonly PlayerLocomotionVisual _locomotion = new();
    private Transform _scaledVisualRoot;

    public void Initialize()
    {
        StopBasicAttackWindup();
        StopDashMotion();
        StopInvincibilityBlink();
        _locomotion.Clear();
        _character4D = GetComponentInChildren<Character4D>(true);
        _character4D.gameObject.SetActive(true);
        _character4D.Initialize();
        ScaleWorldAppearance();
        _blinkRenderers = _character4D.GetComponentsInChildren<SpriteRenderer>(true);
        _animationManager = _character4D.AnimationManager;
        _restRotation = _character4D.transform.localRotation;
        _dashClipLength = _animationManager.Animator.runtimeAnimatorController.animationClips.First(clip => clip.name == DashClipName).length;
        _locomotion.Initialize(this, _character4D.transform, _contactShadow, _moveDustPrefab,
            _landingCompression, _landingDuration, _minimumLandingAirTime);

        SetDirection(Vector2.right);
        SetState(CharacterState.Idle);
    }

    private void ScaleWorldAppearance()
    {
        Transform visualRoot = _character4D.transform;
        if (_scaledVisualRoot == visualRoot) { return; }

        Vector3 position = visualRoot.position;
        if (_contactShadow != null)
        {
            float feetY = _contactShadow.transform.position.y;
            position.y = feetY + (position.y - feetY) * WorldAppearanceScale;
        }

        // 접지 위치를 기준으로 외형만 확대하고 플레이어 루트는 유지
        visualRoot.localScale *= WorldAppearanceScale;
        visualRoot.position = position;
        _scaledVisualRoot = visualRoot;
    }

    /// <summary>
    /// 초기화된 캐릭터 자식 표시의 활성 상태 변경
    /// </summary>
    public void SetVisible(bool isVisible)
    {
        if (!isVisible)
        {
            StopBasicAttackWindup();
            StopInvincibilityBlink();
        }
        _locomotion.SetVisible(isVisible);
        // 접속 종료 중 먼저 파괴된 캐릭터 자식 제외
        if (_character4D != null)
        {
            _character4D.gameObject.SetActive(isVisible);
        }
    }

    /// <summary>
    /// 애니메이션 상태, 접지 표시와 지상 이동 전환 연출
    /// </summary>
    public void SetState(CharacterState state)
    {
        if (state == CharacterState.Death)
        {
            StopBasicAttackWindup();
            StopDashMotion();
            StopInvincibilityBlink();
        }
        _animationManager.SetState(state);
        _locomotion.SetState(state, _dashMotion != null);
    }

    /// <summary>현재 맵의 연출 풀 연결과 씬 전환 시 교체</summary>
    public void SetObjectPool(ObjectPool objectPool) => _locomotion.SetObjectPool(objectPool);

    public void SetDirection(Vector2 direction)
    {
        _sightDirection = direction.x >= 0f ? 1 : -1;
        _character4D.SetDirection(direction);
    }

    public void TriggerAttack() => _animationManager.Attack();

    public void TriggerHit()
    {
        StopBasicAttackWindup();
        _animationManager.Hit();
    }

    /// <summary>기본 공격 입력 직후 외형만 살짝 기울이는 준비 연출</summary>
    public void PlayBasicAttackWindup()
    {
        if (_basicAttackWindup != null || _dashMotion != null || _animationManager.IsAction ||
            !_character4D.gameObject.activeInHierarchy) { return; }

        Transform visualRoot = _character4D.transform;
        _basicAttackRestRotation = visualRoot.localRotation;
        visualRoot.localRotation = _basicAttackRestRotation * Quaternion.Euler(0f, 0f, _sightDirection * BasicAttackWindupAngle);
        _basicAttackWindup = visualRoot.DOLocalRotateQuaternion(_basicAttackRestRotation, BasicAttackWindupDuration)
            .SetEase(Ease.OutQuad)
            .OnComplete(() => _basicAttackWindup = null);
    }

    /// <summary>준비 연출 중단과 외형 회전 복원</summary>
    public void StopBasicAttackWindup()
    {
        if (_basicAttackWindup == null) { return; }
        _basicAttackWindup.Kill();
        _basicAttackWindup = null;
        if (_character4D != null) { _character4D.transform.localRotation = _basicAttackRestRotation; }
    }

    /// <summary>공통 상태 전환을 통한 사망 연출</summary>
    public void TriggerDie() => SetState(CharacterState.Death);

    /// <summary>
    /// 플레이어 행동 연출
    /// </summary>
    public void PlayAction(PlayerActionType action, float duration = 0f)
    {
        StopBasicAttackWindup();
        _locomotion.StopLandingMotion();
        StopDashMotion();
        switch (action)
        {
            case PlayerActionType.BasicAttack: TriggerAttack(); break;
            case PlayerActionType.ProjectileSkill: _animationManager.HeavySlash1H(); break;
            case PlayerActionType.AreaSkill: _animationManager.Slash2H(); break;
            case PlayerActionType.DashSkill:
                _restActionSpeed = _animationManager.Animator.GetFloat(ActionSpeedParameter);
                _animationManager.Animator.SetFloat(ActionSpeedParameter, _dashClipLength / duration);
                _animationManager.FastStab();
                _dashMotion = StartCoroutine(CoDashMotion(duration));
                break;
            default: throw new System.ArgumentOutOfRangeException(nameof(action));
        }
    }

    /// <summary>
    /// 스킬 실행 방식에 대응하는 기존 행동 종류 조회
    /// </summary>
    public static PlayerActionType GetSkillAction(PlayerSkillExecutionType executionType)
    {
        return executionType switch
        {
            PlayerSkillExecutionType.Projectile => PlayerActionType.ProjectileSkill,
            PlayerSkillExecutionType.Area => PlayerActionType.AreaSkill,
            PlayerSkillExecutionType.Dash => PlayerActionType.DashSkill,
            _ => throw new System.ArgumentOutOfRangeException(nameof(executionType))
        };
    }

    /// <summary>
    /// 준비·돌진·복귀에 따른 시각 루트 기울기와 애니메이션 길이 연결
    /// 실제 이동 루트와 물리 콜라이더는 회전 대상에서 제외
    /// </summary>
    private IEnumerator CoDashMotion(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float lean = progress < 0.2f ? Mathf.SmoothStep(0f, 1f, progress / 0.2f)
                : Mathf.SmoothStep(1f, 0f, (progress - 0.2f) / 0.8f);
            _character4D.transform.localRotation = _restRotation * Quaternion.Euler(0f, 0f, -_sightDirection * _dashLeanAngle * lean);
            elapsed += Time.deltaTime;
            yield return null;
        }
        RestoreDashPose();
        _dashMotion = null;
    }

    /// <summary>
    /// 벽 충돌·중단·돌진 종료 시 모션 복귀 시간 단축
    /// 이동 판정은 기존 실행기에서 종료
    /// </summary>
    public void EndDashAction()
    {
        StopDashMotion();
    }

    private void StopDashMotion()
    {
        if (_dashMotion == null)
        {
            return;
        }
        StopCoroutine(_dashMotion);
        _dashMotion = null;
        RestoreDashPose();
    }

    private void RestoreDashPose()
    {
        _character4D.transform.localRotation = _restRotation;
        _animationManager.Animator.SetFloat(ActionSpeedParameter, _restActionSpeed);
    }

    /// <summary>
    /// 실제 무적 지속시간 동안 캐릭터 렌더러만 점멸
    /// 애니메이션과 장비의 활성 상태 유지
    /// </summary>
    public void PlayInvincibilityBlink(float duration)
    {
        StopInvincibilityBlink();
        if (duration <= 0f || !isActiveAndEnabled) { return; }
        _invincibilityBlink = StartCoroutine(CoInvincibilityBlink(duration));
    }

    private IEnumerator CoInvincibilityBlink(float duration)
    {
        float startedAt = Time.time;
        int previousPhase = -1;
        while (Time.time < startedAt + duration)
        {
            int phase = Mathf.FloorToInt((Time.time - startedAt) / _invincibilityBlinkInterval);
            if (phase != previousPhase)
            {
                SetBlinkHidden(phase % 2 == 0);
                previousPhase = phase;
            }
            yield return null;
        }
        SetBlinkHidden(false);
        _invincibilityBlink = null;
    }

    /// <summary>
    /// 사망·비활성화·풀 반환 시 점멸 중단과 표시 복원
    /// </summary>
    public void StopInvincibilityBlink()
    {
        if (_invincibilityBlink != null)
        {
            StopCoroutine(_invincibilityBlink);
            _invincibilityBlink = null;
        }
        SetBlinkHidden(false);
    }

    private void SetBlinkHidden(bool hidden)
    {
        foreach (SpriteRenderer renderer in _blinkRenderers)
        {
            // 접속 종료 중 먼저 파괴된 캐릭터 자식 제외
            if (renderer != null) { renderer.forceRenderingOff = hidden; }
        }
    }

    private void OnDisable()
    {
        StopBasicAttackWindup();
        _locomotion.Clear();
        StopDashMotion();
        StopInvincibilityBlink();
    }

    public void ApplyEquipmentChange(EquipSlot slot, InventoryItemData inventoryItemData)
    {
        ApplyEquipmentAppearance(slot, inventoryItemData?.ItemData);
    }

    /// <summary>초기화된 캐릭터의 현재 몸체와 장비 외형 복사</summary>
    internal void CopyAppearanceTo(Character4D preview)
    {
        for (int i = 0; i < preview.Parts.Count; i++)
        {
            CopyPreviewPart(_character4D.Parts[i], preview.Parts[i]);
        }

        preview.Animator.SetInteger("WeaponType", (int)_character4D.WeaponType);
    }

    private static void CopyPreviewPart(Character source, Character target)
    {
        target.Body = source.Body?.ToList();
        target.Head = source.Head;
        target.Ears = source.Ears?.ToList();
        target.Hair = source.Hair;
        target.HairCropped = source.HairCropped;
        target.Beard = source.Beard;
        target.Expressions = source.Expressions?.ToList() ?? target.Expressions;
        target.Expression = "Default";
        target.Helmet = source.Helmet;
        target.ShowHelmet = source.ShowHelmet;
        // 미착용 갑옷과 선택 장비의 빈 목록 유지
        target.Armor = source.Armor?.ToList();
        target.PrimaryWeapon = source.PrimaryWeapon;
        target.SecondaryWeapon = source.SecondaryWeapon;
        target.Back = source.Back;
        target.Quiver = source.Quiver;
        target.Shield = source.Shield?.ToList();
        target.CompositeWeapon = source.CompositeWeapon?.ToList();
        target.Makeup = source.Makeup;
        target.Mask = source.Mask;
        target.Earrings = source.Earrings?.ToList();
        target.Wings = source.Wings;
        target.WeaponType = source.WeaponType;
        target.EquipmentTags = source.EquipmentTags?.ToList() ?? new();
        target.Initialize();

        SpriteRenderer[] sourceRenderers = source.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer renderer in target.GetComponentsInChildren<SpriteRenderer>(true))
        {
            SpriteRenderer original = System.Array.Find(sourceRenderers, candidate =>
                candidate.name == renderer.name && candidate.transform.parent.name == renderer.transform.parent.name);
            if (original != null) { renderer.color = original.color; }
        }
    }

    /// <summary>
    /// 장비 데이터에 따른 외형 적용, 빈 슬롯은 기존 외형 해제
    /// 인벤토리와 능력치 변경 제외
    /// </summary>
    public void ApplyEquipmentAppearance(EquipSlot slot, ItemData itemData)
    {


        // 설정 에셋인 _mappingConfig를 사용할 경우와 로컬 딕셔너리 하이브리드 지원
        if (itemData == null || string.IsNullOrEmpty(itemData.SpriteId))
        {
            switch (slot)
            {
                case EquipSlot.Weapon: _character4D.UnEquip(EquipmentPart.MeleeWeapon1H); break;
                case EquipSlot.Helmet: _character4D.UnEquip(EquipmentPart.Helmet); break;
                case EquipSlot.Armor: _character4D.UnEquip(EquipmentPart.Armor); break;
            }
            return;
        }

        ItemSprite itemSprite = null;
        var sc = _character4D.SpriteCollection;

        switch (slot)
        {
            case EquipSlot.Weapon:
                itemSprite = sc.MeleeWeapon1H?.FirstOrDefault(i => i.Id == itemData.SpriteId);
                if (itemSprite != null) _character4D.Equip(itemSprite, EquipmentPart.MeleeWeapon1H);
                break;
            case EquipSlot.Helmet:
                itemSprite = sc.Armor?.FirstOrDefault(i => i.Id == itemData.SpriteId);
                if (itemSprite != null)
                {
                    _character4D.Equip(itemSprite, EquipmentPart.Helmet);
                }
                break;

            case EquipSlot.Armor:
                itemSprite = sc.Armor?.FirstOrDefault(i => i.Id == itemData.SpriteId);
                if (itemSprite != null) _character4D.Equip(itemSprite, EquipmentPart.Armor);
                break;
        }
    }

}
