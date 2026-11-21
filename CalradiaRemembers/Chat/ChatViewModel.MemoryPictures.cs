// Code written by Gabriel Mailhot, 20/11/2026.
// Pictures of memories in the Memories panel. Bhelogan (Discord, 20/11/2026) asked for his mod's pictures there; Gabriel:
// "Right now the background is an image that cannot be extended. What if I click on a memory record and it shows an image
// left to that panel? The new panel could have a 'generate new image' button that raise an event", and Bhelogan: "or have
// them expand if you click on them, like the dialog images". So: a click on a memory selects it; its picture stands to the
// left of the panel at its own proportions, a click enlarges it and another shrinks it; under it, "Paint this memory" when
// a mod listens (CrNpcMemory.ImageRequested), "Painting..." while it works. A memory with no picture and no painter shows
// nothing beside the panel, as before.

#region

using System;
using CalradiaRemembers.Logic.AuthoredQuests;
using CalradiaRemembers.Logic.Extension;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   public sealed partial class ChatViewModel
   {
      /// <summary>TUNE: the gap between the Memories panel and the picture, and the panel's half width (536 / 2).</summary>
      private const float MemoryPictureGap = 24f, MemoryPanelHalfWidth = 268f;

      private MemoryEntryViewModel _selectedMemory;
      private bool _memoryPictureLarge;
      private bool _memoryPictureWasBusy;
      private DateTime _lastMemoryPictureTick = DateTime.MinValue;

      /// <summary>The selected memory's picture (CR's copy), or "" when it has none or the file is gone.</summary>
      [DataSourceProperty]
      public string MemoryPicturePath => MemoryPictures.PathOf(_selectedMemory?.Memory) ?? "";

      /// <summary>The picture's box, kept to its proportions: small beside the panel, large when clicked.</summary>
      [DataSourceProperty]
      public float MemoryPictureWidth => PictureSize().Width;

      /// <inheritdoc cref="MemoryPictureWidth" />
      [DataSourceProperty]
      public float MemoryPictureHeight => PictureSize().Height;

      /// <summary>Where the picture stands: left of the panel when small, centred over the screen when large.</summary>
      [DataSourceProperty]
      public float MemoryPictureOffsetX => _memoryPictureLarge ? 0f : -(MemoryPanelHalfWidth + MemoryPictureGap + PictureSize().Width / 2f);

      /// <summary>The picture is drawn (panel open, a memory selected, its file on disk).</summary>
      [DataSourceProperty]
      public bool IsMemoryPictureVisible => _isMemoryPanelOpen && MemoryPicturePath.Length > 0;

      /// <summary>The picture beside the panel, its empty frame or its paint button: something stands left of the panel.</summary>
      [DataSourceProperty]
      public bool IsMemoryPictureGroupVisible => !_memoryPictureLarge && (IsMemoryPictureVisible || IsMemoryPicturePlaceholderVisible || IsMemoryPaintVisible);

      /// <summary>The picture enlarged over the screen (a click shrinks it back).</summary>
      [DataSourceProperty]
      public bool IsMemoryPictureLarge => _memoryPictureLarge && IsMemoryPictureVisible;

      /// <summary>The empty frame stands in for a picture not painted yet, when a mod could paint it.</summary>
      [DataSourceProperty]
      public bool IsMemoryPicturePlaceholderVisible => _isMemoryPanelOpen && _selectedMemory?.Memory != null && MemoryPicturePath.Length == 0 && OffersPainting();

      /// <summary>The paint button shows (a mod listens, the memory is not a secret) and the picture is not enlarged.</summary>
      [DataSourceProperty]
      public bool IsMemoryPaintVisible => _isMemoryPanelOpen && _selectedMemory?.Memory != null && OffersPainting() && !_memoryPictureLarge;

      /// <summary>"Paint this memory", "Paint it anew", or "Painting..." while a painting is on its way.</summary>
      [DataSourceProperty]
      public string MemoryPaintLabel => MemoryPicturePolicy.PaintLabel(PaintFacts());

      /// <summary>False while a painting is on its way, so the button cannot be pressed twice.</summary>
      [DataSourceProperty]
      public bool IsMemoryPaintEnabled => !PaintFacts().Busy;

      /// <summary>A click on a row: that memory is selected (a second click on it keeps it).</summary>
      internal void SelectMemory(MemoryEntryViewModel row)
      {
         if (_selectedMemory != null) _selectedMemory.IsSelected = false;
         _selectedMemory = row?.Memory != null ? row : null;
         if (_selectedMemory != null)
         {
            _selectedMemory.IsSelected = true;
            // The memory gets its id now: a picture asked for or set later hangs on it.
            MemoryPictures.EnsureId(_selectedMemory.Memory);
         }

         _memoryPictureLarge = false;
         NotifyMemoryPicture();
      }

      /// <summary>A click on the picture enlarges it; another shrinks it.</summary>
      public void ExecuteToggleMemoryPictureSize()
      {
         _memoryPictureLarge = !_memoryPictureLarge;
         NotifyMemoryPicture();
      }

      /// <summary>"Paint this memory": the request goes to every mod that paints (CrNpcMemory.ImageRequested).</summary>
      public void ExecutePaintMemory()
      {
         try
         {
            if (_selectedMemory?.Memory == null || !OffersPainting() || PaintFacts().Busy) return;

            string npcId = MainSpeaker().Profile?.Id;
            MemoryPictures.Request(npcId, _selectedMemory.Memory);
            NotifyMemoryPicture();
         }
         catch (Exception ex) { Foundation.ModLog.Error($"[MEMORY PICTURE] paint request failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>A picture was set, taken away or failed for this memory: the row and the picture are read again.</summary>
      internal void OnMemoryPictureChanged(string memoryId)
      {
         try
         {
            var profile = MainSpeaker().Profile;
            foreach (MemoryEntryViewModel row in _memories)
            {
               if (row.Memory == null || row.Memory.Id != memoryId) continue;

               // Attach replaced the memory with a copy carrying the picture: the row follows it.
               row.Memory = profile?.Events?.Find(e => e != null && e.Id == memoryId) ?? row.Memory;
               row.PictureChanged();
            }

            NotifyMemoryPicture();
         }
         catch (Exception ex) { Foundation.ModLog.Error($"[MEMORY PICTURE] panel refresh failed: {ex.GetType().Name}: {ex.Message}"); }
      }

      /// <summary>Once a second while the chat is open: a painting that timed out offers its button again.</summary>
      internal void TickMemoryPicture()
      {
         if (!_isMemoryPanelOpen || _selectedMemory == null) return;

         DateTime now = DateTime.UtcNow;
         if ((now - _lastMemoryPictureTick).TotalSeconds < 1) return;
         _lastMemoryPictureTick = now;

         bool busy = PaintFacts().Busy;
         if (busy == _memoryPictureWasBusy) return;
         _memoryPictureWasBusy = busy;
         NotifyMemoryPicture();
      }

      #region private

      private bool OffersPainting() => MemoryPicturePolicy.OffersPainting(PaintFacts());

      private MemoryPaintFacts PaintFacts()
      {
         var memory = _selectedMemory?.Memory;

         return new MemoryPaintFacts {
            AnyPainter = CrNpcMemory.AnyPainter, IsPrivate = memory?.IsPrivate == true,
            HasPicture = !string.IsNullOrEmpty(memory?.ImageFile), Busy = memory?.Id != null && MemoryPictures.IsBusy(memory.Id)
         };
      }

      private (float Width, float Height) PictureSize()
      {
         float aspectW = 4f, aspectH = 3f;
         string path = MemoryPicturePath;
         if (path.Length > 0 && CalradiaRemembers.Portraits.RuntimePortraitFactory.TryGetDimensions(path, out int w, out int h) && w > 0 && h > 0)
         {
            aspectW = w;
            aspectH = h;
         }

         return SceneImageLayout.Fit(aspectW, aspectH, !_memoryPictureLarge);
      }

      private void NotifyMemoryPicture()
      {
         _memoryPictureWasBusy = PaintFacts().Busy;
         OnPropertyChanged(nameof(MemoryPicturePath));
         OnPropertyChanged(nameof(MemoryPictureWidth));
         OnPropertyChanged(nameof(MemoryPictureHeight));
         OnPropertyChanged(nameof(MemoryPictureOffsetX));
         OnPropertyChanged(nameof(IsMemoryPictureVisible));
         OnPropertyChanged(nameof(IsMemoryPictureGroupVisible));
         OnPropertyChanged(nameof(IsMemoryPictureLarge));
         OnPropertyChanged(nameof(IsMemoryPicturePlaceholderVisible));
         OnPropertyChanged(nameof(IsMemoryPaintVisible));
         OnPropertyChanged(nameof(MemoryPaintLabel));
         OnPropertyChanged(nameof(IsMemoryPaintEnabled));
      }

      #endregion
   }
}
