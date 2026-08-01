// Code written by Gabriel Mailhot, 01/08/2026.
// A2's adaptive layer: CorrespondenceInterestPolicy turns the player's correspondence behaviour, implicit
// (read but ignored, or actually answered) and explicit (a stated preference), into the deltas that
// eventually reach LetterWeights.WeightWithInterest. Get a sign or a floor wrong here and the mailbox either
// never learns anything (a damper that does not damp) or, worse, learns to silence a correspondent
// completely (a floor that is not held), which is exactly the outcome EffectiveWeightFloor exists to forbid.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class CorrespondenceInterestTests
    {
        // A read-but-ignored letter is a mild signal, not a snub: it must cost the sender and the subject
        // something, but never touch global interest, or one quiet correspondent would dampen everybody
        // else's mail too.
        [Test]
        public void GIVEN_an_acknowledged_but_unanswered_letter_WHEN_the_delta_is_computed_THEN_sender_and_subject_dip_but_global_is_untouched()
        {
            InterestDeltas delta = CorrespondenceInterestPolicy.OnAcknowledge();

            delta.SenderDelta.Should().Be(CorrespondenceInterestPolicy.AcknowledgeSenderPenalty);
            delta.SubjectDelta.Should().Be(CorrespondenceInterestPolicy.AcknowledgeSubjectPenalty);
            delta.GlobalDelta.Should().Be(0);
            delta.SenderDelta.Should().BeNegative();
            delta.SubjectDelta.Should().BeNegative();
        }

        // A genuine reply is the strongest possible interest signal and must recover sender interest hard,
        // able to undo several Acknowledge penalties in a single reply, while global stays untouched for the
        // same reason as OnAcknowledge.
        [Test]
        public void GIVEN_the_player_actually_replies_WHEN_the_delta_is_computed_THEN_sender_recovers_sharply_and_global_is_untouched()
        {
            InterestDeltas delta = CorrespondenceInterestPolicy.OnReply();

            delta.SenderDelta.Should().Be(CorrespondenceInterestPolicy.ReplySenderBonus);
            delta.SubjectDelta.Should().Be(CorrespondenceInterestPolicy.ReplySubjectBonus);
            delta.GlobalDelta.Should().Be(0);
            delta.SenderDelta.Should().BePositive();
            delta.SenderDelta.Should().BeGreaterThan(delta.SubjectDelta);
        }

        // The core "reaches but never passes the floor" guarantee: a sender repeatedly acknowledged and
        // never replied to must settle exactly at SenderFloor, never below it, so even the most ignored
        // correspondent keeps their sliver of a chance.
        [Test]
        public void GIVEN_a_sender_acknowledged_over_and_over_WHEN_clamped_after_each_step_THEN_interest_reaches_but_never_passes_the_floor()
        {
            int running = 0;

            for (int i = 0; i < 50; i++)
            {
                running += CorrespondenceInterestPolicy.OnAcknowledge().SenderDelta;
                running = CorrespondenceInterestPolicy.ClampSender(running);
            }

            running.Should().Be(CorrespondenceInterestPolicy.SenderFloor);
        }

        // A reply after a run of acknowledges must genuinely lift the sender back toward zero, proving the
        // player can repair a soured correspondence rather than being stuck at the floor forever.
        [Test]
        public void GIVEN_several_acknowledges_WHEN_a_reply_follows_THEN_sender_interest_lifts_back_toward_zero()
        {
            int running = 0;
            for (int i = 0; i < 5; i++)
            {
                running += CorrespondenceInterestPolicy.OnAcknowledge().SenderDelta;
                running = CorrespondenceInterestPolicy.ClampSender(running);
            }

            int beforeReply = running;

            running += CorrespondenceInterestPolicy.OnReply().SenderDelta;
            running = CorrespondenceInterestPolicy.ClampSender(running);

            running.Should().BeGreaterThan(beforeReply);
        }

        // DeltasFor must touch ONLY the channel named by Scope. A Sender-scoped preference that leaked into
        // Subject or Global would make "write less often, Marlene" also quiet down every other correspondent
        // or subject, which is not what the player asked for.
        [TestCase(PreferenceScope.Sender, PreferenceDirection.Less, PreferenceStrength.Strong, -CorrespondenceInterestPolicy.SenderStrongMagnitude, 0, 0)]
        [TestCase(PreferenceScope.Sender, PreferenceDirection.Less, PreferenceStrength.Mild, -CorrespondenceInterestPolicy.SenderMildMagnitude, 0, 0)]
        [TestCase(PreferenceScope.Sender, PreferenceDirection.More, PreferenceStrength.Strong, CorrespondenceInterestPolicy.SenderStrongMagnitude, 0, 0)]
        [TestCase(PreferenceScope.Sender, PreferenceDirection.More, PreferenceStrength.Mild, CorrespondenceInterestPolicy.SenderMildMagnitude, 0, 0)]
        [TestCase(PreferenceScope.Subject, PreferenceDirection.Less, PreferenceStrength.Strong, 0, -CorrespondenceInterestPolicy.SubjectStrongMagnitude, 0)]
        [TestCase(PreferenceScope.Subject, PreferenceDirection.Less, PreferenceStrength.Mild, 0, -CorrespondenceInterestPolicy.SubjectMildMagnitude, 0)]
        [TestCase(PreferenceScope.Subject, PreferenceDirection.More, PreferenceStrength.Strong, 0, CorrespondenceInterestPolicy.SubjectStrongMagnitude, 0)]
        [TestCase(PreferenceScope.Subject, PreferenceDirection.More, PreferenceStrength.Mild, 0, CorrespondenceInterestPolicy.SubjectMildMagnitude, 0)]
        [TestCase(PreferenceScope.All, PreferenceDirection.Less, PreferenceStrength.Strong, 0, 0, -CorrespondenceInterestPolicy.AllStrongMagnitude)]
        [TestCase(PreferenceScope.All, PreferenceDirection.Less, PreferenceStrength.Mild, 0, 0, -CorrespondenceInterestPolicy.AllMildMagnitude)]
        [TestCase(PreferenceScope.All, PreferenceDirection.More, PreferenceStrength.Strong, 0, 0, CorrespondenceInterestPolicy.AllStrongMagnitude)]
        [TestCase(PreferenceScope.All, PreferenceDirection.More, PreferenceStrength.Mild, 0, 0, CorrespondenceInterestPolicy.AllMildMagnitude)]
        [TestCase(PreferenceScope.None, PreferenceDirection.Less, PreferenceStrength.Strong, 0, 0, 0)]
        public void GIVEN_a_stated_preference_WHEN_the_delta_is_computed_THEN_only_the_scoped_channel_moves_by_the_named_magnitude(
            PreferenceScope scope, PreferenceDirection direction, PreferenceStrength strength,
            int expectedSender, int expectedSubject, int expectedGlobal)
        {
            var pref = new CorrespondencePreference(scope, direction, strength);

            InterestDeltas delta = CorrespondenceInterestPolicy.DeltasFor(pref);

            delta.SenderDelta.Should().Be(expectedSender);
            delta.SubjectDelta.Should().Be(expectedSubject);
            delta.GlobalDelta.Should().Be(expectedGlobal);
        }

        // The default, unstated preference must be a true no-op: nothing about the mailbox should move for
        // an NPC who never expressed any preference at all.
        [Test]
        public void GIVEN_CorrespondencePreference_None_WHEN_the_delta_is_computed_THEN_every_channel_is_zero()
        {
            InterestDeltas delta = CorrespondenceInterestPolicy.DeltasFor(CorrespondencePreference.None);

            delta.SenderDelta.Should().Be(0);
            delta.SubjectDelta.Should().Be(0);
            delta.GlobalDelta.Should().Be(0);
        }

        // Each clamp must hold its OWN floor, and must never let a value climb past the shared neutral
        // ceiling either: interest is a damper, never a bonus above zero.
        [TestCase(-1000, CorrespondenceInterestPolicy.SenderFloor)]
        [TestCase(1000, 0)]
        [TestCase(-10, -10)]
        public void GIVEN_a_value_outside_range_WHEN_ClampSender_is_applied_THEN_it_holds_the_sender_floor_and_ceiling(int input, int expected)
        {
            CorrespondenceInterestPolicy.ClampSender(input).Should().Be(expected);
        }

        [TestCase(-1000, CorrespondenceInterestPolicy.SubjectFloor)]
        [TestCase(1000, 0)]
        [TestCase(-5, -5)]
        public void GIVEN_a_value_outside_range_WHEN_ClampSubject_is_applied_THEN_it_holds_the_subject_floor_and_ceiling(int input, int expected)
        {
            CorrespondenceInterestPolicy.ClampSubject(input).Should().Be(expected);
        }

        [TestCase(-1000, CorrespondenceInterestPolicy.GlobalFloor)]
        [TestCase(1000, 0)]
        [TestCase(-15, -15)]
        public void GIVEN_a_value_outside_range_WHEN_ClampGlobal_is_applied_THEN_it_holds_the_global_floor_and_ceiling(int input, int expected)
        {
            CorrespondenceInterestPolicy.ClampGlobal(input).Should().Be(expected);
        }
    }
}
