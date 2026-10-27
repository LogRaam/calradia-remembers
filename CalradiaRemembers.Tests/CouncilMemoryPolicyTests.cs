// Code written by Gabriel Mailhot, 27/10/2026.
// CouncilMemoryPolicy decides what each character present keeps of a conversation, beside the one addressed. DepaHN
// (Nexus, 26/10/2026): "What is said in a council meeting stays in the meeting, and the characters remember nothing of
// it afterward." If this is wrong, council members forget the sitting again, or every onlooker of an ordinary talk is
// handed a summary of a conversation they only watched.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class CouncilMemoryPolicyTests
   {
      // STAKE: DepaHN's report: every member seated must keep what was said and decided, not only the anchor.
      [Test]
      public void GIVEN_a_member_seated_at_a_council_WHEN_it_closes_THEN_they_keep_the_sitting()
      {
         CouncilMemoryPolicy.Keeps(new WitnessFacts {InCouncil = true, HasMemory = true}).Should().Be(WitnessKeeps.TheSitting);
      }

      // STAKE: an onlooker to an ordinary talk keeps that they were there, as before; a council is not every conversation.
      [Test]
      public void GIVEN_an_onlooker_to_an_ordinary_conversation_WHEN_it_closes_THEN_they_keep_that_they_were_present()
      {
         CouncilMemoryPolicy.Keeps(new WitnessFacts {InCouncil = false, HasMemory = true}).Should().Be(WitnessKeeps.ThatTheyWerePresent);
      }

      // STAKE: LapitaPolata (Nexus, 27/10/2026): a witness who spoke in another conversation came back remembering only that
      // they had witnessed it. Having spoken, they keep what was said, as the one addressed does.
      [Test]
      public void GIVEN_a_witness_who_spoke_in_an_ordinary_conversation_WHEN_it_closes_THEN_they_keep_the_conversation()
      {
         CouncilMemoryPolicy.Keeps(new WitnessFacts {InCouncil = false, HasMemory = true, SpokeInIt = true}).Should().Be(WitnessKeeps.TheConversation);
      }

      // STAKE: the one addressed keeps the conversation's own memory; a second, witness one would double it.
      [Test]
      public void GIVEN_the_one_addressed_or_someone_with_no_memory_WHEN_it_closes_THEN_nothing_more_is_kept()
      {
         CouncilMemoryPolicy.Keeps(new WitnessFacts {InCouncil = true, HasMemory = true, IsTheOneAddressed = true}).Should().Be(WitnessKeeps.Nothing);
         CouncilMemoryPolicy.Keeps(new WitnessFacts {InCouncil = true, HasMemory = false}).Should().Be(WitnessKeeps.Nothing);
      }
   }
}
