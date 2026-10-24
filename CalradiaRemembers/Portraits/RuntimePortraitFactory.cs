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
using CalradiaRemembers.Foundation;
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

        /// <summary>How many textures CR has loaded from files this session (the balance survey reads it beside graphics memory).</summary>
        public static int CachedCount => Cache.Count;

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
        ///   The fixed-border widths of a nine-patch (nine-slice) sprite: the corners stay their native size
        ///   while the edges and centre stretch, so a frame drawn far larger than its source keeps crisp
        ///   corners. All-zero (the default) is an ordinary full-stretch sprite (the portrait case).
        /// </summary>
        public readonly struct NinePatch
        {
            public NinePatch(int left, int right, int top, int bottom)
            {
                Left = left;
                Right = right;
                Top = top;
                Bottom = bottom;
            }

            public int Left { get; }
            public int Right { get; }
            public int Top { get; }
            public int Bottom { get; }

            public bool IsFlat => Left == 0 && Right == 0 && Top == 0 && Bottom == 0;

            public override string ToString() => $"{Left},{Right},{Top},{Bottom}";
        }

        private static string KeyFor(string fullPath, bool correctRedBlue, NinePatch ninePatch)
            => $"{fullPath}|{correctRedBlue}|{ninePatch}";

        /// <summary>
        ///   Cached load by path: builds a sprite once per (file, options) and reuses it, so N chat lines
        ///   sharing one portrait hold ONE texture. A miss is cached as null so a missing file is not retried
        ///   every line. <paramref name="correctRedBlue" /> (the name is historical) re-encodes the image as an RGBA
        ///   PNG, the one encoding the engine shows true whatever the file was; false hands the file over as it is (CR's
        ///   own RGBA icons). <paramref name="ninePatch" /> makes the sprite a stretchable frame.
        /// </summary>
        public static Sprite GetOrCreateCached(string fullPath, bool correctRedBlue = true, NinePatch ninePatch = default)
        {
            if (string.IsNullOrEmpty(fullPath)) return null;

            string key = KeyFor(fullPath, correctRedBlue, ninePatch);
            if (Cache.TryGetValue(key, out var cached)) return cached.sprite;

            Sprite sprite = TryCreate(fullPath, correctRedBlue, ninePatch, out Sprite s, out EngTex tex, out string diag) ? s : null;
            Cache[key] = (sprite, tex?.Width ?? 0, tex?.Height ?? 0);
            // Said once per image (a miss is cached): a window drawn from missing art is see-through, and the reason
            // was thrown away (EmperorHaterius, 04/10/2026, whose log could not have told us why).
            if (sprite == null) ModLog.Error($"[IMAGE] could not load {fullPath}: {diag}");

            return sprite;
        }

        /// <summary>The pixel dimensions of the (cached) image at <paramref name="fullPath" />, false if it will not load.</summary>
        public static bool TryGetDimensions(string fullPath, out int width, out int height)
        {
            width = 0;
            height = 0;
            if (GetOrCreateCached(fullPath) == null) return false;

            (Sprite _, int w, int h) = Cache[KeyFor(fullPath, true, default)];
            width = w;
            height = h;

            return width > 0 && height > 0;
        }

        /// <summary>
        ///   Loads <paramref name="fullPath" /> and builds a full-texture sprite. Returns the engine texture so
        ///   the caller can release it. <paramref name="diag" /> narrates each step for the in-game log.
        /// </summary>
        public static bool TryCreate(string fullPath, bool correctRedBlue, NinePatch ninePatch, out Sprite sprite, out EngTex engineTexture, out string diag)
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

                string uniqueName = $"cr_rt_tex_{StableHash(fullPath):x8}";

                // The engine shows a 24-bit image (a JPG, an RGB PNG) with red and blue swapped and an RGBA PNG true.
                // Swapping every image fixed the first and turned the second blue (Lordfadooboo, 23/10/2026: his own
                // PNG portraits with alpha). So every image is handed over in the one encoding that renders true (the
                // encoding of every CR icon and of the political map), never guessed from the file's own format.
                // cr.testall portrait_channels reads the engine's pixels back to check it.
                byte[] corrected = correctRedBlue ? NormalizeToRgba(File.ReadAllBytes(fullPath)) : File.ReadAllBytes(fullPath);
                engineTexture = EngTex.CreateFromMemory(corrected);

                if (engineTexture == null)
                {
                    diag = "CreateFromMemory returned null";

                    return false;
                }

                engineTexture.SetTextureAsAlwaysValid();
                engineTexture.PreloadTexture(true);

                // A UNIQUE NATIVE TEXTURE NAME is what keeps two different images apart: the render layer's
                // by-name cache otherwise unifies same-named textures, so every custom row wore the first-loaded
                // face (the player-wears-the-NPC's-portrait report). This is exactly why the engine's own
                // path-loader avoided the clash (it names the texture by its file path); we do the same in memory.
                try { engineTexture.Name = uniqueName; }
                catch
                {
                    // naming is defensive; rendering works without it
                }

                int w = engineTexture.Width;
                int h = engineTexture.Height;
                diag = $"loaded {w}x{h} (IsLoaded={engineTexture.IsLoaded()}, name={uniqueName}) from {Path.GetFileName(fullPath)}";
                if (w <= 0 || h <= 0) diag += " (WARN zero dims: texture may still be loading)";
                ModLog.Info($"[PORTRAIT] {diag}");

                // The name must be UNIQUE PER FILE: the render layer caches by sprite/category NAME, so two
                // different images sharing "cr_runtime_portrait" collide and every row shows the first-loaded
                // face (player report: the player's line wore the NPC's portrait). Hash the path into the name.
                string spriteName = $"cr_runtime_portrait_{StableHash(fullPath):x8}";

                sprite = SpriteFromTexture(engineTexture, spriteName, w, h, ninePatch);
                diag += $", name={spriteName}";

                return true;
            }
            catch (Exception ex)
            {
                diag = $"{ex.GetType().Name}: {ex.Message}";

                return false;
            }
        }

        /// <summary>
        ///   A full-texture sprite over an engine texture already made: a file loaded above, or a frame of a tale's video
        ///   (Chat/CrVideoPlayer, Gabriel 11/10/2026). <paramref name="spriteName" /> must be unique: the render layer caches
        ///   by name.
        /// </summary>
        public static Sprite SpriteFromTexture(EngTex engineTexture, string spriteName, int w, int h, NinePatch ninePatch = default)
        {
            var tex2d = new Texture(new EngineTexture(engineTexture));
            var category = new SpriteCategory(spriteName, 1, false) {SheetSizes = new[] {new Vec2i(w, h)}};
            SheetsField?.SetValue(category, new List<Texture> {tex2d});
            IsLoadedSetter?.Invoke(category, new object[] {true});

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

            // Nine-patch: a flat sprite keeps the full-stretch behaviour (0,0,0,0); a frame passes its fixed
            // corner widths so the borders stay crisp while the middle stretches to the widget's size.
            var nine = new SpriteNinePatchParameters(ninePatch.Left, ninePatch.Right, ninePatch.Top, ninePatch.Bottom);

            return new SpriteGeneric(spriteName, part, nine);
        }

        /// <summary>
        ///   Re-encodes any image GDI+ reads (JPG, RGB or RGBA PNG, palette or grey PNG) as a 32-bit RGBA PNG, colours
        ///   untouched. The clone to 32bppArgb is what makes GDI+ write PNG colour type 6 even for an opaque image (a
        ///   plain re-save keeps the source's own type). Anything unexpected falls back to the file's own bytes and says
        ///   so: a portrait with the engine's own colours, never a missing one. Also the cr.testall seam.
        /// </summary>
        internal static byte[] NormalizeToRgba(byte[] imageBytes)
        {
            try
            {
                using (var input = new MemoryStream(imageBytes))
                using (var source = new System.Drawing.Bitmap(input))
                using (System.Drawing.Bitmap rgba = source.Clone(new System.Drawing.Rectangle(0, 0, source.Width, source.Height),
                           System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                using (var output = new MemoryStream())
                {
                    rgba.Save(output, System.Drawing.Imaging.ImageFormat.Png);
                    byte[] png = output.ToArray();
                    int type = CalradiaRemembers.Logic.Diagnostics.PngHeader.ColorType(png);
                    if (type == CalradiaRemembers.Logic.Diagnostics.PngHeader.Rgba) return png;

                    ModLog.Error($"[PORTRAIT] RGBA re-encode came out as PNG colour type {type}; using the raw file bytes.");

                    return imageBytes;
                }
            }
            catch (Exception ex)
            {
                ModLog.Error($"[PORTRAIT] RGBA re-encode failed ({ex.GetType().Name}: {ex.Message}); using the raw file bytes.");

                return imageBytes;
            }
        }
    }
}
