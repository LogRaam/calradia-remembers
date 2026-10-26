// Code written by Gabriel Mailhot, 25/10/2026.
// JealousyWoundPolicy decides whether a jealous reaction wounds the bond (Committed falls to Estranged) and plants a
// grudge, the "Grievances" an encyclopedia page lists. Zivkyn (Nexus, 25/10/2026): two twins taken as consorts, both
// agreeing to share him, "Yet one always ends up with a Greivence." Gabriel (25/10/2026): "si elles acceptent, elles
// devraient etre plus tolerantes a la jalousie [...] accepter de partager ne veut pas dire accepter etre la deuxieme."
// If this is wrong, either a consort who accepted the household still turns Estranged and nurses a grudge over the
// other consort, or a sentence of acceptance buys immunity for liaisons she never agreed to.

#region

using CalradiaRemembers.Logic.Romance;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Jealousy
{
   [TestFixture]
   public sealed class JealousyWoundPolicyTests
   {
      private static JealousyWoundFacts Facts(JealousyTrack track, bool household, RomanticActTier tier = RomanticActTier.Intimacy,
                                              JealousStake stake = JealousStake.PlayerPartner)
         => new() {Stake = stake, Tier = tier, Track = track, CourtedIsHouseholdPartner = household};

      // STAKE: Zivkyn's twins. A consort who said she accepts, over the other consort, must not be estranged or
      // nurse a grievance: "si elles acceptent, elles devraient etre plus tolerantes".
      [Test]
      public void GIVEN_a_consort_who_accepted_WHEN_the_player_lies_with_the_other_consort_THEN_she_bears_it_without_wound_or_grudge()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.AcceptedAgainstCulture, household: true);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeFalse();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeFalse();
      }

      // STAKE: a woman whose people share partners by custom must never be harsher than one who only gave her word
      // (the 22/09/2026 ordering: custom is the gentlest track).
      [Test]
      public void GIVEN_a_partner_whose_people_share_WHEN_the_player_lies_with_another_partner_of_the_house_THEN_she_bears_it_too()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.Competition, household: true);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeFalse();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeFalse();
      }

      // STAKE: what she accepted is a household, not a blank cheque. A liaison with someone outside it still breaks
      // the bond and is nursed, or one sentence would buy the player immunity for every affair.
      [Test]
      public void GIVEN_a_consort_who_accepted_WHEN_the_player_lies_with_someone_outside_the_household_THEN_it_wounds_and_is_nursed()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.AcceptedAgainstCulture, household: false);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeTrue();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeTrue();
      }

      // STAKE: a partner who never accepted keeps the betrayal she always had, even over another consort.
      [Test]
      public void GIVEN_a_partner_who_never_accepted_WHEN_the_player_lies_with_another_consort_THEN_it_is_still_a_betrayal()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.Betrayal, household: true);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeTrue();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeTrue();
      }

      // STAKE: a flirt makes her doubt, never breaks the bond or hardens into a grudge (player report: a wife turned
      // Estranged over mere small talk).
      [Test]
      public void GIVEN_a_mere_flirt_WHEN_she_hears_of_it_THEN_nothing_wounds_and_nothing_is_nursed()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.Betrayal, household: false, tier: RomanticActTier.Flirt);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeFalse();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeFalse();
      }

      // STAKE: the secret lover's hurt stays private: her bond is wounded, but she nurses no grudge.
      [Test]
      public void GIVEN_a_secret_lover_WHEN_the_player_lies_with_another_THEN_the_bond_is_wounded_but_no_grudge_is_nursed()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.Betrayal, household: false, stake: JealousStake.SecretLover);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeTrue();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeFalse();
      }

      // STAKE: the courted's own spouse has no bond with the player to wound, but an affront to her marriage bed is nursed.
      [Test]
      public void GIVEN_the_courted_spouse_WHEN_the_player_lies_with_her_husband_THEN_she_nurses_it_with_no_bond_to_wound()
      {
         JealousyWoundFacts f = Facts(JealousyTrack.Betrayal, household: false, stake: JealousStake.SpouseOfCourted);

         JealousyWoundPolicy.WoundsTheBond(f).Should().BeFalse();
         JealousyWoundPolicy.PlantsAGrudge(f).Should().BeTrue();
      }
   }
}
