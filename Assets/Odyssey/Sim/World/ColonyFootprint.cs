#nullable enable
using System;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.World
{
    /// <summary>
    /// Which layers the colony's footprint has changed on since the home area last looked
    /// (design 43 §3a, §3c): the one dirty signal for "something the colony placed was put down
    /// or taken away".
    ///
    /// <para><b>Every placement path touches it, and nothing else does.</b> The paths are the
    /// checklist in design 43 §3a — a floor or an edifice raised or demolished, a floor lost to a
    /// collapse, a build site placed or gone, a zone cell joined or left, a power line or line
    /// order laid or taken up. A path that forgets to touch is a home that silently fails to grow,
    /// which is why the list lives in the design document beside this type.</para>
    ///
    /// <para><b>Felling, mining, walking and fighting never touch it.</b> They change nothing the
    /// colony placed, and touching on them would make the busy arm of the tick benchmark pay for a
    /// rebuild it does not need.</para>
    ///
    /// <para>Not saved and not hashed: it is bookkeeping for a cache, and a fresh or loaded world
    /// starts with every layer dirty.</para>
    /// </summary>
    public sealed class ColonyFootprint
    {
        readonly GridSize _size;
        readonly bool[] _dirty;

        public ColonyFootprint(GridSize size)
        {
            _size = size;
            _dirty = new bool[Math.Max(1, size.SizeY)];
            TouchAll();
        }

        /// <summary>Bumped on every touch, so a test can ask whether anything was touched at all.</summary>
        public int Version { get; private set; }

        /// <summary>Is any layer waiting to be looked at again?</summary>
        public bool AnyDirty { get; private set; }

        /// <summary>Something the colony placed changed in this cell.</summary>
        public void Touch(int cell)
        {
            if ((uint)cell >= (uint)_size.CellCount) return;
            _dirty[cell / _size.LayerStride] = true;
            AnyDirty = true;
            Version++;
        }

        /// <summary>Every layer, as after a load: nothing is known about any of them.</summary>
        public void TouchAll()
        {
            Array.Fill(_dirty, true);
            AnyDirty = true;
            Version++;
        }

        /// <summary>Is this layer dirty? Out of range is never dirty.</summary>
        public bool IsDirty(int layer) => (uint)layer < (uint)_dirty.Length && _dirty[layer];

        /// <summary>The reader has looked at every dirty layer.</summary>
        internal void ClearDirty()
        {
            Array.Clear(_dirty, 0, _dirty.Length);
            AnyDirty = false;
        }
    }
}
