// Code written by Gabriel Mailhot, 09/09/2026.
// What this pins: whether a lord's written offer to buy one of your prisoners can still be honoured when you open
// the letter, and what the player is told when it cannot.
//
// Why it exists. A player reported (Nexus, 2026-09-08): "Lords are writing me to offer some good sums of coin for
// the release of prisoners, and my only option is to decline the letter. I wanted to accept an offer, but was
// forced to decline." The Accept button was already withheld for a captive no longer in his prison, which is the
// right rule: you cannot sell someone you do not hold. What was wrong is that the mod knew the reason and said
// nothing, so a lapsed offer and a broken feature looked identical from his seat. He opened with "this isn't
// necessarily a bug" because he had no way to tell.
//
// So the reason became a value rather than a hidden boolean, and the same value now feeds two places: the sentence
// in the reading window, and a line written when the letter ARRIVES. That second one is deliberate. Nobody yet
// knows how often these offers land too late to take, and if it turns out to be most of them the answer will be to
// speed this kind of letter, NOT to interfere with the vanilla ransom offer, which the player answers themselves
// through a map notification that auto-declines after two days.
//
// If these tests fail, a player is about to be handed a generous offer, a single Decline button, and no idea why.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class PrisonerOfferAvailabilityPolicyTests
    {
        // The ordinary case, and the one that must never be blocked by mistake: the terms are there and the
        // captive is still in your prison, so the trade is exactly as sellable as the letter says.
        [Test]
        public void GIVEN_the_captive_is_still_held_WHEN_judged_THEN_the_offer_can_be_taken()
        {
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = true, HasAmount = true, CaptiveResolved = true, StillHeld = true})
                .Should().Be(PrisonerOfferBlock.None);
            PrisonerOfferAvailabilityPolicy.Describe(PrisonerOfferBlock.None, "Derthert").Should().BeEmpty();
        }

        // The reported case. The rule itself is right, so what is pinned here is that the refusal now carries a
        // REASON, and one naming the person, since "Derthert is no longer your prisoner" is an explanation while a
        // missing button is an accusation against the mod.
        [Test]
        public void GIVEN_the_captive_has_left_your_prison_WHEN_judged_THEN_it_says_so_by_name()
        {
            PrisonerOfferBlock block = PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = true, HasAmount = true, CaptiveResolved = true, StillHeld = false});

            block.Should().Be(PrisonerOfferBlock.NoLongerHeld);
            PrisonerOfferAvailabilityPolicy.Describe(block, "Derthert")
                .Should().Contain("Derthert").And.Contain("no longer your prisoner");
        }

        // Three failures that look identical to a player and are not identical to us: without telling them apart
        // in the log, the next report of this cannot be diagnosed any faster than the first one was.
        [Test]
        public void GIVEN_each_way_an_offer_can_lapse_WHEN_judged_THEN_they_are_told_apart()
        {
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = false, HasAmount = true, CaptiveResolved = true, StillHeld = true}).Should().Be(PrisonerOfferBlock.TermsMissing);
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = true, HasAmount = false, CaptiveResolved = true, StillHeld = true}).Should().Be(PrisonerOfferBlock.TermsMissing);
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = true, HasAmount = true, CaptiveResolved = false, StillHeld = false}).Should().Be(PrisonerOfferBlock.CaptiveUnknown);
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = true, HasAmount = true, CaptiveResolved = true, StillHeld = false}).Should().Be(PrisonerOfferBlock.NoLongerHeld);
        }

        // Missing terms are judged BEFORE anything else, because a letter with no captive and no sum names no
        // trade at all: reporting it as "they are no longer held" would send the next reader hunting a prisoner
        // the letter never actually named.
        [Test]
        public void GIVEN_no_terms_at_all_WHEN_judged_THEN_that_is_the_reason_given_rather_than_a_missing_captive()
        {
            PrisonerOfferAvailabilityPolicy.Evaluate(new PrisonerOfferAvailabilityPolicy.PrisonerOfferFacts {HasTargetId = false, HasAmount = false, CaptiveResolved = false, StillHeld = false})
                .Should().Be(PrisonerOfferBlock.TermsMissing);
        }

        // Every blocked reason must produce something a player can read, and a nameless captive must not produce
        // a sentence with a hole in it where the name should be.
        [TestCase(PrisonerOfferBlock.NoLongerHeld)]
        [TestCase(PrisonerOfferBlock.CaptiveUnknown)]
        [TestCase(PrisonerOfferBlock.TermsMissing)]
        public void GIVEN_any_blocked_offer_WHEN_described_THEN_it_reads_as_a_sentence_even_with_no_name(PrisonerOfferBlock block)
        {
            foreach (string name in new[] {null, "", "   "})
            {
                string note = PrisonerOfferAvailabilityPolicy.Describe(block, name);

                note.Should().NotBeNullOrWhiteSpace();
                note.Should().EndWith(".");
            }
        }
    }
}
