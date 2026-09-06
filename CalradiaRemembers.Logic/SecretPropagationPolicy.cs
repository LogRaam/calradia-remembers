// Code written by Gabriel Mailhot, 05/09/2026.
// Mystery-lover pillar (design phase 3b-2): a lord who KNOWS the player's secret-lover identity (learned only
// because the player confessed it) may, over time, let it slip to his own FRIENDS, so the secret spreads gradually
// rather than staying frozen with the one person who was told. This pure policy holds the numeric contract of that
// spread: who counts as a friend close enough to be told, and how likely a slip is on a given day. The engine owns
// the rest (a true friend of the PLAYER guards it and never spreads, decided by HeldSecretPolicy; picking an actual
// friend; the random roll), so the spread stays a slow trickle, not a broadcast.

namespace CalradiaRemembers.Logic
{
   /// <summary>The numeric contract for how a held secret-lover identity trickles from a knowing lord to his friends.</summary>
   public static class SecretPropagationPolicy
   {
      /// <summary>The relation a hero needs toward the teller to count as a friend close enough to be told. Tuning.</summary>
      public const int FriendRelationFloor = 20;

      /// <summary>The per-day chance a knowing lord (who is not the player's own confidant) lets the secret slip to one friend. Tuning: low, so it stays a trickle.</summary>
      public const int DailySpreadChancePercent = 8;

      /// <summary>True when this relation is warm enough that the teller would confide the secret to that hero.</summary>
      public static bool IsFriend(int relation) => relation >= FriendRelationFloor;
   }
}
