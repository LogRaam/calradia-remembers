// Code written by Gabriel Mailhot, 01/11/2026.
// CaptorAudiencePolicy decides when a lord sends for his prisoner and what kind of talk it is, and the parole ledger holds the
// player to the oath that can end it. If it is wrong, a captive is back in Windmolen's report (26/10/2026: "just execute you or
// let you free or ransom you"), a sworn enemy is offered a way out that cannot happen, or an oath costs nothing.

#region

using System;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NpcMemoryService.Core.Models;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class CaptorAudiencePolicyTests
   {
      // Rolls the top of the range, to reach the last weight, or the bottom, to reach the first.
      private static readonly Func<int, int> First = total => 0;

      private static CaptiveSceneIntent? Pick(CaptorAudienceFacts f, CaptiveSceneIntent wanted)
      {
         for (int roll = 0; roll < 500; roll++)
         {
            int r = roll;
            if (CaptorAudiencePolicy.Select(f, total => r % Math.Max(total, 1)) == wanted) return wanted;
         }

         return null;
      }

      [Test]
      public void GIVEN_a_new_captive_WHEN_the_first_day_passes_THEN_no_audience_is_due_yet()
      {
         // STAKE: Gabriel's "go": a first talk around the second day; a captor at the cell door at once is what the old daily scenes felt like.
         CaptorAudiencePolicy.IsDue(CaptorAudiencePolicy.FirstAudienceDay - 1, null).Should().BeFalse();
         CaptorAudiencePolicy.IsDue(CaptorAudiencePolicy.FirstAudienceDay, null).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_audience_this_week_WHEN_the_days_pass_THEN_the_next_waits_a_full_week()
      {
         // STAKE: Gabriel's "go": at most one a week; at levels that never had such scenes, they must not become a nuisance.
         CaptorAudiencePolicy.IsDue(20, CaptorAudiencePolicy.DaysBetweenAudiences - 1).Should().BeFalse();
         CaptorAudiencePolicy.IsDue(20, CaptorAudiencePolicy.DaysBetweenAudiences).Should().BeTrue();
      }

      [Test]
      public void GIVEN_a_vendetta_WHEN_a_talk_is_drawn_THEN_nothing_is_offered()
      {
         // STAKE: CaptorReleasePolicy refuses every release at this grudge; a bargain here would be a promise the bridge then breaks.
         CaptorAudiencePolicy.Select(new CaptorAudienceFacts {HeaviestGrudge = CaptorReleasePolicy.VendettaGrudgeFloor, Honor = 2}, First).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_lord_without_any_reason_to_talk_WHEN_a_talk_is_drawn_THEN_he_can_simply_keep_the_prisoner()
      {
         // STAKE: the design's indifferent captor: a roll that lands on him leaves the player waiting, not in a scene.
         CaptorAudiencePolicy.Select(new CaptorAudienceFacts(), First).Should().BeNull();
      }

      [Test]
      public void GIVEN_kin_or_an_old_friend_WHEN_a_talk_is_drawn_THEN_the_torn_friend_can_come()
      {
         // STAKE: DepaHN's kin who turn on the player, and the roadmap's "a captor who is a friend of old, torn".
         Pick(new CaptorAudienceFacts {IsKin = true}, CaptiveSceneIntent.TornFriend).Should().NotBeNull();
         Pick(new CaptorAudienceFacts {Regard = 20}, CaptiveSceneIntent.TornFriend).Should().NotBeNull();
         Pick(new CaptorAudienceFacts {Regard = 5}, CaptiveSceneIntent.TornFriend).Should().BeNull();
      }

      [Test]
      public void GIVEN_a_commoner_clan_WHEN_a_talk_is_drawn_THEN_nobody_plays_host()
      {
         // STAKE: the roadmap: "a guest of rank". Courtesy to a nobody would read as a bug, not as a code of honour.
         Pick(new CaptorAudienceFacts {Honor = 2, Mercy = 2, PlayerClanTier = CaptorAudiencePolicy.GuestOfRankTier - 1}, CaptiveSceneIntent.Courtesy).Should().BeNull();
         Pick(new CaptorAudienceFacts {Honor = 2, Mercy = 2, PlayerClanTier = CaptorAudiencePolicy.GuestOfRankTier}, CaptiveSceneIntent.Courtesy).Should().NotBeNull();
      }

      [Test]
      public void GIVEN_an_empty_purse_WHEN_a_talk_is_drawn_THEN_no_bargain_opens()
      {
         // STAKE: the mod declares what is possible before the model speaks; a price the player cannot meet is a scene with no way out.
         Pick(new CaptorAudienceFacts {Calculating = 2, CanPayTheRansom = false}, CaptiveSceneIntent.Bargain).Should().BeNull();
         Pick(new CaptorAudienceFacts {Calculating = 2, CanPayTheRansom = true}, CaptiveSceneIntent.Bargain).Should().NotBeNull();
      }

      [Test]
      public void GIVEN_a_man_without_honour_WHEN_a_talk_is_drawn_THEN_he_does_not_ask_for_an_oath()
      {
         // STAKE: an oath is worth something only to someone who keeps his own.
         Pick(new CaptorAudienceFacts {Honor = 0}, CaptiveSceneIntent.Parole).Should().BeNull();
         Pick(new CaptorAudienceFacts {Honor = 1}, CaptiveSceneIntent.Parole).Should().NotBeNull();
      }

      [Test]
      public void GIVEN_hardcore_WHEN_the_captor_is_cruel_or_kind_THEN_the_share_of_talk_follows_him()
      {
         // STAKE: Gabriel: these scenes add to the draw at Hardcore, and an honourable lord leans to them; cruelty keeps the old scenes.
         CaptorAudiencePolicy.HardcoreShare(new CaptorAudienceFacts {Mercy = -2, Honor = -2}).Should().Be(10);
         CaptorAudiencePolicy.HardcoreShare(new CaptorAudienceFacts()).Should().Be(30);
         CaptorAudiencePolicy.HardcoreShare(new CaptorAudienceFacts {Mercy = 2, Honor = 2}).Should().Be(80);
      }

      [Test]
      public void GIVEN_each_kind_of_talk_WHEN_it_opens_THEN_it_is_never_a_sexual_scene()
      {
         // STAKE: these scenes run below Hardcore; a sexual rule set reaching them would break the player's content level.
         foreach (CaptiveSceneIntent intent in new[] {CaptiveSceneIntent.Courtesy, CaptiveSceneIntent.Bargain, CaptiveSceneIntent.Parole, CaptiveSceneIntent.TornFriend})
         {
            CaptiveSceneIntentRules.IsSexual(intent).Should().BeFalse();
            CaptiveSceneIntentRules.IsAudience(intent).Should().BeTrue();
         }

         CaptiveSceneIntentRules.IsAudience(CaptiveSceneIntent.Interrogation).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_oath_WHEN_the_player_attacks_that_realm_in_time_THEN_it_is_broken()
      {
         // STAKE: an oath the game does not hold the player to is a free key to the cell.
         CaptorAudiencePolicy.BreaksParole(new ParoleFightFacts {PlayerAttacked = true, EnemyFactionId = "k", SwornFactionId = "k", Today = 10, UntilDay = 10}).Should().BeTrue();
      }

      [Test]
      public void GIVEN_an_oath_WHEN_that_realm_attacks_the_player_THEN_it_stands()
      {
         // STAKE: nobody swears away the right to defend themselves; a patrol must not turn the player into a perjurer.
         CaptorAudiencePolicy.BreaksParole(new ParoleFightFacts {PlayerAttacked = false, EnemyFactionId = "k", SwornFactionId = "k", Today = 10, UntilDay = 10}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_oath_WHEN_its_term_is_over_or_another_realm_is_attacked_THEN_it_stands()
      {
         // STAKE: the oath is sixty days against one realm, not a lifelong peace with the world.
         CaptorAudiencePolicy.BreaksParole(new ParoleFightFacts {PlayerAttacked = true, EnemyFactionId = "k", SwornFactionId = "k", Today = 11, UntilDay = 10}).Should().BeFalse();
         CaptorAudiencePolicy.BreaksParole(new ParoleFightFacts {PlayerAttacked = true, EnemyFactionId = "x", SwornFactionId = "k", Today = 10, UntilDay = 10}).Should().BeFalse();
      }

      [Test]
      public void GIVEN_an_oath_WHEN_saved_and_read_THEN_it_still_binds()
      {
         // STAKE: a reload must not free the player of their word.
         var ledger = new CaptorParoleLedger();
         ledger.Add(new CaptorOathTaken {CaptorId = "lord", FactionId = "k", FactionName = "The Realm", Today = 100});

         CaptorParoleLedger read = CaptorParoleLedger.Deserialize(ledger.Serialize());

         read.Oaths.Should().ContainSingle(o => o.CaptorId == "lord" && o.FactionId == "k" && o.UntilDay == 100 + CaptorAudiencePolicy.ParoleDays);
         read.NextId.Should().Be(2);
      }

      [TestCase(null)]
      [TestCase("")]
      [TestCase("{broken")]
      public void GIVEN_an_old_or_broken_save_WHEN_read_THEN_no_oath_binds(string json)
      {
         // STAKE: the save-migration rule: a save from before this feature never bricks a campaign.
         CaptorParoleLedger.Deserialize(json).Oaths.Should().BeEmpty();
      }
   }
}
