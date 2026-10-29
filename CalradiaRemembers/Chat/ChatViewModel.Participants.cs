// Code written by Gabriel Mailhot, 29/10/2026.
// The participants panel. RaveN017Mod (Nexus PM, 29/10/2026): the clan members he invited "often don't interact much
// at all", and he wanted a way to make it clear who he is addressing. They were witnesses, and the only way to draw
// one in (speaking their name) was invisible. Gabriel, 29/10/2026: a panel opening on the left, listing the active
// voices and the witnesses, where a click on a witness makes them active; witnesses keep behaving as they do today,
// and the active ones speak every turn, like the council. The rows come from CastPanelPolicy; the roles and the
// three-voice cap stay in ConversationCast, which the name-addressing road asks too.

#region

using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Conversation;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      private readonly MBBindingList<ParticipantRowViewModel> _participantRows = new MBBindingList<ParticipantRowViewModel>();
      private bool _isParticipantsOpen;

      /// <summary>The panel's rows: the one spoken to first, then the active voices, the witnesses and the bystanders.</summary>
      [DataSourceProperty]
      public MBBindingList<ParticipantRowViewModel> ParticipantRows => _participantRows;

      /// <summary>
      ///   The panel can be opened: an ordinary conversation with someone else present. Not in a council (its own
      ///   window seats everyone), nor a captive or captor scene (no ensemble turn there), nor once it has ended.
      /// </summary>
      [DataSourceProperty]
      public bool IsParticipantsAvailable => _mode == ChatMode.Conversation && !_conversationEnded && !_captorScene
                                             && _councilScope == null && _cast != null && _witnesses.Count > 0;

      /// <summary>The panel is open (and only while it can be).</summary>
      [DataSourceProperty]
      public bool IsParticipantsOpen
      {
         get => _isParticipantsOpen && IsParticipantsAvailable;
         set
         {
            if (_isParticipantsOpen == value) return;
            _isParticipantsOpen = value;
            OnPropertyChanged(nameof(IsParticipantsOpen));
         }
      }

      /// <summary>Who the player's next words go to (blank: the one the conversation opened on). For cr.test.</summary>
      public string AddressedHeroIdForDiagnostics => _currentSpeakerHeroId ?? "";

      /// <summary>The panel's title.</summary>
      [DataSourceProperty]
      public string ParticipantsTitle => "Who Is Here";

      /// <summary>The line under the rows: how the panel works, and the cap.</summary>
      [DataSourceProperty]
      public string ParticipantsFootnote
         => $"Click a name to speak to them. A witness you speak to joins the conversation; at most {ConversationCast.MaxActiveVoices} speak at once.";

      /// <summary>The close button's label.</summary>
      [DataSourceProperty]
      public string ParticipantsCloseText => "Close";

      /// <summary>Opens or closes the panel (the witnesses line in the title bar). The review panel shares that ground, so it closes.</summary>
      public void ExecuteToggleParticipants()
      {
         if (!IsParticipantsAvailable) return;

         IsParticipantsOpen = !_isParticipantsOpen;
         if (_isParticipantsOpen && _isComposeOpen) IsComposeOpen = false;
         RefreshParticipants();
      }

      /// <summary>Closes the panel.</summary>
      public void ExecuteCloseParticipants() => IsParticipantsOpen = false;

      /// <summary>
      ///   Turns the player to <paramref name="heroId" />: their next words go to them, as if they had spoken the
      ///   name. A witness is drawn in for good while there is room; at a full table they are answered this turn
      ///   and stay a witness. Public so cr.test drives the same road the click does.
      /// </summary>
      public void AddressFromPanel(string heroId)
      {
         if (string.IsNullOrEmpty(heroId) || _cast?.RoleOf(heroId) == null) return;

         string resolved = heroId == _npcHero?.StringId ? null : heroId;
         if (resolved != null && _witnesses.All(w => w?.HeroStringId != resolved)) return; // left the room

         _currentSpeakerHeroId = resolved;
         string name = resolved == null ? _npcName : SpeakerForCurrent().Name;
         ModLog.Info($"[ADDRESS] turn routed to {name} (participants panel)");

         if (resolved != null && _cast.Promote(resolved))
         {
            ModLog.Info($"[SPOTLIGHT] {name} promoted to an active participant (participants panel).");
            AppendSystemMessage($"{name} joins the conversation.", true);
         }

         RefreshWitnessUi();
      }

      /// <summary>
      ///   Sends an active voice back among the witnesses: they listen and react again, as before. A player's choice
      ///   in the interface, not an order given in the scene, so it always lands (an order spoken aloud is
      ///   SilenceOrderPolicy's). Never the one the conversation opened on. Public so cr.test drives it.
      /// </summary>
      public void StepBackFromPanel(string heroId)
      {
         if (_cast == null || !_cast.Demote(heroId)) return;

         string name = _witnesses.FirstOrDefault(w => w?.HeroStringId == heroId)?.Name ?? heroId;
         if (_currentSpeakerHeroId == heroId) _currentSpeakerHeroId = null; // back to the one the conversation is with
         ModLog.Info($"[SPOTLIGHT] {name} stepped back to a witness (participants panel).");
         AppendSystemMessage($"{name} steps back and listens.", true);

         RefreshWitnessUi();
      }

      /// <summary>
      ///   Takes someone who left the room out of the cast too, so they neither keep the scene an ensemble nor
      ///   hold one of the three seats.
      /// </summary>
      private void ForgetFromCast(string heroId)
      {
         if (_cast?.Remove(heroId) == true) ModLog.Info($"[SPOTLIGHT] {heroId} left the scene and the cast.");
      }

      /// <summary>Takes every departed witness out of the cast (a private audience, a room emptied).</summary>
      private void ForgetDepartedFromCast()
      {
         if (_cast == null) return;

         var present = new HashSet<string>(_witnesses.Where(w => !string.IsNullOrEmpty(w?.HeroStringId)).Select(w => w.HeroStringId));
         foreach (string id in _cast.ActiveIds.Concat(_cast.WitnessIds).Where(id => id != _cast.AnchorId && !present.Contains(id)).ToList())
            ForgetFromCast(id);
      }

      /// <summary>Rebuilds the rows from the cast and who is present. Called with the witnesses line.</summary>
      private void RefreshParticipants()
      {
         _participantRows.Clear();
         if (IsParticipantsAvailable)
         {
            IReadOnlyList<CastPanelRow> rows = CastPanelPolicy.Rows(new CastPanelFacts {
               Cast = _cast,
               AnchorName = _npcName,
               Present = _witnesses.Where(w => w != null).Select(w => new CastPanelMember {Id = w.HeroStringId, Name = w.Name}).ToList(),
               AddressedId = _currentSpeakerHeroId
            });

            foreach (CastPanelRow row in rows)
               _participantRows.Add(new ParticipantRowViewModel(row, AddressFromPanel, StepBackFromPanel));
         }

         OnPropertyChanged(nameof(IsParticipantsAvailable));
         OnPropertyChanged(nameof(IsParticipantsOpen));
      }
   }
}
