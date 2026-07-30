// Code written by Gabriel Mailhot, 30/07/2026.
// Bespoke COUNCIL UI, Inc 0: a dedicated 3-column window (left seat strip / centre chatroom / right
// engagements) bound to this SAME ChatViewModel, exactly like the Manuscript skin is a second prefab over the
// same data. This file owns the extra state the new prefab needs that the existing Classic/Manuscript prefabs
// never bound: the seat tiles (CouncilParticipants), the agenda line, the dossier overlay, and the empty
// Engagements placeholder (Inc 1 fills it from CouncilSessionBehavior's ledger).

#region

using System;
using System.Collections.Generic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private readonly MBBindingList<CouncilEngagementViewModel> _engagements = new MBBindingList<CouncilEngagementViewModel>();
      private readonly MBBindingList<CouncilParticipantViewModel> _councilParticipants = new MBBindingList<CouncilParticipantViewModel>();
      private string _councilAgendaText = "";
      private ChatMessageViewModel _councilDossierPortrait; // transient carrier resolving the clicked seat's portrait, released on close/reopen
      private bool _isCouncilDossierOpen;

      /// <summary>
      ///   The short "Ordre du jour" line shown under the title in the bespoke council window. Seeded from the
      ///   scope in <see cref="ActivateCouncil" />; Inc 1 may replace this placeholder with the actual matter the
      ///   player raises.
      /// </summary>
      [DataSourceProperty]
      public string CouncilAgendaText
      {
         get => _councilAgendaText;
         set
         {
            if (_councilAgendaText == value) return;
            _councilAgendaText = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The left strip's seat tiles: the anchor plus every present witness. Populated by <see cref="PopulateCouncilParticipants" />.</summary>
      [DataSourceProperty]
      public MBBindingList<CouncilParticipantViewModel> CouncilParticipants => _councilParticipants;

      /// <summary>The right panel's pledge rows. Always empty in Inc 0 (Inc 1 fills it from the council's ledger).</summary>
      [DataSourceProperty]
      public MBBindingList<CouncilEngagementViewModel> Engagements => _engagements;

      /// <summary>The placeholder shown in the Engagements panel while <see cref="Engagements" /> is empty.</summary>
      [DataSourceProperty]
      public string EngagementsPlaceholderText => "Aucun engagement pour l'instant";

      /// <summary>True while at least one engagement has been recorded (Inc 1); hides the empty-state placeholder.</summary>
      [DataSourceProperty]
      public bool HasEngagements => _engagements.Count > 0;

      /// <summary>
      ///   The inverse of <see cref="HasEngagements" />, exposed separately because the prefab's IsVisible
      ///   binding has no negation operator: the empty-state placeholder needs its OWN positive boolean.
      /// </summary>
      [DataSourceProperty]
      public bool ShowEngagementsPlaceholder => !HasEngagements;

      /// <summary>True while the council dossier overlay is open, showing the seat clicked in the left strip.</summary>
      [DataSourceProperty]
      public bool IsCouncilDossierOpen
      {
         get => _isCouncilDossierOpen;
         set
         {
            if (_isCouncilDossierOpen == value) return;
            _isCouncilDossierOpen = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>Closes the council dossier overlay. Bound via Command.Click on its close button.</summary>
      public void ExecuteCloseCouncilDossier() => CloseCouncilDossier();

      /// <summary>Closes the council dossier overlay.</summary>
      public void CloseCouncilDossier()
      {
         IsCouncilDossierOpen = false;
      }

      /// <summary>
      ///   Opens the council dossier overlay for <paramref name="heroStringId" />, reusing the exact same
      ///   Panel* dossier data <see cref="OpenPortraitPanel" /> populates (facts, background, character,
      ///   appearance, discoveries, grievances, portrait). <see cref="PopulatePanelData" /> reads its portrait
      ///   fields off a <see cref="ChatMessageViewModel" />, the chat-line carrier, not a bare Hero (there is no
      ///   hero-typed overload), so a TRANSIENT one is built here purely to resolve this seat's portrait; it is
      ///   never added to <see cref="Messages" /> (it is not a chat line) and its vanilla tableau is released
      ///   before the next one is built and at council close (<see cref="ReleaseCouncilPortraits" />), so it can
      ///   never linger as an orphaned GPU render-target.
      /// </summary>
      public void OpenCouncilDossier(string heroStringId)
      {
         try
         {
            if (string.IsNullOrEmpty(heroStringId)) return;

            Hero hero = Hero.FindFirst(h => h.StringId == heroStringId);
            if (hero == null) return;

            _councilDossierPortrait?.ReleasePortrait();
            _councilDossierPortrait = new ChatMessageViewModel(hero, hero.Name?.ToString() ?? "", "", hero == Hero.MainHero);

            if (!PopulatePanelData(_councilDossierPortrait))
            {
               _councilDossierPortrait = null;

               return;
            }

            IsCouncilDossierOpen = true;
         }
         catch (Exception ex)
         {
            ModLog.Error($"ChatViewModel.OpenCouncilDossier failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Seats the left strip: the anchor (<see cref="MainSpeaker" />) followed by every present witness,
      ///   resolved to a live hero and deduplicated by StringId. Called by <see cref="ActivateCouncil" /> once
      ///   the caller (ChatManager.OpenForCouncil/OpenForAssembly) has already added every witness, so the whole
      ///   seated roster is present. Guards every resolution gap rather than throwing: a hero who cannot be
      ///   found (already left play) is simply absent from the strip.
      /// </summary>
      public void PopulateCouncilParticipants()
      {
         ClearCouncilParticipants();

         var seatedHeroIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

         ChatSpeaker anchor = MainSpeaker();
         if (anchor?.Hero != null)
         {
            _councilParticipants.Add(new CouncilParticipantViewModel(anchor.Hero, "Presides over this council"));
            seatedHeroIds.Add(anchor.Hero.StringId);
         }

         foreach (WitnessEntry w in _witnesses)
         {
            if (w == null || string.IsNullOrEmpty(w.HeroStringId) || seatedHeroIds.Contains(w.HeroStringId)) continue;

            Hero hero = Hero.FindFirst(h => h.StringId == w.HeroStringId);
            if (hero == null) continue;

            seatedHeroIds.Add(w.HeroStringId);
            _councilParticipants.Add(new CouncilParticipantViewModel(hero, w.RelationToNpc));
         }

         OnPropertyChanged(nameof(CouncilParticipants));
      }

      /// <summary>
      ///   Frees every seat's vanilla character tableau and clears the strip. Called by
      ///   <see cref="ChatManager.KillChatWindow" /> before the movie is released, mirroring
      ///   <see cref="ReleaseAllPortraits" /> for the ordinary message list.
      /// </summary>
      public void ReleaseCouncilPortraits()
      {
         ClearCouncilParticipants();
         _councilDossierPortrait?.ReleasePortrait();
         _councilDossierPortrait = null;
      }

      /// <summary>Removes any council seat highlight (a no-op when none is set). See <see cref="SetActiveCouncilSpeaker(string)" />.</summary>
      public void ClearActiveCouncilSpeaker()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.IsActiveSpeaker = false;
      }

      /// <summary>Highlights the seat matching <paramref name="heroStringId" /> and clears every other seat's highlight.</summary>
      public void SetActiveCouncilSpeaker(string heroStringId)
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.IsActiveSpeaker = !string.IsNullOrEmpty(heroStringId)
                              && string.Equals(p.HeroStringId, heroStringId, StringComparison.OrdinalIgnoreCase);
      }

      /// <summary>Releases every seat tile's tableau and empties the strip. Shared by re-population and close-time cleanup.</summary>
      private void ClearCouncilParticipants()
      {
         foreach (CouncilParticipantViewModel p in _councilParticipants)
            p.ReleasePortrait();

         _councilParticipants.Clear();
      }

      #region IRoundTableHost (council seat highlight)

      void IRoundTableHost.SetActiveCouncilSpeaker(ChatSpeaker speaker) => SetActiveCouncilSpeaker(speaker?.Hero?.StringId);

      void IRoundTableHost.ClearActiveCouncilSpeaker() => ClearActiveCouncilSpeaker();

      #endregion
   }
}
