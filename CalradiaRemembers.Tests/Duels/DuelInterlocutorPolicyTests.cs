// Code written by Gabriel Mailhot, 14/08/2026.
// Duels pillar, bugfix (player report, group conversation): a duel challenge came from a witness present in
// the room, "speaking on their own turn", and got mechanically armed as that witness against the player, even
// though the fiction described a quarrel between two OTHER people. challenge_duel carries no target
// parameter, so ResolveDuelChallenge always used whoever spoke as "opponent". This policy is the guard that
// stops that: it feeds ChatViewModel.ResolveDuelChallenge, deciding whether the speaker who just emitted the
// action is a fair player-vs-NPC target at all.

#region

using CalradiaRemembers.Logic.Duels;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Duels
{
   [TestFixture]
   public sealed class DuelInterlocutorPolicyTests
   {
      // The conversation's own NPC is always a legitimate target regardless of any sticky addressee: this is
      // the ordinary one-on-one case the feature was built for, and it must keep working exactly as before.
      [Test]
      public void GIVEN_the_main_speaker_WHEN_checking_THEN_they_are_a_direct_interlocutor()
      {
         DuelInterlocutorPolicy.IsDirectInterlocutor(true, "main_hero", "someone_else").Should().BeTrue();
      }

      // The player addressed this witness directly (the 15B sticky pivot): they are exactly who the player is
      // talking to this turn, so a challenge from them is a fair player-vs-NPC target.
      [Test]
      public void GIVEN_a_witness_the_player_has_addressed_WHEN_checking_THEN_they_are_a_direct_interlocutor()
      {
         DuelInterlocutorPolicy.IsDirectInterlocutor(false, "witness_c", "witness_c").Should().BeTrue();
      }

      // The exact bug: a witness present in the room but NOT the one the player is addressing this turn is
      // speaking on their own: a quarrel with someone else in the room, not the player. Arming a duel against
      // them mis-targets the fight (Gabriel's report: the quarrel was A-vs-C, the mod armed C-vs-player).
      [Test]
      public void GIVEN_a_witness_who_is_not_the_addressed_speaker_WHEN_checking_THEN_they_are_not_an_interlocutor()
      {
         DuelInterlocutorPolicy.IsDirectInterlocutor(false, "witness_c", "witness_a").Should().BeFalse();
      }

      // No one is currently addressed (null/empty sticky id): a non-main speaker cannot be the player's
      // interlocutor by definition, so this must fail safe to "not an interlocutor" rather than matching on
      // two empty strings.
      [Test]
      public void GIVEN_nobody_is_currently_addressed_WHEN_checking_a_witness_THEN_they_are_not_an_interlocutor()
      {
         DuelInterlocutorPolicy.IsDirectInterlocutor(false, "witness_c", null).Should().BeFalse();
         DuelInterlocutorPolicy.IsDirectInterlocutor(false, null, null).Should().BeFalse();
      }
   }
}
