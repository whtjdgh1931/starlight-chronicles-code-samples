using System;
using System.Collections;
using Assets.HeroEditor4D.Common.Scripts.Enums;
using UnityEngine;

/// <summary>이동 상태 전환의 접지 그림자·먼지·착지 연출</summary>
internal sealed class PlayerLocomotionVisual
{
    private const float LandingSettleStart = 0.42f;
    private const float LandingReboundRatio = 0.25f;
    private const int StartDustCount = 4;

    private MonoBehaviour _coroutineHost;
    private Transform _visualRoot;
    private SpriteRenderer _contactShadow;
    private ParticleSystem _moveDustPrefab;
    private ObjectPool _moveDustPool;
    private ParticleSystem _activeMoveDust;
    private PoolableObject _activeMoveDustPoolable;
    private Coroutine _moveDustRelease;
    private float _landingCompression;
    private float _landingDuration;
    private float _minimumLandingAirTime;
    private Coroutine _landingMotion;
    private Vector3 _restScale;
    private CharacterState _state;
    private float _jumpStartedAt;
    private bool _isVisible;

    /// <summary>시각 루트 기준 크기와 프리팹에 저장된 접지 설정 연결</summary>
    internal void Initialize(MonoBehaviour coroutineHost, Transform visualRoot, SpriteRenderer contactShadow,
        ParticleSystem moveDustPrefab, float landingCompression, float landingDuration, float minimumLandingAirTime)
    {
        if (moveDustPrefab != null && contactShadow == null)
        {
            throw new MissingReferenceException($"{coroutineHost.name}: 이동 먼지의 발밑 기준으로 접지 그림자 필요");
        }

        _coroutineHost = coroutineHost;
        _visualRoot = visualRoot;
        _contactShadow = contactShadow;
        _moveDustPrefab = moveDustPrefab;
        _landingCompression = landingCompression;
        _landingDuration = landingDuration;
        _minimumLandingAirTime = minimumLandingAirTime;
        _restScale = visualRoot.localScale;
        _state = CharacterState.Idle;
        _jumpStartedAt = 0f;
        _isVisible = true;
    }

    internal void SetObjectPool(ObjectPool objectPool)
    {
        if (objectPool == null) { throw new ArgumentNullException(nameof(objectPool)); }
        if (_moveDustPool != objectPool) { StopMoveDust(); }
        _moveDustPool = objectPool;
    }

    /// <summary>이전 상태에서 다음 상태로 넘어갈 때의 연출 선택</summary>
    internal void SetState(CharacterState state, bool isDashing)
    {
        CharacterState previousState = _state;
        bool grounded = IsGroundedState(state);
        if (state == CharacterState.Jump && previousState != CharacterState.Jump)
        {
            _jumpStartedAt = Time.time;
        }
        if (!grounded)
        {
            StopLandingMotion();
        }

        _state = state;
        SetContactShadow(grounded && _isVisible);
        if (state != CharacterState.Run || isDashing)
        {
            StopMoveDust();
        }

        if (!_isVisible || !_coroutineHost.isActiveAndEnabled || isDashing)
        {
            return;
        }

        bool landed = previousState == CharacterState.Jump && grounded && Time.time - _jumpStartedAt >= _minimumLandingAirTime;
        if (landed && _visualRoot.gameObject.activeInHierarchy)
        {
            StopLandingMotion();
            _landingMotion = _coroutineHost.StartCoroutine(CoLandingMotion());
        }
        else if (previousState == CharacterState.Idle && state == CharacterState.Run)
        {
            PlayMoveDust();
        }
    }

    internal void SetVisible(bool isVisible)
    {
        _isVisible = isVisible;
        if (!isVisible)
        {
            StopLandingMotion();
            StopMoveDust();
        }

        SetContactShadow(isVisible && IsGroundedState(_state));
    }

    /// <summary>비활성화·재초기화 시 착지 크기 복원과 접지 연출 정리</summary>
    internal void Clear()
    {
        StopLandingMotion();
        StopMoveDust();
        SetContactShadow(false);
    }

    internal void StopLandingMotion()
    {
        if (_landingMotion == null)
        {
            return;
        }

        _coroutineHost.StopCoroutine(_landingMotion);
        _landingMotion = null;
        if (_visualRoot != null) { _visualRoot.localScale = _restScale; }
    }

    private static bool IsGroundedState(CharacterState state) => state == CharacterState.Idle || state == CharacterState.Run;

    private void SetContactShadow(bool visible)
    {
        // 이전 씬과 외형 미리보기에서는 생략 가능한 접지 장식
        if (_contactShadow != null) { _contactShadow.enabled = visible; }
    }

    private IEnumerator CoLandingMotion()
    {
        float elapsed = 0f;
        while (elapsed < _landingDuration)
        {
            float progress = Mathf.Clamp01(elapsed / _landingDuration);
            float compression = progress < LandingSettleStart
                ? Mathf.Lerp(_landingCompression, 0f, Mathf.SmoothStep(0f, 1f, progress / LandingSettleStart))
                : -_landingCompression * LandingReboundRatio * Mathf.Sin(Mathf.PI * Mathf.InverseLerp(LandingSettleStart, 1f, progress));
            _visualRoot.localScale = Vector3.Scale(_restScale, new Vector3(1f + compression * 0.5f, 1f - compression, 1f));
            elapsed += Time.deltaTime;
            yield return null;
        }

        _visualRoot.localScale = _restScale;
        _landingMotion = null;
    }

    private void PlayMoveDust()
    {
        // 맵 준비 전과 외형 미리보기의 선택 효과 생략
        if (_moveDustPrefab == null || _moveDustPool == null) { return; }

        Vector3 feet = _contactShadow.transform.position;
        GameObject effect = _moveDustPool.Pop(_moveDustPrefab.gameObject);
        effect.transform.SetPositionAndRotation(feet, Quaternion.identity);
        effect.layer = _contactShadow.gameObject.layer;
        var renderer = effect.GetComponent<ParticleSystemRenderer>();
        renderer.sortingLayerID = _contactShadow.sortingLayerID;
        renderer.sortingOrder = _contactShadow.sortingOrder + 1;
        var dust = effect.GetComponent<ParticleSystem>();
        _activeMoveDust = dust;
        _activeMoveDustPoolable = effect.GetComponent<PoolableObject>();
        dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        dust.Play(false);

        for (int i = 0; i < StartDustCount; i++)
        {
            float spread = i / (float)(StartDustCount - 1);
            float horizontal = Mathf.Lerp(-0.55f, 0.55f, spread);
            var particle = new ParticleSystem.EmitParams
            {
                position = feet + new Vector3(Mathf.Lerp(-0.1f, 0.1f, spread), 0.05f, 0f),
                velocity = new Vector3(horizontal, Mathf.Lerp(0.12f, 0.38f, 1f - spread), 0f)
            };
            dust.Emit(particle, 1);
        }
        _moveDustRelease = _coroutineHost.StartCoroutine(CoReleaseMoveDust(dust.main.duration));
    }

    private IEnumerator CoReleaseMoveDust(float delay)
    {
        yield return new WaitForSeconds(delay);
        _moveDustRelease = null;
        StopMoveDust();
    }

    private void StopMoveDust()
    {
        if (_moveDustRelease != null)
        {
            _coroutineHost.StopCoroutine(_moveDustRelease);
            _moveDustRelease = null;
        }

        ParticleSystem dust = _activeMoveDust;
        PoolableObject poolable = _activeMoveDustPoolable;
        _activeMoveDust = null;
        _activeMoveDustPoolable = null;
        if (dust != null && dust.gameObject.activeSelf)
        {
            dust.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            poolable.Release();
        }
    }
}
