// Code written by Gabriel Mailhot, 29/10/2026.
// What the chat's participants panel shows and allows. RaveN017Mod (Nexus PM, 29/10/2026): invited clan members
// "often don't interact much at all", and he asked for a way to make it clear who he is addressing. Gabriel's answer
// the same day: a panel listing the active voices and the witnesses, where a click on a witness makes them active.
// If these rules are wrong the panel lies about who is speaking, offers to draw in someone who cannot join, or lets
// the player send away the very person they came to talk to.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Conversation;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   public sealed class CastPanelPolicyTests
   {
      private static CastPanelMember M(string id, string name) => new CastPanelMember {Id = id, Name = name};

      private static IReadOnlyList<CastPanelRow> Rows(ConversationCast cast, string addressed, params CastPanelMember[] present)
         => CastPanelPolicy.Rows(new CastPanelFacts {Cast = cast, AnchorName = "Lord Olek", Present = present, AddressedId = addressed});

      // The report itself: the companions he brought sat silent and he could not tell why. The panel names who is
      // speaking and who is only watching, the lord first since the conversation is his.
      [Test]
      public void GIVEN_a_lord_and_two_invited_companions_WHEN_the_panel_is_built_THEN_the_lord_speaks_and_the_companions_watch()
      {
         var cast = new ConversationCast("olek", new[] {"ira", "sley"});

         IReadOnlyList<CastPanelRow> rows = Rows(cast, null, M("ira", "Ira"), M("sley", "Sley"));

         rows.Select(r => r.Name).Should().Equal("Lord Olek", "Ira", "Sley");
         rows[0].Standing.Should().Be(CastPanelStanding.Anchor);
         rows[0].IsAddressed.Should().BeTrue();
         rows.Skip(1).Should().OnlyContain(r => r.Standing == CastPanelStanding.Witness && r.CanAddress && r.WouldJoin);
      }

      // Once drawn in, a companion is listed with the voices, above those still watching, and may be sent back.
      [Test]
      public void GIVEN_a_companion_drawn_in_WHEN_the_panel_is_built_THEN_they_are_listed_with_the_speakers_and_may_step_back()
      {
         var cast = new ConversationCast("olek", new[] {"ira", "sley"});
         cast.Promote("sley");

         IReadOnlyList<CastPanelRow> rows = Rows(cast, "sley", M("ira", "Ira"), M("sley", "Sley"));

         rows.Select(r => r.Name).Should().Equal("Lord Olek", "Sley", "Ira");
         rows[1].Standing.Should().Be(CastPanelStanding.Active);
         rows[1].IsAddressed.Should().BeTrue();
         rows[1].CanStepBack.Should().BeTrue();
         rows[0].IsAddressed.Should().BeFalse();
         rows[0].CanAddress.Should().BeTrue();
      }

      // The person the player came to talk to is the conversation's reason to exist: never offered a way out.
      [Test]
      public void GIVEN_any_scene_WHEN_the_panel_is_built_THEN_the_lord_spoken_to_can_never_be_sent_back()
      {
         var cast = new ConversationCast("olek", new[] {"ira"});
         cast.Promote("ira");

         Rows(cast, "ira", M("ira", "Ira"))[0].CanStepBack.Should().BeFalse();
      }

      // Three voices at most (each is a model call and a wait every turn). A full table must not promise that a
      // click will draw the fourth in; the player can still turn to them for a word.
      [Test]
      public void GIVEN_a_full_table_WHEN_the_panel_is_built_THEN_a_witness_can_be_spoken_to_but_is_not_offered_a_seat()
      {
         var cast = new ConversationCast("olek", new[] {"ira", "sley", "brave"});
         cast.Promote("ira");
         cast.Promote("sley");

         CastPanelRow brave = Rows(cast, null, M("ira", "Ira"), M("sley", "Sley"), M("brave", "Brave")).Single(r => r.Id == "brave");

         brave.Standing.Should().Be(CastPanelStanding.Witness);
         brave.CanAddress.Should().BeTrue();
         brave.WouldJoin.Should().BeFalse();
      }

      // A nameless guard has no character behind him to speak with his own memory: shown, never clickable.
      [Test]
      public void GIVEN_a_bystander_with_no_character_WHEN_the_panel_is_built_THEN_they_are_listed_last_and_cannot_be_drawn_in()
      {
         var cast = new ConversationCast("olek", new[] {"ira"});

         IReadOnlyList<CastPanelRow> rows = Rows(cast, null, M("", "A gate guard"), M("ira", "Ira"));

         rows.Last().Name.Should().Be("A gate guard");
         rows.Last().Standing.Should().Be(CastPanelStanding.Bystander);
         rows.Last().CanAddress.Should().BeFalse();
         rows.Last().CanStepBack.Should().BeFalse();
      }

      // Someone who left the room is not listed, even if the cast has not caught up: the panel shows who is there.
      [Test]
      public void GIVEN_a_promoted_companion_who_left_WHEN_the_panel_is_built_THEN_they_are_not_listed()
      {
         var cast = new ConversationCast("olek", new[] {"ira"});
         cast.Promote("ira");

         Rows(cast, null).Select(r => r.Name).Should().Equal("Lord Olek");
      }

      // No real anchor hero (a nameless commoner's chat): there is no cast, so there is no panel to show.
      [Test]
      public void GIVEN_no_cast_WHEN_the_panel_is_built_THEN_it_is_empty()
      {
         CastPanelPolicy.Rows(new CastPanelFacts()).Should().BeEmpty();
         CastPanelPolicy.Rows(null).Should().BeEmpty();
      }
   }
}
