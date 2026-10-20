using UnityEngine;

namespace Run.EnvironmentMode
{
    /// <summary>
    /// Remembers the authored colours of every sprite under a root, so the group can be faded or
    /// tinted (ghost traps, the placement preview) and then restored exactly.
    /// </summary>
    internal sealed class SpriteTint
    {
        private readonly SpriteRenderer[] _renderers;
        private readonly Color[] _baseColors;

        public SpriteTint(GameObject root)
        {
            _renderers = root.GetComponentsInChildren<SpriteRenderer>(true);
            _baseColors = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                _baseColors[i] = _renderers[i].color;
            }
        }

        /// <summary>Blends every sprite <paramref name="amount"/> of the way to <paramref name="tint"/> at the given opacity.</summary>
        public void Set(Color tint, float amount, float alpha)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                Color color = Color.Lerp(_baseColors[i], tint, amount);
                color.a = _baseColors[i].a * alpha;
                _renderers[i].color = color;
            }
        }

        public void Restore()
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].color = _baseColors[i];
            }
        }
    }
}
