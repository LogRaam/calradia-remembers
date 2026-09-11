// Code written by Gabriel Mailhot, 10/09/2026.
// Player report (Nexus, 2026-09-10): "It feels like the AI using this like never follows me despite it agreeing
// to :( or send troops to X location.. it never does :("
//
// The first half was ours twice over. follow_me was gated on factionAtWar, and in an ordinary campaign nearly
// every kingdom is at war with somebody nearly all the time, so the verb was almost never taught; and a verb that
// is not taught is ABSENT rather than refused, which a model reads as "nobody mentioned it" and not as
// "forbidden", so the lord agreed warmly and nothing happened. See EscortEligibilityPolicy for that half.
//
// This file is the other half, and it is Gabriel's own condition on relaxing the gate (2026-09-10): "si il
// accepte de suivre le joueur et apres il y a un appel pour joindre une armee, ce Lord devrait expliquer au
// joueur qu'il doit le quitter pour suivre l'appel de son seigneur."
//
// He is right, and it is what makes the relaxation honest rather than merely permissive. A lord may now ride with
// a player while his realm is at war, so he must be able to leave when that war actually calls him, and leaving
// without a word would be a worse version of the very complaint above: the player would once again watch a
// promise evaporate with no explanation.
//
// So an escort no longer ends on a bool. It ends for a NAMED reason, and every reason has a sentence. Until today
// all four ways an escort could end were silent to the player, visible only in a log nobody reads: the term
// running out, the lord dying, the lord being taken prisoner, and war breaking out between the two realms.

namespace CalradiaRemembers.Logic.Escort
{
    /// <summary>Why an escort ended, in the order the checks are made.</summary>
    public enum EscortEndReason
    {
        /// <summary>It has not ended. The lord rides on.</summary>
        None,

        /// <summary>The agreed term ran out, which is the ordinary and expected ending.</summary>
        TermExpired,

        /// <summary>The lord is dead.</summary>
        LordDead,

        /// <summary>The lord has been taken prisoner and is in no position to escort anyone.</summary>
        LordCaptured,

        /// <summary>War broke out between the lord's realm and the player's, which no promise survives.</summary>
        WarWithPlayer,

        /// <summary>
        ///   The lord's own war called him: he has joined an army, begun a siege, or been drawn into a battle.
        ///   The reason this enum exists, and the one Gabriel required be spoken aloud.
        /// </summary>
        SummonedByLiege
    }

    /// <summary>The facts an ending rests on, read off the live campaign by the caller.</summary>
    public sealed class EscortStandingFacts
    {
        /// <summary>Days still to run on the agreed term, already decremented for today.</summary>
        public int DaysRemaining { get; set; }

        /// <summary>The lord is no longer among the living, or can no longer be found at all.</summary>
        public bool IsDead { get; set; }

        /// <summary>The lord is somebody's prisoner.</summary>
        public bool IsCaptured { get; set; }

        /// <summary>The lord's realm and the player's are now at war with each other.</summary>
        public bool IsAtWarWithPlayer { get; set; }

        /// <summary>
        ///   The lord's own war has claimed him: his party has joined an army, laid a siege, or been drawn into a
        ///   battle. Not merely "his realm is at war", which no longer ends anything by itself, exactly as it no
        ///   longer prevents anything by itself.
        /// </summary>
        public bool SummonedToWar { get; set; }
    }

    /// <summary>Whether an escort ends today, why, and what the lord says about it.</summary>
    public static class EscortContinuationPolicy
    {
        /// <summary>
        ///   The reason this escort ends, or <see cref="EscortEndReason.None" />. Order matters only in that the
        ///   gravest facts are read first: a dead lord is not "summoned", and a captured one is not "out of time".
        /// </summary>
        public static EscortEndReason Resolve(EscortStandingFacts facts)
        {
            if (facts == null) return EscortEndReason.LordDead; // unknowable is treated as gone, never as riding on

            if (facts.IsDead) return EscortEndReason.LordDead;
            if (facts.IsCaptured) return EscortEndReason.LordCaptured;
            if (facts.IsAtWarWithPlayer) return EscortEndReason.WarWithPlayer;
            if (facts.SummonedToWar) return EscortEndReason.SummonedByLiege;

            return facts.DaysRemaining <= 0 ? EscortEndReason.TermExpired : EscortEndReason.None;
        }

        /// <summary>True when the escort is over, whatever the reason.</summary>
        public static bool ShouldEnd(EscortStandingFacts facts) => Resolve(facts) != EscortEndReason.None;

        /// <summary>
        ///   What the player is told, in the lord's own voice where a voice makes sense. Every ending gets a
        ///   sentence: an escort that simply stops is the complaint this whole change answers, and the term
        ///   running out quietly is the same silence as any other.
        ///   <para>
        ///     Empty when nothing has ended. A missing name still reads as a sentence, since these are built from
        ///     live campaign reads that can be null mid-transition.
        ///   </para>
        /// </summary>
        public static string Describe(EscortEndReason reason, string lordName)
        {
            string who = string.IsNullOrWhiteSpace(lordName) ? "Your escort" : lordName.Trim();

            switch (reason)
            {
                case EscortEndReason.SummonedByLiege:
                    return $"{who} breaks off: his liege has called the muster, and he must answer it. He rides for the war.";
                case EscortEndReason.WarWithPlayer:
                    return $"{who} turns away. There is war between your realms now, and no promise outlives that.";
                case EscortEndReason.LordCaptured:
                    return $"{who} has been taken prisoner and rides with you no longer.";
                case EscortEndReason.LordDead:
                    return $"{who} rides with you no longer.";
                case EscortEndReason.TermExpired:
                    return $"{who} has ridden with you as long as he agreed to, and returns to his own business.";
                default:
                    return "";
            }
        }
    }
}
