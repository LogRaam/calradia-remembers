// Code written by Gabriel Mailhot, 30/07/2026.
// Bespoke COUNCIL UI, Inc 0: a dedicated 3-column window (left seat strip / centre chatroom / right
// engagements) bound to this SAME ChatViewModel, exactly like the Manuscript skin is a second prefab over the
// same data. This file owns the extra state the new prefab needs that the existing Classic/Manuscript prefabs
// never bound: the seat tiles (CouncilParticipants), the agenda line, the dossier overlay, and the empty
// Engagements placeholder (Inc 1 fills it from CouncilSessionBehavior's ledger).

#region

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CalradiaRemembers.Behaviors;
using CalradiaRemembers.Logic.Assemblies;
using NpcMemoryService.Core.Compression;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private const int CouncilEngagementExtractTimeoutSeconds = 45;
      private readonly MBBindingList<CouncilEngagementViewModel> _engagements = new MBBindingList<CouncilEngagementViewModel>();
      private readonly MBBindingList<CouncilParticipantViewModel> _councilParticipants = new MBBindingList<CouncilParticipantViewModel>();
      private readonly EngagementLedger _engagementLedger = new EngagementLedger();
      private string _councilAgendaText = "";
      private ChatMessageViewModel _councilDossierPortrait; // transient carrier resolving the clicked seat's portrait, released on close/reopen
      private bool _isCouncilDossierOpen;
      private bool _engagementExtractionRunning;

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
      public string EngagementsPlaceholderText => "No decisions yet.";

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
            // Council narrator model (2026-08-02): the anchor is the conversation's staging body only (the
            // profile behind the LLM call, hidden behind the opaque backdrop), never a privileged voice. The
            // seat label used to read "Presides over this council"; that presiding language is exactly what
            // the reshape drops, so the label is now the same neutral seat description any other member gets.
            _councilParticipants.Add(new CouncilParticipantViewModel(anchor.Hero, "Seated at this council"));
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

      #region Engagements extraction (Inc 1)

      /// <summary>
      ///   Kicks a background reading of the council's standing engagements, unless one is already in flight, the
      ///   LLM is unavailable, or this is not a council. Fired once per player turn (from ExecuteSendMessage), so
      ///   the right-hand Engagements panel mirrors what the table has settled through the previous turn. Fire and
      ///   forget: a failed or slow reading simply leaves the panel as it was. There is no cancel button by design
      ///   (Gabriel 2026-07-30); a decision is walked back purely by the conversation no longer asserting it, which
      ///   the next reading picks up.
      /// </summary>
      private void KickCouncilEngagementExtraction()
      {
         if (_councilScope == null || _engagementExtractionRunning) return;

         var behavior = NpcMemoryCampaignBehavior.Instance;
         if (behavior?.LlmServices is not {IsAvailable: true}) return;

         _engagementExtractionRunning = true;
         _ = RunCouncilEngagementExtractionAsync();
      }

      /// <summary>
      ///   Reads the whole conversation, asks the extractor which decisions currently stand, and reconciles the
      ///   ledger to that snapshot (a decision walked back in the talk simply stops appearing and is dropped). The
      ///   LLM runs off-thread; the ledger mutation and the binding refresh happen back on the main thread. Never
      ///   throws, and always clears the in-flight guard.
      /// </summary>
      private async Task RunCouncilEngagementExtractionAsync()
      {
         try
         {
            var behavior = NpcMemoryCampaignBehavior.Instance;
            string conversation = _transcript.ToPlainText(PlayerTranscriptLabel());
            if (string.IsNullOrWhiteSpace(conversation) || behavior?.LlmServices?.LlmClient == null) return;

            var extractor = new CouncilEngagementExtractor(behavior.LlmServices.LlmClient);
            string raw = null;

            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(CouncilEngagementExtractTimeoutSeconds)))
            {
               try
               {
                  raw = await Task.Run(() => extractor.ExtractAsync(conversation, cts.Token)).ConfigureAwait(false);
               }
               catch (Exception netEx)
               {
                  ModLog.Error($"[ENGAGEMENTS] extraction call failed: {netEx.Message}");
               }
            }

            await MainThreadDispatcher.SwitchToMainThread();

            // The window may have closed or the council ended while the call was in flight.
            if (_councilScope == null || raw == null) return;

            _engagementLedger.Reconcile(EngagementSnapshotParser.Parse(raw));
            RefreshEngagementsBinding();
            ModLog.Info($"[ENGAGEMENTS] reconciled: {_engagementLedger.Engagements.Count} standing.");
         }
         catch (Exception ex)
         {
            ModLog.Error($"RunCouncilEngagementExtractionAsync failed: {ex.GetType().Name}: {ex.Message}");
         }
         finally
         {
            _engagementExtractionRunning = false;
         }
      }

      /// <summary>Rebuilds the bound Engagements rows from the ledger and refreshes the empty-state gates.</summary>
      private void RefreshEngagementsBinding()
      {
         _engagements.Clear();

         foreach (CouncilEngagement e in _engagementLedger.Engagements)
            _engagements.Add(new CouncilEngagementViewModel(e.Subject, e.Text, DescribeEngagementStatus(e.Status)));

         OnPropertyChanged(nameof(HasEngagements));
         OnPropertyChanged(nameof(ShowEngagementsPlaceholder));
      }

      /// <summary>The seated-panel wording for an engagement's standing: settled versus still under discussion.</summary>
      private static string DescribeEngagementStatus(EngagementStatus status)
         => status == EngagementStatus.Agreed ? "Agreed" : "Discussed";

      #endregion

      #region Engagements seal (apply-at-End)

      /// <summary>
      ///   Council apply-at-End for engagements: real world-effect executors (grant fief, gold, etc.) do not
      ///   exist yet in this codebase (only a bandit-clear quest is executable, see CouncilLift.Settle), so an
      ///   AGREED engagement's applied effect is a durable memory sealed onto the concerned hero, so the NPC
      ///   remembers what the council settled. A decision bound to a seated member (see
      ///   <see cref="EngagementSubjectMatcher" />) seals in the first person on THEM; a collective or unresolved
      ///   subject seals on the presiding anchor instead. What the player saw standing in the Engagements panel
      ///   at the last reconciliation is exactly what is sealed here: the ledger is one turn behind, never
      ///   mid-turn, which is the intended "only what was seen applies" property the whole feature is built on.
      ///   Called once from <see cref="FinalizeCouncilResolutions" />, under the same idempotency guard, so it
      ///   fires once per close.
      /// </summary>
      public void SealCouncilEngagements(NpcMemoryCampaignBehavior behavior, int gameDay)
      {
         try
         {
            if (behavior?.Store == null) return;

            var names = new List<string>();
            var heroIds = new List<string>();

            foreach (CouncilParticipantViewModel seat in _councilParticipants)
            {
               names.Add(seat.Name);
               heroIds.Add(seat.HeroStringId);
            }

            string anchorHeroId = MainSpeaker()?.Hero?.StringId;

            foreach (CouncilEngagement e in _engagementLedger.PendingCommit())
            {
               if (string.IsNullOrWhiteSpace(e.Text)) continue;

               int idx = EngagementSubjectMatcher.MatchIndex(e.Subject, names);
               bool onBound = idx >= 0;
               string heroId = onBound ? heroIds[idx] : anchorHeroId;

               if (string.IsNullOrEmpty(heroId)) continue;

               string summary = EngagementSealText.Summarize(e.Text, onBound);
               if (string.IsNullOrEmpty(summary)) continue;

               NpcProfile profile = behavior.Store.Get(heroId);
               if (profile == null) continue;

               profile.Events.Add(new NotableEvent(gameDay, NotableEventType.Agreement, summary));
               behavior.Store.Set(profile);
               ModLog.Info($"[ENGAGEMENTS] sealed onto {heroId}: {summary}");
            }
         }
         catch (Exception ex)
         {
            ModLog.Error($"SealCouncilEngagements failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      #endregion

      #region IRoundTableHost (council seat highlight)

      void IRoundTableHost.SetActiveCouncilSpeaker(ChatSpeaker speaker) => SetActiveCouncilSpeaker(speaker?.Hero?.StringId);

      void IRoundTableHost.ClearActiveCouncilSpeaker() => ClearActiveCouncilSpeaker();

      #endregion
   }
}
