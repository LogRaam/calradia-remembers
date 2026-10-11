// Code written by Gabriel Mailhot, 11/10/2026.
// SPIKE. Gabriel (11/10/2026): could a tale show a video where it shows a picture? "Je prefere 2": the video in the
// tale's own picture frame, no sound, played once then back to its first frame, a small play icon to play it again and a
// loop icon. The game ships a video player (TaleWorlds.Engine.VideoPlayerView, the TaleWorlds logo at start-up, VP8 .ivf
// with its sound apart) but only ever plays it full screen. A View can render into a texture instead of the screen
// (View.SetRenderTarget; it is how the 3D character portraits reach the menus), and CR already turns an engine texture
// into a sprite (RuntimePortraitFactory). This puts the two together; cr.video_spike drives it. What the spike must
// answer: does the video reach the texture, is it smooth, does graphics memory stay flat, and what does the texture
// show before the first frame and after the last.

#region

using System.Collections.Generic;
using TaleWorlds.Engine;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>One video playing into a texture, shown by any <see cref="CrRuntimeImageWidget" /> whose path names it.</summary>
    internal static class CrVideoSurface
    {
        /// <summary>The image-path prefix a widget reads as "a live sprite", not a file.</summary>
        public const string LivePrefix = "cr-live:";

        private static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        private static VideoPlayerView _player;
        private static Texture _target;
        private static int _serial;

        /// <summary>The live path of the video playing now, or null.</summary>
        public static string CurrentPath { get; private set; }

        /// <summary>The sprite a widget shows for <paramref name="livePath" />, or null when nothing plays under it.</summary>
        public static Sprite SpriteFor(string livePath)
            => livePath != null && Sprites.TryGetValue(livePath, out Sprite s) ? s : null;

        /// <summary>
        ///   Plays <paramref name="file" /> into a new render target of <paramref name="width" /> x <paramref name="height" />
        ///   and returns its live path (null and a reason on failure). Any video already playing is stopped first.
        /// </summary>
        public static string Start(string file, int width, int height, float fps, bool loop, out string report)
        {
            Stop();
            _serial++;
            string name = $"cr_video_{_serial}";
            try
            {
                _target = Texture.CreateRenderTarget(name, width, height, false, false, false, true);
                if (_target == null)
                {
                    report = "CreateRenderTarget returned null.";
                    return null;
                }

                _player = VideoPlayerView.CreateVideoPlayerView();
                if (_player == null)
                {
                    report = "CreateVideoPlayerView returned null.";
                    return null;
                }

                _player.SetRenderTarget(_target);
                _player.SetRenderOrder(-10000);
                _player.PlayVideo(file, "", fps, loop);

                string live = LivePrefix + name;
                Sprites[live] = RuntimePortraitFactory.SpriteFromTexture(_target, name, width, height);
                CurrentPath = live;
                report = $"playing {System.IO.Path.GetFileName(file)} into {name} ({width}x{height}, {fps} fps, loop={loop}); target IsRenderTarget={_target.IsRenderTarget}.";
                ModLog.Info("[VIDEO] " + report);

                return live;
            }
            catch (System.Exception ex)
            {
                report = $"{ex.GetType().Name}: {ex.Message}";
                ModLog.Error("[VIDEO] start failed: " + report);
                Stop();

                return null;
            }
        }

        /// <summary>Whether the video has played to its end (always false while looping), or null when none plays.</summary>
        public static bool? IsFinished()
        {
            try { return _player?.IsVideoFinished(); }
            catch (System.Exception ex)
            {
                ModLog.Error($"[VIDEO] IsVideoFinished failed: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        ///   Stops the video and frees the player, and its texture a few frames later. The caller takes the live picture off
        ///   its widget FIRST (ChatViewModel.ClearLiveSceneImage): a widget must never draw a released texture.
        /// </summary>
        public static void Stop()
        {
            try
            {
                if (_player != null)
                {
                    _player.StopVideo();
                    _player.SetEnable(false);
                    _player.FinalizePlayer();
                    ModLog.Info("[VIDEO] stopped and finalized.");
                }
            }
            catch (System.Exception ex) { ModLog.Error($"[VIDEO] stop failed: {ex.GetType().Name}: {ex.Message}"); }

            // A few frames late: the layer may still draw this frame with the sprite it had.
            try { _target?.ReleaseAfterNumberOfFrames(3); }
            catch (System.Exception ex) { ModLog.Error($"[VIDEO] texture release failed: {ex.GetType().Name}: {ex.Message}"); }

            _player = null;
            _target = null;
            if (CurrentPath != null) Sprites.Remove(CurrentPath);
            CurrentPath = null;
        }
    }
}
