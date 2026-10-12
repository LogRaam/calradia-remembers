// Code written by Gabriel Mailhot, 30/09/2026.
// The one door every character's initiative toward the player goes through (Gabriel, 30/09/2026, after Nexusrrr's baby
// son asked to discuss warfare). An inventory found about a dozen paths, each with its own filter, two of them checking
// age, several trusting Hero.IsLord (an occupation, not an age). If this gate lets a child through, a baby writes
// threats or sends killers; if it lets a prisoner through, a captive mails the player from his cell; if it lets the dead
// or the player through, the world acts in the name of someone who cannot act.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class NpcActionGateTests
   {
      // A free, living adult who is not the player: the ordinary case, and it must stay open.
      [Test]
      public void GIVEN_a_free_living_adult_WHEN_they_would_take_the_initiative_THEN_they_may()
      {
         NpcActionGate.Allows(new ActorFacts()).Should().BeTrue();
      }

      // Nexusrrr's report, whatever the path: children answer, they do not take the initiative. A child of a lord's
      // house passes Hero.IsLord, so rank is never read as age here.
      [Test]
      public void GIVEN_a_child_WHEN_they_would_take_the_initiative_THEN_it_does_not_happen()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}).Should().Be(ActionRefusal.Child);
      }

      // Lordfadooboo and crazyblox (Nexus, 12/10/2026): a new Story Mode game hung on the loading screen of the training
      // field. The elder brother rides in the player's party there, so since 2.7.8 (family asks for a private word) he
      // could ask for one as the tutorial's mission opened. The game itself disables every interaction during its
      // tutorial ("Interaction with this person is disabled during tutorial stage"); CR's characters keep that rule.
      [Test]
      public void GIVEN_the_story_modes_tutorial_restricts_interaction_WHEN_anyone_would_take_the_initiative_THEN_it_does_not_happen()
      {
         NpcActionGate.Check(new ActorFacts {TutorialRestrictsInteraction = true}, ActionKind.CompanionAudience).Should().Be(ActionRefusal.TutorialUnderWay);
         NpcActionGate.Check(new ActorFacts {TutorialRestrictsInteraction = true}, ActionKind.Letter).Should().Be(ActionRefusal.TutorialUnderWay);
      }

      // A captive sends no courier and rides out to no one (the letters' own M15 rule, now everyone's).
      [Test]
      public void GIVEN_a_prisoner_WHEN_they_would_take_the_initiative_THEN_it_does_not_happen()
      {
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}).Should().Be(ActionRefusal.Prisoner);
      }

      // The dead, the player, and no one at all never act toward the player.
      [Test]
      public void GIVEN_the_dead_the_player_or_no_one_WHEN_checked_THEN_none_takes_the_initiative()
      {
         NpcActionGate.Check(new ActorFacts {IsAlive = false}).Should().Be(ActionRefusal.Dead);
         NpcActionGate.Check(new ActorFacts {IsPlayer = true}).Should().Be(ActionRefusal.IsThePlayer);
         NpcActionGate.Check(new ActorFacts {Exists = false}).Should().Be(ActionRefusal.NoOne);
         NpcActionGate.Check(null).Should().Be(ActionRefusal.NoOne);
      }

      // A hero the game has taken out of the world (disabled: not yet in play, or set aside by a mod, such as a custom
      // family's late parent) has no whereabouts and does nothing, like the dead (Maifailun, 30/09/2026).
      [Test]
      public void GIVEN_a_hero_taken_out_of_the_world_WHEN_they_would_act_THEN_it_does_not_happen()
      {
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.Letter).Should().Be(ActionRefusal.OutOfTheWorld);
      }

      // Maifailun (Nexus, 30/09/2026): "my characters dead father is also partaking in the meeting". Gabriel the same
      // day: sitting at the table is an action, so it asks the same gate as every other.
      [Test]
      public void GIVEN_a_dead_lord_of_the_house_WHEN_the_player_convenes_a_council_THEN_he_does_not_take_a_seat()
      {
         NpcActionGate.Check(new ActorFacts {IsAlive = false, Age = 60}, ActionKind.CouncilSeat).Should().Be(ActionRefusal.Dead);
      }

      // The council's own age (CouncilRosterPolicy.MinSeatAge, 13, Gabriel's rule of 25/08/2026): a teen of the house
      // may sit and speak true to their years, though the game does not yet count them of age.
      [Test]
      public void GIVEN_a_teen_of_the_house_below_the_games_majority_WHEN_a_council_is_convened_THEN_they_may_sit()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true, Age = 14}, ActionKind.CouncilSeat).Should().Be(ActionRefusal.None);
      }

      // ... but a younger child is never a candidate for the table (toddlers were once seated at a war council).
      [Test]
      public void GIVEN_a_young_child_WHEN_a_council_is_convened_THEN_they_do_not_sit()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true, Age = 8}, ActionKind.CouncilSeat).Should().Be(ActionRefusal.Child);
      }

      // Every other kind keeps the game's majority: a fourteen-year-old still writes no threats and asks no audience.
      [Test]
      public void GIVEN_a_teen_below_the_games_majority_WHEN_they_would_write_first_THEN_it_does_not_happen()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true, Age = 14}, ActionKind.Letter).Should().Be(ActionRefusal.Child);
      }

      // ── Per kind: every action refuses the dead, the disabled and the player; age and captivity depend on the act ──
      // The engine counts a DISABLED hero as alive (Hero.IsAlive is only "not dead"), which is why every one of the
      // paths below, each checking IsAlive alone, let a hero the game had taken out of the world through (inventory,
      // 30/09/2026).

      // A letter the player sends: a child may answer it (the gate's own exception since it was built), a disabled
      // hero may not, and a captive still cannot be reached by a courier.
      [Test]
      public void GIVEN_the_player_writes_to_someone_WHEN_they_would_receive_it_THEN_a_child_may_a_captive_or_a_hero_out_of_the_world_may_not()
      {
         NpcActionGate.Allows(new ActorFacts {IsChild = true}, ActionKind.ReceiveLetter).Should().BeTrue();
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.ReceiveLetter).Should().Be(ActionRefusal.OutOfTheWorld);
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}, ActionKind.ReceiveLetter).Should().Be(ActionRefusal.Prisoner);
      }

      // A warrant is read in the liege's name and sergeants ride under it: no warrant from a dead, absent, child or
      // captive ruler (the warrant stage checked nothing at all, while the trial bench already asked the gate).
      [Test]
      public void GIVEN_a_ruler_who_is_a_child_or_a_captive_WHEN_a_warrant_would_be_issued_in_their_name_THEN_none_is()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.Warrant).Should().Be(ActionRefusal.Child);
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}, ActionKind.Warrant).Should().Be(ActionRefusal.Prisoner);
      }

      // A witness only stands in the room: the player's young son riding in the party may be present.
      [Test]
      public void GIVEN_a_child_riding_with_the_player_WHEN_a_conversation_gathers_its_witnesses_THEN_the_child_may_be_present()
      {
         NpcActionGate.Allows(new ActorFacts {IsChild = true}, ActionKind.Witness).Should().BeTrue();
      }

      // A scene held for landfall is received later: whoever it was with may no longer be in the world by then.
      [Test]
      public void GIVEN_a_scene_held_for_landfall_WHEN_its_partner_was_taken_out_of_the_world_meanwhile_THEN_it_is_not_received()
      {
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.HeldScene).Should().Be(ActionRefusal.OutOfTheWorld);
      }

      // Marriage has its own age rules (MarriageEligibility) and a captive's hand can still be offered; the gate adds
      // only what the prospect lists lacked: the dead and the disabled are never offered.
      [Test]
      public void GIVEN_a_marriage_prospect_WHEN_offered_THEN_captivity_and_age_are_left_to_marriage_s_own_rules()
      {
         NpcActionGate.Allows(new ActorFacts {IsPrisoner = true, IsChild = true}, ActionKind.MarriageProspect).Should().BeTrue();
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.MarriageProspect).Should().Be(ActionRefusal.OutOfTheWorld);
      }

      // A ward is young by definition (WardPolicy's own age band): the gate leaves age to it.
      [Test]
      public void GIVEN_a_young_ward_WHEN_placed_THEN_age_is_left_to_the_ward_s_own_band()
      {
         NpcActionGate.Allows(new ActorFacts {IsChild = true}, ActionKind.Ward).Should().BeTrue();
      }

      // A hostage is a free adult (HostageEligibilityPolicy); the gate agrees and adds the disabled.
      [Test]
      public void GIVEN_a_child_or_captive_relative_WHEN_pledged_as_a_hostage_THEN_they_are_not()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.Hostage).Should().Be(ActionRefusal.Child);
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}, ActionKind.Hostage).Should().Be(ActionRefusal.Prisoner);
      }

      // A duel is never fought with a child; captivity is judged by the duel's own rules (a captive may be offered a
      // duel for their freedom), so the gate does not refuse it.
      [Test]
      public void GIVEN_a_duel_WHEN_the_opponent_is_a_child_or_a_captive_THEN_the_child_is_refused_and_captivity_left_to_the_duel()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.Duel).Should().Be(ActionRefusal.Child);
         NpcActionGate.Allows(new ActorFacts {IsPrisoner = true}, ActionKind.Duel).Should().BeTrue();
      }

      // A companion sent on an errand rides alone across the map: never a child.
      [Test]
      public void GIVEN_a_child_in_the_party_WHEN_the_player_would_send_them_on_a_mission_THEN_they_are_not_sent()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.CompanionMission).Should().Be(ActionRefusal.Child);
      }

      // A member of the player's household handed into a lord's service (take_into_service): the household list came
      // from Clan.Heroes, which keeps the disabled; never a child or a captive either.
      [Test]
      public void GIVEN_a_household_member_WHEN_offered_into_a_lords_service_THEN_no_child_captive_or_absent_hero_is()
      {
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.EnterService).Should().Be(ActionRefusal.Child);
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.EnterService).Should().Be(ActionRefusal.OutOfTheWorld);
      }

      // A lord riding at the player's side (follow_me): until now refused a captive or a child only by the accident
      // that neither leads a party of their own.
      [Test]
      public void GIVEN_a_lord_asked_to_ride_with_the_player_WHEN_they_are_a_captive_or_a_child_THEN_they_do_not()
      {
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}, ActionKind.Escort).Should().Be(ActionRefusal.Prisoner);
         NpcActionGate.Check(new ActorFacts {IsChild = true}, ActionKind.Escort).Should().Be(ActionRefusal.Child);
      }

      // The paymaster of an ambush, or a nemesis, facing the captive player: resolved from an id stored days before,
      // so re-asked when the scene opens; one who has since been taken, or left the world, does not appear.
      [Test]
      public void GIVEN_a_captor_resolved_from_days_ago_WHEN_the_scene_opens_THEN_one_since_taken_or_gone_does_not_appear()
      {
         NpcActionGate.Check(new ActorFacts {IsPrisoner = true}, ActionKind.CaptorScene).Should().Be(ActionRefusal.Prisoner);
         NpcActionGate.Check(new ActorFacts {IsDisabled = true}, ActionKind.CaptorScene).Should().Be(ActionRefusal.OutOfTheWorld);
      }

      // Gabriel (30/09/2026): "le gating never captive pour les actions ne doit pas bloquer un jugement de prisonnier ou un
      // chat avec un prisonnier." Talking with the player's own captive is a scene whose partner IS a prisoner; held for
      // landfall at sea, it must still be received ashore.
      [Test]
      public void GIVEN_a_conversation_with_a_prisoner_held_for_landfall_WHEN_received_THEN_the_captive_is_not_refused()
      {
         NpcActionGate.Allows(new ActorFacts {IsPrisoner = true}, ActionKind.HeldScene).Should().BeTrue();
      }

      // A captive in the room still witnesses what is said there; witnesses never checked captivity before the gate.
      [Test]
      public void GIVEN_a_captive_in_the_room_WHEN_the_witnesses_are_gathered_THEN_the_captive_is_not_refused()
      {
         NpcActionGate.Allows(new ActorFacts {IsPrisoner = true}, ActionKind.Witness).Should().BeTrue();
      }

      // The refusal a log names is the truest one: a dead child is dead, a captive child is a child.
      [Test]
      public void GIVEN_several_reasons_at_once_WHEN_checked_THEN_the_most_absolute_is_named()
      {
         NpcActionGate.Check(new ActorFacts {IsAlive = false, IsChild = true}).Should().Be(ActionRefusal.Dead);
         NpcActionGate.Check(new ActorFacts {IsChild = true, IsPrisoner = true}).Should().Be(ActionRefusal.Child);
      }
   }
}
