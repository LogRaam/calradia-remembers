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
using CalradiaRemembers.Mapping;
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

      /// <summary>Additive save key for the per-marriage family-swing ledger (minor 3; old saves load with none).</summary>
      private const string AppliedSwingsSaveKey = "CalradiaRemembers_MarriageAppliedSwings_v1";

      // Minor 3: spouseId -> (family memberId -> the relation delta the marriage swing applied), so a divorce can
      // revert exactly what the wedding moved. Absent entry = nothing to revert (old save, or a swing already reverted).
      private Dictionary<string, Dictionary<string, int>> _appliedSwings = new();

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

         // Minor 5: widowhood ends the marriage just as a divorce does, so it must clear the same in-law grudges.
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
      }

      /// <summary>
      ///   Minor 5 (romance audit): the player's UnblessedMarriage in-law grudges are cleared when the marriage
      ///   ends by DIVORCE, but were left standing at WIDOWHOOD. A marriage ended by the spouse's death is over
      ///   just the same, so the same grudges resolve here too. Death nulls the Spouse link and moves the pair
      ///   into each other's ExSpouses, so the dead spouse is recognised there. Idempotent (a no-op when no such
      ///   grudge stands). The family-swing (minor 3) is deliberately NOT reverted at death: the family lost their
      ///   kin, they are grieving, not returned to indifference, so their marriage-born warmth or chill stands.
      /// </summary>
      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         Hero player = Hero.MainHero;
         if (victim == null || player == null) return;

         bool wasPlayerSpouse = victim == player.Spouse || (player.ExSpouses?.Contains(victim) ?? false);
         if (!wasPlayerSpouse) return;

         int today = (int) CampaignTime.Now.ToDays;
         SafeRun.Step("MarriageApprovalBehavior.WidowhoodGrudgePurge",
            () => GrudgeBehavior.Instance?.ResolveUnblessedMarriageGrudgesAgainst(player.StringId, today));
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

      private static void HealPlayerSpouseStatus() => HealSpouseStatus(Hero.MainHero?.Spouse);

      /// <summary>
      ///   Makes the player's spouse's CR profile read as a marriage: minted if she has none, her romantic status set to
      ///   Committed unless deliberately Estranged/Broken. Run on load (C3) and, since 02/10/2026, at the wedding itself.
      /// </summary>
      internal static void HealSpouseStatus(Hero spouse)
      {
         if (spouse == null) return;

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return;

         // On a save that predates CR, the player's spouse may have NO profile yet (never spoken to since the mod
         // was installed). Mint one now so the first conversation already reads as a marriage, not a first
         // meeting, and the romance logic sees Committed from the start instead of one session later. FromHero
         // seeds SpouseName + a Romantic profile, so pairing it with the heal below covers both the never-met and
         // the stuck-status cases. This is the DATA half of the in-progress-save recognition; the prompt half
         // (framing an established spouse/kin as no stranger even with zero events) lives in PromptBuilder.
         NpcProfile profile = store.Get(spouse.StringId);
         bool created = profile == null;
         if (created) profile = HeroProfileMapper.FromHero(spouse);
         if (profile?.Romantic == null) return;

         if (!created && !ProfileMutator.SpouseStatusNeedsHeal(profile.Romantic.Status)) return;

         RomanticStatus before = profile.Romantic.Status;
         profile.Romantic.Status = RomanticStatus.Committed;
         profile.Romantic.IsSecretLover = false;
         store.Set(profile);
         ModLog.Info(created
            ? $"[MARRIAGE] seeded a Committed profile for pre-existing player-spouse {spouse.Name} (in-progress-save recognition)."
            : $"[MARRIAGE] healed player-spouse {spouse.Name} romantic status {before} to Committed (C3 save migration).");
      }

      /// <summary>Removes a blessing (test / cleanup). Returns true when one was present.</summary>
      public bool Revoke(string spouseHeroId)
         => spouseHeroId != null && _approvals.Remove(spouseHeroId);

      public override void SyncData(IDataStore dataStore)
      {
         // Same save-safety contract as the profile store: never brick a save/load over our own
         // blob, degrade to "no blessings this pass" instead.
         if (dataStore.IsSaving)
         {
            JsonCampaignState.Save(dataStore, SaveKey, _approvals);
            JsonCampaignState.Save(dataStore, AppliedSwingsSaveKey, _appliedSwings);
         }
         else
         {
            _approvals = JsonCampaignState.Load(dataStore, SaveKey, LegacySaveKey, () => new Dictionary<string, MarriageApprovalRecord>());
            _appliedSwings = JsonCampaignState.Load(dataStore, AppliedSwingsSaveKey, AppliedSwingsSaveKey, () => new Dictionary<string, Dictionary<string, int>>());
         }
      }

      /// <summary>
      ///   Minor 3 (romance audit): reverts the family relation swing this spouse's marriage applied, so the
      ///   wedding's ripple does not persist forever after a divorce, and never stacks across remarriages into the
      ///   same house. Applies the exact negative of each recorded per-member delta (skipping any who have since
      ///   died), then clears the ledger. A no-op when nothing was recorded (an older save, or already reverted).
      /// </summary>
      public void RevertFamilySwing(string spouseId)
      {
         if (string.IsNullOrEmpty(spouseId) || !_appliedSwings.TryGetValue(spouseId, out Dictionary<string, int> deltas)) return;

         var reverted = 0;
         foreach (KeyValuePair<string, int> kv in deltas)
         {
            Hero member = Hero.FindFirst(h => h != null && h.StringId == kv.Key);
            if (member is not {IsAlive: true} || kv.Value == 0) continue;

            ChangeRelationAction.ApplyPlayerRelation(member, -kv.Value, false, false);
            reverted++;
         }

         _appliedSwings.Remove(spouseId);
         if (reverted > 0) ModLog.Info($"[MARRIAGE] reverted the family swing of the ended marriage to {spouseId} ({reverted} kin) (minor 3).");
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
      private static (int affected, int warmed, int cooled, List<Hero> disapproving, Dictionary<string, int> appliedDeltas) ApplyToFamily(Clan family, Hero spouse, int baseDelta)
      {
         int affected = 0,
             warmed = 0,
             cooled = 0;
         var disapproving = new List<Hero>();
         var appliedDeltas = new Dictionary<string, int>(); // minor 3: per-member ledger, so the swing can be reverted at divorce
         foreach (Hero member in family.Heroes.ToList())
         {
            if (member is not {IsAlive: true}) continue;
            if (member == spouse || member == Hero.MainHero) continue;

            int lean = (int) member.GetRelationWithPlayer() / 2;
            int delta = Clamp(baseDelta + lean, -MaxFamilySwing, MaxFamilySwing);

            if (delta == 0) continue;

            ChangeRelationAction.ApplyPlayerRelation(
               member, delta, false, false);
            appliedDeltas[member.StringId] = delta;
            affected++;
            if (delta > 0) warmed++;
            else
            {
               cooled++;
               disapproving.Add(member);
            }
         }

         return (affected, warmed, cooled, disapproving, appliedDeltas);
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

         RememberTheWedding(spouse);

         // brorespeta123 (Nexus, 01/10/2026): "just got married and the wife got amnesia.. forgot who i was". A marriage
         // sealed through the game's own proposal never ran the CR marry verb, so her romantic status kept its
         // pre-wedding value for the whole session; only the next load healed it. Heal it at the wedding itself.
         SafeRun.Step("MarriageApprovalBehavior.HealSpouseAtWedding", () => HealSpouseStatus(spouse));

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

         bool approved = MarriageGuardPolicy.FamilyApproves(_crIsMarrying, record != null, record?.ClanId, family.StringId);
         int baseDelta = approved
            ? ApprovedRelationBonus
            : UnapprovedRelationPenalty;
         (int affected, int warmed, int cooled, List<Hero> disapproving, Dictionary<string, int> appliedDeltas) = ApplyToFamily(family, spouse, baseDelta);
         string clan = family.Name?.ToString() ?? "their family";

         // Minor 3 (romance audit): record the exact per-member swing so a later divorce can REVERT it, so the
         // family's marriage reaction does not persist forever (and never stacks +80 across remarriages into the
         // same house). Keyed by the spouse, whose marriage the swing belongs to.
         if (appliedDeltas.Count > 0) _appliedSwings[spouse.StringId] = appliedDeltas;

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

      // True only while CR itself is sealing a marriage (MarryThroughCr). Anything else that marries the player is the
      // base game, whose marriages always carry the family's consent. Main thread only, like every engine action.
      private static bool _crIsMarrying;

      /// <summary>
      ///   Seals a marriage CR brokered (a conversation, a quest reward), so the family's reaction is judged by its
      ///   blessing. The base game's own marriages never pass through here and are approved by construction.
      /// </summary>
      internal static void MarryThroughCr(Hero player, Hero spouse)
      {
         _crIsMarrying = true;
         try { MarriageAction.Apply(player, spouse); }
         finally { _crIsMarrying = false; }
      }

      /// <summary>
      ///   Writes the wedding onto the new spouse's own memory. Nothing did before, so a spouse married through a
      ///   vanilla offer came to her first conversation with an empty history, which the prompt reads as "already
      ///   married and sharing a life" (zhenja1, 25/09/2026). Read here, BEFORE the marriage moves her into the
      ///   player's clan, so "another house" is still her birth clan. Through CR's own public memory door, which
      ///   creates her profile properly if she has none yet. Never throws: a missed memory must not stop a wedding.
      /// </summary>
      private static void RememberTheWedding(Hero spouse)
      {
         try
         {
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(spouse.StringId);
            string line = WeddingMemoryText.For(new WeddingFacts {
               PlayerName = Hero.MainHero?.Name?.ToString() ?? "",
               HaveSharedHistory = profile?.Events != null && profile.Events.Count > 0,
               HaveMet = spouse.HasMet,
               FromAnotherHouse = spouse.Clan != null && spouse.Clan != Clan.PlayerClan
            });

            MemoryResult result = CrNpcMemory.AddMemory(new CrMemoryEntry {
               NpcId = spouse.StringId, Summary = line, Kind = "Agreement", DaysAgo = 0, SourceMod = "CalradiaRemembers"
            });
            ModLog.Info($"[MARRIAGE] {spouse.Name} remembers the wedding ({result}): {line}");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[MARRIAGE] wedding memory failed for {spouse?.Name}: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}