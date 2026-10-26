// Code written by Gabriel Mailhot, 26/10/2026.
// PredecessorMemoryPolicy decides what the old character remembers once he has left the house for a ruler through the
// heir, and how the ruler keeps her romance with him. Gabriel, 26/10/2026: "oui transferer les souvenirs au 'nouveau'
// npc". If this is wrong, the character played for a whole campaign meets his heir as a stranger, or the empress forgets
// the courtship that brought him to her, or his memory swells with every skirmish the chronicle ever recorded.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Dynasty;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PredecessorMemoryPolicyTests
    {
        // STAKE: a long campaign records hundreds of deeds; he keeps the weightiest, or a looter camp outranks a city taken.
        [Test]
        public void GIVEN_a_long_chronicle_WHEN_he_remembers_THEN_he_keeps_the_weightiest_deeds_in_the_order_they_happened()
        {
            var deeds = Enumerable.Range(1, 40).Select(i => new PastDeed {Day = i, Magnitude = i % 10, Summary = $"deed {i}"}).ToList();
            deeds.Add(new PastDeed {Day = 5, Magnitude = 95, Summary = "took Pravend"});

            List<NotableEvent> life = PredecessorMemoryPolicy.OwnLife(deeds);

            life.Should().HaveCount(PredecessorMemoryPolicy.MostDeedsKept);
            life.Select(e => e.summary).Should().Contain("took Pravend");
            life.Select(e => e.gameDay).Should().BeInAscendingOrder();
            life.Should().OnlyContain(e => e.type == NotableEventType.OwnLife);
        }

        // STAKE: met by the heir, he must know why he is at the empress's side and what the heir is to him.
        [Test]
        public void GIVEN_he_left_the_house_WHEN_he_remembers_THEN_he_knows_he_gave_it_to_his_heir_to_wed_her()
        {
            List<NotableEvent> leaving = PredecessorMemoryPolicy.TheLeaving(new PredecessorFacts {
                Name = "Garios", HeirName = "Lucon", HeirIsTheir = "son", RulerName = "Rhagaea", HouseName = "the Pethroi", RulerIsFemale = true
            }, 300);

            leaving.Should().Contain(e => e.type == NotableEventType.OwnLife && e.summary.Contains("Rhagaea"));
            leaving.Should().Contain(e => e.type == NotableEventType.Agreement && e.summary.Contains("Lucon, my son"));
        }

        // STAKE: the succession drops a ruler's private history with "the player", who is now the heir; kept as her own
        // life by his name, the courtship that brought him to her survives, and is never read as one with his heir.
        [Test]
        public void GIVEN_her_romance_with_him_WHEN_he_has_left_the_house_THEN_she_keeps_it_as_her_own_life_by_his_name()
        {
            NotableEvent kept = PredecessorMemoryPolicy.KeptAsHerOwnLife(new NotableEvent(40, NotableEventType.Intimacy, "Spent the night with the player."), "Garios");

            kept.type.Should().Be(NotableEventType.OwnLife);
            kept.summary.Should().Contain("Garios").And.NotContain("the player");
        }

        // STAKE: only her romance changes; her dealings with the house stay history the heir inherits.
        [Test]
        public void GIVEN_a_political_memory_WHEN_he_has_left_the_house_THEN_it_is_left_as_it_was()
        {
            var pact = new NotableEvent(10, NotableEventType.Agreement, "Sealed a pact with the player.");

            PredecessorMemoryPolicy.KeptAsHerOwnLife(pact, "Garios").Should().BeSameAs(pact);
        }
    }
}
