// Code written by Gabriel Mailhot, 18/10/2026.
// Which village a written tale may be about, and when the tale ends with it. fkasad (Nexus, 18/10/2026): the Drowned
// Bridge sent him to a burned village whose town was besieged and fell, so its people could not be spoken to and the
// tale asked him to retake a town. If these rules are wrong, a tale is set in a village nobody can be found in, or goes
// on after its giver has lost the village, or every tale stops finding a village.

#region

using System.Collections.Generic;
using CalradiaRemembers.Logic.AuthoredQuests;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class TaleVillagePolicyTests
   {
      // STAKE: the report: a looted village, a village under raid, a village whose fortress is besieged are all passed
      // over for a quiet one further down the giver's list.
      [Test]
      public void GIVEN_a_looted_a_raided_and_a_besieged_village_before_a_quiet_one_WHEN_picking_THEN_the_quiet_one_is_chosen()
      {
         var villages = new List<TaleVillageFacts> {
            new() {Name = "Ortongard", Looted = true},
            new() {Name = "Ismenia", BeingRaided = true},
            new() {Name = "Rhotae", FortressUnderSiege = true},
            new() {Name = "Hertogea"}
         };

         TaleVillagePolicy.Pick(villages).Name.Should().Be("Hertogea");
      }

      // STAKE: when no village is quiet the tale is not offered now (it comes back later), and the log says why.
      [Test]
      public void GIVEN_no_quiet_village_WHEN_picking_THEN_none_and_the_reason_names_each()
      {
         var villages = new List<TaleVillageFacts> {new() {Name = "Ortongard", Looted = true}, new() {Name = "Rhotae", FortressUnderSiege = true}};

         TaleVillagePolicy.Pick(villages).Should().BeNull();
         TaleVillagePolicy.WhyNoneQuiet(villages).Should().Be("Ortongard is looted, Rhotae's fortress is under siege");
      }

      // STAKE: a village in peace keeps every tale that worked before: nothing new is required of it.
      [Test]
      public void GIVEN_a_village_at_peace_WHEN_judged_THEN_it_is_quiet()
      {
         TaleVillagePolicy.IsQuiet(new TaleVillageFacts {Name = "Hertogea"}).Should().BeTrue();
      }

      // STAKE: Gabriel 18/10/2026: when the giver's town falls, the tale ends without penalty instead of becoming a
      // campaign to retake it; while his house holds the village, it goes on.
      [Test]
      public void GIVEN_the_village_changes_hands_WHEN_judged_THEN_the_giver_lost_it_and_not_before()
      {
         TaleVillagePolicy.GiverLostIt("clan_a", "clan_b").Should().BeTrue();
         TaleVillagePolicy.GiverLostIt("clan_a", null).Should().BeTrue();
         TaleVillagePolicy.GiverLostIt("clan_a", "clan_a").Should().BeFalse();
         TaleVillagePolicy.GiverLostIt(null, "clan_b").Should().BeFalse();
      }

      // STAKE: the journal's last line says what happened, in the world's words.
      [Test]
      public void GIVEN_a_lost_village_WHEN_the_journal_closes_THEN_it_says_who_holds_it_now()
      {
         TaleVillagePolicy.LostLine("Ortongard", "the Vlandian house of dey Meroc").Should().Be("Ortongard has passed to the Vlandian house of dey Meroc. The matter is lost with it.");
      }
   }
}
