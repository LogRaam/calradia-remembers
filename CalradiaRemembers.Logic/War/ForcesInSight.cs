// Code written by Gabriel Mailhot, 05/11/2026.
// The armies and war parties a character can see from where they stand on the campaign map. 3chmidt (Nexus, 05/11/2026):
// "when you speak with an NPC on the open map, they don't notice friendly and enemy armies on the map that are nearby.
// Like when you speak with a friendly lord on the map, they don't notice an army 5 meters away, even if it is an enemy
// army with 1800 soldiers, and they act surprised if you mention a nearby army." Nothing in the prompt said what stood
// in sight. This picks what is said: the nearest forces first, an army before a lone party at the same distance, a few
// at most, each as the eye reads it (whose banner, friend or foe, roughly how many). Pure; the host reads the map.

#region

using System.Collections.Generic;
using System.Linq;

#endregion

namespace CalradiaRemembers.Logic.War
{
    /// <summary>Whose side a force in sight stands on, seen from the character speaking.</summary>
    public enum SightedSide
    {
        /// <summary>Neither ally nor foe of the speaker's realm.</summary>
        Neutral,

        /// <summary>The speaker's own realm, or its allies.</summary>
        Friendly,

        /// <summary>A realm at war with the speaker's.</summary>
        Enemy,

        /// <summary>Bandits, outlaws or raiders: no realm's banner.</summary>
        Outlaws
    }

    /// <summary>One force the speaker can see: an army (its whole host) or a single party.</summary>
    public sealed class SightedForce
    {
        /// <summary>Who leads it, as the speaker would name them ("Lord Derthert"); blank for outlaws.</summary>
        public string Leader { get; init; } = "";

        /// <summary>The realm or band whose colours it flies ("Vlandia", "sea raiders").</summary>
        public string Banner { get; init; } = "";

        /// <summary>How many men it holds, all its parties together for an army.</summary>
        public int Men { get; init; }

        /// <summary>It is an army (several parties marching under one leader), not a single party.</summary>
        public bool IsArmy { get; init; }

        /// <summary>Its side, seen from the speaker.</summary>
        public SightedSide Side { get; init; }

        /// <summary>How far it stands from the conversation, in map units.</summary>
        public float Distance { get; init; }
    }

    /// <summary>What forces in sight are told to the character, and in what words.</summary>
    public static class ForcesInSight
    {
        /// <summary>The most forces named in one turn: the prompt names the nearest, never the whole map.</summary>
        public const int MaxNamed = 5;

        /// <summary>Fewer men than this read as a handful, not a number.</summary>
        public const int HandfulBelow = 20;

        /// <summary>
        ///   The lines to tell, nearest first (an army before a party at the same distance), at most <see cref="MaxNamed" />;
        ///   empty when nothing is in sight. A force of no men is not one.
        /// </summary>
        public static List<string> Lines(IEnumerable<SightedForce> sighted)
        {
            return (sighted ?? Enumerable.Empty<SightedForce>())
                  .Where(f => f != null && f.Men > 0)
                  .OrderBy(f => f.Distance)
                  .ThenByDescending(f => f.IsArmy)
                  .Take(MaxNamed)
                  .Select(Describe)
                  .ToList();
        }

        /// <summary>One force as the eye reads it: "an enemy army of Vlandia under Lord Derthert, some 1,800 men".</summary>
        public static string Describe(SightedForce f)
        {
            string kind = f.IsArmy ? "army" : "party";
            string men = MenWords(f.Men);
            string under = string.IsNullOrWhiteSpace(f.Leader) ? "" : $" under {f.Leader.Trim()}";
            string banner = string.IsNullOrWhiteSpace(f.Banner) ? "" : f.Banner.Trim();

            switch (f.Side)
            {
                case SightedSide.Outlaws:
                    return $"a band of {(banner.Length > 0 ? banner : "outlaws")}, {men}";
                case SightedSide.Enemy:
                    return $"an enemy {kind}{(banner.Length > 0 ? $" of {banner}" : "")}{under}, {men}";
                case SightedSide.Friendly:
                    return $"a friendly {kind}{(banner.Length > 0 ? $" of {banner}" : "")}{under}, {men}";
                default:
                    return $"{(f.IsArmy ? "an army" : "a party")}{(banner.Length > 0 ? $" of {banner}" : "")}{under}, neither friend nor foe, {men}";
            }
        }

        /// <summary>
        ///   A count as a captain judges it at a glance: a handful, then tens, then fifties, then hundreds; never the exact
        ///   figure, which nobody could read off a column on the march.
        /// </summary>
        public static string MenWords(int men)
        {
            if (men < HandfulBelow) return "a handful of men";
            int step = men < 100 ? 10 : men < 1000 ? 50 : 100;
            int rounded = (men + step / 2) / step * step;

            return $"some {rounded:N0} men";
        }
    }
}
