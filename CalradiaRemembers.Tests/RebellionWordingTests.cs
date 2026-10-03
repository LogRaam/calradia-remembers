// Code written by Gabriel Mailhot, 02/10/2026.
// FloatingMammal (Nexus, 02/10/2026): a lady of a house that rose against the Southern Empire, once received back, claimed
// she had fought on the Empire's side; she was not lying, she had never been told. Her house's members now remember the
// rising and the return. If these words are wrong, a former rebel again speaks as a loyalist, or the memory names the
// wrong realm, or the return reads as though the rising never happened.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
    [TestFixture]
    public sealed class RebellionWordingTests
    {
        private static RebellionFacts Rising() => new RebellionFacts {House = "dey Molarn", Realm = "the Southern Empire", Ruler = "Rhagaea", Banner = "the Molarn rebels"};

        // She remembers she was a rebel, against whom, and under what banner.
        [Test]
        public void GIVEN_a_house_that_rose_WHEN_its_member_remembers_THEN_she_knows_she_was_a_rebel_and_against_whom()
        {
            string memory = RebellionWording.RoseMemory(Rising());

            memory.Should().Contain("rose against the Southern Empire and Rhagaea").And.Contain("the Molarn rebels").And.Contain("We were rebels.");
        }

        // A rising with no banner of its own still says it was a rebellion.
        [Test]
        public void GIVEN_a_rising_with_no_banner_WHEN_remembered_THEN_it_is_still_a_rebellion()
        {
            RebellionWording.RoseMemory(new RebellionFacts {House = "dey Molarn", Realm = "the Southern Empire"}).Should().Contain("in open rebellion");
        }

        // FloatingMammal's case exactly: received back, she still knows she fought as a rebel.
        [Test]
        public void GIVEN_a_house_received_back_WHEN_its_member_remembers_THEN_the_rising_is_not_erased()
        {
            RebellionWording.ReturnedMemory(Rising()).Should().Contain("received back into the Southern Empire").And.Contain("We fought as rebels");
        }
    }
}
