// Code written by Gabriel Mailhot, 26/10/2026.
// WandererBackstoryPolicy decides which of the game's lines are a wanderer's own story and how that story is kept as one
// memory of their life before the player. fearente (Nexus, 26/10/2026): a companion hired a week before spoke of a
// childhood shared with him, and the story the game had him tell at their first meeting was never remembered. If this is
// wrong, the story is lost again, kept twice, or a refusal to speak of the past is kept as though it were a story.

#region

using CalradiaRemembers.Logic.Companions;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class WandererBackstoryPolicyTests
   {
      // STAKE: the game tells the story through these lines, in this order; missing one loses part of who they are.
      [Test]
      public void GIVEN_the_games_story_lines_WHEN_told_THEN_each_is_recognised_as_the_story()
      {
         foreach (string id in new[] {"wanderer_prebackstory", "wanderer_introduction_a", "wanderer_introduction_b", "wanderer_introduction_c", "wanderer_introduction_d"})
            WandererBackstoryPolicy.IsStoryLine(id).Should().BeTrue(id);
      }

      // STAKE: "I do not care to talk about my past" is no story; kept as one, it would give them a past they refused to tell.
      [Test]
      public void GIVEN_the_generic_line_or_any_other_WHEN_said_THEN_it_is_not_kept_as_the_story()
      {
         WandererBackstoryPolicy.IsStoryLine("wanderer_backstory_generic").Should().BeFalse();
         WandererBackstoryPolicy.IsStoryLine("wanderer_job_status_1").Should().BeFalse();
         WandererBackstoryPolicy.IsStoryLine(null).Should().BeFalse();
      }

      // STAKE: the story is told across several lines; kept in order as one memory, it reads as the life they told.
      [Test]
      public void GIVEN_the_story_told_line_by_line_WHEN_kept_THEN_it_is_one_memory_in_the_order_told()
      {
         string kept = WandererBackstoryPolicy.Keep(null, "I was born in Pravend.");
         kept = WandererBackstoryPolicy.Keep(kept, "My father kept horses.");

         WandererBackstoryPolicy.IsTheKeptStory(kept).Should().BeTrue();
         kept.IndexOf("Pravend").Should().BeLessThan(kept.IndexOf("horses"));
      }

      // STAKE: a story heard twice (a second conversation, a reload) must not be kept twice.
      [Test]
      public void GIVEN_a_line_already_kept_WHEN_told_again_THEN_the_memory_is_unchanged()
      {
         string kept = WandererBackstoryPolicy.Keep(null, "I was born in Pravend.");

         WandererBackstoryPolicy.Keep(kept, "I was born in Pravend.").Should().Be(kept);
      }
   }
}
