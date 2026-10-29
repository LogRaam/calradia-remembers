// Code written by Gabriel Mailhot, 28/10/2026.
// BrawlPolicy says who fights the player with steel and who with fists, and who may start a brawl. Gabriel (28/10/2026):
// the duel is noble, the street brawl a settling of scores, and a companion only brawls. avalanche24 (Nexus, 28/10/2026):
// a companion hired hours before called him out for being told to follow orders; a landowner with no skill at arms called
// out a master for saying no. If this is wrong, those challenges come back with fists, or a real grievance can never be
// settled.

#region

using CalradiaRemembers.Logic.Duels;
using CalradiaRemembers.Logic.Social;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class BrawlPolicyTests
   {
      private static BrawlInitiativeFacts Companion(bool grudge = false, int regard = 0, ArmsGap gap = ArmsGap.Even, int valor = 0)
         => new() {Station = SocialStationPolicy.Companion, HoldsAGrudge = grudge, Regard = regard, Rung = PlayerRung.Sellsword, Gap = gap, Valor = valor};

      // STAKE: Gabriel's line between the two: nobles duel with steel, a companion and the street fight with fists.
      [Test]
      public void GIVEN_each_station_WHEN_asked_how_it_fights_THEN_nobles_duel_and_commoners_brawl()
      {
         BrawlPolicy.KindFor(SocialStationPolicy.Lord).Should().Be(DuelKind.Honour);
         BrawlPolicy.KindFor(SocialStationPolicy.Companion).Should().Be(DuelKind.Brawl);
         BrawlPolicy.KindFor(SocialStationPolicy.GangLeader).Should().Be(DuelKind.Brawl);
         BrawlPolicy.KindFor("preacher").Should().BeNull();
      }

      // STAKE: avalanche24's new hire called him out for being told to follow orders: without a grudge or a soured regard,
      // no companion starts a brawl.
      [Test]
      public void GIVEN_a_companion_with_no_grievance_WHEN_asked_to_call_the_player_out_THEN_she_may_not()
      {
         BrawlPolicy.MayCallOut(Companion()).Should().BeFalse();
      }

      // STAKE: a companion with a real grudge may settle it with fists, as Gabriel's ruling allows.
      [Test]
      public void GIVEN_a_companion_with_a_grudge_WHEN_asked_to_call_the_player_out_THEN_she_may()
      {
         BrawlPolicy.MayCallOut(Companion(grudge: true)).Should().BeTrue();
         BrawlPolicy.MayCallOut(Companion(regard: -25)).Should().BeTrue();
      }

      // STAKE: avalanche24's landowner: a notable never rolls up his own sleeves, whatever his grievance.
      [Test]
      public void GIVEN_a_landowner_with_a_grudge_WHEN_asked_to_call_the_player_out_THEN_he_may_not()
      {
         BrawlPolicy.MayCallOut(new BrawlInitiativeFacts {Station = "ruralnotable", HoldsAGrudge = true, Rung = PlayerRung.Nobody, Gap = ArmsGap.Even}).Should().BeFalse();
      }

      // STAKE: a weakling facing a master of arms keeps his fists in his pockets, unless he is reckless (Valor 2).
      [Test]
      public void GIVEN_a_far_better_fighter_WHEN_a_grudge_holder_weighs_a_brawl_THEN_only_the_reckless_tries()
      {
         BrawlPolicy.MayCallOut(Companion(grudge: true, gap: ArmsGap.FarBelow)).Should().BeFalse();
         BrawlPolicy.MayCallOut(Companion(grudge: true, gap: ArmsGap.FarBelow, valor: 2)).Should().BeTrue();
      }

      // STAKE: Gabriel's "l'echelon du joueur pese lourd": a wanderer with a grudge calls out a nobody, not a lord.
      [Test]
      public void GIVEN_a_wanderer_with_a_grudge_WHEN_the_player_is_a_nobody_or_a_lord_THEN_only_the_nobody_is_called_out()
      {
         BrawlPolicy.MayCallOut(new BrawlInitiativeFacts {Station = SocialStationPolicy.Wanderer, HoldsAGrudge = true, Rung = PlayerRung.Nobody, Gap = ArmsGap.Even}).Should().BeTrue();
         BrawlPolicy.MayCallOut(new BrawlInitiativeFacts {Station = SocialStationPolicy.Wanderer, HoldsAGrudge = true, Rung = PlayerRung.RisingHouse, Gap = ArmsGap.Even}).Should().BeFalse();
      }
   }
}
