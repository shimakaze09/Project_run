namespace Run.Common
{
    /// <summary>
    /// Implemented by components that cache an absolute world X somewhere other than their
    /// transform, so they can correct it when the world is re-based toward the origin
    /// (see the floating-origin logic in the level generator).
    /// </summary>
    public interface IWorldShiftable
    {
        /// <summary>Called after the object's transform has been shifted by <paramref name="dx"/> on X.</summary>
        void OnWorldShift(float dx);
    }
}
