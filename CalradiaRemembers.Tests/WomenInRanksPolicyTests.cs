// Code written by Gabriel Mailhot, 14/11/2026.
// WomenInRanksPolicy decides, for each soldier entering a battle, whether she is a woman. Gabriel, 14/11/2026: a share of
// women per culture, Sturgia, Battania and Khuzait the highest (25%), the Nords and the Southern Empire the lowest (10%),
// the other cultures patriarchal (0). If this is wrong: Vlandian or Aserai ranks show women, the Northern and Western
// Empires follow the Southern one (the three share one culture), a lord or companion changes body, another mod's women
// troops are redrawn, or the share seen in battle is far from the one written in the file.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Troops;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class WomenInRanksPolicyTests
   {
      // The shipped women_in_ranks.json.
      private static readonly WomenInRanksTable Shipped = new() {
         Cultures = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {["sturgia"] = 25, ["battania"] = 25, ["khuzait"] = 25, ["nord"] = 10},
         Realms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) {["empire_s"] = 10}
      };

      private static RankSoldierFacts Soldier(string culture, string realm = null, int seed = 1)
         => new() {CultureId = culture, RealmId = realm, TroopId = culture + "_recruit", Seed = seed, HasFemaleFace = true};

      // STAKE (EvilPoptart1987, Nexus 15/11/2026: "ridiculously tiny heads"; Gabriel: "mince et beaucoup moins muscles"): the
      // townswoman range her body was drawn in reaches weight 0.95 and build 0.97, a stout peasant's frame under which her head
      // looked tiny. Whatever the template draws, she is lean; a heavier draw stays a little heavier so the ranks still vary.
      [Test]
      public void GIVEN_a_woman_soldiers_drawn_weight_or_build_WHEN_her_frame_is_set_THEN_it_is_lean_and_still_varies()
      {
         WomenInRanksPolicy.LeanFrame(0.97f).Should().BeLessThanOrEqualTo(WomenInRanksPolicy.LeanFrameHighest);
         WomenInRanksPolicy.LeanFrame(0.97f).Should().BeLessThanOrEqualTo(0.30f);
         WomenInRanksPolicy.LeanFrame(0f).Should().Be(WomenInRanksPolicy.LeanFrameLowest);
         WomenInRanksPolicy.LeanFrame(0.9f).Should().BeGreaterThan(WomenInRanksPolicy.LeanFrame(0.3f));
         WomenInRanksPolicy.LeanFrame(5f).Should().Be(WomenInRanksPolicy.LeanFrameHighest);
         WomenInRanksPolicy.LeanFrame(-1f).Should().Be(WomenInRanksPolicy.LeanFrameLowest);
         WomenInRanksPolicy.LeanFrame(float.NaN).Should().Be(WomenInRanksPolicy.LeanFrameLowest);
      }

      private static double ShareOfWomen(string culture, string realm = null)
         => Enumerable.Range(0, 10000).Count(seed => WomenInRanksPolicy.IsWoman(Shipped, Soldier(culture, realm, seed))) / 100.0;

      // STAKE (Gabriel): the share seen across a battle is the one written in the file.
      [TestCase("sturgia", null, 25)]
      [TestCase("battania", null, 25)]
      [TestCase("khuzait", null, 25)]
      [TestCase("nord", null, 10)]
      [TestCase("empire", "empire_s", 10)]
      public void GIVEN_many_soldiers_of_a_culture_WHEN_they_enter_battle_THEN_the_share_of_women_is_the_one_written(string culture, string realm, int percent)
      {
         ShareOfWomen(culture, realm).Should().BeApproximately(percent, 2.0);
      }

      // STAKE (Gabriel): "Les autres cultures sont patriarcales."
      [TestCase("vlandia")]
      [TestCase("aserai")]
      public void GIVEN_a_patriarchal_culture_WHEN_its_soldiers_enter_battle_THEN_none_is_a_woman(string culture)
      {
         ShareOfWomen(culture).Should().Be(0);
      }

      // STAKE: the three empires share one culture; only the Southern Empire's armies have women, not the Northern or Western.
      [Test]
      public void GIVEN_imperial_soldiers_WHEN_they_fight_for_the_northern_or_western_empire_THEN_none_is_a_woman()
      {
         ShareOfWomen("empire", "empire_n").Should().Be(0);
         ShareOfWomen("empire", "empire_w").Should().Be(0);
      }

      // STAKE: a realm entry wins over the culture: a Sturgian recruit in the Southern Empire's army follows the South.
      [Test]
      public void GIVEN_a_realm_entry_WHEN_a_soldier_of_another_culture_fights_in_its_army_THEN_the_realm_share_applies()
      {
         WomenInRanksPolicy.PercentFor(Shipped, "sturgia", "empire_s").Should().Be(10);
         WomenInRanksPolicy.PercentFor(Shipped, "sturgia", "vlandia").Should().Be(25);
      }

      // STAKE: lords, companions and the player have their own body; a troop already a woman (the game's, another mod's) stays as it is.
      [Test]
      public void GIVEN_a_hero_or_a_troop_already_a_woman_WHEN_drawn_THEN_nothing_changes()
      {
         var table = new WomenInRanksTable {Cultures = new Dictionary<string, int> {["sturgia"] = 100}};

         WomenInRanksPolicy.IsWoman(table, new RankSoldierFacts {CultureId = "sturgia", IsHero = true, HasFemaleFace = true}).Should().BeFalse();
         WomenInRanksPolicy.IsWoman(table, new RankSoldierFacts {CultureId = "sturgia", IsAlreadyFemale = true, HasFemaleFace = true}).Should().BeFalse();
      }

      // STAKE: with no woman's face for the culture she would get a man's face on a woman's body; she stays a man.
      [Test]
      public void GIVEN_a_culture_with_no_womans_face_WHEN_drawn_THEN_the_soldier_stays_a_man()
      {
         var table = new WomenInRanksTable {Cultures = new Dictionary<string, int> {["sturgia"] = 100}};

         WomenInRanksPolicy.IsWoman(table, new RankSoldierFacts {CultureId = "sturgia", HasFemaleFace = false}).Should().BeFalse();
      }

      // STAKE: the same soldier is drawn the same way each time; a soldier does not change between two looks.
      [Test]
      public void GIVEN_the_same_soldier_WHEN_drawn_twice_THEN_the_answer_is_the_same()
      {
         Enumerable.Range(0, 200).All(seed => WomenInRanksPolicy.IsWoman(Shipped, Soldier("sturgia", seed: seed)) == WomenInRanksPolicy.IsWoman(Shipped, Soldier("sturgia", seed: seed)))
                   .Should().BeTrue();
      }

      // STAKE: a player's mistyped share (150, -5) neither throws nor goes beyond all or none.
      [Test]
      public void GIVEN_a_share_outside_0_to_100_WHEN_read_THEN_it_is_held_to_all_or_none()
      {
         var table = new WomenInRanksTable {Cultures = new Dictionary<string, int> {["a"] = 150, ["b"] = -5}};

         WomenInRanksPolicy.PercentFor(table, "a", null).Should().Be(100);
         WomenInRanksPolicy.PercentFor(table, "b", null).Should().Be(0);
      }
   }
}
