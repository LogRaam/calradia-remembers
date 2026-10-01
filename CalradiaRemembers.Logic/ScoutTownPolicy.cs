// Code written by Gabriel Mailhot, 01/10/2026.
// fkasad (Nexus, 01/10/2026): "Go scout a city (actually entering the city not like the vanilla where you just have to
// spend some time nearby). Make contact with local gang leader, negotiate with him for intel." A lord at war sends the
// player into an enemy town; the deed is the gang leader agreeing, in his own conversation, to tell what he knows, and
// what he knows is the town's true state, read off the game in the same bands the seat-standing pack already gives him.
// The report is the task's evidence, so the lord who sent the player reads it when he reports back.
//
// Lowering the town's security through the gang leader (fkasad's second half) is deliberately NOT here: it changes
// the world, and is kept for later.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Knowledge;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>Whether a town may be named for a scout_town task.</summary>
    public sealed class ScoutTownFacts
    {
        /// <summary>A town: a castle has no streets and no gang to buy intel from.</summary>
        public bool IsTown { get; set; }

        /// <summary>The giver's realm is at war with the town's: nobody scouts a friend's walls.</summary>
        public bool GiverAtWarWithIt { get; set; }

        /// <summary>A gang leader in the world lives there: the man the deed depends on.</summary>
        public bool HasGangLeader { get; set; }
    }

    /// <summary>The rules of a scout_town task, and the report the gang leader gives. Pure.</summary>
    public static class ScoutTownPolicy
    {
        /// <summary>Whether this town may be named.</summary>
        public static bool MayAsk(ScoutTownFacts f) => f != null && f.IsTown && f.GiverAtWarWithIt && f.HasGangLeader;

        /// <summary>
        ///   The report, from what the town truly is: what its walls hold, what its granary holds and which way it
        ///   goes, how its people feel, how safe its streets are. A band the game could not read is left out.
        /// </summary>
        public static string Report(SeatFacts town, string informant, int day)
        {
            var parts = new List<string>();
            AddIf(parts, Garrison(town?.Garrison ?? GarrisonBand.Unknown));
            AddIf(parts, Food(town?.FoodStores ?? FoodBand.Unknown, town?.FoodDirection ?? FoodTrend.Unknown));
            AddIf(parts, Loyalty(town?.Loyalty ?? LoyaltyBand.Unknown));
            AddIf(parts, Security(town?.Security ?? SecurityBand.Unknown));

            string what = parts.Count == 0 ? "little worth carrying back" : string.Join("; ", parts);

            return $"Learned from {informant}, inside {town?.PlaceName}, on day {day}: {what}";
        }

        private static void AddIf(List<string> parts, string part)
        {
            if (!string.IsNullOrEmpty(part)) parts.Add(part);
        }

        private static string Garrison(GarrisonBand b) => b switch {
            GarrisonBand.Skeleton => "a skeleton garrison, a handful of men on the walls",
            GarrisonBand.UnderStrength => "a garrison under strength",
            GarrisonBand.Adequate => "a garrison adequate to hold",
            GarrisonBand.Strong => "a strong garrison",
            _ => null
        };

        private static string Food(FoodBand b, FoodTrend t)
        {
            string stores = b switch {
                FoodBand.Empty => "its granary nearly empty",
                FoodBand.Thin => "thin stores",
                FoodBand.Adequate => "adequate stores",
                FoodBand.Full => "full granaries",
                _ => null
            };
            if (stores == null) return null;

            return t switch {
                FoodTrend.Falling => stores + ", and falling",
                FoodTrend.Rising => stores + ", and rising",
                _ => stores
            };
        }

        private static string Loyalty(LoyaltyBand b) => b switch {
            LoyaltyBand.Rebellious => "its people on the edge of revolt",
            LoyaltyBand.Restless => "its people restless",
            LoyaltyBand.Content => "its people content",
            LoyaltyBand.Devoted => "its people devoted to their lord",
            _ => null
        };

        private static string Security(SecurityBand b) => b switch {
            SecurityBand.Lawless => "lawless streets",
            SecurityBand.Uneasy => "uneasy streets",
            SecurityBand.Settled => "well-kept streets",
            _ => null
        };
    }
}
