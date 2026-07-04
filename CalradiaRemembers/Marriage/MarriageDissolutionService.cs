// Code written by Gabriel Mailhot, 04/07/2026.

#region

using System;
using System.Linq;
using System.Reflection;
using TaleWorlds.CampaignSystem;

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

            TryClearRomanticState(npc, spouse);

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

      #region private

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