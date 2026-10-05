// Code written by Gabriel Mailhot, 05/10/2026.
// How long a background model call may run: a letter, a conversation summary, a courier's secret, a letter's memory.
// CarolusIV (Nexus, 05/10/2026): letters timed out on his slow local model while his MCM "LLM Response Timeout" stood at
// 180; each of these calls carried its own hard-coded 60 seconds and never read the setting. They honour it now, and
// never wait less than the minute they always had.

namespace CalradiaRemembers.Logic
{
   /// <summary>The timeout of a call nobody waits on in a conversation. Pure.</summary>
   public static class BackgroundCallTimeout
   {
      /// <summary>What every background call had before it read the player's setting.</summary>
      public const int FloorSeconds = 60;

      public static int Seconds(int playerTimeoutSeconds) => playerTimeoutSeconds > FloorSeconds ? playerTimeoutSeconds : FloorSeconds;
   }
}
