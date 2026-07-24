// Code written by Gabriel Mailhot, 21/07/2026.
// ASSEMBLY pillar, Council module: the two pure decisions the [RESOLUTION] channel needs beyond what
// CouncilActorPolicy and ResolutionLifecycle already cover. (1) Whether a NEW resolution may be recorded: a
// second live pledge of the SAME kind from the SAME member is ignored rather than stacked, generalising
// QuestService.IssueQuest's own "one task at a time" rule (never stack a new quest on an unfinished one) to
// the council's own ledger. (2) Which of a member's live resolutions a bare "type: withdraw" targets, since
// the ratified design carries no resolution id in the block: the owner explicitly rejected a manual delete
// button ("withdrawal happens only in the conversation"), so this is the only place a withdrawal is resolved,
// and it must never guess between two live pledges it cannot tell apart, mirroring CouncilActorPolicy's own
// "ambiguous means refuse, not guess" discipline.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.Assemblies
{
    /// <summary>Session-ledger decisions for the [RESOLUTION] channel, layered on top of <see cref="AssemblyLedger" /> and <see cref="ResolutionLifecycle" />.</summary>
    public static class CouncilResolutionPolicy
    {
        /// <summary>The one resolution kind this slice's lift executor understands (see the mod's council lift).</summary>
        public const string QuestKind = "quest";

        /// <summary>The kind that reverses an already-recorded resolution rather than recording a new one.</summary>
        public const string WithdrawKind = "withdraw";

        /// <summary>
        ///   The one action verb whose effect is PERCEPTION rather than a world transfer: how the NPC regards
        ///   the player. Ratified 2026-07-24: at a council, this alone executes on the spot, because a lord you
        ///   anger IS angrier at once, and that shift must colour how he weighs every request for the rest of
        ///   the sitting. World deeds (gold, troops, prisoners, marriage, schemes, missions) are NOT sealed at
        ///   the table: they are commitments recorded as [RESOLUTION] and carried out only when the council
        ///   rises. Held here, in the pure layer, so <see cref="ExecutesImmediatelyAtCouncil" /> is the single
        ///   testable authority the bridge and any future caller both defer to.
        /// </summary>
        public const string PerceptionActionType = "change_relation";

        /// <summary>
        ///   True when an action verb the model emitted during a COUNCIL turn should execute immediately rather
        ///   than being deferred to the lift. Only the perception verb (<see cref="PerceptionActionType" />)
        ///   qualifies: feelings are real and immediate, deeds are promises for later. Everything else returns
        ///   false, so the bridge drops it from immediate dispatch at a council and lets the [RESOLUTION]
        ///   channel be the only way a real deed is committed there. Case-insensitive; a null/blank type is not
        ///   immediate (nothing to run).
        /// </summary>
        public static bool ExecutesImmediatelyAtCouncil(string actionType)
            => !string.IsNullOrWhiteSpace(actionType)
               && string.Equals(actionType.Trim(), PerceptionActionType, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        ///   True when a new resolution of <paramref name="kind" /> may be recorded for a member already
        ///   carrying <paramref name="existingForActor" />. False only when one of their existing entries is
        ///   the SAME kind and still provisional (not yet withdrawn or committed): a second pledge on top of a
        ///   live one would let the table promise the same member two tasks at once, which the mod's own
        ///   quest system already refuses one level down (QuestService.IssueQuest). A different kind, or a
        ///   kind that is already terminal (withdrawn/applied/failed), never blocks a new one.
        /// </summary>
        public static bool CanRecord(string kind, IEnumerable<AssemblyResolutionEntry> existingForActor)
        {
            if (existingForActor == null) return true;

            return !existingForActor.Any(e =>
                string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase) && ResolutionLifecycle.IsProvisional(e.Status));
        }

        /// <summary>
        ///   Which of <paramref name="entriesForActor" /> a bare "type: withdraw" pulls back. Only entries that
        ///   can still be reversed are candidates (a committed or already-withdrawn one is not "live" to pull).
        ///   Exactly one live entry is the common case and always wins outright. Several live entries (a
        ///   member with more than one pledge on the table) are disambiguated by <paramref name="detail" />,
        ///   a bidirectional substring match against each entry's own recorded detail; anything else
        ///   (no live entry, or several live entries and no match, or more than one matching) returns null:
        ///   a wrong guess here would withdraw the WRONG pledge, silently leaving the one the player actually
        ///   meant to keep.
        /// </summary>
        public static AssemblyResolutionEntry SelectWithdrawTarget(IReadOnlyList<AssemblyResolutionEntry> entriesForActor, string detail)
        {
            if (entriesForActor == null) return null;

            List<AssemblyResolutionEntry> live = entriesForActor.Where(e => ResolutionLifecycle.CanReverse(e.Status)).ToList();

            if (live.Count == 0) return null;
            if (live.Count == 1) return live[0];

            if (string.IsNullOrWhiteSpace(detail)) return null; // several live pledges and nothing to tell them apart by

            List<AssemblyResolutionEntry> matches = live.Where(e =>
                !string.IsNullOrWhiteSpace(e.Detail) &&
                (e.Detail.IndexOf(detail, StringComparison.OrdinalIgnoreCase) >= 0
                 || detail.IndexOf(e.Detail, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();

            return matches.Count == 1 ? matches[0] : null;
        }
    }
}
