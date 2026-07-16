// Code written by Gabriel Mailhot, 14/07/2026.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Duels;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.duel_here [name]
      ///   WILDERNESS SPIKE. Stand on the open map (not inside a settlement) and run this to open a throwaway
      ///   field-combat scene against the named lord (or the conversation NPC, or any nearby lord), spawn the
      ///   two fighters, hold it for about half a minute, and return to the map. It measures VRAM before and
      ///   after in the log under [DUELSPIKE], to prove whether a field scene opens and closes cleanly with no
      ///   render-target leak. This is plumbing only: the real non-lethal duel (witnesses, stakes, the
      ///   challenge_duel action) is the rest of Inc 1, not this.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("duel_here", "cr")]
      public static string ForceDuelHere(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior _, out string error)) return error;

         if (Settlement.CurrentSettlement != null)
            return "You are inside a settlement. The town path is already covered by the ambush code; " +
                   "leave to the open map to test the WILDERNESS scene.";

         Hero opponent = ResolveHero(args)
                      ?? Hero.AllAliveHeroes.FirstOrDefault(h => h != Hero.MainHero && h.IsLord && h.IsAlive && !h.IsPrisoner);

         return DuelSceneSpike.OpenFieldDuel(opponent);
      }
   }
}
