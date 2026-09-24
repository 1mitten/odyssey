#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using Odyssey.Hud;
using Odyssey.Presentation.Bootstrap;
using Odyssey.Presentation.World;
using Odyssey.Sim.Contracts;
using Odyssey.Sim.Defs;
using Odyssey.Sim.Pawns;
using UnityEngine;

namespace Odyssey.Tests.Presentation
{
    /// <summary>
    /// The blood seam (design 33 §7d): <see cref="CombatFeedback"/> hands every landed hit to
    /// <see cref="IBloodEffects.Spurt"/>, sharp or blunt by the weapon, and every down and death
    /// to <see cref="IBloodEffects.Pool"/>; a miss and a dodge reach it not at all; a world change
    /// clears it. Nothing is drawn — the default is a no-op — so the seam is proven by a recorder.
    /// </summary>
    public class BloodSeamTests
    {
        sealed class Recorder : IBloodEffects
        {
            public readonly List<(Vector3 Feet, Vector3 Wound, Vector3 Direction, float Amount, bool Sharp)> Spurts =
                new List<(Vector3, Vector3, Vector3, float, bool)>();
            public readonly List<(PawnId Who, Vector3 At, float Size, float Length)> Pools =
                new List<(PawnId, Vector3, float, float)>();
            public int Clears;

            public void Spurt(Vector3 feet, Vector3 wound, Vector3 direction, float amount, bool sharp) =>
                Spurts.Add((feet, wound, direction, amount, sharp));

            public void Pool(PawnId who, Vector3 feet, float sizeFactor, float bodyLength) =>
                Pools.Add((who, feet, sizeFactor, bodyLength));

            public void Clear() => Clears++;
        }

        static readonly PawnId Attacker = new PawnId(1), Victim = new PawnId(2);

        static WorldSnapshot Frame(params CombatEventView[] events)
        {
            var snapshot = new WorldSnapshot();
            snapshot.BeginWrite(500, new GridSize(12, 12, 4), 0);
            snapshot.AddPawn(new PawnView(Attacker, new CellRef(1, 1, 0), 800, 800, 600, flags: PawnFlags.Person));
            snapshot.AddPawn(new PawnView(Victim, new CellRef(3, 1, 0), 800, 800, 600, kind: PawnKindIndex.Marauder,
                flags: PawnFlags.Person | PawnFlags.Hostile));
            foreach (CombatEventView e in events) snapshot.AddCombatEvent(e);
            return snapshot;
        }

        static CombatEventView Event(int id, CombatEventKind kind, int amount = 0, int weapon = -1) =>
            new CombatEventView(id, 400 + id, kind, Attacker, Victim, new CellRef(3, 1, 0), amount, weapon);

        [Test]
        public void HitsSpurtDownsAndDeathsPoolAndMissesDrawNothing()
        {
            var blood = new Recorder();
            var feedback = new CombatFeedback
            {
                Blood = blood,
                BloodSides = new BloodSides(new bool?[] { false, true }, new bool?[] { null, null, null, null }, false),
            };

            object world = new object();
            feedback.Consume(Frame(), world, null, null);
            Assert.That(blood.Clears, Is.EqualTo(1), "a world seen for the first time clears what the last one left");

            feedback.Consume(Frame(
                Event(1, CombatEventKind.Swing, amount: 20, weapon: 1),
                Event(2, CombatEventKind.Hit, amount: 7_500, weapon: 1),
                Event(3, CombatEventKind.Miss, weapon: 1),
                Event(4, CombatEventKind.Dodge, weapon: 1),
                Event(5, CombatEventKind.Hit, amount: 4_000, weapon: 0),
                Event(6, CombatEventKind.Stun, amount: 60, weapon: 0),
                Event(7, CombatEventKind.Hit, amount: 3_000),
                Event(8, CombatEventKind.Downed),
                Event(9, CombatEventKind.Died)), world, null, null);

            Assert.That(blood.Spurts.Count, Is.EqualTo(3), "every landed hit, and only those");
            Assert.That(blood.Spurts[0].Sharp, Is.True, "the sharp weapon cut blunt");
            Assert.That(blood.Spurts[0].Amount, Is.EqualTo(7.5f).Within(1e-5f), "the damage is in whole points");
            Assert.That(blood.Spurts[1].Sharp, Is.False, "the blunt weapon cut sharp");
            Assert.That(blood.Spurts[2].Sharp, Is.False, "fists cut sharp");

            // The attacker stands to the west, so the blow travels east.
            Vector3 along = blood.Spurts[0].Direction;
            Assert.That(along.x, Is.GreaterThan(0.99f), $"the blow travelled {along}");
            Assert.That(along.y, Is.EqualTo(0f));
            Assert.That(blood.Spurts[0].Wound.y, Is.GreaterThan(blood.Spurts[0].Feet.y), "a spurt starts at the wound, not the feet");
            Assert.That(blood.Spurts[0].Feet.y, Is.EqualTo(blood.Pools[0].At.y).Within(1e-4f), "and lands on the ground the body stands on");

            Assert.That(blood.Pools.Count, Is.EqualTo(2), "a down and a death");
            Assert.That(blood.Pools[0].Size, Is.EqualTo(BloodModel.PoolSize(CombatEventKind.Downed)));
            Assert.That(blood.Pools[1].Size, Is.EqualTo(BloodModel.PoolSize(CombatEventKind.Died)));
            Assert.That(blood.Pools[0].Who, Is.EqualTo(Victim), "a pool is under the body that fell");
            Assert.That(blood.Pools[0].Length, Is.EqualTo(BloodSpray.PersonLength), "a person is a person's length");

            feedback.Consume(Frame(), new object(), null, null);
            Assert.That(blood.Clears, Is.EqualTo(2), "a load kept the last world's blood");
        }

        /// <summary>
        /// The table is read off the content, which is the one owner of which weapon cuts: the
        /// owner's bat and crowbar blunt, machete and arc blade sharp, fists blunt, a rat's teeth
        /// sharp. The hog's tusks are blunt in <c>Species.xml</c>; §7d records that the owner said
        /// "bites" were sharp.
        /// </summary>
        [Test]
        public void TheContentSaysWhichBlowsCut()
        {
            BloodSides sides = CombatFeedback.BloodSidesOf(ContentPack.Pawns());
            Assert.That(sides.IsSharp(ItemHandle.Bat, PawnKindIndex.Colonist), Is.False);
            Assert.That(sides.IsSharp(ItemHandle.Crowbar, PawnKindIndex.Colonist), Is.False);
            Assert.That(sides.IsSharp(ItemHandle.Machete, PawnKindIndex.Colonist), Is.True);
            Assert.That(sides.IsSharp(ItemHandle.ArcBlade, PawnKindIndex.Colonist), Is.True);
            Assert.That(sides.IsSharp(-1, PawnKindIndex.Colonist), Is.False, "fists");
            Assert.That(sides.IsSharp(-1, PawnKindIndex.Marauder), Is.False, "a marauder's fists");
            Assert.That(sides.IsSharp(-1, PawnKindIndex.DuctRat), Is.True, "a rat's bite");
            Assert.That(sides.IsSharp(-1, PawnKindIndex.MiddenHog), Is.False, "the hog's tusks, as the content has them");
        }

        [Test]
        public void TheDefaultSeamDrawsNothingAndTheDefaultTableIsBlunt()
        {
            var feedback = new CombatFeedback();
            Assert.That(feedback.Blood, Is.SameAs(NoBloodEffects.Instance));
            Assert.That(feedback.BloodSides.IsSharp(ItemHandle.Machete, 0), Is.False);
        }
    }
}
