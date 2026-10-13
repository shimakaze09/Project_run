using Run.Common;

namespace Run.Generation
{
    /// <summary>
    /// Something added to a <see cref="LevelChunk"/> while the game runs (not part of the authored
    /// prefab), such as a trap placed by the environment player.
    /// </summary>
    /// <remarks>
    /// Attachments are children of the chunk, so they ride along with its transform. The chunk
    /// also forwards floating-origin shifts (for any cached world X) and tells attachments when
    /// it is recycled, so a pooled chunk never comes back with last lap's traps still on it.
    /// </remarks>
    public interface IChunkAttachment : IWorldShiftable
    {
        /// <summary>The chunk was scrolled off-screen and returned to its pool; clean up now.</summary>
        void OnChunkRecycled();
    }
}
