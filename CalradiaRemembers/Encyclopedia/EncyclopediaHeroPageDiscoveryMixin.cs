// Code written by Gabriel Mailhot, 01/06/2026.

#region

using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Bannerlord.UIExtenderEx.Attributes;
using Bannerlord.UIExtenderEx.ViewModels;
using CalradiaRemembers.Behaviors;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.ViewModelCollection.Encyclopedia.Pages;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Encyclopedia
{
   /// <summary>
   ///   UIExtenderEx mixin that displays the personal traits the player has
   ///   discovered about an NPC through conversation, on that NPC's encyclopedia
   ///   hero page. Bound via <c>@CalradiaDiscoveryLabel</c> (cream header) and
   ///   <c>@CalradiaDiscoveryItems</c> (gold bullet list). Both are empty strings
   ///   when there are no discoveries — the AutoHide widgets collapse and the
   ///   block takes no space on pages with no data.
   /// </summary>
   [ViewModelMixin]
   public sealed class EncyclopediaHeroPageDiscoveryMixin
      : BaseViewModelMixin<EncyclopediaHeroPageVM>
   {
      private static readonly FieldInfo HeroField =
         typeof(EncyclopediaHeroPageVM).GetField("_hero", BindingFlags.NonPublic | BindingFlags.Instance);

      private string _items;

      private string _label;

      public EncyclopediaHeroPageDiscoveryMixin(EncyclopediaHeroPageVM vm) : base(vm)
      {
         Refresh();
      }

      /// <summary>
      ///   Gold bullet list — each discovery on its own line ("• description").
      ///   Empty string when there are no discoveries (auto-hides).
      /// </summary>
      [DataSourceProperty]
      public string CalradiaDiscoveryItems
      {
         get => _items;
         set
         {
            if (_items == value) return;
            _items = value;
            ViewModel?.OnPropertyChanged();
         }
      }

      /// <summary>Cream header — "Discoveries:" or empty (auto-hides).</summary>
      [DataSourceProperty]
      public string CalradiaDiscoveryLabel
      {
         get => _label;
         set
         {
            if (_label == value) return;
            _label = value;
            ViewModel?.OnPropertyChanged();
         }
      }

      public override void OnRefresh()
      {
         Refresh();
      }

      #region private

      private static string BuildItems(List<DiscoveredTrait> traits)
      {
         var sb = new StringBuilder();
         foreach (DiscoveredTrait trait in traits)
         {
            if (sb.Length > 0) sb.Append('\n');
            sb.Append("• ");
            sb.Append(trait.Description);
         }

         return sb.ToString();
      }

      private void Refresh()
      {
         var hero = HeroField?.GetValue(ViewModel) as Hero;
         if (hero == null || hero == Hero.MainHero)
         {
            CalradiaDiscoveryLabel = "";
            CalradiaDiscoveryItems = "";

            return;
         }

         var behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(hero.StringId);

         if (profile == null || profile.DiscoveredTraits.Count == 0)
         {
            CalradiaDiscoveryLabel = "";
            CalradiaDiscoveryItems = "";

            return;
         }

         CalradiaDiscoveryLabel = "Discoveries:";
         CalradiaDiscoveryItems = BuildItems(profile.DiscoveredTraits);
      }

      #endregion
   }
}