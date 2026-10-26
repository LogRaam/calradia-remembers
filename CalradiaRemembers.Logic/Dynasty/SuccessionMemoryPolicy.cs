// Code written by Gabriel Mailhot, 22/06/2026.
// Dynastic succession: when the player dies and an heir takes over, an NPC inherits the standing, debts,
// and grudges of their dealings with the predecessor — but NOT the predecessor's private history. Pure so
// the rule for what crosses the generation stays tested: a leak would have a lord recall the late ruler's
// love affairs to their child, which is exactly the immersion break this guards against.

#region

using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic.Dynasty
{
    /// <summary>The rule for which recorded events survive a succession (are inherited by the heir).</summary>
    public static class SuccessionMemoryPolicy
    {
        /// <summary>
        ///   True when an event is inherited by the heir: world, political, and relational dealings
        ///   (meetings, conflicts, collaborations, agreements, betrayals) become the heir's reputation,
        ///   debts, and grudges. False for the predecessor's PRIVATE history — flirtations, intimacy,
        ///   captivity, jealousy — which dies with them and is dropped.
        /// </summary>
        public static bool SurvivesSuccession(NotableEventType type) => type switch
        {
            NotableEventType.Flirt
                or NotableEventType.Intimacy
                or NotableEventType.Captivity
                or NotableEventType.Jealousy => false,
            _ => true
        };

        /// <summary>
        ///   Reframes one NPC's memory for the heir: the predecessor's private history is dropped, the rest kept,
        ///   and the profile marks whose standing it inherited and what that person was to the heir. Split out of
        ///   SuccessionBehavior.Apply (26/09/2026) so the cr.testall check can run it on a DETACHED profile: the old
        ///   test ran the real Apply, which reframed every profile in the player's save.
        /// </summary>
        /// <summary>
        ///   Forgets the bond this NPC had with the player, keeping who she is. For an abdication (Gabriel, 26/10/2026): the
        ///   old character lives on, wed to the ruler he left for, and the heir must not inherit his lover, consort or
        ///   courtship. Her orientation, preferences, kinks, sketches and her own lover are hers, not the player's, and stay.
        /// </summary>
        public static void ForgetTheBondWithThePlayer(RomanticProfile romantic)
        {
            if (romantic == null) return;

            romantic.Status = RomanticStatus.None;
            romantic.StatusSinceDay = null;
            romantic.LoversSinceDay = null;
            romantic.AttractionToPlayer = 0;
            romantic.IsConsort = false;
            romantic.IsSecretLover = false;
            romantic.SecretLoverSinceDay = null;
            romantic.IsOpenArrangement = false;
            romantic.AcceptsPlayersOtherPartners = false;
            romantic.LoverKnownToPlayer = false;
        }

        public static void Reframe(NpcProfile profile, string predecessorName, string kinship, bool predecessorRetired = false)
        {
            if (profile == null) return;

            profile.Events.RemoveAll(e => !SurvivesSuccession(e.type));
            profile.InheritedFromName = predecessorName;
            profile.InheritedKinship = kinship;
            profile.InheritedPredecessorRetired = predecessorRetired;
        }
    }
}
