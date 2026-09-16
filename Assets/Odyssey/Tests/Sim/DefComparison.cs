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

        static string Show(object? value) => value == null ? "nothing" : $"'{value}'";
    }
}
