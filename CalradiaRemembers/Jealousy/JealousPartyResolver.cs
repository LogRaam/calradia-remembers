// Code written by Gabriel Mailhot, 19/06/2026.
// Sprint 24 (jealousy): infer, on demand, a single plausible jealous party for a romantic
// act between the player and an NPC — never an off-screen NPC<->NPC romance simulation.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Marriage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Jealousy
{
    /// <summary>How serious the romantic act is — drives grudge severity and letter likelihood.</summary>
    public enum RomanticActTier
    {
        Flirt,
        Intimacy,
        Marriage
    }

    /// <summary>Who the jealous party is, and why they have a stake — sets the reaction's flavour.</summary>
    public enum JealousStake
    {
        /// <summary>The spouse of the NPC the player courted — an affront to their marriage bed.</summary>
        SpouseOfCourted,

        /// <summary>The player's own spouse/primary partner — betrayal, or competition in a sharing culture.</summary>
        PlayerPartner,

        /// <summary>A close associate of the courted NPC who could plausibly have married them.</summary>
        RivalSuitor,

        /// <summary>An NPC who carried a strong attraction to the player and was passed over.</summary>
        SpurnedAdmirer
    }

    /// <summary>The chosen jealous party plus the context needed to flavour their reaction.</summary>
    public sealed class JealousResolution
    {
        public Hero Party;          // who grows jealous
        public JealousStake Stake;
        public Hero Courted;        // the NPC the player acted romantically toward
        public Hero Other;          // the rival focus for competition jealousy (else null)
    }

    /// <summary>
    ///   Picks at most ONE believable jealous party for a romantic act, in priority order:
    ///   the courted NPC's spouse, then the player's own partner, then a rival suitor, then a
    ///   spurned admirer. Every candidate must have a real, inferable stake — no random malus.
    /// </summary>
    public static class JealousPartyResolver
    {
        // A rival suitor must be at least a genuine associate of the courted NPC (NPC<->NPC
        // relation), since the game does not model NPC desire — we infer it from "close enough
        // to plausibly have wanted them, and able to marry them".
        private const int RivalRelationThreshold = 20;

        // A spurned admirer must have carried real attraction to the player (0..100 scale).
        private const int SpurnedAttractionThreshold = 55;

        /// <summary>
        ///   Resolves the jealous party for the player's romantic act toward <paramref name="courted" />,
        ///   or null when no plausible party exists. <paramref name="lockedRivalId" /> pins a rival
        ///   chosen on an earlier act so the grudge stays consistent across the arc.
        /// </summary>
        public static JealousResolution Resolve(
            Hero courted,
            System.Func<string, NpcProfile> getProfile,
            string lockedRivalId)
        {
            Hero player = Hero.MainHero;
            if (courted == null || player == null || courted == player) return null;

            // 1) The courted NPC's spouse — strongest, most telegraphed stake.
            Hero spouseOfCourted = courted.Spouse;
            if (spouseOfCourted != null && spouseOfCourted.IsAlive && spouseOfCourted != player)
                return new JealousResolution {
                    Party = spouseOfCourted, Stake = JealousStake.SpouseOfCourted, Courted = courted
                };

            // 2) The player's own partner (native spouse, else a committed consort).
            Hero partner = ResolvePlayerPartner(courted, getProfile);
            if (partner != null)
                return new JealousResolution {
                    Party = partner, Stake = JealousStake.PlayerPartner, Courted = courted, Other = courted
                };

            // 3) A rival suitor for the courted NPC — kept consistent via the locked id when present.
            Hero rival = ResolveRivalSuitor(courted, lockedRivalId);
            if (rival != null)
                return new JealousResolution {
                    Party = rival, Stake = JealousStake.RivalSuitor, Courted = courted
                };

            // 4) A spurned admirer of the player.
            Hero admirer = ResolveSpurnedAdmirer(courted, getProfile);
            if (admirer != null)
                return new JealousResolution {
                    Party = admirer, Stake = JealousStake.SpurnedAdmirer, Courted = courted, Other = courted
                };

            return null;
        }

        private static Hero ResolvePlayerPartner(Hero courted, System.Func<string, NpcProfile> getProfile)
        {
            Hero player = Hero.MainHero;
            Hero spouse = player.Spouse;
            if (spouse != null && spouse.IsAlive && spouse != courted)
                return spouse;

            // A committed consort counts as a primary partner for jealousy purposes.
            if (getProfile == null) return null;
            foreach (Hero h in Hero.AllAliveHeroes)
            {
                if (h == courted || h == player) continue;
                NpcProfile p = getProfile(h.StringId);
                if (p?.Romantic == null) continue;
                if (p.Romantic.IsConsort && p.Romantic.Status == RomanticStatus.Committed)
                    return h;
            }
            return null;
        }

        private static Hero ResolveRivalSuitor(Hero courted, string lockedRivalId)
        {
            // A rival suitor only makes sense for someone still unwed — no one schemes to marry a
            // person already married (to the player or to anyone else).
            if (courted.Spouse != null) return null;

            // Honour a previously locked rival if they are still a valid match for the courted NPC.
            if (!string.IsNullOrEmpty(lockedRivalId))
            {
                Hero locked = Hero.FindFirst(h => h.StringId == lockedRivalId);
                if (locked != null && locked.IsAlive && MarriageProspectResolver.IsMarriageable(courted, locked))
                    return locked;
            }

            Hero best = null;
            int bestRelation = RivalRelationThreshold - 1;
            foreach (Hero h in Hero.AllAliveHeroes)
            {
                if (h == courted || h == Hero.MainHero) continue;
                if (!MarriageProspectResolver.IsMarriageable(courted, h)) continue; // unwed, adult, opposite sex to X, age gap
                int rel = h.GetRelation(courted);
                if (rel > bestRelation)
                {
                    bestRelation = rel;
                    best = h;
                }
            }
            return best;
        }

        private static Hero ResolveSpurnedAdmirer(Hero courted, System.Func<string, NpcProfile> getProfile)
        {
            if (getProfile == null) return null;
            Hero player = Hero.MainHero;

            Hero best = null;
            int bestAttraction = SpurnedAttractionThreshold - 1;
            foreach (Hero h in Hero.AllAliveHeroes)
            {
                if (h == courted || h == player) continue;
                if (h.Spouse == player) continue; // already the player's — not spurned
                NpcProfile p = getProfile(h.StringId);
                if (p?.Romantic == null) continue;
                if (p.Romantic.AttractionToPlayer > bestAttraction)
                {
                    bestAttraction = p.Romantic.AttractionToPlayer;
                    best = h;
                }
            }
            return best;
        }
    }
}
