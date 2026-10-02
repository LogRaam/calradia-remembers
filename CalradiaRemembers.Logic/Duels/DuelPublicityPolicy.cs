// Code written by Gabriel Mailhot, 02/10/2026.
// fkasad (Nexus, 01/10/2026), a duel with the head of a faction inside his own keep: "[DUEL] armed from chat ... 0
// witness(es). -0 witness(es)??? There was like 15 chaos lords inside the castle." and "[WORLD] recorded Affront (... mag
// 6 ...): Amfildor bested Pyucius the Dung Tongued in a duel of honour. -mag 6 for an enemy lord when defeating a dozen
// looters is 40???"
//
// Two faults. The duel counted as its audience the witnesses of the CONVERSATION, which are only lords the player already
// has a CR profile with, capped at four and thinned by the witness-density setting: right for who speaks in a room,
// wrong for who saw a duel. And the duel's world event had a fixed weight of 6, while any battle the player fights
// against a lord weighs at least 40. The audience is now whoever is truly present (the larger of the two counts), and
// the weight follows who was fought and before how many. The count weighed is capped, since it also feeds the renown won
// and the influence the loser's house pays (two points each per witness), and a keep full of lords must not make one
// duel worth more than a war.

namespace CalradiaRemembers.Logic.Duels
{
    /// <summary>Who saw a duel, and how far word of it travels. Pure.</summary>
    public static class DuelPublicityPolicy
    {
        /// <summary>The most witnesses a duel's renown, influence and news are weighed on: past ten, a crowd is a crowd.</summary>
        public const int MaxWitnessesWeighed = 10;

        /// <summary>How far a duel with a man of no house travels: real news, a little.</summary>
        public const int BaseMagnitude = 15;

        /// <summary>What each step of the opponent's station adds (established, great house, ruler).</summary>
        public const int MagnitudePerStation = 10;

        /// <summary>What each witness weighed adds: a duel before a court travels further.</summary>
        public const int MagnitudePerWitness = 2;

        /// <summary>The audience weighed: whoever was truly there (the conversation's witnesses or those present in the place), capped.</summary>
        public static int Audience(int conversationWitnesses, int presentInPlace)
        {
            int seen = conversationWitnesses > presentInPlace ? conversationWitnesses : presentInPlace;

            return seen < 0 ? 0 : seen > MaxWitnessesWeighed ? MaxWitnessesWeighed : seen;
        }

        /// <summary>
        ///   How far word of the duel travels: the opponent's station (0 no house of note .. 3 ruler) and the audience;
        ///   a duel refused travels half as far as one fought.
        /// </summary>
        public static int Magnitude(int opponentStation, int audience, bool fought)
        {
            int station = opponentStation < 0 ? 0 : opponentStation > 3 ? 3 : opponentStation;
            int seen = audience < 0 ? 0 : audience > MaxWitnessesWeighed ? MaxWitnessesWeighed : audience;
            int m = BaseMagnitude + station * MagnitudePerStation + seen * MagnitudePerWitness;

            return fought ? m : m / 2;
        }
    }
}
