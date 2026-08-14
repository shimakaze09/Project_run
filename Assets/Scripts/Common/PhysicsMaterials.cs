using UnityEngine;

namespace Run.Common
{
    /// <summary>
    /// Shared 2D physics materials, created at runtime and cached for the session.
    /// </summary>
    /// <remarks>
    /// Keeping these in code (rather than as project assets) means the gameplay systems
    /// stay self-contained — the scene needs no manual material wiring.
    /// </remarks>
    public static class PhysicsMaterials
    {
        private static PhysicsMaterial2D _frictionless;

        /// <summary>
        /// Zero friction, zero bounce. Applied to the player so it can never cling to a
        /// surface it is pressed against.
        /// </summary>
        /// <remarks>
        /// 2D friction is combined between the two touching colliders as
        /// <c>sqrt(a · b)</c>, so zeroing it on one side forces the pair to zero — the
        /// player alone carries this material and is guaranteed frictionless against
        /// every piece in the world.
        /// </remarks>
        public static PhysicsMaterial2D Frictionless
        {
            get
            {
                if (_frictionless == null)
                {
                    _frictionless = new PhysicsMaterial2D("Frictionless")
                    {
                        friction = 0f,
                        bounciness = 0f,
                        hideFlags = HideFlags.HideAndDontSave
                    };
                }

                return _frictionless;
            }
        }
    }
}
