// Code written by Gabriel Mailhot, 21/07/2026.
// Perception layer (new pillar, foundation for a later stripping mechanic): classifies how the PLAYER is
// currently dressed from flat, engine-agnostic facts, so an NPC can perceive and remark on it. This project
// has no TaleWorlds reference by design, so the mod-side PlayerDressResolver decides what "covered" and
// "armour" mean against the two real Bannerlord equipment sets (BattleEquipment/CivilianEquipment); this
// policy only turns those already-reduced facts into one of four perceptible bands.

namespace CalradiaRemembers.Logic
{
    /// <summary>The band an onlooker would perceive at a glance, from wholly bare to genuine battle harness.</summary>
    public enum PlayerDressBand
    {
        Naked,
        Rags,
        Civilian,
        Armoured
    }

    /// <summary>
    ///   Pure classification of the player's state of dress. Consumed by the mod's EncounterContextBuilder to
    ///   feed <c>EncounterContext.PlayerDress</c>, which PromptBuilder renders as a short, factual note only
    ///   for a remarkable band (Naked/Rags); see that project for the render-worthiness gate.
    /// </summary>
    public static class PlayerDressPolicy
    {
        /// <summary>
        ///   The highest civilian garment tier that still reads as poor/ragged rather than merely plain:
        ///   Bannerlord's cheapest civilian tunics and trousers sit at Tier1 (int value 0); anything above
        ///   that is at least presentable townswear. Pinned by PlayerDressPolicyTests.
        /// </summary>
        public const int RagsMaxTier = 0;

        /// <summary>
        ///   Classifies the player's state of dress from flat facts already reduced by the mod-side reader.
        ///   Never throws: every input is a primitive compared, never indexed or dereferenced.
        /// </summary>
        /// <param name="isBodyOrLegCovered">
        ///   True when ANY garment (civilian clothing or battle armour) is worn on the torso or legs, in
        ///   EITHER of the two equipment sets a Bannerlord hero keeps (battle and civilian). False only when
        ///   both sets are genuinely empty there: the one case that reads as Naked. Real nudity must be
        ///   reachable, so this must not be satisfied by "no armour" alone while clothing remains worn: it is
        ///   the mod reader's job to check both sets before calling this false.
        /// </param>
        /// <param name="isWearingBattleArmor">
        ///   True when at least one of the covering pieces is genuine battle protection (non-civilian) rather
        ///   than ordinary clothing. Ignored when <paramref name="isBodyOrLegCovered" /> is false: a
        ///   contradictory true here can never override a report of nothing worn.
        /// </param>
        /// <param name="garmentTier">
        ///   The highest tier among the covering CIVILIAN pieces (any value when none is worn or none is
        ///   civilian; it is then never consulted). Only decides the Rags/Civilian split. Out-of-range or
        ///   negative values are simply compared, never thrown on.
        /// </param>
        public static PlayerDressBand Classify(bool isBodyOrLegCovered, bool isWearingBattleArmor, int garmentTier)
        {
            if (!isBodyOrLegCovered) return PlayerDressBand.Naked;
            if (isWearingBattleArmor) return PlayerDressBand.Armoured;

            return garmentTier <= RagsMaxTier
                ? PlayerDressBand.Rags
                : PlayerDressBand.Civilian;
        }
    }
}
