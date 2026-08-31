// Code written by Gabriel Mailhot, 31/08/2026.
// CaptorSceneGrudgePolicy decides whether a closed captor scene (the player holding an NPC prisoner) plants a
// CaptiveAbuse grudge. It exists because a player reported (Mimo, Standard mode) that keeping a captive SAFE and
// well treated still planted an abuse grudge: the old code formed the grudge from the opening INTENT alone. The
// rule under test makes the intent only a CEILING and lets the player's actual CONDUCT decide, with a
// conservative fallback when the conduct cannot be read. These tests pin both edges: a merciful scene must never
// resent the player, and a genuinely abusive one still must.

#region

using CalradiaRemembers.Logic.Grudges;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Grudges
{
    [TestFixture]
    public sealed class CaptorSceneGrudgePolicyTests
    {
        private const int Reward = 0;
        private const int Interrogation = 1;
        private const int Domination = 2;
        private const int Torture = 3;

        // STAKE: a Reward scene (you rewarded your own prisoner) must never plant resentment, whatever else is
        // read. The severity floor short-circuits before any conduct check.
        [Test]
        public void GIVEN_a_reward_scene_WHEN_deciding_THEN_no_grudge_regardless_of_conduct()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Reward, abuseConfirmed: true).Should().Be(0);
            CaptorSceneGrudgePolicy.GrudgeSeverity(Reward, abuseConfirmed: null).Should().Be(0);
        }

        // STAKE: THE player report. A merciful interrogation, read or not, must plant nothing. With no conduct
        // read (null, the Standard case the player hit), the conservative fallback must suppress it.
        [Test]
        public void GIVEN_an_interrogation_with_no_conduct_read_WHEN_deciding_THEN_no_grudge()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Interrogation, abuseConfirmed: null).Should().Be(0);
        }

        // STAKE: the same for a lesser-but-coercive intent. A domination the player carried out with restraint,
        // with no reliable read, must not resent them just because of how the scene was framed at the start.
        [Test]
        public void GIVEN_a_domination_with_no_conduct_read_WHEN_deciding_THEN_no_grudge()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Domination, abuseConfirmed: null).Should().Be(0);
        }

        // STAKE: torture is abuse by its very nature, physical torment leaves a mark whatever is said, so even
        // with no read available it must still form (at its full ceiling). This is the line the conservative
        // fallback draws.
        [Test]
        public void GIVEN_torture_with_no_conduct_read_WHEN_deciding_THEN_it_forms_at_full_severity()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Torture, abuseConfirmed: null).Should().Be(Torture);
        }

        // STAKE: when the conduct CAN be read and confirms real abuse, a lesser intent forms at the ceiling the
        // intent allows (the read re-enables what the conservative fallback withheld).
        [Test]
        public void GIVEN_a_domination_with_confirmed_abuse_WHEN_deciding_THEN_it_forms_at_the_intent_ceiling()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Domination, abuseConfirmed: true).Should().Be(Domination);
        }

        // STAKE: a confirmed-merciful read overrides the intent entirely. Even a scene OPENED as torture, if the
        // read says the player showed mercy, plants nothing: conduct beats intent, which is the whole point.
        [Test]
        public void GIVEN_confirmed_mercy_WHEN_deciding_THEN_nothing_forms_even_for_a_torture_intent()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Torture, abuseConfirmed: false).Should().Be(0);
            CaptorSceneGrudgePolicy.GrudgeSeverity(Domination, abuseConfirmed: false).Should().Be(0);
        }

        // STAKE: a confirmed-abuse read is CAPPED by the intent, never amplified past it, so a rough
        // interrogation cannot be punished as if it were torture.
        [Test]
        public void GIVEN_an_interrogation_with_confirmed_abuse_WHEN_deciding_THEN_it_is_capped_at_the_intent()
        {
            CaptorSceneGrudgePolicy.GrudgeSeverity(Interrogation, abuseConfirmed: true).Should().Be(Interrogation);
        }
    }
}
