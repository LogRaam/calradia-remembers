// Code written by Gabriel Mailhot, 04/09/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic.WorldAlmanac
{
    /// <summary>How sharp a strategist's counsel is, deduced from their Tactics skill. Higher = keener judgment.</summary>
    public enum AdvisorTacticsTier
    {
        Green,
        Seasoned,
        Veteran,
        Expert,
        Master
    }

    /// <summary>
    ///   Turns an advisor's Tactics skill into the QUALITY of their strategic counsel: a higher skill sees further
    ///   and speaks with more precision, a lower one keeps it broad and hedged. Pure so the tiering and the guidance
    ///   text (which the prompt hands the model) are unit-tested rather than tuned by eye. Note: this scales the
    ///   advisor's JUDGMENT and foresight only, never the facts: a green tactician gives a vaguer OPINION, but the
    ///   numbers and events the prompt grounds them in stay exactly the same. The player's incentive is to keep a
    ///   high-Tactics companion for keener counsel.
    /// </summary>
    public static class TacticsAdvicePolicy
    {
        // Bannerlord skills run 0..300; a strong companion sits well above 150 and a true master near 275+. These
        // bands are the tunable dial for "how good does a strategist have to be to give expert counsel".
        public const int SeasonedFloor = 50;
        public const int VeteranFloor = 100;
        public const int ExpertFloor = 175;
        public const int MasterFloor = 250;

        /// <summary>The advisor's tier for a Tactics skill level (a negative or zero skill reads as Green).</summary>
        public static AdvisorTacticsTier TierFor(int tacticsSkill)
        {
            if (tacticsSkill >= MasterFloor) return AdvisorTacticsTier.Master;
            if (tacticsSkill >= ExpertFloor) return AdvisorTacticsTier.Expert;
            if (tacticsSkill >= VeteranFloor) return AdvisorTacticsTier.Veteran;
            if (tacticsSkill >= SeasonedFloor) return AdvisorTacticsTier.Seasoned;

            return AdvisorTacticsTier.Green;
        }

        /// <summary>
        ///   One sentence, addressed to the model, describing how this tier advises: it shapes the depth, confidence
        ///   and foresight of the generated counsel. Never says the advisor may be wrong about the FACTS, only about
        ///   their reading and predictions.
        /// </summary>
        public static string GuidanceFor(AdvisorTacticsTier tier)
        {
            switch (tier)
            {
                case AdvisorTacticsTier.Master:
                    return "You are a master strategist: anticipate what lesser minds miss, weigh second-order consequences, and counsel decisively.";
                case AdvisorTacticsTier.Expert:
                    return "You see several moves ahead: name specific threats and openings, and give confident, pointed counsel.";
                case AdvisorTacticsTier.Veteran:
                    return "You judge the balance of power well: point to concrete strengths, weaknesses and openings, without overreaching.";
                case AdvisorTacticsTier.Seasoned:
                    return "You read the field competently: give sound, plain counsel, even if it holds few surprises.";
                default:
                    return "You grasp only the broad strokes: keep your counsel general and admit plainly where you are unsure.";
            }
        }

        /// <summary>A short human label for the tier, for the persona line ("a veteran tactician").</summary>
        public static string LabelFor(AdvisorTacticsTier tier)
        {
            switch (tier)
            {
                case AdvisorTacticsTier.Master: return "a master tactician";
                case AdvisorTacticsTier.Expert: return "an expert tactician";
                case AdvisorTacticsTier.Veteran: return "a veteran tactician";
                case AdvisorTacticsTier.Seasoned: return "a seasoned tactician";
                default: return "a green tactician";
            }
        }

        /// <summary>Convenience: tier + its guidance for a raw skill in one call.</summary>
        public static string GuidanceFor(int tacticsSkill) => GuidanceFor(TierFor(tacticsSkill));
    }
}
