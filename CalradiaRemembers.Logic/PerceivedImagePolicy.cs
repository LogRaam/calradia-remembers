// Code written by Gabriel Mailhot, 30/09/2026.
// THE PERCEIVED IMAGE, increment P1 (ROADMAP, SELF-ESTEEM; Gabriel 30/09/2026: "la combinaison des deux devrait
// permettre de generer une image percue du NPC qui serait affichee dans le panneau d'info sur le NPC dans le chat").
// One numberless sentence of how a character seems: his bearing (self-esteem) and his day (the mood of the day). The
// 14/09 design rejected a mood BAR (a texture turned into a mechanic to optimise); a sentence of prose is what it left
// open, and this is it. Built from templates, no model call, so it costs nothing and updates as the mood moves.
//
// Ruling 2 (30/09/2026): it shows what SHOWS. A man who keeps face (a calculating mind, a great house) hides a shaken
// self-regard from a stranger; someone who knows him well sees the strain beneath. The image is a reward of the bond.

#region

using NpcMemoryService.Core.Mood;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>What the image is composed from.</summary>
    public sealed class PerceivedImageFacts
    {
        /// <summary>How he regards himself lately; null when unknown.</summary>
        public SelfEsteemBand? SelfEsteem { get; set; }

        /// <summary>His mood as it stands now (the day's, as the player has moved it); null when none.</summary>
        public DayMood? Mood { get; set; }

        public bool IsFemale { get; set; }

        /// <summary>The player knows him well (personal regard at friendly or better): masks do not hold.</summary>
        public bool KnowsHimWell { get; set; }

        /// <summary>He keeps face before strangers (a calculating mind, or the bearing of a great house).</summary>
        public bool KeepsFace { get; set; }
    }

    /// <summary>Composes the perceived image. Pure.</summary>
    public static class PerceivedImagePolicy
    {
        /// <summary>The sentence, or null when nothing is known.</summary>
        public static string Compose(PerceivedImageFacts f)
        {
            if (f == null || (f.SelfEsteem == null && f.Mood == null)) return null;

            string he = f.IsFemale ? "She" : "He";
            string himself = f.IsFemale ? "herself" : "himself";
            string him = f.IsFemale ? "her" : "him";

            SelfEsteemBand esteem = f.SelfEsteem ?? SelfEsteemBand.Steady;
            bool low = esteem == SelfEsteemBand.Shaken || esteem == SelfEsteemBand.Broken;
            bool masked = low && f.KeepsFace;

            string bearing = masked
                ? f.KnowsHimWell
                    ? $"{he} carries {himself} easily, but you know {him} well enough to see the strain beneath it"
                    : $"{he} carries {himself} easily"
                : esteem switch {
                    SelfEsteemBand.Broken => $"{he} carries {himself} like someone who has been beaten too often",
                    SelfEsteemBand.Shaken => $"{he} holds {himself} a little too carefully, as if braced for a blow",
                    SelfEsteemBand.Assured => $"{he} carries {himself} with quiet assurance",
                    SelfEsteemBand.Proud => $"{he} carries {himself} high, like someone used to being deferred to",
                    _ => $"{he} carries {himself} easily"
                };

            string day = DayClause(f.Mood, f.IsFemale);

            return day == null ? bearing + "." : bearing + ", " + day + ".";
        }

        private static string DayClause(DayMood? mood, bool female)
        {
            string he = female ? "she" : "he";
            string his = female ? "her" : "his";

            return mood switch {
                DayMood.Grim => $"and today {his} face is set and grim",
                DayMood.ShortTempered => $"and today {his} patience is plainly thin",
                DayMood.Guarded => $"and today {he} gives nothing away",
                DayMood.Restless => $"and today {he} cannot keep still",
                DayMood.Expansive => $"and today {he} is in an open, talkative humour",
                DayMood.HighSpirits => $"and today {he} is in fine spirits",
                DayMood.Weary => $"and today {he} looks worn out",
                _ => null
            };
        }
    }
}
