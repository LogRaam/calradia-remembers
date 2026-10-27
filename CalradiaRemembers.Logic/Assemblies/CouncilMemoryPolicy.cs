// Code written by Gabriel Mailhot, 27/10/2026.
// DepaHN (Nexus, 26/10/2026): "the council is completely cut off from memory. What is said in a council meeting stays in
// the meeting, and the characters remember nothing of it afterward, as if it never happened." Only the lord the player
// addressed kept a summary of the sitting; every other member seated kept "was present while the player spoke with X",
// with nothing of what was said or decided. Now each one seated keeps the sitting, summarised in their own voice, as the
// anchor does. Gabriel: "oui fait les 3 correctifs". LapitaPolata (Nexus, 27/10/2026): a witness invited into another
// conversation, who spoke in it, came back remembering only "witnessed the conversation"; a witness who SPOKE now keeps
// the conversation too (Gabriel: "go"). A silent onlooker keeps the plain line, which costs no summary.

namespace CalradiaRemembers.Logic.Assemblies
{
   /// <summary>What a character present at a conversation keeps of it, beside the one the player addressed.</summary>
   public enum WitnessKeeps
   {
      /// <summary>Nothing: not someone with a memory to hold it, or the one addressed (kept by the conversation's own memory).</summary>
      Nothing,

      /// <summary>A silent onlooker to an ordinary conversation: that they were there.</summary>
      ThatTheyWerePresent,

      /// <summary>A witness who spoke in an ordinary conversation: the conversation, in their own voice.</summary>
      TheConversation,

      /// <summary>A member seated at a council: the sitting itself, what was said and decided, in their own voice.</summary>
      TheSitting
   }

   /// <summary>The facts of one character present at a conversation's close.</summary>
   public sealed class WitnessFacts
   {
      /// <summary>The conversation was a council sitting.</summary>
      public bool InCouncil { get; init; }

      /// <summary>The character has a memory to hold it (a profile).</summary>
      public bool HasMemory { get; init; }

      /// <summary>The character is the one the player addressed.</summary>
      public bool IsTheOneAddressed { get; init; }

      /// <summary>The character spoke in the conversation (a line of theirs is in it).</summary>
      public bool SpokeInIt { get; init; }
   }

   /// <summary>What each character present keeps of a conversation.</summary>
   public static class CouncilMemoryPolicy
   {
      /// <summary>The sitting for a council member, the conversation for a witness who spoke, presence for a silent onlooker.</summary>
      public static WitnessKeeps Keeps(WitnessFacts f)
      {
         if (f == null || !f.HasMemory || f.IsTheOneAddressed) return WitnessKeeps.Nothing;

         if (f.InCouncil) return WitnessKeeps.TheSitting;

         return f.SpokeInIt ? WitnessKeeps.TheConversation : WitnessKeeps.ThatTheyWerePresent;
      }
   }
}
