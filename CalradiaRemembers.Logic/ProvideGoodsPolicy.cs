// Code written by Gabriel Mailhot, 08/07/2026.
// Personal supply quests (referential increment 2, ratified 2026-07-08): a lord may ask the player to bring a
// COUNT of goods of a category (horses for the cavalry, livestock or grain to feed the host), handed over for
// real. This is the pure part: normalizing the category word the LLM wrote to a canonical key, clamping the
// count, and the satisfied test. Mapping a canonical key to actual game item objects is engine glue (mod-side),
// out of scope here.

namespace CalradiaRemembers.Logic
{
    /// <summary>Pure rules for a <c>ProvideGoods</c> supply task: category normalization, count clamp, satisfaction.</summary>
    public static class ProvideGoodsPolicy
    {
        /// <summary>Canonical category key: mounts.</summary>
        public const string Horses = "horses";

        /// <summary>Canonical category key: cattle, sheep, hogs, and the like.</summary>
        public const string Livestock = "livestock";

        /// <summary>Canonical category key: grain and staple food.</summary>
        public const string Grain = "grain";

        /// <summary>TUNE: the smallest supply count a task may demand.</summary>
        public const int MinCount = 1;

        /// <summary>TUNE: the largest supply count a task may demand (a lord does not ask for a herd of a thousand).</summary>
        public const int MaxCount = 200;

        /// <summary>
        ///   Normalizes a raw category word (as the LLM wrote it, plus common synonyms) to one of the three
        ///   canonical keys, or null when it names no supported supply category.
        /// </summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            switch (raw.Trim().ToLowerInvariant())
            {
                case "horse":
                case "horses":
                case "mount":
                case "mounts":
                case "warhorse":
                case "war horse":
                case "warhorses":
                case "steed":
                case "steeds":
                    return Horses;
                case "livestock":
                case "cattle":
                case "cow":
                case "cows":
                case "sheep":
                case "hog":
                case "hogs":
                case "pig":
                case "pigs":
                case "goat":
                case "goats":
                case "animals":
                    return Livestock;
                case "grain":
                case "grains":
                case "wheat":
                case "food":
                case "corn":
                case "provisions":
                    return Grain;
                default:
                    return null;
            }
        }

        /// <summary>Clamps a demanded count into the sane band.</summary>
        public static int ClampCount(int count)
            => count < MinCount ? MinCount : count > MaxCount ? MaxCount : count;

        /// <summary>True when the running tally of handed-over goods meets a positive required count.</summary>
        public static bool IsSatisfied(int handedOverCount, int requiredCount)
            => requiredCount > 0 && handedOverCount >= requiredCount;
    }
}
