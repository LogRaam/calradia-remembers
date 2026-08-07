// Code written by Gabriel Mailhot, 06/08/2026.
// mediate_peace (third-party war mediation): the pure gate deciding whether the player, as a MEDIATOR talking
// to a FACTION LEADER, may broker peace between that leader's faction and a NAMED enemy faction. This is the
// NEW third-party diplomacy verb; before it, make_peace existed only as a council Parley motion for the
// player's OWN faction, so there was no way to broker peace between OTHER realms in conversation.
//
// WHY IT MATTERS: the policy is consulted at BOTH surfaces around the LLM ("the prompt is advice, the bridge is
// law"): whether mediate_peace is taught at all (EncounterContextBuilder), and whether it actually executes
// (BannerlordGameStateBridge.ExecuteMediatePeace, which calls MakePeaceAction.Apply on the leader's faction and
// the target). Get a gate wrong and either a trusted, high-standing player who SHOULD be able to broker peace
// is refused (the feature is dead), or a low-regard/low-stature nobody, or someone talking to a mere lord with
// no war, appears to end a war that the game never actually ends (a hollow, un-verified promise). The stature
// and regard floors are what keep mediation a weighty act of prestige rather than a trivial button.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PeaceMediationPolicyTests
   {
      // The core positive case the whole verb ships for: a genuine faction leader at war with the target, a
      // player of real noble standing (tier at the floor) whom the leader deeply trusts (regard at the floor)
      // MUST be able to mediate. If this refuses, the feature never fires for anyone.
      [Test]
      public void GIVEN_a_leader_at_war_and_a_trusted_high_standing_player_WHEN_evaluating_THEN_they_can_mediate()
      {
         PeaceMediationPolicy.Evaluate(true, true, PeaceMediationPolicy.RegardFloor, PeaceMediationPolicy.StatureTierFloor)
            .Should().Be(PeaceMediationBlock.None);
         PeaceMediationPolicy.CanMediate(true, true, PeaceMediationPolicy.RegardFloor, PeaceMediationPolicy.StatureTierFloor)
            .Should().BeTrue();
      }

      // Both floors are inclusive ("boundary belongs to the rule"): a player who has earned exactly the deep-trust
      // regard and exactly tier 3 nobility must count as qualified, not need one further point of either before
      // mediation ever becomes possible.
      [Test]
      public void GIVEN_regard_and_tier_exactly_at_their_floors_WHEN_evaluating_THEN_it_passes()
      {
         PeaceMediationPolicy.CanMediate(true, true, 30, 3).Should().BeTrue();
      }

      // Absolute structural gate: a common lord or companion speaks for no realm, so mediation is meaningless
      // however trusted and highborn the player is. Refusing here is what keeps the verb scoped to kings/rulers
      // (npcHero.IsFactionLeader at the bridge) and never taught to an ordinary NPC.
      [Test]
      public void GIVEN_the_npc_is_not_a_faction_leader_WHEN_evaluating_THEN_it_is_refused_even_at_full_regard_and_stature()
      {
         PeaceMediationPolicy.Evaluate(false, true, 100, 6)
            .Should().Be(PeaceMediationBlock.NpcNotFactionLeader);
         PeaceMediationPolicy.CanMediate(false, true, 100, 6).Should().BeFalse();
      }

      // No war, nothing to broker: a leader at peace with the named faction cannot have a peace mediated. This
      // stops the verb appearing to "end a war" that does not exist, a hollow promise the bridge would never
      // honour (FactionManager.IsAtWarAgainstFaction is re-checked at commit).
      [Test]
      public void GIVEN_the_leaders_faction_is_not_at_war_with_the_target_WHEN_evaluating_THEN_there_is_no_war_to_mediate()
      {
         PeaceMediationPolicy.Evaluate(true, false, 100, 6)
            .Should().Be(PeaceMediationBlock.NoWarToMediate);
         PeaceMediationPolicy.CanMediate(true, false, 100, 6).Should().BeFalse();
      }

      // The stature floor is what makes mediation an act of prestige: one does not broker a peace between realms
      // as a landless upstart. Just below tier 3 must refuse, even when the leader already trusts the player, so
      // the player's own standing genuinely gates the verb.
      [Test]
      public void GIVEN_a_trusted_player_below_the_stature_floor_WHEN_evaluating_THEN_stature_is_too_low()
      {
         PeaceMediationPolicy.Evaluate(true, true, 100, PeaceMediationPolicy.StatureTierFloor - 1)
            .Should().Be(PeaceMediationBlock.StatureTooLow);
         PeaceMediationPolicy.CanMediate(true, true, 100, PeaceMediationPolicy.StatureTierFloor - 1)
            .Should().BeFalse();
      }

      // The deep-trust regard bar is what stops a highborn stranger dictating peace to a leader who barely knows
      // them. Just below the floor (even a friend at genuine-regard 10-29) must refuse, so brokering a realm's
      // peace stays reserved for someone the leader esteems in real, tested trust.
      [Test]
      public void GIVEN_a_high_standing_player_the_leader_does_not_deeply_trust_WHEN_evaluating_THEN_regard_is_too_low()
      {
         PeaceMediationPolicy.Evaluate(true, true, PeaceMediationPolicy.RegardFloor - 1, 6)
            .Should().Be(PeaceMediationBlock.RegardTooLow);
         PeaceMediationPolicy.CanMediate(true, true, PeaceMediationPolicy.RegardFloor - 1, 6)
            .Should().BeFalse();
      }

      // Which reason surfaces when several gates fail is real, player-facing behaviour (it grounds the refusal
      // line). Pinning the structural gates ahead of the player's own qualities keeps that report honest: someone
      // talking to a mere lord should be told there is no realm to speak for, never that their standing is short.
      [Test]
      public void GIVEN_a_non_leader_with_no_war_and_a_low_stature_low_regard_player_WHEN_evaluating_THEN_the_leader_gate_is_reported_first()
      {
         PeaceMediationPolicy.Evaluate(false, false, 0, 0)
            .Should().Be(PeaceMediationBlock.NpcNotFactionLeader);
      }

      // Companion gate to the test above: when the NPC IS a leader but there is no war, the "no war" reason must
      // surface ahead of the player's stature/regard, so a player is never told their standing is short when the
      // real obstacle is that the leader has no war to end.
      [Test]
      public void GIVEN_a_leader_with_no_war_and_a_low_stature_low_regard_player_WHEN_evaluating_THEN_the_no_war_gate_is_reported_first()
      {
         PeaceMediationPolicy.Evaluate(true, false, 0, 0)
            .Should().Be(PeaceMediationBlock.NoWarToMediate);
      }
   }
}
