// Code written by Gabriel Mailhot, 06/09/2026.
// Battle-memory enrichment (Gabriel, 2026-09-06): today a battle leaves every NPC present the SAME generic line
// ("Fought at your side when we broke the enemy"). It should leave real facts: whom the player bested, who put the
// player down, and what it was worth. This file is the engine-agnostic vocabulary for one such deed. The mod's
// mission journal fills these from the real arena/battlefield (who removed whom), and the policy beside it rates
// how remarkable the deed actually was. Nothing here touches the game.
//
// TRUTH RULE (Gabriel, ratified): a LORD is "bested", "put down", or "taken alive", NEVER "killed", unless the
// engine really did kill them, because Bannerlord knocks heroes out far more often than it kills them and a mod
// built on memory must not have lords mourning men who are alive and well. Rank-and-file soldiers do die, and may
// be spoken of as dead.

namespace CalradiaRemembers.Logic.Battles
{
    /// <summary>What actually became of a foe the player put out of the fight.</summary>
    public enum CombatFeatOutcome
    {
        /// <summary>Beaten out of the fight and left on the field: the honest default for a hero, who is almost never truly dead.</summary>
        Bested,

        /// <summary>Beaten and taken captive: a verifiable fact the player can check in their own prisoner roster.</summary>
        TakenAlive,

        /// <summary>Truly dead. Only ever set when the engine actually killed them.</summary>
        Slain
    }

    /// <summary>How remarkable a deed is, which decides how far word of it may travel.</summary>
    public enum CombatFeatTier
    {
        /// <summary>Beating someone far beneath you. Nobody remarks on it, and nothing is recorded.</summary>
        Unremarkable,

        /// <summary>A real deed, worth remembering by those who SAW it, but not worth the realm's gossip.</summary>
        Notable,

        /// <summary>An exploit: you bested someone well above you, or a name the realm knows. This is what may circulate as world gossip (Gabriel's rule).</summary>
        Exploit
    }

    /// <summary>The fighting facts of one combatant, as the game itself knows them; the policy turns these into a prowess score.</summary>
    public sealed class CombatantFacts
    {
        public CombatantFacts(int bestWeaponSkill, int riding, int athletics, int level, int clanTier)
        {
            BestWeaponSkill = bestWeaponSkill;
            Riding = riding;
            Athletics = athletics;
            Level = level;
            ClanTier = clanTier;
        }

        /// <summary>The highest of their weapon skills: what they actually fight with.</summary>
        public int BestWeaponSkill { get; }

        public int Riding { get; }
        public int Athletics { get; }
        public int Level { get; }

        /// <summary>
        ///   Their clan's tier, the FAME axis. Bannerlord keeps renown on the clan, not the hero, so this is the
        ///   game's own measure of how known a name is. Beating a famous champion is a story whatever the skills
        ///   say, which is why fame is weighed apart from prowess.
        /// </summary>
        public int ClanTier { get; }
    }

    /// <summary>One thing the player did to one named foe in one engagement.</summary>
    public sealed class CombatFeat
    {
        public CombatFeat(string foeId, string foeName, CombatFeatOutcome outcome, CombatFeatTier tier)
        {
            FoeId = foeId ?? "";
            FoeName = foeName ?? "";
            Outcome = outcome;
            Tier = tier;
        }

        public string FoeId { get; }

        /// <summary>The foe named through HeroLabel upstream, so a namesake is never confused for another.</summary>
        public string FoeName { get; }

        public CombatFeatOutcome Outcome { get; }
        public CombatFeatTier Tier { get; }
    }
}
