#nullable enable
using System;
using System.Collections.Generic;
using Odyssey.Sim.Defs;

namespace Odyssey.Sim.Worldgen
{
    /// <summary>What one authored template cell is. One character in the authoring format.</summary>
    public enum ShellCellKind : byte
    {
        /// <summary>Outside the shell. The generator does not touch the cell at all.</summary>
        Void = 0,

        /// <summary>Interior space, with a slab at its lower boundary.</summary>
        Open = 1,

        /// <summary>Interior space with no slab — a lightwell, an atrium void, an open stairwell.</summary>
        OpenNoSlab = 2,

        Wall = 3,
        Door = 4,
        Window = 5,
        Pillar = 6,

        /// <summary>The lower half of a stair. Pairs with an adjacent <see cref="StairUpper"/>.</summary>
        StairLower = 7,

        /// <summary>The upper half of a stair. Two cells climb exactly one layer (section 5).</summary>
        StairUpper = 8,

        Ladder = 9,
    }

    /// <summary>
    /// A compiled shell template: the authored cells, resolved once, indexed in O(1).
    ///
    /// Templates are **data**, expressed as a <see cref="TemplateDef"/> and compiled here. The
    /// authoring format is ASCII rows — one character per cell, one row per z, one block of rows
    /// per layer — because that is simultaneously readable in a diff, trivially embeddable in XML
    /// as a list of strings, and impossible to author in metres by accident.
    ///
    /// <code>
    ///   ' '  outside the shell        '#'  wall          '+'  door
    ///   '.'  interior, has a slab     'o'  window        'I'  pillar
    ///   ','  interior, no slab        'L'  ladder        '&lt;' '&gt;'  stair, lower then upper
    /// </code>
    ///
    /// The vertical extent travels with the template: <see cref="BottomLayer"/> is negative for a
    /// basement and <see cref="TopLayer"/> is the highest occupied storey, both relative to street
    /// level. Nothing outside this class knows the ground-layer offset.
    /// </summary>
    public sealed class ShellTemplate
    {
        readonly byte[] _cells;

        ShellTemplate(TemplateDef def, byte[] cells, ushort stuff)
        {
            Id = def.defName;
            SizeX = def.sizeX;
            SizeZ = def.sizeZ;
            BottomLayer = def.bottomLayer;
            TopLayer = def.topLayer;
            Weight = def.weight;
            Roof = def.roof;
            DamageTolerance = def.damageTolerance;
            Stuff = stuff;
            Source = def;
            _cells = cells;
        }

        public string Id { get; }
        public int SizeX { get; }
        public int SizeZ { get; }
        public int BottomLayer { get; }
        public int TopLayer { get; }
        public int Weight { get; }
        public bool Roof { get; }
        public int DamageTolerance { get; }
        public ushort Stuff { get; }

        /// <summary>The Def this was compiled from, for module ids and error messages.</summary>
        public TemplateDef Source { get; }

        public int LayerCount => TopLayer - BottomLayer + 1;

        /// <summary>The topmost layer this template writes to, roof included.</summary>
        public int HighestLayer => Roof ? TopLayer + 1 : TopLayer;

        /// <summary><paramref name="layer"/> is relative to street level, in [BottomLayer, TopLayer].</summary>
        public ShellCellKind Cell(int layer, int x, int z) =>
            (ShellCellKind)_cells[((layer - BottomLayer) * SizeZ + z) * SizeX + x];

        /// <summary>Does the template fit a plot of this footprint, in this many layers?</summary>
        public bool Fits(int plotSizeX, int plotSizeZ, int layersBelow, int layersAbove) =>
            SizeX <= plotSizeX && SizeZ <= plotSizeZ &&
            -BottomLayer <= layersBelow && HighestLayer <= layersAbove;

        /// <summary>Everything but <see cref="ShellCellKind.Void"/> and an open void has a slab.</summary>
        public static bool HasSlab(ShellCellKind kind) =>
            kind != ShellCellKind.Void && kind != ShellCellKind.OpenNoSlab;

        /// <summary>Walls, windows and pillars block movement; ruined doors and connectors do not.</summary>
        public static bool Blocks(ShellCellKind kind) =>
            kind == ShellCellKind.Wall || kind == ShellCellKind.Window || kind == ShellCellKind.Pillar;

        /// <summary>The edifice def index this kind places, or <c>EdificeNone</c> for open space.</summary>
        public static ushort EdificeFor(ShellCellKind kind)
        {
            switch (kind)
            {
                case ShellCellKind.Wall: return CoreContent.EdificeWall;
                case ShellCellKind.Door: return CoreContent.EdificeDoor;
                case ShellCellKind.Window: return CoreContent.EdificeWindow;
                case ShellCellKind.Pillar: return CoreContent.EdificePillar;
                case ShellCellKind.StairLower: return CoreContent.EdificeStairLower;
                case ShellCellKind.StairUpper: return CoreContent.EdificeStairUpper;
                case ShellCellKind.Ladder: return CoreContent.EdificeLadder;
                default: return CoreContent.EdificeNone;
            }
        }

        /// <summary>The presentation module id for a kind. Ids only; nothing resolves an asset here.</summary>
        public string ModuleFor(ShellCellKind kind)
        {
            switch (kind)
            {
                case ShellCellKind.Wall: return Source.wallModuleId;
                case ShellCellKind.Door: return Source.doorModuleId;
                case ShellCellKind.Window: return Source.windowModuleId;
                case ShellCellKind.Pillar: return Source.pillarModuleId;
                case ShellCellKind.StairLower:
                case ShellCellKind.StairUpper: return Source.stairModuleId;
                case ShellCellKind.Ladder: return Source.ladderModuleId;
                default: return Source.slabModuleId;
            }
        }

        public static ShellCellKind KindOf(char c)
        {
            switch (c)
            {
                case ' ': return ShellCellKind.Void;
                case '.': return ShellCellKind.Open;
                case ',': return ShellCellKind.OpenNoSlab;
                case '#': return ShellCellKind.Wall;
                case '+': return ShellCellKind.Door;
                case 'o': return ShellCellKind.Window;
                case 'I': return ShellCellKind.Pillar;
                case '<': return ShellCellKind.StairLower;
                case '>': return ShellCellKind.StairUpper;
                case 'L': return ShellCellKind.Ladder;
                default: throw new DefLoadException($"Unknown template character '{c}'.");
            }
        }

        /// <summary>
        /// Compile an authored Def. Every structural rule a stamped shell depends on is checked
        /// here, so a broken template is a content error with a name attached rather than a
        /// collapsing map on tick one.
        /// </summary>
        public static ShellTemplate Compile(TemplateDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (def.sizeX < 2 || def.sizeZ < 2)
                throw new DefLoadException($"Template '{def.defName}' is smaller than 2x2.", def.Origin);
            if (def.bottomLayer > 0 || def.topLayer < 0)
                throw new DefLoadException($"Template '{def.defName}' must span street level (layer 0).", def.Origin);

            int layers = def.LayerCount;
            if (def.rows.Count != layers * def.sizeZ)
                throw new DefLoadException(
                    $"Template '{def.defName}' has {def.rows.Count} rows, expected {layers * def.sizeZ} " +
                    $"({layers} layers x {def.sizeZ} rows).", def.Origin);

            var cells = new byte[layers * def.sizeZ * def.sizeX];
            int open = 0;
            for (int li = 0; li < layers; li++)
            {
                for (int z = 0; z < def.sizeZ; z++)
                {
                    string row = def.rows[li * def.sizeZ + z];
                    if (row.Length != def.sizeX)
                        throw new DefLoadException(
                            $"Template '{def.defName}' layer {li + def.bottomLayer} row {z} is " +
                            $"{row.Length} cells wide, expected {def.sizeX}.", def.Origin);
                    for (int x = 0; x < def.sizeX; x++)
                    {
                        var kind = KindOf(row[x]);
                        cells[(li * def.sizeZ + z) * def.sizeX + x] = (byte)kind;
                        if (kind == ShellCellKind.Open) open++;
                    }
                }
            }

            if (open == 0)
                throw new DefLoadException($"Template '{def.defName}' has no interior space.", def.Origin);

            var template = new ShellTemplate(def, cells, CoreContent.StuffByName(def.stuff));
            template.ValidateConnectors();
            return template;
        }

        /// <summary>
        /// A stair is two adjacent cells on one layer that climb to the next, and a ladder is one
        /// cell that does the same, so both need headroom directly above. Checking it at compile
        /// time is the difference between "a test names the template" and "a colonist walks into
        /// a wall three milestones later".
        /// </summary>
        void ValidateConnectors()
        {
            for (int layer = BottomLayer; layer <= TopLayer; layer++)
            for (int z = 0; z < SizeZ; z++)
            for (int x = 0; x < SizeX; x++)
            {
                var kind = Cell(layer, x, z);
                if (kind != ShellCellKind.StairLower && kind != ShellCellKind.StairUpper && kind != ShellCellKind.Ladder)
                    continue;

                if (layer == TopLayer)
                    throw new DefLoadException(
                        $"Template '{Id}' has a connector at ({x},{z}) on its top layer {layer}, " +
                        "which has nothing to connect to.", Source.Origin);

                var above = Cell(layer + 1, x, z);
                if (above == ShellCellKind.Void || Blocks(above))
                    throw new DefLoadException(
                        $"Template '{Id}' connector at ({x},{z},L{layer}) is blocked from above by {above}.",
                        Source.Origin);

                // A stair is two adjacent cells, so each half must find the other. Checking both
                // halves rather than only the lower one means deleting either character is caught.
                if (kind == ShellCellKind.StairLower || kind == ShellCellKind.StairUpper)
                {
                    var other = kind == ShellCellKind.StairLower ? ShellCellKind.StairUpper : ShellCellKind.StairLower;
                    bool paired =
                        (x + 1 < SizeX && Cell(layer, x + 1, z) == other) ||
                        (x > 0 && Cell(layer, x - 1, z) == other) ||
                        (z + 1 < SizeZ && Cell(layer, x, z + 1) == other) ||
                        (z > 0 && Cell(layer, x, z - 1) == other);
                    if (!paired)
                        throw new DefLoadException(
                            $"Template '{Id}' has an unpaired stair at ({x},{z},L{layer}).", Source.Origin);
                }
            }
        }

        public override string ToString() => $"{Id} {SizeX}x{SizeZ} L{BottomLayer}..L{TopLayer}";
    }

    /// <summary>
    /// The compiled templates a map may stamp, in a fixed order.
    ///
    /// Order is part of the determinism contract: the weighted choice in pass 2 walks this list,
    /// so reordering it changes every map. A Def-driven set must therefore be sorted by
    /// <c>defName</c> before it reaches here, which <see cref="FromDefs"/> does.
    /// </summary>
    public sealed class TemplateSet
    {
        readonly ShellTemplate[] _templates;

        public TemplateSet(IEnumerable<ShellTemplate> templates)
        {
            var list = new List<ShellTemplate>(templates);
            if (list.Count == 0) throw new ArgumentException("A template set cannot be empty.", nameof(templates));
            _templates = list.ToArray();
        }

        public int Count => _templates.Length;
        public ShellTemplate this[int index] => _templates[index];

        public int IndexOf(string id)
        {
            for (int i = 0; i < _templates.Length; i++)
                if (string.Equals(_templates[i].Id, id, StringComparison.Ordinal)) return i;
            return -1;
        }

        /// <summary>
        /// Compile a Def table into a set. Sorting by name first means the load order of content
        /// files cannot change a map, which is the same guarantee the Def table itself gives.
        /// </summary>
        public static TemplateSet FromDefs(IEnumerable<TemplateDef> defs)
        {
            var list = new List<TemplateDef>(defs);
            list.Sort((a, b) => string.CompareOrdinal(a.defName, b.defName));
            var compiled = new List<ShellTemplate>(list.Count);
            foreach (var def in list) compiled.Add(ShellTemplate.Compile(def));
            return new TemplateSet(compiled);
        }
    }
}
