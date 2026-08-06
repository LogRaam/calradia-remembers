// Code written by Gabriel Mailhot, 06/08/2026.
// v1.34.1 hotfix. swear_oath's "keep the peace" kind became usable in a 1:1 conversation (shipped v1.34.0,
// LIVE), and its breach check punished the WRONG party: it marked the oath broken whenever the promisor's
// faction and the named house merely ended up at war, WITHOUT ever reading WHO DECLARED it. A lord who swore
// to keep the peace and was then INVADED (the victim) was branded an oathbreaker for the aggression committed
// AGAINST him. This pure policy fixes it: a keep-peace oath breaks ONLY when the promisor's OWN faction was
// the one that marched. The "who declared" fact comes from CR's established WarDeclared reading (faction1 is
// the declarer/aggressor, faction2 the defender), the same convention QuestDeedTracker.OnWarDeclared and
// WarDeedMatcher already rely on, so no signal is invented. Kept engine-free so the rule stays tested; the
// host (CalradiaRemembers.Commitments.CommitmentBehavior) resolves the live factions and passes ids in.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Decides whether a freshly declared war breaches a promisor's "keep the peace" oath.</summary>
    public static class KeepPeaceBreachPolicy
    {
        /// <summary>
        ///   True only when the war BREACHES the oath: the promisor's OWN faction
        ///   (<paramref name="promisorFactionId" />) is the DECLARER (<paramref name="declarerFactionId" />)
        ///   and the very house the oath was sworn to keep peace with (<paramref name="oathFactionId" />) is
        ///   the DEFENDER (<paramref name="defenderFactionId" />). Every other case is NOT a breach: a war the
        ///   promisor was dragged into as the defender (they were invaded, the victim, never an oathbreaker), a
        ///   war against some other house, or an unrelated war elsewhere in the world. Mirrors
        ///   <see cref="WarDeedMatcher.IsSatisfied" /> in shape: the aggressor must be the promisor and the
        ///   defender must be the oath's named house. Fails closed on any blank/unresolved id so an empty
        ///   faction lookup can never read as a breach (a false "oathbreaker" is worse than a missed one).
        /// </summary>
        public static bool ShouldBreach(
            string promisorFactionId,
            string oathFactionId,
            string declarerFactionId,
            string defenderFactionId)
        {
            if (string.IsNullOrWhiteSpace(promisorFactionId) || string.IsNullOrWhiteSpace(oathFactionId) ||
                string.IsNullOrWhiteSpace(declarerFactionId) || string.IsNullOrWhiteSpace(defenderFactionId))
                return false;

            bool promisorIsDeclarer = string.Equals(promisorFactionId, declarerFactionId, StringComparison.OrdinalIgnoreCase);
            bool oathHouseIsDefender = string.Equals(oathFactionId, defenderFactionId, StringComparison.OrdinalIgnoreCase);

            return promisorIsDeclarer && oathHouseIsDefender;
        }
    }
}
