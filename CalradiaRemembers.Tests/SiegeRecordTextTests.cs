// Code written by Gabriel Mailhot, 23/10/2026.
// SiegeRecordText words the news of a siege begun and a siege lifted. chmidt (Nexus, 23/10/2026): a lord would not
// believe that someone had besieged his city, knowing only who held it afterwards. If this is wrong, the news never names
// the besieger, or weighs too little for the two realms to hear of it.

#region

using CalradiaRemembers.Logic.Battles;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class SiegeRecordTextTests
   {
      private static readonly SiegeNews Pravend = new() {Besieger = "Derthert of the dey Meroc", BesiegerRealm = "Vlandia", Place = "Pravend", PlaceRealm = "the Northern Empire"};

      // STAKE: chmidt: the besieger is named, with both realms, so "X besieged your city" has something behind it.
      [Test]
      public void GIVEN_a_siege_begun_WHEN_worded_THEN_it_names_the_besieger_the_place_and_both_realms()
      {
         SiegeRecordText.Started(Pravend).Should().Be("Derthert of the dey Meroc (Vlandia) laid siege to Pravend of the Northern Empire.");
      }

      // STAKE: a siege that failed is news too, and says who failed.
      [Test]
      public void GIVEN_a_siege_lifted_WHEN_worded_THEN_it_says_the_walls_held()
      {
         SiegeRecordText.Lifted(Pravend).Should().Be("The siege of Pravend of the Northern Empire by Derthert of the dey Meroc (Vlandia) was lifted; the walls held.");
      }

      // STAKE: a siege with no lord at its head still reads as a sentence.
      [Test]
      public void GIVEN_no_lord_and_no_realm_WHEN_worded_THEN_it_still_reads()
      {
         SiegeRecordText.Started(new SiegeNews {Place = "Pravend"}).Should().Be("A host laid siege to Pravend.");
      }

      // STAKE: the realm's own people must hear of it: magnitude plus the realm's 25 clears the awareness threshold of 50.
      [Test]
      public void GIVEN_the_weight_of_the_news_WHEN_a_realm_member_weighs_it_THEN_it_clears_the_threshold()
      {
         (SiegeRecordText.StartedMagnitude + 25).Should().BeGreaterThanOrEqualTo(50);
         (SiegeRecordText.LiftedMagnitude + 25).Should().BeGreaterThanOrEqualTo(50);
      }
   }
}
