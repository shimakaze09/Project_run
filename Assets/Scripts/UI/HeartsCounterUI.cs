using UnityEngine;
using TMPro;
using Run.Player;

namespace Run.UI
{
    /// <summary>Updates a text label to show the player's current heart count.</summary>
    public sealed class HeartsCounterUI : MonoBehaviour
    {
        [SerializeField] private TMP_Text _text;

        private PlayerHealth _health;

        private void Start()
        {
            _health = FindFirstObjectByType<PlayerHealth>();
            if (_health != null)
            {
                _health.HealthChanged += OnHealthChanged;
                OnHealthChanged(_health.CurrentHearts);
            }
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.HealthChanged -= OnHealthChanged;
            }
        }

        private void OnHealthChanged(int current)
        {
            _text.text = $"{current}";
        }
    }
}