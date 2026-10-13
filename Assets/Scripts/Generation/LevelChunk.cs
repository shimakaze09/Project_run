using System.Collections.Generic;
using UnityEngine;
using Run.Collectibles;
using Run.Hazards;

namespace Run.Generation
{
    /// <summary>An Inspector-authored level layout. Children are real prefab objects, not code recipes.</summary>
    public sealed class LevelChunk : MonoBehaviour
    {
        [Min(1f)] public float Width = 10f;
        public bool SafeStart;
        [Min(0f)] public float StartWeight = 1f;
        [Min(0f)] public float EndWeight = 1f;
        [Min(0f)] public float RequiredJumpSpan;
        [Min(0f)] public float RequiredJumpHeight;

        private Coin[] _coins;
        private PatrolEnemy[] _enemies;
        private Vector3[] _enemyPositions;
        private readonly List<IChunkAttachment> _attachments = new List<IChunkAttachment>();
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
                _enemies = GetComponentsInChildren<PatrolEnemy>(true);
                _enemyPositions = new Vector3[_enemies.Length];
                for (int i = 0; i < _enemies.Length; i++)
                    _enemyPositions[i] = _enemies[i].transform.localPosition;
            }
            transform.position = new Vector3(x, 0f, 0f);
            gameObject.SetActive(true);
            foreach (var coin in _coins)
            {
                coin.gameObject.SetActive(true);
                coin.Prime();
            }
            for (int i = 0; i < _enemies.Length; i++)
            {
                _enemies[i].transform.localPosition = _enemyPositions[i];
                _enemies[i].ResetPatrol();
            }
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
