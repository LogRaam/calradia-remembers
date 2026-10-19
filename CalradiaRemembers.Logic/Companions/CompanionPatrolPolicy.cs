// Code written by Gabriel Mailhot, 17/10/2026.
// A companion patrols a settlement for the player with men taken from the player's party. jaserms1 (Nexus,
// 16/10/2026): a war council ordered a companion to patrol villages and nothing happened. Gabriel the same day: "Les
// compagnons doivent pouvoir accomplir des missions pour le joueur." His rulings: a patrol anywhere, with the
// player's own men, until the player says stop (in chat or by letter), and a letter of news each month. On 17/10 he
// added the troop screen (choose the men, companions included, up to the leader's party size) and accepted that the
// clan pays the patrol's wages.
//
// The engine spike he ran on 17/10/2026 (cr.patrol_spike, Ebereth) is what these rules answer. With the AI lock off
// the patrol engages bandits on its own. Without a leash it drifts: after a fight its behaviour still read
// PatrolAroundPoint while the anchor had moved, 117 away in three days. With the leash it came back, and a 10-man
// band fled for half a day and was then beaten and its leader captured.

namespace CalradiaRemembers.Logic.Companions
{
    /// <summary>What the hourly leash knows of a patrolling party.</summary>
    public sealed class PatrolLeashFacts
    {
        /// <summary>The party is in a battle right now; nothing is touched.</summary>
        public bool InBattle { get; init; }

        /// <summary>Its short-term behaviour is chasing or fleeing a party: re-issuing would break off the fight or the escape.</summary>
        public bool EngagingOrFleeing { get; init; }

        /// <summary>Its default behaviour is still a patrol.</summary>
        public bool Patrolling { get; init; }

        /// <summary>The patrol is anchored on the ordered settlement (the engine re-anchors it elsewhere after a fight).</summary>
        public bool AnchoredOnOrderedSettlement { get; init; }

        /// <summary>Map distance from the party to the ordered settlement.</summary>
        public float DistanceToSettlement { get; init; }

        /// <summary>The party's food is running out, so a trip to buy it is allowed.</summary>
        public bool FoodLow { get; init; }
    }

    /// <summary>Pure: the companion patrol's rules (men to prefill, the leash, the monthly letter, food).</summary>
    public static class CompanionPatrolPolicy
    {
        /// <summary>Farther than this from the ordered settlement, the patrol is sent back, whatever its behaviour reads.</summary>
        public const float LeashDistance = 15f;

        /// <summary>Men prefilled on the troop screen when the order named no number (the player changes it there).</summary>
        public const int DefaultBand = 20;

        /// <summary>Days between two letters of news from a patrolling companion (Gabriel: each month).</summary>
        public const int ReportEveryDays = 30;

        /// <summary>Days of food the patrol takes from the player's stock when it rides out.</summary>
        public const int FoodDaysAtDeparture = 5;

        /// <summary>Under this many days of food the patrol may leave its post to buy more.</summary>
        public const int FoodLowDays = 2;

        /// <summary>
        ///   The men prefilled on the troop screen: the number the order named, else <see cref="DefaultBand" />, never
        ///   more than half the player's healthy men (the player can still move more by hand on the screen).
        /// </summary>
        public static int PrefillMen(int? namedInOrder, int playerHealthyMen)
        {
            if (playerHealthyMen <= 0) return 0;
            int wanted = namedInOrder is > 0 ? namedInOrder.Value : DefaultBand;
            int ceiling = namedInOrder is > 0 ? playerHealthyMen : playerHealthyMen / 2;

            return wanted < ceiling ? wanted : ceiling;
        }

        /// <summary>
        ///   True when the patrol must be re-issued on its settlement this hour: never in a battle, a chase or a flight;
        ///   otherwise when it stopped patrolling (unless it went to buy food it lacks), when its patrol was re-anchored
        ///   elsewhere, or when it is farther than <see cref="LeashDistance" />.
        /// </summary>
        public static bool ShouldReissue(PatrolLeashFacts facts)
        {
            if (facts == null || facts.InBattle || facts.EngagingOrFleeing) return false;
            if (!facts.Patrolling) return !facts.FoodLow;
            if (!facts.AnchoredOnOrderedSettlement) return true;

            return facts.DistanceToSettlement > LeashDistance;
        }

        /// <summary>True when a month has passed since the last letter (or the departure).</summary>
        public static bool ReportDue(int lastReportDay, int today) => today - lastReportDay >= ReportEveryDays;

        /// <summary>Food units to move to a band of <paramref name="men" /> for <see cref="FoodDaysAtDeparture" /> days, at the engine's daily need per man.</summary>
        public static int FoodUnitsForDeparture(int men, float dailyFoodPerMan)
        {
            if (men <= 0 || dailyFoodPerMan <= 0f) return 0;
            float units = men * dailyFoodPerMan * FoodDaysAtDeparture;

            // A float rate (0.05f) is never exact; without the margin 10.0000001 would round up to a unit nobody needs.
            return (int) System.Math.Ceiling(units - 0.001f);
        }
    }
}
