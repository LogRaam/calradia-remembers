// Code written by Gabriel Mailhot, 27/06/2026.
// "Stance with teeth": turns the multi-axis posture (StanceDescriptor reads it for the prompt; this reads it
// for ACTION) into at most one warranted consequence, so what the player earns through deeds actually bites.
// Pure and tunable — the thresholds and the trait gates that decide WHO dares to do WHAT stay protected by tests.
// The host fires the consequence through existing systems (letters, schemes, interception, an ambush scene).
//
// Who does what, when hatred runs deep: an honourable lord will not stoop to murder or a scheme — he rides out
// to challenge you openly (or just sends a cold word). A cold, cruel schemer has you killed; a cold schemer who
// still has a scruple plots instead. And no one dares strike at you in person while they are terrified of you —
// deep fear pushes a hateful lord toward the indirect (a plot) or merely a threat.

namespace CalradiaRemembers.Logic
{
    /// <summary>The one consequence an NPC's posture warrants this evaluation, or <see cref="None" />.</summary>
    public enum StanceConsequence
    {
        /// <summary>Neutral — nothing is warranted.</summary>
        None,

        // ── Hostile, mild → lethal ──
        /// <summary>Curt and unwilling to deal; colours the next conversation.</summary>
        ColdShoulder,

        /// <summary>A cold or threatening letter arrives.</summary>
        Threat,

        /// <summary>Quietly funds/launches a scheme against the player (a cold schemer's path).</summary>
        Plot,

        /// <summary>Rides out to challenge the player in the field (a bold lord's path).</summary>
        Intercept,

        /// <summary>Has the player struck down — an ambush in town (the rarest, coldest, cruellest path).</summary>
        Assassinate,

        // ── Friendly, mild → strong ──
        /// <summary>Genuinely glad to see the player; colours the next conversation.</summary>
        WarmWelcome,

        /// <summary>Opens a door — a gift, a reduced price, an unsolicited boon.</summary>
        Favor,

        /// <summary>Tips the player off to a plot against them (a trusted friend's path).</summary>
        Warning
    }

    /// <summary>Reads an <see cref="NpcStance" /> (plus the NPC's traits) into the one consequence it warrants.</summary>
    public static class StanceConsequencePolicy
    {
        // ── Hostile thresholds (Affection is bipolar −100..+100) ──
        private const int HostileFloor = -20; // below this, the hostile ladder applies at all
        private const int ThreatFloor = -40;  // a threatening letter rather than a mere cold shoulder
        private const int LethalHatred = -65; // deep enough to consider acting against the player in earnest

        // A lord too terrified of the player will not strike at them in person (intercept/assassinate);
        // his hatred turns indirect (a plot) or to mere words.
        private const int BoldFearCeiling = 60;

        // ── Friendly thresholds ──
        private const int FriendlyFloor = 20; // above this, the friendly ladder applies
        private const int FavorFloor = 40;    // a real boon rather than a warm word
        private const int FavorRespect = 30;  // a boon also wants genuine esteem, not just fondness
        private const int WarningTrust = 40;  // tipping you off to a plot takes deep faith in your word
        private const int WarmWelcomeTrustFloor = -40; // M-S1: deep distrust undercuts a warm welcome

        /// <summary>
        ///   The single consequence this posture warrants, or <see cref="StanceConsequence.None" />. Traits are
        ///   the authored −2..+2 scale (honour, valour, mercy, calculating) and decide WHO dares WHAT.
        /// </summary>
        /// <summary>
        ///   Named rather than positional (2026-09-10): this took FOUR INTS in a row, honor, valor, mercy and
        ///   calculating, and nothing but their order told them apart. It does not invent a facts type either:
        ///   Appreciations.TraitFacts already carries exactly these five trait values and is already built from
        ///   the engine by GrudgeBridge.BuildTraitFacts, so the realignment is to ASK FOR THE TYPE THAT EXISTS
        ///   rather than to unpack it into loose numbers at every call site.
        /// </summary>
        public static StanceConsequence Resolve(NpcStance stance, Appreciations.TraitFacts traits)
        {
            if (stance == null) return StanceConsequence.None;

            traits ??= new Appreciations.TraitFacts();
            int honor = traits.Honor;
            int valor = traits.Valor;
            int mercy = traits.Mercy;
            int calculating = traits.Calculating;

            int affection = stance.Affection;

            if (affection <= HostileFloor) return ResolveHostile(stance, affection, honor, valor, mercy, calculating);
            if (affection >= FriendlyFloor) return ResolveFriendly(stance, affection);

            return StanceConsequence.None;
        }

        private static StanceConsequence ResolveHostile(NpcStance stance, int affection,
            int honor, int valor, int mercy, int calculating)
        {
            if (affection <= LethalHatred)
            {
                bool undeterred = stance.Fear < BoldFearCeiling; // not too afraid to act in person
                bool dishonourable = honor <= 0;                 // an honourable lord won't murder or scheme

                if (dishonourable && calculating >= 1 && mercy <= -1 && undeterred) return StanceConsequence.Assassinate;
                if (dishonourable && calculating >= 1) return StanceConsequence.Plot;
                if (valor >= 1 && undeterred) return StanceConsequence.Intercept;
                // deep hatred but neither schemer nor bold (nor dares): it still comes as a threat.
            }

            return affection <= ThreatFloor
                ? StanceConsequence.Threat
                : StanceConsequence.ColdShoulder;
        }

        private static StanceConsequence ResolveFriendly(NpcStance stance, int affection)
        {
            if (affection >= FavorFloor)
            {
                if (stance.Trust >= WarningTrust) return StanceConsequence.Warning; // a trusted friend tips you off
                if (stance.Respect >= FavorRespect) return StanceConsequence.Favor;
            }

            // M-S1 (regard audit): fondness alone does not warrant a WARM welcome when trust has collapsed. A
            // "genuinely glad to see you" hint printed alongside a "you deeply distrust the player" axis line read
            // as the perception system contradicting itself, with nothing arbitrating the two. Deep distrust
            // withholds the warm hint; the axis lines still speak for themselves.
            if (stance.Trust <= WarmWelcomeTrustFloor) return StanceConsequence.None;

            return StanceConsequence.WarmWelcome;
        }
    }
}
