#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Contracts;

namespace Odyssey.Sim.Pathing
{
    /// <summary>
    /// Districts kept current by repairing them where an edit touched them, rather than flooding
    /// every region on the board on every rebuild (HT1, <c>docs/design/05-ai-and-jobs.md</c> §7).
    ///
    /// <para><b>Why not "re-flood the component the edit touched".</b> On a real board nearly every
    /// region on the surface is one district, so the touched component is the board. What can
    /// change instead is narrower. A district can only <b>split</b> where the edit took something
    /// out, and then only if the regions that bordered what went — the surviving ends of every link
    /// freed — stop reaching each other: any path between two surviving regions either avoids the
    /// edit or enters and leaves it through such border regions. A district can only <b>merge</b>
    /// through a link built by the edit. So the repair starts a search from each <i>seed</i> — every
    /// region the rebuild allocated, and both ends of every link it freed or built — and runs the
    /// searches in turn, one region each. Searches that meet are one group; each group carries the
    /// old district ids its seeds had. It stops as soon as no two live groups carry the same id and
    /// every live group carries one. The ordinary edit meets within a few regions, through the block
    /// just rebuilt.</para>
    ///
    /// <para>Then: a group that ran out of frontier explored its whole component, a closed piece, and
    /// takes a fresh id. A live group keeps the id with the most members of those it carries, and
    /// each smaller id it carries is relabelled by walking that id's regions from its own seeds —
    /// which is why a merge costs the smaller district and not the board. Every region no search
    /// reached keeps its id: its component had no seed, so no edit touched it.</para>
    ///
    /// <para><b>Ids mean nothing but equality</b> (<see cref="Reachable"/>), are not saved and are
    /// not in the state hash. They are kept per mode with a member count and a free list; a full
    /// rebuild (<see cref="RecomputeDistricts"/>) numbers them afresh. <see cref="DistrictCount"/>
    /// stays the number of live districts.</para>
    ///
    /// <para><b>What it costs</b>: the regions the searches visit before they meet, plus the smaller
    /// side of a merge, plus a closed piece's size — never the board. The oracle is
    /// <see cref="DerivedTablesDisagree"/>, run after every edit of the randomised fixtures.</para>
    /// </summary>
    public sealed partial class NavGraph
    {
        // ---- per-mode district bookkeeping ------------------------------------------------------
        readonly int[][] _districtSize = new int[TraverseModes.Count][];
        readonly int[] _districtCap = new int[TraverseModes.Count];
        readonly List<int>[] _freeDistrictIds = NewLists(TraverseModes.Count);
        bool _districtsBuilt;

        // ---- what this rebuild touched ----------------------------------------------------------
        readonly List<int> _repairSeeds = new List<int>();
        readonly List<int> _builtLinks = new List<int>();

        // ---- scratch for the repair, reused ---------------------------------------------------
        int[] _visitStamp = new int[64];
        int[] _visitGroup = new int[64];
        int[] _frontNext = new int[64];
        int _stamp;
        readonly List<int> _groupParent = new List<int>();
        readonly List<int> _groupHead = new List<int>();
        readonly List<int> _groupTail = new List<int>();
        readonly List<List<int>> _groupLabels = new List<List<int>>();
        readonly List<bool> _groupClosed = new List<bool>();
        readonly Dictionary<int, int> _labelLiveGroups = new Dictionary<int, int>();
        readonly List<int> _relabelQueue = new List<int>();
        readonly List<int> _visited = new List<int>();
        readonly List<int> _groupId = new List<int>();

        static List<int>[] NewLists(int n)
        {
            var lists = new List<int>[n];
            for (int i = 0; i < n; i++) lists[i] = new List<int>();
            return lists;
        }

        /// <summary>A region's district in every mode, as it is freed: out of its district's count.</summary>
        void LeaveDistricts(int region)
        {
            if (!_districtsBuilt) return;
            for (int m = 0; m < TraverseModes.Count; m++)
            {
                int d = _district[m][region];
                if (d >= 0) Shrink(m, d);
                _district[m][region] = -1;
            }
        }

        /// <summary>A region the repair must start from: allocated, or at an end of a link freed or built.</summary>
        void AddRepairSeed(int region)
        {
            if (_districtsBuilt) _repairSeeds.Add(region);
        }

        /// <summary>A link this rebuild built: its ends are joined before any search (a merge runs through one).</summary>
        void NoteBuiltLink(int link)
        {
            if (!_districtsBuilt) return;
            _builtLinks.Add(link);
            _repairSeeds.Add(_linkA[link]);
            _repairSeeds.Add(_linkB[link]);
        }

        int NewDistrictId(int mode)
        {
            List<int> free = _freeDistrictIds[mode];
            int id;
            if (free.Count > 0)
            {
                id = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else
            {
                id = _districtCap[mode]++;
                int[] sizes = _districtSize[mode];
                if (id >= sizes.Length) Array.Resize(ref _districtSize[mode], Math.Max(16, sizes.Length * 2));
            }

            _districtSize[mode][id] = 0;
            _districtCount[mode]++;
            return id;
        }

        void Shrink(int mode, int id)
        {
            if (--_districtSize[mode][id] > 0) return;
            _freeDistrictIds[mode].Add(id);
            _districtCount[mode]--;
        }

        void Relabel(int mode, int region, int id)
        {
            int old = _district[mode][region];
            if (old == id) return;
            if (old >= 0) Shrink(mode, old);
            _district[mode][region] = id;
            _districtSize[mode][id]++;
        }

        /// <summary>
        /// Keep the districts current after a rebuild. The first build, and any rebuild after the
        /// graph was marked wholly dirty, numbers them afresh; every other rebuild repairs them.
        /// </summary>
        void UpdateDistricts()
        {
            bool full = !_districtsBuilt || _dirtyList.Count * 4 >= BlockCount;
            if (full)
            {
                RecomputeDistricts();
                ResetDistrictBookkeeping();
            }
            else
            {
                EnsureRepairCapacity();
                for (int m = 0; m < TraverseModes.Count; m++) RepairDistricts(m);
            }

            _repairSeeds.Clear();
            _builtLinks.Clear();
        }

        void ResetDistrictBookkeeping()
        {
            for (int m = 0; m < TraverseModes.Count; m++)
            {
                int n = _districtCount[m];
                if (_districtSize[m] == null || _districtSize[m].Length < Math.Max(16, n))
                    _districtSize[m] = new int[Math.Max(16, n * 2)];
                else Array.Clear(_districtSize[m], 0, _districtSize[m].Length);
                for (int r = 0; r < _regionCount; r++)
                {
                    int d = _district[m][r];
                    if (d >= 0 && _regionAlive[r]) _districtSize[m][d]++;
                }
                _districtCap[m] = n;
                _freeDistrictIds[m].Clear();
            }
            _districtsBuilt = true;
        }

        void EnsureRepairCapacity()
        {
            int n = _regionAlive.Length;
            if (_visitStamp.Length >= n) return;
            Array.Resize(ref _visitStamp, n);
            Array.Resize(ref _visitGroup, n);
            Array.Resize(ref _frontNext, n);
        }

        bool IsSeedable(int region) =>
            region >= 0 && region < _regionCount && _regionAlive[region]
            && _regionKind[region] != RegionKind.None && _regionKind[region] != RegionKind.Impassable;

        void RepairDistricts(int m)
        {
            int[] d = _district[m];
            int stamp = ++_stamp;

            _groupParent.Clear();
            _groupHead.Clear();
            _groupTail.Clear();
            _groupClosed.Clear();
            _labelLiveGroups.Clear();
            _visited.Clear();

            // One group per distinct seed; a region is visited once, by the first group to reach it.
            for (int i = 0; i < _repairSeeds.Count; i++)
            {
                int r = _repairSeeds[i];
                if (!IsSeedable(r) || _visitStamp[r] == stamp) continue;
                int g = _groupParent.Count;
                _groupParent.Add(g);
                _groupHead.Add(r);
                _groupTail.Add(r);
                _groupClosed.Add(false);
                if (_groupLabels.Count <= g) _groupLabels.Add(new List<int>());
                List<int> labels = _groupLabels[g];
                labels.Clear();
                if (d[r] >= 0) labels.Add(d[r]);
                _visitStamp[r] = stamp;
                _visitGroup[r] = g;
                _frontNext[r] = -1;
                _visited.Add(r);
            }

            int groups = _groupParent.Count;
            if (groups == 0) return;

            // Everything the edit built joins its ends at once: a merge can only happen through a new
            // region or a new link, and both ends of every new link are seeds. After this a group with
            // no old id is new regions and nothing else — every link out of a new region is new — so it
            // is a whole component already.
            for (int i = 0; i < _builtLinks.Count; i++)
            {
                int l = _builtLinks[i];
                if (_linkOneWay[l] || (_linkMode[l] & (1 << m)) == 0) continue;
                int a = _linkA[l], b = _linkB[l];
                if (_visitStamp[a] != stamp || _visitStamp[b] != stamp) continue;
                int ga = Find(_visitGroup[a]), gb = Find(_visitGroup[b]);
                if (ga != gb) Join(ga, gb);
            }

            // What is still unknown: whether groups carrying the same old id still reach each other
            // through the old graph, or it split. Only those need searching.
            int shared = 0;
            for (int g = 0; g < groups; g++)
            {
                if (_groupParent[g] != g) continue;
                if (_groupLabels[g].Count == 0)
                {
                    _groupClosed[g] = true;
                    continue;
                }
                foreach (int label in _groupLabels[g])
                {
                    int count = _labelLiveGroups.TryGetValue(label, out int c) ? c + 1 : 1;
                    _labelLiveGroups[label] = count;
                    if (count == 2) shared++;
                }
            }

            // The searches, in turn, one region each, until no two live groups carry one id. Groups
            // that meet are joined; a group out of frontier has explored its whole component.
            while (shared > 0)
            {
                bool progressed = false;
                for (int g = 0; g < groups && shared > 0; g++)
                {
                    if (_groupParent[g] != g || _groupClosed[g]) continue;
                    int r = _groupHead[g];
                    if (r == -1)
                    {
                        _groupClosed[g] = true;
                        foreach (int label in _groupLabels[g]) if (Unlive(label)) shared--;
                        continue;
                    }

                    progressed = true;
                    _groupHead[g] = _frontNext[r];
                    if (_groupHead[g] == -1) _groupTail[g] = -1;

                    // The group this expansion belongs to: a meeting below can make it a child.
                    int cur = g;
                    int s = _adjStart[r];
                    int e = s + _adjCount[r];
                    for (int i = s; i < e; i++)
                    {
                        int l = _adjLinks[i];
                        if (_linkOneWay[l] || (_linkMode[l] & (1 << m)) == 0) continue;
                        int other = _linkA[l] == r ? _linkB[l] : _linkA[l];
                        if (_visitStamp[other] != stamp)
                        {
                            _visitStamp[other] = stamp;
                            _visitGroup[other] = cur;
                            _frontNext[other] = -1;
                            _visited.Add(other);
                            if (_groupTail[cur] == -1) _groupHead[cur] = other; else _frontNext[_groupTail[cur]] = other;
                            _groupTail[cur] = other;
                            continue;
                        }

                        int h = Find(_visitGroup[other]);
                        if (h == cur) continue;
                        // Two live groups: the ids they both carried are now carried once.
                        foreach (int label in _groupLabels[Math.Max(cur, h)])
                            if (_groupLabels[Math.Min(cur, h)].Contains(label) && Unlive(label)) shared--;
                        cur = Join(cur, h);
                    }
                }

                if (!progressed) break;
            }

            // Every group's id. A closed group explored its whole piece and takes a fresh id. A live
            // group keeps the id with the most members of those its seeds carried, and every smaller
            // id it carried is walked from its own regions and relabelled: those districts merged.
            _groupId.Clear();
            for (int g = 0; g < groups; g++) _groupId.Add(-1);
            for (int g = 0; g < groups; g++)
            {
                if (_groupParent[g] != g) continue;
                List<int> labels = _groupLabels[g];
                if (_groupClosed[g] || labels.Count == 0)
                {
                    _groupId[g] = NewDistrictId(m);
                    continue;
                }

                int keep = labels[0];
                for (int i = 1; i < labels.Count; i++)
                {
                    int x = labels[i];
                    if (_districtSize[m][x] > _districtSize[m][keep] || (_districtSize[m][x] == _districtSize[m][keep] && x < keep))
                        keep = x;
                }
                _groupId[g] = keep;
                for (int i = 0; i < labels.Count; i++)
                    if (labels[i] != keep) Absorb(m, g, labels[i], keep);
            }

            for (int i = 0; i < _visited.Count; i++)
            {
                int r = _visited[i];
                Relabel(m, r, _groupId[Find(_visitGroup[r])]);
            }
        }

        /// <summary>
        /// Relabel every region of district <paramref name="from"/> in group <paramref name="group"/>'s
        /// component as <paramref name="into"/>, walking out from that district's regions the group
        /// visited — every surviving piece of a district an edit touched has a seed in it — through
        /// regions still carrying <paramref name="from"/>. Costs the smaller district.
        /// </summary>
        void Absorb(int m, int group, int from, int into)
        {
            int[] d = _district[m];
            _relabelQueue.Clear();
            for (int i = 0; i < _visited.Count; i++)
            {
                int r = _visited[i];
                if (d[r] == from && Find(_visitGroup[r]) == group) _relabelQueue.Add(r);
            }

            for (int q = 0; q < _relabelQueue.Count; q++)
            {
                int r = _relabelQueue[q];
                if (d[r] != from) continue;
                Relabel(m, r, into);
                int s = _adjStart[r];
                int e = s + _adjCount[r];
                for (int i = s; i < e; i++)
                {
                    int l = _adjLinks[i];
                    if (_linkOneWay[l] || (_linkMode[l] & (1 << m)) == 0) continue;
                    int other = _linkA[l] == r ? _linkB[l] : _linkA[l];
                    if (d[other] == from) _relabelQueue.Add(other);
                }
            }
        }

        bool Unlive(int label)
        {
            int count = _labelLiveGroups[label] - 1;
            _labelLiveGroups[label] = count;
            return count == 1;
        }

        int Find(int g)
        {
            while (_groupParent[g] != g)
            {
                _groupParent[g] = _groupParent[_groupParent[g]];
                g = _groupParent[g];
            }
            return g;
        }

        /// <summary>Join two groups: the lower index is the root; ids and frontiers are combined.</summary>
        int Join(int a, int b)
        {
            int root = Math.Min(a, b), child = Math.Max(a, b);
            List<int> into = _groupLabels[root];
            foreach (int label in _groupLabels[child]) if (!into.Contains(label)) into.Add(label);

            if (_groupHead[child] != -1)
            {
                if (_groupTail[root] == -1) _groupHead[root] = _groupHead[child];
                else _frontNext[_groupTail[root]] = _groupHead[child];
                _groupTail[root] = _groupTail[child];
            }
            _groupHead[child] = -1;
            _groupTail[child] = -1;
            _groupParent[child] = root;
            return root;
        }
    }
}
