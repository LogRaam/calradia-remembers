// Code written by Gabriel Mailhot, 15/11/2026.
// superdeeplearningaibot (Discord, 15/11/2026, "Civil War Siege Win registers as FiefLost for the Ruler", with Bellum
// Civile): "Me and Rhagaea successfully besieged and captured Phycaon ... Her prompt dump shows: YOUR DAY: ... What set
// your day this way: the loss of Phycaon." His log: "Queen Rhagaea: FiefLost" and "Strategos Turiados: FiefGained" at the
// same instant. The engine's own reading (1.5.7): a captured town waits with its captor while the realm votes, and the
// vote's award (SettlementClaimantDecision -> ChangeOwnerOfSettlementAction.ApplyByKingDecision) takes it from her, so
// "old owner" is the queen herself. A ruler placing a town in the hands of her own vassal has lost nothing: it stays in
// her realm, and the placing is hers to do. A lord whose fief the crown takes back has lost it, by the same detail.

namespace CalradiaRemembers.Logic
{
    /// <summary>One fief changing hands, as the one who held it sees it.</summary>
    public sealed class FiefHandover
    {
        /// <summary>The crown placed it (the realm's vote, the ruler's decision or a gift), rather than a siege, a rebellion or a barter.</summary>
        public bool ByTheCrownsHand { get; init; }

        /// <summary>The one who held it rules the realm the new holder serves: a ruler placing a town with her own vassal.</summary>
        public bool HolderRulesTheNewHoldersRealm { get; init; }
    }

    /// <summary>Whether a fief changing hands is a loss to the one who held it, the blow that can set a day.</summary>
    public static class FiefLossPolicy
    {
        /// <summary>
        ///   A loss, except when a ruler places a town with a vassal of her own realm by the crown's hand: the town stays hers
        ///   to rule. A lord stripped of his fief, a town taken by siege or rebellion, a fief bartered away: each a loss.
        /// </summary>
        public static bool IsLoss(FiefHandover handover) => handover == null || !(handover.ByTheCrownsHand && handover.HolderRulesTheNewHoldersRealm);
    }
}
