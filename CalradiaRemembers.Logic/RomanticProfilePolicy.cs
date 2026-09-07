// Code written by Gabriel Mailhot, 01/07/2026.
// Extracted from CalradiaRemembers.Mapping.RomanticProfileBuilder: the pure core of a romantic
// profile roll (trait-derived sketches, orientation, preference/kink pools and picks), moved next to
// its tests. BEHAVIOR-PRESERVING: same Random instance, same call order as before, so an existing
// NPC's frozen kinks/preferences do not change; only the ordering of the CALLS matters, not which
// class hosts the method body.

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   The pure decisions behind a fresh <c>RomanticProfile</c> roll: the trait-based fallback
    ///   sketches, sexual orientation, and the preference/kink pools + distinct picks. Takes the
    ///   caller's own <see cref="Random" /> instance directly (rather than a Func-seam) so the exact
    ///   sequence of draws made by the original engine code is reproduced call-for-call.
    /// </summary>
    public static class RomanticProfilePolicy
    {
        /// <summary>
        ///   Derives relational and intimate sketches directly from trait values, for archetypes with
        ///   no named sketch. Every combination of traits produces coherent prose.
        /// </summary>
        public static (string relational, string intimate) BuildTraitBasedSketches(RomanticTraits t)
        {
            // ── Relational sketch ─────────────────────────────────────────────

            var relParts = new List<string>();

            // Honor axis: fidelity and reliability
            if (t.Honor >= 1)
                relParts.Add("Their word means something in love as in all things — a declaration from them carries weight, a commitment is kept.");
            else if (t.Honor <= -1)
                relParts.Add("Oaths sit lightly on them in love as in politics — the warmth may be real without the reliability being so.");

            // Mercy axis: emotional texture
            if (t.Mercy >= 1)
                relParts.Add("There is genuine warmth in how they approach attachment — they forgive, they consider, they show up.");
            else if (t.Mercy <= -1)
                relParts.Add("No softness in how they love — direct, demanding, and entirely without sentiment about the harder moments.");

            // Valor axis: pace and pursuit
            if (t.Valor >= 1)
                relParts.Add("Pursues directly and without hesitation — reticence bores them; clear interest draws an equally clear response.");
            else if (t.Valor <= -1)
                relParts.Add("Cautious in pursuit as in all things — approaches slowly, needs safety before risking the declaration.");

            // Generosity axis: giving and attachment
            if (t.Generosity >= 1)
                relParts.Add("Generous in courtship and more so in commitment — gives time, attention, and resource without careful accounting.");
            else if (t.Generosity <= -1)
                relParts.Add("Keeps something in reserve even here — does not lavish, does not overflow, holds their offerings carefully.");

            // Calculating axis: strategy vs. spontaneity
            if (t.Calculating >= 1)
                relParts.Add("Nothing about their approach is accidental — timing is considered, words are chosen, the architecture of the courtship is deliberate.");
            else if (t.Calculating <= -1)
                relParts.Add("Acts on feeling before thought, which produces both the best and most disastrous moments of their romantic life.");

            string relational = string.Join(" ", relParts);
            if (string.IsNullOrEmpty(relational))
                relational = "Follows the expected path in love as in everything — attachment develops through familiarity and persists through habit.";

            // ── Intimate sketch ───────────────────────────────────────────────

            var intParts = new List<string>();

            if (t.Mercy >= 1)
                intParts.Add("Attentive and present in intimacy — the partner's experience is not an afterthought.");
            else if (t.Mercy <= -1)
                intParts.Add("Intensity without softness — what they bring is real and direct, without the comfort of reassurance around it.");

            if (t.Valor >= 1)
                intParts.Add("Confident and unhesitating — brings the same energy here as to any confrontation, which turns out to be considerable.");
            else if (t.Valor <= -1)
                intParts.Add("Careful and deliberate — needs the room to feel unhurried, which, given the space, produces something genuine.");

            if (t.Calculating >= 1)
                intParts.Add("Observant and precise — has paid attention and applies what they have learned with reliable effect.");
            else if (t.Calculating <= -1)
                intParts.Add("Entirely in the moment — no plan, no architecture, just presence, which is either exhilarating or disorganized depending on the partner.");

            if (t.Honor >= 1)
                intParts.Add("What they say they will do, they do — no false promises, no manufactured intensity.");

            string intimate = string.Join(" ", intParts);
            if (string.IsNullOrEmpty(intimate))
                intimate = "Familiar and unhurried — finds comfort in the known, presence in the moment, consistency as its own intimacy.";

            return (relational, intimate);
        }

        // ── Orientation ───────────────────────────────────────────────────────

        /// <summary>
        ///   Homosexual, Pansexual and Asexual are kept in the enum for mod use but are not assigned
        ///   in vanilla: lords marry the opposite gender by game mechanic, so pure homosexuality
        ///   creates incoherent data.
        /// </summary>
        public static SexualOrientation DetermineOrientation(Random rng)
        {
            int roll = rng.Next(100);
            if (roll < 65) return SexualOrientation.Heterosexual;
            if (roll < 90) return SexualOrientation.BiCurious;
            return SexualOrientation.Bisexual;
        }

        // ── Preferences ───────────────────────────────────────────────────────

        /// <summary>
        ///   Rolls 1-2 distinct preferences from the pool this character's traits and CULTURE make plausible.
        ///   <paramref name="culture" /> is the hero's own jealousy norm (null for an unknown culture, which falls
        ///   back to monogamy exactly as jealousy_cultures.json documents), and <paramref name="clanTier" /> is
        ///   their standing, which that norm's own elite gate reads.
        /// </summary>
        public static List<RomanticPreference> DeterminePreferences(
            Random rng, RomanticTraits traits, JealousyCulture culture = null, int clanTier = 0)
        {
            HashSet<RomanticPreference> pool = BuildPreferencePool(traits, culture, clanTier);
            int count = rng.Next(1, 3);
            return PickDistinct(pool, count, rng);
        }

        /// <summary>
        ///   The candidate preferences a hero's traits and culture make plausible. Switch is always eligible.
        ///   <para>
        ///     Polyamory hangs on the CULTURE, never on honour (Gabriel's ruling, 2026-09-07, after a player asked
        ///     why it took a dishonourable trait to reach it). Honour decides INFIDELITY, the betrayal of an
        ///     exclusivity that was actually promised, which is a different thing and is already governed where it
        ///     genuinely happens. Keeping both on one trait meant the mod declared in jealousy_cultures.json that
        ///     some cultures hold shared partners normal, then generated strictly monogamous nobles from them.
        ///   </para>
        /// </summary>
        public static HashSet<RomanticPreference> BuildPreferencePool(
            RomanticTraits t, JealousyCulture culture = null, int clanTier = 0)
        {
            HashSet<RomanticPreference> pool = new() {RomanticPreference.Switch};

            if (t.Honor >= 1) { pool.Add(RomanticPreference.MonogamousStrict); pool.Add(RomanticPreference.Devoted); }
            if (t.Honor <= -1) { pool.Add(RomanticPreference.MonogamousFlexible); pool.Add(RomanticPreference.Casual); }

            // The culture's OWN definition of tolerating shared partners, reused rather than a second one invented
            // here, so the elite gate and the exclusivity stance stay in one place: jealousy_cultures.json.
            if (culture != null && culture.ToleratesSharedPartners(clanTier)) pool.Add(RomanticPreference.Polyamorous);
            if (t.Honor == 0) pool.Add(RomanticPreference.MonogamousFlexible);
            if (t.Mercy <= -1) pool.Add(RomanticPreference.Dominant);
            if (t.Mercy >= 1) { pool.Add(RomanticPreference.Submissive); pool.Add(RomanticPreference.Devoted); }
            if (t.Calculating >= 1) { pool.Add(RomanticPreference.SlowBurn); pool.Add(RomanticPreference.Reserved); }
            if (t.Calculating <= -1) pool.Add(RomanticPreference.Intense);
            if (t.Valor >= 1) pool.Add(RomanticPreference.Intense);
            if (t.Valor <= -1) pool.Add(RomanticPreference.Reserved);
            if (t.Generosity >= 1) pool.Add(RomanticPreference.Devoted);
            if (t.Generosity <= -1) { pool.Add(RomanticPreference.Independent); pool.Add(RomanticPreference.Possessive); }
            if (t.Mercy <= -1 && t.Calculating >= 1) pool.Add(RomanticPreference.Possessive);
            if (t.Valor >= 1 && t.Honor <= -1) pool.Add(RomanticPreference.Casual);

            return pool;
        }

        // ── Kinks ─────────────────────────────────────────────────────────────

        /// <summary>Rolls a kink count (weighted toward 0) then picks that many distinct kinks from the pool.</summary>
        public static List<Kink> DetermineKinks(Random rng, RomanticTraits traits)
        {
            int roll = rng.Next(100);
            int count;
            if (roll < 60) count = 0;
            else if (roll < 85) count = 1;
            else if (roll < 97) count = 2;
            else count = 3;

            if (count == 0) return new List<Kink>();

            HashSet<Kink> pool = BuildKinkPool(traits);
            return PickDistinct(pool, count, rng);
        }

        /// <summary>The candidate kinks a hero's traits make plausible. A small base pool is always eligible.</summary>
        public static HashSet<Kink> BuildKinkPool(RomanticTraits t)
        {
            HashSet<Kink> pool = new() {
                Kink.Roleplay, Kink.PublicAffection, Kink.Voyeurism, Kink.Exhibitionism
            };

            if (t.Mercy <= -1)
            {
                pool.Add(Kink.Dominance); pool.Add(Kink.Sadism); pool.Add(Kink.BondageGiving); pool.Add(Kink.PowerImbalance);
                pool.Add(Kink.Degradation); pool.Add(Kink.FreeUse); pool.Add(Kink.ImpactPlay);
                pool.Add(Kink.FearPlay); pool.Add(Kink.Objectification);
            }
            if (t.Mercy >= 1)
            {
                pool.Add(Kink.Submission); pool.Add(Kink.BondageReceiving); pool.Add(Kink.Masochism);
                pool.Add(Kink.Praise);
            }
            if (t.Honor <= -1)
            {
                pool.Add(Kink.PowerImbalance); pool.Add(Kink.SwitchTendencies);
                pool.Add(Kink.CorruptionKink); pool.Add(Kink.Degradation);
            }
            if (t.Honor >= 1)
            {
                pool.Add(Kink.Breeding); pool.Add(Kink.Praise);
            }
            if (t.Calculating >= 1)
            {
                pool.Add(Kink.Dominance); pool.Add(Kink.Possessiveness); pool.Add(Kink.BondageGiving);
                pool.Add(Kink.OrgasmControl); pool.Add(Kink.Chastity); pool.Add(Kink.Objectification);
                pool.Add(Kink.SensoryDeprivation); pool.Add(Kink.Training); pool.Add(Kink.MasterSlave); pool.Add(Kink.PetPlay);
            }
            if (t.Valor >= 1)
            {
                pool.Add(Kink.Exhibitionism); pool.Add(Kink.Masochism);
                pool.Add(Kink.Prize);
            }
            if (t.Generosity <= -1)
            {
                pool.Add(Kink.Possessiveness);
                pool.Add(Kink.MasterSlave); pool.Add(Kink.FreeUse); pool.Add(Kink.PetPlay);
            }

            return pool;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        /// <summary>Shuffles the pool (Fisher-Yates) and takes the first <paramref name="count" /> entries.</summary>
        public static List<T> PickDistinct<T>(HashSet<T> pool, int count, Random rng)
        {
            List<T> source = new(pool);
            Shuffle(source, rng);
            return source.GetRange(0, Math.Min(count, source.Count));
        }

        private static void Shuffle<T>(List<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
