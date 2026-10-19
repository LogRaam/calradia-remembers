// Code written by Gabriel Mailhot, 18/07/2026.
// This gate decides which [WITNESS_REACTION] lines a model emitted may be VOICED, and the stake is larger
// than a stray bubble on screen: a kept line is committed to the transcript, so it is replayed into every
// prompt rebuilt afterwards. A wrongly-kept line therefore does not appear once, it joins the conversation's
// history and the model keeps that speaker talking. The rule lived inside ChatViewModel until now, which
// meant it had NO test at all (the mod is net472, this project is net10), and that is exactly how one of
// these faults shipped and reached players through the 15C witness path.
//
// Each test below names the fault it guards.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Witnesses;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WitnessReactionGateTests
   {
      // The ordinary case, so the guards below cannot be satisfied by refusing everything.
      [Test]
      public void GIVEN_a_line_from_someone_in_the_room_WHEN_deciding_THEN_it_is_voiced()
      {
         Decide("Olaf", "He is lying to you.").IsKept.Should().BeTrue();
      }

      // Round-table audit D1, and the one that was ACTIVE in shipped builds: small models invent a speaker.
      // Displayed, story-logged and transcripted, the invention fed itself, so a lady who was never in the
      // room "spoke" and the model kept her in the scene for the rest of the conversation.
      [Test]
      public void GIVEN_a_line_from_someone_who_is_not_in_the_room_WHEN_deciding_THEN_it_is_dropped()
      {
         Decide("Rhagaea", "I would not trust him either.")
            .Verdict.Should().Be(WitnessReactionVerdict.DropAbsent);
      }

      // The NPC whose turn it is speaks through [DIALOGUE]. A reaction under their own name is the model
      // answering itself, and showing it would give the same character two voices in one turn.
      [Test]
      public void GIVEN_a_line_attributed_to_the_speaker_themselves_WHEN_deciding_THEN_it_is_dropped()
      {
         Decide("Derthert", "And I say no.")
            .Verdict.Should().Be(WitnessReactionVerdict.DropSelfAttributed);
      }

      // The mechanical anti-repeat: the prompt asks for variety and weaker models loop a line verbatim
      // anyway, which reads as a broken record and costs a prompt slot every rebuild.
      [Test]
      public void GIVEN_the_same_line_this_speaker_just_said_WHEN_deciding_THEN_it_is_dropped()
      {
         Decide("Olaf", "He is lying to you.", lastLines: new Dictionary<string, string> {["Olaf"] = "He is lying to you."})
            .Verdict.Should().Be(WitnessReactionVerdict.DropRepeated);
      }

      // The boundary that made the extraction worth doing. The host stores last lines under the CANONICAL
      // name, while the model writes whatever casing it likes; a lookup on the raw spelling would miss the
      // stored line and let the repeat through. Pins that the gate resolves the name BEFORE looking up.
      [Test]
      public void GIVEN_a_repeat_written_in_different_casing_WHEN_deciding_THEN_it_is_still_caught()
      {
         Decide("olaf", "He is lying to you.", lastLines: new Dictionary<string, string> {["Olaf"] = "He is lying to you."})
            .Verdict.Should().Be(WitnessReactionVerdict.DropRepeated);
      }

      // Only a VERBATIM repeat is a fault: the same speaker developing their point is the scene working.
      [Test]
      public void GIVEN_a_different_line_from_the_same_speaker_WHEN_deciding_THEN_it_is_voiced()
      {
         Decide("Olaf", "And there is worse.", lastLines: new Dictionary<string, string> {["Olaf"] = "He is lying to you."})
            .IsKept.Should().BeTrue();
      }

      // The model copies the whole prompt label ("Olaf (one of your men, loyal)") into the name. The room is
      // matched, and the line DISPLAYED, under the short name the player knows.
      [Test]
      public void GIVEN_a_name_carrying_its_prompt_role_WHEN_deciding_THEN_it_is_matched_and_named_canonically()
      {
         WitnessReactionDecision decision = Decide("Olaf (one of your men, loyal to a fault)", "He is lying to you.");

         decision.IsKept.Should().BeTrue();
         decision.CanonicalName.Should().Be("Olaf");
      }

      // Matching is by NAME, never by hero id, precisely so a synthetic flavour witness keeps its voice: a
      // gang enforcer or a tavern apprentice has no hero behind it but is genuinely standing in the room.
      [Test]
      public void GIVEN_a_synthetic_witness_with_no_hero_behind_it_WHEN_deciding_THEN_it_may_still_speak()
      {
         Decide("A hard-eyed enforcer", "Boss says you talk too much.",
               present: new[] {"A hard-eyed enforcer"})
            .IsKept.Should().BeTrue();
      }

      // An empty line is nothing to show, and must not overwrite what the speaker last said.
      [TestCase("")]
      [TestCase("   ")]
      [TestCase(null)]
      public void GIVEN_an_empty_line_WHEN_deciding_THEN_it_is_dropped_as_blank(string text)
      {
         Decide("Olaf", text).Verdict.Should().Be(WitnessReactionVerdict.DropBlank);
      }

      // Fail safe: an empty room can produce no reactions at all. This is the state right after a privacy
      // request clears the room, where a smaller model still emits witness lines out of habit.
      [Test]
      public void GIVEN_nobody_present_WHEN_deciding_THEN_nothing_may_be_voiced()
      {
         Decide("Olaf", "He is lying to you.", present: new string[0])
            .Verdict.Should().Be(WitnessReactionVerdict.DropAbsent);
         WitnessReactionGate.Decide(new WitnessReactionGate.WitnessReactionFacts {EmittedName = "Olaf", Text = "Anything.", MainSpeakerName = "Derthert", PresentNames = null, LastLines = null})
                            .Verdict.Should().Be(WitnessReactionVerdict.DropAbsent);
      }

      #region private

      private static WitnessReactionDecision Decide(string emitted,
                                                    string text,
                                                    IReadOnlyList<string> present = null!,
                                                    IReadOnlyDictionary<string, string> lastLines = null!)
         => WitnessReactionGate.Decide(new WitnessReactionGate.WitnessReactionFacts {EmittedName = emitted, Text = text, MainSpeakerName = "Derthert", PresentNames = present ?? new[] {"Olaf", "Alympia"}, LastLines = lastLines});

      #endregion
   }
}
