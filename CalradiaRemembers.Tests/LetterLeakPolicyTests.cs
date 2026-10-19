// Code written by Gabriel Mailhot, 24/08/2026.
// What makes an intercepted letter anyone's secret: substance. Since 26/09/2026 the old leak roll and antagonist pick
// are gone (Gabriel: an intercepted letter with substance ALWAYS becomes a secret carried in the world, see
// CourierSecretPolicyTests); what remains is the bar a letter must clear, so a one-line note is never a "secret".

#region

using CalradiaRemembers.Logic.Letters;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LetterLeakPolicyTests
    {
        // Substance heuristic: a short note is below the bar; a fuller letter clears it.
        [Test]
        public void GIVEN_content_length_THEN_substance_tracks_the_minimum()
        {
            LetterLeakPolicy.HasSubstance("Meet me at Sargot.").Should().BeFalse();
            LetterLeakPolicy.HasSubstance(null).Should().BeFalse();
            LetterLeakPolicy.HasSubstance("   ").Should().BeFalse();
            LetterLeakPolicy.HasSubstance("I mean to march on Pravend the moment the Vlandian host turns south; hold your oath.").Should().BeTrue();
        }
    }
}
