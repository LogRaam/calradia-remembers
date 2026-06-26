// Code written by Gabriel Mailhot, 25/06/2026.
// Birth-time clan correction for CR-created pregnancies. A child of a CR conception ALWAYS belongs to the
// MOTHER's clan: player-mother / legitimate-wife → the player's clan (they are already in it), another woman →
// her own house (a bastard). Vanilla DeliverOffSpring routes a player-FATHERED child into the player's clan,
// so we reassign it at birth — but ONLY for pregnancies CR itself created, leaving vanilla NPC patrilineality
// untouched. See PregnancyService for the conception side.

#region

using System.Collections.Generic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Tracks the mothers CR has made pregnant and, at birth, places their offspring in the MOTHER's clan.
   ///   The tracked set is persisted (a pregnancy spans ~84 game-days, often across saves).
   /// </summary>
   public sealed class PregnancyBirthBehavior : CampaignBehaviorBase
   {
      private const string SaveKey = "CalradiaRemembers_CrExpectingMothers_v1";

      public static PregnancyBirthBehavior Instance { get; private set; }

      private List<string> _crMotherIds = new();

      public PregnancyBirthBehavior() => Instance = this;

      public override void RegisterEvents()
      {
         CampaignEvents.OnGivenBirthEvent.AddNonSerializedListener(this, OnGivenBirth);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
      }

      public override void SyncData(IDataStore dataStore)
         => dataStore.SyncData(SaveKey, ref _crMotherIds);

      /// <summary>
      ///   Registers a mother CR has just made pregnant, so her child is reassigned to her clan at birth.
      ///   Called by <see cref="PregnancyService" /> after a successful conception.
      /// </summary>
      public void TrackConception(Hero mother)
      {
         if (mother == null) return;
         if (!_crMotherIds.Contains(mother.StringId)) _crMotherIds.Add(mother.StringId);
      }

      private void OnGivenBirth(Hero mother, List<Hero> aliveChildren, int stillbornCount)
      {
         // Only correct CR's own pregnancies; vanilla NPC births keep their (patrilineal) clan.
         if (mother == null || !_crMotherIds.Remove(mother.StringId) || aliveChildren == null) return;

         Clan motherClan = mother.Clan;
         foreach (Hero child in aliveChildren)
         {
            if (child == null || child.Clan == motherClan) continue;
            child.Clan = motherClan;
            ModLog.Info($"[PREGNANCY] CR birth: moved {child.Name} into the mother's clan ({motherClan?.Name?.ToString() ?? "—"}).");
         }
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         // A mother who dies while pregnant will never give birth — drop her so the set can't leak.
         if (victim != null) _crMotherIds.Remove(victim.StringId);
      }
   }
}
