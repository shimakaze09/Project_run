using System.Collections.Generic;
using UnityEngine;
using Run.Collectibles;
using Run.Core;
using Run.Hazards;

namespace Run.Generation
{
    /// <summary>An Inspector-authored level layout. Children are real prefab objects, not code recipes.</summary>
    public sealed class LevelChunk : MonoBehaviour
    {
        // Same High-Contrast pair TrapFactory uses, so both modes speak one visual language. Set
        // here rather than relying on the colours VisualArtSetup bakes into these prefabs because
        // that bake is an Editor-time tool - it can't react to a Settings toggle at runtime.
        // Hazard isn't the coins' warm gold (read as "same colour" at a glance); ground gets a
        // separate, lighter edge shade too (its "Moss edge" trim below) so a run of ground reads
        // as a surface rather than one feature-less block.
        private static readonly Color SafeHC = new Color(0.2f, 0.42f, 0.58f);
        private static readonly Color SafeEdgeHC = new Color(0.86f, 0.95f, 1f);
        private static readonly Color HazardHC = new Color(1f, 0.15f, 0.45f);

        // Re-rolled every placement (including a pooled reuse), so a power-up reads as an
        // occasional find rather than something every carrying chunk guarantees.
        private const float PowerUpSpawnChance = 0.3f;

        [Min(1f)] public float Width = 10f;
        public bool SafeStart;
        [Min(0f)] public float StartWeight = 1f;
        [Min(0f)] public float EndWeight = 1f;
        [Min(0f)] public float RequiredJumpSpan;
        [Min(0f)] public float RequiredJumpHeight;

        private Coin[] _coins;
        private PowerUpPickup[] _powerUps;
        private PatrolEnemy[] _enemies;
        private Vector3[] _enemyPositions;
        private readonly List<IChunkAttachment> _attachments = new List<IChunkAttachment>();
        private SpriteRenderer[] _hazardRenderers;
        private Color[] _hazardBaseColors;
        private SpriteRenderer[] _groundRenderers;
        private Color[] _groundBaseColors;
        private SpriteRenderer[] _groundEdgeRenderers;
        private Color[] _groundEdgeBaseColors;
        public LevelChunk Source { get; set; }
        public float EndX => transform.position.x + Width;

        /// <summary>How many runtime attachments (e.g. placed traps) this chunk carries.</summary>
        public int AttachmentCount => _attachments.Count;

        public void Attach(IChunkAttachment attachment)
        {
            if (attachment != null && !_attachments.Contains(attachment)) _attachments.Add(attachment);
        }

        public void Detach(IChunkAttachment attachment) => _attachments.Remove(attachment);

        public void Place(float x)
        {
            if (_coins == null)
            {
                _coins = GetComponentsInChildren<Coin>(true);
                _powerUps = GetComponentsInChildren<PowerUpPickup>(true);
                _enemies = GetComponentsInChildren<PatrolEnemy>(true);
                _enemyPositions = new Vector3[_enemies.Length];
                for (int i = 0; i < _enemies.Length; i++)
                    _enemyPositions[i] = _enemies[i].transform.localPosition;
                CacheContrastGroups();
            }
            transform.position = new Vector3(x, 0f, 0f);
            gameObject.SetActive(true);
            foreach (var coin in _coins)
            {
                coin.gameObject.SetActive(true);
                coin.Prime();
            }
            // No Prime() needed here, unlike Coin: PowerUpPickup re-reads its own world position
            // fresh in OnEnable (fired by this SetActive), so there's no stale cached state to
            // reset - just turning it back on is enough to get a correct bob origin.
            foreach (var powerUp in _powerUps)
            {
                powerUp.gameObject.SetActive(Random.value < PowerUpSpawnChance);
            }
            for (int i = 0; i < _enemies.Length; i++)
            {
                _enemies[i].transform.localPosition = _enemyPositions[i];
                _enemies[i].ResetPatrol();
            }
            ApplyContrast();
        }

        private void OnEnable() => AccessibilityPreferences.Changed += ApplyContrast;

        private void OnDisable() => AccessibilityPreferences.Changed -= ApplyContrast;

        /// <summary>
        /// Groups every baked sprite by whether its piece is lethal (has a <see cref="Hazard"/>,
        /// which <see cref="PatrolEnemy"/> also counts as) or standable ground/platform/crate -
        /// the same split <c>VisualArtSetup</c> uses to decide how to paint each piece originally.
        /// Coins and power-ups are left alone either way; they're pickups, not level geometry to
        /// tell apart from a hazard.
        /// </summary>
        private void CacheContrastGroups()
        {
            var hazards = new List<SpriteRenderer>();
            var ground = new List<SpriteRenderer>();
            var groundEdge = new List<SpriteRenderer>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var piece = transform.GetChild(i);
                if (piece.GetComponent<Coin>() != null || piece.GetComponent<PowerUpPickup>() != null ||
                    piece.GetComponent<SpriteRenderer>() == null)
                {
                    continue;
                }

                if (piece.GetComponent<Hazard>() != null)
                {
                    hazards.AddRange(piece.GetComponentsInChildren<SpriteRenderer>(true));
                    continue;
                }

                foreach (var sr in piece.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    (sr.name == "Moss edge" ? groundEdge : ground).Add(sr);
                }
            }

            _hazardRenderers = hazards.ToArray();
            _hazardBaseColors = new Color[_hazardRenderers.Length];
            for (int i = 0; i < _hazardRenderers.Length; i++) _hazardBaseColors[i] = _hazardRenderers[i].color;

            _groundRenderers = ground.ToArray();
            _groundBaseColors = new Color[_groundRenderers.Length];
            for (int i = 0; i < _groundRenderers.Length; i++) _groundBaseColors[i] = _groundRenderers[i].color;

            _groundEdgeRenderers = groundEdge.ToArray();
            _groundEdgeBaseColors = new Color[_groundEdgeRenderers.Length];
            for (int i = 0; i < _groundEdgeRenderers.Length; i++) _groundEdgeBaseColors[i] = _groundEdgeRenderers[i].color;
        }

        private void ApplyContrast()
        {
            if (_hazardRenderers == null)
            {
                return;
            }

            bool hc = AccessibilityPreferences.HighContrast;
            for (int i = 0; i < _hazardRenderers.Length; i++) _hazardRenderers[i].color = hc ? HazardHC : _hazardBaseColors[i];
            for (int i = 0; i < _groundRenderers.Length; i++) _groundRenderers[i].color = hc ? SafeHC : _groundBaseColors[i];
            for (int i = 0; i < _groundEdgeRenderers.Length; i++) _groundEdgeRenderers[i].color = hc ? SafeEdgeHC : _groundEdgeBaseColors[i];
        }

        public void Shift(float dx)
        {
            transform.position += new Vector3(dx, 0f, 0f);
            // Coin bob origins are local to this root; only enemy bounds are world-space.
            foreach (var enemy in _enemies) enemy.OnWorldShift(dx);
            foreach (var attachment in _attachments) attachment.OnWorldShift(dx);
        }

        public void Recycle()
        {
            foreach (var coin in _coins) coin.RestoreBobOrigin();
            // Iterate a copy: attachments usually detach themselves while cleaning up.
            foreach (var attachment in _attachments.ToArray()) attachment.OnChunkRecycled();
            _attachments.Clear();
            gameObject.SetActive(false);
        }
    }
}
