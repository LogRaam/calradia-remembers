// Code written by Gabriel Mailhot, 30/07/2026.

#region

using System;
using CalradiaRemembers.GUI;
using CalradiaRemembers.Portraits;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>
   ///   One seated member of the bespoke COUNCIL window's left "CLAN JOUEUR" strip: a portrait tile (the anchor
   ///   or a witness) with a seat label, clickable to open that hero's dossier. Portrait resolution mirrors
   ///   <see cref="ChatMessageViewModel" /> exactly (vanilla tableau via <see cref="PortraitTextureGuard" />, then
   ///   a custom ChatPortraits image via <see cref="ChatPortraitResolver" /> if the player has dropped one for this
   ///   hero) so a seat never shows the wrong face and each seat gets its OWN texture rather than the first hero's.
   /// </summary>
   public sealed class CouncilParticipantViewModel : ViewModel
   {
      private bool _isActiveSpeaker;
      private string _portraitAdditionalArgs = "";
      private string _portraitId = "";
      private string _portraitTextureProviderName = "";

      /// <summary>Default seat tile size: a vanilla 3D character render is taller than wide; a square tile squished it.</summary>
      private const float DefaultPortraitWidth = 54f;

      private const float DefaultPortraitHeight = 72f;

      /// <summary>Box a custom PNG fits into, aspect-preserved (mirrors ChatMessageViewModel.ApplyCustomPortrait).</summary>
      private const float CustomPortraitBoxWidth = 64f;

      private const float CustomPortraitBoxHeight = 80f;

      public CouncilParticipantViewModel(Hero hero, string seatLabel)
      {
         HeroStringId = hero?.StringId ?? "";
         Name = hero?.Name?.ToString() ?? "Unknown";
         SeatLabel = seatLabel ?? "";
         PortraitWidth = DefaultPortraitWidth;
         PortraitHeight = DefaultPortraitHeight;

         ResolvePortrait(hero);
         ApplyCustomPortrait(hero?.StringId, hero?.Name?.ToString());
      }

      /// <summary>The Bannerlord hero id this seat belongs to, used to route a dossier click and highlight requests.</summary>
      public string HeroStringId { get; }

      /// <summary>
      ///   True while this seated member is currently taking their turn in the council's per-member lap, driving
      ///   a highlight border on their tile. Off by default; toggled by <see cref="ChatViewModel.SetActiveCouncilSpeaker" />
      ///   / <see cref="ChatViewModel.ClearActiveCouncilSpeaker" />.
      /// </summary>
      [DataSourceProperty]
      public bool IsActiveSpeaker
      {
         get => _isActiveSpeaker;
         set
         {
            if (_isActiveSpeaker == value) return;
            _isActiveSpeaker = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string Name { get; }

      [DataSourceProperty]
      public string PlayerPortraitPath { get; private set; } = "";

      /// <summary>
      ///   The seat tile's portrait height in Gauntlet units, aspect-correct: taller-than-wide for the default
      ///   vanilla render, or fit to the custom-PNG box when this seat has a dropped image. Squished flat
      ///   before this (a fixed 66x66 square) because a character render (or most portrait art) is not square.
      /// </summary>
      [DataSourceProperty]
      public float PortraitHeight { get; private set; }

      /// <summary>See <see cref="PortraitHeight" />.</summary>
      [DataSourceProperty]
      public float PortraitWidth { get; private set; }

      [DataSourceProperty]
      public string PortraitAdditionalArgs
      {
         get => _portraitAdditionalArgs;
         private set
         {
            if (_portraitAdditionalArgs == value) return;
            _portraitAdditionalArgs = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string PortraitId
      {
         get => _portraitId;
         private set
         {
            if (_portraitId == value) return;
            _portraitId = value;
            OnPropertyChangedWithValue(value);
         }
      }

      [DataSourceProperty]
      public string PortraitTextureProviderName
      {
         get => _portraitTextureProviderName;
         private set
         {
            if (_portraitTextureProviderName == value) return;
            _portraitTextureProviderName = value;
            OnPropertyChangedWithValue(value);
         }
      }

      /// <summary>The seat's description, e.g. "kin of the player's house, seated at this council".</summary>
      [DataSourceProperty]
      public string SeatLabel { get; }

      [DataSourceProperty]
      public bool ShowCustomPortrait { get; private set; }

      [DataSourceProperty]
      public bool ShowVanillaPortrait => !ShowCustomPortrait && !string.IsNullOrEmpty(_portraitId);

      /// <summary>Left-click on this seat's tile: opens the full dossier for this hero. Bound via Command.Click.</summary>
      public void ExecuteOpenDossier()
      {
         try
         {
            ChatManager.ActiveViewModel?.OpenCouncilDossier(HeroStringId);
         }
         catch (Exception ex)
         {
            ModLog.Error($"CouncilParticipantViewModel.ExecuteOpenDossier failed: {ex.GetType().Name}: {ex.Message}");
         }
      }

      /// <summary>
      ///   Frees this seat's vanilla character tableau (a GPU render-target) at council close. A custom portrait
      ///   is a cached sprite (no render-target), so there is nothing to free and it must stay visible.
      /// </summary>
      public void ReleasePortrait()
      {
         if (ShowCustomPortrait) return;
         if (string.IsNullOrEmpty(_portraitId)) return;

         PortraitId = "";
         PortraitTextureProviderName = "";
         PortraitAdditionalArgs = "";
      }

      /// <summary>
      ///   Custom chat portraits: if the player has dropped an image for this hero in ModuleData/ChatPortraits/
      ///   (by StringId or name), show it instead of the vanilla 3D render.
      /// </summary>
      private void ApplyCustomPortrait(string stringId, string displayName)
      {
         string path = ChatPortraitResolver.ResolveForHero(stringId, displayName);

         if (string.IsNullOrEmpty(path)) return;

         PlayerPortraitPath = path;
         ShowCustomPortrait = true;

         // Fit the PNG into the custom box, aspect-preserved (mirrors ChatMessageViewModel.ApplyCustomPortrait);
         // fall back to the default tile size if the file's dimensions cannot be read.
         if (RuntimePortraitFactory.TryGetDimensions(path, out int w, out int h) && w > 0 && h > 0)
         {
            float scale = Math.Min(CustomPortraitBoxWidth / w, CustomPortraitBoxHeight / h);
            PortraitWidth = w * scale;
            PortraitHeight = h * scale;
         }
         else
         {
            PortraitWidth = DefaultPortraitWidth;
            PortraitHeight = DefaultPortraitHeight;
         }

         // A custom seat uses a cached sprite, not a 3D render-target: clear the vanilla ids so it is never
         // counted as a live tableau nor released by ReleasePortrait (it would vanish).
         _portraitId = "";
         _portraitTextureProviderName = "";
         _portraitAdditionalArgs = "";
      }

      private void ResolvePortrait(Hero hero)
      {
         PortraitData portrait = PortraitTextureGuard.Create(hero?.CharacterObject);
         _portraitId = portrait.Id;
         _portraitTextureProviderName = portrait.TextureProviderName;
         _portraitAdditionalArgs = portrait.AdditionalArgs;
      }
   }
}
