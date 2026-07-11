// Code written by Gabriel Mailhot, 09/07/2026.
// PORTRAIT SPIKE: proof that a hand-made JPG/PNG can be loaded at runtime and shown in a GauntletUI widget,
// VRAM-safely. There is no clean public API to make a Sprite from a raw texture, so we assemble one: load the
// file bytes into an engine Texture, wrap it as a 2D texture, stand it up as a single-sheet SpriteCategory, and
// build a full-texture SpritePart + SpriteGeneric (the UV fields have no public setter, so they are set by
// reflection). The engine Texture is handed back so the caller can Release() it: the create_render_texture leak
// that sank v1.23 came from never releasing textures, so releasing is the whole point of the VRAM half of this spike.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TaleWorlds.Engine.GauntletUI;
using TaleWorlds.Library;
using TaleWorlds.TwoDimension;
using EngTex = TaleWorlds.Engine.Texture;

#endregion

namespace CalradiaRemembers.Portraits
{
    /// <summary>Builds a GauntletUI <see cref="Sprite" /> from an image file on disk. Spike-grade (reflection).</summary>
    internal static class RuntimePortraitFactory
    {
        private static readonly FieldInfo MinUField = Backing("MinU");
        private static readonly FieldInfo MinVField = Backing("MinV");
        private static readonly FieldInfo MaxUField = Backing("MaxU");
        private static readonly FieldInfo MaxVField = Backing("MaxV");
        private static readonly FieldInfo SheetWidthField = Backing("SheetWidth");
        private static readonly FieldInfo SheetHeightField = Backing("SheetHeight");

        private static readonly FieldInfo SheetsField =
            typeof(SpriteCategory).GetField("<SpriteSheets>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

        // SpritePart.Texture returns null unless its category reads as LOADED (decompiled:
        // "_category != null && _category.IsLoaded && SpriteSheets?.Count >= SheetID"), and IsLoaded's
        // setter is internal: the black-frame culprit once the sheets were in place.
        private static readonly MethodInfo IsLoadedSetter =
            typeof(SpriteCategory).GetProperty("IsLoaded")?.GetSetMethod(true);

        private static readonly Dictionary<string, (Sprite sprite, int width, int height)> Cache =
            new Dictionary<string, (Sprite, int, int)>();

        private static FieldInfo Backing(string name)
            => typeof(SpritePart).GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

        /// <summary>Deterministic FNV-1a hash of the path (case-folded), for the per-file sprite name.</summary>
        private static uint StableHash(string path)
        {
            uint h = 2166136261;
            foreach (char c in (path ?? "").ToLowerInvariant())
            {
                h ^= c;
                h *= 16777619;
            }

            return h;
        }

        /// <summary>
        ///   Cached load by path: builds a sprite once per file and reuses it, so N chat lines sharing one
        ///   portrait hold ONE texture. A miss is cached as null so a missing file is not retried every line.
        ///   The kept texture is intentionally not released (it stays available); a full feature would evict.
        /// </summary>
        public static Sprite GetOrCreateCached(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;
            if (Cache.TryGetValue(fullPath, out var cached)) return cached.sprite;

            Sprite sprite = TryCreate(fullPath, out Sprite s, out EngTex tex, out _) ? s : null;
            Cache[fullPath] = (sprite, tex?.Width ?? 0, tex?.Height ?? 0);

            return sprite;
        }

        /// <summary>The pixel dimensions of the (cached) image at <paramref name="fullPath" />, false if it will not load.</summary>
        public static bool TryGetDimensions(string fullPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (GetOrCreateCached(fullPath) == null) return false;

            (Sprite _, int w, int h) = Cache[fullPath];
            width = w;
            height = h;

            return width > 0 && height > 0;
        }

        /// <summary>
        ///   Loads <paramref name="fullPath" /> and builds a full-texture sprite. Returns the engine texture so
        ///   the caller can release it. <paramref name="diag" /> narrates each step for the in-game log.
        /// </summary>
        public static bool TryCreate(string fullPath, out Sprite sprite, out EngTex engineTexture, out string diag)
        {
            sprite = null;
            engineTexture = null;

            try
            {
                if (!File.Exists(fullPath))
                {
                    diag = $"file not found: {fullPath}";

                    return false;
                }

                byte[] bytes = SwapRedBlue(File.ReadAllBytes(fullPath));
                engineTexture = EngTex.CreateFromMemory(bytes);

                if (engineTexture == null)
                {
                    diag = "CreateFromMemory returned null";

                    return false;
                }

                // Keep the from-memory texture alive and force its GPU upload, so it is renderable this frame
                // rather than getting collected or rendering before the deferred upload lands (a likely cause of
                // the black frame).
                engineTexture.SetTextureAsAlwaysValid();
                engineTexture.PreloadTexture(true);

                int w = engineTexture.Width;
                int h = engineTexture.Height;
                diag = $"loaded {w}x{h} (IsLoaded={engineTexture.IsLoaded()}) from {Path.GetFileName(fullPath)} ({bytes.Length} bytes)";
                if (w <= 0 || h <= 0) diag += " (WARN zero dims: texture may still be loading)";

                // The name must be UNIQUE PER FILE: the render layer caches by sprite/category NAME, so two
                // different images sharing "cr_runtime_portrait" collide and every row shows the first-loaded
                // face (player report: the player's line wore the NPC's portrait). Hash the path into the name.
                string spriteName = $"cr_runtime_portrait_{StableHash(fullPath):x8}";

                var tex2d = new Texture(new EngineTexture(engineTexture));
                var category = new SpriteCategory(spriteName, 1, false) {SheetSizes = new[] {new Vec2i(w, h)}};
                SheetsField?.SetValue(category, new List<Texture> {tex2d});
                IsLoadedSetter?.Invoke(category, new object[] {true});
                diag += $", sheets={category.SpriteSheets?.Count ?? -1}, catLoaded={category.IsLoaded}, name={spriteName}";

                // SheetID is 1-BASED (vanilla sprite data starts at 1; the engine indexes SpriteSheets[SheetID - 1]),
                // so 0 underflows the sheet array: the earlier black frame and the IndexOutOfRangeException.
                var part = new SpritePart($"{spriteName}_part", category, w, h) {SheetID = 1, SheetX = 0, SheetY = 0};
                MinUField?.SetValue(part, 0f);
                MinVField?.SetValue(part, 0f);
                MaxUField?.SetValue(part, 1f);
                MaxVField?.SetValue(part, 1f);
                SheetWidthField?.SetValue(part, w);
                SheetHeightField?.SetValue(part, h);
                part.UpdateInitValues();

                var nine = new SpriteNinePatchParameters(0, 0, 0, 0);
                sprite = new SpriteGeneric(spriteName, part, nine);

                return true;
            }
            catch (Exception ex)
            {
                diag = $"{ex.GetType().Name}: {ex.Message}";

                return false;
            }
        }

        /// <summary>
        ///   The engine's <c>CreateFromMemory</c> decodes the file with RED and BLUE swapped relative to what
        ///   the 2D sprite renderer samples (player report: warm skin rendered BLUE). Decode the image
        ///   ourselves (GDI+), swap R and B on every pixel, and re-encode, so the engine's backwards read
        ///   lands the colours the right way round. Any decode surprise falls back to the original bytes
        ///   (worst case: the old swapped colours, never a missing portrait).
        /// </summary>
        private static byte[] SwapRedBlue(byte[] imageBytes)
        {
            try
            {
                using (var input = new MemoryStream(imageBytes))
                using (var bitmap = new System.Drawing.Bitmap(input))
                {
                    var rect = new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height);
                    System.Drawing.Imaging.BitmapData data = bitmap.LockBits(
                        rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                        System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                    try
                    {
                        int count = Math.Abs(data.Stride) * data.Height;
                        var pixels = new byte[count];
                        System.Runtime.InteropServices.Marshal.Copy(data.Scan0, pixels, 0, count);

                        for (var i = 0; i + 3 < count; i += 4)
                        {
                            byte blue = pixels[i]; // 32bppArgb lays out B,G,R,A per pixel
                            pixels[i] = pixels[i + 2];
                            pixels[i + 2] = blue;
                        }

                        System.Runtime.InteropServices.Marshal.Copy(pixels, 0, data.Scan0, count);
                    }
                    finally { bitmap.UnlockBits(data); }

                    using (var output = new MemoryStream())
                    {
                        bitmap.Save(output, System.Drawing.Imaging.ImageFormat.Png);

                        return output.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[PORTRAIT] channel swap failed ({ex.GetType().Name}: {ex.Message}); using the raw file bytes.");

                return imageBytes;
            }
        }
    }
}
