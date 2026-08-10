// Code written by Gabriel Mailhot, 10/08/2026.
// Guards LoverConceptionPolicy (Inc 3c): whether a fertile partner conceives by her lover on a tryst. A
// conception may happen only when the affair is active (a lover is taken), the mother is fertile (the host's
// PregnancyService.IsEligible read), and a modest per-tryst roll lands. This is the trigger for the lover-child
// attribution pipeline (the child belongs to the lover, never the player). Get it wrong and either no lover-child
// is ever conceived (the whole Inc 3c line is dead), or a conception is attempted for a partner with no lover or
// an infertile mother (an engine conception with no genetic father, or on a woman the engine will never deliver).

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class LoverConceptionPolicyTests
    {
        // The core precondition: a lover-child needs a lover. With no active affair there is no genetic father,
        // so no conception may be attempted; without this gate the tryst pass would ask the engine to conceive a
        // child fathered by no one.
        [Test]
        public void GIVEN_no_active_affair_WHEN_asked_THEN_no_conception()
        {
            LoverConceptionPolicy.ShouldConceive(affairActive: false, motherFertile: true, rollPercent: 0)
                                 .Should().BeFalse("a lover-child requires a lover to father it");
        }

        // Fertility is the host's PregnancyService.IsEligible read (female, alive, fertile age, not already
        // carrying, births enabled). An infertile mother must never be put forward: the engine would either
        // refuse or, worse, sit on a pregnancy it will never deliver.
        [Test]
        public void GIVEN_an_infertile_mother_WHEN_asked_THEN_no_conception()
        {
            LoverConceptionPolicy.ShouldConceive(affairActive: true, motherFertile: false, rollPercent: 0)
                                 .Should().BeFalse("only a fertile mother the engine will deliver may conceive");
        }

        // The boundary the mod's MBRandom roll is compared against: a roll UNDER the chance conceives, a roll AT
        // or ABOVE it does not. Pins the strictly-less comparison so the ChancePercent knob actually governs the
        // frequency rather than an off-by-one letting every tryst (or no tryst) conceive.
        [Test]
        public void GIVEN_an_active_affair_and_a_fertile_mother_WHEN_the_roll_lands_under_the_chance_THEN_she_conceives()
        {
            LoverConceptionPolicy.ShouldConceive(affairActive: true, motherFertile: true, rollPercent: LoverConceptionPolicy.ChancePercent - 1)
                                 .Should().BeTrue("a roll below the chance means a lover-child is conceived this tryst");
            LoverConceptionPolicy.ShouldConceive(affairActive: true, motherFertile: true, rollPercent: LoverConceptionPolicy.ChancePercent)
                                 .Should().BeFalse("a roll at the chance is outside it (strictly-less comparison)");
        }
    }
}
