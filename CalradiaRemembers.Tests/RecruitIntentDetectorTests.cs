// Code written by Gabriel Mailhot, 28/06/2026.
// Documents the gate that decides when the NPC is asked where to FIND a companion, so the host only
// then injects the real "wanderers for hire" facts. The flagship case is the reported "Oros" bug: the
// player asked where to find a companion and the NPC invented one because nothing grounded the answer.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RecruitIntentDetectorTests
    {
        [Test]
        public void GIVEN_the_reported_line_WHEN_detecting_THEN_it_reads_as_a_recruiting_question()
        {
            // The exact shape of the screenshot that prompted this feature.
            RecruitIntentDetector.Detect("In exchange, tell me where I ca find a companion.")
                .Should().BeTrue();
        }

        [Test]
        public void GIVEN_various_recruiting_phrasings_WHEN_detecting_THEN_each_is_caught()
        {
            RecruitIntentDetector.Detect("Where can I hire a sellsword?").Should().BeTrue();
            RecruitIntentDetector.Detect("I am looking to recruit a wanderer.").Should().BeTrue();
            RecruitIntentDetector.Detect("Do you know any mercenaries for hire?").Should().BeTrue();
            RecruitIntentDetector.Detect("Who might join my party?").Should().BeTrue();
        }

        [Test]
        public void GIVEN_an_ordinary_message_WHEN_detecting_THEN_it_is_not_a_recruiting_question()
        {
            RecruitIntentDetector.Detect("Good day. How fares your harvest this season?")
                .Should().BeFalse();
        }

        [Test]
        public void GIVEN_empty_or_whitespace_WHEN_detecting_THEN_it_is_never_a_match()
        {
            RecruitIntentDetector.Detect(null).Should().BeFalse();
            RecruitIntentDetector.Detect("").Should().BeFalse();
            RecruitIntentDetector.Detect("   ").Should().BeFalse();
        }

        [Test]
        public void GIVEN_mixed_casing_WHEN_detecting_THEN_the_match_is_case_insensitive()
        {
            RecruitIntentDetector.Detect("WHERE DO I FIND A COMPANION").Should().BeTrue();
        }
    }
}
