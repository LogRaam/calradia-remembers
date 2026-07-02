// Code written by Gabriel Mailhot, 01/07/2026.

#region

using System;

#endregion

namespace CalradiaRemembers.Logic
{
   /// <summary>
   ///   The pure avoid-repeat rule behind a captive scene's driving "focus": pick a random index into
   ///   a pool of flavour strings, but never the same index the previous round already used, so a
   ///   captor never opens two rounds in a row with the identical focus line. The strings themselves
   ///   stay content (mod-side, <c>CalradiaRemembers.Chat.CaptiveSceneText</c>); this is only the
   ///   selection rule.
   /// </summary>
   public static class SceneFocusSelector
   {
      /// <summary>
      ///   Draws an index in [0, poolSize) that is never equal to <paramref name="lastIndex" />, as long
      ///   as the pool has more than one entry. A negative <paramref name="lastIndex" /> (no prior
      ///   round) or a pool of exactly one entry allows any draw. <paramref name="roll" /> is called
      ///   with <paramref name="poolSize" /> (or <paramref name="poolSize" /> - 1 when re-rolling to
      ///   skip the repeat) and is expected to return a 0..(n-1) draw.
      /// </summary>
      public static int PickIndex(int poolSize, int lastIndex, Func<int, int> roll)
      {
         if (poolSize <= 1) return 0;
         if (lastIndex < 0 || lastIndex >= poolSize) return roll(poolSize);

         // Draw among the (poolSize - 1) indices other than lastIndex, then shift past it.
         int candidate = roll(poolSize - 1);

         return candidate < lastIndex ? candidate : candidate + 1;
      }
   }
}
