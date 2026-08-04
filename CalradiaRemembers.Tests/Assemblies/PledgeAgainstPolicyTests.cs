// Code written by Gabriel Mailhot, 04/08/2026.
// Documents pledge_against's one council-specific caveat: a member must not be allowed to scheme against a
// rival seated at the very same table (COUNCIL_ACTIONS.md's Kimi review, "trahison a table"). The stake across
// every test here is the same: getting this wrong either lets the table launder a public betrayal it never
// priced, or wrongly refuses a legitimate pledge against someone who was never actually present.
#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Assemblies
{
    [TestFixture]
    public sealed class PledgeAgainstPolicyTests
    {
        // The exact case the caveat exists for: the rival named is one of the OTHER seats present when the
        // pledge was struck, so the lift must refuse it rather than let a scheme land against a fellow seat.
        [Test]
        public void GIVEN_the_rival_was_seated_at_this_council_WHEN_checked_THEN_it_is_flagged()
        {
            var witnessIds = new List<string> {"hero_a", "hero_b"};

            PledgeAgainstPolicy.IsRivalSeatedAtCouncil("hero_b", witnessIds).Should().BeTrue();
        }

        // The ordinary case: a rival who never sat at this table must not be blocked by a caveat meant only for
        // table treachery, or every legitimate pledge_against would be refused for no reason.
        [Test]
        public void GIVEN_the_rival_was_not_seated_at_this_council_WHEN_checked_THEN_it_is_not_flagged()
        {
            var witnessIds = new List<string> {"hero_a", "hero_b"};

            PledgeAgainstPolicy.IsRivalSeatedAtCouncil("hero_c", witnessIds).Should().BeFalse();
        }

        // A null or empty witness list (an older save written before audit M6, or a solitary "council" of one)
        // must never match: a missing record is not proof the rival sat here, and a wrong refusal here would
        // silently break every pre-existing pledge_against resolution on load.
        [Test]
        public void GIVEN_a_null_or_empty_witness_list_WHEN_checked_THEN_it_is_never_flagged()
        {
            PledgeAgainstPolicy.IsRivalSeatedAtCouncil("hero_b", null).Should().BeFalse();
            PledgeAgainstPolicy.IsRivalSeatedAtCouncil("hero_b", new List<string>()).Should().BeFalse();
        }

        // A blank or missing target id (the caller's own defensive edge, mirroring every other resolver in this
        // codebase) must never match, whatever the witness list holds.
        [Test]
        public void GIVEN_a_null_or_blank_target_id_WHEN_checked_THEN_it_is_never_flagged()
        {
            var witnessIds = new List<string> {"hero_a"};

            PledgeAgainstPolicy.IsRivalSeatedAtCouncil(null, witnessIds).Should().BeFalse();
            PledgeAgainstPolicy.IsRivalSeatedAtCouncil("", witnessIds).Should().BeFalse();
        }
    }
}
