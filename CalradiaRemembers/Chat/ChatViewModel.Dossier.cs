// Code written by Gabriel Mailhot, 31/10/2026.
// What the NPC dossier shows of what the player has learned, the same panel in both chat skins and at the council.
// Gabriel (31/10/2026): "Dans le panneau du NPC, on pourrait ajouter tout ce que le joueur a appris sur ce NPC et qui a ete
// ajoute a l'Encyclopedia". A reply can teach something while the panel is open, so these sections are read again after
// each one; the rest of the dossier stays as it was filled, since none of it can change mid-sentence.

#region

using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Chat;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private string _panelHeroId = "";

      /// <summary>The accords standing between this NPC and the player.</summary>
      [DataSourceProperty]
      public string PanelAccords { get; private set; } = "";

      /// <summary>Their bond with the player.</summary>
      [DataSourceProperty]
      public string PanelHeart { get; private set; } = "";

      /// <summary>The kindnesses the player knows this NPC remembers.</summary>
      [DataSourceProperty]
      public string PanelKindnesses { get; private set; } = "";

      /// <summary>The nemesis dossier, once the player has met them as one.</summary>
      [DataSourceProperty]
      public string PanelNemesis { get; private set; } = "";

      /// <summary>The heading of the nemesis dossier.</summary>
      [DataSourceProperty]
      public string PanelNemesisLabel { get; private set; } = "";

      /// <summary>The aggressions they lived through, at Hardcore.</summary>
      [DataSourceProperty]
      public string PanelTrauma { get; private set; } = "";

      [DataSourceProperty]
      public bool ShowPanelAccords { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelHeart { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelKindnesses { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelNemesis { get; private set; }

      [DataSourceProperty]
      public bool ShowPanelTrauma { get; private set; }

      /// <summary>
      ///   Reads the open dossier's learned sections again, so what a reply taught shows at once. Does nothing while no
      ///   NPC dossier is open. cr.test npc_dossier drives this seam.
      /// </summary>
      internal void RefreshOpenDossier()
      {
         if (string.IsNullOrEmpty(_panelHeroId)) return;
         if (!IsPortraitPanelOpen && !IsManuscriptLeftPageOpen && !IsCouncilDossierOpen) return;

         Hero hero = Hero.FindFirst(h => h.StringId == _panelHeroId);
         if (hero == null) return;

         ResolveLearnedSections(hero, NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId));
         NotifyLearnedSections();
      }

      private void ClearLearnedSections()
      {
         ApplyLearnedSections(new DossierSections());
         PanelInfoFacts = "";
      }

      private void ResolveLearnedSections(Hero hero, NpcProfile profile)
      {
         DossierSections sections = DossierReader.SectionsFor(hero, profile);
         ApplyLearnedSections(sections);
         PanelInfoFacts = ComposePanelFacts(hero, profile, sections.Bearing);
      }

      private void ApplyLearnedSections(DossierSections sections)
      {
         PanelInfoDiscoveries = sections.Learned;
         ShowPanelDiscoveries = sections.Learned.Length > 0;
         PanelHeart = sections.Heart;
         ShowPanelHeart = sections.Heart.Length > 0;
         PanelKindnesses = sections.Kindnesses;
         ShowPanelKindnesses = sections.Kindnesses.Length > 0;
         PanelGrievances = sections.Grievances;
         ShowPanelGrievances = sections.Grievances.Length > 0;
         PanelAccords = sections.Accords;
         ShowPanelAccords = sections.Accords.Length > 0;
         PanelNemesisLabel = sections.NemesisLabel;
         PanelNemesis = sections.Nemesis;
         ShowPanelNemesis = sections.Nemesis.Length > 0 && sections.NemesisLabel.Length > 0;
         PanelTrauma = sections.Trauma;
         ShowPanelTrauma = sections.Trauma.Length > 0;
      }

      private void NotifyLearnedSections()
      {
         OnPropertyChanged(nameof(PanelInfoFacts));
         OnPropertyChanged(nameof(ShowPanelDiscoveries));
         OnPropertyChanged(nameof(PanelInfoDiscoveries));
         OnPropertyChanged(nameof(ShowPanelHeart));
         OnPropertyChanged(nameof(PanelHeart));
         OnPropertyChanged(nameof(ShowPanelKindnesses));
         OnPropertyChanged(nameof(PanelKindnesses));
         OnPropertyChanged(nameof(ShowPanelGrievances));
         OnPropertyChanged(nameof(PanelGrievances));
         OnPropertyChanged(nameof(ShowPanelAccords));
         OnPropertyChanged(nameof(PanelAccords));
         OnPropertyChanged(nameof(ShowPanelNemesis));
         OnPropertyChanged(nameof(PanelNemesisLabel));
         OnPropertyChanged(nameof(PanelNemesis));
         OnPropertyChanged(nameof(ShowPanelTrauma));
         OnPropertyChanged(nameof(PanelTrauma));
      }

      // Inline facts, one "Label: value" per line (no table alignment, per the design note).
      private static string ComposePanelFacts(Hero hero, NpcProfile profile, string bearing)
      {
         int regard = profile?.ReputationWithPlayer ?? 0;
         string why = profile == null ? null : SafeRead(() => DossierReader.WhyTowardThePlayer(hero, regard));

         var facts = new StringBuilder();
         facts.Append("Culture: ").Append(hero.Culture?.Name?.ToString() ?? "Unknown").Append('\n');
         facts.Append("Age: ").Append((int) hero.Age).Append('\n');
         facts.Append("Role: ").Append(DescribeOccupation(hero)).Append('\n');
         facts.Append("Relation: ").Append(hero.GetRelationWithPlayer().ToString("+#;-#;0")).Append('\n');
         facts.Append("Personal regard: ").Append(NpcDossierText.RegardLine(ThirdPartyDescriptor.DescribeRelation(regard), regard, why));
         if (!string.IsNullOrEmpty(bearing)) facts.Append('\n').Append("Bearing: ").Append(bearing);

         return facts.ToString();
      }
   }
}
