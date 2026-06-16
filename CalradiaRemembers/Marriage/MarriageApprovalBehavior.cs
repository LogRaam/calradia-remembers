// Code written by Gabriel Mailhot, 13/06/2026.
// Marriage system Phase A.1: persist the family's blessing and resolve the
// relational consequence when the player marries — a blessed clan warms to the
// player, a slighted clan turns cold across every member but the new spouse.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.Core;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Marriage
{
    /// <summary>
    ///   The engine of the marriage system (Phase A): it remembers which matches a family
    ///   has blessed and, the moment the player marries, swings the spouse's whole family
    ///   for or against the player depending on whether that blessing was earned.
    ///
    ///   Consequence is PURE RELATIONAL by design. Bannerlord's native hero relation is
    ///   collapsed to the clan for diplomacy, so changing each member's relation is felt
    ///   as the whole family warming or souring — no kingdom-level alliance is touched.
    ///   The new spouse is never penalized: they have left their birth clan for the
    ///   player's, and a marriage is no betrayal in their own eyes.
    /// </summary>
    public sealed class MarriageApprovalBehavior : CampaignBehaviorBase
    {
        public static MarriageApprovalBehavior Instance;

        private const string SaveKey = "CalradiaRemembers_MarriageApprovals_v1";

        // Base relation swing applied to every member of the spouse's family clan (the
        // spouse excepted) the moment the player marries. The clan LEADER's verdict (whether
        // a blessing was earned) sets the SIGN; each member then leans by their own standing
        // with the player, so a dissenting kinsman can resist or even invert the family tide.
        private const int ApprovedRelationBonus    = 40;
        private const int UnapprovedRelationPenalty = -40;

        // Per-member lean: a member's existing relation with the player tilts their reaction
        // (relation / 2 added to the base). A beloved kinsman rejoices harder; one who already
        // resents the player stays cold despite a blessing — or warms despite a slight.
        private const int MaxFamilySwing = 80; // clamp on the final per-member delta

        // Keyed by the intended spouse's hero id. Presence == the family blessed this match.
        private Dictionary<string, MarriageApprovalRecord> _approvals
            = new Dictionary<string, MarriageApprovalRecord>();

        public MarriageApprovalBehavior() => Instance = this;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Records a family's blessing for the player to marry <paramref name="spouse" />.</summary>
        public void Grant(Hero spouse, Hero approver, int day)
        {
            if (spouse == null) return;
            _approvals[spouse.StringId] = new MarriageApprovalRecord {
                SpouseHeroId     = spouse.StringId,
                ApprovedByHeroId = approver?.StringId,
                ClanId           = spouse.Clan?.StringId,
                DayGranted       = day
            };
            ModLog.Info($"[MARRIAGE] blessing granted: player may marry {spouse.Name} " +
                        $"(by {approver?.Name?.ToString() ?? "?"}).");
        }

        /// <summary>True when the spouse's family has blessed a marriage to the player.</summary>
        public bool IsApproved(string spouseHeroId)
            => spouseHeroId != null && _approvals.ContainsKey(spouseHeroId);

        public MarriageApprovalRecord GetRecord(string spouseHeroId)
            => spouseHeroId != null && _approvals.TryGetValue(spouseHeroId, out MarriageApprovalRecord r)
                ? r
                : null;

        /// <summary>Removes a blessing (test / cleanup). Returns true when one was present.</summary>
        public bool Revoke(string spouseHeroId)
            => spouseHeroId != null && _approvals.Remove(spouseHeroId);

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public override void RegisterEvents()
        {
            // BeforeHeroesMarried fires while the spouse is still in their birth clan — the
            // right moment to read the family the consequence should fall on.
            CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);
        }

        public override void SyncData(IDataStore dataStore)
        {
            // Same save-safety contract as the profile store: never brick a save/load over
            // our own blob — degrade to "no blessings this pass" instead.
            string json = null;
            if (dataStore.IsSaving)
            {
                try { json = JsonConvert.SerializeObject(_approvals, Formatting.None); }
                catch (Exception ex)
                {
                    ModLog.Error($"[MARRIAGE] approval serialize failed — persisting nothing this save. {ex.GetType().Name}: {ex.Message}");
                    json = "{}";
                }
            }

            dataStore.SyncData(SaveKey, ref json);

            if (!dataStore.IsSaving && !string.IsNullOrEmpty(json))
            {
                try
                {
                    _approvals = JsonConvert.DeserializeObject<Dictionary<string, MarriageApprovalRecord>>(json)
                                 ?? new Dictionary<string, MarriageApprovalRecord>();
                }
                catch (Exception ex)
                {
                    _approvals = new Dictionary<string, MarriageApprovalRecord>();
                    ModLog.Error($"[MARRIAGE] approval load failed — blessings dropped for this load so the campaign can still open. {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        #region private

        /// <summary>
        ///   The player is marrying. Warm or chill the spouse's birth family depending on
        ///   whether that family had blessed the match. Read here (before the spouse moves
        ///   into the player's clan) so the consequence lands on the right family. A blessing
        ///   is single-use.
        /// </summary>
        private void OnBeforeHeroesMarried(Hero firstHero, Hero secondHero, bool showNotification)
        {
            Hero spouse = OtherSpouseIfPlayerInvolved(firstHero, secondHero);
            if (spouse == null) return;

            Clan family = spouse.Clan;

            bool approved = IsApproved(spouse.StringId);
            _approvals.Remove(spouse.StringId); // consumed whether or not the family was external

            if (family == null || family == Clan.PlayerClan)
            {
                ModLog.Info($"[MARRIAGE] {spouse.Name} weds into the player's house; no external family to weigh in.");
                return;
            }

            int baseDelta = approved ? ApprovedRelationBonus : UnapprovedRelationPenalty;
            (int affected, int warmed, int cooled) = ApplyToFamily(family, spouse, baseDelta);
            string clan = family.Name?.ToString() ?? "their family";

            // Surface the dissent: a blessed union that still has sour kin, or a slighted
            // family with a few quiet well-wishers, is the heart of the love-vs-ambition beat.
            string aside = approved
                ? (cooled > 0 ? $" Yet {Kin(cooled)} still resent it." : "")
                : (warmed > 0 ? $" Though {Kin(warmed)} quietly wish you well." : "");

            string msg = approved
                ? $"With their blessing, {clan} embraces your union with {spouse.Name}.{aside}"
                : $"You wed {spouse.Name} without {clan}'s blessing — the family is affronted.{aside}";

            MBInformationManager.AddQuickInformation(new TextObject(msg));
            StoryLog.WriteNarration(msg);
            ModLog.Info($"[MARRIAGE] {(approved ? "APPROVED" : "UNAPPROVED")} marriage to {spouse.Name}: " +
                        $"{clan} — {affected} kin moved ({warmed} warmed, {cooled} cooled).");
        }

        /// <summary>
        ///   Applies the marriage swing to every living family member except the spouse and the
        ///   player. The base sign comes from the family's verdict; each member then leans by
        ///   their own standing with the player (relation / 2), clamped, so a dissenter can resist
        ///   or invert the tide. Returns how many kin moved, and how many warmed vs cooled.
        /// </summary>
        private static (int affected, int warmed, int cooled) ApplyToFamily(Clan family, Hero spouse, int baseDelta)
        {
            int affected = 0, warmed = 0, cooled = 0;
            foreach (Hero member in family.Heroes.ToList())
            {
                if (member == null || !member.IsAlive) continue;
                if (member == spouse || member == Hero.MainHero) continue;

                int lean  = (int) member.GetRelationWithPlayer() / 2;
                int delta = Clamp(baseDelta + lean, -MaxFamilySwing, MaxFamilySwing);
                if (delta == 0) continue;

                ChangeRelationAction.ApplyPlayerRelation(
                    member, delta, affectRelatives: false, showQuickNotification: false);
                affected++;
                if (delta > 0) warmed++; else cooled++;
            }
            return (affected, warmed, cooled);
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

        /// <summary>"1 kinsman" / "N kin" — small grammatical helper for the family-reaction notice.</summary>
        private static string Kin(int n) => n == 1 ? "1 kinsman" : $"{n} kin";

        private static Hero OtherSpouseIfPlayerInvolved(Hero a, Hero b)
        {
            if (a == Hero.MainHero) return b;
            if (b == Hero.MainHero) return a;
            return null;
        }

        #endregion
    }
}
