// Code written by Gabriel Mailhot, 17/10/2026.
// The world's long-term memory. Gabriel's ruling, 17/10/2026: "Pour le World Events, l'expiration de 42 jours devrait
// etre pour les evenements recents. Mais apres 42 jours, ca devrait basculer vers une memoire long terme. La memoire
// long terme servirait entre autre a definir les relations entre NPC.. et pourrait parfois etre utilise pour des
// conversations entre joueur et NPC sur le passe." His answers the same day accepted the significance bar of
// docs/design/NPC_REGARD.md section 7.7a, and asked that captures, executions and discovered schemes between NPCs be
// recorded so they reach this tier. Third-party news still ages out of the recent tier after 42 days
// (WorldEventPruner.RumorWindowDays); what this policy calls memorable moves to the long-term tier instead of being
// deleted, is known to those tied to it rather than to those near it, and is bounded by a cap.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What kind of happening a world event is, as the long-term memory sorts it.</summary>
    public enum HistoryKind
    {
        /// <summary>Not on the bar: gossip, hardship, omens, looming wars, seasonal accounts. Fades with the news.</summary>
        None,

        /// <summary>A battle between lords' hosts (a siege assault included, see <see cref="HistoryCandidateFacts.IsSiege" />).</summary>
        Battle,

        /// <summary>A fortress or town changed hands by siege.</summary>
        FiefTaken,

        /// <summary>A lord put to death by another's judgement.</summary>
        Execution,

        /// <summary>Two nobles wed.</summary>
        Marriage,

        /// <summary>A realm declared war on another.</summary>
        War,

        /// <summary>Two realms made peace.</summary>
        Peace,

        /// <summary>A lord's scheme against another came to light (discovered, or succeeded in the open).</summary>
        SchemeExposed,

        /// <summary>A lord taken captive (by a lord, or a ruler or house head by anyone).</summary>
        Capture,

        /// <summary>A house rose against its realm, or was received back after rising (Gabriel, 17/10/2026).</summary>
        Rebellion,

        /// <summary>Two nobles no longer wed (Gabriel, 17/10/2026).</summary>
        Divorce,

        /// <summary>A lord fell in battle (Gabriel, 17/10/2026).</summary>
        FellInBattle,

        /// <summary>An event of the campaign's authored history the game has no kind of its own for (a betrayal, a feud, a debt...).</summary>
        Authored
    }

    /// <summary>The facts of a third-party event leaving the news, read to decide whether the world keeps it.</summary>
    public sealed class HistoryCandidateFacts
    {
        /// <summary>The kind of event. <see cref="HistoryKind.None" /> means it is not on the bar and fades.</summary>
        public HistoryKind Kind { get; init; }

        /// <summary>How far word of it carried (0..100). Decides a battle of unknown size; wrong, an old save keeps every field battle or none.</summary>
        public int Magnitude { get; init; }

        /// <summary>Men on the field when the battle began, both sides; 0 when unknown (battles recorded before 17/10/2026).</summary>
        public int MenAtStart { get; init; }

        /// <summary>The battle was a siege. False, a small garrison's last stand would fade like a skirmish.</summary>
        public bool IsSiege { get; init; }
    }

    /// <summary>One observer's ties to one long-term event, filled by the engine; read to decide whether he knows it.</summary>
    public sealed class HistoryKnowledgeFacts
    {
        /// <summary>The observer is one of the event's actors. Wrong, and a man would forget his own defeat.</summary>
        public bool IsActor { get; init; }

        /// <summary>The observer is a parent, child, sibling or spouse of an actor.</summary>
        public bool IsKinOfAnActor { get; init; }

        /// <summary>The observer belongs to the same clan as an actor.</summary>
        public bool SharesAClanWithAnActor { get; init; }

        /// <summary>The observer lives at the event's place (his home) or his clan holds it.</summary>
        public bool IsOfThePlace { get; init; }

        /// <summary>The observer's realm is one of the event's realms.</summary>
        public bool IsOfARealmInvolved { get; init; }

        /// <summary>The event's magnitude (0..100); from <see cref="LongTermHistoryPolicy.RealmWideMagnitude" /> up, the realm knows it.</summary>
        public int Magnitude { get; init; }

        /// <summary>The player kept the event from this observer (a secret, or hidden from him): he does not know it, whatever his ties.</summary>
        public bool WithheldByThePlayer { get; init; }
    }

    /// <summary>A long-term entry as eviction sees it.</summary>
    public interface IHistoryRecord
    {
        /// <summary>In-game day the event occurred.</summary>
        int Day { get; }

        /// <summary>How far word of it carried (0..100).</summary>
        int Magnitude { get; }

        /// <summary>Written by the campaign's author (docs/design/AUTHORED_HISTORY.md): owned by the file, never evicted.</summary>
        bool IsAuthored { get; }
    }

    /// <summary>
    ///   Pure rules of the world's long-term memory (Gabriel, 17/10/2026; design 7.7a): which aged-out events are kept,
    ///   who knows a kept one, and what goes first when the tier is full.
    /// </summary>
    public static class LongTermHistoryPolicy
    {
        /// <summary>The most entries the long-term tier keeps (7.7a: "a few hundred kilobytes of JSON").</summary>
        public const int Cap = 3000;

        /// <summary>From this magnitude up, everyone of the realms involved knows a long-term event (7.7a: "the greatest").</summary>
        public const int RealmWideMagnitude = 45;

        /// <summary>
        ///   A battle whose size was never recorded is kept from this magnitude: a fight over a settlement or a great
        ///   battle (BattleRecordText.Magnitude gives 45 and up to those, 35 to an ordinary field battle).
        /// </summary>
        public const int UnknownSizeBattleFloor = 45;

        /// <summary>
        ///   The magnitude of a lesser lord's capture by a lord: written straight into the long-term tier, since it is
        ///   not news (Lordfadooboo, 03/10/2026: only a ruler's or a landed house head's capture is), and set below the
        ///   least news-worthy capture (35, CaptiveNotoriety.NewsMagnitude) so it is evicted first.
        /// </summary>
        public const int LesserCaptureMagnitude = 25;

        /// <summary>The world event status that marks a lord's capture between NPCs (the player's own uses CaptureNorm.Status).</summary>
        public const string CaptureStatus = "npc_captured";

        /// <summary>The world event status that marks a lord's death in battle.</summary>
        public const string FellInBattleStatus = "fell_in_battle";

        /// <summary>The world event status that marks a house received back into the realm it rose against.</summary>
        public const string RebelReturnedStatus = "rebel_returned";

        /// <summary>
        ///   The magnitude of a lord's death in battle: from <see cref="RealmWideMagnitude" /> up, so his whole realm
        ///   remembers its fallen lord, and under an execution (65), so a judgement's scandal outlasts a death in the
        ///   field when the memory is full.
        /// </summary>
        public const int FellInBattleMagnitude = 50;

        /// <summary>True when a third-party event leaving the news is history the world keeps (the 7.7a bar).</summary>
        public static bool IsMemorable(HistoryCandidateFacts facts)
        {
            if (facts == null) return false;

            switch (facts.Kind)
            {
                case HistoryKind.Battle:
                    if (facts.IsSiege) return true;

                    return facts.MenAtStart > 0
                        ? facts.MenAtStart >= BattleRecordText.SeveralHundredFrom
                        : facts.Magnitude >= UnknownSizeBattleFloor;
                case HistoryKind.None:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>True when the observer would know this long-term event: tied to it, or of its realm when it was great.</summary>
        public static bool WouldKnow(HistoryKnowledgeFacts facts)
        {
            if (facts == null || facts.WithheldByThePlayer) return false;
            if (facts.IsActor || facts.IsKinOfAnActor || facts.SharesAClanWithAnActor || facts.IsOfThePlace) return true;

            return facts.IsOfARealmInvolved && facts.Magnitude >= RealmWideMagnitude;
        }

        /// <summary>
        ///   Bounds what the game recorded to <see cref="Cap" /> in place: the lowest magnitude goes first, then the
        ///   oldest. Authored events do not count and are never evicted: the cap bounds the game, not the author.
        /// </summary>
        public static void Evict<T>(List<T> history) where T : IHistoryRecord
        {
            if (history == null) return;

            List<T> recorded = history.Where(e => !e.IsAuthored).ToList();
            if (recorded.Count <= Cap) return;

            foreach (T least in recorded.OrderBy(e => e.Magnitude).ThenBy(e => e.Day).Take(recorded.Count - Cap).ToList())
                history.Remove(least);
        }
    }
}
