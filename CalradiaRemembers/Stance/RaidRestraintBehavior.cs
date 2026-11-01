// Code written by Gabriel Mailhot, 01/11/2026.
// DepaHN (Nexus, 27/10/2026): a friend or relative should not burn the player's villages without a reason. Gabriel, 01/11/2026:
// a friend MAY hold back, by chance, and the likelier the closer he is. Once a day every friend or relative at war with the
// player who has not drawn for this war draws (RaidRestraintPolicy); so a war just declared, a save from before this, a defector
// and a friendship made mid-war all take the one path. The draw holds to the peace, unless he is given a reason: a vendetta, a
// member of his house killed by the player, or the player raiding his lands.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Raids;
using CalradiaRemembers.Mapping;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Stance
{
   /// <summary>Holds what each lord drew in the war against the player, and sets it aside when he is given a reason.</summary>
   public sealed class RaidRestraintBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_raid_restraint";

      // The lords sparing the player's villages: the score model asks for every lord and every target, every few hours.
      private readonly HashSet<string> _sparing = new HashSet<string>();

      /// <summary>cr.test all seam: the clan whose villages count as the player's, on a save where the player holds none.</summary>
      internal static Clan DebugProtectedClan;

      public RaidRestraintBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static RaidRestraintBehavior Instance { get; private set; }

      /// <summary>The draws of the wars under way (never null).</summary>
      public RaidRestraintLedger Ledger { get; private set; } = new RaidRestraintLedger();

      private static Clan ProtectedClan => DebugProtectedClan ?? Clan.PlayerClan;

      public override void RegisterEvents()
      {
         CampaignEvents.DailyTickEvent.AddNonSerializedListener(this, OnDailyTick);
         CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, OnVillageStateChanged);
         CampaignEvents.HeroKilledEvent.AddNonSerializedListener(this, OnHeroKilled);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[RAID] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[RAID] save failed: {ex.GetType().Name}: {ex.Message}");
            }
         }
         else
         {
            byte[] bytes = null;
            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch
            {
               /* key absent in a save written before this feature */
            }

            ReplaceLedger(RaidRestraintLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null));
         }
      }

      /// <summary>True when this lord will not raid this village: it is the player's, and he drew to spare them.</summary>
      internal static bool Spares(Hero lord, Settlement village)
      {
         if (lord == null || village == null || !village.IsVillage || Instance == null || !ModSettings.RaidRestraint) return false;

         return village.OwnerClan == ProtectedClan && Instance._sparing.Contains(lord.StringId);
      }

      /// <summary>True when the character knows they will not touch the player's villages, for the prompt.</summary>
      internal static bool SparesThePlayer(Hero lord)
         => lord != null && Instance != null && ModSettings.RaidRestraint && Clan.PlayerClan?.Villages?.Count > 0 && Instance._sparing.Contains(lord.StringId);

      /// <summary>True while this lord spares the player's villages, for a letter still on the road.</summary>
      internal bool StillSpares(string heroId) => heroId != null && _sparing.Contains(heroId);

      /// <summary>Puts a ledger in force. Internal for cr.test all.</summary>
      internal void ReplaceLedger(RaidRestraintLedger ledger)
      {
         Ledger = ledger ?? new RaidRestraintLedger();
         RefreshSparing();
      }

      /// <summary>Sets aside the restraint of every lord of this house, for a reason he can name. Internal for cr.test all.</summary>
      internal void BreakForHouse(Clan house, string reason)
      {
         if (house == null) return;

         foreach (RaidRestraint draw in Ledger.Draws.Where(d => d.IsSparing))
         {
            Hero lord = FindHero(draw.HeroId);
            if (lord?.Clan == house) Break(draw, lord, reason);
         }

         RefreshSparing();
      }

      private static void Break(RaidRestraint draw, Hero lord, string reason)
      {
         draw.BrokenBecause = reason;
         ModLog.Info($"[RAID] {lord?.Name} no longer spares the player's villages: {reason}.");
      }

      private void OnDailyTick()
      {
         try
         {
            ForgetEndedWars();
            BreakOnVendettas();
            DrawForTheNewcomers();
            RefreshSparing();
         }
         catch (Exception ex)
         {
            ModLog.Error($"[RAID] daily pass failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      private void ForgetEndedWars()
      {
         Ledger.Draws.RemoveAll(d =>
         {
            Hero lord = FindHero(d.HeroId);

            return lord == null || lord.IsDead || lord.MapFaction?.StringId != d.FactionId || !AtWarWithThePlayer(lord);
         });
      }

      private void BreakOnVendettas()
      {
         foreach (RaidRestraint draw in Ledger.Draws.Where(d => d.IsSparing))
         {
            Hero lord = FindHero(draw.HeroId);
            if (RaidRestraintPolicy.GrudgeBreaksRestraint(EncounterContextBuilder.HeaviestGrudgeAgainstPlayer(lord))) Break(draw, lord, "a vendetta");
         }
      }

      // Nothing to spare, nothing to draw for: a lord who draws once the player holds a village draws knowing what is at stake.
      private void DrawForTheNewcomers()
      {
         if (!ModSettings.RaidRestraint || !(Clan.PlayerClan?.Villages?.Count > 0)) return;

         foreach (Hero lord in Hero.AllAliveHeroes.ToList())
         {
            if (!lord.IsLord || lord.IsChild || lord.Clan == Clan.PlayerClan || !HeroPresence.IsInTheWorld(lord) || !AtWarWithThePlayer(lord)) continue;

            string faction = lord.MapFaction.StringId;
            if (Ledger.HasDrawn(lord.StringId, faction)) continue;

            int chance = RaidRestraintPolicy.ChanceToSpare(Facts(lord));
            if (chance <= 0) continue;

            int roll = MBRandom.RandomInt(100);
            bool spares = RaidRestraintPolicy.Spares(chance, roll);
            Ledger.Add(lord.StringId, faction, spares, (int) CampaignTime.Now.ToDays);
            ModLog.Info($"[RAID] {lord.Name} ({lord.MapFaction.Name}) drew {roll} against {chance}%: {(spares ? "spares" : "does not spare")} the player's villages in this war.");
         }
      }

      private static RaidRestraintFacts Facts(Hero lord) => new() {
         Regard = NpcMemoryCampaignBehavior.Instance?.Store?.Get(lord.StringId)?.ReputationWithPlayer ?? 0,
         IsKin = IsKinOrInLaw(lord),
         HeaviestGrudge = EncounterContextBuilder.HeaviestGrudgeAgainstPlayer(lord)
      };

      // DepaHN's son-in-law: the kinship label stops at blood, so a marriage is walked here.
      internal static bool IsKinOrInLaw(Hero lord)
      {
         if (EncounterContextBuilder.ResolveKinshipToPlayer(lord) != null) return true;

         Hero spouse = Hero.MainHero?.Spouse;
         bool mySpousesKin = spouse != null && (lord == spouse.Father || lord == spouse.Mother || spouse.Siblings.Contains(lord));

         return mySpousesKin || (lord.Spouse != null && EncounterContextBuilder.ResolveKinshipToPlayer(lord.Spouse) != null);
      }

      private void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raider)
      {
         if (raider == MobileParty.MainParty && newState == Village.VillageStates.BeingRaided)
            BreakForHouse(village?.Settlement?.OwnerClan, "the player raided his lands");
      }

      private void OnHeroKilled(Hero victim, Hero killer, KillCharacterAction.KillCharacterActionDetail detail, bool showNotification)
      {
         if (killer != null && killer == Hero.MainHero) BreakForHouse(victim?.Clan, "the player killed one of his house");
      }

      private void RefreshSparing()
      {
         _sparing.Clear();
         foreach (RaidRestraint draw in Ledger.Draws.Where(d => d.IsSparing)) _sparing.Add(draw.HeroId);
      }

      private static bool AtWarWithThePlayer(Hero lord)
      {
         IFaction theirs = lord?.MapFaction;
         IFaction mine = Hero.MainHero?.MapFaction;

         return theirs != null && mine != null && theirs != mine && theirs.IsAtWarWith(mine);
      }

      internal static Hero FindHero(string id) => Hero.AllAliveHeroes.FirstOrDefault(h => h.StringId == id);
   }
}
