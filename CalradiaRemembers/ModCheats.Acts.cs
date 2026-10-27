// Code written by Gabriel Mailhot, 27/10/2026.
// The gestures a character loves and dislikes in bed (Gabriel, 26/10/2026), seen and tried from the console.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.GameState;
using CalradiaRemembers.Logic.Romance;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.acts [name]: the gestures a character loves, dislikes, and which of them the player has learned.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("acts", "cr")]
      public static string ListActs(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         Hero hero = ResolveHero(args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         Mapping.HeroProfileMapper.UpgradeIfNeeded(profile, hero);
         behavior.Store.Set(profile);
         RomanticProfile r = profile.Romantic;
         if (r == null) return $"{hero.Name} has no romantic profile.";

         string Ids(List<IntimateAct> acts) => acts == null || acts.Count == 0 ? "(none)" : string.Join(", ", acts.Select(IntimateActReading.IdOf));

         return $"{hero.Name}: loves {Ids(r.LovedActs)}; dislikes {Ids(r.DislikedActs)}; learned by the player {Ids(r.KnownActs)}. "
                + $"Said to the character at Hardcore only. Gestures: {string.Join(", ", IntimateActPolicy.All.Select(IntimateActReading.IdOf))}.";
      }

      /// <summary>
      ///   cr.act &lt;gesture&gt; [performed|proposed] [name]: plays a reported gesture through the real action path, as if the
      ///   reader had seen it in this exchange (Hardcore, intimacy permitted).
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("act", "cr")]
      public static string TryAct(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         if (args == null || args.Count == 0) return "Usage: cr.act <gesture> [performed|proposed] [name]. See cr.acts.";

         IntimateAct act = IntimateActReading.Parse(args[0]);
         if (act == IntimateAct.None) return $"'{args[0]}' is no gesture. See cr.acts.";
         List<string> rest = args.Skip(1).ToList();
         string done = rest.Count > 0 && (rest[0] == "performed" || rest[0] == "proposed") ? rest[0] : "performed";
         if (rest.Count > 0 && rest[0] == done) rest.RemoveAt(0);

         Hero hero = ResolveHero(rest);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";
         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         Mapping.HeroProfileMapper.UpgradeIfNeeded(profile, hero);
         int before = profile.ReputationWithPlayer;

         var action = new GameAction {Type = IntimateActReading.Verb, Parameters = new Dictionary<string, string> {["act"] = IntimateActReading.IdOf(act), ["done"] = done}};
         ActionOutcome outcome = Mapping.IntimateActApplier.Apply(action, hero, profile, behavior.GameStateBridge);
         behavior.Store.Set(profile);

         return $"{hero.Name}: '{IntimateActReading.IdOf(act)}' {done}: regard {before} -> {profile.ReputationWithPlayer}"
                + (outcome?.WasRefused == true ? " (nothing to judge: an ordinary gesture, below Hardcore, or intimacy not permitted)." : ".");
      }
   }
}
