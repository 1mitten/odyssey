#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Odyssey.Tests.Sim
{
    /// <summary>
    /// Compares two content records of the same shape and reports every public field that
    /// differs, with the path to it.
    ///
    /// <para>This is what makes "the XML is the same content as the code" a test rather than a
    /// hope, for the pawn tables (OQ-15) and the world tables (OQ-16) alike. It walks fields by
    /// reflection rather than listing them, so a field added to a Def tomorrow is compared
    /// without anybody remembering to come back here.</para>
    ///
    /// <para><b>Fields only, deliberately.</b> <c>Def.Origin</c> and <c>Def.Abstract</c> are
    /// properties: the loaded copy has a pack, a file and a line and the in-code oracle has none,
    /// and that is provenance rather than content. Comparing it would fail every run for a
    /// difference that means nothing.</para>
    ///
    /// <para><b>A reflective comparer can pass by walking nothing</b>, which is the failure mode
    /// that makes this whole approach worthless if it is not checked. Every test file that uses
    /// it carries controls that perturb one value and require the walk to name it.</para>
    /// </summary>
    public static class DefComparison
    {
        /// <summary>Every difference between two records, as readable paths. Empty means equal.</summary>
        public static List<string> Differences(object? expected, object? actual, string path)
        {
            var differences = new List<string>();
            Compare(expected, actual, path, differences);
            return differences;
        }

        static void Compare(object? expected, object? actual, string path, List<string> differences)
        {
            if (expected == null || actual == null)
            {
                if (!ReferenceEquals(expected, actual))
                    differences.Add($"{path}: expected {Show(expected)}, found {Show(actual)}");
                return;
            }

            Type type = expected.GetType();
            if (type != actual.GetType())
            {
                differences.Add($"{path}: expected a {type.Name}, found a {actual.GetType().Name}");
                return;
            }

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                if (!expected.Equals(actual))
                    differences.Add($"{path}: expected {Show(expected)}, found {Show(actual)}");
                return;
            }

            if (expected is IList expectedList && actual is IList actualList)
            {
                if (expectedList.Count != actualList.Count)
                {
                    differences.Add($"{path}: expected {expectedList.Count} entries, found {actualList.Count}");
                    return;
                }
                for (int i = 0; i < expectedList.Count; i++)
                    Compare(expectedList[i], actualList[i], $"{path}[{i}]", differences);
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Compare(field.GetValue(expected), field.GetValue(actual), $"{path}.{field.Name}", differences);
        }

        /// <summary>
        /// One number standing for every value in a content record, by the same walk
        /// <see cref="Differences"/> uses.
        ///
        /// <para><b>What replaced the oracle, and why it had to.</b> While the simulation built its
        /// content from <c>PawnContent.Core()</c>, the right test was "the XML says the same as the
        /// code", because the code was the specification every soak hash and tuning decision had
        /// been measured against. Since the simulation loads the XML there is no second copy to
        /// disagree with, and the question changes to the one a golden answers: has the content
        /// moved without anybody saying so? A fingerprint makes a content change cost one
        /// deliberate line instead of passing silently.</para>
        ///
        /// <para>The path is folded in as well as the value, so renaming a field or reordering a
        /// table moves the fingerprint too — those change what the simulation reads just as surely
        /// as editing a number does.</para>
        ///
        /// <para>It shares the walk rather than copying it, so the controls that prove the walk
        /// reaches nested lists prove it for both callers at once. A walk that reached nothing
        /// would return the bare offset basis and every control would fail.</para>
        /// </summary>
        public static ulong Fingerprint(object? value, string path)
        {
            ulong hash = 14695981039346656037UL;
            Fold(value, path, ref hash);
            return hash;
        }

        static void Fold(object? value, string path, ref ulong hash)
        {
            Absorb(path, ref hash);

            if (value == null)
            {
                Absorb("null", ref hash);
                return;
            }

            Type type = value.GetType();

            if (type.IsPrimitive || type.IsEnum || type == typeof(string))
            {
                Absorb(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "", ref hash);
                return;
            }

            if (value is IList list)
            {
                Absorb(list.Count.ToString(System.Globalization.CultureInfo.InvariantCulture), ref hash);
                for (int i = 0; i < list.Count; i++) Fold(list[i], $"{path}[{i}]", ref hash);
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
                Fold(field.GetValue(value), $"{path}.{field.Name}", ref hash);
        }

        static void Absorb(string text, ref ulong hash)
        {
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                hash = (hash ^ (byte)c) * 1099511628211UL;
                hash = (hash ^ (byte)(c >> 8)) * 1099511628211UL;
            }
            hash = (hash ^ 0xFF) * 1099511628211UL;
        }

        static string Show(object? value) => value == null ? "nothing" : $"'{value}'";
    }
}
