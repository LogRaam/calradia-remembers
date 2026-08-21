// Code written by Gabriel Mailhot, 21/06/2026.
// NPC-initiated interceptions: this policy decides TWO things a lord's own AI cannot judge on its own.
// Choose() picks WHETHER a lord rides out to force a meeting, and WHY (territorial challenge vs reacting
// to news) - the answer becomes InterceptionBehavior.BeginPursuit, which physically moves the lord's party
// and, once caught, opens the CR chat AUTOMATICALLY with no vanilla menu the player could back out of.
// ShouldAbandonChase() decides when a pursuer gives up. Get either wrong and the player either never sees
// the feature fire, or gets harassed relentlessly (a lord who never gives up, or who is sent again the
// moment the last chase ends): the guards here (per-lord cooldown, one pursuer at a time, notoriety floor,
// outrun/timeout release) are what keeps "lords ride out to meet you" from reading as being hunted.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class InterceptionPolicyTests
   {
      // ── ShouldAbandonChase ─────────────────────────────────────────────────

      // Pins MaxPursuitDays as a hard stop: without it a lord who never physically catches the player
      // (a moving target on a big map) would chase indefinitely, which is exactly the "being hunted"
      // feel the guards in this policy exist to avoid.
      [Test]
      public void GIVEN_a_chase_past_the_timeout_WHEN_judging_THEN_it_is_abandoned()
      {
         InterceptionPolicy.ShouldAbandonChase(5f, 1f, 10f,
            InterceptionPolicy.MaxPursuitDays).Should().BeTrue();
      }

      // ── CanMeetOnLand ──────────────────────────────────────────────────────

      // The baseline the whole guard exists to permit: with both parties ashore the meeting must still
      // be allowed to open, or the sea fix would silently kill the feature everywhere.
      [Test]
      public void GIVEN_both_parties_ashore_WHEN_judging_reachability_THEN_the_meeting_may_be_held()
      {
         InterceptionPolicy.CanMeetOnLand(false, false).Should().BeTrue();
      }

      // The freeze itself: two players reported an endless loading screen on v1.30.16 when a lord who had
      // announced he was riding out reached them while they were sailing. Staging the meeting asks the
      // engine for a conversation scene on the terrain under the party, and open water has none, so the
      // load never completes. The player being at sea must block the meeting outright.
      [Test]
      public void GIVEN_the_player_is_at_sea_WHEN_judging_reachability_THEN_no_meeting_may_be_staged()
      {
         InterceptionPolicy.CanMeetOnLand(true, false).Should().BeFalse();
      }

      // The mirror case, which is also plain fiction: a lord aboard a ship cannot ride out to anyone, and
      // a meeting staged from his side would hit the same missing scene. Blocked whichever of the two
      // took to the water, not just the player.
      [Test]
      public void GIVEN_the_pursuer_is_at_sea_WHEN_judging_reachability_THEN_no_meeting_may_be_staged()
      {
         InterceptionPolicy.CanMeetOnLand(false, true).Should().BeFalse();
      }

      // A hostile lord has no standing to challenge you outside their own land: TerritorialPatrol reads
      // "crossing their lands", so PlayerInOwnTerritory is load-bearing, not just FactionHostileToPlayer.
      [Test]
      public void GIVEN_a_hostile_realm_but_the_player_is_elsewhere_WHEN_choosing_THEN_no_patrol()
      {
         InterceptionPolicy.Choose(Facts(true, false, 90))
                           .Should().Be(InterceptionTrigger.None);
      }

      // The other half of the feature besides territorial challenge: a lord who heard of the player's
      // deeds rides out to react "while it is fresh" (see HeardOfDeed's own doc). No hostility or
      // territory needed - news alone is a full trigger.
      [Test]
      public void GIVEN_a_lord_who_has_heard_of_a_recent_deed_WHEN_choosing_THEN_they_ride_out_to_react()
      {
         InterceptionPolicy.Choose(Facts(heardDeed: true)).Should().Be(InterceptionTrigger.HeardOfDeed);
      }

      // NotorietyThreshold exists because "a traveller of at least this notoriety is worth a hostile
      // lord's challenge" (see the constant's own doc): an unknown nobody should be able to cross
      // hostile territory unmolested. One point below the floor.
      [Test]
      public void GIVEN_a_nobody_in_hostile_land_WHEN_choosing_THEN_no_one_bothers()
      {
         //Arrange — below the notoriety floor and nothing else applies.
         InterceptionPolicy.Choose(Facts(true, true, 49))
                           .Should().Be(InterceptionTrigger.None);
      }

      // Notoriety and territory alone are not enough: without actual hostility a lord at peace with the
      // player has no reason to challenge them, however famous or deep in their land they are.
      [Test]
      public void GIVEN_a_notorious_player_in_hostile_land_but_the_realm_is_at_peace_WHEN_choosing_THEN_no_patrol()
      {
         InterceptionPolicy.Choose(Facts(false, true, 90))
                           .Should().Be(InterceptionTrigger.None);
      }

      // The positive case: all three TerritorialPatrol conditions (hostility, territory, notoriety)
      // together are what the CHANGELOG describes as "a hostile realm's lord challenging a notorious
      // traveller crossing their land" - this pins that the trigger actually fires when they align.
      [Test]
      public void GIVEN_a_notorious_player_in_hostile_land_WHEN_choosing_THEN_a_territorial_patrol_rides_out()
      {
         InterceptionPolicy.Choose(Facts(true, true, 60))
                           .Should().Be(InterceptionTrigger.TerritorialPatrol);
      }

      // AbandonMinDistance is checked FIRST, before the timeout: giving up right as the pursuer is about
      // to physically connect would be the most visible, most frustrating way this policy could fail -
      // a chase that ends one step from succeeding.
      [Test]
      public void GIVEN_almost_caught_even_past_the_timeout_WHEN_judging_THEN_the_pursuer_still_presses_on()
      {
         //Arrange — being right on top of the player overrides the timeout: do not give up on the cusp.
         InterceptionPolicy.ShouldAbandonChase(4f, 8f, 3f,
            InterceptionPolicy.MaxPursuitDays + 3).Should().BeFalse();
      }

      // "One interception at a time" per the class doc: without this guard several lords could converge
      // on the player simultaneously, which is the harassment feel the whole policy is built to avoid.
      [Test]
      public void GIVEN_another_pursuer_is_already_active_WHEN_choosing_THEN_no_second_interception_starts()
      {
         InterceptionPolicy.Choose(Facts(true, true, 90, anotherActive: true))
                           .Should().Be(InterceptionTrigger.None);
      }

      // Pins the ordering in Choose(): the class doc says "a hostile territorial challenge outranks
      // merely reacting to news" - this locks that priority so a future reordering of the two checks
      // would be caught here rather than silently changing which opener the player gets.
      [Test]
      public void GIVEN_both_a_territorial_challenge_and_recent_news_WHEN_choosing_THEN_the_patrol_takes_priority()
      {
         InterceptionPolicy.Choose(Facts(true, true, 70, true))
                           .Should().Be(InterceptionTrigger.TerritorialPatrol);
      }

      // The baseline: with every fact false/zero, nothing should fire. Also pins WouldIntercept as a
      // faithful mirror of Choose() (None both ways), since callers use the bool convenience form too.
      [Test]
      public void GIVEN_nothing_applies_WHEN_choosing_THEN_the_lord_stays_their_course()
      {
         InterceptionPolicy.Choose(Facts()).Should().Be(InterceptionTrigger.None);
         InterceptionPolicy.WouldIntercept(Facts()).Should().BeFalse();
      }

      // The far side of the same boundary as the nobody-in-hostile-land test above: the floor is
      // INCLUSIVE (>=), so a player exactly at NotorietyThreshold is already worth a challenge, not
      // one point shy of it.
      [Test]
      public void GIVEN_notoriety_exactly_at_the_floor_WHEN_choosing_THEN_it_still_triggers()
      {
         InterceptionPolicy.Choose(Facts(true, true, InterceptionPolicy.NotorietyThreshold))
                           .Should().Be(InterceptionTrigger.TerritorialPatrol);
      }

      // Fail safe: a missing facts object must resolve to None, never throw and never accidentally
      // trigger a pursuit the caller did not ask for.
      [Test]
      public void GIVEN_null_facts_WHEN_choosing_THEN_it_is_a_safe_none()
      {
         InterceptionPolicy.Choose(null).Should().Be(InterceptionTrigger.None);
      }

      // The "hopeless chase" case named in ShouldAbandonChase's own doc: a pursuer who plainly cannot
      // force the meeting should stop trying rather than trail the player pointlessly across the map.
      [Test]
      public void GIVEN_the_player_clearly_faster_and_well_clear_WHEN_judging_THEN_the_hopeless_chase_is_given_up()
      {
         //Arrange — player 25% faster, well beyond the encounter range.
         InterceptionPolicy.ShouldAbandonChase(4f, 5f, 20f, 1)
                           .Should().BeTrue();
      }

      // Same AbandonMinDistance override as the timeout case above, but against the speed check this
      // time: proximity beats speed too, since the encounter is imminent whatever the speed gap.
      [Test]
      public void GIVEN_the_player_faster_but_almost_caught_WHEN_judging_THEN_the_pursuer_keeps_pressing()
      {
         //Arrange — within AbandonMinDistance, the encounter is imminent whatever the speed.
         InterceptionPolicy.ShouldAbandonChase(4f, 8f, 3f, 1)
                           .Should().BeFalse();
      }

      // Pins the OutrunSpeedRatio boundary itself (1.15): a slight speed edge must NOT read as
      // "uncatchable", or a pursuer would give up on a chase it could still plausibly win.
      [Test]
      public void GIVEN_the_player_only_marginally_faster_WHEN_judging_THEN_the_chase_continues()
      {
         //Arrange — 4.5/4 = 1.125, below the 1.15 outrun ratio.
         InterceptionPolicy.ShouldAbandonChase(4f, 4.5f, 20f, 1)
                           .Should().BeFalse();
      }

      // A pursuer as fast as or faster than the player can never be "outrun", so this branch of
      // ShouldAbandonChase should never trip regardless of distance or days pursued.
      [Test]
      public void GIVEN_the_pursuer_at_least_as_fast_as_the_player_WHEN_judging_THEN_they_press_on()
      {
         InterceptionPolicy.ShouldAbandonChase(5f, 4f, 30f, 2)
                           .Should().BeFalse();
      }

      // ── ShouldAbandonChase, isClosing (the player-report fix) ─────────────

      // The bug itself: "I'll ride up to them and everything but they won't talk to me" (a player closing
      // the gap on purpose must never be judged outrun just because their instantaneous speed looks 25%
      // faster than the pursuer's). isClosing overrides the outrun branch outright.
      [Test]
      public void GIVEN_the_gap_is_closing_even_past_the_outrun_ratio_WHEN_judging_THEN_the_chase_is_not_abandoned()
      {
         //Arrange: player nominally 25% faster (would read as outrun on its own), but the distance is closing.
         InterceptionPolicy.ShouldAbandonChase(4f, 5f, 20f, 1, isClosing: true)
                           .Should().BeFalse();
      }

      // Same guard against the OTHER give-up path: a chase past MaxPursuitDays must still be given the
      // chance to connect if the player is visibly closing the distance, rather than timing out from
      // under someone who is actively riding to the meeting.
      [Test]
      public void GIVEN_the_gap_is_closing_even_past_the_timeout_WHEN_judging_THEN_the_chase_is_not_abandoned()
      {
         InterceptionPolicy.ShouldAbandonChase(5f, 1f, 10f, InterceptionPolicy.MaxPursuitDays, isClosing: true)
                           .Should().BeFalse();
      }

      // The guard is opt-in (default false): every caller above passes no isClosing argument at all, and
      // must keep behaving exactly as it did before this fix (a silent behavioural change to every other
      // caller would be worse than the bug it fixes).
      [Test]
      public void GIVEN_isClosing_is_not_specified_WHEN_judging_a_stale_chase_THEN_the_default_still_abandons()
      {
         InterceptionPolicy.ShouldAbandonChase(5f, 1f, 10f, InterceptionPolicy.MaxPursuitDays)
                           .Should().BeTrue();
      }

      // The per-lord cooldown is checked before any trigger, absolute: this is the other half of "won't
      // pester you" (CHANGELOG) alongside the single-pursuer guard - a lord who just intercepted the
      // player cannot be sent right back out even if every other condition still screams for it.
      [Test]
      public void GIVEN_this_lord_is_on_cooldown_WHEN_choosing_THEN_nothing_fires_however_strong_the_trigger()
      {
         InterceptionPolicy.Choose(Facts(true, true, 90, true, true))
                           .Should().Be(InterceptionTrigger.None);
      }

      // ── Grudge / AppreciationOwed (the two signature affect ledgers get a proactive face-to-face channel) ──

      // The baseline positive case: a live, unresolved, significant grudge against the player is a full
      // trigger on its own, no hostility/territory/notoriety needed - mirrors HeardOfDeed's own "news alone
      // is a full trigger" shape.
      [Test]
      public void GIVEN_a_lord_who_holds_a_live_grudge_against_the_player_WHEN_choosing_THEN_they_come_to_air_it()
      {
         InterceptionPolicy.Choose(Facts(holdsGrudge: true)).Should().Be(InterceptionTrigger.Grudge);
      }

      // The mirror positive case for the warm ledger: a significant, recent appreciation is likewise a full
      // trigger on its own.
      [Test]
      public void GIVEN_a_lord_who_holds_a_significant_appreciation_of_the_player_WHEN_choosing_THEN_they_come_to_acknowledge_it()
      {
         InterceptionPolicy.Choose(Facts(holdsAppreciation: true)).Should().Be(InterceptionTrigger.AppreciationOwed);
      }

      // Priority pin 1/3: TerritorialPatrol (a hostile realm defending its own land) outranks a grudge - a
      // lord already riding out to challenge a border-crosser does not additionally need a personal grievance
      // to justify the meeting, and the two reasons must never silently swap which one colours the opener.
      [Test]
      public void GIVEN_both_a_territorial_challenge_and_a_live_grudge_WHEN_choosing_THEN_the_patrol_still_takes_priority()
      {
         InterceptionPolicy.Choose(Facts(true, true, 90, holdsGrudge: true))
                           .Should().Be(InterceptionTrigger.TerritorialPatrol);
      }

      // Priority pin 2/3: a grudge (a wrong pressed) outranks merely reacting to fresh news of an unrelated
      // deed - the design brief's own ordering ("Grudge - a wrong pressed" above "HeardOfDeed - fresh news").
      [Test]
      public void GIVEN_both_a_live_grudge_and_recent_news_WHEN_choosing_THEN_the_grudge_takes_priority()
      {
         InterceptionPolicy.Choose(Facts(heardDeed: true, holdsGrudge: true))
                           .Should().Be(InterceptionTrigger.Grudge);
      }

      // Priority pin 3/3: fresh news outranks a standing appreciation - AppreciationOwed is deliberately the
      // LEAST urgent of the four (design brief: "nothing is lost by a kindness waiting one more day"), so it
      // must never pre-empt a lord reacting to something that just happened.
      [Test]
      public void GIVEN_both_recent_news_and_a_significant_appreciation_WHEN_choosing_THEN_the_news_takes_priority()
      {
         InterceptionPolicy.Choose(Facts(heardDeed: true, holdsAppreciation: true))
                           .Should().Be(InterceptionTrigger.HeardOfDeed);
      }

      // The full stack, all four positive facts at once: the SAME guards and priority order must hold
      // together, not just pairwise - TerritorialPatrol wins over everything, exactly as the class doc says.
      [Test]
      public void GIVEN_every_trigger_fact_at_once_WHEN_choosing_THEN_the_full_priority_order_holds()
      {
         InterceptionPolicy.Choose(Facts(true, true, 90, true, holdsGrudge: true, holdsAppreciation: true))
                           .Should().Be(InterceptionTrigger.TerritorialPatrol);
      }

      // The cooldown and single-pursuer guards gate the two new triggers exactly as they gate the older
      // ones - a live grudge, however strong, must never override "this lord already intercepted you
      // recently" or "someone else is already coming", or the two new channels would reopen the harassment
      // risk the guards exist to prevent.
      [Test]
      public void GIVEN_a_live_grudge_but_the_lord_is_on_cooldown_WHEN_choosing_THEN_nothing_fires()
      {
         InterceptionPolicy.Choose(Facts(onCooldown: true, holdsGrudge: true))
                           .Should().Be(InterceptionTrigger.None);
      }

      [Test]
      public void GIVEN_a_significant_appreciation_but_another_pursuer_is_active_WHEN_choosing_THEN_nothing_fires()
      {
         InterceptionPolicy.Choose(Facts(anotherActive: true, holdsAppreciation: true))
                           .Should().Be(InterceptionTrigger.None);
      }

      #region private

      private static InterceptionFacts Facts(
         bool hostile = false,
         bool inTerritory = false,
         int notoriety = 0,
         bool heardDeed = false,
         bool onCooldown = false,
         bool anotherActive = false,
         bool holdsGrudge = false,
         bool holdsAppreciation = false)
         => new() {
            FactionHostileToPlayer = hostile,
            PlayerInOwnTerritory = inTerritory,
            PlayerNotoriety = notoriety,
            HeardRecentPlayerDeed = heardDeed,
            OnCooldown = onCooldown,
            AnotherPursuerActive = anotherActive,
            HoldsLiveGrudgeAgainstPlayer = holdsGrudge,
            HoldsSignificantAppreciation = holdsAppreciation
         };

      #endregion
   }
}