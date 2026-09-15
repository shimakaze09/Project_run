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
}
