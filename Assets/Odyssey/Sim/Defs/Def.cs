#nullable enable
using System;

namespace Odyssey.Sim.Defs
{
    /// <summary>
    /// Base of all content records. A Def is named, immutable after load, and referenced by name
    /// rather than by file position, so content can be reordered, patched or replaced by a mod
    /// without breaking anything that points at it.
    ///
    /// Defs are content, never save data. A save stores a Def <em>name</em>; it never stores a
    /// copy. That is what lets content change under an existing save, and what lets a missing mod
    /// degrade to a named sentinel instead of a corrupt file.
    /// </summary>
    public abstract class Def
    {
        /// <summary>Unique within a Def type. The currency of every cross-reference.</summary>
        public string defName = string.Empty;

        /// <summary>Player-facing name. Kept separate from <see cref="defName"/> so it can be localised.</summary>
        public string? label;

        public string? description;

        /// <summary>
        /// Abstract Defs are templates for inheritance and are dropped before binding. They never
        /// reach the running game.
        /// </summary>
        public bool Abstract { get; internal set; }

        /// <summary>Where this Def came from, for error messages that name the culprit.</summary>
        public DefOrigin Origin { get; internal set; }

        public override string ToString() => $"{GetType().Name}({defName})";
    }

    /// <summary>
    /// Provenance for one Def, retained so a validation failure can name the content pack, file
    /// and line rather than only a merged line number. Half of modding support is answering
    /// "which mod broke this", and recording it costs almost nothing.
    /// </summary>
    public readonly struct DefOrigin
    {
        public readonly string Pack;
        public readonly string File;
        public readonly int Line;

        public DefOrigin(string pack, string file, int line)
        {
            Pack = pack;
            File = file;
            Line = line;
        }

        public override string ToString() =>
            string.IsNullOrEmpty(File) ? Pack : $"{Pack}:{File}({Line})";
    }

    /// <summary>
    /// Marks a string field as naming another Def, so load-time validation can prove the target
    /// exists. Without this, a typo in a reference surfaces as a null at three in the morning
    /// during a ten-day headless run instead of as a load error with a file and line.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class DefReferenceAttribute : Attribute
    {
        public DefReferenceAttribute(Type defType)
        {
            DefType = defType ?? throw new ArgumentNullException(nameof(defType));
        }

        public Type DefType { get; }

        /// <summary>When true, an empty value is allowed and means "none".</summary>
        public bool Optional { get; set; }
    }

    /// <summary>Marks a field that must be present and non-default after loading.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class DefRequiredAttribute : Attribute
    {
    }
}
