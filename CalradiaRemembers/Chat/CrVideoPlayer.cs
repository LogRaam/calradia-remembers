// Code written by Gabriel Mailhot, 11/10/2026.
// CR's own video player, shown where a tale's picture stands beside the chat. The game's player stops in conversations;
// this one is driven by the application tick, which runs in them. Each game frame, the clock (Logic/Video/VideoClock) says
// which frame of the video is due; when it changes, the decoded frame (Video/CrvidDecoder, a thread of its own) becomes a
// new engine texture shown in the frame, and older textures are released a few frames later (a spike measured it on
// 11/10/2026: 768x1152 at 30 Hz, nothing skipped, the game unslowed, graphics memory flat). Gabriel (11/10/2026): played once, the video
// comes back to its first frame and waits; a play icon plays it again, a loop icon makes it start over at the end. The
// first frame's texture is kept for that. If the decoder falls behind, the last frame stays rather than a stale one shown.

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Video;
using CalradiaRemembers.Portraits;
using CalradiaRemembers.Video;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Plays one .crvid in the tale's picture frame of the open conversation.</summary>
   internal static class CrVideoPlayer
   {
      /// <summary>Textures kept alive behind the one shown (the layer may still draw the previous one this frame).</summary>
      private const int KeptAlive = 3;

      private static readonly Queue<(Texture Texture, string Path)> Live = new Queue<(Texture, string)>();
      private static readonly Stopwatch Clock = new Stopwatch();
      private static CrvidDecoder _decoder;
      private static CrvidManifest _manifest;
      private static string _file;
      private static Texture _firstTexture;
      private static string _firstPath;
      private static int _shown = -1, _serial;
      private static bool _first;

      /// <summary>A video is open (playing or finished on its first frame).</summary>
      public static bool IsOpen => _manifest != null;

      /// <summary>Playing now (false when finished on its first frame, or none open).</summary>
      public static bool IsPlaying { get; private set; }

      /// <summary>Whether it starts over at the end.</summary>
      public static bool Loop { get; private set; }

      /// <summary>What is open, for the console.</summary>
      public static string Describe()
         => _manifest == null
            ? "No video open."
            : $"{System.IO.Path.GetFileName(_file)}: {_manifest.Width}x{_manifest.Height}, {_manifest.Frames} frames at {_manifest.Fps} fps, "
            + $"{(IsPlaying ? "playing" : "finished on its first frame")}, loop={Loop}, frame {_shown + 1}, "
            + $"swaprb={CrvidDecoder.SwapRedBlue}, flip={CrvidDecoder.FlipVertical}.";

      /// <summary>Opens <paramref name="file" /> in the open conversation's picture frame and plays it. Null on success, else why not.</summary>
      public static string Play(string file, bool? loop)
      {
         ChatViewModel vm = ChatManager.ActiveViewModel;
         if (vm == null)
         {
            Stop();
            return "Open a conversation first: the video shows where a tale's picture stands.";
         }

         // One video after another (a tale's states, each its own video): the frame shown stays until the new video's first
         // frame is decoded; a full Stop would put back, for that moment, the still picture the first video replaced.
         if (IsOpen)
         {
            _decoder?.Dispose();
            _decoder = null;
            if (_firstTexture != null) Live.Enqueue((_firstTexture, _firstPath));
            _firstTexture = null;
            _firstPath = null;
         }

         _decoder = CrvidDecoder.Open(file, loop, out string problem);
         if (_decoder == null)
         {
            Stop();
            return $"{System.IO.Path.GetFileName(file)} cannot play: {problem}";
         }

         _file = file;
         _manifest = _decoder.Manifest;
         Loop = _decoder.Loop;
         _shown = -1;
         _first = true;
         IsPlaying = true;
         Clock.Restart();
         ModLog.Info($"[VIDEO] playing {Describe()}");
         vm.RefreshVideoControls();

         return null;
      }

      /// <summary>The STOP icon (Gabriel, 11/10/2026): the video stops and shows its first frame again; PLAY starts it over.</summary>
      public static void StopPlayback()
      {
         if (IsPlaying) Finish("STOP");
      }

      /// <summary>
      ///   Plays the open video again from its first frame (the PLAY icon). The frame shown stays until the new first frame
      ///   is decoded, so nothing else flashes in between (a full Stop would put back the picture the video replaced).
      /// </summary>
      public static string Replay()
      {
         if (_file == null) return "No video open.";
         _decoder?.Dispose();
         _decoder = CrvidDecoder.Open(_file, Loop, out string problem);
         if (_decoder == null)
         {
            IsPlaying = false;
            ChatManager.ActiveViewModel?.RefreshVideoControls();
            return $"{System.IO.Path.GetFileName(_file)} cannot play: {problem}";
         }

         _shown = -1;
         IsPlaying = true;
         Clock.Restart();
         ModLog.Info($"[VIDEO] played again: {System.IO.Path.GetFileName(_file)}, loop={Loop}.");
         ChatManager.ActiveViewModel?.RefreshVideoControls();

         return null;
      }

      /// <summary>Starts over at the end, or not (the loop icon). Takes effect now, on the playing video too.</summary>
      public static void SetLoop(bool loop)
      {
         Loop = loop;
         if (_decoder != null) _decoder.Loop = loop;
         ChatManager.ActiveViewModel?.RefreshVideoControls();
      }

      /// <summary>Called every game frame (the submodule's application tick).</summary>
      public static void Tick()
      {
         if (_decoder != null)
            while (_decoder.TryTakeError(out string error))
               ModLog.Error("[VIDEO] decoder: " + error);

         if (!IsPlaying || _decoder == null) return;
         if (ChatManager.ActiveViewModel == null)
         {
            Stop();
            return;
         }

         VideoMoment moment = VideoClock.At(Clock.Elapsed.TotalSeconds, _manifest.Fps, _manifest.Frames, Loop);
         if (moment.Finished)
         {
            Finish($"end of play after {Clock.Elapsed.TotalSeconds:0.0}s");
            return;
         }

         // Drop what is already past; show the frame that is due when it is decoded; otherwise keep the last one.
         while (_decoder.TryPeek(out DecodedFrame head) && Behind(head.Index, moment.Frame))
            if (_decoder.TryTake(out DecodedFrame stale)) _decoder.Return(stale.Pixels);
         if (moment.Frame != _shown && _decoder.TryPeek(out DecodedFrame due) && due.Index == moment.Frame && _decoder.TryTake(out due))
            Show(due);
      }

      /// <summary>Stops the video, takes it off the frame, then frees the decoder and the textures.</summary>
      public static void Stop()
      {
         if (_manifest == null && _decoder == null) return;

         ChatManager.ActiveViewModel?.ClearLiveSceneImage();
         _decoder?.Dispose();
         _decoder = null;
         while (Live.Count > 0) Retire();
         if (_firstTexture != null)
         {
            LiveSprites.Unregister(_firstPath);
            try { _firstTexture.ReleaseAfterNumberOfFrames(3); }
            catch (Exception ex) { ModLog.Error($"[VIDEO] release failed: {ex.GetType().Name}: {ex.Message}"); }
         }

         _firstTexture = null;
         _firstPath = null;
         _manifest = null;
         _file = null;
         IsPlaying = false;
         Clock.Reset();
         ChatManager.ActiveViewModel?.RefreshVideoControls();
      }

      // A frame behind the due one (a loop's wrap counted: in a loop, a frame just before the end is behind frame 0).
      private static bool Behind(int frame, int due)
      {
         int n = _manifest.Frames;
         int distance = ((due - frame) % n + n) % n;

         return distance != 0 && distance < n / 2 + 1;
      }

      // Played once and over: back to the first frame, which waits; the decoder's work is done.
      private static void Finish(string why)
      {
         IsPlaying = false;
         _decoder?.Dispose();
         _decoder = null;
         if (_firstPath != null && _shown != 0)
         {
            ChatManager.ActiveViewModel?.SwapLiveSceneImage(_firstPath);
            _shown = 0;
         }

         ModLog.Info($"[VIDEO] finished ({why}); back on its first frame: {System.IO.Path.GetFileName(_file)}.");
         ChatManager.ActiveViewModel?.RefreshVideoControls();
      }

      private static void Show(DecodedFrame frame)
      {
         ChatViewModel vm = ChatManager.ActiveViewModel;
         if (vm == null) return;

         Texture texture;
         try { texture = Texture.CreateFromByteArray(frame.Pixels, _manifest.Width, _manifest.Height); }
         finally { _decoder.Return(frame.Pixels); }

         string name = $"cr_video_frame_{++_serial}";
         Sprite sprite = RuntimePortraitFactory.SpriteFromTexture(texture, name, _manifest.Width, _manifest.Height);
         string path = LiveSprites.LivePrefix + name;
         LiveSprites.Register(path, sprite);

         if (_first) vm.ShowLiveSceneImage(path, _manifest.Width, _manifest.Height);
         else vm.SwapLiveSceneImage(path);
         _first = false;
         _shown = frame.Index;

         // The first frame is kept for the end of a played-once video; the others go once three newer ones stand.
         if (frame.Index == 0 && _firstTexture == null)
         {
            _firstTexture = texture;
            _firstPath = path;
            return;
         }

         Live.Enqueue((texture, path));
         while (Live.Count > KeptAlive) Retire();
      }

      private static void Retire()
      {
         (Texture texture, string path) = Live.Dequeue();
         LiveSprites.Unregister(path);
         try { texture?.ReleaseAfterNumberOfFrames(3); }
         catch (Exception ex) { ModLog.Error($"[VIDEO] release failed: {ex.GetType().Name}: {ex.Message}"); }
      }
   }
}
