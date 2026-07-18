// Code written by Gabriel Mailhot, 18/07/2026.
// This policy answers one question for EVERY face-to-face interlocutor: how many men does this person
// see standing behind the player? The answer becomes EncounterContext.PlayerPartyTroopCount, which the
// SDK renders as a coarse band ("a warband of well over a hundred soldiers rides at their back") plus a
// directive forbidding the NPC to call the player alone or an easy mark. It exists because of a player
// report: an NPC treated a player escorted by two thousand Nords as though they had come alone. Get the
// count wrong and the prompt lies about a fact the NPC can SEE, which is the one class of error the
// whole grounding effort is meant to prevent. These tests pin which force counts, and when none does.

#region

using CalradiaRemembers.Logic;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class PlayerForcePolicyTests
   {
      // The bug that prompted the whole fix: leading an army, the visible force is the ARMY, not the
      // player's own party. Counting only the party is what let an NPC read a player at the head of a
      // host as though they rode with a handful of men.
      [Test]
      public void GIVEN_the_player_leads_an_army_WHEN_resolving_THEN_the_whole_army_is_what_is_seen()
      {
         PlayerForcePolicy.Resolve(false, 40, true, 2000).Should().Be(2000);
      }

      // The other side of that rule: a player merely ATTACHED to someone else's army speaks only for
      // their own party. The host's men stand behind the host, not behind the player, so claiming them
      // would overstate the player exactly as badly as the original bug understated them.
      [Test]
      public void GIVEN_the_player_only_rides_inside_anothers_army_WHEN_resolving_THEN_only_their_own_party_is_seen()
      {
         PlayerForcePolicy.Resolve(false, 40, false, 2000).Should().Be(40);
      }

      // A captive has no force at their back whatever their party still holds in the field: it does not
      // follow them into a cell. Reporting it would hand the captor a fact they can plainly see is false,
      // in the one scene where the power balance is the entire point.
      [Test]
      public void GIVEN_a_captive_player_WHEN_resolving_THEN_no_force_is_seen_however_large_the_party()
      {
         PlayerForcePolicy.Resolve(true, 300, false, 0).Should().Be(0);
      }

      // Captivity outranks command: a captured army leader is still in a cell, so the army he led is not
      // at his back. Pins the ordering of the two rules rather than leaving it to argument order.
      [Test]
      public void GIVEN_a_captive_player_who_led_an_army_WHEN_resolving_THEN_captivity_still_wins()
      {
         PlayerForcePolicy.Resolve(true, 40, true, 2000).Should().Be(0);
      }

      // The empty case must be a clean zero, because the SDK reads zero as "render no force section at
      // all". Anything else would print a band for a player travelling alone.
      [Test]
      public void GIVEN_a_player_travelling_alone_WHEN_resolving_THEN_nothing_is_seen()
      {
         PlayerForcePolicy.Resolve(false, 0, false, 0).Should().Be(0);
      }

      // Fail safe against a bad engine read: a negative roster count must never reach the prompt as a
      // negative force, which would render an absurd band rather than simply no section.
      [Test]
      public void GIVEN_a_nonsensical_negative_count_WHEN_resolving_THEN_it_is_floored_at_nothing()
      {
         PlayerForcePolicy.Resolve(false, -5, false, 0).Should().Be(0);
      }
   }
}
