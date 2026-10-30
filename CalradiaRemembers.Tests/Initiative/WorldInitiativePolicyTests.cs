// Code written by Gabriel Mailhot, 28/10/2026.
// WorldInitiativePolicy decides whose turn it is to come to the player unasked, across every system that does (a lord's
// audience, a companion's word, a letter, a tale, war and law, a lord's aim). Gabriel (28/10/2026): keep the player in the
// game, "pas le sortir trop souvent vers une gestion de conversation sans que le joueur l'ait initie de lui-meme"; "a qui
// le tour", with priorities that stay coherent and variety "pour ne pas qu'un systeme ecrase les autres"; the arrest, the
// war duty, the ambush and a short-deadline envoy always pass; and "Il ne faut pas que les priorites empechent les autres
// [...] chaque categorie qui est dans la file d'attente puisse etre jouee une fois par mois". If this is wrong, the player
// is pulled out of the game in the middle of a battle or right after their own conversation, one loud system silences the
// rest, or an unhappy wife never gets to say so.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Initiative;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Initiative
{
   [TestFixture]
   public sealed class WorldInitiativePolicyTests
   {
      private const double Day = 24;
      private const double Now = 100 * Day;

      private static InitiativeRequest Req(string key, InitiativeFamily family, InitiativeRank rank = InitiativeRank.Ordinary, double filedDaysAgo = 1,
         string character = null, bool letter = false, double askedHoursAgo = 1)
         => new() {Key = key, Family = family, Rank = rank, FiledAtHour = Now - filedDaysAgo * Day, LastAskedAtHour = Now - askedHoursAgo, CharacterId = character, IsLetter = letter};

      private static InitiativeGrant Turn(InitiativeFamily family, double daysAgo, string character = null, double cost = 1)
         => new() {Key = "past", Family = family, GrantedAtHour = Now - daysAgo * Day, CharacterId = character, Cost = cost};

      private static InitiativeMoment Calm(bool busy = false, double settledHoursAgo = 100, double ownTalkHoursAgo = 1000, int active = 0)
         => new() {NowHour = Now, Busy = busy, HoursSinceSettled = settledHoursAgo, HoursSinceOwnTalk = ownTalkHoursAgo, ActiveCount = active, KeptSinceHour = Now - 5 * Day};

      private static InitiativeDecision Decide(IReadOnlyList<InitiativeRequest> requests, IReadOnlyList<InitiativeGrant> grants = null,
         InitiativeSettings settings = null, InitiativeMoment moment = null, double roll = 0)
         => WorldInitiativePolicy.Decide(requests, grants ?? new List<InitiativeGrant>(), settings ?? new InitiativeSettings(), moment ?? Calm(), roll);

      // STAKE: Gabriel's immersion: nothing pulls the player into a conversation in a battle, a scene, a chat or a menu.
      [Test]
      public void GIVEN_the_player_busy_WHEN_a_request_waits_THEN_nothing_comes()
      {
         Decide(new[] {Req("a", InitiativeFamily.Lords)}, moment: Calm(busy: true)).Verdict.Should().Be(InitiativeVerdict.Busy);
      }

      // STAKE: the player's "delai avant que l'orchestrateur decide de lancer une intervention": just arrived, nothing yet.
      [Test]
      public void GIVEN_the_player_just_settled_WHEN_a_request_waits_THEN_the_world_lets_them_settle_first()
      {
         Decide(new[] {Req("a", InitiativeFamily.Lords)}, moment: Calm(settledHoursAgo: 1)).Verdict.Should().Be(InitiativeVerdict.Settling);
      }

      // STAKE: Gabriel's four (the arrest, the war duty, the ambush, the short-deadline envoy) pass even when the budget
      // is spent, unless the player asked urgent matters to wait their turn.
      [Test]
      public void GIVEN_a_world_imposition_and_a_spent_budget_WHEN_decided_THEN_it_passes_unless_urgent_matters_wait()
      {
         var spent = new[] {Turn(InitiativeFamily.Lords, 1), Turn(InitiativeFamily.Letters, 2), Turn(InitiativeFamily.Tales, 3)};
         InitiativeRequest arrest = Req("arrest", InitiativeFamily.WarAndLaw, InitiativeRank.Imposed);

         InitiativeDecision d = Decide(new[] {arrest, Req("aim", InitiativeFamily.Aims)}, spent);
         d.Granted.Should().BeSameAs(arrest);
         d.Ground.Should().Be(InitiativeGround.Imposed);

         Decide(new[] {arrest}, spent, new InitiativeSettings {UrgentMattersWait = true}).Verdict.Should().Be(InitiativeVerdict.BudgetSpent);
      }

      // STAKE: the player's "combien de systemes peuvent se manifester en meme temps": one lord riding after them is enough.
      [Test]
      public void GIVEN_as_many_systems_at_the_player_as_allowed_WHEN_another_waits_THEN_it_waits()
      {
         Decide(new[] {Req("a", InitiativeFamily.Companions)}, moment: Calm(active: 1)).Verdict.Should().Be(InitiativeVerdict.AtOnceReached);
         Decide(new[] {Req("a", InitiativeFamily.Companions)}, settings: new InitiativeSettings {AtOnce = 2}, moment: Calm(active: 1))
            .Verdict.Should().Be(InitiativeVerdict.Granted);
      }

      // STAKE: Gabriel: not pulled into a conversation "sans que le joueur l'ait initie": after the player's own talk, quiet.
      [Test]
      public void GIVEN_the_player_just_had_a_conversation_of_their_own_WHEN_a_request_waits_THEN_it_waits()
      {
         Decide(new[] {Req("a", InitiativeFamily.Lords)}, moment: Calm(ownTalkHoursAgo: 3)).Verdict.Should().Be(InitiativeVerdict.QuietAfterOwnTalk);
      }

      // STAKE: two knocks back to back are what pulls the player out of the game; the gap holds them apart.
      [Test]
      public void GIVEN_a_turn_given_a_few_hours_ago_WHEN_another_waits_THEN_it_is_too_soon()
      {
         Decide(new[] {Req("a", InitiativeFamily.Lords)}, new[] {Turn(InitiativeFamily.Letters, 0.2)}).Verdict.Should().Be(InitiativeVerdict.TooSoon);
      }

      // STAKE: the budget is the player's cap on how often the world comes; an ordinary matter waits once it is spent.
      [Test]
      public void GIVEN_the_windows_budget_spent_WHEN_an_ordinary_request_waits_THEN_it_waits()
      {
         var spent = new[] {Turn(InitiativeFamily.Aims, 1), Turn(InitiativeFamily.Aims, 2), Turn(InitiativeFamily.Aims, 3)};

         Decide(new[] {Req("a", InitiativeFamily.Aims)}, spent).Verdict.Should().Be(InitiativeVerdict.BudgetSpent);
      }

      // STAKE: Gabriel's unhappy wife "qui ne peut jamais le dire au joueur": a family waiting a month has its turn before a
      // pressing lord and past a spent budget.
      [Test]
      public void GIVEN_the_household_unserved_for_a_month_WHEN_a_pressing_lord_also_waits_and_the_budget_is_spent_THEN_the_wife_has_her_turn()
      {
         InitiativeRequest wife = Req("wife_unhappy", InitiativeFamily.Household, InitiativeRank.Ordinary, character: "wife");
         var spent = new[] {Turn(InitiativeFamily.Lords, 1), Turn(InitiativeFamily.Lords, 2), Turn(InitiativeFamily.Lords, 3), Turn(InitiativeFamily.Household, 40)};

         InitiativeDecision d = Decide(new[] {Req("grudge", InitiativeFamily.Lords, InitiativeRank.Pressing), wife}, spent);

         d.Granted.Should().BeSameAs(wife);
         d.Ground.Should().Be(InitiativeGround.FairTurn);
      }

      // STAKE: coherence: a matter that presses comes before one that can wait, when no family has gone a month unserved.
      [Test]
      public void GIVEN_a_pressing_and_an_ordinary_request_WHEN_decided_THEN_the_pressing_one_comes_first()
      {
         InitiativeRequest grudge = Req("grudge", InitiativeFamily.Lords, InitiativeRank.Pressing);

         Decide(new[] {Req("aim", InitiativeFamily.Aims, filedDaysAgo: 9), grudge}, roll: 0.99).Granted.Should().BeSameAs(grudge);
      }

      // STAKE: variety, "pour ne pas qu'un systeme ecrase les autres": a family that just had its turn weighs less.
      [Test]
      public void GIVEN_a_family_that_just_had_its_turn_WHEN_weighed_THEN_it_weighs_less_than_one_that_did_not()
      {
         var grants = new[] {Turn(InitiativeFamily.Letters, 2)};

         WorldInitiativePolicy.DrawWeight(Req("l", InitiativeFamily.Letters), grants, new InitiativeSettings(), Now)
                              .Should().BeLessThan(WorldInitiativePolicy.DrawWeight(Req("c", InitiativeFamily.Companions), grants, new InitiativeSettings(), Now));
      }

      // STAKE: one character does not come back by another way right after (the road interception, then the audience).
      [Test]
      public void GIVEN_a_character_just_served_WHEN_he_knocks_again_THEN_he_is_all_but_passed_over()
      {
         var grants = new[] {Turn(InitiativeFamily.Lords, 1, "herra")};

         WorldInitiativePolicy.DrawWeight(Req("aim", InitiativeFamily.Aims, character: "herra"), grants, new InitiativeSettings(), Now)
                              .Should().BeLessThan(0.1);
      }

      // STAKE: a family the player weighs at 0 never comes; the world's impositions are not switched off by it.
      [Test]
      public void GIVEN_a_family_weighed_at_zero_WHEN_it_knocks_THEN_it_never_comes_but_an_imposition_still_does()
      {
         var off = new InitiativeSettings {FamilyWeights = new Dictionary<InitiativeFamily, int> {[InitiativeFamily.Tales] = 0, [InitiativeFamily.WarAndLaw] = 0}};

         Decide(new[] {Req("tale", InitiativeFamily.Tales)}, settings: off).Verdict.Should().Be(InitiativeVerdict.NothingWaiting);
         Decide(new[] {Req("ambush", InitiativeFamily.WarAndLaw, InitiativeRank.Imposed)}, settings: off).Verdict.Should().Be(InitiativeVerdict.Granted);
      }

      // STAKE: stale news is dropped rather than delivered late; the world's impositions never expire.
      [Test]
      public void GIVEN_requests_waiting_past_their_days_WHEN_decided_THEN_the_ordinary_ones_are_dropped_and_impositions_kept()
      {
         InitiativeRequest old = Req("old", InitiativeFamily.Tales, filedDaysAgo: 11);
         InitiativeRequest summons = Req("summons", InitiativeFamily.WarAndLaw, InitiativeRank.Imposed, filedDaysAgo: 11);

         InitiativeDecision d = Decide(new[] {old, summons});

         d.Expired.Should().ContainSingle().Which.Should().BeSameAs(old);
         d.Granted.Should().BeSameAs(summons);
      }

      // STAKE: an imposition whose reason went (a spent ambush, an envoy who left) and that stopped asking must not sit in
      // the save forever; a weekly asker, between two asks, must not be dropped.
      [Test]
      public void GIVEN_an_imposition_that_stopped_asking_WHEN_decided_THEN_it_is_dropped_but_a_weekly_asker_is_not()
      {
         InitiativeRequest gone = Req("ambush", InitiativeFamily.WarAndLaw, InitiativeRank.Imposed, filedDaysAgo: 20, askedHoursAgo: 15 * Day);
         InitiativeRequest weekly = Req("divorce", InitiativeFamily.Household, InitiativeRank.Pressing, filedDaysAgo: 8, askedHoursAgo: 7 * Day);

         InitiativeDecision d = Decide(new[] {gone, weekly}, settings: new InitiativeSettings {ExpireDays = 30});

         d.Expired.Should().ContainSingle().Which.Should().BeSameAs(gone);
      }

      // STAKE: a letter closes with a click and waits in the book: by default it costs half a turn.
      [Test]
      public void GIVEN_a_letter_WHEN_its_cost_is_weighed_THEN_it_costs_what_the_player_set()
      {
         WorldInitiativePolicy.CostOf(Req("l", InitiativeFamily.Letters, letter: true), new InitiativeSettings()).Should().Be(0.5);
         WorldInitiativePolicy.CostOf(Req("l", InitiativeFamily.Letters, letter: true), new InitiativeSettings {LettersCountAs = LetterCost.Full}).Should().Be(1);
         WorldInitiativePolicy.CostOf(Req("a", InitiativeFamily.Lords), new InitiativeSettings()).Should().Be(1);
      }

      // STAKE: a lord who asked for a word in a town the player has since left must not keep every other system silent: a
      // request whose system stopped asking is passed over, and the one asking now has its turn.
      [Test]
      public void GIVEN_a_stale_pressing_request_and_a_fresh_ordinary_asker_WHEN_decided_THEN_the_asker_has_its_turn()
      {
         InitiativeRequest gone = Req("audience_left_town", InitiativeFamily.Lords, InitiativeRank.Pressing, askedHoursAgo: 48);
         InitiativeRequest asking = Req("companion_word", InitiativeFamily.Companions);

         Decide(new[] {gone, asking}).Granted.Should().BeSameAs(asking);
      }

      // STAKE: the one dial: Rarely comes less often and waits longer than Often.
      [Test]
      public void GIVEN_the_pace_dial_WHEN_set_rarely_or_often_THEN_rarely_is_the_quieter()
      {
         InitiativeSettings rarely = WorldInitiativePolicy.Preset(InitiativePace.Rarely), often = WorldInitiativePolicy.Preset(InitiativePace.Often);

         (rarely.PerWindow / rarely.WindowDays).Should().BeLessThan(often.PerWindow / often.WindowDays);
         rarely.MinGapHours.Should().BeGreaterThan(often.MinGapHours);
      }
   }
}
