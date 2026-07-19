// Code written by Gabriel Mailhot, 07/07/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Appearance;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.appearance [clear | &lt;prose...&gt;]
      ///   No args: shows the player's composed appearance block (derived facts + authored prose) as the NPC
      ///   would read it. "clear": wipes the authored prose. Anything else: sets the authored prose to that text.
      ///   This is the interim edit surface for the appearance pillar until the in-Encyclopedia field lands.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("appearance", "cr")]
      public static string SetAppearance(List<string> args)
      {
         Hero player = Hero.MainHero;
         if (player == null) return "No player hero (load a campaign first).";
         if (AppearanceProseBehavior.Instance == null) return "Appearance store not active (load a campaign first).";

         // Re-read colours fresh so the readout reflects the current character (e.g. after editing in the V menu).
         FaceColorReader.ClearCache();
         AppearanceFacts facts = AppearanceFactsExtractor.Extract(player);

         // Calibration on somebody else: "cr.appearance who <name>" reports the colour offsets the reader
         // derives for that NPC. Added 2026-07-19 because the model called Rhagaea's amber eyes green and
         // there was no way to tell a mis-classified offset from an invented colour without the number.
         if (args is {Count: >= 2} && args[0].Equals("who", StringComparison.OrdinalIgnoreCase))
         {
            Hero target = ResolveHero(args.Skip(1));

            if (target == null) return $"No hero found matching '{string.Join(" ", args.Skip(1))}'.";

            FaceColorReader.ClearCache();
            FaceColors tc = FaceColorReader.Read(target);
            string block = AppearanceComposer.Compose(
               AppearanceFactsExtractor.Extract(target),
               AppearanceProseBehavior.Instance.Get(target.StringId),
               AppearanceVoice.Subject);

            return $"{target.Name} appearance (as another sees them): \"{block}\"\n" +
                   $"Colour readout (calibration): hair={tc.Hair ?? "?"} (offset {tc.HairOffset:0.00}), " +
                   $"skin={tc.Skin ?? "?"} (offset {tc.SkinOffset:0.00}), eyes={tc.Eye ?? "?"} (offset {tc.EyeOffset:0.00}, banded).";
         }

         if (args == null || args.Count == 0)
         {
            string prose = AppearanceProseBehavior.Instance.Get(player.StringId);
            string composed = AppearanceComposer.Compose(facts, prose, AppearanceVoice.Subject);
            FaceColors c = FaceColorReader.Read(player);

            return $"Player appearance (as an NPC sees you): \"{composed}\"\n" +
                   $"Authored prose: \"{prose}\"\n" +
                   $"Colour readout (calibration): hair={c.Hair ?? "?"} (offset {c.HairOffset:0.00}), " +
                   $"skin={c.Skin ?? "?"} (offset {c.SkinOffset:0.00}), eyes={c.Eye ?? "?"} (offset {c.EyeOffset:0.00}, banded).\n" +
                   "Set prose with: cr.appearance <text>   |   clear with: cr.appearance clear";
         }

         if (args.Count == 1 && args[0].Equals("clear", StringComparison.OrdinalIgnoreCase))
         {
            AppearanceProseBehavior.Instance.Set(player.StringId, null);

            return "Player appearance prose cleared. The derived body facts still show.";
         }

         string stored = AppearanceProseBehavior.Instance.Set(player.StringId, string.Join(" ", args));
         string preview = AppearanceComposer.Compose(facts, stored, AppearanceVoice.Subject);

         return $"Player appearance prose set. Full block: \"{preview}\"";
      }
   }
}
