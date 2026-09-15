using UnityEngine;
using Run.Common;

namespace Run.Effects
{
    public enum BurstKind { Dust, Jump, Land, Coin, Death, Start, Milestone, Spark, Ember, Mote }

    /// <summary>One bounded pool of lightweight sprite particles; never allocates per burst.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class GameFeel : MonoBehaviour
    {
        public static GameFeel Instance { get; private set; }
        [SerializeField, Range(32, 512)] private int _capacity = 256;
        [SerializeField, Range(0f, 1f)] private float _intensity = 1f;
        private Particle[] _particles;
        private int _cursor;
        private Sprite _round;
        private Sprite _diamond;
        private readonly System.Random _random = new System.Random(90210);
        public int TotalEmitted { get; private set; }
        public int Capacity => _particles == null ? 0 : _particles.Length;
        public int ActiveCount { get; private set; }

        private struct Particle
        {
            public SpriteRenderer Renderer;
            public Vector3 Velocity;
            public Color Color;
            public float Life, Duration, Size, Spin, Gravity;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            _round = Shapes.Circle;
            _diamond = Shapes.Square;
            _particles = new Particle[Mathf.Clamp(_capacity, 32, 512)];
            for (int i = 0; i < _particles.Length; i++)
            {
                var sprite = new GameObject("FX " + i).AddComponent<SpriteRenderer>();
                sprite.transform.SetParent(transform, false);
                sprite.sprite = _round;
                sprite.sortingOrder = 30;
                sprite.enabled = false;
                _particles[i].Renderer = sprite;
            }
        }

        public void Emit(BurstKind kind, Vector3 position)
        {
            if (_particles == null || _intensity <= 0f) return;
            int count = 1;
            float speed = 1f, life = 0.55f, size = 0.12f, gravity = 0f;
            Color color = new Color(0.54f, 0.95f, 0.9f);
            switch (kind)
            {
                case BurstKind.Dust: count = 2; speed = 0.8f; color = new Color(0.52f, 0.8f, 0.72f, 0.5f); break;
                case BurstKind.Jump: count = 12; speed = 2.3f; break;
                case BurstKind.Land: count = 16; speed = 2.7f; color = new Color(0.5f, 0.85f, 0.75f, 0.7f); break;
                case BurstKind.Coin: count = 18; speed = 3f; life = 0.7f; color = new Color(1f, 0.8f, 0.28f); break;
                case BurstKind.Death: count = 38; speed = 4.5f; life = 1f; gravity = 3f; color = new Color(1f, 0.36f, 0.48f); break;
                case BurstKind.Start: count = 20; speed = 3f; life = 0.8f; break;
                case BurstKind.Milestone: count = 42; speed = 4f; life = 1.1f; gravity = 2f; color = new Color(1f, 0.85f, 0.4f); break;
                case BurstKind.Spark: color = new Color(1f, 0.9f, 0.5f); size = 0.09f; break;
                case BurstKind.Ember: color = new Color(1f, 0.4f, 0.55f); life = 0.8f; size = 0.1f; break;
                case BurstKind.Mote: color = new Color(0.65f, 1f, 0.86f, 0.45f); life = 2.5f; size = 0.06f; speed = 0.3f; break;
            }
            count = Mathf.Max(1, Mathf.RoundToInt(count * _intensity));
            for (int i = 0; i < count; i++)
            {
                int index = _cursor;
                _cursor = (_cursor + 1) % _particles.Length;
                ref Particle particle = ref _particles[index];
                if (particle.Life <= 0f) ActiveCount++;
                float angle = Range(0f, Mathf.PI * 2f);
                float velocity = speed * Range(0.3f, 1f);
                particle.Velocity = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * velocity;
                if (kind == BurstKind.Dust || kind == BurstKind.Land || kind == BurstKind.Jump)
                    particle.Velocity = new Vector3(particle.Velocity.x - 0.8f, Mathf.Abs(particle.Velocity.y) * 0.35f, 0f);
                if (kind == BurstKind.Spark || kind == BurstKind.Ember || kind == BurstKind.Mote)
                    particle.Velocity.y = Mathf.Abs(particle.Velocity.y) + 0.2f;
                particle.Life = particle.Duration = life * Range(0.7f, 1.2f);
                particle.Size = size * Range(0.6f, 1.5f);
                particle.Spin = Range(-200f, 200f);
                particle.Gravity = gravity;
                particle.Color = color;
                var renderer = particle.Renderer;
                renderer.sprite = kind == BurstKind.Coin || kind == BurstKind.Milestone || kind == BurstKind.Spark
                    ? _diamond : _round;
                renderer.transform.position = position + new Vector3(Range(-0.15f, 0.15f), Range(0f, 0.15f), 0f);
                renderer.transform.localScale = Vector3.one * particle.Size;
                renderer.transform.rotation = Quaternion.Euler(0f, 0f, Range(0f, 180f));
                renderer.color = color;
                renderer.enabled = true;
                TotalEmitted++;
            }
        }

        private float Range(float min, float max) => min + (float)_random.NextDouble() * (max - min);
        private void LateUpdate() => Simulate(Time.deltaTime);

        public void Simulate(float deltaTime)
        {
            if (_particles == null || deltaTime <= 0f) return;
            for (int i = 0; i < _particles.Length; i++)
            {
                ref Particle p = ref _particles[i];
                if (p.Life <= 0f) continue;
                p.Life -= deltaTime;
                if (p.Life <= 0f) { p.Renderer.enabled = false; ActiveCount--; continue; }
                p.Velocity.y -= p.Gravity * deltaTime;
                p.Renderer.transform.position += p.Velocity * deltaTime;
                p.Renderer.transform.Rotate(0f, 0f, p.Spin * deltaTime);
                float remaining = p.Life / p.Duration;
                Color color = p.Color; color.a *= remaining;
                p.Renderer.color = color;
                p.Renderer.transform.localScale = Vector3.one * p.Size * (0.35f + remaining * 0.65f);
            }
        }

        public void Shift(float dx)
        {
            if (_particles == null) return;
            foreach (var particle in _particles)
                if (particle.Life > 0f) particle.Renderer.transform.position += new Vector3(dx, 0f, 0f);
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
