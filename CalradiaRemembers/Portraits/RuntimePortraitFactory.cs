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

        private static FieldInfo Backing(string name)
            => typeof(SpritePart).GetField($"<{name}>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);

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

                byte[] bytes = File.ReadAllBytes(fullPath);
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

                var tex2d = new Texture(new EngineTexture(engineTexture));
                var category = new SpriteCategory("cr_runtime_portrait", 1, false) {SheetSizes = new[] {new Vec2i(w, h)}};
                SheetsField?.SetValue(category, new List<Texture> {tex2d});

                // SheetID is 1-BASED (vanilla sprite data starts at 1; the engine indexes SpriteSheets[SheetID - 1]),
                // so 0 underflows the sheet array: the earlier black frame and the IndexOutOfRangeException.
                var part = new SpritePart("cr_runtime_portrait_part", category, w, h) {SheetID = 1, SheetX = 0, SheetY = 0};
                MinUField?.SetValue(part, 0f);
                MinVField?.SetValue(part, 0f);
                MaxUField?.SetValue(part, 1f);
                MaxVField?.SetValue(part, 1f);
                SheetWidthField?.SetValue(part, w);
                SheetHeightField?.SetValue(part, h);
                part.UpdateInitValues();

                var nine = new SpriteNinePatchParameters(0, 0, 0, 0);
                sprite = new SpriteGeneric("cr_runtime_portrait", part, nine);

                return true;
            }
            catch (Exception ex)
            {
                diag = $"{ex.GetType().Name}: {ex.Message}";

                return false;
            }
        }
    }
}
