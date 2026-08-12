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
      ///   True only when the divorcing spouse should be returned to the free-wanderer pool: the player is one
      ///   of the two spouses (<paramref name="playerInMarriage" />), the spouse under consideration is NOT the
      ///   player (<paramref name="candidateIsPlayer" />), that spouse has NO birth house to return to
      ///   (<paramref name="candidateOriginIsPlayerClanOrNone" />: their origin clan is the player's own, stamped
      ///   at promotion, or none at all, the signature of a hero who was never a born noble), and they hold no
      ///   structural post that pins them (<paramref name="candidateHoldsStructuralRole" />: a clan they lead, a
      ///   party they command, a fief they govern). When they DO hold such a post, the caller leaves them in place
      ///   as a Lord of the player's clan (still a sane, un-scattered state) rather than yanking a role out from
      ///   under the world. A born-noble spouse fails <paramref name="candidateOriginIsPlayerClanOrNone" /> and is
      ///   relocated the ordinary way; an NPC-NPC divorce fails <paramref name="playerInMarriage" /> and is untouched.
      /// </summary>
      public static bool ReturnsToWandererPool(bool playerInMarriage,
                                               bool candidateIsPlayer,
                                               bool candidateOriginIsPlayerClanOrNone,
                                               bool candidateHoldsStructuralRole)
      {
         return playerInMarriage
             && !candidateIsPlayer
             && candidateOriginIsPlayerClanOrNone
             && !candidateHoldsStructuralRole;
      }
   }
}
