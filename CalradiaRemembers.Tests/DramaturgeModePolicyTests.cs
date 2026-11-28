// Code written by Gabriel Mailhot, 28/11/2026.
// When the Dramaturge mode's per-turn machinery runs (Stage 6 of the group-scene plan; Gabriel, 28/11/2026: "le mode
// dramaturge avec DRAMA+PROSE+INTERPRET+NSFW"), and which reply the keeper's one rewrite leaves on screen. It is not
// A/B-validated, so it must live only under the Dramaturge mode. If this is wrong: Standard or Enhanced players pay two or
// three extra calls a turn and see replies rewritten they never asked for, or a failed rewrite blanks a good reply.

#region

using CalradiaRemembers.Logic.Chat;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class DramaturgeModePolicyTests
   {
      private static DramaturgeTurnFacts Ordinary() => new() {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = true};

      // STAKE: the Dramaturge player's ordinary talk is where the inner life and the keeper run.
      [Test]
      public void GIVEN_the_dramaturge_mode_on_an_ordinary_turn_WHEN_decided_THEN_the_machinery_runs()
      {
         DramaturgeModePolicy.Runs(Ordinary()).Should().BeTrue();
      }

      // STAKE: Gabriel's rule for the night's build: Standard and Enhanced do not change at all.
      [Test]
      public void GIVEN_standard_or_enhanced_WHEN_decided_THEN_nothing_extra_runs()
      {
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = false, SwitchOnTheMcmMode = true, IsMainSpeaker = true}).Should().BeFalse();
      }

      // STAKE: the in-chat switch on Roleplay or NSFW is a one-call mode; the player turned the machinery off with it.
      [Test]
      public void GIVEN_the_switch_on_roleplay_or_nsfw_WHEN_decided_THEN_nothing_extra_runs()
      {
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = false, IsMainSpeaker = true}).Should().BeFalse();
      }

      // STAKE: the stage's scope is the two-person chat answering the player; groups wait for stage 7, captive scenes keep
      // their own path, and a stage direction the mod wrote has nothing for the keeper or the inner life to read.
      [Test]
      public void GIVEN_a_witness_a_synthetic_line_a_captive_scene_or_a_group_voice_WHEN_decided_THEN_nothing_extra_runs()
      {
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = false}).Should().BeFalse();
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = true, IsSyntheticPlayerLine = true}).Should().BeFalse();
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = true, IsCaptiveEncounter = true}).Should().BeFalse();
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = true, IsCaptorScene = true}).Should().BeFalse();
         DramaturgeModePolicy.Runs(new DramaturgeTurnFacts {DramaturgeMode = true, SwitchOnTheMcmMode = true, IsMainSpeaker = true, IsGroupVoice = true}).Should().BeFalse();
         DramaturgeModePolicy.Runs(null).Should().BeFalse();
      }

      // STAKE: only a verdict of a problem costs the player a second prose call; a pass, or a keeper that gave no verdict,
      // lets the reply stand.
      [Test]
      public void GIVEN_each_verdict_WHEN_deciding_a_rewrite_THEN_only_a_problem_asks_for_one()
      {
         DramaturgeRewritePolicy.ShouldRewrite(new KeeperVerdict {Problem = KeeperProblem.Lie, HadVerdict = true}).Should().BeTrue();
         DramaturgeRewritePolicy.ShouldRewrite(new KeeperVerdict {Problem = KeeperProblem.Invention, HadVerdict = true}).Should().BeTrue();
         DramaturgeRewritePolicy.ShouldRewrite(new KeeperVerdict {Problem = KeeperProblem.None, HadVerdict = true}).Should().BeFalse();
         DramaturgeRewritePolicy.ShouldRewrite(new KeeperVerdict {Problem = KeeperProblem.None, HadVerdict = false}).Should().BeFalse();
         DramaturgeRewritePolicy.ShouldRewrite(null).Should().BeFalse();
      }

      // STAKE: a rewrite that never came, came back empty, or reads as a refusal must not replace a reply the player could
      // have read; one that came is shown, even if its second reading still finds fault (one rewrite, never a loop).
      [Test]
      public void GIVEN_a_rewrite_WHEN_deciding_what_is_shown_THEN_only_a_real_answer_replaces_the_first_reply()
      {
         DramaturgeRewritePolicy.ShowsRewrite(new KeeperRewriteFacts {RewriteAnswered = true, RewriteHasText = true}).Should().BeTrue();
         DramaturgeRewritePolicy.ShowsRewrite(new KeeperRewriteFacts {RewriteAnswered = false, RewriteHasText = true}).Should().BeFalse();
         DramaturgeRewritePolicy.ShowsRewrite(new KeeperRewriteFacts {RewriteAnswered = true, RewriteHasText = false}).Should().BeFalse();
         DramaturgeRewritePolicy.ShowsRewrite(new KeeperRewriteFacts {RewriteAnswered = true, RewriteHasText = true, RewriteLooksLikeRefusal = true}).Should().BeFalse();
      }
   }
}
