#nullable enable
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Hud
{
    /// <summary>One colonist reaching one new level in one skill.</summary>
    public readonly struct SkillLevelUp
    {
        public readonly PawnId Pawn;

        /// <summary>The index into <see cref="SkillCatalogue.All"/> of the skill that rose.</summary>
        public readonly int Skill;

        /// <summary>The level now reached — not the number of levels gained.</summary>
        public readonly int Level;

        public SkillLevelUp(PawnId pawn, int skill, int level)
        {
            Pawn = pawn;
            Skill = skill;
            Level = level;
        }
    }

    /// <summary>
    /// Notices when a colonist's skill level goes up, by watching the published frame (SK4).
    ///
    /// <para><b>There is no simulation side to this, and that is the design.</b>
    /// <c>PawnRegistry</c> already publishes every colonist's level in every skill on every frame,
    /// for all of them and not merely whoever is selected. A level is therefore something this
    /// side can simply <i>compare</i>, and an event in the simulation — a flag, a serial, a queue,
    /// a saved field — would be a second mechanism for a fact already on the wire.</para>
    ///
    /// <para><b>Why polling cannot miss one, which is the thing to understand before changing
    /// it.</b> <see cref="PawnGesture"/> needs a sticky flag <i>and</i> a serial because a gesture
    /// is an instant, and a reader that blinks between two frames misses it for ever. A level is
    /// not an instant, it is a standing value: whatever happened between two reads, the second
    /// read still says what the level is now. So the worst a slow poll can do is see two levels as
    /// one rise, and <see cref="Step"/> reports the level reached rather than the number of steps
    /// taken precisely so that case needs no special handling.</para>
    ///
    /// <para><b>The first sight of a colonist is silent</b>, the way
    /// <c>AlertChimeWatch</c> arms itself on whatever is already on screen. Without it every
    /// colonist would announce her whole history the first time she was published — on a load, on
    /// a new session, and on the frame a colonist joins — which is exactly the bug that rule was
    /// written for in the gesture serial's own remarks.</para>
    ///
    /// <para><b>A fall is not an event.</b> Skills above level ten decay, so a level can go down;
    /// nothing is announced when it does, and the new lower level becomes the one to beat.</para>
    /// </summary>
    public sealed class SkillLevelWatch
    {
        /// <summary>Last level seen, keyed by pawn and skill. Absent means never seen.</summary>
        readonly Dictionary<long, int> _seen = new Dictionary<long, int>();

        /// <summary>Pawns present in the frame being stepped, so the gone can be forgotten.</summary>
        readonly HashSet<int> _present = new HashSet<int>();

        readonly List<SkillLevelUp> _risen = new List<SkillLevelUp>();

        /// <summary>Skills being tracked, for a test and for the developer overlay.</summary>
        public int Tracking => _seen.Count;

        /// <summary>
        /// Read one frame and return every level reached since the last read. The list is reused,
        /// so a caller that wants to keep it must copy it.
        /// </summary>
        public IReadOnlyList<SkillLevelUp> Step(WorldSnapshot snapshot)
        {
            _risen.Clear();
            _present.Clear();

            var pawns = snapshot.Pawns;
            for (int i = 0; i < pawns.Length; i++) _present.Add(pawns[i].Id.Value);

            // One walk of the aspects rather than a lookup per pawn per skill: the lookup is a
            // scan, so asking for fifty colonists' four live skills would be two hundred scans of
            // the same span. This is the shape InspectModel.RefreshSkills already uses.
            var published = snapshot.PawnAspects;
            for (int a = 0; a < published.Length; a++)
            {
                PawnAspect aspect = published[a];
                if (!_present.Contains(aspect.Pawn.Value)) continue;

                for (int s = 0; s < SkillCatalogue.All.Length; s++)
                {
                    SkillCatalogue.Entry entry = SkillCatalogue.All[s];
                    if (!entry.Live || aspect.Key != entry.Level) continue;

                    long slot = Slot(aspect.Pawn, s);
                    int level = aspect.Value;

                    // First sight: record what she already is and say nothing about how she got
                    // there. Every colonist would otherwise announce her starting roll.
                    if (!_seen.TryGetValue(slot, out int last))
                    {
                        _seen[slot] = level;
                        break;
                    }

                    if (level > last) _risen.Add(new SkillLevelUp(aspect.Pawn, s, level));
                    // A fall through decay is not an event, but it is still the new truth: the
                    // level has to be beaten again before anything is said.
                    if (level != last) _seen[slot] = level;
                    break;
                }
            }

            Forget();
            return _risen;
        }

        /// <summary>
        /// Drop colonists the frame no longer carries, so a dead one cannot leave an entry behind
        /// for a future pawn to inherit and be measured against.
        /// </summary>
        void Forget()
        {
            if (_seen.Count == 0) return;

            List<long>? gone = null;
            foreach (var pair in _seen)
            {
                if (_present.Contains(PawnOf(pair.Key))) continue;
                (gone ??= new List<long>()).Add(pair.Key);
            }
            if (gone == null) return;
            for (int i = 0; i < gone.Count; i++) _seen.Remove(gone[i]);
        }

        /// <summary>
        /// Forget every colonist, for a colony that has gone away.
        ///
        /// <para><b>A session boundary is the one thing <see cref="Forget"/> cannot cover.</b> It
        /// drops whoever is missing from the frame it was just given, and between two colonies
        /// there is no frame at all — the interface is on the main menu and nothing is stepped. So
        /// the marks survive into the next colony, where <see cref="PawnId"/> 1 is a different
        /// person: load a save whose first colonist mines better than the last one's and she
        /// announces a level she has always had, which is the exact bug the first-sight rule
        /// exists to prevent, arriving by the one door that rule does not watch.</para>
        /// </summary>
        public void Clear() => _seen.Clear();

        static long Slot(PawnId pawn, int skill) => ((long)pawn.Value << 8) | (uint)skill;

        static int PawnOf(long slot) => (int)(slot >> 8);
    }
}
