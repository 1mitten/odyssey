#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using Odyssey.Sim.Pawns;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The join between an item def index in the simulation and the art the renderer draws for it.
    ///
    /// This is a table indexed by an integer that lives in another assembly, which is the kind of
    /// coupling that breaks silently: add a third item to <see cref="ItemIndex"/> and nothing
    /// fails to compile, nothing throws, and the new item simply draws as the orange stand-in
    /// marker for ever. That marker on every item was the visible symptom the first time round.
    /// </summary>
    public class ModuleIdTests
    {
        [Test]
        public void EveryItemDefHasAModule()
        {
            Assert.That(ModuleIds.ItemModuleCount, Is.EqualTo(ItemIndex.Count),
                "an item def index with no module id draws as the stand-in marker");
        }

        [Test]
        public void ItemModulesAreDistinctAndNamespaced()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < ItemIndex.Count; i++)
            {
                string? id = ModuleIds.Item(i);
                Assert.That(id, Is.Not.Null, $"item def {i} has no module id");
                Assert.That(id, Does.StartWith(ModuleIds.Prefix));
                Assert.That(seen.Add(id!), Is.True, $"{id} is claimed by two item defs");
            }
        }

        [Test]
        public void AnUnknownItemDefHasNoModule()
        {
            // The renderer relies on null here rather than on a bounds check of its own: a def
            // index from a future save or a mod must degrade to the marker, not throw mid-frame.
            Assert.That(ModuleIds.Item(-1), Is.Null);
            Assert.That(ModuleIds.Item(ItemIndex.Count), Is.Null);
        }
    }
}
