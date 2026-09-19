// Code written by Gabriel Mailhot, 18/09/2026.
// The clause that tells the player what an NPC wants to talk about, before they decide whether to listen.
//
// fkasad (Nexus, 18/09/2026): "The invitation to talk should specify the topic the NPC wants to discuss
// (that's how it works in real life)."
//
// His whole complaint turns on this one sentence, and it is worth reading the rest of his report through it:
//
//   "Maybe one in ten NPCs has something interesting to say, but unfortunately, I'm forced to decline the
//    conversation because of the other nine. For me, running a local LLM costs only GPU; for others, it costs
//    money. Either way, it's wasted playtime."
//
// He is not describing a feature he dislikes. He is describing a bet he cannot win. Accepting blind is nine
// platitudes for one good scene, at the price of a model call each, so declining every time is the rational
// play, and it throws the good one away with the rest. Naming the subject is not a courtesy: it is what turns
// a gamble back into a choice, and it costs nothing, needs no model, and was already in hand at the point the
// invitation is raised.

namespace CalradiaRemembers.Logic.Interception
{
   /// <summary>The " It seems to be about X." tail of an audience invitation, or nothing when there is no subject to name.</summary>
   public static class AudienceTopicLine
   {
      /// <summary>How much of a matter's summary an inquiry box can carry before it needs cutting.</summary>
      public const int MaxTopicChars = 110;

      /// <summary>
      ///   The clause to append to the invitation body. Empty when <paramref name="detail" /> says nothing,
      ///   because an invitation that promises a subject and then fails to name one is worse than one that
      ///   never promised: the player learns the clause is noise and stops reading it.
      /// </summary>
      public static string For(string detail)
      {
         // The summary is a sentence and usually ends in a full stop; the clause supplies its own, so the stop
         // comes off FIRST. Taking it off LAST ate the ellipsis of a cut summary and left one lonely dot, which
         // is what the test caught: the order of these three steps is the whole of the formatting.
         string topic = (detail ?? "").Replace('\r', ' ').Replace('\n', ' ').Trim().TrimEnd('.').TrimEnd();

         if (topic.Length == 0) return "";

         // A cut summary ends in the ellipsis and nothing after it: "about the siege of..." is how a sentence
         // trails off, and "about the siege of...." is how a bug looks.
         if (topic.Length > MaxTopicChars)
            return " It seems to be about " + topic.Substring(0, MaxTopicChars).TrimEnd() + "...";

         return " It seems to be about " + topic + ".";
      }
   }
}
