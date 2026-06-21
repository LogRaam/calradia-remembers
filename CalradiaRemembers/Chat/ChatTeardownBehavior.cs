// Code written by Gabriel Mailhot, 21/06/2026.
// Gives ChatManager a per-frame NATIVE tick to tear the chat overlay down on — outside the UI
// LateUpdate, where removing our GauntletLayer NREs GauntletUI's DefragContainers (the letter-reply
// crash). The campaign tick runs the game logic, not the UI draw, so removing the layer here is safe.

#region

using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>
    ///   Drains <see cref="ChatManager.ProcessPendingDestroy"/> every campaign frame. Holds no state
    ///   (the pending flag lives on ChatManager), so <see cref="SyncData"/> is empty. The chat overlay
    ///   un-pauses time when it requests a deferred close, which guarantees this tick fires.
    /// </summary>
    public sealed class ChatTeardownBehavior : CampaignBehaviorBase
    {
        public override void RegisterEvents()
        {
            CampaignEvents.TickEvent.AddNonSerializedListener(this, OnTick);
        }

        public override void SyncData(IDataStore dataStore) { }

        private void OnTick(float dt) => ChatManager.ProcessPendingDestroy();
    }
}
