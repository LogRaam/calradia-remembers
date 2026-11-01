// Code written by Gabriel Mailhot, 01/11/2026.
// The player extorting a character (Gabriel, 23/10/2026; his "go", 01/11/2026). ExtortionPolicy rules; this reads the hold from
// the live world, keeps the attempts, and spends the cost through the systems that already exist: the realm's crime rating,
// the mod's regard, the grudge ledger, the Misdeeds page. The answer is drawn once a day per person, before they speak, so the
// words the model writes and the coin the game moves agree. Coin paid for lands is a bargain: raiding that house within the
// month breaks it. Carrying out a threat of exposure is not built: a refused secret stays a bluff until expose_secret exists.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Grudges;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Extortion;
using CalradiaRemembers.Logic.Grudges;
using CalradiaRemembers.Logic.Trauma;
using CalradiaRemembers.Mapping;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.CharacterDevelopment;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Violence
{
   /// <summary>A hold the player really has over someone today, and how they will answer it.</summary>
   internal sealed class ExtortionHold
   {
      /// <summary>What kind of hold.</summary>
      public ExtortionLever Lever { get; set; }

      /// <summary>The hold in one sentence, for the prompt.</summary>
      public string Words { get; set; }

      /// <summary>Their chance to give in, in percent.</summary>
      public int Chance { get; set; }

      /// <summary>They will give in today.</summary>
      public bool Yields { get; set; }
   }

   /// <summary>Reads the player's hold over a character, keeps every attempt, and watches the bargains made for lands.</summary>
   public sealed class ExtortionBehavior : CampaignBehaviorBase
   {
      // Additive: a save written before this has no such key and reads an empty ledger (save-migration rule).
      private const string SaveKey = "cr_extortions";

      /// <summary>How far a village may lie from the player's party and still be threatened: about three days' ride.</summary>
      private const float VillageReach = 15f;

      // Not saved: a reload draws again, which only ever reopens the answer of a talk that never landed.
      private readonly Dictionary<string, (int Day, int Roll)> _draws = new Dictionary<string, (int Day, int Roll)>();

      public ExtortionBehavior()
      {
         Instance = this;
      }

      /// <summary>The live behavior, or null outside a campaign.</summary>
      public static ExtortionBehavior Instance { get; private set; }

      /// <summary>Every attempt of the campaign (never null).</summary>
      public ExtortionLedger Ledger { get; internal set; } = new ExtortionLedger();

      /// <summary>Self-test support: the roll to use instead of a draw.</summary>
      internal int? ForcedRoll { get; set; }

      private static int Today => (int) CampaignTime.Now.ToDays;

      public override void RegisterEvents()
      {
         CampaignEvents.VillageStateChanged.AddNonSerializedListener(this, OnVillageStateChanged);
      }

      public override void SyncData(IDataStore dataStore)
      {
         if (dataStore.IsSaving)
         {
            byte[] bytes;
            try { bytes = Encoding.UTF8.GetBytes(Ledger.Serialize()); }
            catch (Exception ex)
            {
               ModLog.Error($"[EXTORT] serialize failed, saving an empty ledger: {ex.GetType().Name}: {ex.Message}");
               bytes = Array.Empty<byte>();
            }

            try { dataStore.SyncData(SaveKey, ref bytes); }
            catch (Exception ex)
            {
               ModLog.Error($"[EXTORT] save failed: {ex.GetType().Name}: {ex.Message}");
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

            Ledger = ExtortionLedger.Deserialize(bytes is {Length: > 0} ? Encoding.UTF8.GetString(bytes) : null);
         }
      }

      /// <summary>The hold the player has over this character today, or null: no hold, nothing worth taking, or pressed too lately.</summary>
      internal ExtortionHold HoldOver(Hero npc)
      {
         Hero player = Hero.MainHero;
         if (npc == null || player == null || npc == player || npc.IsChild || npc.IsDead) return null;
         if (npc.IsPrisoner || player.IsPrisoner || npc.Clan == Clan.PlayerClan && player.Spouse != npc) return null;
         if (!ExtortionPolicy.IsWorthAsking(npc.Gold) || !ExtortionPolicy.MayPressAgain(Ledger.LastDay(npc.StringId), Today)) return null;

         string secret = SecretOver(npc, player);
         Village village = secret == null ? VillageInReach(npc) : null;
         int playerMen = MobileParty.MainParty?.MemberRoster?.TotalHealthyCount ?? 0;
         int theirMen = npc.PartyBelongedTo?.MemberRoster?.TotalHealthyCount ?? 0;

         ExtortionLever lever = ExtortionPolicy.Lever(new ExtortionLeverFacts {
            SecretIsKnown = secret != null, VillageInReach = village != null, PlayerMen = playerMen, TheirMen = theirMen
         });
         if (lever == ExtortionLever.None) return null;

         int chance = ExtortionPolicy.ChanceToYield(new ExtortionResistanceFacts {
            Lever = lever, PlayerRoguery = player.GetSkillValue(DefaultSkills.Roguery), PlayerMen = playerMen, TheirMen = theirMen,
            Honor = npc.GetTraitLevel(DefaultTraits.Honor), TiersAbove = (npc.Clan?.Tier ?? 0) - (Clan.PlayerClan?.Tier ?? 0), IsRuler = npc.Clan?.Kingdom?.Leader == npc
         });

         return new ExtortionHold {
            Lever = lever, Chance = chance, Yields = ExtortionPolicy.Yields(chance, RollFor(npc)),
            Words = secret ?? $"{player.Name}'s {playerMen} men stand within reach of your village of {village.Name}, and you have {theirMen} at your back."
         };
      }

      /// <summary>Writes the attempt down and charges what it costs. Internal for ExtortVerb and cr.test all.</summary>
      internal void Settle(Hero npc, ExtortionLever lever, int paid)
      {
         ExtortionCost cost = ExtortionPolicy.Cost(lever, paid > 0, Ledger.TimesPressed(npc.StringId));
         Ledger.Attempts.Add(new ExtortionRecord {VictimId = npc.StringId, ClanId = npc.Clan?.StringId, Lever = lever, Day = Today, Amount = paid});

         if (cost.CrimeRating > 0 && npc.MapFaction != null) ChangeCrimeRatingAction.Apply(npc.MapFaction, cost.CrimeRating);
         Standing.SelfEsteemService.Record(npc, SelfEsteemEvent.Humiliated);
         if (paid > 0) MisdeedLedgerBehavior.Record(npc.StringId, MisdeedKind.Extorted);

         string cause = paid > 0 ? $"The player extorted {paid} denars from me." : "The player tried to extort me.";
         Charge(npc, PlayerDeed.Extorted, cost.RelationHit, cost.GrudgeMagnitude, cause, paid > 0 ? $"You bled me of {paid} denars with a threat." : "You tried to bleed me with a threat, and I refused.");

         ModLog.Info($"[EXTORT] {npc.Name} ({lever}): paid={paid}, crime={cost.CrimeRating}, regard={cost.RelationHit}, grudge={cost.GrudgeMagnitude}.");
      }

      // The party that leads the raid is the one named: in another lord's army, the raid is his and no word of the player's broke.
      internal void OnVillageStateChanged(Village village, Village.VillageStates oldState, Village.VillageStates newState, MobileParty raider)
      {
         try
         {
            if (raider != MobileParty.MainParty || newState != Village.VillageStates.BeingRaided) return;

            ExtortionRecord bargain = Ledger.BargainOver(village?.Settlement?.OwnerClan?.StringId, Today);
            if (bargain == null) return;

            bargain.BargainBroken = true;
            MisdeedLedgerBehavior.Record(bargain.VictimId, MisdeedKind.BrokeExtortionBargain);

            Hero victim = Hero.FindFirst(h => h != null && h.StringId == bargain.VictimId);
            if (victim != null && !victim.IsDead)
               Charge(victim, PlayerDeed.ExtortionBargainBroken, DeedRegardPolicy.ExtortionBargainBrokenVictim, ExtortionPolicy.BrokenBargainGrudge, "The player took my coin to spare my lands, and raided them anyway.",
                  "You took my coin to spare my lands, and raided them anyway.");

            ModLog.Info($"[EXTORT] the bargain with {bargain.VictimId} is broken: {village?.Name} raided within the month.");
         }
         catch (Exception ex) { ModLog.Error($"[EXTORT] raid not read: {ex.GetType().Name}: {ex.Message}"); }
      }

      private int RollFor(Hero npc)
      {
         if (ForcedRoll != null) return ForcedRoll.Value;
         if (_draws.TryGetValue(npc.StringId, out (int Day, int Roll) draw) && draw.Day == Today) return draw.Roll;

         int roll = MBRandom.RandomInt(100);
         _draws[npc.StringId] = (Today, roll);

         return roll;
      }

      // The mod's own regard, the first-hand memory, the grudge: what they carry away from it whichever way it went.
      private static void Charge(Hero npc, PlayerDeed deed, int relationHit, int grudge, string cause, string memory)
      {
         DeedRegardApplier.Apply(npc, deed, relationHit);

         NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
         NpcProfile profile = behavior?.Store?.Get(npc.StringId);
         if (profile != null)
         {
            profile.Events.Add(new NotableEvent(Today, NotableEventType.Confrontation, memory));
            behavior.Store.Set(profile);
         }

         GrudgeBehavior.Instance?.TryPlant(npc, Hero.MainHero, GrudgeSource.Extorted, cause, grudge, knownToPlayer: true, Today);
      }

      // Only what the player really knows: being this married lord's secret lover, their hidden affair found out, or being
      // the hidden lover of the player's own spouse. Nothing else counts, so the model has nothing to invent from.
      private static string SecretOver(Hero npc, Hero player)
      {
         RomanticProfile romantic = RomanticOf(npc);

         if (romantic?.Status == RomanticStatus.SecretLover && npc.Spouse != null && npc.Spouse != player)
            return $"{player.Name} is your lover in secret, and your {(npc.IsFemale ? "husband" : "wife")} {npc.Spouse.Name} does not know.";

         if (IsAffairKnown(romantic))
            return $"{player.Name} knows of your hidden affair with {Hero.FindFirst(h => h != null && h.StringId == romantic.TakenLoverId)?.Name}.";

         RomanticProfile spouses = RomanticOf(player.Spouse);
         if (IsAffairKnown(spouses) && spouses.TakenLoverId == npc.StringId)
            return $"{player.Name} knows that you are the hidden lover of {player.Spouse.Name}.";

         return null;
      }

      private static RomanticProfile RomanticOf(Hero hero)
         => hero == null ? null : NpcMemoryCampaignBehavior.Instance?.Store?.Get(hero.StringId)?.Romantic;

      private static bool IsAffairKnown(RomanticProfile romantic) => romantic is {LoverIsSecret: true, LoverKnownToPlayer: true} && !string.IsNullOrEmpty(romantic.TakenLoverId);

      // The nearest unspoiled village of their house, unless the player already sold that house a bargain.
      private Village VillageInReach(Hero npc)
      {
         Clan clan = npc.Clan;
         MobileParty party = MobileParty.MainParty;
         if (clan == null || clan == Clan.PlayerClan || party == null || Ledger.BargainOver(clan.StringId, Today) != null) return null;

         return clan.Villages
                    .Where(v => v.VillageState == Village.VillageStates.Normal && v.Settlement.GetPosition2D.Distance(party.GetPosition2D) <= VillageReach)
                    .OrderBy(v => v.Settlement.GetPosition2D.Distance(party.GetPosition2D))
                    .FirstOrDefault();
      }
   }
}
