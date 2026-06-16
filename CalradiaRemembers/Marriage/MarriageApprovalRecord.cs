// Code written by Gabriel Mailhot, 13/06/2026.
// Marriage system Phase A: a persisted record that the player obtained a clan's
// blessing to marry one of its members.

namespace CalradiaRemembers.Marriage
{
    /// <summary>
    ///   A persisted blessing: the spouse's family consented to the player marrying them.
    ///   The presence of a record (keyed by the intended spouse's hero id) means the match
    ///   is sanctioned; its absence at marriage time means the player wed without approval,
    ///   which the consequence resolver punishes across the family.
    /// </summary>
    public sealed class MarriageApprovalRecord
    {
        /// <summary>Hero id of the intended spouse this blessing covers.</summary>
        public string SpouseHeroId { get; set; }

        /// <summary>Hero id of the family member (clan leader / guardian) who granted the blessing.</summary>
        public string ApprovedByHeroId { get; set; }

        /// <summary>
        ///   Hero id of the spouse's family clan at the time of approval. Kept so the
        ///   consequence resolver knows which family to reward even if the spouse has since
        ///   moved clans.
        /// </summary>
        public string ClanId { get; set; }

        /// <summary>In-game day the blessing was granted.</summary>
        public int DayGranted { get; set; }
    }
}
