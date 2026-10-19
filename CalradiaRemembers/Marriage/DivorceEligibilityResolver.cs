// Code written by Gabriel Mailhot, 03/07/2026.
// DIVORCE, Phase 1 (NPC case): reads the live game state into the eligibility facts DivorcePolicy reasons
// over, delegating the pure gates to it. Shared by every call site so they can never drift apart: the
// EndMarriageVerb (teaching + execution re-validation), the cr.divorce_info console diagnostic, and
// MarriageDissolutionService (which "leaving spouse" would have to relocate clans).

#region

using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Marriage
{
    /// <summary>
    ///   Gathers the live facts behind end_marriage eligibility and evaluates them via
    ///   <see cref="DivorcePolicy" />. "The leaving spouse" is whichever of the NPC and their spouse
    ///   currently sits in a clan other than the one they were born/created into (i.e. joined the
    ///   other's clan at marriage) — null when neither did, in which case no relocation guard applies.
    /// </summary>
    public static class DivorceEligibilityResolver
    {
        /// <summary>True when the NPC could currently begin an estrangement process, with the first failing reason when not.</summary>
        public static bool IsEligible(Hero npc, out string reason)
            => DivorcePolicy.IsEligible(GatherFacts(npc), out reason);

        /// <summary>True when the NPC's CR romantic profile has them courting the player or kept as a consort:
        /// they would leave the marriage FOR the player, which satisfies the trust gate on its own.</summary>
        private static bool IsRomanticallyBoundToPlayer(Hero npc)
        {
            NpcProfile profile = NpcMemoryCampaignBehavior.Instance?.Store?.Get(npc.StringId);

            // M-R1: an ACTIVE romance (courting or deeper), not a raw ordinal test. Estranged/Broken sort after
            // Courting in the enum, so ">= Courting" wrongly counted a dead romance as a reason to leave a marriage.
            return profile?.Romantic != null
                   && (profile.Romantic.Status.IsCourtingOrDeeper() || profile.Romantic.IsConsort);
        }

        /// <summary>Builds the pure eligibility facts for <paramref name="npc" /> from live game state.</summary>
        public static DivorceEligibilityFacts GatherFacts(Hero npc)
        {
            if (npc is not {IsAlive: true} || npc == Hero.MainHero || Hero.MainHero == null)
                return new DivorceEligibilityFacts();

            Hero spouse = npc.Spouse;
            bool married = spouse is {IsAlive: true};
            Hero leaving = married
                ? ResolveLeavingSpouse(npc, spouse)
                : null;

            return new DivorceEligibilityFacts {
                NpcIsMarried = married,
                NpcRelationWithSpouse = married
                    ? npc.GetRelation(spouse)
                    : 0,
                NpcRelationWithPlayer = npc.GetRelation(Hero.MainHero),
                NpcIsRomanticallyBoundToPlayer = IsRomanticallyBoundToPlayer(npc),
                SpouseIsPlayer = married && spouse == Hero.MainHero,
                EitherPregnant = married && (npc.IsPregnant || spouse.IsPregnant),
                EitherPrisoner = married && (npc.IsPrisoner || spouse.IsPrisoner),
                LeavingSpouseIsClanLeader = leaving?.IsClanLeader ?? false,
                LeavingSpouseLeadsParty = LeadsOwnParty(leaving),
                LeavingSpouseGoverns = leaving?.GovernorOf != null,
                ProcessAlreadyActive = DivorceProcessBehavior.Instance?.HasActive(npc.StringId) == true
            };
        }

        /// <summary>
        ///   Whichever of <paramref name="a" /> / <paramref name="b" /> currently sits outside their
        ///   OriginClan (moved in at marriage), or null when neither did (nothing to relocate). Public:
        ///   also used by <see cref="MarriageDissolutionService" /> to decide who to relocate on success.
        /// </summary>
        public static Hero ResolveLeavingSpouse(Hero a, Hero b)
        {
            if (a == null || b == null) return null;

            // Precise path: whoever now sits outside the clan they came from moved in at marriage.
            if (HeroClanCompat.IsAvailable)
            {
                bool aMoved = DivorcePolicy.HasMovedClans(a.Clan?.StringId, HeroClanCompat.OriginClanOrNull(a)?.StringId);
                bool bMoved = DivorcePolicy.HasMovedClans(b.Clan?.StringId, HeroClanCompat.OriginClanOrNull(b)?.StringId);

                if (aMoved && !bMoved) return a;
                if (bMoved && !aMoved) return b;

                return null; // neither moved, or an ambiguous edge case — no relocation guard applies
            }

            // Fallback when the game build hides OriginClan (an older build, or a beta that renamed it):
            // the spouse who married IN is the one who is not the shared clan's leader (a leader keeps
            // their clan; the newcomer joined it). This keeps divorce FULLY functional and relocating the
            // right spouse, instead of silently losing relocation on any build that changes the property.
            if (a.IsClanLeader && !b.IsClanLeader) return b;
            if (b.IsClanLeader && !a.IsClanLeader) return a;

            return null;
        }

        private static bool LeadsOwnParty(Hero hero)
            => hero?.PartyBelongedTo != null && hero.PartyBelongedTo.LeaderHero == hero;
    }
}
