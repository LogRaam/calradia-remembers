// Code written by Gabriel Mailhot, 15/11/2026.
// What PersonaSectionPolicy decides: which sections of a character's prompt another mod receives as that character's
// persona (CrPrompt.Persona), and which section never leaves CR at all. VonLemberg (RFC, 15/11/2026) wants a ruler to
// speak to another ruler with "their established persona and knowledge"; Gabriel: "une surface qui permet de recuperer le
// persona complet d'un NPC". If this is wrong, a secret another mod confided to the character reaches a third mod, or a
// lord writing to a lord recites the player's standing and CR's verbs.

#region

using System.Linq;
using CalradiaRemembers.Logic.Extension;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PersonaSectionPolicyTests
   {
      // STAKE: the persona is the character himself: who he is, how he sounds, what he wants, remembers and knows. Without
      // these the diplomacy mod's ruler is "a generic chatbot" (the RFC's words).
      [TestCase("identity")]
      // tashmetu (Nexus 19/11/2026): the archetype and personality line left identity for sections of their own; the
      // persona keeps them as identity did.
      [TestCase("archetype")]
      [TestCase("personality")]
      [TestCase("authored_backstory")]
      [TestCase("authored_conviction")]
      [TestCase("character_voice")]
      [TestCase("matters_most")]
      [TestCase("history")]
      [TestCase("own_aim")]
      [TestCase("volatile_knowledge")]
      [TestCase("relationships")]
      public void GIVEN_a_section_about_the_character_himself_WHEN_asked_THEN_it_is_part_of_his_persona(string key)
      {
         PersonaSectionPolicy.IsPersona(key).Should().BeTrue();
      }

      // STAKE: what another mod confided to the character "known to you alone" (CrCharacterText, Private) is that mod's
      // secret: handing it to a third mod breaks CR's promise to the first one.
      [Test]
      public void GIVEN_the_section_another_mod_confided_WHEN_asked_THEN_it_is_never_in_a_persona_and_never_leaves_CR()
      {
         PersonaSectionPolicy.IsPersona("mod_private").Should().BeFalse();
         PersonaSectionPolicy.MayLeaveCr("mod_private").Should().BeFalse();
         PersonaSectionPolicy.MayLeaveCr("MOD_PRIVATE ").Should().BeFalse();
         PersonaSectionPolicy.MayLeaveCr("identity").Should().BeTrue();
      }

      // STAKE (Stage 2, 28/11/2026): what a meeting left holds what the character keeps from the player; a mod handed the
      // stock sections must not read it out to the player through its own prompt.
      [Test]
      public void GIVEN_what_meetings_left_WHEN_asked_THEN_it_is_never_in_a_persona_and_never_leaves_CR()
      {
         PersonaSectionPolicy.IsPersona("meeting_memory").Should().BeFalse();
         PersonaSectionPolicy.MayLeaveCr("meeting_memory").Should().BeFalse();
         NpcMemoryService.Core.Prompts.PromptBuilder.CharacterSections.Select(s => s.Key).Should().Contain(PersonaSectionPolicy.KeptFromThePlayer);
      }

      // STAKE: the sections written for a talk WITH THE PLAYER (his standing, a duel or fief offered to him, a secret held
      // about him) and CR's reply format and verbs: a lord writing to another lord must not recite them.
      [TestCase("player_standing")]
      [TestCase("held_secret")]
      [TestCase("duel_challenge")]
      [TestCase("grant_fief_offer")]
      [TestCase("format_instructions")]
      [TestCase("behavior_guidelines")]
      [TestCase("credulity")]
      [TestCase("encounter_context")]
      // cr.test persona_surface (Gabriel's game, 15/11/2026): how the player learns a trait is CR's reply format, split out
      // of matters_most and fear_and_superstition so the persona carries the trait without it.
      [TestCase("matters_most_discovery")]
      [TestCase("fear_discovery")]
      public void GIVEN_a_section_written_for_the_talk_with_the_player_WHEN_asked_THEN_it_is_not_part_of_the_persona(string key)
      {
         PersonaSectionPolicy.IsPersona(key).Should().BeFalse();
      }

      // STAKE: an allow-list: a section CR adds tomorrow (a new offer to the player) stays out until someone decides it
      // belongs, so it cannot leak by default.
      [Test]
      public void GIVEN_a_section_nobody_has_ruled_on_WHEN_asked_THEN_it_stays_out()
      {
         PersonaSectionPolicy.IsPersona("some_offer_added_next_year").Should().BeFalse();
         PersonaSectionPolicy.IsPersona(null).Should().BeFalse();
         PersonaSectionPolicy.IsPersona("  ").Should().BeFalse();
      }

      // STAKE: a persona key that no longer names a real section is a silent hole (the character loses that part of
      // himself and nobody notices). Every persona key must be a section the SDK's character prompt actually has.
      [Test]
      public void GIVEN_the_persona_keys_WHEN_compared_with_the_prompt_s_sections_THEN_each_is_a_real_section()
      {
         var real = NpcMemoryService.Core.Prompts.PromptBuilder.CharacterSections.Select(s => s.Key).ToHashSet();

         PersonaSectionPolicy.PersonaKeys.Where(k => !real.Contains(k)).Should().BeEmpty();
         real.Should().Contain(PersonaSectionPolicy.ConfidedByAnotherMod);
      }
   }
}
