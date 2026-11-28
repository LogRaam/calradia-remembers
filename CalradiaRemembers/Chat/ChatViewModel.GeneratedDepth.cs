// Code written by Gabriel Mailhot, 28/11/2026.
// Stage 4 of the group-scene plan, "generated depth" (docs/design/group-chat/integration-plan-2026-11-28.md). Gabriel,
// 28/11/2026: "go pour tout le système ... je testerai demain". The chat's half: on the main character's first model turn of an
// ordinary one-to-one conversation, a character with no depth yet is given one in the background (GeneratedDepthService); the
// chat never waits. Whether the character HAD a depth as this conversation opened is captured once per speaker, as the history
// cut is: a depth that lands while the talk goes on stays hidden until the next one, so the character's nature and the cached
// prefix never change between two lines.

#region

using System.Collections.Generic;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Character;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private readonly Dictionary<string, bool> _generatedDepthHidden = new();

      /// <summary>
      ///   True when this speaker had no generated depth as the conversation opened: one that lands meanwhile shows from the next
      ///   conversation on. Captured on first ask, per speaker.
      /// </summary>
      internal bool GeneratedDepthHidden(NpcProfile profile)
      {
         if (profile?.Id == null) return false;

         if (_generatedDepthHidden.TryGetValue(profile.Id, out bool hidden)) return hidden;

         hidden = profile.GeneratedDepth == null;
         _generatedDepthHidden[profile.Id] = hidden;

         return hidden;
      }

      /// <summary>The facts the generation is decided on, read live.</summary>
      internal GeneratedDepthFacts GeneratedDepthFactsFor(ChatSpeaker speaker)
      {
         NpcProfile profile = speaker?.Profile;

         return new GeneratedDepthFacts {
            SwitchOn = ModSettings.GeneratedDepth,
            OrdinaryConversation = speaker?.IsMain == true && _mode == ChatMode.Conversation && !_captorScene && !IsCaptiveEncounter
                                   && ChatManager.ActiveSkin != ChatManager.CouncilSkin,
            EnvoyAudience = IsEnvoyAudience,
            CompanionBriefing = IsCompanionBriefingChat,
            HasCharacter = speaker?.Hero != null && speaker.Hero != Hero.MainHero && profile?.Id != null,
            HasDepth = profile?.GeneratedDepth != null,
            InFlight = GeneratedDepthService.IsInFlight(profile?.Id),
            FailedThisSession = GeneratedDepthService.FailedBefore(profile?.Id)
         };
      }

      /// <summary>
      ///   On a model turn: captures whether the speaker had a depth at opening, then starts its generation in the background when
      ///   the policy says so. Returns at once.
      /// </summary>
      private void StartGeneratedDepthIfDue(ChatSpeaker speaker)
      {
         if (speaker?.Profile == null) return;

         GeneratedDepthHidden(speaker.Profile);
         if (!GeneratedDepthPolicy.ShouldGenerate(GeneratedDepthFactsFor(speaker))) return;

         GeneratedDepthService.Start(NpcMemoryCampaignBehavior.Instance, speaker.Hero, speaker.Profile);
      }
   }
}
