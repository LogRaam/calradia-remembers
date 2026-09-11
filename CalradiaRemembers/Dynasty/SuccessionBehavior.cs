// Code written by Gabriel Mailhot, 22/06/2026.
// Dynastic succession: when the player dies and an heir takes over, every NPC's memory is reframed —
// the predecessor's PRIVATE history is dropped, the political/relational history is kept and marked as
// INHERITED (the prompt then frames it as "your father's dealings"). Deterministic, no LLM call.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic;
using CalradiaRemembers.WorldEvents;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;

#endregion

namespace CalradiaRemembers.Dynasty
{
    /// <summary>Reframes all stored NPC memories through the lens of the new heir on player death.</summary>
    public sealed class SuccessionBehavior : CampaignBehaviorBase
    {
        public static SuccessionBehavior Instance { get; private set; }

        public SuccessionBehavior() => Instance = this;

        public override void RegisterEvents()
            => CampaignEvents.OnPlayerCharacterChangedEvent.AddNonSerializedListener(this, OnPlayerCharacterChanged);

        public override void SyncData(IDataStore dataStore) { }

        private void OnPlayerCharacterChanged(Hero oldPlayer, Hero newPlayer, MobileParty newMainParty, bool isMainPartyChanged)
            => Apply(oldPlayer, newPlayer);

        /// <summary>
        ///   Reframes every stored profile: drops the predecessor's private events, keeps the rest, and
        ///   marks the history inherited from <paramref name="predecessor"/> (with the kinship word).
        ///   Returns how many profiles were touched. Public so a console command can simulate it.
        /// </summary>
        public int Apply(Hero predecessor, Hero heir)
        {
            if (predecessor == null || heir == null || predecessor == heir) return 0;

            var store = NpcMemoryCampaignBehavior.Instance?.Store;
            if (store == null) return 0;

            string predName = predecessor.Name?.ToString();
            string kinship = ResolveKinship(predecessor, heir);

            int n = 0;
            foreach (string id in store.ListIds())
            {
                NpcProfile p = store.Get(id);
                if (p == null) continue;

                // The heir inherits the standing, not the private history.
                p.Events.RemoveAll(e => !SuccessionMemoryPolicy.SurvivesSuccession(e.type));
                p.InheritedFromName = predName;
                p.InheritedKinship = kinship;
                store.Set(p);
                n++;
            }

            RecordChronicleSuccession(predecessor, heir);

            ModLog.Info($"[SUCCESSION] reframed {n} profile(s): the heir is {predName}'s {kinship}.");
            return n;
        }

        /// <summary>
        ///   The chronicle itself does not reset on death — it simply keeps writing, now of the heir's own
        ///   deeds, with this one line marking the turn. Naturally a once-per-reign event; never throws.
        /// </summary>
        private static void RecordChronicleSuccession(Hero predecessor, Hero heir)
        {
            try
            {
                string pronoun = predecessor.IsFemale ? "her" : "his";
                WorldEventStore.Instance?.Record(new WorldEvent {
                    Type = WorldEventType.PlayerFeat,
                    Day = (int) CampaignTime.Now.ToDays,
                    Magnitude = 80,
                    InvolvesPlayer = true,
                    Summary = $"{predecessor.Name} died, and {heir.Name} took up the succession.",
                    ActorIds = new List<string> {predecessor.StringId, heir.StringId},
                    ChronicleLine = $"here ended the days of {predecessor.Name}; the chronicle passes to {heir.Name}, of {pronoun} blood"
                });
            }
            catch (Exception ex) { ModLog.Error($"[SUCCESSION] chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        private static string ResolveKinship(Hero predecessor, Hero heir)
        {
            bool parent  = heir.Father == predecessor || heir.Mother == predecessor;
            bool sibling = heir.Siblings != null && heir.Siblings.Contains(predecessor);
            bool spouse  = heir.Spouse == predecessor || predecessor.Spouse == heir;
            return SuccessionKinship.Label(new SuccessionKinship.PredecessorFacts {WasParent = parent, WasSibling = sibling, WasSpouse = spouse, IsFemale = predecessor.IsFemale});
        }
    }
}
