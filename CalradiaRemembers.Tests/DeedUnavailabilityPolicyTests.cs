// Code written by Gabriel Mailhot, 09/09/2026.
// What this pins: how the mod tells an NPC, before it speaks, which deeds this conversation cannot deliver.
//
// Why it exists. Gabriel's ruling, 09/09/2026: "Le LLM declare, et un gating peut ensuite confirmer ou refuter.
// C'est la qu'il peut y avoir une situation frustrante pour le joueur. Il faudrait que le LLM declare en
// connaissant deja le gating." Players had been describing the symptom for weeks from the other end: an NPC who
// promises troops, influence, or feelings, and a system message a moment later saying it cannot be done.
//
// The cause was that a verb had two states and needed three. It was either TAUGHT, with its whole [ACTION] block,
// or ABSENT. Absence does not read to a language model as "forbidden"; it reads as "not mentioned", so the model
// supplies the permission itself. Meanwhile each of those verbs already owned a perfectly good in-world sentence
// explaining the refusal, sitting in Execute, which is to say running only AFTER the NPC had already promised.
//
// The three rules this file exists to keep honest, in descending order of how much a mistake would cost:
//   1. A refusal forbids the PROMISE, not merely the deed. An NPC who says "I will see it done later" has failed
//      in exactly the way the player complained about.
//   2. Half a refusal is worse than none. A deed with no reason, or a reason with no deed, is dropped entirely.
//   3. The block is bounded. It rides in the Full prompt beside real teachings and must never grow without limit.
//
// If these tests fail, an NPC is about to promise something the game will refuse, or the prompt is about to carry
// a list of the fourteen things this conversation was never about.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Logic.Verbs;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class DeedUnavailabilityPolicyTests
    {
        private static DeedRefusal Refusal(string deed = "lend them soldiers", string reason = "your own party is under strength")
            => new() {Deed = deed, Reason = reason};

        // The ordinary case, and every element of it is load-bearing: the deed, the reason, and the three
        // prohibitions. A block that named the deed and forgot to forbid the promise would leave the exact
        // behaviour players reported.
        [Test]
        public void GIVEN_one_shut_deed_WHEN_composed_THEN_the_deed_the_reason_and_the_ban_on_promising_are_all_there()
        {
            string block = DeedUnavailabilityPolicy.Compose(new[] {Refusal()});

            block.Should().Contain("lend them soldiers")
                 .And.Contain("your own party is under strength")
                 .And.Contain("do not promise it")
                 .And.Contain("for later"); // a deferred promise is the same promise
        }

        // A model handed a list will read the list aloud. The instruction not to is part of the contract, not
        // decoration: the player must hear a character, never an inventory of denials.
        [Test]
        public void GIVEN_any_shut_deed_WHEN_composed_THEN_the_model_is_told_to_speak_in_character_and_not_quote_the_list()
        {
            string block = DeedUnavailabilityPolicy.Compose(new[] {Refusal()});

            block.Should().Contain("in character").And.Contain("Never quote");
        }

        // Silence is the default and must cost nothing at all. Most conversations shut no deed worth naming, and
        // an empty header would still be tokens spent on every single turn of every single conversation.
        [Test]
        public void GIVEN_nothing_shut_WHEN_composed_THEN_the_prompt_carries_not_one_character()
        {
            DeedUnavailabilityPolicy.Compose(null).Should().BeEmpty();
            DeedUnavailabilityPolicy.Compose(new List<DeedRefusal>()).Should().BeEmpty();
            DeedUnavailabilityPolicy.Compose(new DeedRefusal[] {null!, null!}).Should().BeEmpty();
        }

        // Rule 2. A refusal missing either half cannot be spoken, and rendering it anyway would put "You cannot
        // : ." into a prompt whose whole purpose is to stop the NPC saying something incoherent.
        [TestCase(null, "a reason")]
        [TestCase("", "a reason")]
        [TestCase("   ", "a reason")]
        [TestCase("a deed", null)]
        [TestCase("a deed", "")]
        [TestCase("a deed", "   ")]
        public void GIVEN_half_a_refusal_WHEN_composed_THEN_it_is_dropped_rather_than_half_rendered(string deed, string reason)
        {
            DeedUnavailabilityPolicy.Clean(new[] {new DeedRefusal {Deed = deed, Reason = reason}}).Should().BeEmpty();
            DeedUnavailabilityPolicy.Compose(new[] {new DeedRefusal {Deed = deed, Reason = reason}}).Should().BeEmpty();
        }

        // One good refusal must survive beside a broken one: a verb returning nonsense costs its own line, never
        // the whole block. Same containment discipline as a verb whose eligibility throws.
        [Test]
        public void GIVEN_a_broken_refusal_beside_a_sound_one_WHEN_composed_THEN_only_the_broken_one_is_lost()
        {
            string block = DeedUnavailabilityPolicy.Compose(new[] {
                new DeedRefusal {Deed = "  ", Reason = "nonsense"},
                Refusal("lend them your voice at court", "you do not trust them enough")
            });

            block.Should().Contain("lend them your voice at court").And.NotContain("nonsense");
        }

        // Rule 3. Two verbs naming the same deed (a favour and its reciprocal, say) must not say it twice, and
        // the cap holds whatever a future verb does, because the cost lands in a prompt budget shared with the
        // teachings themselves.
        [Test]
        public void GIVEN_more_shut_deeds_than_the_cap_WHEN_composed_THEN_the_block_stays_bounded_and_never_repeats_a_deed()
        {
            var many = Enumerable.Range(0, DeedUnavailabilityPolicy.MaxRefusals + 4)
                                 .Select(i => Refusal($"deed number {i}", "some reason"))
                                 .ToList();
            many.Add(Refusal("deed number 0", "a different reason for the same deed"));

            List<DeedRefusal> kept = DeedUnavailabilityPolicy.Clean(many);

            kept.Should().HaveCount(DeedUnavailabilityPolicy.MaxRefusals);
            kept.Select(r => r.Deed).Should().OnlyHaveUniqueItems();
        }

        // Deed naming is case-insensitive for the duplicate check, since two verbs writing the same deed with
        // different capitalisation is a near-certainty and reads to a player as the NPC stammering.
        [Test]
        public void GIVEN_the_same_deed_in_different_case_WHEN_cleaned_THEN_it_is_still_one_deed()
        {
            DeedUnavailabilityPolicy.Clean(new[] {Refusal("Lend Them Soldiers"), Refusal("lend them soldiers")})
                                    .Should().HaveCount(1);
        }

        // A reason ending in its own full stop must not produce "under strength.." and a runaway reason must be
        // bounded rather than dropped: losing the refusal entirely would be the more damaging failure.
        [Test]
        public void GIVEN_untidy_reasons_WHEN_cleaned_THEN_they_are_tidied_rather_than_lost()
        {
            DeedUnavailabilityPolicy.Clean(new[] {Refusal(reason: "your own party is under strength.")})
                                    .Single().Reason.Should().Be("your own party is under strength");

            string runaway = new string('x', DeedUnavailabilityPolicy.MaxReasonChars + 200);
            DeedRefusal bounded = DeedUnavailabilityPolicy.Clean(new[] {Refusal(reason: runaway)}).Single();

            bounded.Reason.Should().NotBeEmpty().And.HaveLength(DeedUnavailabilityPolicy.MaxReasonChars);
            DeedUnavailabilityPolicy.Compose(new[] {Refusal(reason: runaway)}).Should().Contain("You cannot");
        }

        // One sentence per shut deed, and the deed named in the NPC's own second person. A refusal that leaked a
        // verb id would teach the model a token it must never emit, which is how an untaught action gets emitted.
        [Test]
        public void GIVEN_several_shut_deeds_WHEN_composed_THEN_each_is_one_line_and_no_verb_id_is_ever_shown()
        {
            string block = DeedUnavailabilityPolicy.Compose(new[] {
                Refusal("lend them soldiers from your own party", "you do not trust them enough yet"),
                Refusal("lend them your voice at court", "your clan's influence is already committed")
            });

            block.Split('\n').Count(l => l.TrimStart().StartsWith("- You cannot")).Should().Be(2);
            block.Should().NotContain("lend_troops").And.NotContain("give_influence").And.NotContain("[ACTION]");
        }

        // Order is the verbs' registration order and must survive cleaning, so the block reads the same way twice
        // running for the same campaign state instead of shuffling under the model between turns.
        [Test]
        public void GIVEN_several_shut_deeds_WHEN_cleaned_THEN_they_keep_the_order_the_verbs_gave_them()
        {
            DeedUnavailabilityPolicy.Clean(new[] {Refusal("first deed"), Refusal("second deed"), Refusal("third deed")})
                                    .Select(r => r.Deed)
                                    .Should().ContainInOrder("first deed", "second deed", "third deed");
        }
    }
}
