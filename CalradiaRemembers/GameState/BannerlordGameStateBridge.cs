// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Quests;
using CalradiaRemembers.Storage;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Actions;
using TaleWorlds.CampaignSystem.Encounters;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Roster;
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
            case "join_party":
               return ExecuteJoinParty(action, npcHero);
            case "join_as_mercenary":
               return ExecuteJoinAsMercenary(npcHero);
            case "join_clan":
               return ExecuteJoinClan(npcHero);
            case "scheme_assist":
               return ExecuteSchemeAssist(npcHero);
            case "scheme_heed":
               return ExecuteSchemeHeed(npcHero);
            case "marry":
               return ExecuteMarry(npcHero, context);
            case "take_as_consort":
               return ExecuteTakeAsConsort(npcHero, context);
            case "give_item":
               return ExecuteGiveItem(action, npcHero, context);
            case "give_prisoner":
               return ExecuteGivePrisoner(npcHero, context);
            case "free_prisoner":
               return ExecuteFreePrisoner(npcHero);
            case "grant_blessing":
               return ExecuteGrantBlessing(action, npcHero);
            case "harm_prisoner":
               return ExecuteHarmPrisoner(action);
            case "impregnation_risk":
               return ExecuteImpregnationRisk(npcHero);
            case "gather_news":
               return ExecuteGatherNews(action, npcHero);
            case "reassure_companion":
               return ExecuteReassureCompanion(npcHero);
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

      public string GetCurrentActivity(Hero npc)
      {
         try
         {
            MobileParty party = npc?.PartyBelongedTo;
            if (party == null) return null; // in a settlement / no party of their own — nothing to assert

            // 1. Actively besieging — the most concrete, unambiguous signal.
            if (party.BesiegedSettlement != null)
               return $"laying siege to {party.BesiegedSettlement.Name}";

            // 2. In an army: prefer the ARMY's strategic objective over this party's own
            //    waypoint — a member party follows a gathering point, not the target, so
            //    reading its own destination would itself produce the wrong place.
            Army army = party.Army;
            if (army != null)
            {
               string leader = army.LeaderParty?.LeaderHero?.Name?.ToString()
                            ?? army.ArmyOwner?.Name?.ToString();

               if (army.LeaderParty?.BesiegedSettlement != null)
                  return $"marching with {leader}'s army, laying siege to {army.LeaderParty.BesiegedSettlement.Name}";

               // AiBehaviorObject is an IMapPoint; only name it when it is a settlement we can
               // point to — chasing a roaming party yields no place, so stay vague there.
               Settlement objective = army.AiBehaviorObject as Settlement;
               if (objective != null)
                  return $"marching with {leader}'s army toward {objective.Name}";

               return leader != null ? $"marching with {leader}'s army" : null;
            }

            // 3. A lone party with a set destination.
            if (party.TargetSettlement != null)
               return $"traveling toward {party.TargetSettlement.Name}";

            return null;
         }
         catch (Exception ex)
         {
            ModLog.Error($"GetCurrentActivity failed for {npc?.Name}: {ex.Message}");
            return null;
         }
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

      /// <inheritdoc />
      public string GetTimeOfDay()
      {
         // CurrentHourInDay is 0–24. Map to a part of day so the scene's light matches vanilla.
         float hour = CampaignTime.Now.CurrentHourInDay;
         if (hour < 5f)  return "deep night";
         if (hour < 8f)  return "early morning, around dawn";
         if (hour < 12f) return "morning";
         if (hour < 14f) return "midday";
         if (hour < 18f) return "afternoon";
         if (hour < 21f) return "evening, around dusk";
         return "night";
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

         // The map bar does not refresh while campaign time is frozen under the chat
         // overlay — showing the balance here is the player's only live confirmation.
         return new ActionOutcome($"{npcHero.Name} gives you {amount} denars (purse: {Hero.MainHero.Gold}).", true);
      }

      private static ActionOutcome ExecuteTakeGold(GameAction action, Hero npcHero)
      {
         if (!TryGetInt(action.Parameters, "amount", out int amount)) return null;
         if (amount <= 0 || Hero.MainHero == null) return null;

         // Never take more than the player actually carries (bandits simply empty the purse).
         amount = Math.Min(ClampInt(amount, 1, 10000), Hero.MainHero.Gold);
         if (amount <= 0) return null;

         if (npcHero != null)
         {
            GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, amount);
            StampProvideGoldIfOutstanding(npcHero, amount); // any payment satisfies a ProvideGold quest
            ModLog.Info($"[ACTION] take_gold: player → {npcHero.Name} {amount} gold");
            return new ActionOutcome($"You give {npcHero.Name} {amount} denars (purse: {Hero.MainHero.Gold}).", false);
         }

         // No hero recipient (e.g. a synthesized bandit captor) — the coin is simply taken.
         Hero.MainHero.ChangeHeroGold(-amount);
         ModLog.Info($"[ACTION] take_gold: {amount} gold extorted (no hero recipient).");
         return new ActionOutcome($"They take {amount} denars from you (purse: {Hero.MainHero.Gold}).", false);
      }

      /// <summary>
      ///   harm_prisoner: a captor inflicts real physical harm on the player during a captive
      ///   scene — a beating, a cut, a maiming, or the bruising toll of being taken by force.
      ///   Translates the narrated violence into actual lost health so captivity carries a body
      ///   cost. Only ever taught in the captive prompt rules (menace and spoil scenes), so a
      ///   normal NPC never emits it. Clamped to leave the player at 1 HP — a scene wounds, it
      ///   never kills (death from a scripted scene would be jarring and end the campaign).
      /// </summary>
      // A captive scene wounds but must never kill, nor whittle the player down to nothing when a
      // weak model emits harm_prisoner every single turn. Two guards: a per-scene cap on the number
      // of wounds that bite, and a HP floor well above death. Both reset at each scene's start.
      private const int MinSceneHitPoints  = 15; // a scene never drives the player below this
      private const int MaxHarmsPerScene   = 5;  // wounds beyond this are narrated but cost no HP
      private static int _sceneHarmCount;

      private static ActionOutcome ExecuteHarmPrisoner(GameAction action)
      {
         Hero player = Hero.MainHero;
         if (player == null) return null;

         action.Parameters.TryGetValue("severity", out string severity);
         int damage;
         switch ((severity ?? string.Empty).Trim().ToLowerInvariant())
         {
            case "severe":
            case "grievous":
               damage = 30;
               break;
            case "moderate":
            case "heavy":
               damage = 15;
               break;
            default: // light, mild, or unspecified
               damage = 6;
               break;
         }

         // Weak models sometimes emit harm_prisoner on every turn regardless of the fiction.
         // Past the per-scene cap, or once the player is at the floor, the wound is fiction-only.
         if (_sceneHarmCount >= MaxHarmsPerScene || player.HitPoints <= MinSceneHitPoints)
         {
            _captiveHarmApplied = true;
            ModLog.Info($"[ACTION] harm_prisoner suppressed (cap/floor reached; severity '{severity}', HP {player.HitPoints}).");
            return null;
         }

         int before = player.HitPoints;
         player.HitPoints = Math.Max(MinSceneHitPoints, before - damage); // a scene wounds, never kills
         int lost = before - player.HitPoints;
         if (lost <= 0) return null;

         _sceneHarmCount++;
         _captiveHarmApplied = true; // the guaranteed end-of-scene toll defers to this
         ModLog.Info($"[ACTION] harm_prisoner: player loses {lost} HP (severity '{severity}', now {player.HitPoints}, scene wound #{_sceneHarmCount}).");
         return new ActionOutcome($"You are hurt — you lose {lost} health.", false);
      }

      /// <summary>
      ///   impregnation_risk: the captor's narration flagged a vaginal act carried to completion.
      ///   A female player of fertile age has a small chance to conceive. The father is the real
      ///   captor when there is one (a lord); a faceless bandit's child is attributed to a shared
      ///   hidden "Unknown" hero. Silent to the player — the pregnancy is discovered through normal
      ///   game means, never announced in the chat — so this returns no chat outcome.
      /// </summary>
      private static ActionOutcome ExecuteImpregnationRisk(Hero captorHero)
      {
         CaptivePregnancyService.TryConceive(captorHero);
         return null;
      }

      // Set when a harm_prisoner action wounds the player during the current captive scene, so
      // ApplyForcedSceneToll knows not to double up. Reset at each scene's start.
      private static bool _captiveHarmApplied;

      /// <inheritdoc />
      public void BeginCaptiveSceneTracking()
      {
         _captiveHarmApplied = false;
         _sceneHarmCount     = 0;
         _pendingEscape      = false;
      }

      // ── Captive escape ─────────────────────────────────────────────────────

      private const float EscapeChance = 0.10f; // a slim, deterministic chance — fate, not the LLM
      private static bool _pendingEscape;
      private static bool _forceNextEscape; // console test hook (cr.escape_force)

      /// <inheritdoc />
      public void ForceNextEscapeSuccess() => _forceNextEscape = true;

      /// <inheritdoc />
      public bool ResolvePlayerEscapeAttempt(bool force)
      {
         if (_forceNextEscape) { _forceNextEscape = false; force = true; }
         if (!force && MBRandom.RandomFloat >= EscapeChance) return false;

         // Commit the outcome NOW (irreversible), but defer the actual campaign action to scene
         // close: freeing mid-chat could pop an encounter/menu and disrupt the open scene window.
         _pendingEscape = true;
         ModLog.Info("[ESCAPE] attempt SUCCEEDED — freeing committed (applied at scene close).");
         return true;
      }

      /// <inheritdoc />
      public void FinalizePendingEscape()
      {
         if (!_pendingEscape) return;
         _pendingEscape = false;

         Hero player = Hero.MainHero;
         if (player == null || !player.IsPrisoner) return;

         try
         {
            EndCaptivityAction.ApplyByEscape(player, null, true);
            ModLog.Info("[ESCAPE] EndCaptivityAction.ApplyByEscape applied — player is free.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ESCAPE] freeing failed: {ex.Message}");
         }
      }

      /// <inheritdoc />
      public int ApplyForcedSceneToll()
      {
         Hero player = Hero.MainHero;
         if (player == null || _captiveHarmApplied) return 0; // the captor's violence already wounded them

         const int forcedSceneDamage = 12; // the bruising cost of being taken by force
         int before = player.HitPoints;
         player.HitPoints = Math.Max(MinSceneHitPoints, before - forcedSceneDamage);
         int lost = before - player.HitPoints;
         if (lost <= 0) return 0;

         _captiveHarmApplied = true;
         ModLog.Info($"[CAPTIVE] forced-scene toll: player loses {lost} HP (now {player.HitPoints}).");
         return lost;
      }

      /// <summary>
      ///   join_as_mercenary: a lord has offered — and the player has agreed — to enroll
      ///   the player's clan as paid mercenary swords under his kingdom's banner. The
      ///   prompt only teaches this action when the game confirms the player is free of
      ///   any existing kingdom obligation (ResolveMercenaryOfferKingdom), but every guard
      ///   is re-checked here — the bridge is the law.
      /// </summary>
      private static ActionOutcome ExecuteJoinAsMercenary(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Kingdom kingdom = npcHero.Clan?.Kingdom;
            if (kingdom == null)
               return new ActionOutcome($"({npcHero.Name} does not belong to a kingdom.)", false);

            Clan playerClan = Clan.PlayerClan;
            if (playerClan == null) return null;

            if (playerClan.Kingdom != null)
               return new ActionOutcome(
                  $"(Your clan is already sworn to {playerClan.Kingdom.Name}. " +
                  "Settle that obligation first.)", false);

            // shouldStayInKingdomUntil = Now (no forced lock-in beyond the standard contract logic)
            // awardMultiplier = 1 (standard mercenary wage)
            ChangeKingdomAction.ApplyByJoinFactionAsMercenary(
               playerClan, kingdom, CampaignTime.Now, awardMultiplier: 1, showNotification: true);

            ModLog.Info($"[ACTION] join_as_mercenary: player clan joined {kingdom.Name} as mercenary.");

            return new ActionOutcome(
               $"Your clan takes service under {kingdom.Name}'s banner as hired swords.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_as_mercenary failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   join_clan: a lord has agreed, in conversation, to leave their own house and join the
      ///   player's clan — the most powerful recruitment. Only taught when the game confirms every
      ///   gate (deep trust, the player's house outranking theirs, the lord being vulnerable — see
      ///   <see cref="LordRecruitmentFactsBuilder"/> + <see cref="LordRecruitment"/>), but the whole
      ///   gate is re-checked here: the prompt is advice, the bridge is law. The lord's old clan
      ///   loses a member and its faction cools toward the player — vanilla consequences left to play out.
      /// </summary>
      private static ActionOutcome ExecuteJoinClan(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            Clan playerClan = Clan.PlayerClan;
            if (playerClan == null) return null;

            Logic.LordRecruitmentFacts facts = LordRecruitmentFactsBuilder.Build(npcHero);
            if (facts == null || !Logic.LordRecruitment.IsEligible(facts))
               return new ActionOutcome($"({npcHero.Name} will not leave their house to join yours — not yet.)", false);

            Clan oldClan = npcHero.Clan;

            // Pull them out of any kingdom obligation first, then move them under the player's banner.
            if (oldClan?.Kingdom != null && oldClan.Leader == npcHero && oldClan.Kingdom != playerClan.Kingdom)
               return new ActionOutcome(
                  $"({npcHero.Name} leads a clan sworn to {oldClan.Kingdom.Name}; such a defection cannot be sealed in mere conversation.)", false);

            npcHero.Clan = playerClan;

            ModLog.Info($"[ACTION] join_clan: {npcHero.Name} left {oldClan?.Name?.ToString() ?? "their house"} to join the player's clan.");

            return new ActionOutcome(
               $"{npcHero.Name} forsakes the house of {oldClan?.Name?.ToString() ?? "their birth"} and swears to your clan.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_clan failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   scheme_assist: the NPC (a plotter) has sounded the player out and the player has agreed to
      ///   abet their secret scheme against a rival. Only taught when the game confirms a recruitable
      ///   plot (SchemeStore.GetRecruitableTargetName), but re-validated here: the store re-checks the
      ///   gate, advances the plot, and the plotter warms to the player for throwing in.
      /// </summary>
      private static ActionOutcome ExecuteSchemeAssist(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            string result = WorldEvents.SchemeStore.Instance?.PlayerAssists(npcHero);
            if (string.IsNullOrEmpty(result))
               return new ActionOutcome("(There is no plot of theirs you could help with just now.)", false);

            // Throwing in together warms the plotter toward the player.
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, Hero.MainHero, 3, false);
            ModLog.Info($"[ACTION] scheme_assist: player aided {npcHero.Name}'s scheme.");

            return new ActionOutcome(result, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] scheme_assist failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   scheme_heed: the player has warned this NPC of a secret plot against them, and the NPC
      ///   heeds it. Only taught when the game confirms such a plot (SchemeStore.HasSecretSchemeAgainst),
      ///   but re-validated: the store exposes the scheme (giving the target a chance to counter it on
      ///   later ticks) and the grateful target warms to the player.
      /// </summary>
      private static ActionOutcome ExecuteSchemeHeed(Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            string result = WorldEvents.SchemeStore.Instance?.TargetHeeds(npcHero);
            if (string.IsNullOrEmpty(result))
               return new ActionOutcome("(There is no plot against them you could warn of just now.)", false);

            // Gratitude for the warning warms the target toward the player.
            ChangeRelationAction.ApplyRelationChangeBetweenHeroes(npcHero, Hero.MainHero, 4, false);
            ModLog.Info($"[ACTION] scheme_heed: player warned {npcHero.Name} of a plot against them.");

            return new ActionOutcome(result, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] scheme_heed failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   marry: the NPC has proposed marriage to the player in conversation and the player
      ///   has accepted. Guards are re-checked here (the bridge is the law) before calling
      ///   <see cref="MarriageAction.Apply" />. The mod's internal reputation score (≥50) is
      ///   the gating threshold, matching <see cref="EncounterContextBuilder.ResolveLoveMatchEligible" />.
      /// </summary>
      private static ActionOutcome ExecuteMarry(Hero npcHero, ActionContext context)
      {
         try
         {
            Hero player = Hero.MainHero;
            if (player == null || npcHero == null) return null;

            if (player.Spouse != null)
               return new ActionOutcome(
                  "(You are already wed — no second marriage can be sealed in law.)", false);

            if (npcHero.Clan == Clan.PlayerClan)
               return new ActionOutcome(
                  $"({npcHero.Name} belongs to your own clan — custom forbids it.)", false);

            if (!Marriage.MarriageProspectResolver.IsMarriageable(player, npcHero))
               return new ActionOutcome(
                  $"({npcHero.Name} cannot be wed to you under the laws of Calradia.)", false);

            int reputation = context?.Profile?.ReputationWithPlayer ?? 0;
            if (reputation < 50)
               return new ActionOutcome(
                  $"({npcHero.Name} does not yet hold you dear enough to take this step.)", false);

            MarriageAction.Apply(player, npcHero, true);
            ModLog.Info($"[ACTION] marry: player wed {npcHero.Name}.");

            return new ActionOutcome(
               $"You and {npcHero.Name} are joined in marriage — bound together before Calradia.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] marry failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   take_as_consort: both parties have openly named the committed bond between them.
      ///   Unlike legal marriage, this does not call <see cref="MarriageAction.Apply"/> —
      ///   it is purely mod-tracked. Sets <c>IsConsort = true</c> and
      ///   <c>Status = Committed</c> on the NPC's romantic profile.
      ///   The player may already be wed; consorts are not bound by legal exclusivity.
      /// </summary>
      private static ActionOutcome ExecuteTakeAsConsort(Hero npcHero, ActionContext context)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null || context?.Profile == null) return null;

            NpcProfile profile = context.Profile;

            if (profile.Romantic == null)
               return new ActionOutcome($"({npcHero.Name} has no romantic connection to build upon.)", false);

            if (profile.Romantic.Status < RomanticStatus.Intimate)
               return new ActionOutcome($"({npcHero.Name} is not yet close enough to you for this bond.)", false);

            if (profile.Romantic.IsConsort || profile.Romantic.Status == RomanticStatus.Committed)
               return new ActionOutcome($"(You and {npcHero.Name} are already bound as consorts.)", false);

            profile.Romantic.Status    = RomanticStatus.Committed;
            profile.Romantic.IsConsort = true;

            ModLog.Info($"[ACTION] take_as_consort: {npcHero.Name} is now the player's consort.");

            return new ActionOutcome(
               $"You and {npcHero.Name} are bound as consorts — a commitment not named by law, but no less real.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] take_as_consort failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   join_party: a recruitable wanderer agrees in conversation to take service
      ///   with the player. The action is only taught when the game says the hire can
      ///   succeed (see EncounterContextBuilder.ResolveCompanionAskingPrice), but every
      ///   guard is re-checked here — the prompt is advice, the bridge is law. The agreed
      ///   price is clamped to a band around the vanilla hiring price (floor 75 %, cap
      ///   125 %): eloquence negotiates, it never breaks the hiring economy. Executes the
      ///   same native actions as a vanilla tavern hire, so clan limits, party rosters
      ///   and gold flows behave identically.
      /// </summary>
      private static ActionOutcome ExecuteJoinParty(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null || Hero.MainHero == null) return null;

            if (!npcHero.IsAlive || npcHero.IsPrisoner
                || npcHero.Occupation != Occupation.Wanderer
                || npcHero.CompanionOf != null || npcHero.PartyBelongedTo != null)
               return new ActionOutcome($"({npcHero.Name} is not free to take service.)", false);

            Clan clan = Clan.PlayerClan;
            if (clan == null) return null;
            if (clan.Companions.Count() >= clan.CompanionLimit)
               return new ActionOutcome(
                  $"(Your clan cannot support another companion — limit {clan.CompanionLimit}.)", false);

            int asking = Campaign.Current.Models.CompanionHiringPriceCalculationModel
                                 .GetCompanionHiringPrice(npcHero);
            if (asking <= 0) asking = 1;

            TryGetInt(action.Parameters, "price", out int proposed);
            if (proposed <= 0) proposed = asking;
            int price = ClampInt(proposed, (int) (asking * 0.75f), Math.Max(1, (int) (asking * 1.25f)));

            if (Hero.MainHero.Gold < price)
               return new ActionOutcome($"(You cannot afford the agreed {price} denars.)", false);

            return DoJoin(npcHero, price,
               $"hired for {price} denars (proposed {proposed}, vanilla asking {asking})");
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] join_party failed: {ex.GetType().Name}: {ex.Message}");

            return null;
         }
      }

      /// <summary>
      ///   Performs the actual hire — the native actions shared by the cash hire and the
      ///   in-kind bargain. The caller owns the price logic and affordability check; this
      ///   re-validates the NPC is genuinely recruitable (the bridge is the law) and then
      ///   transfers any cash, adds the companion to the clan, and moves them into the party.
      ///   A price of 0 means the payment was made some other way (goods already handed over).
      /// </summary>
      private static ActionOutcome DoJoin(Hero npcHero, int price, string logDetail)
      {
         if (!RecruitmentPricing.IsRecruitable(npcHero))
            return new ActionOutcome($"({npcHero?.Name} is not free to take service.)", false);

         Clan clan = Clan.PlayerClan;
         int goldBefore = Hero.MainHero.Gold;
         if (price > 0) GiveGoldAction.ApplyBetweenCharacters(Hero.MainHero, npcHero, price);
         AddCompanionAction.Apply(clan, npcHero);
         AddHeroToPartyAction.Apply(npcHero, MobileParty.MainParty);

         ModLog.Info($"[ACTION] join_party: {npcHero.Name} {logDetail}; " +
                     $"player gold {goldBefore} → {Hero.MainHero.Gold}.");

         string note = price > 0
            ? $"{price} denars — purse: {Hero.MainHero.Gold}"
            : "paid in goods";
         return new ActionOutcome($"{npcHero.Name} joins your party ({note}).", true);
      }

      public ActionOutcome GrantConditionalReward(Hero npcHero, NpcProfile profile, InformalQuest quest)
      {
         if (quest == null || quest.Reward == RewardGrant.None || npcHero == null) return null;

         try
         {
            switch (quest.Reward)
            {
               case RewardGrant.JoinParty:
                  return DoJoin(npcHero, 0, "joined the player as the agreed reward (paid in deed)");

               case RewardGrant.MarriageConsent:
                  return GrantMarriageConsent(npcHero, quest);

               // Recognized but not yet executable — declined cleanly rather than faked.
               // The quest factory refuses to issue these, so this is a defensive backstop.
               case RewardGrant.GiveItem:
               case RewardGrant.GiveTroops:
               case RewardGrant.ReleasePrisoner:
                  ModLog.Info($"[GRANT] '{quest.Reward}' not yet supported — declined for {npcHero.Name}.");
                  return new ActionOutcome($"({npcHero.Name} cannot honor that kind of bargain yet.)", false);

               default:
                  return null;
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"[GRANT] {quest.Reward} failed for {npcHero?.Name}: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   marriage_consent: the conditioning deed is verified done — record the family's blessing
      ///   for the intended spouse (the consequence resolver later honors it as an alliance when the
      ///   wedding happens). The spouse was validated at issuance; we re-resolve them by id here.
      /// </summary>
      private static ActionOutcome GrantMarriageConsent(Hero npcHero, InformalQuest quest)
      {
         if (Marriage.MarriageApprovalBehavior.Instance == null)
            return new ActionOutcome($"({npcHero.Name} cannot formalize the betrothal just now.)", false);

         Hero spouse = string.IsNullOrEmpty(quest.MarriageSpouseId)
            ? null
            : Hero.FindFirst(h => h.StringId == quest.MarriageSpouseId);
         if (spouse == null)
            return new ActionOutcome($"({npcHero.Name}: the match can no longer be honored.)", false);

         int day = (int) CampaignTime.Now.ToDays;
         Marriage.MarriageApprovalBehavior.Instance.Grant(spouse, npcHero, day);
         ModLog.Info($"[GRANT] marriage_consent: {npcHero.Name} blesses the player's marriage to {spouse.Name} (deed done).");
         return new ActionOutcome(
            $"{npcHero.Name} gives their blessing — you have earned {spouse.Name}'s hand. Their house stands with you.", true);
      }

      public ActionOutcome SettleItemBargain(NpcProfile giver, Hero giverHero,
                                             InformalQuest quest, IReadOnlyList<ItemOffer> offered)
      {
         try
         {
            if (quest == null || quest.Type != QuestType.DeliverItems) return null;
            if (Hero.MainHero == null || offered == null || offered.Count == 0) return null;

            ItemRoster roster = MobileParty.MainParty?.ItemRoster;
            if (roster == null) return null;

            // Tally only what the player genuinely still holds; the bridge trusts the roster,
            // not the caller's value claim.
            var toRemove = new List<(ItemObject item, int count)>();
            var total = 0;
            foreach (ItemOffer offer in offered)
            {
               if (string.IsNullOrEmpty(offer.ItemId) || offer.Count <= 0) continue;
               ItemObject item = FindRosterItem(roster, offer.ItemId, out int available);
               if (item == null || available <= 0) continue;
               int take = Math.Min(offer.Count, available);
               total += item.Value * take;
               toRemove.Add((item, take));
            }

            if (total < quest.RequiredItemValue)
               return new ActionOutcome(
                  $"({giverHero?.Name}: these goods are worth about {total} denars — I asked for " +
                  $"{quest.RequiredItemValue}. Bring more.)", false);

            // Value met: hand the goods over, then honor the favor.
            foreach ((ItemObject item, int count) in toRemove)
               roster.AddToCounts(item, -count);

            ModLog.Info($"[BARGAIN] {giverHero?.Name}: accepted goods worth {total} denars " +
                        $"(required {quest.RequiredItemValue}) for {quest.Reward}.");

            ActionOutcome grant = GrantConditionalReward(giverHero, giver, quest);
            return grant ?? new ActionOutcome($"{giverHero?.Name} accepts your goods.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[BARGAIN] SettleItemBargain failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>Finds an item in the roster by its StringId, reporting how many the player holds.</summary>
      private static ItemObject FindRosterItem(ItemRoster roster, string itemId, out int available)
      {
         available = 0;
         foreach (ItemRosterElement el in roster)
         {
            ItemObject item = el.EquipmentElement.Item;
            if (item == null || item.StringId != itemId || el.Amount <= 0) continue;
            available = el.Amount;
            return item;
         }
         return null;
      }

      /// <summary>
      ///   give_item: transfers one unit of the named item from the player's party
      ///   inventory to the NPC. The item name must match a roster entry exactly
      ///   (case-insensitive). Quest items are always refused — this guard sits on both
      ///   the prompt side (picker excludes them) and here, so the bridge is the law.
      /// </summary>
      private static ActionOutcome ExecuteGiveItem(GameAction action, Hero npcHero, ActionContext context)
      {
         try
         {
            if (MobileParty.MainParty?.ItemRoster == null) return null;

            action.Parameters.TryGetValue("item", out string itemName);
            string itemId = context?.OfferedItemId; // the exact item the player picked from the bag, if any
            if (string.IsNullOrWhiteSpace(itemName) && string.IsNullOrWhiteSpace(itemId)) return null;

            // Prefer the exact item the player picked from the bag (by id); fall back to a name match
            // for a gift offered purely in free text. The id path is immune to the LLM rewording the name.
            ItemRosterElement found = !string.IsNullOrWhiteSpace(itemId)
               ? FindRosterElementById(itemId)
               : FindItemInRoster(itemName);

            if (found.EquipmentElement.Item == null)
            {
               string label = !string.IsNullOrWhiteSpace(itemName) ? itemName : itemId;
               return new ActionOutcome($"(You don't seem to have '{label}' to give.)", false);
            }

            MobileParty.MainParty.ItemRoster.AddToCounts(found.EquipmentElement, -1);
            string actualName = found.EquipmentElement.Item.Name.ToString();
            int value = found.EquipmentElement.Item.Value;

            Stance.StanceService.OnGiftReceived(npcHero); // a freely given gift builds a little trust
            ModLog.Info($"[ACTION] give_item: player gave '{actualName}' (value {value}) to {npcHero?.Name}");
            return new ActionOutcome($"You give {actualName} to {npcHero?.Name}.", false);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_item failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   grant_blessing: the conversation NPC, as head of their clan, consents to the player
      ///   marrying one of their unwed kin (named in the action) or themselves. The bridge is the
      ///   law — it re-checks the NPC's authority and the match's eligibility before recording the
      ///   family's blessing, which the marriage-consequence resolver later honors as an alliance.
      /// </summary>
      private static ActionOutcome ExecuteGrantBlessing(GameAction action, Hero npcHero)
      {
         try
         {
            if (Marriage.MarriageApprovalBehavior.Instance == null || npcHero == null) return null;
            if (Hero.MainHero?.Spouse != null)
               return new ActionOutcome("(You are already wed — the blessing cannot apply.)", false);

            if (!action.Parameters.TryGetValue("hero", out string spouseName)
                || string.IsNullOrWhiteSpace(spouseName)) return null;

            Hero spouse = Marriage.MarriageProspectResolver.ResolveCandidate(npcHero, spouseName);
            if (spouse == null)
               return new ActionOutcome($"({npcHero.Name} names no one of their house by '{spouseName}'.)", false);

            if (!Marriage.MarriageProspectResolver.HasAuthority(npcHero, spouse))
               return new ActionOutcome($"({npcHero.Name} has no authority to bless that match.)", false);

            if (!Marriage.MarriageProspectResolver.IsMarriageable(Hero.MainHero, spouse))
               return new ActionOutcome($"({spouse.Name} is not free to be matched with you.)", false);

            int day = (int) CampaignTime.Now.ToDays;
            Marriage.MarriageApprovalBehavior.Instance.Grant(spouse, npcHero, day);

            string msg = $"{npcHero.Name} blesses a match between you and {spouse.Name}. Their house will favor the union.";
            MBInformationManager.AddQuickInformation(new TextObject(msg), 0, npcHero.CharacterObject);
            ModLog.Info($"[ACTION] grant_blessing: {npcHero.Name} consented to the player marrying {spouse.Name}.");
            return new ActionOutcome(msg, true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] grant_blessing failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      private static ItemRosterElement FindItemInRoster(string itemName)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return default;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;
            if (item == null) continue;
            if (string.Equals(item.Name.ToString(), itemName, StringComparison.OrdinalIgnoreCase))
               return el;
         }
         return default;
      }

      /// <summary>
      ///   free_prisoner: a captive lord the PLAYER holds has struck a bargain for their freedom and the
      ///   player agreed — release them. The reward they promised (goodwill, a ransom, or service) rides
      ///   alongside as the LLM's own change_relation / give_gold / join_clan actions. Guarded: only a
      ///   hero the player actually holds prisoner is freed, so a stray emission can never let one escape.
      /// </summary>
      private static ActionOutcome ExecuteFreePrisoner(Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            if (!npcHero.IsPrisoner || npcHero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero)
               return new ActionOutcome($"({npcHero.Name} is not yours to set free.)", false);

            EndCaptivityAction.ApplyByReleasedByChoice(npcHero, Hero.MainHero);
            Stance.StanceService.OnGiftReceived(npcHero); // freedom is a gift — a little trust is earned
            ModLog.Info($"[ACTION] free_prisoner: player released {npcHero.Name} from captivity.");
            return new ActionOutcome($"You set {npcHero.Name} free.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] free_prisoner failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>
      ///   gather_news action: the player asked one of their companions to ride out and bring back word of
      ///   the realm, and the companion agreed. Dispatches the errand (the behavior re-validates every guard
      ///   — the prompt is advice, the bridge is law) and reports back to the player.
      /// </summary>
      private static ActionOutcome ExecuteGatherNews(GameAction action, Hero npcHero)
      {
         try
         {
            if (npcHero == null) return null;
            Missions.CompanionMissionBehavior behavior = Missions.CompanionMissionBehavior.Instance;
            if (behavior == null) return null;

            // Optional topic the LLM extracted from the player's request ("news about Sturgia" → Sturgia);
            // null means any/nearest town. The behavior resolves it to a destination.
            string about = null;
            action.Parameters?.TryGetValue("about", out about);

            // Which errand the player asked for (news / scout / steal / trade / envoy); defaults to news.
            string errand = null;
            action.Parameters?.TryGetValue("errand", out errand);

            (bool dispatched, string message) = behavior.Dispatch(npcHero, MapErrand(errand), about);
            ModLog.Info($"[ACTION] gather_news: {npcHero.Name} → {(dispatched ? "dispatched" : "refused")}: {message}");
            return new ActionOutcome(message, dispatched);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] gather_news failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
      }

      /// <summary>Maps the LLM's 'errand' keyword to a mission type; anything unrecognized falls back to news.</summary>
      private static Logic.CompanionMissionType MapErrand(string errand)
      {
         switch ((errand ?? "").Trim().ToLowerInvariant())
         {
            case "scout":
            case "spy":   return Logic.CompanionMissionType.Spy;
            case "steal": return Logic.CompanionMissionType.Steal;
            case "trade":
            case "barter": return Logic.CompanionMissionType.Barter;
            case "envoy": return Logic.CompanionMissionType.Envoy;
            default:      return Logic.CompanionMissionType.GatherNews;
         }
      }

      /// <summary>
      ///   reassure_companion action: the player sincerely addressed an unhappy companion's grievance, so
      ///   they soften a little (the WellTreated happiness driver). Emitted only by a companion who was
      ///   voicing discontent; harmless on anyone else (the driver no-ops without a profile).
      /// </summary>
      private static ActionOutcome ExecuteReassureCompanion(Hero npcHero)
      {
         if (npcHero == null) return null;
         Missions.CompanionHappinessService.ApplyDriver(npcHero, Logic.HappinessDriver.WellTreated);
         ModLog.Info($"[ACTION] reassure_companion: {npcHero.Name} is a little more at ease.");
         return new ActionOutcome($"{npcHero.Name} seems a little more at ease in your service.", true);
      }

      /// <summary>Finds a roster element by the item's StringId (the bag-picked gift path).</summary>
      private static ItemRosterElement FindRosterElementById(string itemId)
      {
         if (MobileParty.MainParty?.ItemRoster == null) return default;
         foreach (ItemRosterElement el in MobileParty.MainParty.ItemRoster)
         {
            if (el.Amount <= 0) continue;
            ItemObject item = el.EquipmentElement.Item;
            if (item != null && item.StringId == itemId) return el;
         }
         return default;
      }

      /// <summary>
      ///   give_prisoner: the player hands over the captive an outstanding deliver_prisoner bargain named.
      ///   The deed names the target, so the bridge resolves which held prisoner satisfies it (no picker),
      ///   transfers them to the lord, and settles the bargain — reward, favour, journal close — in one
      ///   turn (mirrors SettleItemBargain). A spurious emission (no bargain, or the captive not held) is
      ///   reported, never faked.
      /// </summary>
      private ActionOutcome ExecuteGivePrisoner(Hero npcHero, ActionContext context)
      {
         try
         {
            NpcProfile profile = context?.Profile;
            if (profile == null || npcHero == null) return null;

            InformalQuest quest = profile.ActiveQuests?.FirstOrDefault(q =>
               q.Type == QuestType.DeliverPrisoner && q.IsOutstanding);
            if (quest == null) return null; // no such bargain — ignore a stray emission

            var target = new PrisonerDeedTarget(quest.TargetHeroId, quest.TargetFactionId);

            CharacterObject prisonerChar = null;
            Hero prisonerHero = null;
            TroopRoster prison = MobileParty.MainParty?.PrisonRoster;
            if (prison != null)
               foreach (TroopRosterElement el in prison.GetTroopRoster())
               {
                  Hero h = el.Character?.HeroObject;
                  if (h != null && target.Matches(h.StringId, h.MapFaction?.StringId))
                  {
                     prisonerChar = el.Character;
                     prisonerHero = h;
                     break;
                  }
               }

            if (prisonerChar == null)
               return new ActionOutcome($"(You do not hold {quest.TargetHeroName ?? "the captive"} I asked for.)", false);

            // Hand the captive to the lord's party; if they ride with none, take them off the player's hands.
            PartyBase dest = npcHero.PartyBelongedTo?.Party;
            if (dest != null) TransferPrisonerAction.Apply(prisonerChar, MobileParty.MainParty.Party, dest);
            else MobileParty.MainParty.PrisonRoster.AddToCounts(prisonerChar, -1);

            int today = (int) CampaignTime.Now.ToDays;
            quest.SatisfiedOnDay = today;
            quest.Status = QuestStatus.Completed;
            quest.Evidence = $"Delivered {prisonerHero.Name} to {npcHero.Name} on day {today}.";

            ActionOutcome reward = GrantQuestReward(npcHero, profile, quest.RewardGold, quest.RewardRelation);
            ActionOutcome grant = quest.Reward != RewardGrant.None
               ? GrantConditionalReward(npcHero, profile, quest)
               : null;

            Stance.StanceService.OnPromiseKept(profile); // a kept bargain earns trust
            QuestJournalBridge.CloseAsCompleted(quest, npcHero);
            ModLog.Info($"[ACTION] give_prisoner: player delivered {prisonerHero.Name} to {npcHero.Name} ({quest.Reward}).");

            return grant ?? reward ?? new ActionOutcome($"{npcHero.Name} takes custody of {prisonerHero.Name}.", true);
         }
         catch (Exception ex)
         {
            ModLog.Error($"[ACTION] give_prisoner failed: {ex.GetType().Name}: {ex.Message}");
            return null;
         }
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
         int delta = Logic.RelationGate.Resolve(
            proposed, context.Profile, context.EmittedEvent, CampaignTime.Now.ToHours, captive,
            ModSettings.RelationWarmth);

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