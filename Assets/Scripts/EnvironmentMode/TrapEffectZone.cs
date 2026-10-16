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
    /// A non-lethal trap's gameplay effect: debuffs the runner while they overlap it and reverts
    /// the moment they leave, rather than running on a fixed duration like a power-up. Combined
    /// with <see cref="Run.Hazards.Hazard"/>-based traps this is what lets a placement read as a
    /// combo - e.g. Mud right before Spikes narrows the runner's safe jump window instead of just
    /// slowing it down in open ground.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public sealed class TrapEffectZone : MonoBehaviour
    {
        public TrapEffectKind Kind;

        // Tracked rather than trusting Enter/Exit alone: disabling this trap's collider (ghost
        // phase, pooling) while the runner is standing inside it isn't guaranteed to fire
        // OnTriggerExit2D, which would otherwise leave the debuff stuck on permanently.
        private PlayerController _overlapping;

        private void Reset()
        {
            var collider2d = GetComponent<Collider2D>();
            if (collider2d != null)
            {
                collider2d.isTrigger = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_overlapping != null || !other.CompareTag("Player"))
            {
                return;
            }

            var player = other.GetComponent<PlayerController>();
            if (player == null)
            {
                return;
            }

            _overlapping = player;
            Apply(add: true);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (_overlapping == null || !other.CompareTag("Player"))
            {
                return;
            }

            Apply(add: false);
            _overlapping = null;
        }

        private void OnDisable()
        {
            if (_overlapping == null)
            {
                return;
            }

            Apply(add: false);
            _overlapping = null;
        }

        private void Apply(bool add)
        {
            switch (Kind)
            {
                case TrapEffectKind.Slow:
                    if (add) _overlapping.AddSlowZone();
                    else _overlapping.RemoveSlowZone();
                    break;

                case TrapEffectKind.Heavy:
                    if (add) _overlapping.AddHeavyZone();
                    else _overlapping.RemoveHeavyZone();
                    break;
            }
        }
    }
}
