// Code written by Gabriel Mailhot, 27/10/2026.
// What matters most to a character (Gabriel, 27/10/2026: "qu'est-ce qui compte le plus pour un NPC"), seen from the
// console, and learned or forgotten by hand to try the What You Know section.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>
      ///   cr.matters [learn|forget] [name]: what matters most to a character, their fear, superstition and gift tastes, what the
      ///   player has learned of each, and their What You Know lines.
      /// </summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("matters", "cr")]
      public static string Matters(List<string> args)
      {
         if (!TryContext(out NpcMemoryCampaignBehavior behavior, out string error)) return error;
         string verb = args?.FirstOrDefault()?.ToLowerInvariant();
         bool learn = verb == "learn", forget = verb == "forget";
         Hero hero = ResolveHero(learn || forget ? args.Skip(1).ToList() : args);
         if (hero == null) return "No NPC found. Pass a name fragment or start a conversation first.";

         NpcProfile profile = GetOrCreateProfile(behavior, hero);
         Mapping.HeroProfileMapper.UpgradeIfNeeded(profile, hero);
         if (learn || forget)
         {
            profile.MattersMostKnown = learn;
            profile.FearKnown = learn;
            profile.SuperstitionKnown = learn;
            profile.KnownGifts = learn ? (profile.LovedGifts ?? new List<GiftKind>()).Concat(profile.ColdGifts ?? new List<GiftKind>()).ToList() : null;
         }

         behavior.Store.Set(profile);

         IReadOnlyList<string> page = Mapping.KnownPageReader.LinesFor(hero);
         string Kinds(List<GiftKind> k) => k == null || k.Count == 0 ? "(none)" : string.Join(", ", k);

         return $"{hero.Name}: what matters most = {profile.MattersMost} (learned {profile.MattersMostKnown}); fear = {profile.Fear} (learned {profile.FearKnown}); "
                + $"superstition = {profile.Superstition} (learned {profile.SuperstitionKnown}); gifts loved = {Kinds(profile.LovedGifts)}, cold = {Kinds(profile.ColdGifts)}, "
                + $"learned = {Kinds(profile.KnownGifts)}. What You Know on their page: {(page.Count == 0 ? "(nothing yet)" : string.Join(" ", page))}";
      }
   }
}
