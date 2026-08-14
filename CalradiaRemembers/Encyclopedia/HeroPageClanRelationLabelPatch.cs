// Code written by Gabriel Mailhot, 14/08/2026.
// Players confuse vanilla's clan-level "Relation" stat on the encyclopedia hero page with CR's own PERSONAL
// regard row (EncyclopediaHeroPagePersonalRelationMixin, "Personal regard:"). Decompiling
// TaleWorlds.CampaignSystem.ViewModelCollection.dll shows the vanilla stat is built in
// EncyclopediaHeroPageVM.Refresh() as a StringPairItemVM (Stats.Add(new StringPairItemVM(definition4,
// _hero.GetRelationWithPlayer().ToString()))), definition4 = GameTexts.FindText("str_enc_sf_relation").ToString().
// str_enc_sf_relation is a SHARED localization id (other screens read it too), so overriding it globally would
// relabel more than this one row. Instead this postfix runs after the vanilla list is rebuilt, finds the ONE
// Stats entry whose Definition still equals that shared string, and renames only its label (StringPairItemVM.
// Definition has a public setter): the vanilla relation VALUE and every other use of "Relation" in the game
// are untouched.

#region

using System;
using HarmonyLib;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Core;
using TaleWorlds.Core.ViewModelCollection.Generic;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   Relabels the vanilla clan-relation stat on the encyclopedia hero page from "Relation" to "Clan Relation",
   ///   so it reads as visibly distinct from CR's own "Personal regard:" row just below it. See the file header
   ///   for why this targets the built StringPairItemVM instance rather than the shared localization string.
   /// </summary>
   [HarmonyPatch(typeof(EncyclopediaHeroPageVM), "Refresh")]
   public static class HeroPageClanRelationLabelPatch
   {
      private const string ClanRelationLabel = "Clan Relation";

      public static void Postfix(EncyclopediaHeroPageVM __instance)
      {
         try
         {
            string vanillaRelationLabel = GameTexts.FindText("str_enc_sf_relation")?.ToString();
            if (string.IsNullOrEmpty(vanillaRelationLabel)) return;

            MBBindingList<StringPairItemVM> stats = __instance?.Stats;
            if (stats == null) return;

            foreach (StringPairItemVM stat in stats)
            {
               if (stat.Definition != vanillaRelationLabel) continue;

               stat.Definition = ClanRelationLabel;

               return;
            }

            // Not found this refresh (e.g. the player's own page, which skips the relation row): expected,
            // not an error, no row to rename.
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ENCYCLOPEDIA] failed to relabel the clan-relation stat (non-fatal): {ex.GetType().Name}: {ex.Message}");
         }
      }
   }
}
