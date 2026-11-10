// Code written by Gabriel Mailhot, 27/10/2026.
// NaturePolicy gives a character, once, perhaps a fear and perhaps a superstition; GiftTastePolicy the kinds of gift
// they love and those that leave them cold, read from the item's own category, and sizes what a gift earns (Gabriel,
// 27/10/2026: "un NPC pourrait [...] preferer certains cadeaux [...] Une Dame pourrait aimer les fourrures", "nous
// pourrions aussi ajouter certaines superstitions et phobies"). If it is wrong, every character fears something, a
// Nord sailor dreads the sea, a character's tastes change from one game to the next, a fur cloak is read as nothing,
// a gift they love earns no more than any other, a cold one costs regard, or the page tells what was never learned.
// EvilPoptart1987 (Nexus, 09/11/2026): "Many characters are rolling a preference for "silver" in gifts. The problem is,
// there is no such item. There is silver ore, which they don't want and jewelry which is what silver is processed into."
// If the silver rules break, characters again love a gift the player can only meet with raw ore.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Character;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NatureAndGiftPolicyTests
   {
      private static IEnumerable<string> Heroes => Enumerable.Range(0, 1000).Select(i => "lord_" + i);

      private static double Share<T>(IEnumerable<T> rolls, System.Func<T, bool> holds) => rolls.Count(holds) / 1000.0;

      // STAKE: most characters fear nothing worth naming; a fear is a note, not a rule of everyone.
      [Test]
      public void GIVEN_many_characters_WHEN_rolled_THEN_a_fear_is_the_exception_and_the_timid_fear_more()
      {
         double plain = Share(Heroes.Select(id => NaturePolicy.Roll(new NatureFacts {HeroId = id})), r => r.Fear != Fear.None);
         double timid = Share(Heroes.Select(id => NaturePolicy.Roll(new NatureFacts {HeroId = id, Traits = new RomanticTraits {Valor = -2}})), r => r.Fear != Fear.None);

         plain.Should().BeInRange(0.28, 0.42);
         timid.Should().BeGreaterThan(plain + 0.15);
      }

      // STAKE: the people weigh it: a sea-going Nord never fears the sea, a Battanian keeps the old ways more than a
      // calculating Imperial.
      [Test]
      public void GIVEN_their_people_WHEN_rolled_THEN_the_culture_weighs()
      {
         Heroes.Select(id => NaturePolicy.Roll(new NatureFacts {HeroId = id, Culture = "nord"})).Should().NotContain(r => r.Fear == Fear.TheSea);

         double battanian = Share(Heroes.Select(id => NaturePolicy.Roll(new NatureFacts {HeroId = id, Culture = "battania"})), r => r.Superstition != Superstition.None);
         double coldImperial = Share(Heroes.Select(id => NaturePolicy.Roll(new NatureFacts {HeroId = id, Culture = "empire", Traits = new RomanticTraits {Calculating = 2}})),
            r => r.Superstition != Superstition.None);
         battanian.Should().BeGreaterThan(coldImperial * 2);
      }

      // STAKE: the same character fears and believes the same on every roll.
      [Test]
      public void GIVEN_the_same_character_WHEN_rolled_twice_THEN_the_same()
      {
         foreach (string id in Heroes.Take(100))
         {
            NaturePolicy.Roll(new NatureFacts {HeroId = id, Culture = "sturgia"}).Fear.Should().Be(NaturePolicy.Roll(new NatureFacts {HeroId = id, Culture = "sturgia"}).Fear);
            GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id}).Loved.Should().Equal(GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id}).Loved);
         }
      }

      // STAKE: nothing on the page until learned; once learned, plain words.
      [Test]
      public void GIVEN_a_fear_and_a_superstition_WHEN_her_page_opens_THEN_only_the_learned_are_named()
      {
         NaturePolicy.PageLines(new NaturePageFacts {Fear = Fear.TheSea, Superstition = Superstition.Omens, IsFemale = true}).Should().BeEmpty();
         NaturePolicy.PageLines(new NaturePageFacts {Fear = Fear.TheSea, FearKnown = true, Superstition = Superstition.Omens, SuperstitionKnown = true, IsFemale = true})
                     .Should().Equal("She fears the sea.", "She reads omens in birds and signs before a battle or a journey.");
      }

      // STAKE: the game knows what was given from the item's own category: a fur cloak is furs, a war horse a horse, a
      // tier-3 sword a weapon; a pack horse or a sack of grain is no gift of taste.
      [Test]
      public void GIVEN_item_categories_WHEN_read_THEN_their_kind_of_gift()
      {
         GiftTastePolicy.KindOf("fur").Should().Be(GiftKind.Furs);
         GiftTastePolicy.KindOf("war_horse").Should().Be(GiftKind.Horses);
         GiftTastePolicy.KindOf("melee_weapons_3").Should().Be(GiftKind.Weapons);
         GiftTastePolicy.KindOf("heavy_armor").Should().Be(GiftKind.Armor);
         GiftTastePolicy.KindOf("sumpter_horse").Should().Be(GiftKind.None);
         GiftTastePolicy.KindOf("grain").Should().Be(GiftKind.None);
         GiftTastePolicy.KindOf(null).Should().Be(GiftKind.None);
      }

      // STAKE: "Une Dame pourrait aimer les fourrures": a Sturgian loves furs far more often than an Imperial, who leans to
      // wine; and every character loves one or two kinds, a kind never both loved and cold.
      [Test]
      public void GIVEN_their_people_WHEN_gift_tastes_are_rolled_THEN_the_culture_weighs_most()
      {
         double sturgianFurs = Share(Heroes.Select(id => GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id, Culture = "sturgia"})), t => t.Loved.Contains(GiftKind.Furs));
         double imperialFurs = Share(Heroes.Select(id => GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id, Culture = "empire"})), t => t.Loved.Contains(GiftKind.Furs));
         double imperialWine = Share(Heroes.Select(id => GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id, Culture = "empire"})), t => t.Loved.Contains(GiftKind.Wine));

         sturgianFurs.Should().BeGreaterThan(imperialFurs * 2.5);
         imperialWine.Should().BeGreaterThan(imperialFurs * 2.5);
         foreach (string id in Heroes)
         {
            GiftTastes t = GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id, Culture = "sturgia"});
            t.Loved.Count.Should().BeInRange(1, 2);
            t.Cold.Count.Should().BeInRange(0, 1);
            t.Loved.Should().NotIntersectWith(t.Cold).And.NotContain(GiftKind.None);
         }
      }

      // STAKE: a gift they love earns twice what its value would; a cold one half, and never costs regard (Gabriel's
      // ruling awaited; this is the overnight default).
      [Test]
      public void GIVEN_a_gift_WHEN_its_regard_is_sized_THEN_loved_doubles_cold_halves_and_none_costs()
      {
         GiftTastePolicy.Warmth(2, GiftWelcome.Loved).Should().Be(4);
         GiftTastePolicy.Warmth(2, GiftWelcome.Ordinary).Should().Be(2);
         GiftTastePolicy.Warmth(2, GiftWelcome.Cold).Should().Be(1);
         GiftTastePolicy.Warmth(1, GiftWelcome.Cold).Should().Be(0);
         GiftTastePolicy.Warmth(0, GiftWelcome.Loved).Should().Be(0);
         GiftTastePolicy.Welcome(GiftKind.Furs, new[] {GiftKind.Furs}, new[] {GiftKind.Weapons}).Should().Be(GiftWelcome.Loved);
         GiftTastePolicy.Welcome(GiftKind.Weapons, new[] {GiftKind.Furs}, new[] {GiftKind.Weapons}).Should().Be(GiftWelcome.Cold);
         GiftTastePolicy.Welcome(GiftKind.None, new[] {GiftKind.Furs}, null).Should().Be(GiftWelcome.Ordinary);
      }

      // STAKE: EvilPoptart1987's report: no character newly rolled loves or shuns "silver", which only raw ore carried.
      [Test]
      public void GIVEN_many_characters_of_every_kind_WHEN_their_gift_tastes_are_rolled_THEN_none_is_given_silver()
      {
         var traits = new RomanticTraits {Calculating = 2, Generosity = -2};
         foreach (string id in Heroes)
         {
            GiftTastes t = GiftTastePolicy.Roll(new GiftTasteFacts {HeroId = id, Traits = traits, IsNoble = false});
            t.Loved.Should().NotContain(GiftKind.Silver);
            t.Cold.Should().NotContain(GiftKind.Silver);
         }
      }

      // STAKE: the game's silverware (category "jewelry") pleases one who loves jewelry; a load of silver ore is an
      // ordinary gift, not the one a character was said to treasure.
      [Test]
      public void GIVEN_silverware_and_silver_ore_WHEN_their_kind_is_read_THEN_silverware_is_jewelry_and_ore_is_nothing_special()
      {
         GiftTastePolicy.KindOf("jewelry").Should().Be(GiftKind.Jewelry);
         GiftTastePolicy.KindOf("silver").Should().Be(GiftKind.None);
      }

      // STAKE: a save from before keeps its characters' tastes, mended: a love of silver becomes a love of jewelry (what
      // silver is made into), learned as such if the player had learned it, and never both loved and shunned.
      [Test]
      public void GIVEN_a_taste_for_silver_rolled_in_an_older_save_WHEN_it_is_mended_THEN_it_reads_as_jewelry()
      {
         GiftTastes lovedSilver = GiftTastePolicy.WithoutSilver(new GiftTastes {Loved = new List<GiftKind> {GiftKind.Silver}, Cold = new List<GiftKind> {GiftKind.Weapons}},
            new[] {GiftKind.Silver}, out List<GiftKind> known);
         lovedSilver.Loved.Should().Equal(GiftKind.Jewelry);
         lovedSilver.Cold.Should().Equal(GiftKind.Weapons);
         known.Should().Equal(GiftKind.Jewelry);

         GiftTastePolicy.WithoutSilver(new GiftTastes {Loved = new List<GiftKind> {GiftKind.Jewelry, GiftKind.Silver}, Cold = new List<GiftKind>()}, null, out _)
                        .Loved.Should().Equal(GiftKind.Jewelry);

         GiftTastes coldSilver = GiftTastePolicy.WithoutSilver(new GiftTastes {Loved = new List<GiftKind> {GiftKind.Jewelry}, Cold = new List<GiftKind> {GiftKind.Silver}}, null, out _);
         coldSilver.Loved.Should().Equal(GiftKind.Jewelry);
         coldSilver.Cold.Should().BeEmpty();
      }

      // STAKE: only the tastes learned by giving reach the page.
      [Test]
      public void GIVEN_gift_tastes_WHEN_her_page_opens_THEN_only_the_learned_are_named()
      {
         var f = new GiftPageFacts {Loved = new[] {GiftKind.Furs, GiftKind.Wine}, Cold = new[] {GiftKind.Weapons}, Known = new[] {GiftKind.Furs, GiftKind.Weapons}, IsFemale = true};

         GiftTastePolicy.PageLines(f).Should().Equal("Gifts that delight her: furs.", "Gifts that leave her cold: weapons.");
         GiftTastePolicy.PageLines(new GiftPageFacts {Loved = new[] {GiftKind.Furs}}).Should().BeEmpty();
      }
   }
}
