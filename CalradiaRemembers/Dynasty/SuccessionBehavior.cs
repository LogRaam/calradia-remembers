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
        {
            // Only a real succession reframes memories: the reframe DELETES private history for good. Another mod
            // switching who the player controls (Wallace1067, The Original Player Switcher) is no death, and was
            // wiping real memories from the whole save each way. See SuccessionTrigger.
            PlayerChangeKind kind = ClassifyChange(oldPlayer);

            if (!SuccessionTrigger.IsSuccession(kind))
            {
                ModLog.Info($"[SUCCESSION] the player character changed from {oldPlayer?.Name} to {newPlayer?.Name}, but "
                          + $"{oldPlayer?.Name} is alive and active: a switch, not a succession. Nothing reframed.");

                return;
            }

            Apply(oldPlayer, newPlayer, kind);
        }

        /// <summary>
        ///   What the change meant, read from the predecessor's own state when the event fires: vanilla kills him
        ///   before handing over on a death, and disables him on a retirement (ApplyHeirSelectionAction, decompiled).
        /// </summary>
        public static PlayerChangeKind ClassifyChange(Hero oldPlayer)
            => SuccessionTrigger.Classify(new PlayerChangeFacts {
                PredecessorIsDead = oldPlayer != null && oldPlayer.IsDead,
                PredecessorIsRetired = oldPlayer != null && oldPlayer.HeroState == Hero.CharacterStates.Disabled
            });

        /// <summary>
        ///   Reframes every stored profile: drops the predecessor's private events, keeps the rest, and
        ///   marks the history inherited from <paramref name="predecessor"/> (with the kinship word).
        ///   Returns how many profiles were touched. Public so a console command can simulate it.
        /// </summary>
        public int Apply(Hero predecessor, Hero heir, PlayerChangeKind kind = PlayerChangeKind.Death)
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
                SuccessionMemoryPolicy.Reframe(p, predName, kinship, kind == PlayerChangeKind.Retirement);
                store.Set(p);
                n++;
            }

            RecordChronicleSuccession(predecessor, heir, kind);

            ModLog.Info($"[SUCCESSION] reframed {n} profile(s): the heir is {predName}'s {kinship}.");
            return n;
        }

        /// <summary>
        ///   The chronicle itself does not reset on death — it simply keeps writing, now of the heir's own
        ///   deeds, with this one line marking the turn. Naturally a once-per-reign event; never throws.
        /// </summary>
        private static void RecordChronicleSuccession(Hero predecessor, Hero heir, PlayerChangeKind kind)
        {
            try
            {
                string pronoun = predecessor.IsFemale ? "her" : "his";
                bool retired = kind == PlayerChangeKind.Retirement;
                WorldEventStore.Instance?.Record(new WorldEvent {
                    Type = WorldEventType.PlayerFeat,
                    Day = (int) CampaignTime.Now.ToDays,
                    Magnitude = 80,
                    InvolvesPlayer = true,
                    // A retired predecessor is alive: saying he died would be a checkable falsehood.
                    Summary = retired
                        ? $"{predecessor.Name} retired, and {heir.Name} took up the succession."
                        : $"{predecessor.Name} died, and {heir.Name} took up the succession.",
                    ActorIds = new List<string> {predecessor.StringId, heir.StringId},
                    ChronicleLine = retired
                        ? $"here {predecessor.Name} laid down the sword; the chronicle passes to {heir.Name}, of {pronoun} blood"
                        : $"here ended the days of {predecessor.Name}; the chronicle passes to {heir.Name}, of {pronoun} blood"
                });
            }
            catch (Exception ex) { ModLog.Error($"[SUCCESSION] chronicle record failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        internal static string ResolveKinship(Hero predecessor, Hero heir)
        {
            bool parent  = heir.Father == predecessor || heir.Mother == predecessor;
            bool sibling = heir.Siblings != null && heir.Siblings.Contains(predecessor);
            bool spouse  = heir.Spouse == predecessor || predecessor.Spouse == heir;
            return SuccessionKinship.Label(new SuccessionKinship.PredecessorFacts {WasParent = parent, WasSibling = sibling, WasSpouse = spouse, IsFemale = predecessor.IsFemale});
        }
    }
}
