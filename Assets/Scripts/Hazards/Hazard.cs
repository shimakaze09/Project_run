using UnityEngine;
using Run.Core;
using Run.Player;

namespace Run.Hazards
{
    /// <summary>
    /// Marks a trigger collider as lethal: any player that touches it dies.
    /// </summary>
    /// <remarks>
    /// This is the common base for every damaging obstacle (spikes, enemies, saws…).
    /// Keeping the "kill on contact" rule in one place means new hazard shapes only
    /// need their own visuals/movement and can inherit the lethal behaviour, or simply
    /// attach this component as-is. The collider is forced to a trigger so hazards are
    /// felt but never physically block the runner.
    /// </remarks>
    [RequireComponent(typeof(Collider2D))]
    public class Hazard : MonoBehaviour
    {
        [Tooltip("Reported cause of death when this hazard kills the player.")]
        [SerializeField] protected DeathCause _cause = DeathCause.Hazard;

        protected virtual void Reset()
        {
            // Sensible default when the component is added in the editor.
            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
            {
                collider2d.isTrigger = true;
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            var player = other.GetComponent<PlayerController>();
            if (player != null)
            {
                player.Kill(_cause);
            }
        }
    }
}
