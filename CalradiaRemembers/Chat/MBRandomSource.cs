// Code written by Gabriel Mailhot, 20/06/2026.

#region

using CalradiaRemembers.Logic;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   Engine-backed <see cref="IRandomSource" /> for the mod: the real game RNG. The pure logic in
   ///   CalradiaRemembers.Logic depends only on the interface, so tests inject a deterministic source.
   /// </summary>
   public sealed class MBRandomSource : IRandomSource
   {
      public static readonly MBRandomSource Instance = new();

      public int Next(int maxExclusive) => MBRandom.RandomInt(maxExclusive);
   }
}