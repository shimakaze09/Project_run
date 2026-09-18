using UnityEngine;

namespace Run.Player
{
    /// <summary>
    /// Tracks the player's hearts. A hazard or left-behind hit costs one heart;
    /// the player only actually dies once hearts reach zero.
    /// </summary>
    public sealed class PlayerHealth : MonoBehaviour
    {
        [SerializeField, Min(1)] private int _maxHearts = 3;

        private int _currentHearts;

        public int MaxHearts => _maxHearts;
        public int CurrentHearts => _currentHearts;
        public bool IsDepleted => _currentHearts <= 0;

        /// <summary>Fired whenever hearts change, with the new current heart count.</summary>
        public event System.Action<int> HealthChanged;

        private void Awake()
        {
            _currentHearts = _maxHearts;
        }

        /// <summary>Removes one heart. Returns true if hearts reached zero (player should now die).</summary>
        public bool TakeHit()
        {
            if (_currentHearts <= 0)
            {
                return true;
            }

            _currentHearts = Mathf.Max(0, _currentHearts - 1);
            HealthChanged?.Invoke(_currentHearts);
            return _currentHearts <= 0;
        }

        /// <summary>Resets hearts to full, e.g. at the start of a new run.</summary>
        public void ResetHealth()
        {
            _currentHearts = _maxHearts;
            HealthChanged?.Invoke(_currentHearts);
        }
    }
}