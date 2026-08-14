using System.Collections.Generic;
using UnityEngine;
using Run.Common;

namespace Run.Generation
{
    /// <summary>
    /// A contiguous stretch of generated world (one "chunk"). Tracks its horizontal
    /// span and the pooled pieces it owns so it can hand them all back on recycle.
    /// </summary>
    /// <remarks>
    /// Segment instances are themselves pooled by the generator, which is why they are
    /// plain classes reset through <see cref="Reset"/> rather than fresh allocations.
    /// </remarks>
    public sealed class Segment
    {
        private readonly List<KeyValuePair<PieceKind, GameObject>> _pieces =
            new List<KeyValuePair<PieceKind, GameObject>>();

        /// <summary>World-space X where the segment begins.</summary>
        public float StartX { get; private set; }

        /// <summary>Total width of the segment in world units.</summary>
        public float Width { get; set; }

        /// <summary>World-space X where the segment ends.</summary>
        public float EndX => StartX + Width;

        /// <summary>Prepares the (possibly recycled) segment to describe a new chunk.</summary>
        public void Reset(float startX)
        {
            StartX = startX;
            Width = 0f;
            _pieces.Clear();
        }

        /// <summary>Records a piece that belongs to this segment.</summary>
        public void Add(PieceKind kind, GameObject piece)
        {
            if (piece != null)
            {
                _pieces.Add(new KeyValuePair<PieceKind, GameObject>(kind, piece));
            }
        }

        /// <summary>Re-bases the segment and all its pieces by <paramref name="dx"/> on X (floating origin).</summary>
        public void Shift(float dx)
        {
            StartX += dx;

            for (int i = 0; i < _pieces.Count; i++)
            {
                var piece = _pieces[i].Value;
                if (piece == null)
                {
                    continue;
                }

                Vector3 position = piece.transform.position;
                position.x += dx;
                piece.transform.position = position;

                // Fix any world X cached outside the transform (coin bob origin, patrol bounds…).
                var shiftable = piece.GetComponent<IWorldShiftable>();
                if (shiftable != null)
                {
                    shiftable.OnWorldShift(dx);
                }
            }
        }

        /// <summary>Returns every owned piece to the factory pool and clears the record.</summary>
        public void Release(PieceFactory factory)
        {
            for (int i = 0; i < _pieces.Count; i++)
            {
                factory.Release(_pieces[i].Key, _pieces[i].Value);
            }

            _pieces.Clear();
        }
    }
}
