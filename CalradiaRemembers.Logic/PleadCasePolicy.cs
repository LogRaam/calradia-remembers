// Code written by Gabriel Mailhot, 01/10/2026.
// Lordfadooboo (Nexus, 01/10/2026): "a lord asking you to argue on their behalf to their family leader or faction
// leader, or a lord in another kingdom". fkasad: "Pleading their case? That's a good idea. A very good one, in fact."
// The giver names the person to persuade; the plea is made in that person's own conversation, and it counts only
// when they are truly won over FOR the giver: the sway_opinion verb, stance "for", about the giver, which since the
// same day also needs the player to have named the giver in that conversation. The deed is the persuaded heart, not
// the player's attempt, so a plea that fails leaves the task open.

namespace CalradiaRemembers.Logic
{
    /// <summary>One sway that just landed, as the plea task reads it.</summary>
    public sealed class SwayFacts
    {
        /// <summary>The character who was persuaded (the one the player spoke to).</summary>
        public string PersuadedId { get; set; }

        /// <summary>The person their opinion moved about.</summary>
        public string AboutId { get; set; }

        /// <summary>The opinion moved toward that person, not against them.</summary>
        public bool Warmer { get; set; }
    }

    /// <summary>The rules of a plead_case task. Pure.</summary>
    public static class PleadCasePolicy
    {
        /// <summary>
        ///   Whether this person may be named as the one to plead before: the same living adult third person an
        ///   earn_regard task may name, and someone who holds a court (a lord or a notable), since only they are
        ///   ever taught to be swayed.
        /// </summary>
        public static bool MayBePleadedTo(EarnRegardTargetFacts who, bool holdsCourt)
            => holdsCourt && EarnRegardPolicy.MayBeNamed(who);

        /// <summary>Whether this sway answers the plea: the named person, won over, toward the giver.</summary>
        public static bool AnswersThePlea(SwayFacts sway, string pleadBeforeId, string giverId)
            => sway != null && sway.Warmer && !string.IsNullOrEmpty(pleadBeforeId) && !string.IsNullOrEmpty(giverId)
               && sway.PersuadedId == pleadBeforeId && sway.AboutId == giverId;
    }
}
