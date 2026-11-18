// Code written by Gabriel Mailhot, 18/11/2026.
// SceneFindingEcho decides when a tale scene's narration showed a finding without writing its marker. Schmidt (Discord,
// 18/11/2026): the narrator described the wolf pelt on its frame and never wrote [[found:pelt]], the scene could not end,
// and "the quest is never ending". If this is too strict, a forgotten marker still traps the player in the scene; if too
// loose, a finding counts before the player ever comes to it, from words the place itself is full of.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SceneFindingEchoTests
   {
      private static readonly SceneFindingText Pelt = new() {Id = "pelt", Text = "A wolf pelt rigged on a wooden frame, eyes of polished stone: the beast the shepherds saw."};
      private static readonly SceneFindingText Chains = new() {Id = "chains", Text = "A trampled clearing with a dropped iron collar and a length of chain."};

      private const string Opening = "Another cold night of waiting by the wood. Then the player notices something on the path: quiet, watchful, the dark "
                                     + "close around them. Fresh boot prints in the mud lead from the edge of the village into the wood. The prints are of "
                                     + "many men, not of any beast.";

      private static IReadOnlyList<string> Seen(string narration, params SceneFindingText[] left)
         => SceneFindingEcho.Seen(new SceneFindingEchoFacts {Narration = narration, Left = left, SceneWords = Opening});

      // STAKE (Schmidt's turn 13, word for word in substance): the pelt shown in full counts, or the scene never ends.
      [Test]
      public void GIVEN_the_narration_that_showed_the_pelt_without_its_marker_WHEN_checked_THEN_the_pelt_is_found()
      {
         const string turn13 = "And nearer than any of those, off to the left of the way, a frame of cut wood stands upright in the mud. A pelt is "
                               + "stretched on it, pegged out flat, the legs spread and the head at the top. Grey, with the guard hairs still on it. "
                               + "The eye-sockets have been filled: two round stones, polished, set in the fur.";

         Seen(turn13, Pelt, Chains).Should().Equal("pelt");
      }

      // STAKE: the place itself (the wood, the path, the prints, a beast spoken of) proves nothing; no finding counts from it.
      [Test]
      public void GIVEN_a_narration_of_the_path_and_the_wood_WHEN_checked_THEN_nothing_is_found()
      {
         Seen("You follow the prints into the wood. The path is churned mud and the dark is close. No beast made these; men did.", Pelt, Chains)
            .Should().BeEmpty();
      }

      // STAKE: one passing word is not a finding: a clearing is only a clearing until the collar and chain are in it.
      [Test]
      public void GIVEN_a_single_word_of_a_finding_WHEN_checked_THEN_it_is_not_found()
      {
         Seen("The trees open onto a clearing, silver under the moon.", Chains).Should().BeEmpty();
      }

      // STAKE: the collar and chain shown in a clearing count, so the next required finding is the only one left.
      [Test]
      public void GIVEN_the_collar_and_chain_shown_WHEN_checked_THEN_the_chains_are_found()
      {
         Seen("In the trampled clearing something glints: an iron collar, dropped, a length of chain still on it.", Pelt, Chains).Should().Equal("chains");
      }
   }
}
