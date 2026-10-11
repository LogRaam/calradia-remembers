// Code written by Gabriel Mailhot, 11/10/2026.
// Reads a .crvid (CR's own video: a zip of JPEG frames and a manifest, see Logic/Video/Crvid.cs) and decodes its frames
// ahead on a thread of its own, so the game's frame never waits on a JPEG. The thread owns the archive (ZipArchive is not
// thread-safe); the main thread only takes decoded frames and makes textures of them (CrVideoPlayer). At most a few frames
// wait decoded, in buffers used over and over: a 768x1152 frame is 3.5 MB, and allocating one thirty times a second would
// fill the large-object heap and stall the game in collections. Errors on the thread are queued for the main thread to log
// (ModLog is not thread-safe). Pixel order and orientation are switches: the game's CreateFromByteArray was never seen with
// colour or an asymmetric picture, so the first look in game settles both (cr.video swaprb / flip).

#region

using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Threading;
using CalradiaRemembers.Logic.Video;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Video
{
   /// <summary>One decoded frame: its index and its pixels (width x height x 4 bytes).</summary>
   internal sealed class DecodedFrame
   {
      public int Index { get; set; }

      public byte[] Pixels { get; set; }
   }

   /// <summary>Decodes a .crvid's frames ahead, on its own thread, into reused buffers.</summary>
   internal sealed class CrvidDecoder : IDisposable
   {
      /// <summary>How many decoded frames may wait; the thread pauses beyond it.</summary>
      private const int Ahead = 3;

      /// <summary>
      ///   Give the engine R,G,B,A rather than GDI+'s B,G,R,A (default on: CreateFromMemory reads files with red and blue
      ///   swapped, so the raw call likely wants RGBA). Settled by eye in game: cr.video swaprb on|off.
      /// </summary>
      public static volatile bool SwapRedBlue = true;

      /// <summary>Hand the rows bottom-up (cr.video flip on|off), in case the engine reads them that way.</summary>
      public static volatile bool FlipVertical;

      private readonly ConcurrentQueue<DecodedFrame> _ready = new ConcurrentQueue<DecodedFrame>();
      private readonly ConcurrentBag<byte[]> _pool = new ConcurrentBag<byte[]>();
      private readonly ConcurrentQueue<string> _errors = new ConcurrentQueue<string>();
      private readonly string _path;
      private readonly Thread _thread;
      private volatile bool _stop;

      private CrvidDecoder(string path, CrvidManifest manifest, bool loop)
      {
         _path = path;
         Manifest = manifest;
         Loop = loop;
         _thread = new Thread(Run) {IsBackground = true, Name = "CR video decoder", Priority = ThreadPriority.BelowNormal};
         _thread.Start();
      }

      public CrvidManifest Manifest { get; }

      /// <summary>Whether the thread starts over at the end (it may change while playing).</summary>
      public volatile bool Loop;

      /// <summary>Opens <paramref name="path" />, checks its manifest, and starts decoding from the first frame. Null and why when it cannot.</summary>
      public static CrvidDecoder Open(string path, bool? loop, out string problem)
      {
         problem = null;
         try
         {
            CrvidManifest manifest;
            using (ZipArchive zip = ZipFile.OpenRead(path))
            {
               ZipArchiveEntry entry = zip.GetEntry("manifest.json");
               if (entry == null)
               {
                  problem = "no manifest.json in the archive";
                  return null;
               }

               using (var reader = new StreamReader(entry.Open()))
                  manifest = JsonConvert.DeserializeObject<ManifestFile>(reader.ReadToEnd())?.ToManifest();
            }

            var problems = CrvidRules.Problems(manifest);
            if (problems.Count > 0)
            {
               problem = string.Join("; ", problems);
               return null;
            }

            return new CrvidDecoder(path, manifest, loop ?? manifest.Loop);
         }
         catch (Exception ex)
         {
            problem = $"{ex.GetType().Name}: {ex.Message}";
            return null;
         }
      }

      /// <summary>The oldest decoded frame waiting, or false. The caller hands its buffer back with <see cref="Return" />.</summary>
      public bool TryPeek(out DecodedFrame frame) => _ready.TryPeek(out frame);

      /// <summary>Takes the oldest decoded frame off the queue.</summary>
      public bool TryTake(out DecodedFrame frame) => _ready.TryDequeue(out frame);

      /// <summary>A buffer the engine has copied (CreateFromByteArray pins it only for the call), free to decode into again.</summary>
      public void Return(byte[] pixels)
      {
         if (pixels != null) _pool.Add(pixels);
      }

      /// <summary>An error from the thread, for the main thread to log.</summary>
      public bool TryTakeError(out string error) => _errors.TryDequeue(out error);

      /// <summary>Stops the thread and waits for it (briefly), then drops what was decoded.</summary>
      public void Dispose()
      {
         _stop = true;
         if (!_thread.Join(500)) _errors.Enqueue("the decoder thread did not stop within 500 ms");
         while (_ready.TryDequeue(out _)) { }
      }

      private void Run()
      {
         try
         {
            using (ZipArchive zip = ZipFile.OpenRead(_path))
            {
               int w = Manifest.Width, h = Manifest.Height, frames = Manifest.Frames;
               var index = 0;
               while (!_stop)
               {
                  if (index >= frames)
                  {
                     if (!Loop) return;
                     index = 0;
                  }

                  if (_ready.Count >= Ahead)
                  {
                     Thread.Sleep(2);
                     continue;
                  }

                  byte[] pixels = _pool.TryTake(out byte[] reused) ? reused : new byte[w * h * 4];
                  if (Decode(zip, index, w, h, pixels)) _ready.Enqueue(new DecodedFrame {Index = index, Pixels = pixels});
                  else _pool.Add(pixels);
                  index++;
               }
            }
         }
         catch (Exception ex)
         {
            _errors.Enqueue($"{ex.GetType().Name}: {ex.Message}");
         }
      }

      private bool Decode(ZipArchive zip, int index, int w, int h, byte[] pixels)
      {
         ZipArchiveEntry entry = zip.GetEntry(CrvidRules.FrameEntry(index));
         if (entry == null)
         {
            _errors.Enqueue($"frame {CrvidRules.FrameEntry(index)} is missing");
            return false;
         }

         using (var memory = new MemoryStream())
         {
            using (Stream s = entry.Open()) s.CopyTo(memory);
            memory.Position = 0;
            using (var bitmap = new Bitmap(memory))
            {
               if (bitmap.Width != w || bitmap.Height != h)
               {
                  _errors.Enqueue($"frame {index + 1} is {bitmap.Width}x{bitmap.Height}, not {w}x{h} as the manifest says");
                  return false;
               }

               BitmapData data = bitmap.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
               try
               {
                  int row = w * 4;
                  bool flip = FlipVertical;
                  for (var y = 0; y < h; y++)
                     Marshal.Copy(IntPtr.Add(data.Scan0, (flip ? h - 1 - y : y) * data.Stride), pixels, y * row, row);
               }
               finally { bitmap.UnlockBits(data); }
            }
         }

         // GDI+ lays a pixel out B,G,R,A.
         if (SwapRedBlue)
            for (var i = 0; i + 3 < pixels.Length; i += 4)
            {
               byte b = pixels[i];
               pixels[i] = pixels[i + 2];
               pixels[i + 2] = b;
            }

         return true;
      }

      /// <summary>manifest.json as written by Tools/Video/make_crvid.py.</summary>
      private sealed class ManifestFile
      {
         [JsonProperty("version")] public int Version { get; set; }
         [JsonProperty("fps")] public float Fps { get; set; }
         [JsonProperty("width")] public int Width { get; set; }
         [JsonProperty("height")] public int Height { get; set; }
         [JsonProperty("frames")] public int Frames { get; set; }
         [JsonProperty("loop")] public bool Loop { get; set; }

         public CrvidManifest ToManifest()
            => new() {Version = Version, Fps = Fps, Width = Width, Height = Height, Frames = Frames, Loop = Loop};
      }
   }
}
