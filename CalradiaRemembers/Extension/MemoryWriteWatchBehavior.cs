// Code written by Gabriel Mailhot, 20/11/2026.
// The clock of CrNpcMemory.MemoryWritten (Bhelogan, Discord, 20/11/2026: "a 'new memory added' event ... would let us prepare
// the picture while the scene is still fresh"). A memory is written from more than a hundred places, so rather than raising
// the event at each, CR looks at every character's memories when the session starts (taking stock: what a save holds is not
// news), every hour, and as a conversation opens and closes (MemoryPictures, MemoryWriteWatch).

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Looks at the memories at session start and every hour, for <see cref="CrNpcMemory.MemoryWritten" />.</summary>
   internal sealed class MemoryWriteWatchBehavior : CampaignBehaviorBase
   {
      public override void RegisterEvents()
      {
         CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => MemoryPictures.LookAtEveryone(null));
         CampaignEvents.HourlyTickEvent.AddNonSerializedListener(this, MemoryPictures.OnHourlyTick);
      }

      public override void SyncData(IDataStore dataStore)
      {
      }
   }
}
