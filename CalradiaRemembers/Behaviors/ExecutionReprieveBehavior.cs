// Code written by Gabriel Mailhot, 26/11/2026.
// froggyluv (Nexus, 26/11/2026): a cocky lord, spared after the "Execute" order was rescinded, "denied that -that had never
// happened. So it seems as if that part of bannerlord is not linked into CR yet". Gabriel (26/11/2026): build it. Follows the
// game's own execution dialogue (ConversationManager.ConsequenceRunned, which fires for every sentence spoken, read in the
// 1.5.7 binaries) and the execution scene's cancel (ExecutionScenePatch), and gives the spared lord the memory of it
// (Logic.Captivity.ExecutionReprievePolicy) on his profile. An execution carried out is ExecutedKinGrudgeBehavior's.

#region

using System;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Captivity;
using CalradiaRemembers.Mapping;
using HarmonyLib;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Conversation;
using TaleWorlds.CampaignSystem.SceneInformationPopupTypes;

#endregion

namespace CalradiaRemembers.Behaviors
{
   /// <summary>Gives a lord the player sentenced to death and then spared the memory of it.</summary>
   public sealed class ExecutionReprieveBehavior : CampaignBehaviorBase
   {
      private ExecutionReprieveTracker _tracker = new();

      /// <summary>The live instance, for the execution scene patch and cr.test all.</summary>
      public static ExecutionReprieveBehavior Instance { get; private set; }

      public ExecutionReprieveBehavior() => Instance = this;

      public override void RegisterEvents()
         => CampaignEvents.OnSessionLaunchedEvent.AddNonSerializedListener(this, _ => Subscribe());

      // The memory lives on the spared lord's profile, which the store already saves; a sentence pending across a save is
      // dropped on purpose (the scene that settles it cannot outlive the session).
      public override void SyncData(IDataStore dataStore) { }

      private void Subscribe()
      {
         _tracker = new ExecutionReprieveTracker();
         ConversationManager manager = Campaign.Current?.ConversationManager;
         if (manager == null) return;

         manager.ConsequenceRunned -= OnConsequenceRunned;
         manager.ConsequenceRunned += OnConsequenceRunned;
      }

      private void OnConsequenceRunned(ConversationSentence sentence)
      {
         try
         {
            Hero partner = Hero.OneToOneConversationHero;
            if (sentence == null || partner == null) return;

            Remember(partner, _tracker.OnSentence(sentence.Id, partner.StringId));
         }
         catch (Exception ex) { ModLog.Error($"[EXECUTION] following the execution dialogue failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The player cancelled the execution scene of <paramref name="victim" />. Returns the line remembered, or null.</summary>
      public string ExecutionCancelled(Hero victim) => victim == null ? null : Remember(victim, _tracker.OnExecutionCancelled(victim.StringId));

      /// <summary>The player let the execution scene of <paramref name="victim" /> go ahead.</summary>
      public void ExecutionCarriedOut(Hero victim)
      {
         if (victim != null) _tracker.OnExecutionCarriedOut(victim.StringId);
      }

      /// <summary>cr.test all seam: a sentence of the game's dialogue spoken with <paramref name="partner" />, through the real tracker.</summary>
      internal string DebugSentence(string sentenceId, Hero partner) => partner == null ? null : Remember(partner, _tracker.OnSentence(sentenceId, partner.StringId));

      private static string Remember(Hero lord, ReprieveWay way)
      {
         string line = ExecutionReprievePolicy.Memory(way, Hero.MainHero?.Name?.ToString());
         if (line == null || lord == null || lord.IsDead) return null;

         var store = NpcMemoryCampaignBehavior.Instance?.Store;
         if (store == null) return null;

         NpcProfile profile = store.Get(lord.StringId) ?? HeroProfileMapper.FromHero(lord);
         profile.Events.Add(new NotableEvent((int) CampaignTime.Now.ToDays, NotableEventType.Confrontation, line));
         store.Set(profile);
         ModLog.Info($"[EXECUTION] {lord.Name} remembers being spared ({way}): {line}");

         return line;
      }
   }

   /// <summary>
   ///   The execution scene's two answers when the player is the executioner: cancel spares the condemned (after a battle's
   ///   sentence it takes him prisoner, from the party screen it leaves him one), go ahead settles nothing to remember.
   /// </summary>
   [HarmonyPatch(typeof(HeroExecutionSceneNotificationData))]
   public static class ExecutionScenePatch
   {
      /// <summary>The player chose not to execute.</summary>
      [HarmonyPostfix]
      [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.OnNegativeAction))]
      public static void Cancelled(HeroExecutionSceneNotificationData __instance)
      {
         try
         {
            if (__instance?.Executer == Hero.MainHero && __instance.Victim != Hero.MainHero) ExecutionReprieveBehavior.Instance?.ExecutionCancelled(__instance.Victim);
         }
         catch (Exception ex) { ModLog.Error($"[EXECUTION] reading a cancelled execution failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>The player went ahead.</summary>
      [HarmonyPostfix]
      [HarmonyPatch(nameof(HeroExecutionSceneNotificationData.OnAffirmativeAction))]
      public static void CarriedOut(HeroExecutionSceneNotificationData __instance)
      {
         try
         {
            if (__instance?.Executer == Hero.MainHero) ExecutionReprieveBehavior.Instance?.ExecutionCarriedOut(__instance.Victim);
         }
         catch (Exception ex) { ModLog.Error($"[EXECUTION] reading an execution failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
