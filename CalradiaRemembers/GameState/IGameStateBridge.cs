using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

namespace CalradiaRemembers.GameState
{
    /// <summary>One item the player offers to settle an item bargain: a roster item id and how many.</summary>
    public readonly struct ItemOffer
    {
        public ItemOffer(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public string ItemId { get; }
        public int Count { get; }
    }

    /// <summary>
    /// Abstraction over TaleWorlds' campaign state for everything
    /// the mod needs to read from or write to the game world. All game-state
    /// access should go through this interface — no direct <c>Hero.MainHero</c>
    /// or <c>Campaign.Current</c> calls anywhere else in the mod.
    ///
    /// Sprint 10: write operations added (<see cref="ExecuteAction"/>).
    /// </summary>
    public interface IGameStateBridge
    {
        /// <summary>Current diplomatic posture between the player and the NPC's faction.</summary>
        DiplomaticStatus GetDiplomaticStatus(Hero npc);

        /// <summary>The player's structural standing vis-à-vis this NPC (free, captive, vassal, etc.).</summary>
        PlayerStatusVsNpc GetPlayerStatus(Hero npc);

        /// <summary>
        ///   The realm binding the two when one of them RULES it, which grounds the feudal note in the
        ///   prompt. Null when neither rules the other's kingdom. Lives beside
        ///   <see cref="GetPlayerStatus" /> on purpose: resolving it in the caller instead would duplicate
        ///   the "who rules whose realm" test that the two of them share.
        /// </summary>
        string GetSharedRealmName(Hero npc);

        /// <summary>The kind of scene the player is currently in (outdoor, settlement, keep, etc.).</summary>
        SceneType GetCurrentScene();

        /// <summary>
        ///   A short, plain description of what <paramref name="npc"/> is currently doing on
        ///   the campaign map — laying siege, marching with an army toward an objective, or
        ///   traveling to a settlement. Returns null when it cannot be read with confidence:
        ///   feeding a guessed destination would let the prompt endorse the very confabulation
        ///   this is meant to prevent, so uncertainty must produce null, not a best guess.
        /// </summary>
        string GetCurrentActivity(Hero npc);

        /// <summary>The current in-game day (from <c>CampaignTime.Now</c>).</summary>
        int GetCurrentGameDay();

        /// <summary>Current season name: Spring, Summer, Autumn, or Winter.</summary>
        string GetSeasonName();

        /// <summary>
        ///   The current part of the day (e.g. "morning", "afternoon", "evening, around dusk",
        ///   "night") derived from <c>CampaignTime.Now</c>, so a scene's lighting matches vanilla.
        /// </summary>
        string GetTimeOfDay();

        /// <summary>
        ///   Rolls a captive's escape attempt. Returns true on success and COMMITS the outcome
        ///   (irreversible), but defers the actual campaign freeing to <see cref="FinalizePendingEscape"/>
        ///   at scene close so the open chat window is not disrupted. <paramref name="force"/> skips
        ///   the chance roll (console test only).
        /// </summary>
        bool ResolvePlayerEscapeAttempt(bool force);

        /// <summary>
        ///   Console test hook: forces the next <see cref="ResolvePlayerEscapeAttempt"/> to succeed,
        ///   so the full escape flow can be exercised despite the slim natural chance.
        /// </summary>
        void ForceNextEscapeSuccess();

        /// <summary>
        ///   Applies a committed escape (<see cref="EndCaptivityAction"/> by escape) if one is
        ///   pending. Called once when a captive scene closes. No-op otherwise.
        /// </summary>
        void FinalizePendingEscape();

        /// <summary>
        ///   The captor has chosen to free the player. Re-validates the release gate
        ///   (<c>CaptorReleasePolicy</c>) and, on success, COMMITS the freeing but defers the actual
        ///   campaign action to <see cref="FinalizePendingRelease"/> at scene close so the open chat window
        ///   is not disrupted. Returns true when the release took, false when the gate refuses it.
        /// </summary>
        bool CommitCaptorRelease(Hero captor);

        /// <summary>
        ///   Applies a committed captor-granted release (<see cref="EndCaptivityAction"/> by choice) if one
        ///   is pending. Called once when a captive scene closes. No-op otherwise.
        /// </summary>
        void FinalizePendingRelease();

        /// <summary>
        ///   Short summary of active inter-kingdom wars, suitable for injection
        ///   into the world state section of the system prompt.
        ///   Returns an empty string when there are no active wars.
        /// </summary>
        string GetActiveConflicts();

        /// <summary>
        ///   The towns and castles each realm holds and the realms that have fallen, for the world state (Schmidt, Discord
        ///   11/11/2026). Empty when there is nothing to tell.
        /// </summary>
        string GetRealmStanding();

        /// <summary>
        ///   The live game relation between the player and <paramref name="npc"/>
        ///   ([-100, 100]). This is the single authoritative relationship score;
        ///   it is synced into the prompt's CURRENT STANCE each turn. Returns 0 if
        ///   either hero is missing.
        /// </summary>
        int GetPlayerRelation(Hero npc);

        /// <summary>
        ///   Whether the intimacy consent bar permits intimacy to COUNT with <paramref name="npc"/> right now
        ///   (adult-prompt audit M3). The thresholds used to be prose in the prompt that nothing enforced;
        ///   this is the single mechanical answer, read both by the intimacy relation bonus and by the
        ///   romantic arc, so the verdict the model was shown and the rule that runs cannot drift apart.
        ///   Deliberately blind to a past affair: a shared history softens how a refusal is VOICED, it never
        ///   buys a standing exemption. Returns false when the pair cannot be resolved.
        /// </summary>
        bool PermitsIntimacyWith(Hero npc, NpcProfile profile);

        /// <summary>
        ///   Executes a game action emitted by the LLM in an [ACTION] block and
        ///   returns a short outcome to surface in the chat, or null when the action
        ///   changed nothing (unknown type, gated out, invalid parameters).
        /// </summary>
        ActionOutcome ExecuteAction(GameAction action, Hero npcHero, ActionContext context);

        /// <summary>
        ///   Pays the reward promised by a completed informal quest: <paramref name="gold"/>
        ///   denars from the NPC to the player, and <paramref name="relation"/> added to the
        ///   NPC's personal regard. Unlike routine warmth, a verified quest reward bypasses
        ///   the relation cooldown and cap — the deed was confirmed by real game events.
        ///   Returns a combined outcome for the chat, or null when nothing was granted.
        /// </summary>
        ActionOutcome GrantQuestReward(Hero npcHero, NpcProfile profile, int gold, int rewardRelation);

        /// <summary>
        ///   Honors a non-monetary bargain reward (<see cref="RewardGrant" />) the moment its
        ///   deed is verified: the wanderer joins the party, a marriage is consented, and so on.
        ///   The bridge is the law — it re-checks the grant is mechanically possible and
        ///   declines (with a log and a soft chat line) any grant not yet supported. Returns
        ///   the chat outcome, or null when the grant is <see cref="RewardGrant.None" />.
        /// </summary>
        ActionOutcome GrantConditionalReward(Hero npcHero, NpcProfile profile, InformalQuest quest);

        /// <summary>
        ///   Settles an item-barter bargain in one offer: tallies the denar value of the
        ///   <paramref name="offered" /> goods actually held, and — only if it meets the
        ///   quest's required value — removes them from the player's party and executes the
        ///   bargain's reward grant. A short offer removes nothing and asks for more.
        ///   <see cref="ActionOutcome.IsPositive" /> is true exactly when the deal closed.
        /// </summary>
        ActionOutcome SettleItemBargain(NpcProfile giver, Hero giverHero, InformalQuest quest, IReadOnlyList<ItemOffer> offered);

        /// <summary>
        ///   Settles a <see cref="QuestType.ProvideGoods" /> supply task: tallies the COUNT of handed-over
        ///   goods that match the quest's category, and, when the required count is met, hands them over and
        ///   COMPLETES the quest (pays its gold/regard reward, closes the journal). A short delivery removes
        ///   nothing and asks for more; <see cref="ActionOutcome.IsPositive" /> is true only when it closed.
        /// </summary>
        ActionOutcome SettleSupplyQuest(NpcProfile giver, Hero giverHero, InformalQuest quest, IReadOnlyList<ItemOffer> offered);

        /// <summary>
        ///   Completes a troop hand-over task (a garrison reinforced, recruits delivered) once the verb has moved the men:
        ///   stamps the evidence, pays the reward, closes the journal entry, records the memory.
        /// </summary>
        ActionOutcome SettleTroopHandOver(NpcProfile giver, Hero giverHero, InformalQuest quest, TroopHandOver handOver);

        /// <summary>The match some lord has sent the player to broker, when this character has the say over the lady; else null.</summary>
        PendingMatch MatchToArrange(Hero authorizer);

        /// <summary>
        ///   The one with the say over the lady consented: re-checks the law, her heart and her family's consent, and
        ///   weds them, or says why not.
        /// </summary>
        ActionOutcome SettleMatch(Hero authorizer);

        /// <summary>Whether some lord has sent the player to scout this town (an outstanding scout_town task).</summary>
        bool HasScoutTask(TaleWorlds.CampaignSystem.Settlements.Settlement town);

        /// <summary>
        ///   The gang leader told: records the town's true state as the scout_town task's evidence, for its giver, and
        ///   tells the player what he heard.
        /// </summary>
        ActionOutcome SettleScoutTask(Hero informant, TaleWorlds.CampaignSystem.Settlements.Settlement town);

        /// <summary>
        ///   Applies the standing penalty for the player being publicly abased before
        ///   <paramref name="witnessCount"/> onlookers (Sprint 17 captive humiliation).
        ///   The clan loses Renown (prestige) and Influence (political authority),
        ///   scaled by the number of witnesses. The world is only mutated when the player
        ///   is genuinely a prisoner — console test scenes report the figures without
        ///   touching the save. Returns the (renownLoss, influenceLoss) magnitudes for the
        ///   chat message; (0, 0) when there are no witnesses.
        /// </summary>
        (int renownLoss, int influenceLoss) ApplyPlayerPublicHumiliation(int witnessCount);

        /// <summary>
        ///   Resets the captive-scene harm tracker at the start of a captive encounter, so the
        ///   guaranteed end-of-scene toll (<see cref="ApplyForcedSceneToll"/>) can tell whether
        ///   the captor's narration already wounded the player this scene.
        /// </summary>
        void BeginCaptiveSceneTracking();

        /// <summary>
        ///   Marks the captive scene closed at scene end, so harm_prisoner (which deducts REAL health)
        ///   re-validates that a scene is genuinely running before wounding the player. Prevents a stray
        ///   or late emission from harming the player during ordinary captivity conversation.
        /// </summary>
        void EndCaptiveSceneTracking();

        /// <summary>
        ///   Applies the guaranteed body toll for a forced sexual captive scene: if no
        ///   harm_prisoner action wounded the player during the scene, deducts a moderate amount
        ///   of health so an aggression is never bloodless. No-op when harm was already applied.
        ///   Like harm_prisoner itself it always touches health (so console test scenes show the
        ///   effect), clamped to leave the player at 1 HP. Returns the HP actually lost.
        /// </summary>
        int ApplyForcedSceneToll();
    }
}
