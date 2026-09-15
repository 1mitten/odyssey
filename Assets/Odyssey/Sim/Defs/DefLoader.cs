#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace Odyssey.Sim.Defs
{
    /// <summary>One content pack: core, or a mod.</summary>
    public interface IDefSource
    {
        /// <summary>Stable identifier. Matching is on package id, never on display name.</summary>
        string PackId { get; }

        /// <summary>Each document, with the file name it came from, in a deterministic order.</summary>
        IEnumerable<(string file, string xml)> Documents();
    }

    /// <summary>A content pack held in memory. The default for tests.</summary>
    public sealed class InMemoryDefSource : IDefSource
    {
        readonly List<(string, string)> _docs = new List<(string, string)>();

        public InMemoryDefSource(string packId) { PackId = packId; }

        public string PackId { get; }

        public InMemoryDefSource Add(string file, string xml)
        {
            _docs.Add((file, xml));
            return this;
        }

        public IEnumerable<(string file, string xml)> Documents() => _docs;
    }

    /// <summary>
    /// Loads content into a <see cref="DefDatabase"/>.
    ///
    /// Pass order, from docs/design/04-data-model.md. Patch runs before inherit, which is easy to
    /// get backwards and matters: a mod patching an abstract parent expects the change to flow
    /// down to every child, and that only happens in this order.
    ///
    /// Errors accumulate within a pass and are thrown together at its end, so one run reports
    /// every broken file rather than making the author fix them one at a time.
    ///
    /// Binding uses reflection, which is a load-time cost only and never touches a tick. The
    /// source-generated binder that replaces it (D7) is an ahead-of-time-safety and startup-cost
    /// improvement, not a runtime one, and it is deliberately deferred until a measurement asks.
    /// </summary>
    public sealed class DefLoader
    {
        readonly List<IDefSource> _sources = new List<IDefSource>();
        readonly Dictionary<string, Type> _typesByName = new Dictionary<string, Type>(StringComparer.Ordinal);
        readonly List<string> _errors = new List<string>();

        public DefLoader Register<T>() where T : Def => Register(typeof(T));

        public DefLoader Register(Type defType)
        {
            if (!typeof(Def).IsAssignableFrom(defType))
                throw new ArgumentException($"{defType.Name} does not derive from Def.", nameof(defType));
            _typesByName[defType.Name] = defType;
            return this;
        }

        public DefLoader AddSource(IDefSource source)
        {
            _sources.Add(source ?? throw new ArgumentNullException(nameof(source)));
            return this;
        }

        public DefDatabase Load()
        {
            _errors.Clear();

            // P0 Discover, P1 Parse. Provenance is retained per node.
            var parsed = Parse();

            // P2 Patch is applied here once patch operations land; the hook exists so its
            // position in the order cannot drift.

            // P3 Inherit, P4 Prune abstracts.
            var concrete = ResolveInheritance(parsed);

            // P5 Bind.
            var bound = Bind(concrete);

            // P7 Index into dense tables with integer handles.
            var tables = BuildTables(bound);

            // P8 Resolve cross-references, P9 Validate.
            var database = new DefDatabase(tables);
            ValidateReferences(bound, database);

            ThrowIfErrors();
            return database;
        }

        // ------------------------------------------------------------------ parse

        sealed class ParsedDef
        {
            public string TypeName = string.Empty;
            public string DefName = string.Empty;
            public string? ParentName;
            public bool Abstract;
            public XElement Element = null!;
            public DefOrigin Origin;
        }

        List<ParsedDef> Parse()
        {
            var parsed = new List<ParsedDef>();
            foreach (var source in _sources)
            {
                foreach (var (file, xml) in source.Documents())
                {
                    XDocument doc;
                    try
                    {
                        doc = XDocument.Parse(xml, LoadOptions.SetLineInfo);
                    }
                    catch (Exception e)
                    {
                        _errors.Add($"{source.PackId}:{file}: XML is not well formed. {e.Message}");
                        continue;
                    }

                    if (doc.Root == null)
                    {
                        _errors.Add($"{source.PackId}:{file}: document is empty.");
                        continue;
                    }

                    foreach (var element in doc.Root.Elements())
                    {
                        int line = (element as System.Xml.IXmlLineInfo)?.LineNumber ?? 0;
                        var origin = new DefOrigin(source.PackId, file, line);

                        string typeName = element.Name.LocalName;
                        bool isAbstract = string.Equals(
                            (string?)element.Attribute("Abstract"), "true", StringComparison.OrdinalIgnoreCase);
                        string? parentName = (string?)element.Attribute("ParentName");
                        string defName = element.Element("defName")?.Value?.Trim()
                                         ?? (string?)element.Attribute("Name")
                                         ?? string.Empty;

                        if (!isAbstract && string.IsNullOrEmpty(defName))
                        {
                            _errors.Add($"{origin}: <{typeName}> has no defName.");
                            continue;
                        }

                        parsed.Add(new ParsedDef
                        {
                            TypeName = typeName,
                            DefName = string.IsNullOrEmpty(defName) ? (string?)element.Attribute("Name") ?? "" : defName,
                            ParentName = parentName,
                            Abstract = isAbstract,
                            Element = element,
                            Origin = origin,
                        });
                    }
                }
            }
            return parsed;
        }

        // ------------------------------------------------------------ inheritance

        List<ParsedDef> ResolveInheritance(List<ParsedDef> parsed)
        {
            var byName = new Dictionary<string, ParsedDef>(StringComparer.Ordinal);
            foreach (var def in parsed)
            {
                string key = Key(def.TypeName, def.DefName);
                if (!string.IsNullOrEmpty(def.DefName) && byName.ContainsKey(key))
                    _errors.Add($"{def.Origin}: duplicate {def.TypeName} named '{def.DefName}'.");
                else if (!string.IsNullOrEmpty(def.DefName))
                    byName[key] = def;
            }

            foreach (var def in parsed)
            {
                if (def.ParentName == null) continue;
                var chain = new List<ParsedDef>();
                var cursor = def;
                var seen = new HashSet<string>(StringComparer.Ordinal);

                while (cursor.ParentName != null)
                {
                    if (!seen.Add(Key(cursor.TypeName, cursor.DefName)))
                    {
                        _errors.Add($"{def.Origin}: inheritance cycle through '{cursor.DefName}'.");
                        break;
                    }
                    if (!byName.TryGetValue(Key(cursor.TypeName, cursor.ParentName), out var parent))
                    {
                        _errors.Add($"{def.Origin}: ParentName '{cursor.ParentName}' not found for {def.TypeName}.");
                        break;
                    }
                    chain.Add(parent);
                    cursor = parent;
                }

                // Nearest ancestor first, so a child overrides its parent, which overrides its own.
                for (int i = 0; i < chain.Count; i++) MergeInherited(def.Element, chain[i].Element);
            }

            return parsed.Where(d => !d.Abstract).ToList();
        }

        /// <summary>
        /// Copy elements the child does not define. A child that names an element wins outright;
        /// it does not merge into the parent value, because "partially overridden list" is the
        /// kind of rule nobody can predict.
        /// </summary>
        static void MergeInherited(XElement child, XElement parent)
        {
            foreach (var parentChild in parent.Elements())
            {
                string name = parentChild.Name.LocalName;
                if (name == "defName") continue;
                if (child.Element(name) != null) continue;
                child.Add(new XElement(parentChild));
            }
        }

        static string Key(string typeName, string defName) => typeName + "/" + defName;

        // ----------------------------------------------------------------- bind

        List<Def> Bind(List<ParsedDef> parsed)
        {
            var bound = new List<Def>();
            foreach (var p in parsed)
            {
                if (!_typesByName.TryGetValue(p.TypeName, out var type))
                {
                    _errors.Add($"{p.Origin}: unknown Def type <{p.TypeName}>. Register it on the loader.");
                    continue;
                }

                Def def;
                try
                {
                    def = (Def)Activator.CreateInstance(type)!;
                    BindFields(def, type, p.Element, p.Origin);
                }
                catch (Exception e)
                {
                    _errors.Add($"{p.Origin}: could not bind <{p.TypeName}>. {e.Message}");
                    continue;
                }

                def.Origin = p.Origin;
                def.Abstract = p.Abstract;
                bound.Add(def);
            }
            return bound;
        }

        void BindFields(object target, Type type, XElement element, DefOrigin origin)
        {
            foreach (var child in element.Elements())
            {
                string name = child.Name.LocalName;
                var field = type.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    _errors.Add($"{origin}: <{type.Name}> has no field '{name}'.");
                    continue;
                }

                try
                {
                    field.SetValue(target, ParseValue(field.FieldType, child, origin));
                }
                catch (DefLoadException e)
                {
                    _errors.Add(e.Message);
                }
                catch (Exception e)
                {
                    _errors.Add($"{origin}: field '{name}' of <{type.Name}>: {e.Message}");
                }
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<DefRequiredAttribute>() == null) continue;
                object? value = field.GetValue(target);
                bool missing = value == null
                               || (value is string s && s.Length == 0)
                               || (value is int i && i == 0 && element.Element(field.Name) == null);
                if (missing) _errors.Add($"{origin}: <{type.Name}> requires '{field.Name}'.");
            }
        }

        object? ParseValue(Type type, XElement element, DefOrigin origin)
        {
            string text = element.Value.Trim();

            if (type == typeof(string)) return element.Value;
            if (type == typeof(int)) return int.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(long)) return long.Parse(text, CultureInfo.InvariantCulture);
            if (type == typeof(bool)) return bool.Parse(text);
            if (type == typeof(float)) return float.Parse(text, CultureInfo.InvariantCulture);
            if (type.IsEnum)
            {
                if (Enum.TryParse(type, text, ignoreCase: true, out object? parsed)) return parsed;
                throw new DefLoadException(
                    $"{origin}: '{text}' is not a valid {type.Name}. Expected one of: {string.Join(", ", Enum.GetNames(type))}.");
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            {
                var itemType = type.GetGenericArguments()[0];
                var list = (System.Collections.IList)Activator.CreateInstance(type)!;
                foreach (var item in element.Elements()) list.Add(ParseValue(itemType, item, origin));
                return list;
            }

            if (type.IsClass)
            {
                object nested = Activator.CreateInstance(type)!;
                BindFields(nested, type, element, origin);
                return nested;
            }

            throw new DefLoadException($"{origin}: no way to parse a {type.Name}.");
        }

        // ---------------------------------------------------------------- tables

        Dictionary<Type, object> BuildTables(List<Def> bound)
        {
            var tables = new Dictionary<Type, object>();
            foreach (var group in bound.GroupBy(d => d.GetType()))
            {
                // Sorted by name so that handle assignment is stable across runs and machines,
                // independent of file discovery order. Handles go into save files, so this is a
                // determinism requirement rather than tidiness.
                var items = group.OrderBy(d => d.defName, StringComparer.Ordinal).ToArray();
                var tableType = typeof(DefTable<>).MakeGenericType(group.Key);
                var array = Array.CreateInstance(group.Key, items.Length);
                for (int i = 0; i < items.Length; i++) array.SetValue(items[i], i);
                tables[group.Key] = Activator.CreateInstance(
                    tableType,
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    binder: null,
                    args: new object[] { array },
                    culture: null)!;
            }
            return tables;
        }

        // ------------------------------------------------------------ validation

        void ValidateReferences(List<Def> bound, DefDatabase database)
        {
            foreach (var def in bound)
            {
                foreach (var field in def.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var attr = field.GetCustomAttribute<DefReferenceAttribute>();
                    if (attr == null) continue;
                    if (field.FieldType != typeof(string))
                    {
                        _errors.Add($"{def.Origin}: [DefReference] on '{field.Name}' requires a string field.");
                        continue;
                    }

                    string? value = (string?)field.GetValue(def);
                    if (string.IsNullOrEmpty(value))
                    {
                        if (!attr.Optional)
                            _errors.Add($"{def.Origin}: '{field.Name}' must name a {attr.DefType.Name}.");
                        continue;
                    }

                    if (!ReferenceExists(database, attr.DefType, value!))
                        _errors.Add($"{def.Origin}: '{field.Name}' names {attr.DefType.Name} '{value}', which does not exist.");
                }
            }
        }

        static bool ReferenceExists(DefDatabase database, Type defType, string name)
        {
            var tableProperty = typeof(DefDatabase).GetMethod(nameof(DefDatabase.Table))!.MakeGenericMethod(defType);
            object table;
            try
            {
                table = tableProperty.Invoke(database, null)!;
            }
            catch (TargetInvocationException)
            {
                return false; // no Defs of that type were loaded at all
            }

            var tryGet = table.GetType().GetMethod("TryGetHandle")!;
            object?[] args = { name, null };
            return (bool)tryGet.Invoke(table, args)!;
        }

        void ThrowIfErrors()
        {
            if (_errors.Count == 0) return;
            string joined = string.Join(Environment.NewLine, _errors);
            throw new DefLoadException(
                $"Content failed to load with {_errors.Count} error(s):{Environment.NewLine}{joined}");
        }
    }
}
