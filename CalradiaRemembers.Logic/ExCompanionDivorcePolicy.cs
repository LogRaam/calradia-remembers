// Code written by Gabriel Mailhot, 11/08/2026.
// "Marry your own companion", divorce half. A wanderer companion promoted to Lord to wed the player has NO
// birth house to be sent home to on divorce: CR's dissolution relocates a leaving spouse by their OriginClan,
// and the engine's Clan setter stamped the PLAYER'S OWN clan as that companion's OriginClan during promotion
// (its backing _clan started null). Left to the ordinary path they would either linger forever as a Lord of
// the player's house, or (on a build where the origin reads as some other clan) be scattered into a random
// same-culture clan. This pure rule decides, from facts the host reads live, when a divorcing spouse should
// instead be returned to the free-wanderer pool they were recruited from: alive, clanless, a companion-candidate
// again. A born-noble spouse keeps a real birth house and is never caught by this.

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   Pure decision for the divorce half of the marry-your-own-companion feature: whether a divorcing spouse
   ///   is an ex-companion who must be returned to the free-wanderer pool rather than relocated to a birth clan.
   /// </summary>
   public static class ExCompanionDivorcePolicy
   {
      /// <summary>
      ///   What is known about a divorcing spouse and the marriage they are leaving. FOUR booleans in a row
      ///   of which two are requirements and two are disqualifiers, alternating, which is the arrangement a
      ///   swap survives most comfortably.
      ///   Named rather than positional (2026-09-10). Gabriel: the code should be an example of good
      ///   practice. A run of same-typed arguments hides a swap that compiles in silence, and a test
      ///   that names only what it is about reads as the rule it protects.
      /// </summary>
      public sealed class DivorcedSpouseFacts
      {
         /// <summary>The player is one of the two spouses. An NPC-NPC divorce is left untouched.</summary>
         public bool PlayerInMarriage { get; set; }

         /// <summary>The spouse under consideration IS the player, who is never sent to the wanderer pool.</summary>
         public bool CandidateIsPlayer { get; set; }

         /// <summary>
         ///   They have no birth house to return to: their origin clan is the player's own, stamped at
         ///   promotion, or none at all. That is the signature of a hero who was never a born noble.
         /// </summary>
         public bool CandidateOriginIsPlayerClanOrNone { get; set; }

         /// <summary>
         ///   They hold a post that pins them: a clan they lead, a party they command, a fief they govern.
         ///   The caller then leaves them in place as a Lord of the player's clan rather than yanking a role
         ///   out from under the world.
         /// </summary>
         public bool CandidateHoldsStructuralRole { get; set; }
      }

      /// <summary>
      ///   True only when the divorcing spouse should be returned to the free-wanderer pool: the player is one
      ///   of the two spouses (<c>PlayerInMarriage</c>), the spouse under consideration is NOT the
      ///   player (<c>CandidateIsPlayer</c>), that spouse has NO birth house to return to
      ///   (<c>CandidateOriginIsPlayerClanOrNone</c>: their origin clan is the player's own, stamped
      ///   at promotion, or none at all, the signature of a hero who was never a born noble), and they hold no
      ///   structural post that pins them (<c>CandidateHoldsStructuralRole</c>: a clan they lead, a
      ///   party they command, a fief they govern). When they DO hold such a post, the caller leaves them in place
      ///   as a Lord of the player's clan (still a sane, un-scattered state) rather than yanking a role out from
      ///   under the world. A born-noble spouse fails <c>CandidateOriginIsPlayerClanOrNone</c> and is
      ///   relocated the ordinary way; an NPC-NPC divorce fails <c>PlayerInMarriage</c> and is untouched.
      /// </summary>
      public static bool ReturnsToWandererPool(DivorcedSpouseFacts facts)
      {
         if (facts == null) return false;

         bool playerInMarriage = facts.PlayerInMarriage;
         bool candidateIsPlayer = facts.CandidateIsPlayer;
         bool candidateOriginIsPlayerClanOrNone = facts.CandidateOriginIsPlayerClanOrNone;
         bool candidateHoldsStructuralRole = facts.CandidateHoldsStructuralRole;

         return playerInMarriage
             && !candidateIsPlayer
             && candidateOriginIsPlayerClanOrNone
             && !candidateHoldsStructuralRole;
      }
   }
}
