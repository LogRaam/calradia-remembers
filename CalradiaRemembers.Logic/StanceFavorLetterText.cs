// Code written by Gabriel Mailhot, 30/10/2026.
// The warm letter a lord sends unbidden once his regard for the player has hardened into friendship (the Favor rung of
// StanceConsequencePolicy, and a Warning with no real plot to name). Until now it was ONE fixed text for every lord:
// tashmetu (Nexus, 30/10/2026) relayed two players, both with Bellum Civile's collection (which raises relations on a
// tournament win), receiving "a crowd of lords writing about a tournament he won three years ago as if it made them
// lifelong friends", the same words from each. Gabriel (30/10/2026): "textes fixes, pas de delais ni de couts", so the
// letter stays a template (no model call) but rotates: by how many such letters this lord already sent (his own letters
// never repeat back to back), offset by a stable reading of who he is (two lords writing the same week rarely match).
// Every text stays general on purpose: NPCs never lie, so none names a deed, a battle or a feast the lord never saw.

namespace CalradiaRemembers.Logic
{
    /// <summary>The fixed texts of a lord's unbidden friendly letter, and which one a given lord sends next. Pure.</summary>
    public static class StanceFavorLetterText
    {
        /// <summary>The texts, each a different voice of the same goodwill: open, reserved, practical, plain.</summary>
        public static readonly string[] Letters =
        {
            "I count you a friend, and I am not shy to say so. Should you ever have need of me, a sword, a coin, " +
            "a word in the right ear, you have only to ask. And were anything to move against you that reached " +
            "my ear, you would hear of it from me first.",
            "I have no gift for long letters, so I will be brief. I think well of you, and I would have you know it. " +
            "If the road ever turns hard for you, send word, and I will see what I can do.",
            "There are few in this realm whose word I would take without a second look, and you are one of them. " +
            "Keep that in mind, should you ever need a friend who does not ask too many questions.",
            "It is a rare thing to find someone worth trusting, and rarer still to say so in writing. I say it now: " +
            "my door is open to you, and my help with it, should you have need.",
            "I write only so that you know where I stand. I hold you in good regard, and that is not a thing I give " +
            "lightly. If ever you hear that some harm is meant you, ask me: I may know more than I say aloud.",
            "Friendship is easy to claim and hard to prove, so I will not dress this up. If you are ever short of men, " +
            "of silver or of someone to speak for you, think of me before you think of others."
        };

        /// <summary>
        ///   The text this lord sends now. <paramref name="sentBefore" /> is how many friendly letters he has already sent the
        ///   player: each new one moves to the next text. <paramref name="senderId" /> shifts where his own sequence starts.
        /// </summary>
        public static string For(string senderId, int sentBefore)
        {
            int start = (int)(Hashing.StableHash.Of(senderId) % (uint)Letters.Length);
            int step = sentBefore < 0 ? 0 : sentBefore;

            return Letters[(start + step) % Letters.Length];
        }
    }
}
