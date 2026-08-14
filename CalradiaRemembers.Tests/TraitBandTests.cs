// Code written by Gabriel Mailhot, 13/08/2026.
// Pins how a Bannerlord trait level (the small signed integer hero.GetTraitLevel returns) becomes the plain
// band word "low"/"moderate"/"high".
//
// WHY IT MATTERS: per TraitBand's own header, this word is what the prose + interpreter bench (cr.prose_interpret_test)
// writes into the action interpreter's context digest to describe the seeded lord ("Honour high, Mercy low"), so the
// interpreter can calibrate a change_relation delta to the character. If the sign-to-word mapping were wrong, the
// digest would misrepresent the very disposition the bench is trying to measure the interpreter against, and the
// comparison between prose models would rest on a lie about who the NPC is.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class TraitBandTests
    {
        // A negative level means the trait's OPPOSITE lean dominates (a cruel lord has negative Mercy). The
        // digest must call that "low", not "moderate", or a proud, unforgiving lord would read as unremarkable
        // and the extractor would under-weight the affront he takes.
        [Test]
        public void GIVEN_a_negative_level_WHEN_describing_THEN_it_reads_low()
        {
            TraitBand.Describe(-2).Should().Be("low");
            TraitBand.Describe(-1).Should().Be("low");
        }

        // Zero is the neutral case: no marked lean either way. It must read "moderate" rather than borrow one of
        // the two extreme words, so a character with no notable trait is not painted as leaning.
        [Test]
        public void GIVEN_a_zero_level_WHEN_describing_THEN_it_reads_moderate()
        {
            TraitBand.Describe(0).Should().Be("moderate");
        }

        // A positive level means the trait itself is pronounced (an honourable lord has positive Honor). It must
        // read "high" so the extractor sees the disposition the seeded profile actually set.
        [Test]
        public void GIVEN_a_positive_level_WHEN_describing_THEN_it_reads_high()
        {
            TraitBand.Describe(1).Should().Be("high");
            TraitBand.Describe(2).Should().Be("high");
        }
    }
}
