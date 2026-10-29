// Code written by Gabriel Mailhot, 28/10/2026.
// ParleyEntryPolicy decides whether the council picker offers a parley with a realm. timicu (Nexus, 28/10/2026) asked for
// peace treaties and concessions negotiated in conversation; the parley that does it was reachable only from the console.
// If this is wrong, the button offers a parley whose peace the game then refuses (a vassal cannot make his realm's
// peace), or offers one with a realm that is not at war or has no ruler to meet.

#region

using CalradiaRemembers.Logic.Assemblies;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ParleyEntryPolicyTests
   {
      private static ParleyEntryFacts Enemy(bool leads = true, bool atWar = true, bool ruler = true, bool gone = false)
         => new() {PlayerLeadsFaction = leads, AtWarWithPlayer = atWar, HasRuler = ruler, Eliminated = gone};

      // STAKE: a ruler at war can seek the parley timicu asked for.
      [Test]
      public void GIVEN_a_ruler_at_war_with_a_realm_WHEN_the_picker_opens_THEN_a_parley_is_offered()
      {
         ParleyEntryPolicy.MayOffer(Enemy()).Should().BeTrue();
      }

      // STAKE: peace is the ruler's to make; a vassal offered a parley would see its peace refused.
      [Test]
      public void GIVEN_a_player_who_does_not_lead_their_faction_WHEN_the_picker_opens_THEN_no_parley()
      {
         ParleyEntryPolicy.MayOffer(Enemy(leads: false)).Should().BeFalse();
      }

      // STAKE: a parley is sought with an enemy that still stands and has someone to speak for it.
      [Test]
      public void GIVEN_a_realm_at_peace_without_a_ruler_or_fallen_WHEN_the_picker_opens_THEN_no_parley()
      {
         ParleyEntryPolicy.MayOffer(Enemy(atWar: false)).Should().BeFalse();
         ParleyEntryPolicy.MayOffer(Enemy(ruler: false)).Should().BeFalse();
         ParleyEntryPolicy.MayOffer(Enemy(gone: true)).Should().BeFalse();
      }
   }
}
