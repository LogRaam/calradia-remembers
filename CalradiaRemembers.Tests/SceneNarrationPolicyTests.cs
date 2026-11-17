// Code written by Gabriel Mailhot, 17/11/2026.
// SceneNarrationPolicy decides whether a narration the model wrote reaches the chat when the player turned "Scene
// Narration" off (1Enfant, Nexus, 16/11/2026: "How do I disable the description of the surrounding environment?").
// Wrong one way, the purple description the player turned off keeps appearing; wrong the other way, a captive scene
// loses what is done to the prisoner, or a reply made only of narration leaves the player facing silence.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public class SceneNarrationPolicyTests
   {
      // STAKE: the player who turned the description off still sees it after every reply.
      [Test]
      public void GIVEN_the_switch_off_WHEN_an_ordinary_reply_carries_a_narration_and_words_THEN_the_narration_is_hidden()
         => SceneNarrationPolicy.Shows(new SceneNarrationFacts {SettingOn = false, NothingSpoken = false}).Should().BeFalse();

      // STAKE: a player who kept the default loses the narration they never asked to lose.
      [Test]
      public void GIVEN_the_switch_on_WHEN_a_reply_carries_a_narration_THEN_it_is_shown()
         => SceneNarrationPolicy.Shows(new SceneNarrationFacts {SettingOn = true}).Should().BeTrue();

      // STAKE: in a captive scene the narration is what happens to the prisoner; hiding it hides the scene itself.
      [Test]
      public void GIVEN_the_switch_off_WHEN_the_scene_is_a_captive_one_THEN_the_narration_is_shown()
         => SceneNarrationPolicy.Shows(new SceneNarrationFacts {SettingOn = false, CaptiveScene = true}).Should().BeTrue();

      // STAKE: a reply made only of narration would leave an empty turn, the player waiting on a character who said nothing.
      [Test]
      public void GIVEN_the_switch_off_WHEN_the_narration_is_the_whole_reply_THEN_it_is_shown()
         => SceneNarrationPolicy.Shows(new SceneNarrationFacts {SettingOn = false, NothingSpoken = true}).Should().BeTrue();
   }
}
