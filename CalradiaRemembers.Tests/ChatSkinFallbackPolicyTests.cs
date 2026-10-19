// Code written by Gabriel Mailhot, 04/10/2026.
// Which chat window opens when the chosen skin's art cannot be drawn. The Dark Leather (Manuscript) skin paints its
// End Conversation and Memories labels INTO its page art, and its buttons are invisible hit-boxes over them. EmperorHaterius
// (Nexus, 04/10/2026): on his machine the art never loaded, the window was see-through, no button showed, Escape did
// nothing, and every conversation ended only with cr.reset. Origin draws its own buttons, so it is the safe window.
// If this rule is wrong, a player whose images do not load is trapped in every conversation again.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ChatSkinFallbackPolicyTests
   {
      private const string Classic = "CalradiaRemembersChat";
      private const string Manuscript = "CalradiaRemembersChat_Manuscript";

      // The report itself: Dark Leather chosen, its art missing. Without the fallback the player sees no way out.
      [Test]
      public void GIVEN_Dark_Leather_chosen_WHEN_its_art_cannot_be_loaded_THEN_the_Origin_window_opens_instead()
      {
         ChatSkinFallbackPolicy.Resolve(new ChatSkinFacts {Requested = Manuscript, ManuscriptSkin = Manuscript, ClassicSkin = Classic, ManuscriptArtLoads = false})
            .Should().Be(Classic);
      }

      // The art loads: the player's choice stands. A fallback that fired here would take away a skin that works.
      [Test]
      public void GIVEN_Dark_Leather_chosen_WHEN_its_art_loads_THEN_Dark_Leather_opens()
      {
         ChatSkinFallbackPolicy.Resolve(new ChatSkinFacts {Requested = Manuscript, ManuscriptSkin = Manuscript, ClassicSkin = Classic, ManuscriptArtLoads = true})
            .Should().Be(Manuscript);
      }

      // Origin and the council window are not judged on the Manuscript art: their controls do not depend on it.
      [Test]
      public void GIVEN_another_window_chosen_WHEN_the_Dark_Leather_art_is_missing_THEN_that_window_still_opens()
      {
         ChatSkinFallbackPolicy.Resolve(new ChatSkinFacts {Requested = Classic, ManuscriptSkin = Manuscript, ClassicSkin = Classic, ManuscriptArtLoads = false})
            .Should().Be(Classic);
         ChatSkinFallbackPolicy.Resolve(new ChatSkinFacts {Requested = "CalradiaRemembersCouncil", ManuscriptSkin = Manuscript, ClassicSkin = Classic, ManuscriptArtLoads = false})
            .Should().Be("CalradiaRemembersCouncil");
      }

      // No name at all (a bad setting or override): Origin, so a bad skin can never leave the player with no chat.
      [Test]
      public void GIVEN_no_skin_named_WHEN_resolved_THEN_the_Origin_window_opens()
      {
         ChatSkinFallbackPolicy.Resolve(new ChatSkinFacts {Requested = " ", ManuscriptSkin = Manuscript, ClassicSkin = Classic, ManuscriptArtLoads = true})
            .Should().Be(Classic);
      }
   }
}
