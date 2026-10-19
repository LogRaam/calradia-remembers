// Code written by Gabriel Mailhot, 23/08/2026.
// The player-facing entry to a council: the shared logic behind the campaign-bar button (Map/CouncilNavigationElement;
// reachable on the map and inside a settlement alike). Decides which scopes could seat someone right now, whether a council may open at all
// (feature on, no chat open, not captive, on land), shows a scope picker, and convenes through the SAME
// VRAM-safe staged path cr.council uses (CouncilConversationStager). A council on a boat is refused: no
// conversation scene loads on open water, and the direct fallback would ride the live map (Gabriel 2026-08-23).

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Assemblies;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Assemblies;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.Core;
using TaleWorlds.Library;
using TaleWorlds.Localization;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Shared "convene a council" logic behind the campaign-bar button (reachable on the map and inside a settlement).</summary>
   public static class CouncilEntry
   {
      // The scopes a player convenes directly. Assembly is the separate invite-and-wait model; Parley is
      // contextual to a specific war (it needs an enemy target), so neither belongs in this general picker.
      private static readonly CouncilScope[] PlayerScopes =
         {CouncilScope.Party, CouncilScope.Companions, CouncilScope.Family, CouncilScope.ClanLords, CouncilScope.WarCouncil};

      /// <summary>The scopes that would seat at least one member right now, in menu order.</summary>
      public static List<CouncilScope> OfferableScopes()
      {
         var list = new List<CouncilScope>();

         foreach (CouncilScope scope in PlayerScopes)
            try
            {
               if (CouncilRosterResolver.Seated(scope).Count > 0) list.Add(scope);
            }
            catch (Exception ex) { ModLog.Error($"[COUNCIL ENTRY] scope {scope} check failed: {ex.Message}"); }

         return list;
      }

      /// <summary>
      ///   True when the player may convene a council right now: the feature is enabled, nothing blocks it
      ///   (<see cref="ChatManager.PreCouncilBlockReason" />: no chat open, not captive), the player is on land
      ///   (never on a boat), and at least one scope would seat someone. Drives the campaign-bar button's
      ///   permission (it greys out when a council cannot open).
      /// </summary>
      public static bool CanConveneNow()
      {
         try
         {
            if (!ModSettings.EnableCouncil) return false;
            if (ChatManager.PreCouncilBlockReason() != null) return false;
            if (MobileParty.MainParty?.IsCurrentlyAtSea == true) return false;

            return OfferableScopes().Count > 0;
         }
         catch (Exception ex)
         {
            ModLog.Error($"[COUNCIL ENTRY] CanConveneNow failed: {ex.GetType().Name}: {ex.Message}");

            return false;
         }
      }

      /// <summary>
      ///   Shows the scope picker (or convenes straight away when only one scope is offerable) and opens that
      ///   council. Surfaces a plain reason rather than doing nothing when it cannot open.
      /// </summary>
      public static void PromptAndConvene()
      {
         if (MobileParty.MainParty?.IsCurrentlyAtSea == true)
         {
            MBInformationManager.AddQuickInformation(new TextObject("You cannot convene a council at sea."));

            return;
         }

         if (OfferableScopes().Count == 0)
         {
            MBInformationManager.AddQuickInformation(new TextObject("There is no one to seat at a council right now."));

            return;
         }

         // Always ASK which council, with a Cancel (Gabriel 2026-08-23): list every type, each with how many it
         // would seat, so the player chooses even when only one has anyone and can always back out. Picking an
         // empty type is handled downstream (Convene surfaces "nobody is seated for that council").
         List<InquiryElement> elements = PlayerScopes
                                         .Select(s => new InquiryElement(s, ScopeLabel(s) + " (" + CouncilRosterResolver.Seated(s).Count + ")", null))
                                         .ToList();

         MBInformationManager.ShowMultiSelectionInquiry(
            new MultiSelectionInquiryData(
               titleText: "Hold a Council",
               descriptionText: "What council would you convene?",
               inquiryElements: elements,
               isExitShown: true,
               minSelectableOptionCount: 1,
               maxSelectableOptionCount: 1,
               affirmativeText: "Convene",
               negativeText: "Cancel",
               affirmativeAction: OnScopeChosen,
               negativeAction: null,
               soundEventPath: "",
               isSeachAvailable: false),
            pauseGameActiveState: false,
            prioritize: true);
      }

      #region private

      private static void OnScopeChosen(List<InquiryElement> selected)
      {
         if (selected == null || selected.Count == 0) return;
         if (selected[0].Identifier is CouncilScope scope) Convene(scope);
      }

      private static void Convene(CouncilScope scope)
      {
         try
         {
            string result = CouncilConversationStager.StageCouncil(scope);
            ModLog.Info($"[COUNCIL ENTRY] convene {scope} -> {result}");

            // Staging did not start and no window opened: surface the reason instead of doing nothing.
            if (!CouncilConversationStager.IsStaging && !ChatManager.IsOpen && !string.IsNullOrEmpty(result))
               MBInformationManager.AddQuickInformation(new TextObject(result));
         }
         catch (Exception ex) { ModLog.Error($"[COUNCIL ENTRY] convene {scope} failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      private static string ScopeLabel(CouncilScope scope)
         => scope switch {
            CouncilScope.Party => "Those riding with you",
            CouncilScope.Companions => "Your companions",
            CouncilScope.Family => "Your kin",
            CouncilScope.ClanLords => "Your clan's lords",
            CouncilScope.WarCouncil => "A war council",
            _ => scope.ToString()
         };

      #endregion
   }
}
