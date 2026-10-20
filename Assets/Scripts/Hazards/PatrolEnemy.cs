using UnityEngine;
using Run.Common;
using Run.Core;

namespace Run.Hazards
{
    /// <summary>
    /// A ground enemy that paces back and forth between two X bounds, à la Goomba.
    /// Inherits the lethal-on-contact behaviour from <see cref="Hazard"/>.
    /// </summary>
    /// <remarks>
    /// Movement is purely script-driven (the collider stays a trigger) so the enemy
    /// glides along a fixed line without needing its own physics body. The generator
    /// calls <see cref="Configure"/> after placement to set the patrol range, and the
    /// enemy only moves while a run is actually in progress.
    /// </remarks>
    public sealed class PatrolEnemy : Hazard, IWorldShiftable
    {
        [Header("Patrol")]
        [SerializeField, Min(0f)] private float _speed = 2.5f;

        [SerializeField] private float _patrolMinLocalX;
        [SerializeField] private float _patrolMaxLocalX;
        private float _minX;
        private float _maxX;
        private int _direction = -1;    // start heading left, toward the incoming player
        private bool _configured;

        /// <summary>Sets the patrol range (and optional speed) for this enemy instance.</summary>
        public void Configure(float minX, float maxX, float speed = -1f)
        {
            _patrolMinLocalX = Mathf.Min(minX, maxX) - transform.position.x;
            _patrolMaxLocalX = Mathf.Max(minX, maxX) - transform.position.x;
            _minX = Mathf.Min(minX, maxX);
            _maxX = Mathf.Max(minX, maxX);
            if (speed > 0f)
            {
                _speed = speed;
            }

            _direction = -1;
            _configured = true;
        }

        /// <summary>A snapshot of this enemy's back-and-forth movement, for predicting where it will be.</summary>
        public PatrolMotion Motion => new PatrolMotion
        {
            X = transform.position.x,
            MinX = _minX,
            MaxX = _maxX,
            Speed = _configured ? _speed : 0f,
            Direction = _direction
        };

        /// <summary>Keeps the cached patrol bounds aligned after a floating-origin shift.</summary>
        public void OnWorldShift(float dx)
        {
            _minX += dx;
            _maxX += dx;
        }

        public void ResetPatrol()
        {
            _minX = transform.position.x + _patrolMinLocalX;
            _maxX = transform.position.x + _patrolMaxLocalX;
            _direction = -1;
            _configured = true;
        }

        private void Update()
        {
            if (!_configured)
            {
                return;
            }

            var game = GameManager.Instance;
            if (game == null || game.State != GameState.Playing)
            {
                return;
            }

            Vector3 position = transform.position;
            position.x += _direction * _speed * Time.deltaTime;

            if (position.x <= _minX)
            {
                position.x = _minX;
                _direction = 1;
            }
            else if (position.x >= _maxX)
            {
                position.x = _maxX;
                _direction = -1;
            }

            transform.position = position;
        }
    }

    /// <summary>
    /// The pacing movement of a <see cref="PatrolEnemy"/> as plain numbers, so its future position
    /// can be predicted without stepping the game (used by the computer runner's jump planner).
    /// </summary>
    public struct PatrolMotion
    {
        public float X;
        public float MinX;
        public float MaxX;
        public float Speed;
        public int Direction;

        /// <summary>X after <paramref name="seconds"/> more of pacing, matching <see cref="PatrolEnemy"/>'s Update.</summary>
        public float PredictX(float seconds)
        {
            float span = MaxX - MinX;
            if (span <= 0f || Speed <= 0f)
            {
                return X;
            }

            // Unfold the back-and-forth into one loop of length 2·span: the first half walks
            // right from MinX, the second half walks back left from MaxX.
            float along = Mathf.Clamp(X, MinX, MaxX) - MinX;
            float start = Direction > 0 ? along : 2f * span - along;
            float travelled = Mathf.Repeat(start + Speed * seconds, 2f * span);
            return travelled <= span ? MinX + travelled : MaxX - (travelled - span);
        }
    }
}
