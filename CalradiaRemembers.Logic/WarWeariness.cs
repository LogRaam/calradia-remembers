// Code written by Gabriel Mailhot, 30/06/2026.
// Companion war-weariness & honorable retirement (ROADMAP) — the pure model, built first. A SEPARATE axis from
// happiness: it ACCUMULATES with the real toll of war (wounds, captures) instead of drifting like a mood, and a
// spent veteran wants an HONORABLE retirement, not a bitter departure. Valor sets how much war a companion BEARS
// before wanting out (a brave, hardened soul endures far more than a cautious one); real rest slowly eases the
// toll. The engine maps game events to the drivers and persists the score; this stays pure and unit-tested.

using NpcMemoryService.Core.Calendar;
namespace CalradiaRemembers.Logic
{
    /// <summary>How worn down by war a companion is — the bands the accumulating score falls into.</summary>
    public enum WearinessBand
    {
        /// <summary>Untouched by the toll of war.</summary>
        Fresh,

        /// <summary>Blooded and hardened, still willing.</summary>
        Seasoned,

        /// <summary>The years of blood are telling on them.</summary>
        Weary,

        /// <summary>Worn out — they want to lay down the sword.</summary>
        Spent
    }

    /// <summary>What a spent companion asks for when they retire — decided by whether they hold land.</summary>
    public enum RetirementKind
    {
        /// <summary>Landed / a governor: withdraw from the FIELD, but keep the fief and stay in the clan.</summary>
        StepBackToGovern,

        /// <summary>Landless: leave your service entirely, on a warm farewell.</summary>
        LeaveService
    }

    /// <summary>
    ///   Pure war-weariness arithmetic: clamp, the per-event tolls, rest recovery, the Valor-scaled retirement
    ///   tolerance, banding, the "wants to retire" test, and the retirement shape by landedness. All magnitudes
    ///   and thresholds are tunable constants Gabriel adjusts by in-game feel.
    /// </summary>
    public static class WarWeariness
    {
        public const int Min = 0;
        public const int Max = 100;

        /// <summary>The toll a single battle wound adds.</summary>
        public const int WoundWear = 1;

        /// <summary>The toll being taken prisoner on your business adds.</summary>
        public const int CaptureWear = 10;

        /// <summary>Suggested recovery per week of real rest, for the engine's rest tick (a no-cost tuning default).</summary>
        public const int RestStepPerWeek = 3;

        // Retirement tolerance: how high the score must climb before a companion wants out. Valor RAISES it, a
        // brave, hardened soul bears far more war than a cautious one, clamped so even a coward endures some and
        // a hero is not effectively immortal. Raised (2026-07-25) so a companion endures many defeats/wounds and
        // captures before wanting out: Bannerlord is combat-heavy, and the old numbers made retirement come far
        // too easily for a game built around constant fighting.
        private const int BaseRetireThreshold = 90;
        private const int ValorTolerance = 14; // per point of Valor (Bannerlord traits run -2..+2)
        public const int MinRetireThreshold = 55;
        public const int MaxRetireThreshold = 140;

        /// <summary>
        ///   Minimum days of service (2 Calradian years, 2 * 84) before a companion may ask to retire for
        ///   war-weariness, however high their toll: a companion who has served only briefly should never ask
        ///   to leave in their first two years, no matter how hard the toll. Player report 2026-07-25: a
        ///   22-year-old companion with under a year of service asked to retire.
        /// </summary>
        public static int MinServiceDays => 2 * WorldCalendar.DaysPerYear;

        public static int Clamp(int score) => score < Min ? Min : score > Max ? Max : score;

        /// <summary>Moves the score by a raw delta, clamped to [Min, Max].</summary>
        public static int Apply(int score, int delta) => Clamp(score + delta);

        /// <summary>Adds the toll of one battle wound.</summary>
        public static int ApplyWound(int score) => Apply(score, WoundWear);

        /// <summary>Adds the toll of one capture.</summary>
        public static int ApplyCapture(int score) => Apply(score, CaptureWear);

        /// <summary>
        ///   Eases the score toward zero by at most <paramref name="step" /> — recovery during real rest
        ///   (peacetime, benched, garrisoned). A no-op once already rested, and ignores a non-positive step.
        /// </summary>
        public static int Rest(int score, int step)
            => step <= 0 || score <= Min ? Clamp(score) : Clamp(score - step);

        /// <summary>
        ///   How much war this companion bears before wanting out: a base tolerance raised by Valor (a brave,
        ///   hardened lord endures more), clamped so neither the coward nor the hero reaches an absurd extreme.
        /// </summary>
        public static int RetireThreshold(int valor)
        {
            int t = BaseRetireThreshold + ValorTolerance * valor;

            return t < MinRetireThreshold ? MinRetireThreshold : t > MaxRetireThreshold ? MaxRetireThreshold : t;
        }

        /// <summary>True when a companion is worn down enough to ask for retirement (score at or over their tolerance).</summary>
        public static bool WantsToRetire(int score, int valor) => score >= RetireThreshold(valor);

        /// <summary>True once a companion has served at least <see cref="MinServiceDays" /> (the minimum-tenure gate).</summary>
        public static bool HasServedLongEnough(int daysServed) => daysServed >= MinServiceDays;

        /// <summary>
        ///   The weariness band, read RELATIVE to this companion's own Valor-scaled tolerance — so the same raw
        ///   score reads as a worse band for a coward than for a hardened veteran.
        /// </summary>
        public static WearinessBand Band(int score, int valor)
        {
            int thr = RetireThreshold(valor);
            if (score >= thr) return WearinessBand.Spent;
            if (score >= thr * 3 / 5) return WearinessBand.Weary;    // >= 60% of tolerance
            if (score >= thr / 4) return WearinessBand.Seasoned;     // >= 25% of tolerance

            return WearinessBand.Fresh;
        }

        /// <summary>What a spent companion asks for: a landed one steps back to govern; a landless one leaves.</summary>
        public static RetirementKind Retirement(bool isLanded)
            => isLanded ? RetirementKind.StepBackToGovern : RetirementKind.LeaveService;

        /// <summary>A short natural-language reading of the band, for prompts and the encyclopedia line.</summary>
        public static string Describe(int score, int valor)
        {
            switch (Band(score, valor))
            {
                case WearinessBand.Spent:    return "worn out by war — they long to lay down the sword";
                case WearinessBand.Weary:    return "war-weary, the years of blood telling on them";
                case WearinessBand.Seasoned: return "blooded and hardened, but still willing";
                default:                     return "fresh, untouched by the toll of war";
            }
        }
    }
}
