// Code written by Gabriel Mailhot, 02/06/2026.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Bannerlord.UIExtenderEx;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Chat;
using HarmonyLib;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.GauntletUI.PrefabSystem;
using TaleWorlds.Library;
using TaleWorlds.ModuleManager;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers
{
   /// <summary>
   ///   Entry point for the NPC Memory Service mod. Bannerlord instantiates this
   ///   class on game launch and invokes lifecycle hooks. We use them to register
   ///   our <see cref="NpcMemoryCampaignBehavior" /> with the campaign system and
   ///   to expose the mod's Gauntlet UI prefabs to the engine.
   /// </summary>
   public class NpcMemoryServiceSubmodule : MBSubModuleBase
   {
      private const string ModName = "NPC Memory Service";
      private const string ModuleId = "CalradiaRemembers";

      /// <summary>
      ///   Called after all submodules have loaded — safe to assume other mods
      ///   are present at this point.
      /// </summary>
      protected override void OnBeforeInitialModuleScreenSetAsRoot()
      {
         base.OnBeforeInitialModuleScreenSetAsRoot();
         Log($"{ModName} ready (main menu reached).");
      }

      /// <summary>
      ///   Called every time the user starts or loads a campaign. This is where
      ///   we hook our <see cref="CampaignBehaviorBase" /> into the active campaign.
      /// </summary>
      protected override void OnGameStart(Game game, IGameStarter gameStarterObject)
      {
         base.OnGameStart(game, gameStarterObject);

         // Only register for campaign games (not custom battles, sieges, etc.)
         if (game.GameType is not Campaign) return;

         var campaignStarter = (CampaignGameStarter) gameStarterObject;
         campaignStarter.AddBehavior(new NpcMemoryCampaignBehavior());
         // Marriage system (family-approval engine) — SHIPPED from v1.7.0. The approval chat path
         // (grant_blessing, Phase A.2a) now exists, and the consequence only ever lands on the
         // PLAYER's own marriage (OnBeforeHeroesMarried bails when the player is not a spouse), so
         // vanilla NPC-NPC weddings are untouched. Marrying into a house without first earning its
         // blessing chills that family — the intended love-vs-ambition mechanic.
         campaignStarter.AddBehavior(new Marriage.MarriageApprovalBehavior());
         Log($"{ModName} campaign behavior registered.");
      }

      /// <summary>
      ///   Called once when Bannerlord finishes loading all mods and is ready
      ///   to show the main menu. Earliest reliable hook for "the mod is live".
      /// </summary>
      protected override void OnSubModuleLoad()
      {
         base.OnSubModuleLoad();
         Log($"{ModName} v1.7.8 loaded.");

         try
         {
            var harmony = new Harmony(ModuleId);
            harmony.PatchAll();
            Log("Harmony patches applied.");
         }
         catch (Exception ex)
         {
            Log($"Harmony patch failed: {ex.Message}");
         }

         RegisterGuiResources();
         InitializeUiExtender();
      }

      #region private

      // ── Logging helper ────────────────────────────────────────────────────

      /// <summary>
      ///   Logs a message to the Bannerlord in-game console (top-left) and the
      ///   game's log files. Useful during development; consider toggling off
      ///   for release once the mod is stable.
      /// </summary>
      private static void Log(string message)
      {
         InformationManager.DisplayMessage(new InformationMessage($"[{ModName}] {message}"));
      }

      /// <summary>
      ///   Inserts a single Widget-derived type into the built-in types map.
      ///   Uses the type's short name (no namespace) as the XML tag.
      /// </summary>
      private static void RegisterBuiltInType(Dictionary<string, Type> builtinTypes, Type widgetType)
      {
         builtinTypes[widgetType.Name] = widgetType;
      }


      /// <summary>
      ///   Boots UIExtenderEx so our encyclopedia mixin + prefab patch are applied
      ///   (adds the NPC's personal regard to their encyclopedia hero page).
      /// </summary>
      private void InitializeUiExtender()
      {
         try
         {
            var extender = UIExtender.Create(ModuleId);
            extender.Register(typeof(NpcMemoryServiceSubmodule).Assembly);
            extender.Enable();
            Log("UIExtender enabled (encyclopedia personal-relation extension).");
         }
         catch (Exception ex)
         {
            Log($"UIExtender init failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Registers our custom Widget-derived classes as built-in types in the
      ///   WidgetFactory, so &lt;ChatScrollablePanel/&gt; (and similar) are recognized
      ///   by the XML parser without requiring a separate .xml definition file.
      ///   WidgetFactory.AddCustomType is for XML-defined widgets and expects a
      ///   .xml file matching the type name. For pure-C# widgets we need to
      ///   inject them directly into the private _builtinTypes dictionary —
      ///   the same dictionary that WidgetFactory.Initialize populates from
      ///   WidgetInfo.GetWidgetInfos() at engine startup.
      /// </summary>
      private void RegisterCustomBuiltInWidgets()
      {
         try
         {
            WidgetFactory factory = UIResourceManager.WidgetFactory;
            if (factory == null)
            {
               Log("WidgetFactory not available — custom widgets not registered.");

               return;
            }

            FieldInfo field = typeof(WidgetFactory).GetField("_builtinTypes", BindingFlags.NonPublic | BindingFlags.Instance);

            if (field == null)
            {
               Log("Could not find WidgetFactory._builtinTypes field.");

               return;
            }

            if (field.GetValue(factory) is not Dictionary<string, Type> builtinTypes)
            {
               Log("WidgetFactory._builtinTypes was null or unexpected type.");

               return;
            }

            RegisterBuiltInType(builtinTypes, typeof(ChatScrollablePanel));

            Log("Custom widget types registered.");
         }
         catch (Exception ex)
         {
            Log($"RegisterCustomBuiltInWidgets failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      // ── GUI registration ──────────────────────────────────────────────────

      /// <summary>
      ///   Registers the module's GUI folder with Bannerlord's resource depot so
      ///   our Gauntlet prefabs (chat window, etc.) can be loaded by name at runtime.
      ///   Also registers our custom built-in widget types so the XML parser
      ///   recognizes them.
      /// </summary>
      private void RegisterGuiResources()
      {
         string moduleRoot = ModuleHelper.GetModuleFullPath(ModuleId);
         string guiFolder = Path.Combine(moduleRoot, "GUI");
         string prefabFolder = Path.Combine(guiFolder, "Prefabs");

         if (!Directory.Exists(prefabFolder))
         {
            Log($"GUI prefab folder not found: {prefabFolder}");

            return;
         }

         if (UIResourceManager.ResourceDepot != null) UIResourceManager.ResourceDepot.AddLocation(moduleRoot, "/GUI/");

         RegisterCustomBuiltInWidgets();

         Log($"Registered GUI resources at {guiFolder}.");
      }

      #endregion
   }
}