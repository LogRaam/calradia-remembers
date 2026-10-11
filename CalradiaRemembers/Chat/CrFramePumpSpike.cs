// Code written by Gabriel Mailhot, 11/10/2026.
// SPIKE. The game's video player does not run while a conversation is open (vanilla or CR: Gabriel's runs, 11/10/2026),
// and a tale is always lived in conversation. Gabriel: "on pourrait developper notre propre runtime (boucle RT) qui
// appartiendrait au chat et qui permettrait de jouer un video", from a format of our own (a sequence of images). Decoding
// is solved (a background thread decodes JPEG frames ahead); the one unknown is DISPLAY: the engine has no way to update a
// texture in place, so each new frame is a NEW texture (Texture.CreateFromByteArray) shown in the tale's picture frame, the
// previous one released. This measures exactly that, with frames computed once beforehand so only the engine's cost is
// seen: frames shown and skipped, the cost of each creation, the game's frame time, graphics and managed memory, per
// second, in a conversation, for a minute. cr.video_spike pump <width> <height> <hz> [seconds]. In Chat, not Portraits:
// it drives the chat's picture frame, and Portraits must not come to know Chat (the namespace-cycle ratchet).

#region

using System;
using System.Collections.Generic;
using System.Diagnostics;
using CalradiaRemembers.Diagnostics;
using CalradiaRemembers.Portraits;
using TaleWorlds.TwoDimension;
using Texture = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers.Chat
{
    /// <summary>Shows a new engine texture per frame in the tale's picture frame, at a fixed rate, and measures it.</summary>
    internal static class CrFramePumpSpike
    {
        private const int DistinctFrames = 16;
        private const int KeptAlive = 3;

        private static readonly Queue<(Texture Texture, string Path)> Live = new Queue<(Texture, string)>();
        private static readonly Stopwatch Clock = new Stopwatch();
        private static byte[][] _frames;
        private static int _width, _height, _shown, _serial;
        private static float _hz, _seconds;
        private static bool _first;

        // Per-second and whole-run measures.
        private static int _secFrames, _secSkipped, _totalFrames, _totalSkipped, _secTicks;
        private static double _secCreateMs, _secCreateMax, _secDtMax, _secDtSum, _runCreateMax;
        private static int _lastSecond;
        private static long _vramStart;

        public static bool Running { get; private set; }

        public static string Start(int width, int height, float hz, float seconds)
        {
            Stop();
            ChatViewModel vm = ChatManager.ActiveViewModel;
            if (vm == null) return "Open a conversation first: the frames show where a tale's picture stands.";

            _width = width;
            _height = height;
            _hz = hz;
            _seconds = seconds;
            _frames = new byte[DistinctFrames][];
            for (var i = 0; i < DistinctFrames; i++) _frames[i] = Frame(width, height, i);

            _shown = -1;
            _first = true;
            _secFrames = _secSkipped = _totalFrames = _totalSkipped = _secTicks = 0;
            _secCreateMs = _secCreateMax = _secDtMax = _secDtSum = _runCreateMax = 0;
            _lastSecond = 0;
            _vramStart = VramProbe.Read().GameDedicatedMb;
            Running = true;
            Clock.Restart();
            string report = $"pumping {width}x{height} at {hz} Hz for {seconds} s ({DistinctFrames} distinct frames, a new texture each); "
                          + $"VRAM {_vramStart} MB, managed {GC.GetTotalMemory(false) / 1048576} MB.";
            ModLog.Info("[PUMP] " + report);

            return report;
        }

        /// <summary>Called every game frame (the submodule's application tick).</summary>
        public static void Tick(float dt)
        {
            if (!Running) return;

            double elapsed = Clock.Elapsed.TotalSeconds;
            if (elapsed >= _seconds)
            {
                Stop();
                return;
            }

            _secTicks++;
            _secDtSum += dt;
            _secDtMax = Math.Max(_secDtMax, dt * 1000.0);

            var index = (int) (elapsed * _hz);
            if (index != _shown)
            {
                if (_shown >= 0 && index > _shown + 1)
                {
                    _secSkipped += index - _shown - 1;
                    _totalSkipped += index - _shown - 1;
                }

                _shown = index;
                Show(index);
            }

            var second = (int) elapsed;
            if (second != _lastSecond)
            {
                _lastSecond = second;
                ModLog.Info($"[PUMP] s{second}: shown {_secFrames}, skipped {_secSkipped}, create avg {(_secFrames > 0 ? _secCreateMs / _secFrames : 0):F2} ms "
                          + $"max {_secCreateMax:F2} ms, game frame avg {(_secTicks > 0 ? _secDtSum / _secTicks * 1000 : 0):F1} ms max {_secDtMax:F1} ms, "
                          + $"VRAM {VramProbe.Read().GameDedicatedMb} MB, managed {GC.GetTotalMemory(false) / 1048576} MB.");
                _secFrames = _secSkipped = _secTicks = 0;
                _secCreateMs = _secCreateMax = _secDtMax = _secDtSum = 0;
            }
        }

        public static void Stop()
        {
            if (!Running) return;

            Running = false;
            ChatManager.ActiveViewModel?.ClearLiveSceneImage();
            while (Live.Count > 0) Retire();
            _frames = null;
            ModLog.Info($"[PUMP] stopped after {Clock.Elapsed.TotalSeconds:F1} s: {_totalFrames} frames shown, {_totalSkipped} skipped, "
                      + $"worst creation {_runCreateMax:F2} ms; VRAM {_vramStart} -> {VramProbe.Read().GameDedicatedMb} MB, managed {GC.GetTotalMemory(false) / 1048576} MB.");
        }

        private static void Show(int index)
        {
            var watch = Stopwatch.StartNew();
            Texture texture = Texture.CreateFromByteArray(_frames[index % DistinctFrames], _width, _height);
            string name = $"cr_pump_{++_serial}";
            Sprite sprite = RuntimePortraitFactory.SpriteFromTexture(texture, name, _width, _height);
            string path = CrVideoSurface.LivePrefix + name;
            CrVideoSurface.Register(path, sprite);
            watch.Stop();

            ChatViewModel vm = ChatManager.ActiveViewModel;
            if (vm == null)
            {
                Stop();
                return;
            }

            if (_first) vm.ShowLiveSceneImage(path, _width, _height);
            else vm.SwapLiveSceneImage(path);
            _first = false;

            Live.Enqueue((texture, path));
            while (Live.Count > KeptAlive) Retire();

            double ms = watch.Elapsed.TotalMilliseconds;
            _secFrames++;
            _totalFrames++;
            _secCreateMs += ms;
            _secCreateMax = Math.Max(_secCreateMax, ms);
            _runCreateMax = Math.Max(_runCreateMax, ms);
        }

        // A frame no widget shows any more: its sprite forgotten, its texture released a few frames later.
        private static void Retire()
        {
            (Texture texture, string path) = Live.Dequeue();
            CrVideoSurface.Unregister(path);
            try { texture?.ReleaseAfterNumberOfFrames(3); }
            catch (Exception ex) { ModLog.Error($"[PUMP] release failed: {ex.GetType().Name}: {ex.Message}"); }
        }

        // A grey gradient with a white bar at a position set by the frame number: motion that shows any stutter. Grey and
        // white read the same whichever order the engine takes the colour channels in.
        private static byte[] Frame(int width, int height, int i)
        {
            var pixels = new byte[width * height * 4];
            int barX = i * width / DistinctFrames;
            int barW = Math.Max(4, width / DistinctFrames);
            for (var y = 0; y < height; y++)
            {
                var shade = (byte) (40 + 120 * y / height);
                int row = y * width * 4;
                for (var x = 0; x < width; x++)
                {
                    byte v = x >= barX && x < barX + barW ? (byte) 255 : shade;
                    int p = row + x * 4;
                    pixels[p] = v;
                    pixels[p + 1] = v;
                    pixels[p + 2] = v;
                    pixels[p + 3] = 255;
                }
            }

            return pixels;
        }
    }
}
