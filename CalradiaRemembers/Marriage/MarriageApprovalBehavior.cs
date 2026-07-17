// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Grudges;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Services;
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
   ///   Consequence is PURE RELATIONAL by design. Bannerlord's native hero relation is
   ///   collapsed to the clan for diplomacy, so changing each member's relation is felt
   ///   as the whole family warming or souring — no kingdom-level alliance is touched.
   ///   The new spouse is never penalized: they have left their birth clan for the
   ///   player's, and a marriage is no betrayal in their own eyes.
   /// </summary>
   public sealed class MarriageApprovalBehavior : CampaignBehaviorBase
   {
      // Base relation swing applied to every member of the spouse's family clan (the
      // spouse excepted) the moment the player marries. The clan LEADER's verdict (whether
      // a blessing was earned) sets the SIGN; each member then leans by their own standing
      // with the player, so a dissenting kinsman can resist or even invert the family tide.
      private const int ApprovedRelationBonus = 40;
      private const string LegacySaveKey = "CalradiaRemembers_MarriageApprovals_v1";

      // Per-member lean: a member's existing relation with the player tilts their reaction
      // (relation / 2 added to the base). A beloved kinsman rejoices harder; one who already
      // resents the player stays cold despite a blessing — or warms despite a slight.
      private const int MaxFamilySwing = 80; // clamp on the final per-member delta

      // Current key: JSON state stored as a UTF-8 byte[] (JsonCampaignState), which has no size
      // limit. LegacySaveKey is the old STRING key, kept only to migrate a save written before
      // this behavior switched over (Bannerlord's native string serializer corrupts a save once
      // the blob grows past roughly 65,535 characters).
      private const string SaveKey = "CalradiaRemembers_MarriageApprovals_v2bytes";
      private const int UnapprovedRelationPenalty = -40;
      public static MarriageApprovalBehavior Instance;

      // Keyed by the intended spouse's hero id. Presence == the family blessed this match.
      private Dictionary<string, MarriageApprovalRecord> _approvals = new();

      public MarriageApprovalBehavior()
      {
         Instance = this;
      }

      public MarriageApprovalRecord GetRecord(string spouseHeroId)
         => spouseHeroId != null && _approvals.TryGetValue(spouseHeroId, out MarriageApprovalRecord r)
            ? r
            : null;

      // ── Public API ────────────────────────────────────────────────────────

      /// <summary>Records a family's blessing for the player to marry <paramref name="spouse" />.</summary>
      public void Grant(Hero spouse, Hero approver, int day)
      {
         if (spouse == null) return;
         _approvals[spouse.StringId] = new MarriageApprovalRecord {
            SpouseHeroId = spouse.StringId,
            ApprovedByHeroId = approver?.StringId,
            ClanId = spouse.Clan?.StringId,
            DayGranted = day
         };
         ModLog.Info($"[MARRIAGE] blessing granted: player may marry {spouse.Name} " +
                     $"(by {approver?.Name?.ToString() ?? "?"}).");
      }

      /// <summary>True when the spouse's family has blessed a marriage to the player.</summary>
      public bool IsApproved(string spouseHeroId)
         => spouseHeroId != null && _approvals.ContainsKey(spouseHeroId);

      // ── Lifecycle ─────────────────────────────────────────────────────────

      public override void RegisterEvents()
      {
         // BeforeHeroesMarried fires while the spouse is still in their birth clan — the
         // right moment to read the family the consequence should fall on.
         CampaignEvents.BeforeHeroesMarried.AddNonSerializedListener(this, OnBeforeHeroesMarried);

         // C3 save-heal: fix a player's spouse whose romantic status predates the marriage-sets-Committed fix.
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, OnSessionLaunched);
      }

      /// <summary>
      ///   Romance audit C3 (save migration): the player's own spouse must sit at
      ///   <see cref="RomanticStatus.Committed" />, but marriage never set it, so a save made before this fix
      ///   carries a spouse stuck at a pre-marriage status or corrupted to
      ///   <see cref="RomanticStatus.SecretLover" /> ("married to another, clandestine"). Heals that one profile
      ///   on load, idempotently: a spouse already Committed, or one deliberately Estranged/Broken, is left
      ///   untouched (<see cref="ProfileMutator.SpouseStatusNeedsHeal" />). Additive and self-limiting: no new
      ///   save key, touches only the current <see cref="Hero.MainHero" />'s spouse.
      /// </summary>
      private void OnSessionLaunched(CampaignGameStarter starter)
      {
         SafeRun.Step("MarriageApprovalBehavior.HealPlayerSpouseStatus", HealPlayerSpouseStatus);
         SafeRun.Step("MarriageApprovalBehavior.SweepStaleApprovals", SweepStaleApprovals);
      }

      /// <summary>
      ///   Romance audit M-B2: the approval registry had no cleanup, so a blessing outlived the match it was for.
      ///   A candidate who has since died or wed another leaves an orphan record forever, and a widow remarrying
      ///   the player years later could arrive carrying a dead relative's blessing. Purge on load any record
      ///   whose spouse can no longer be wed to the player (gone, dead, or already married), idempotently and
      ///   without a new save key. The clan-identity check at the wedding (M-B1) is the belt to this braces.
      /// </summary>
      private void SweepStaleApprovals()
      {
         if (_approvals.Count == 0) return;

         var removed = 0;
         foreach (string spouseId in _approvals.Keys.ToList())
         {
            Hero spouse = Hero.FindFirst(h => h != null && h.StringId == spouseId);

            if (spouse is {IsAlive: true, Spouse: null}) continue; // still a valid, marriageable candidate

            _approvals.Remove(spouseId);
            removed++;
         }

         if (removed > 0) ModLog.Info($"[MARRIAGE] swept {removed} stale marriage blessing(s) (M-B2).");
      }

      private static void HealPlayerSpouseStatus()
      {
         Hero spouse = Hero.MainHero?.Spouse;
         if (spouse == null) return;

         NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId);
         if (profile?.Romantic == null) return;

         if (!ProfileMutator.SpouseStatusNeedsHeal(profile.Romantic.Status)) return;

         RomanticStatus before = profile.Romantic.Status;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.IsSecretLover = false;
         NpcMemoryCampaignBehavior.Instance.Store.Set(profile);
         ModLog.Info($"[MARRIAGE] healed player-spouse {spouse.Name} romantic status {before} to Committed (C3 save migration).");
      }

      /// <summary>Removes a blessing (test / cleanup). Returns true when one was present.</summary>
      public bool Revoke(string spouseHeroId)
         => spouseHeroId != null && _approvals.Remove(spouseHeroId);

      public override void SyncData(IDataStore dataStore)
      {
         // Same save-safety contract as the profile store: never brick a save/load over our own
         // blob, degrade to "no blessings this pass" instead.
         if (dataStore.IsSaving)
            JsonCampaignState.Save(dataStore, SaveKey, _approvals);
         else
            _approvals = JsonCampaignState.Load(dataStore, SaveKey, LegacySaveKey, () => new Dictionary<string, MarriageApprovalRecord>());
      }

      #region private

      /// <summary>
      ///   Applies the marriage swing to every living family member except the spouse and the
      ///   player. The base sign comes from the family's verdict; each member then leans by
      ///   their own standing with the player (relation / 2), clamped, so a dissenter can resist
      ///   or invert the tide. Returns how many kin moved, how many warmed vs cooled, and the
      ///   actual disapproving members (those who cooled) so an unblessed match can plant a
      ///   Grudges-pillar grievance on exactly the same kin this swing already chilled, never on
      ///   one who warmed to it despite the family's own verdict.
      /// </summary>
      private static (int affected, int warmed, int cooled, List<Hero> disapproving) ApplyToFamily(Clan family, Hero spouse, int baseDelta)
      {
         int affected = 0,
             warmed = 0,
             cooled = 0;
         var disapproving = new List<Hero>();
         foreach (Hero member in family.Heroes.ToList())
         {
            if (member is not {IsAlive: true}) continue;
            if (member == spouse || member == Hero.MainHero) continue;

            int lean = (int) member.GetRelationWithPlayer() / 2;
            int delta = Clamp(baseDelta + lean, -MaxFamilySwing, MaxFamilySwing);

            if (delta == 0) continue;

            ChangeRelationAction.ApplyPlayerRelation(
               member, delta, false, false);
            affected++;
            if (delta > 0) warmed++;
            else
            {
               cooled++;
               disapproving.Add(member);
            }
         }

         return (affected, warmed, cooled, disapproving);
      }

      private static int Clamp(int v, int min, int max) => v < min ? min : v > max ? max : v;

      /// <summary>"1 kinsman" / "N kin" — small grammatical helper for the family-reaction notice.</summary>
      private static string Kin(int n) => n == 1
         ? "1 kinsman"
         : $"{n} kin";

      private static Hero OtherSpouseIfPlayerInvolved(Hero a, Hero b)
      {
         if (a == Hero.MainHero) return b;
         if (b == Hero.MainHero) return a;

         return null;
      }

      /// <summary>
      ///   Grudges pillar, Tier 2 live plant site: an UnblessedMarriage grievance on every family member
      ///   who actually disapproved of this match (<paramref name="disapproving" />, already resolved by
      ///   <see cref="ApplyToFamily" />), via the real <see cref="Tier2GrudgeRules.UnblessedMarriageGrudge" />
      ///   rule and <see cref="GrudgeBehavior.TryPlant" /> gate (anti-noise guardrails, the per-holder cap
      ///   can still legitimately refuse). Guarded so a failure here never breaks the relation swing or the
      ///   quick-info notice already applied above it.
      /// </summary>
      private static void PlantUnblessedMarriageGrudges(List<Hero> disapproving, Hero spouse, string clan)
      {
         if (disapproving == null || disapproving.Count == 0) return;
         if (GrudgeBehavior.Instance == null || Hero.MainHero == null) return;

         try
         {
            var facts = new UnblessedMarriageFacts {MarriageIsUnblessed = true, StillMarried = true};
            GrudgeProposal proposal = Tier2GrudgeRules.UnblessedMarriageGrudge(facts);

            if (!proposal.Forms) return;

            var today = (int) CampaignTime.Now.ToDays;
            string cause = $"The player wed {spouse.Name} into {clan} without the family's blessing.";

            foreach (Hero member in disapproving)
               GrudgeBehavior.Instance.TryPlant(member, Hero.MainHero, GrudgeSource.UnblessedMarriage,
                  cause, proposal.Magnitude, proposal.KnownToPlayer, today);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MARRIAGE] UnblessedMarriage grudge plant failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

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

         // M-B1: read the RECORD, not just a bare "is there a blessing" flag, so the +40 warm swing only lands on
         // the family that actually blessed. A spouse who changed clans since the blessing (or whose blessing
         // clan was destroyed) would otherwise turn an unrelated family warm on a blessing it never gave.
         MarriageApprovalRecord record = GetRecord(spouse.StringId);
         _approvals.Remove(spouse.StringId); // consumed whether or not the family was external

         if (family == null || family == Clan.PlayerClan)
         {
            ModLog.Info($"[MARRIAGE] {spouse.Name} weds into the player's house; no external family to weigh in.");

            return;
         }

         bool approved = MarriageGuardPolicy.BlessingHonoursFamily(record != null, record?.ClanId, family.StringId);
         int baseDelta = approved
            ? ApprovedRelationBonus
            : UnapprovedRelationPenalty;
         (int affected, int warmed, int cooled, List<Hero> disapproving) = ApplyToFamily(family, spouse, baseDelta);
         string clan = family.Name?.ToString() ?? "their family";

         // Grudges pillar, Tier 2 (UnblessedMarriage): ONLY the player's own marriages ever plant this
         // source (NPC-NPC weddings happen off-screen, so a hidden grudge there is noise the player never
         // perceives). Plants on exactly the kin this swing already chilled, never on one who happened to
         // warm despite the family's own verdict. Lasts while the marriage stands; DivorceProcessBehavior /
         // SpouseDivorceDemandBehavior clear it the moment this same marriage ends.
         if (!approved)
            PlantUnblessedMarriageGrudges(disapproving, spouse, clan);

         // Surface the dissent: a blessed union that still has sour kin, or a slighted
         // family with a few quiet well-wishers, is the heart of the love-vs-ambition beat.
         string aside = approved
            ? cooled > 0
               ? $" Yet {Kin(cooled)} still resent it."
               : ""
            : warmed > 0
               ? $" Though {Kin(warmed)} quietly wish you well."
               : "";

         string msg = approved
            ? $"With their blessing, {clan} embraces your union with {spouse.Name}.{aside}"
            : $"You wed {spouse.Name} without {clan}'s blessing — the family is affronted.{aside}";

         MBInformationManager.AddQuickInformation(new TextObject(msg));
         StoryLog.WriteNarration(msg);
         ModLog.Info($"[MARRIAGE] {(approved ? "APPROVED" : "UNAPPROVED")} marriage to {spouse.Name}: " +
                     $"{clan} — {affected} kin moved ({warmed} warmed, {cooled} cooled).");
      }

      #endregion
   }
}