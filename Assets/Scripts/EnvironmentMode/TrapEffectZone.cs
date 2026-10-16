using UnityEngine;
using Run.Player;

namespace Run.EnvironmentMode
{
    /// <summary>What a non-lethal trap does to the runner for as long as they're standing in it.</summary>
    public enum TrapEffectKind
    {
        /// <summary>Cuts run speed (Mud).</summary>
        Slow,

        /// <summary>Heavier gravity, so jumps fall short (Tar).</summary>
        Heavy
    }

    /// <summary>
    /// A non-lethal trap's gameplay effect: refreshes a short timed debuff on
    /// <see cref="PlayerController"/> every physics step the runner overlaps it, then lets it
    /// expire on its own, rather than tracking the exact moment they leave.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TrapEffectZone : MonoBehaviour
    {
        // Long enough to still be in effect for a jump taken shortly after leaving the puddle,
        // short enough to read as "you're still feeling that" rather than a lingering power-up.
        private const float LingerDuration = 0.6f;

        public TrapEffectKind Kind;

        private void Reset()
        {
            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
            {
                collider2d.isTrigger = true;
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            var player = other.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            switch (Kind)
            {
                case TrapEffectKind.Slow:
                    player.ApplySlow(LingerDuration);
                    break;

                case TrapEffectKind.Heavy:
                    player.ApplyHeavy(LingerDuration);
                    break;
            }
        }
    }
}
