// Code written by Gabriel Mailhot, 11/08/2026.
// ExCompanionDivorcePolicy is the divorce half of "marry your own companion". A wanderer companion promoted to
// Lord to wed the player has NO birth house: the engine stamped the player's OWN clan as their OriginClan at
// promotion. CR's divorce relocates a leaving spouse by their OriginClan, so without this rule the ex-companion
// would either linger forever as a Lord of the player's house or be scattered into a random clan. These tests
// pin exactly which divorcing spouse is returned to the free-wanderer pool: only the player's own promoted
// ex-companion (origin is the player's clan or none, no structural post), never a born-noble spouse and never an
// NPC-NPC divorce. If any clause flipped, a real lord could be dissolved into a wanderer, or an ex-companion left
// to scatter, the two failure modes the guard exists to prevent.

#region

using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using FluentAssertions;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests
{
   [TestFixture]
   public sealed class ExCompanionDivorcePolicyTests
   {
      // The case the guard exists for: the player divorces a promoted ex-companion who has no birth house (origin
      // is the player's own clan) and holds no post. They must be freed back to the wanderer pool, alive and
      // clanless, not left as a permanent Lord of the player's house.
      [Test]
      public void GIVEN_a_promoted_ex_companion_spouse_with_no_house_and_no_post_WHEN_divorcing_THEN_returned_to_the_wanderer_pool()
      {
         ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = true, CandidateIsPlayer = false, CandidateOriginIsPlayerClanOrNone = true, CandidateHoldsStructuralRole = false}).Should().BeTrue();
      }

      // A born-noble spouse of the player keeps a REAL birth house (origin is not the player's clan), so they are
      // relocated the ordinary way. Returning them to the wanderer pool would strip a lord of their identity, the
      // exact over-reach this flag guards against.
      [Test]
      public void GIVEN_a_born_noble_spouse_with_a_real_birth_house_WHEN_divorcing_THEN_not_returned_to_the_pool()
      {
         ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = true, CandidateIsPlayer = false, CandidateOriginIsPlayerClanOrNone = false, CandidateHoldsStructuralRole = false}).Should().BeFalse();
      }

      // The player is never their own spouse, and never the one relocated. Guarding the player side keeps the rule
      // from ever acting on the player themselves even if a caller passed them as the candidate.
      [Test]
      public void GIVEN_the_candidate_is_the_player_WHEN_divorcing_THEN_not_returned_to_the_pool()
      {
         ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = true, CandidateIsPlayer = true, CandidateOriginIsPlayerClanOrNone = true, CandidateHoldsStructuralRole = false}).Should().BeFalse();
      }

      // An NPC-NPC divorce has no player companion to free; the ordinary relocation owns that case. Without this
      // guard a same-culture wanderer married to some NPC could be wrongly yanked out of the world.
      [Test]
      public void GIVEN_no_player_in_the_marriage_WHEN_divorcing_THEN_not_returned_to_the_pool()
      {
         ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = false, CandidateIsPlayer = false, CandidateOriginIsPlayerClanOrNone = true, CandidateHoldsStructuralRole = false}).Should().BeFalse();
      }

      // An ex-companion who now leads a party, governs a fief, or heads a clan is pinned by that post: yanking them
      // into a clanless wanderer would orphan the role. They are left in place as a Lord of the player's house
      // instead, still a sane un-scattered state, so the guard must refuse the pool return here.
      [Test]
      public void GIVEN_an_ex_companion_holding_a_structural_post_WHEN_divorcing_THEN_left_in_place_not_pooled()
      {
         ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = true, CandidateIsPlayer = false, CandidateOriginIsPlayerClanOrNone = true, CandidateHoldsStructuralRole = true}).Should().BeFalse();
      }
   }
}
