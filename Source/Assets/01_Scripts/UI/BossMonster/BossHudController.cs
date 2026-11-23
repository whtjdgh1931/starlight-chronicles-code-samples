using UnityEngine;

public class BossHudController : MonoBehaviour
{
    [SerializeField] private BossHudView _view;

    private BossEncounterController _encounterController;
    private BossMonster _boss;
    private bool _isInitialized;

    [SerializeField] private StatusEffectPresenter _statusEffectPresenter;

    /// <summary>
    /// 보스 생성 방식과 무관한 HUD 초기화
    /// 기존 보스와 로컬 생성 이벤트 연결 해제
    /// </summary>
    public void Initialize()
    {
        ValidateReferences();
        Shutdown();
        _statusEffectPresenter.Initialize();
        _view.Hide();
        _isInitialized = true;
    }

    /// <summary>
    /// 기존 로컬 보스 생성 이벤트와 HUD 연결
    /// </summary>
    public void Initialize(BossEncounterController encounterController)
    {
        if (encounterController == null)
        {
            throw new System.ArgumentNullException(nameof(encounterController));
        }

        Initialize();
        _encounterController = encounterController;
        _encounterController.OnBossSpawned += HandleBossSpawned;
    }

    private void ValidateReferences()
    {
        if (_view == null)
        {
            throw new MissingReferenceException($"{name}: BossHudView 참조 필요");
        }

        if (_statusEffectPresenter == null)
        {
            throw new MissingReferenceException($"{name}: _statusEffectPresenter 참조 필요");
        }
    }

    private void Bind(BossMonster boss)
    {
        Unbind();

        _boss = boss;
        _boss.HealthChanged += HandleHealthChanged;
        _boss.OnReturnedToPool += HandleBossReturned;

        _statusEffectPresenter.Bind(_boss.StatusEffects);
        if (_boss.MaxHp > 0f) { HandleHealthChanged(_boss.CurrentHp, _boss.MaxHp); }
    }

    /// <summary>
    /// 초기화된 HUD에 현재 화면의 보스 연결
    /// 보스 데이터 초기화 완료 후 호출, 동일 보스의 중복 연결 제외
    /// </summary>
    public void ShowBoss(BossMonster boss)
    {
        if (!_isInitialized)
        {
            throw new System.InvalidOperationException($"{name}: 보스 HUD 초기화 필요");
        }

        if (_boss == boss)
        {
            return;
        }

        Bind(boss);
    }

    /// <summary>
    /// 제거된 보스와 일치하는 HUD 연결 해제
    /// 이전 보스의 제거 알림으로 새 보스 화면이 닫히는 상황 방지
    /// </summary>
    public void HideBoss(BossMonster boss)
    {
        if (_boss == boss)
        {
            Unbind();
        }
    }

    private void Unbind()
    {
        if (_boss != null)
        {
            _boss.HealthChanged -= HandleHealthChanged;
            _boss.OnReturnedToPool -= HandleBossReturned;

            _boss = null;
        }

        _statusEffectPresenter.Unbind();
        _view.Hide();
    }

    private void HandleBossSpawned(BossMonster boss)
    {
        ShowBoss(boss);
    }

    private void HandleBossReturned(MonsterBase monster)
    {
        Unbind();
    }

    private void HandleHealthChanged(float currentHp, float maxHp)
    {
        if (_view.gameObject.activeSelf)
        {
            _view.SetHealth(currentHp, maxHp);
            return;
        }
        _view.Show(currentHp, maxHp, _boss.BossData.PhaseChangeHpRatio);
    }

    /// <summary>
    /// 보스 화면과 선택적으로 연결된 로컬 생성 이벤트 해제
    /// </summary>
    public void Shutdown()
    {
        Unbind();

        if (_encounterController != null)
        {
            _encounterController.OnBossSpawned -= HandleBossSpawned;
            _encounterController = null;
        }

        _isInitialized = false;
    }

}
