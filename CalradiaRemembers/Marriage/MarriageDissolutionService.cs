// Code written by Gabriel Mailhot, 03/07/2026.
// DIVORCE, Phase 1 (NPC case): the actual dissolution act, from a completed in-game reflection spike on
// Bannerlord 1.3.15. Findings trusted verbatim:
//   - Hero.Spouse's SETTER IS PUBLIC and self-cleaning: `a.Spouse = null` nulls BOTH sides and appends
//     each to the other's ExSpouses. No reflection needed for the core act.
//   - Clan restore for the spouse who joined the other's clan mirrors TaleWorlds' own fixup in
//     Hero.AfterLoad(): prefer OriginClan when !IsEliminated, else the smallest same-culture
//     non-eliminated non-bandit clan, else any non-eliminated non-bandit clan. Never leave a hero
//     clanless; never relocate a hero who IsClanLeader.
//   - What MarriageAction's HandleClanChangeAfterMarriageForHero would otherwise have to clean up
//     (governorship, party/army leadership) is instead a Phase 1 REFUSAL: DivorceEligibilityResolver
//     already guards a leaving spouse who leads a party or governs, so by the time this runs, a plain
//     Hero.Clan reassignment is safe.
//   - Romance's persisted state is nulled via the INTERNAL Romance.SetRomanticState(a, b, RomanceLevelEnum.Ended),
//     invoked by reflection (the enum itself IS public) and wrapped in try/catch: if it fails on a future
//     game patch, this logs and continues — the marriage still ends, only the Romance book-keeping lags.
//   - Children's Father/Mother are independent of Spouse: dissolution leaves parentage intact, no action needed.

#region

using System;
using System.Linq;
using System.Reflection;
using CalradiaRemembers.Logic;
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
        private static MethodInfo _setRomanticStateMethod;
        private static bool _romanceReflectionFailed;

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
