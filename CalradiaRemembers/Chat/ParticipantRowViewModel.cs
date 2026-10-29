// Code written by Gabriel Mailhot, 29/10/2026.
// One line of the chat's participants panel (ChatViewModel.Participants): a name to click to speak to them, a word on
// where they stand, and for an active voice a button to send them back among the witnesses.

#region

using System;
using CalradiaRemembers.Logic.Conversation;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>One person present in the conversation, as the participants panel shows them.</summary>
   public sealed class ParticipantRowViewModel : ViewModel
   {
      private readonly Action<string> _address;
      private readonly Action<string> _stepBack;
      private readonly CastPanelRow _row;

      public ParticipantRowViewModel(CastPanelRow row, Action<string> address, Action<string> stepBack)
      {
         _row = row;
         _address = address;
         _stepBack = stepBack;
      }

      /// <summary>The name, marked when the player's next words go to them.</summary>
      [DataSourceProperty]
      public string NameText => _row.IsAddressed ? $"▸ {_row.Name}" : _row.Name;

      /// <summary>Where they stand, in a few words.</summary>
      [DataSourceProperty]
      public string StandingText
      {
         get
         {
            switch (_row.Standing)
            {
               case CastPanelStanding.Anchor:
                  return "the one you came to see";
               case CastPanelStanding.Active:
                  return "speaking";
               case CastPanelStanding.Witness:
                  return _row.WouldJoin ? "watching: click to draw them in" : $"watching ({ConversationCast.MaxActiveVoices} already speak)";
               default:
                  return "looking on";
            }
         }
      }

      /// <summary>The player is speaking to them now.</summary>
      [DataSourceProperty]
      public bool IsAddressed => _row.IsAddressed;

      /// <summary>The name cannot be clicked: already spoken to, or a bystander with no character behind them.</summary>
      [DataSourceProperty]
      public bool IsAddressDisabled => !_row.CanAddress;

      /// <summary>An active voice the player may send back.</summary>
      [DataSourceProperty]
      public bool CanStepBack => _row.CanStepBack;

      /// <summary>The step-back button's label.</summary>
      [DataSourceProperty]
      public string StepBackText => "Step back";

      /// <summary>Speak to them.</summary>
      public void ExecuteAddress()
      {
         if (_row.CanAddress) _address?.Invoke(_row.Id);
      }

      /// <summary>Send them back among the witnesses.</summary>
      public void ExecuteStepBack()
      {
         if (_row.CanStepBack) _stepBack?.Invoke(_row.Id);
      }
   }
}
