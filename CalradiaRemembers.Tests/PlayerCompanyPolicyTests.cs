// Code written by Gabriel Mailhot, 03/10/2026.
// nerethi (Nexus) asked to invite someone into a conversation. Gabriel's design (03/10/2026): "Il devrait y avoir
// request private et invite someone toujours disponibles; invite someone permet de selectionner un compagnon, un
// prisonnier ou un Lord qui voyage avec le joueur. Alors que request private repropose au NPC principal de la
// conversation de revenir seul. Si les temoins sont tous des temoins accompagnant le joueur, alors le request private
// est accepte automatiquement." If these rules are wrong: a stranger is offered as "yours", the person you are
// talking to is invited into her own conversation, your own people refuse to step out, or a lord's guards leave
// without the lord having a say.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerCompanyPolicyTests
   {
      private static readonly List<CompanyMember> Company = new()
      {
         new CompanyMember {Id = "comp_ira", Name = "Ira", Role = CompanyRole.Companion},
         new CompanyMember {Id = "lord_garios", Name = "Garios", Role = CompanyRole.TravellingLord},
         new CompanyMember {Id = "lord_caladog", Name = "Caladog", Role = CompanyRole.Prisoner}
      };

      // Gabriel: a companion, a prisoner or a lord travelling with the player may be invited.
      [Test]
      public void GIVEN_the_players_company_WHEN_inviting_THEN_companions_prisoners_and_travelling_lords_are_offered()
      {
         PlayerCompanyPolicy.InviteCandidates(Company, "notable_x", new string[0]).Should().HaveCount(3);
      }

      // Nobody is invited twice, and the person you are speaking with is never invited into her own conversation.
      [Test]
      public void GIVEN_one_already_present_and_one_being_spoken_to_WHEN_inviting_THEN_neither_is_offered()
      {
         PlayerCompanyPolicy.InviteCandidates(Company, "lord_garios", new[] {"comp_ira"})
                            .Should().ContainSingle(m => m.Id == "lord_caladog");
      }

      // Gabriel: every witness is someone accompanying the player, so the request is granted at once.
      [Test]
      public void GIVEN_only_the_players_own_people_in_the_room_WHEN_privacy_is_asked_THEN_it_is_granted_at_once()
      {
         PrivateAudiencePolicy.Decide(new PrivateAudienceFacts {
            Allowed = true, AnyWitness = true,
            AllWitnessesAreThePlayers = PlayerCompanyPolicy.AllAreThePlayers(new[] {
               new PresentWitness {HeroId = "comp_ira", IsPlayerCompanion = true},
               new PresentWitness {HeroId = "lord_garios"}
            }, new HashSet<string> {"comp_ira", "lord_garios", "lord_caladog"})
         }).Should().Be(PrivateAudienceVerdict.ClearAtOnce);
      }

      // One person of the lord's own (a guard, his steward) means the lord decides: the request goes to him.
      [Test]
      public void GIVEN_one_witness_who_is_not_the_players_WHEN_privacy_is_asked_THEN_the_npc_decides()
      {
         bool allMine = PlayerCompanyPolicy.AllAreThePlayers(new[] {
            new PresentWitness {HeroId = "comp_ira", IsPlayerCompanion = true},
            new PresentWitness {HeroId = "lord_steward"},
            new PresentWitness {HeroId = null}
         }, new HashSet<string> {"comp_ira"});

         PrivateAudiencePolicy.Decide(new PrivateAudienceFacts {Allowed = true, AnyWitness = true, AllWitnessesAreThePlayers = allMine})
                              .Should().Be(PrivateAudienceVerdict.AskTheNpc);
      }

      // Unchanged from before (ApocalyptikGreen, 10/07/2026): a prisoner has no say over who stays.
      [Test]
      public void GIVEN_the_players_prisoner_WHEN_privacy_is_asked_THEN_it_is_granted_at_once()
      {
         PrivateAudiencePolicy.Decide(new PrivateAudienceFacts {Allowed = true, AnyWitness = true, NpcIsPlayersPrisoner = true})
                              .Should().Be(PrivateAudienceVerdict.ClearAtOnce);
      }

      // Always available means it can be asked again after a refusal, but not while the last request is still waiting.
      [Test]
      public void GIVEN_a_request_still_waiting_or_an_empty_room_WHEN_privacy_is_asked_THEN_nothing_is_asked()
      {
         PrivateAudiencePolicy.Decide(new PrivateAudienceFacts {Allowed = true, AnyWitness = true, RequestPending = true})
                              .Should().Be(PrivateAudienceVerdict.Unavailable);
         PrivateAudiencePolicy.Decide(new PrivateAudienceFacts {Allowed = true, AnyWitness = false})
                              .Should().Be(PrivateAudienceVerdict.Unavailable);
      }
   }
}
