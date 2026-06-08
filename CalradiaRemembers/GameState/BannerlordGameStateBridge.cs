// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;
using TaleWorlds.Engine;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.GameState
{
   /// <summary>
   ///   Concrete implementation of <see cref="IGameStateBridge" /> using the
   ///   TaleWorlds campaign API. This is the ONLY class in the mod that should
   ///   reach directly into <c>Hero.MainHero</c>, <c>Campaign.Current</c>,
   ///   <c>FactionManager</c>, etc. Everything else uses the abstraction.
   /// </summary>
   public sealed class BannerlordGameStateBridge : IGameStateBridge
   {
      // ── Sprint 10: action execution ───────────────────────────────────────

      public ActionOutcome ExecuteAction(GameAction action, Hero npcHero, ActionContext context)
      {
         if (action == null) return null;

         switch (action.Type.ToLowerInvariant())
         {
            case "change_relation":
               return ExecuteChangeRelation(action, npcHero, context);
            case "give_gold":
               return ExecuteGiveGold(action, npcHero);
            case "take_gold":
               return ExecuteTakeGold(action, npcHero);
            default:
               ModLog.Info($"[ACTION] Unknown type '{action.Type}' — ignored.");

               return null;
         }
      }

      public string GetActiveConflicts()
      {
         if (Kingdom.All == null) return "";

         var wars = new List<string>();
         List<Kingdom> kingdoms = RetrieveAllKingdoms();

         for (var i = 0; i < kingdoms.Count; i++)
            for (int j = i + 1; j < kingdoms.Count; j++)
            {
               Kingdom a = kingdoms[i];
               Kingdom b = kingdoms[j];
               if (FactionManager.IsAtWarAgainstFaction(a, b))
                  wars.Add($"{a.Name} vs {b.Name}");
            }

         return wars.Count > 0
            ? string.Join("; ", wars)
            : "";
      }

      public int GetCurrentGameDay()
         => (int) CampaignTime.Now.ToDays;

      public SceneType GetCurrentScene()
      {
         Settlement settlement = Hero.MainHero?.CurrentSettlement ?? PlayerEncounter.LocationEncounter?.Settlement;

         if (settlement == null)
            return SceneType.Outdoor;

         if (settlement.IsCastle || settlement.IsFortification)
            return SceneType.Keep;

         if (settlement.IsTown || settlement.IsVillage)
            return SceneType.Settlement;

         return SceneType.Outdoor;
      }

      public DiplomaticStatus GetDiplomaticStatus(Hero npc)
      {
         if (npc == null || npc.MapFaction == null || Hero.MainHero?.MapFaction == null)
            return DiplomaticStatus.Unknown;

         // Same faction (or same clan, both treated as "allied" here)
         if (npc.MapFaction == Hero.MainHero.MapFaction)
            return DiplomaticStatus.Allied;

         if (FactionManager.IsAtWarAgainstFaction(npc.MapFaction, Hero.MainHero.MapFaction))
            return DiplomaticStatus.AtWar;

         return DiplomaticStatus.AtPeace;
      }

      public int GetPlayerRelation(Hero npc)
      {
         if (npc == null) return 0;

         // GetRelationWithPlayer reads the same store that
         // ChangeRelationAction.ApplyPlayerRelation writes to.
         // CharacterRelationManager.GetHeroRelation is a separate
         // internal dict and does not reflect ApplyPlayerRelation changes.
         return (int) npc.GetRelationWithPlayer();
      }

      public PlayerStatusVsNpc GetPlayerStatus(Hero npc)
      {
         if (npc == null || Hero.MainHero == null)
            return PlayerStatusVsNpc.Unknown;

         // Player is the NPC's captive
         if (Hero.MainHero.IsPrisoner && Hero.MainHero.PartyBelongedToAsPrisoner?.LeaderHero == npc)
            return PlayerStatusVsNpc.Captive;

         // NPC is the player's captive
         if (npc.IsPrisoner && npc.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero)
            return PlayerStatusVsNpc.NpcIsCaptive;

         // Same clan
         if (npc.Clan != null && npc.Clan == Clan.PlayerClan)
            return PlayerStatusVsNpc.ClanMember;

         // Player is a vassal of the NPC's kingdom (and NPC leads it)
         if (Hero.MainHero.Clan?.Kingdom?.Leader == npc && npc != Hero.MainHero)
            return PlayerStatusVsNpc.Liege;

         // NPC is a vassal of the player's kingdom (player leads it)
         if (npc.Clan?.Kingdom?.Leader == Hero.MainHero && Hero.MainHero.Clan?.Kingdom != null)
            return PlayerStatusVsNpc.Vassal;

         return PlayerStatusVsNpc.Free;
      }

      public string GetSeasonName()
      {
         // Bannerlord year = 84 campaign days (21 per season).
         var dayOfYear = (int) (CampaignTime.Now.ToDays % 84);

         if (dayOfYear < 21) return "Spring";
         if (dayOfYear < 42) return "Summer";
         if (dayOfYear < 63) return "Autumn";

         return "Winter";
      }

      public ActionOutcome GrantQuestReward(Hero npcHero, NpcProfile profile, int gold, int relation)
      {
         if (npcHero == null || Hero.MainHero == null || profile == null) return null;

         var parts = new List<string>();

         if (gold > 0)
         {
            int amount = ClampInt(gold, 1, 100000);
            GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount, true);
            parts.Add($"{amount} denars");
         }

         if (relation > 0)
         {
            // Verified reward: applied straight to the personal score, bypassing the
            // RelationGate cooldown/cap — the deed was confirmed by real game events.
            int before = profile.ReputationWithPlayer;
            int after = ClampInt(before + relation, -100, 100);
            profile.ReputationWithPlayer = after;
            profile.LastRelationGainHour = CampaignTime.Now.ToHours;
            if (after != before) parts.Add($"+{after - before} regard");
            ModLog.Info($"[QUEST] reward relation: {npcHero.Name} {before}→{after} (+{relation} promised)");
         }

         if (parts.Count == 0) return null;

         var message = $"{npcHero.Name} rewards you: {string.Join(", ", parts)}.";
         MBInformationManager.AddQuickInformation(new TextObject(message), 0, npcHero.CharacterObject);
         PlaySound(ModSettings.RelationIncreaseSound);
         ModLog.Info($"[QUEST] {message}");

         return new ActionOutcome(message, true);
      }

      // ── Sprint 17: public humiliation standing penalty ────────────────────
      private const int RenownLossPerWitness    = 2;
      private const int InfluenceLossPerWitness = 3;
      private const int MaxRenownLoss           = 12;
      private const int MaxInfluenceLoss        = 18;

      public (int renownLoss, int influenceLoss) ApplyPlayerPublicHumiliation(int witnessCount)
      {
         if (witnessCount <= 0 || Hero.MainHero?.Clan == null) return (0, 0);

         int renownLoss    = ClampInt(witnessCount * RenownLossPerWitness,    0, MaxRenownLoss);
         int influenceLoss = ClampInt(witnessCount * InfluenceLossPerWitness, 0, MaxInfluenceLoss);

         // Only mutate the real campaign when the player is genuinely a prisoner.
         // Console test scenes (player not imprisoned) report the figures without
         // touching the save — so the mechanic can be verified safely.
         bool real = Hero.MainHero.IsPrisoner;
         if (real)
         {
            GainRenownAction.Apply(Hero.MainHero, -renownLoss, doNotNotify: true);
            ChangeClanInfluenceAction.Apply(Hero.MainHero.Clan, -influenceLoss);
         }

         ModLog.Info($"[HUMILIATION] witnesses={witnessCount} renown-{renownLoss} influence-{influenceLoss}" +
                     (real ? "" : " (test mode — not applied)"));

         return (renownLoss, influenceLoss);
      }

      #region private

      private static int ClampInt(int value, int min, int max)
         => value < min ? min : value > max ? max : value;

      private static ActionOutcome ExecuteGiveGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount)) return null;
         if (amount <= 0 || npcHero == null || Hero.MainHero == null) return null;

         amount = ClampInt(amount, 1, 10000);
         GiveGoldAction.ApplyBetweenCharacters(npcHero, Hero.MainHero, amount);
         ModLog.Info($"[ACTION] give_gold: {npcHero.Name} → player {amount} gold");

         return new ActionOutcome($"{npcHero.Name} gives you {amount} denars.", true);
      }

      private static ActionOutcome ExecuteTakeGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount)) return null;
         if (amount <= 0 || npcHero == null || Hero.MainHero == null) return null;

         amount = ClampInt(amount, 1, 10000);
         GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, amount);
         ModLog.Info($"[ACTION] take_gold: player → {npcHero.Name} {amount} gold");

         // If this NPC has an outstanding ProvideGold quest, any payment satisfies it.
         StampProvideGoldIfOutstanding(npcHero, amount);

         return new ActionOutcome($"You give {npcHero.Name} {amount} denars.", false);
      }

      private static void PlaySound(string eventPath)
      {
         if (string.IsNullOrWhiteSpace(eventPath)) return;
         try
         {
            // PlaySound2D is the correct API for UI/2D sounds — it handles
            // non-positional playback without requiring a scene context.
            bool played = SoundEvent.PlaySound2D(eventPath);
            ModLog.Info($"[SOUND] PlaySound2D('{eventPath}') = {played}.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[SOUND] PlaySound failed for '{eventPath}': {ex.Message}");
         }
      }

      private static List<Kingdom> RetrieveAllKingdoms()
      {
         var kingdoms = new List<Kingdom>();
         foreach (Kingdom k in Kingdom.All)
            if (!k.IsEliminated)
               kingdoms.Add(k);

         return kingdoms;
      }

      /// <summary>
      ///   Stamps an outstanding <see cref="QuestType.ProvideGold" /> quest as satisfied
      ///   the moment the player gives any gold to the quest giver. Any non-zero payment
      ///   counts — the LLM can acknowledge whether the amount was sufficient in dialogue.
      /// </summary>
      private static void StampProvideGoldIfOutstanding(Hero npcHero, int amountGiven)
      {
         BannerlordNpcMemoryStore store = NpcMemoryCampaignBehavior.Instance?.Store;

         if (store == null || npcHero == null) return;

         NpcProfile profile = store.Get(npcHero.StringId);

         if (profile == null) return;

         InformalQuest quest = profile.ActiveQuests.FirstOrDefault(q =>
            q.Type == QuestType.ProvideGold && q.IsOutstanding);

         if (quest == null) return;

         var day = (int) CampaignTime.Now.ToDays;
         quest.SatisfiedOnDay = day;
         quest.Evidence = $"Player provided {amountGiven} denars on day {day}.";
         store.Set(profile);

         QuestJournalBridge.NotifyDeedDone(quest, npcHero);
         ModLog.Info($"[QUEST] ProvideGold satisfied: {npcHero.Name} received {amountGiven} denars.");
      }

      private static bool TryGetInt(IReadOnlyDictionary<string, string> parameters, string key, out int value)
      {
         value = 0;

         return parameters.TryGetValue(key, out string raw)
                && int.TryParse(raw, out value);
      }

      private ActionOutcome ExecuteChangeRelation(GameAction action, Hero npcHero, ActionContext context)
      {
         if (!TryGetInt(action.Parameters, "delta", out int proposed)) return null;
         if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

         bool captive = GetPlayerStatus(npcHero) == PlayerStatusVsNpc.NpcIsCaptive;
         int delta = RelationGate.Resolve(
            proposed, context.Profile, context.EmittedEvent, CampaignTime.Now.ToHours, captive);

         if (delta == 0)
         {
            ModLog.Info($"[ACTION] change_relation: proposed {proposed:+#;-#;0} gated to 0 (cooldown/clamp).");

            return null;
         }

         // Apply to OUR per-NPC personal score, NOT Bannerlord's relation. The game's
         // relation is clan-level by design (DiplomacyModel.GetHeroesForEffectiveRelation
         // collapses all clan members to the clan leader), so writing it spreads the
         // change across the whole clan. The personal score lives per-hero on the
         // profile — no spread — and drives the prompt's personal regard + greeting mood.
         int before = context.Profile.ReputationWithPlayer;
         int after = ClampInt(before + delta, -100, 100);
         context.Profile.ReputationWithPlayer = after;
         ModLog.Info($"[ACTION] change_relation (personal): {npcHero.Name} {before}→{after} (delta {delta:+#;-#;0}, proposed {proposed:+#;-#;0})");

         string message = delta > 0
            ? $"{npcHero.Name} regards you more warmly. ({delta:+#;-#;0})"
            : $"{npcHero.Name} regards you more coldly. ({delta:+#;-#;0})";

         // Banner: correct hero name + NPC portrait. No soundEventPath — that
         // parameter does not trigger custom-bank events reliably. Sound below.
         MBInformationManager.AddQuickInformation(
            new TextObject(message), 0, npcHero.CharacterObject);

         // Play the custom sound directly via FMOD event API.
         string soundPath = delta > 0
            ? ModSettings.RelationIncreaseSound
            : ModSettings.RelationDecreaseSound;
         PlaySound(soundPath);

         return new ActionOutcome(message, delta > 0);
      }

      #endregion
   }
}