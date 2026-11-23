using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>맵 하늘의 구름과 새 등장, 비행, 풀 반환</summary>
public sealed class SkyAmbientSpawner : MonoBehaviour
{
    [Serializable]
    private struct FlightSettings
    {
        public SpriteRenderer[] Prefabs;
        public Vector2 SpeedRange;
        public Vector2 IntervalRange;
        [Min(1)] public int MaxActive;
        public bool FlipWithDirection;
    }

    private readonly struct Flight
    {
        public readonly SpriteRenderer Renderer;
        public readonly float Speed;

        public Flight(SpriteRenderer renderer, float speed)
        {
            Renderer = renderer;
            Speed = speed;
        }
    }

    [SerializeField] private Rect _flightBounds = new(-12f, 2f, 24f, 4f);
    [SerializeField] private FlightSettings _clouds;
    [SerializeField] private FlightSettings _birds;
    [SerializeField, Min(0)] private int _initialCloudCount = 2;

    private readonly List<Flight> _activeClouds = new();
    private readonly List<Flight> _activeBirds = new();
    private ObjectPool _pool;
    private float _nextCloud;
    private float _nextBird;

    /// <summary>준비된 맵 풀 연결과 초기 하늘 채우기</summary>
    public void Initialize(ObjectPool pool)
    {
        if (pool == null) { throw new ArgumentNullException(nameof(pool)); }
        if (_pool == pool) { return; }
        ValidateSettings(_clouds);
        ValidateSettings(_birds);
        if (_flightBounds.width <= 0f || _flightBounds.height <= 0f)
        {
            throw new InvalidOperationException($"{name}: 양수 크기의 비행 범위 필요");
        }
        ReturnFlights(_activeClouds);
        ReturnFlights(_activeBirds);
        _pool = pool;
        if (isActiveAndEnabled) { BeginFlights(); }
    }

    private void ValidateSettings(FlightSettings settings)
    {
        if (settings.Prefabs == null || settings.Prefabs.Length == 0 || settings.MaxActive < 1 ||
            settings.SpeedRange.x <= 0f || settings.SpeedRange.y < settings.SpeedRange.x ||
            settings.IntervalRange.x <= 0f || settings.IntervalRange.y < settings.IntervalRange.x)
        {
            throw new InvalidOperationException($"{name}: 하늘 프리팹과 비행 설정 필요");
        }
        foreach (SpriteRenderer prefab in settings.Prefabs)
        {
            if (prefab == null || prefab.sprite == null)
            {
                throw new MissingReferenceException($"{name}: 하늘 스프라이트 참조 필요");
            }
        }
    }

    private void OnEnable()
    {
        if (_pool != null) { BeginFlights(); }
    }

    private void BeginFlights()
    {
        int count = Mathf.Min(_initialCloudCount, _clouds.MaxActive);
        for (int i = 0; i < count; i++)
        {
            float fraction = (i + 1f) / (count + 1f);
            _activeClouds.Add(Spawn(_clouds, Mathf.Lerp(_flightBounds.xMin, _flightBounds.xMax, fraction)));
        }
        _nextCloud = Time.time + NextInterval(_clouds);
        _nextBird = Time.time + NextInterval(_birds);
    }

    private void LateUpdate()
    {
        if (_pool == null) { return; }
        UpdateFlights(_clouds, _activeClouds, ref _nextCloud);
        UpdateFlights(_birds, _activeBirds, ref _nextBird);
    }

    private void UpdateFlights(FlightSettings settings, List<Flight> active, ref float nextSpawn)
    {
        for (int index = active.Count - 1; index >= 0; index--)
        {
            Flight flight = active[index];
            flight.Renderer.transform.position += transform.right * (flight.Speed * Time.deltaTime);
            Bounds bounds = flight.Renderer.bounds;
            bool movingRight = flight.Speed > 0f;
            float edge = transform.InverseTransformPoint(movingRight ? bounds.min : bounds.max).x;
            if (movingRight ? edge >= _flightBounds.xMax : edge <= _flightBounds.xMin)
            {
                active.RemoveAt(index);
                _pool.Push(flight.Renderer.gameObject);
            }
        }
        if (active.Count >= settings.MaxActive || Time.time < nextSpawn) { return; }

        active.Add(Spawn(settings));
        nextSpawn = Time.time + NextInterval(settings);
    }

    private Flight Spawn(FlightSettings settings, float? initialX = null)
    {
        SpriteRenderer prefab = settings.Prefabs[Random.Range(0, settings.Prefabs.Length)];
        GameObject instance = _pool.Pop(prefab.gameObject);
        var renderer = instance.GetComponent<SpriteRenderer>();
        Transform visual = instance.transform;
        visual.rotation = transform.rotation * prefab.transform.localRotation;
        visual.localScale = prefab.transform.localScale;
        if (instance.TryGetComponent(out Animator animator))
        {
            animator.Rebind();
            animator.Update(0f);
        }
        bool movingRight = Random.value < 0.5f;
        renderer.flipX = settings.FlipWithDirection ? !movingRight : prefab.flipX;

        float y = Random.Range(_flightBounds.yMin, _flightBounds.yMax);
        visual.position = transform.TransformPoint(new Vector3(0f, y, 0f));
        Bounds bounds = renderer.bounds;
        float edge = transform.InverseTransformPoint(movingRight ? bounds.max : bounds.min).x;
        float entry = movingRight ? _flightBounds.xMin : _flightBounds.xMax;
        visual.position = transform.TransformPoint(new Vector3(initialX ?? (entry - edge), y, 0f));

        float speed = Random.Range(settings.SpeedRange.x, settings.SpeedRange.y);
        return new Flight(renderer, movingRight ? speed : -speed);
    }

    private static float NextInterval(FlightSettings settings) => Random.Range(settings.IntervalRange.x, settings.IntervalRange.y);

    private void OnDisable()
    {
        ReturnFlights(_activeClouds);
        ReturnFlights(_activeBirds);
    }

    private void ReturnFlights(List<Flight> active)
    {
        if (_pool != null && _pool.gameObject.activeInHierarchy)
        {
            foreach (Flight flight in active)
            {
                if (flight.Renderer != null) { _pool.Push(flight.Renderer.gameObject); }
            }
        }
        active.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Matrix4x4 previous = Gizmos.matrix;
        Color color = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(_flightBounds.center, _flightBounds.size);
        Gizmos.matrix = previous;
        Gizmos.color = color;
    }
}
