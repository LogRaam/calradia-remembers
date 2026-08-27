// Code written by Gabriel Mailhot, 06/07/2026.
// Nemesis pillar increment 2 (REDIRECT, R2 execute / R3 torture entry point): the prior pass built a
// recurring daily-tick popup menu (Torture / Execute / Leave) for a nemesis held prisoner. Gabriel
// redirected: no clickable menu. Instead the player takes the initiative, from the party-screen
// prisoner "Talk" action, exactly like talking to any other prisoner hero. Vanilla already lets the
// player converse with a hero prisoner they hold (PartyCharacterVM.ExecuteTalk -> PartyVM opens a
// real conversation, which reaches the ordinary "hero_main_options" hub the same as any other hero
// conversation; LordConversationsCampaignBehavior itself registers hero-prisoner-only lines there,
// e.g. "player_prisoner_talk_let_go" and "player_is_leaving_enemy_prisoner", gated on
// Hero.OneToOneConversationHero.IsPrisoner). No vanilla block to work around, so this only ADDS one
// player line to that same hub: when the conversation partner is the tracked nemesis the player
// currently holds prisoner, offer to open the captor scene (interrogation, torture, whatever the
// player's own words make of it) instead of an ordinary chat. From there the interaction is a CHAT:
// the player describes what they do, and the LLM detects the execute_prisoner action exactly like
// every other CR action (see BannerlordGameStateBridge.ExecuteAction).

#region

using System;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Mapping;
using CalradiaRemembers.Witnesses;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Nemesis
{
    /// <summary>
    ///   Registers the player lines that let the player open a PLAYER-AS-CAPTOR scene against a hero they
    ///   currently hold prisoner, from the ordinary party-screen "Talk" conversation (the same
    ///   <c>hero_main_options</c> hub <see cref="CalradiaRemembers.Dialogue.LlmDialogFlow" /> already uses
    ///   for every other hero). TWO mutually-exclusive lines share one consequence: a distinct, charged
    ///   line when the partner is the tracked nemesis (<see cref="NemesisBehavior.PlayerHeldNemesisPrisoner" />),
    ///   and a plainer captor line for any OTHER hero prisoner the player holds, so every captive can be
    ///   subjected to the scene, not only a nemesis (Gabriel 2026-08-21). The consequence opens
    ///   <see cref="ChatManager.OpenForCaptorScene" /> directly (never <see cref="ChatSceneStager" />)
    ///   because these lines only ever fire from WITHIN an already-live conversation scene the player
    ///   started themselves, so riding it is the VRAM-safe choice.
    /// </summary>
    public static class NemesisPrisonerDialogFlow
    {
        // The player's own line opening the captor scene against the tracked NEMESIS. Deliberately distinct
        // from both LlmDialogFlow's neutral "A word with you." and the generic captor line below, so the
        // player can tell them apart: this one names the hunt now ended in chains.
        private const string OpenCaptorSceneOptionText =
            "The hunt between us is over, for now. Let us speak, now that you are the one in chains.";

        // The generic captor line, shown for any OTHER hero prisoner the player holds (not the nemesis, who
        // gets the charged line above). Still unmistakably a captor's opening, distinct from the neutral
        // "A word with you." so the player chooses the confrontation deliberately.
        private const string OpenCaptorSceneGenericOptionText =
            "You are mine now, and these chains are yours. Let us have a word, you and I.";

        /// <summary>
        ///   Adds the player line to the <c>hero_main_options</c> hub, the same hub reached whether the
        ///   player talked to a free hero or (per vanilla's own "player_prisoner_talk_let_go" /
        ///   "player_is_leaving_enemy_prisoner" lines, registered by the engine's own
        ///   LordConversationsCampaignBehavior) a hero prisoner they hold. Routed to a nonexistent output
        ///   token (mirrors <see cref="CalradiaRemembers.Dialogue.LlmDialogFlow" />'s own dummy-token
        ///   pattern) so the vanilla dialogue simply idles behind the chat overlay until
        ///   <see cref="ChatManager.Close" /> ends the conversation the party screen started.
        /// </summary>
        public static void Register(CampaignGameStarter starter)
        {
            starter.AddPlayerLine(
                "calradia_remembers_nemesis_captor_scene",
                "hero_main_options",
                "cr_nemesis_captor_scene_dummy_token",
                OpenCaptorSceneOptionText,
                CanOpenCaptorScene,
                OpenCaptorScene,
                116); // above LlmDialogFlow's ordinary "A word with you." (110), so it stands out first

            starter.AddPlayerLine(
                "calradia_remembers_prisoner_captor_scene",
                "hero_main_options",
                "cr_nemesis_captor_scene_dummy_token",
                OpenCaptorSceneGenericOptionText,
                CanOpenCaptorSceneForAnyPrisoner,
                OpenCaptorScene,
                115); // just below the nemesis line; the two conditions are mutually exclusive, so only one ever shows
        }

        #region private

        /// <summary>
        ///   True only when the conversation partner IS the tracked, living nemesis the player currently
        ///   holds prisoner. Re-checked at click time by <see cref="OpenCaptorScene" /> too (the bridge is
        ///   law), in case he was freed or escaped between the line showing and being picked.
        /// </summary>
        private static bool CanOpenCaptorScene()
        {
            Hero convoHero = Hero.OneToOneConversationHero;

            return convoHero != null && NemesisBehavior.Instance?.PlayerHeldNemesisPrisoner() == convoHero;
        }

        /// <summary>
        ///   True when the conversation partner is any hero prisoner the player currently holds, EXCEPT the
        ///   tracked nemesis (who gets the charged line via <see cref="CanOpenCaptorScene" /> instead, so the
        ///   two lines never both show). "Held by the player" is the same test the captive-chat path uses
        ///   (<c>IsPrisoner</c> + <c>PartyBelongedToAsPrisoner.LeaderHero == Hero.MainHero</c>). The player
        ///   must not themselves be a captive: a captor scene is the player-as-captor direction.
        /// </summary>
        private static bool CanOpenCaptorSceneForAnyPrisoner()
        {
            if (Hero.MainHero?.IsPrisoner == true) return false;

            Hero convoHero = Hero.OneToOneConversationHero;
            if (convoHero is not {IsPrisoner: true}) return false;
            if (convoHero.PartyBelongedToAsPrisoner?.LeaderHero != Hero.MainHero) return false;

            return NemesisBehavior.Instance?.PlayerHeldNemesisPrisoner() != convoHero;
        }

        /// <summary>
        ///   Opens the captor scene against the hero prisoner the player is talking to (the nemesis for the
        ///   charged line, any other held prisoner for the generic one), mirroring
        ///   <see cref="CalradiaRemembers.Dialogue.LlmDialogFlow" />'s profile-loading shape but for the
        ///   captor-scene entry (<see cref="ChatManager.OpenForCaptorScene" />, never
        ///   <see cref="ChatSceneStager" />: this consequence runs from WITHIN the live conversation the
        ///   party-screen Talk already started, so opening directly rides that scene instead of starting a
        ///   second one). Re-validates the partner is still a prisoner the player holds (the bridge is law,
        ///   in case they were freed between the line showing and being picked). Default intent Interrogation;
        ///   the player's own words in chat, not a menu, decide whether it stays a conversation or turns to
        ///   torture or execution.
        /// </summary>
        private static void OpenCaptorScene()
        {
            try
            {
                Hero prisonerHero = Hero.OneToOneConversationHero;
                bool heldByPlayer = prisonerHero is {IsPrisoner: true}
                                    && prisonerHero.PartyBelongedToAsPrisoner?.LeaderHero == Hero.MainHero;
                if (!heldByPlayer)
                {
                    ModLog.Info("[NEMESIS] captor scene aborted, the partner is no longer a prisoner the player holds.");

                    return;
                }

                NpcMemoryCampaignBehavior behavior = NpcMemoryCampaignBehavior.Instance;
                if (behavior?.LlmServices is not {IsAvailable: true})
                {
                    InformationManager.ShowInquiry(new InquiryData(
                        "No response",
                        "The LLM service is not available. Set your OpenRouter API key in the mod options first.",
                        true, false, "OK", null, null, null), true);

                    return;
                }

                NpcProfile profile = behavior.Store?.Get(prisonerHero.StringId) ?? HeroProfileMapper.FromHero(prisonerHero);

                ChatManager.OpenForCaptorScene(prisonerHero, profile, CaptiveSceneIntent.Interrogation);

                // Bring-participants-to-a-captor-scene. A captor scene opens with no witnesses of its own, so
                // both pickers are wired here, each a no-op when there is nobody to bring, each queued after
                // the chat opens exactly like an ordinary conversation's companion picker.
                //   Increment 1: offer the player's own companions, so one can be brought in and handed the
                //   prisoner to act (the reward scene, PromptBuilder.AppendCompanionActingOnCaptive).
                //   Increment 2: offer any OTHER lord prisoner the player holds, so one can be brought in as
                //   coerced leverage against this one ("use Ascyron to pressure Lucon").
                // Companions first (they act), prisoners second (they are the leverage).
                ChatViewModel opened = ChatManager.ActiveViewModel;
                if (opened != null)
                {
                    WitnessResolver.ShowCompanionPickerIfNeeded(prisonerHero, opened.AddWitness);
                    WitnessResolver.ShowBroughtCaptivePickerIfNeeded(prisonerHero, opened.AddWitness);
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[NEMESIS] OpenCaptorScene failed: {ex.GetType().Name}: {ex.Message}");
            }
        }

        #endregion
    }
}
