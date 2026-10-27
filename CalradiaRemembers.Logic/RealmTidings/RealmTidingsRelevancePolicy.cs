// Code written by Gabriel Mailhot, 27/10/2026.
// DepaHN (Nexus, 26/10/2026): "I'm in the south, and a lord in the north whom I've never met keeps writing to me because
// his relationship with me somehow improved. He tells me about rebellions and wars in the north that have nothing to do
// with me [...] At the very least, the player would then get something useful from them: news about their own circle,
// their kingdom's affairs, war and peace, hunger and prosperity." A writer needed only warm regard (regard can rise for
// a man never met, through the player's deeds or fame), and the news was chosen by what the WRITER knows and cares for.
// Now only someone the player has met writes, and only of what touches the player: their realm, their kin, the country
// around them, or the writer's own house, which is how an acquaintance grows. Gabriel: "oui fait les 3 correctifs".

namespace CalradiaRemembers.Logic.RealmTidings
{
   /// <summary>Who would write the player news of the realm.</summary>
   public sealed class TidingsWriterFacts
   {
      /// <summary>The player and the writer have met.</summary>
      public bool HasMetThePlayer { get; init; }

      /// <summary>The writer holds the player in warm regard.</summary>
      public bool HoldsThePlayerWarmly { get; init; }
   }

   /// <summary>How a piece of news touches the player.</summary>
   public sealed class TidingsEventFacts
   {
      /// <summary>It concerns the player's own realm.</summary>
      public bool InThePlayersRealm { get; init; }

      /// <summary>It concerns one of the player's kin.</summary>
      public bool InvolvesThePlayersKin { get; init; }

      /// <summary>It happened in the country around the player.</summary>
      public bool NearThePlayer { get; init; }

      /// <summary>It concerns the writer or the writer's own house: news of a friend's own life.</summary>
      public bool AboutTheWritersHouse { get; init; }
   }

   /// <summary>Who may write the player news, and which news is worth sending them.</summary>
   public static class RealmTidingsRelevancePolicy
   {
      /// <summary>Only someone the player has met, and who holds them warmly, writes them news unbidden.</summary>
      public static bool MayWrite(TidingsWriterFacts f) => f != null && f.HasMetThePlayer && f.HoldsThePlayerWarmly;

      /// <summary>News is worth sending when it touches the player, or is the writer's own: anything else is a distant bulletin.</summary>
      public static bool TouchesThePlayer(TidingsEventFacts f)
         => f != null && (f.InThePlayersRealm || f.InvolvesThePlayersKin || f.NearThePlayer || f.AboutTheWritersHouse);
   }
}
