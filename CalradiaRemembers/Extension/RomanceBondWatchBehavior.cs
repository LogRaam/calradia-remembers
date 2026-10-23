// Code written by Gabriel Mailhot, 23/10/2026.
// The clock of CrRomance.StatusChanged (LapitaPolata, 22/10/2026; Gabriel, 23/10/2026). A bond moves in many places (the
// chat, a wedding, a divorce, jealousy, a letter), so rather than raising the event at each, CR reads every bond with the
// player once an hour and when a conversation closes, and announces what moved (RomanceBondWatch). Created for every
// campaign started or loaded, so the first reading of a save only learns its bonds: they are not news.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Reads the bonds with the player at session start and every hour, for <see cref="CrRomance.StatusChanged" />.</summary>
   internal sealed class RomanceBondWatchBehavior : CampaignBehaviorBase
   {
      public RomanceBondWatchBehavior() => CrRomance.ResetWatch();

      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => CrRomance.CheckForChanges());
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, CrRomance.CheckForChanges);
      }

      public override void SyncData(IDataStore dataStore)
      {
      }
   }
}
