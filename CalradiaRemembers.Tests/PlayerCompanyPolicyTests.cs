// Code written by Gabriel Mailhot, 03/10/2026.
// nerethi (Nexus) asked to invite someone into a conversation. Gabriel's design (03/10/2026): "Il devrait y avoir
// request private et invite someone toujours disponibles; invite someone permet de selectionner un compagnon, un
// prisonnier ou un Lord qui voyage avec le joueur. Alors que request private repropose au NPC principal de la
// conversation de revenir seul. Si les temoins sont tous des temoins accompagnant le joueur, alors le request private
// est accepte automatiquement." If these rules are wrong: a stranger is offered as "yours", the person you are
// talking to is invited into her own conversation, your own people refuse to step out, or a lord's guards leave
// without the lord having a say.
// Windmolen (Nexus, 23/11/2026) asked to invite the heroes present in the keep, tavern or dungeon too; Gabriel
// (23/11/2026, "go"). If wrong: they are missing from the list, offered twice, or offered inside a tale's own scene.

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

      // STAKE: Windmolen sees lords and ladies standing in the keep and cannot bring them in; they are offered, as "here".
      [Test]
      public void GIVEN_heroes_present_where_the_character_stands_WHEN_inviting_THEN_they_are_offered_as_present_here()
      {
         var here = new[] {new CompanyMember {Id = "lady_rhagaea", Name = "Rhagaea"}};

         PlayerCompanyPolicy.InviteCandidates(Company, "lord_x", new string[0], presentHere: here)
                            .Should().ContainSingle(m => m.Id == "lady_rhagaea" && m.Role == CompanyRole.PresentHere);
      }

      // STAKE: a companion standing beside the player is the player's, offered once, and the one spoken to never.
      [Test]
      public void GIVEN_a_present_hero_who_is_also_the_players_or_the_one_spoken_to_WHEN_inviting_THEN_offered_once_as_the_players_or_not_at_all()
      {
         var here = new[] {new CompanyMember {Id = "comp_ira", Name = "Ira"}, new CompanyMember {Id = "lord_x", Name = "X"}};
         IReadOnlyList<CompanyMember> offered = PlayerCompanyPolicy.InviteCandidates(Company, "lord_x", new string[0], presentHere: here);

         offered.Should().ContainSingle(m => m.Id == "comp_ira").Which.Role.Should().Be(CompanyRole.Companion);
         offered.Should().NotContain(m => m.Id == "lord_x");
      }

      // STAKE: a tale's scene keeps its own cast; strangers are not brought into it.
      [Test]
      public void GIVEN_a_scene_where_strangers_are_not_welcome_WHEN_inviting_THEN_no_present_hero_is_offered()
      {
         var here = new[] {new CompanyMember {Id = "lady_rhagaea", Name = "Rhagaea"}};

         PlayerCompanyPolicy.InviteCandidates(Company, "lord_x", new string[0], presentHere: here, strangersWelcome: false)
                            .Should().NotContain(m => m.Id == "lady_rhagaea");
      }

      // STAKE: a stranger brought in is not the player's; asking for privacy still goes to the character.
      [Test]
      public void GIVEN_a_present_hero_brought_in_WHEN_privacy_is_asked_THEN_the_npc_decides()
      {
         bool allMine = PlayerCompanyPolicy.AllAreThePlayers(new[] {new PresentWitness {HeroId = "lady_rhagaea"}}, new HashSet<string> {"comp_ira"});

         allMine.Should().BeFalse();
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

      // STAKE: whiskeypatt's lord, a witness, said yes to a private word with Rhagaea's guest list and emptied her room;
      // the conversation then snapped back to her mid-reply.
      [Test]
      public void GIVEN_a_witness_accepts_privacy_WHEN_judged_THEN_the_room_stays_as_it_is()
      {
         PrivateAudiencePolicy.ClearsTheRoom(new PrivacyAnswerFacts {FromMainCharacter = false, Accepted = true}).Should().BeFalse();
      }

      // STAKE: the character the conversation is with still grants the room, as Request Private always meant.
      [Test]
      public void GIVEN_the_main_character_accepts_privacy_WHEN_judged_THEN_the_room_is_cleared()
      {
         PrivateAudiencePolicy.ClearsTheRoom(new PrivacyAnswerFacts {FromMainCharacter = true, Accepted = true}).Should().BeTrue();
         PrivateAudiencePolicy.ClearsTheRoom(new PrivacyAnswerFacts {FromMainCharacter = true, Accepted = false}).Should().BeFalse();
      }

      // STAKE: unchanged (ApocalyptikGreen, 10/07/2026): the player's prisoner yields the room even when the reply refuses.
      [Test]
      public void GIVEN_the_players_prisoner_refuses_WHEN_judged_THEN_the_room_is_cleared_anyway()
      {
         PrivateAudiencePolicy.ClearsTheRoom(new PrivacyAnswerFacts {FromMainCharacter = true, MainIsPlayersPrisoner = true}).Should().BeTrue();
      }
   }
}
