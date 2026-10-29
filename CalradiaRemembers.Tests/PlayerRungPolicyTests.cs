// Code written by Gabriel Mailhot, 28/10/2026.
// PlayerRungPolicy places the player on five rungs of standing and reads who is the better fighter, for the line every
// character is given about the player. Gabriel (28/10/2026): "Un paysan qui parle au joueur niveau 1 sera plus rude que si
// il parle au joueur niveau 20 Clan tier 3". avalanche24 (Nexus, 28/10/2026): a companion "with his best stat less than
// 100" spoke of winning a war beside a player who "kills 100+ enemies in almost every battle". If this is wrong, a
// villager treats a great lord like a vagrant, or a weakling speaks to a master of arms as his better.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Battles;
using CalradiaRemembers.Logic.Social;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerRungPolicyTests
   {
      private static CombatantFacts Fighter(int weapon, int level) => new() {BestWeaponSkill = weapon, Riding = 50, Athletics = 50, Level = level};

      // STAKE: Gabriel's own pair: a level 1 with no house is a nobody to the world; a level 20 heading a tier 3 house is not.
      [Test]
      public void GIVEN_gabriels_two_players_WHEN_placed_THEN_the_fresh_one_is_a_nobody_and_the_tier_three_one_heads_a_rising_house()
      {
         PlayerRungPolicy.Rung(new PlayerRungFacts {Stature = new Stature(0, false), Level = 1}).Should().Be(PlayerRung.Nobody);
         PlayerRungPolicy.Rung(new PlayerRungFacts {Stature = new Stature(3, false), Level = 20}).Should().Be(PlayerRung.RisingHouse);
      }

      // STAKE: a crown outranks every house, and a vassal with a fief is a lord of the realm, whatever else is true.
      [Test]
      public void GIVEN_a_ruler_and_a_landed_vassal_WHEN_placed_THEN_sovereign_and_lord_of_the_realm()
      {
         PlayerRungPolicy.Rung(new PlayerRungFacts {Stature = new Stature(2, true)}).Should().Be(PlayerRung.Sovereign);
         PlayerRungPolicy.Rung(new PlayerRungFacts {Stature = new Stature(2, false), SwornVassal = true, HoldsFief = true}).Should().Be(PlayerRung.LordOfTheRealm);
      }

      // STAKE: avalanche24 is a mercenary captain without lands: a sword for hire, neither a nobody nor a lord.
      [Test]
      public void GIVEN_a_landless_mercenary_captain_WHEN_placed_THEN_a_sword_for_hire()
      {
         PlayerRungPolicy.Rung(new PlayerRungFacts {Stature = new Stature(0, false), Mercenary = true, Level = 10}).Should().Be(PlayerRung.Sellsword);
      }

      // STAKE: avalanche24's companion, best skill under 100, beside a master of 330: he must know he is far below.
      [Test]
      public void GIVEN_a_weak_companion_beside_a_master_of_arms_WHEN_compared_THEN_he_knows_the_player_is_far_better()
      {
         ArmsGap gap = PlayerRungPolicy.Gap(Fighter(90, 8), Fighter(330, 40));

         gap.Should().Be(ArmsGap.FarBelow);
         PlayerRungPolicy.GapLine(gap).Should().Contain("far better fighter than you");
      }

      // STAKE: two fighters of a like skill are not told one outclasses the other; the line says nothing at arms.
      [Test]
      public void GIVEN_two_fighters_of_like_skill_WHEN_compared_THEN_nothing_is_said_at_arms()
      {
         ArmsGap gap = PlayerRungPolicy.Gap(Fighter(200, 20), Fighter(210, 21));

         gap.Should().Be(ArmsGap.Even);
         PlayerRungPolicy.Line(PlayerRung.RisingHouse, gap).Should().NotContain("fighter");
      }

      // STAKE: a skill point gained mid-campaign must not move the line (it sits in the cached prompt): the gap moves only
      // across a wide band.
      [Test]
      public void GIVEN_a_skill_point_gained_WHEN_compared_again_THEN_the_gap_is_unchanged()
      {
         PlayerRungPolicy.Gap(Fighter(150, 15), Fighter(330, 40)).Should().Be(PlayerRungPolicy.Gap(Fighter(151, 15), Fighter(330, 40)));
      }
   }
}
