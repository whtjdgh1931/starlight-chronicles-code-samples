using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ParticleSystem))]
public sealed class TitleLeafParticles : MaskableGraphic
{
    [SerializeField] private Texture2D _leafTexture;

    private readonly ParticleSystem.Particle[] _particles = new ParticleSystem.Particle[20];
    private ParticleSystem _system;

    public override Texture mainTexture => _leafTexture;

    protected override void OnEnable()
    {
        base.OnEnable();
        if (Application.isPlaying)
        {
            _system = GetComponent<ParticleSystem>();
            ConfigureBounds();
            _system.Play();
        }
    }

    private void LateUpdate()
    {
        SetVerticesDirty();
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        if (_system != null)
        {
            ConfigureBounds();
        }
    }

    private void ConfigureBounds()
    {
        Rect bounds = rectTransform.rect;
        var shape = _system.shape;
        shape.scale = new Vector3(bounds.width, 1f, 1f);
        shape.position = new Vector3(0f, bounds.yMax + 24f, 0f);

        var velocity = _system.velocityOverLifetime;
        velocity.y = -(bounds.height + 48f) / _system.main.startLifetime.constant;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        if (_system == null)
        {
            return;
        }

        int count = _system.GetParticles(_particles);
        for (int i = 0; i < count; i++)
        {
            ParticleSystem.Particle particle = _particles[i];
            Color32 tint = particle.GetCurrentColor(_system) * color;
            float halfSize = particle.GetCurrentSize(_system) * 0.5f;
            float angle = particle.rotation * Mathf.Deg2Rad;
            var right = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle)) * halfSize;
            var up = new Vector3(-right.y, right.x);
            Vector3 center = particle.position;
            int start = mesh.currentVertCount;
            mesh.AddVert(center - right - up, tint, Vector2.zero);
            mesh.AddVert(center + right - up, tint, Vector2.right);
            mesh.AddVert(center + right + up, tint, Vector2.one);
            mesh.AddVert(center - right + up, tint, Vector2.up);
            mesh.AddTriangle(start, start + 1, start + 2);
            mesh.AddTriangle(start + 2, start + 3, start);
        }
    }
}
