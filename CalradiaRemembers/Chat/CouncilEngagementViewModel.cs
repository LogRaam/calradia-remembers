// Code written by Gabriel Mailhot, 30/07/2026.

#region

using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   One row of the bespoke COUNCIL window's right "Engagements" panel: a pledge made and its current standing.
   ///   Inc 0 only wires the binding TYPE and the empty-state placeholder; the council does not yet populate this
   ///   list from <see cref="CalradiaRemembers.Assemblies.CouncilSessionBehavior" />'s ledger. Inc 1 fills it.
   /// </summary>
   public sealed class CouncilEngagementViewModel : ViewModel
   {
      public CouncilEngagementViewModel(string label, string detail, string status)
      {
         Label = Mapping.ShownText.Of(label ?? "");
         Detail = Mapping.ShownText.Of(detail ?? "");
         Status = status ?? "";
      }

      /// <summary>What was pledged, e.g. "Marry the player's sister to Lord Aldric".</summary>
      [DataSourceProperty]
      public string Detail { get; }

      /// <summary>Who made the pledge, e.g. "Aldric vows:".</summary>
      [DataSourceProperty]
      public string Label { get; }

      /// <summary>The pledge's current lifecycle state, e.g. "Agreed", "Applied", "Failed".</summary>
      [DataSourceProperty]
      public string Status { get; }
   }
}
