// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Linq;
using System.Reflection;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Marriage
{
   /// <summary>
   ///   Orchestrates the real dissolution of a marriage between two live heroes: re-validates every
   ///   guard against LIVE state (nothing here trusts a caller's earlier eligibility check — "the bridge
   ///   is law"), relocates the leaving spouse's clan when one applies, nulls the engine's own
   ///   Hero.Spouse link, and clears the internal Romance book-keeping best-effort. Touches engine
   ///   internals directly, hence the heavy documentation.
   /// </summary>
   public static class MarriageDissolutionService
   {
      private static bool _romanceReflectionFailed;
      private static MethodInfo _setRomanticStateMethod;

      /// <summary>
      ///   Divorce, Phase 2b: dissolves the PLAYER's own marriage to <paramref name="spouse" /> once the
      ///   spouse's demand is accepted. Thin wrapper over <see cref="TryDissolve" />: the player is always
      ///   passed as the first hero, so every guard (pregnancy, captivity, still-married-to-each-other) and
      ///   the relocation logic are the EXACT same path the NPC-NPC case uses, never a parallel
      ///   reimplementation. "The player never relocates" falls out of the existing invariant rather than
      ///   needing new code: <see cref="DivorceEligibilityResolver.ResolveLeavingSpouse" /> only ever
      ///   relocates a hero who moved OUT of their own OriginClan, and the player (always
      ///   <see cref="Clan.PlayerClan" />'s leader) never does — it is the spouse who joined the player's
      ///   clan at marriage who "moved", so they are always the one <see cref="ResolveRestorationClan" />
      ///   would send home. The one genuine wrinkle: a spouse who was ALREADY a clan leader in their own
      ///   right before marrying the player is refused here exactly as <see cref="TryDissolve" /> already
      ///   refuses any clan-leader/party-leader/governor leaving spouse — flagged for review, since that
      ///   marriage's own clan-merge behaviour was never audited by this feature.
      /// </summary>
      public static bool TryDissolvePlayerMarriage(Hero spouse, out string reason)
         => TryDissolve(Hero.MainHero, spouse, out reason);

      /// <summary>
      ///   Attempts to dissolve the marriage between <paramref name="npc" /> and <paramref name="spouse" />.
      ///   Returns true on success; on failure, <paramref name="reason" /> names why (never throws into
      ///   the caller). Every guard is re-checked here, live, regardless of what the caller already knew.
      /// </summary>
      public static bool TryDissolve(Hero npc, Hero spouse, out string reason)
      {
         try
         {
            if (npc == null || spouse == null)
            {
               reason = "missing hero";

               return false;
            }

            if (!npc.IsAlive || !spouse.IsAlive)
            {
               reason = "one of the pair is no longer alive";

               return false;
            }

            if (npc.Spouse != spouse || spouse.Spouse != npc)
            {
               reason = "no longer married to each other";

               return false;
            }

            if (npc.IsPregnant || spouse.IsPregnant)
            {
               reason = "a pregnancy is in play";

               return false;
            }

            if (npc.IsPrisoner || spouse.IsPrisoner)
            {
               reason = "one of the pair is held captive";

               return false;
            }

            Hero leaving = DivorceEligibilityResolver.ResolveLeavingSpouse(npc, spouse);

            if (leaving != null)
            {
               if (leaving.IsClanLeader)
               {
                  reason = $"{leaving.Name} heads their own clan and cannot be relocated";

                  return false;
               }

               if (leaving.PartyBelongedTo != null && leaving.PartyBelongedTo.LeaderHero == leaving)
               {
                  reason = $"{leaving.Name} leads a party";

                  return false;
               }

               if (leaving.GovernorOf != null)
               {
                  reason = $"{leaving.Name} governs a settlement";

                  return false;
               }

               Clan destination = ResolveRestorationClan(leaving);

               if (destination == null)
               {
                  reason = "no clan could be found to receive the leaving spouse";

                  return false;
               }

               leaving.Clan = destination;
            }

            npc.Spouse = null; // self-cleaning: nulls both sides, appends each to the other's ExSpouses
            ForeignDivorceBehavior.NoteDissolvedByCr(npc, spouse); // our own consequences follow; not news twice

            // "Marry your own companion", divorce half: a wanderer companion promoted to Lord to wed the player
            // carries the player's own clan as their OriginClan (stamped by the engine's Clan setter at promotion),
            // so ResolveLeavingSpouse saw no clan move and the relocation above never ran. Left as-is they would
            // linger as a permanent Lord of the player's house; return them instead to the free-wanderer pool they
            // were recruited from, alive and clanless, unless a structural post now pins them.
            TryReturnExCompanionToWandererPool(npc, spouse);

            TryClearRomanticState(npc, spouse);
            ClearPlayerConsortFlag(npc, spouse);

            // Minor 3 (romance audit): revert the family relation swing the wedding applied. Only the party who
            // married INTO the other's house had their birth family swung (never the player's own clan), so revert
            // the non-player spouse's ledger when the player's marriage ends.
            Hero exSpouse = npc == Hero.MainHero
               ? spouse
               : spouse == Hero.MainHero
                  ? npc
                  : null;
            if (exSpouse != null) MarriageApprovalBehavior.Instance?.RevertFamilySwing(exSpouse.StringId);

            reason = null;

            return true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DIVORCE] dissolution failed: {ex.GetType().Name}: {ex.Message}");
            reason = "an error prevented the dissolution";

            return false;
         }
      }

      /// <summary>
      ///   Why a <see cref="TryDissolve" /> refusal happened, read LIVE from the same facts the guards test
      ///   (romance audit M-D2/M-D6). The host uses this to react honestly: a
      ///   <see cref="DivorceBlock.Transient" /> pregnancy/captivity must be deferred (no penalty, no false
      ///   "resisted" record), while a <see cref="DivorceBlock.Structural" /> role must be released before a
      ///   retry can ever succeed. Deliberately mirrors the guard order inside <see cref="TryDissolve" /> so the
      ///   two never drift apart. Returns <see cref="DivorceBlock.None" /> for a clean pair (any refusal was
      ///   genuine spousal resistance, not one of these mechanical blocks).
      /// </summary>
      public static DivorceBlock ClassifyBlock(Hero npc, Hero spouse)
      {
         if (npc == null || spouse == null) return DivorceBlock.None;

         Hero leaving = DivorceEligibilityResolver.ResolveLeavingSpouse(npc, spouse);

         return DivorceBlockPolicy.Classify(new DivorceBlockPolicy.DivorceBlockFacts {EitherPregnant = npc.IsPregnant || spouse.IsPregnant, EitherCaptive = npc.IsPrisoner || spouse.IsPrisoner, LeavingIsClanLeader = leaving != null && leaving.IsClanLeader, LeavingLeadsParty = leaving?.PartyBelongedTo != null && leaving.PartyBelongedTo.LeaderHero == leaving, LeavingIsGovernor = leaving?.GovernorOf != null});
      }

      /// <summary>
      ///   Best-effort release of the STRUCTURAL posts that keep a leaving spouse from ever departing (romance
      ///   audit M-D2): a governorship is vacated at once (<see cref="ChangeGovernorAction.RemoveGovernorOf" />),
      ///   and a party they lead is set to disband (<see cref="DisbandPartyAction.StartDisband" /> is idempotent,
      ///   so the daily walk-out retry may call this again harmlessly until the party is gone and the retry
      ///   succeeds). This is what closes the "hand your spouse a party to freeze the divorce forever" exploit.
      ///   The MAIN party is never touched. A spouse who heads their OWN clan cannot be relocated by any of this
      ///   and is left to the caller's retry, by design: that is a rare setup, not the exploit. Fully guarded so a
      ///   partial engine failure never throws into the walk-out.
      /// </summary>
      public static void ReleaseStructuralRoles(Hero leaving)
      {
         if (leaving == null) return;

         try
         {
            if (leaving.GovernorOf != null)
            {
               ChangeGovernorAction.RemoveGovernorOf(leaving);
               ModLog.Info($"[DIVORCE] released {leaving.Name} from their governorship so the estrangement can complete.");
            }
         }
         catch (Exception ex) { ModLog.Error($"[DIVORCE] governor release failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }

         try
         {
            MobileParty party = leaving.PartyBelongedTo;

            if (party != null && party.LeaderHero == leaving && party != MobileParty.MainParty && !party.IsDisbanding)
            {
               DisbandPartyAction.StartDisband(party);
               ModLog.Info($"[DIVORCE] {leaving.Name} leads a party; began disbanding it so the estrangement can complete.");
            }
         }
         catch (Exception ex) { ModLog.Error($"[DIVORCE] party disband failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>
      ///   Romance audit M-R6: a divorce ends the bond, so clear a stale <c>IsConsort</c> flag on the player's
      ///   ex-partner. Nothing anywhere else ever reset it (verified by grep), so an ex "consort" stayed one for
      ///   life, skewing the jealousy senior-retire recourse and divorce eligibility. Only fires when the player
      ///   is one of the pair (an NPC-NPC dissolution has no player consort to clear). Best-effort and guarded so
      ///   a store hiccup never breaks a dissolution that has already legally completed.
      /// </summary>
      private static void ClearPlayerConsortFlag(Hero a, Hero b)
      {
         Hero player = Hero.MainHero;
         if (player == null) return;

         Hero other = a == player
            ? b
            : b == player
               ? a
               : null;

         if (other == null) return; // NPC-NPC divorce: no player consort involved

         try
         {
            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            NpcProfile profile = store?.Get(other.StringId);

            if (profile?.Romantic == null || !profile.Romantic.IsConsort) return;

            profile.Romantic.IsConsort = false;
            store.Set(profile);
            ModLog.Info($"[DIVORCE] cleared stale consort flag on {other.Name} at divorce (M-R6).");
         }
         catch (Exception ex) { ModLog.Error($"[DIVORCE] consort-flag clear failed (non-fatal): {ex.GetType().Name}: {ex.Message}"); }
      }

      #region private

      /// <summary>
      ///   "Marry your own companion", divorce half: when one of the divorcing pair is the player and the other
      ///   is a promoted ex-companion (derived, not persisted: their OriginClan is the player's own clan or none
      ///   at all, the signature a born noble never has), return that ex-companion to the free-wanderer pool they
      ///   were recruited from (clanless, no longer a companion, <see cref="Occupation.Wanderer" />) instead of
      ///   leaving them a permanent Lord of the player's house. Skipped when the game build hides
      ///   <c>Hero.OriginClan</c> (the derivation cannot be trusted then) or when the ex-companion holds a
      ///   structural post (a clan they lead, a party they command, a fief they govern): those are left in place,
      ///   still a sane un-scattered state. Best-effort and fully guarded: the marriage has already legally ended
      ///   by this point, this is only the ex-companion's landing state.
      /// </summary>
      private static void TryReturnExCompanionToWandererPool(Hero a, Hero b)
      {
         try
         {
            Hero player = Hero.MainHero;

            if (player == null) return;

            Hero other = a == player
               ? b
               : b == player
                  ? a
                  : null;

            if (other == null) return; // NPC-NPC divorce: no player companion involved

            // Derive ex-companion-ness from the origin house, and only when the build exposes it: a promoted
            // companion's origin is the player's own clan (stamped at promotion) or none; a born noble's origin
            // is their real birth house, so they are relocated the ordinary way instead.
            Clan origin = HeroClanCompat.OriginClanOrNull(other);
            bool originIsPlayerClanOrNone = HeroClanCompat.IsAvailable && (origin == null || origin == Clan.PlayerClan);

            bool holdsStructuralRole = other.IsClanLeader
               || (other.PartyBelongedTo != null && other.PartyBelongedTo.LeaderHero == other)
               || other.GovernorOf != null;

            if (!ExCompanionDivorcePolicy.ReturnsToWandererPool(new ExCompanionDivorcePolicy.DivorcedSpouseFacts {PlayerInMarriage = true, CandidateIsPlayer = false, CandidateOriginIsPlayerClanOrNone = originIsPlayerClanOrNone, CandidateHoldsStructuralRole = holdsStructuralRole}))
               return;

            other.CompanionOf = null; // defensive: already null since promotion nulled it
            other.Clan = null;        // clanless free wanderer; _clan was the player's clan, origin is left as-is (harmless)
            other.SetNewOccupation(Occupation.Wanderer);

            ModLog.Info($"[DIVORCE] returned ex-companion {other.Name} to the free-wanderer pool (no birth house to relocate to).");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DIVORCE] ex-companion wanderer-return failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Mirrors Hero.AfterLoad()'s own clan-restore ladder: the leaving spouse's OriginClan when
      ///   still standing, else the smallest non-eliminated non-bandit clan sharing their culture, else
      ///   the smallest non-eliminated non-bandit clan of any culture. Never returns an eliminated or
      ///   bandit clan; null only when literally none exist (should not happen in a real campaign).
      /// </summary>
      private static Clan ResolveRestorationClan(Hero leaving)
      {
         Clan origin = HeroClanCompat.OriginClanOrNull(leaving);

         if (origin is {IsEliminated: false}) return origin;

         Clan sameCulture = Clan.NonBanditFactions
                                .Where(c => c is {IsEliminated: false} && c.Culture == leaving.Culture)
                                .OrderBy(c => c.Heroes?.Count ?? 0)
                                .FirstOrDefault();

         if (sameCulture != null) return sameCulture;

         return Clan.NonBanditFactions
                    .Where(c => c is {IsEliminated: false})
                    .OrderBy(c => c.Heroes?.Count ?? 0)
                    .FirstOrDefault();
      }

      /// <summary>
      ///   Best-effort: clears the internal Romance record between the pair via reflection
      ///   (Romance.SetRomanticState is internal; its RomanceLevelEnum parameter type is public). A
      ///   failure here (e.g. a future game patch renaming the method) is logged and swallowed — the
      ///   marriage itself has already ended by this point, this is bookkeeping only.
      /// </summary>
      private static void TryClearRomanticState(Hero a, Hero b)
      {
         if (_romanceReflectionFailed) return;

         try
         {
            // Romance.SetRomanticState is STATIC internal (not instance) in Bannerlord 1.3.15 — verified by
            // reflection on TaleWorlds.CampaignSystem.dll. Invoked with a null target as a static method.
            if (_setRomanticStateMethod == null)
               _setRomanticStateMethod = typeof(Romance).GetMethod("SetRomanticState",
                  BindingFlags.NonPublic | BindingFlags.Static);

            if (_setRomanticStateMethod == null)
            {
               _romanceReflectionFailed = true;
               ModLog.Error("[DIVORCE] could not resolve Romance.SetRomanticState by reflection — romantic state left as-is (non-fatal).");

               return;
            }

            _setRomanticStateMethod.Invoke(null, new object[] {a, b, Romance.RomanceLevelEnum.Ended});
         }
         catch (Exception ex)
         {
            ModLog.Error($"[DIVORCE] Romance.SetRomanticState reflection failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion
   }
}