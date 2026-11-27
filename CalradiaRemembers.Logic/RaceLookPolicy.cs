// Code written by Gabriel Mailhot, 26/11/2026.
// CatWithFourEyes (Discord, 26/11/2026), on "NPCs see what you actually look like": "can this thing be overriden with text
// description fully? I mean, some mods have non-human races, like LOTR, GoT and smt-like-Forgotten Realms conversions, or
// New_race". CR read gender, age, build, hair and skin from the face, never the race, so an elf or a dwarf was "a tall man".
// Gabriel (26/11/2026): "on fait b puis a": first a file per race in the Custom folder (races.json, keyed by the game's
// race name) that names the people a character belongs to, may add one line of its own, and may silence the facts a face
// reads wrongly for that race (a green orc read as "dark-skinned"); then a description that replaces the game's.
// A race with no entry reads exactly as before.

#region

using System;
using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>How one of the game's races is described, as written in races.json.</summary>
    public sealed class RaceLook
    {
        /// <summary>The people's name ("elf", "dwarf", "orc"): a woman reads "an elf woman", a man "an elf man".</summary>
        public string Noun { get; init; }

        /// <summary>A woman's whole noun when the author wants another word ("she-orc"); wins over <see cref="Noun" />.</summary>
        public string FemaleNoun { get; init; }

        /// <summary>A man's whole noun when the author wants another word; wins over <see cref="Noun" />.</summary>
        public string MaleNoun { get; init; }

        /// <summary>One sentence of the author's own, added after the derived description ("Long ears rise through the hair.").</summary>
        public string Line { get; init; }

        /// <summary>The derived facts left unsaid for this race: skin, hair, eyes, beard, build, height, age.</summary>
        public IReadOnlyCollection<string> Omit { get; init; } = new List<string>();
    }

    /// <summary>Finds a race's look and applies it to a hero's appearance facts. Pure.</summary>
    public static class RaceLookPolicy
    {
        /// <summary>The words <see cref="RaceLook.Omit" /> understands.</summary>
        public static readonly IReadOnlyList<string> OmittableFacts = new[] {"skin", "hair", "eyes", "beard", "build", "height", "age"};

        /// <summary>The look written for <paramref name="raceName" /> (the game's race name, any case), or null for none.</summary>
        public static RaceLook Find(IReadOnlyDictionary<string, RaceLook> table, string raceName)
        {
            if (table == null || string.IsNullOrWhiteSpace(raceName)) return null;

            foreach (KeyValuePair<string, RaceLook> entry in table)
                if (string.Equals(entry.Key?.Trim(), raceName.Trim(), StringComparison.OrdinalIgnoreCase))
                    return entry.Value;

            return null;
        }

        /// <summary>
        ///   The noun a hero of this race is called by ("elf woman", or the author's own "she-orc"), or null when there is no
        ///   look or it names no people: the caller then keeps "woman" or "man".
        /// </summary>
        public static string KindNoun(RaceLook look, bool isFemale)
        {
            if (look == null) return null;

            string whole = (isFemale ? look.FemaleNoun : look.MaleNoun)?.Trim();
            if (!string.IsNullOrEmpty(whole)) return whole;

            string noun = look.Noun?.Trim();

            return string.IsNullOrEmpty(noun) ? null : noun + (isFemale ? " woman" : " man");
        }

        /// <summary>"a" or "an" before <paramref name="nounPhrase" />, by its first letter.</summary>
        public static string WithArticle(string nounPhrase)
        {
            string phrase = (nounPhrase ?? "").Trim();
            if (phrase.Length == 0) return phrase;

            return ("aeiouAEIOU".IndexOf(phrase[0]) >= 0 ? "an " : "a ") + phrase;
        }

        /// <summary>
        ///   The facts as this race's look has them: its noun and line carried, the omitted facts blanked (an omitted build
        ///   or height reads as average, which is never voiced). With no look the facts come back unchanged.
        /// </summary>
        public static AppearanceFacts Apply(AppearanceFacts facts, RaceLook look)
        {
            if (facts == null || look == null) return facts;

            var omit = new HashSet<string>((look.Omit ?? new List<string>()).Where(o => o != null).Select(o => o.Trim()), StringComparer.OrdinalIgnoreCase);
            string line = look.Line?.Trim();

            return new AppearanceFacts {
                IsFemale = facts.IsFemale,
                AgeYears = omit.Contains("age") ? 0 : facts.AgeYears,
                HeightNormalized = omit.Contains("height") ? 0.5 : facts.HeightNormalized,
                BuildNormalized = omit.Contains("build") ? 0.5 : facts.BuildNormalized,
                WeightNormalized = omit.Contains("build") ? 0.5 : facts.WeightNormalized,
                HairColorDescriptor = omit.Contains("hair") ? null : facts.HairColorDescriptor,
                HasBeard = !omit.Contains("beard") && facts.HasBeard,
                SkinToneDescriptor = omit.Contains("skin") ? null : facts.SkinToneDescriptor,
                EyeColorDescriptor = omit.Contains("eyes") ? null : facts.EyeColorDescriptor,
                KindNoun = KindNoun(look, facts.IsFemale),
                RaceLine = string.IsNullOrEmpty(line) ? null : line
            };
        }
    }
}
