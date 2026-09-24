#nullable enable

using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Presentation.Rendering;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The painted Meadow ground (<c>docs/design/38-meadow-overhaul.md</c> §17): the shader is in the
    /// project, a complete look builds a material that carries every texture, an incomplete one
    /// builds nothing, and the grass keeps the old lift exactly when the painted ground is off.
    ///
    /// <para>None of these needs the packs: the textures are made here. The one question that does —
    /// whether the committed asset resolved on this machine — is asked of the art, not of the asset's
    /// presence (CLAUDE.md), and ignored where it did not.</para>
    /// </summary>
    public class MeadowGroundTests
    {
        readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            MeadowLook.GroundEnabled = true;
            foreach (Object made in _made)
                if (made != null) Object.DestroyImmediate(made);
            _made.Clear();
        }

        Texture2D Texture(string name)
        {
            var texture = new Texture2D(4, 4) { name = name };
            _made.Add(texture);
            return texture;
        }

        MeadowLook Look(bool complete)
        {
            var look = ScriptableObject.CreateInstance<MeadowLook>();
            _made.Add(look);
            look.grassA = Texture("grassA");
            look.grassB = Texture("grassB");
            look.clover = Texture("clover");
            look.flowers = Texture("flowers");
            look.leaves = Texture("leaves");
            look.earth = complete ? Texture("earth") : null;
            return look;
        }

        [Test]
        public void TheGroundShaderIsInTheProject()
        {
            Shader? shader = Shader.Find("Odyssey/MeadowGround");
            Assert.That(shader, Is.Not.Null, "Odyssey/MeadowGround is missing, so the ground falls back to the stock tile");
            Assert.That(shader!.isSupported, Is.True, "the ground shader does not compile on this device");
        }

        [Test]
        public void ACompleteLookBuildsAMaterialCarryingEveryTexture()
        {
            MeadowLook look = Look(complete: true);
            Material? material = MeadowLook.NewGroundMaterial(look);
            Assert.That(material, Is.Not.Null);
            _made.Add(material!);

            Assert.That(material!.enableInstancing, Is.True, "the ground is drawn instanced; a material without it draws nothing");
            Assert.That(material.GetTexture("_GrassA"), Is.SameAs(look.grassA));
            Assert.That(material.GetTexture("_GrassB"), Is.SameAs(look.grassB));
            Assert.That(material.GetTexture("_Clover"), Is.SameAs(look.clover));
            Assert.That(material.GetTexture("_Flowers"), Is.SameAs(look.flowers));
            Assert.That(material.GetTexture("_Leaves"), Is.SameAs(look.leaves));
            Assert.That(material.GetTexture("_Earth"), Is.SameAs(look.earth));
            Assert.That(material.HasProperty("_BaseColor"), Is.True,
                "no _BaseColor, so the depth shade and the zone washes would stop reaching the ground");
        }

        [Test]
        public void AnIncompleteLookBuildsNothing()
        {
            MeadowLook look = Look(complete: false);
            Assert.That(look.HasGround, Is.False);
            Assert.That(MeadowLook.NewGroundMaterial(look), Is.Null,
                "a ground missing one of its textures would draw a black patch wherever that layer shows");
        }

        /// <summary>
        /// The lift that pulled the single olive texture towards lime belongs to the stock ground
        /// only: with the painted ground the pack's own colours stand (owner, 2026-09-24).
        /// </summary>
        [Test]
        public void TheGrassKeepsItsLiftExactlyWhenThePaintedGroundIsOff()
        {
            const int grass = 10;
            MeadowLook.GroundEnabled = false;
            Color stock = StuffPalette.TerrainTint(grass);
            Assert.That(stock, Is.Not.EqualTo(Color.white), "the stock grass lost the lift it was tuned with");

            MeadowLook.GroundEnabled = true;
            if (!MeadowLook.GroundActive)
                Assert.Ignore("the Meadow look's textures did not resolve on this machine");
            Assert.That(StuffPalette.TerrainTint(grass), Is.EqualTo(Color.white),
                "the painted ground is still being lifted towards lime, which is not the pack's colour");
        }
    }
}
