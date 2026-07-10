// Code written by Gabriel Mailhot, 10/07/2026.
// FPS fix for long text in an EditableTextWidget (the chat composer and the letter editor, MaxLength 8000).
// The engine's EditableText.UpdateSelectedText recomputes the visible window EVERY FRAME by trimming the full
// string one character at a time, each trim allocating a new string and re-summing glyph widths: O(n^2) work and
// tens of MB of garbage per frame at 8000 characters, which tanks the frame rate the moment the box holds a long
// message. This prefix replaces that recomputation, for LONG left-aligned text only, with the same window
// arithmetic done in O(n) (one width sum, pointer trims, a single substring), plus a per-instance cache so an
// unchanged frame skips the work entirely. Short text (every vanilla box) keeps the original engine path.

extern alias GameNumerics;

#region

using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using TaleWorlds.TwoDimension;
using Vector2 = GameNumerics::System.Numerics.Vector2;

#endregion

namespace CalradiaRemembers.GUI
{
    /// <summary>
    ///   Replaces the engine's per-frame O(n^2) visible-window recomputation for long editable text with an
    ///   equivalent O(n) computation (and a skip when nothing changed). See the file header for the full story.
    /// </summary>
    [HarmonyPatch(typeof(EditableText), "UpdateSelectedText")]
    public static class LongEditableTextPatch
    {
        /// <summary>Below this length the original engine code runs untouched (every vanilla box qualifies).</summary>
        private const int LongTextThreshold = 600;

        private static readonly bool Available;
        private static readonly GetCharacterWidthDelegate CharWidth;
        private static readonly FindCharacterPositionDelegate FindCharPos;
        private static readonly Func<RichText, int> GetWidth;

        private static readonly ConditionalWeakTable<EditableText, CacheEntry> Cache = new ConditionalWeakTable<EditableText, CacheEntry>();

        static LongEditableTextPatch()
        {
            try
            {
                CharWidth = AccessTools.MethodDelegate<GetCharacterWidthDelegate>(AccessTools.Method(typeof(EditableText), "GetCharacterWidth"));
                FindCharPos = AccessTools.MethodDelegate<FindCharacterPositionDelegate>(AccessTools.Method(typeof(EditableText), "FindCharacterPosition"));
                GetWidth = AccessTools.MethodDelegate<Func<RichText, int>>(AccessTools.PropertyGetter(typeof(RichText), "Width"));
                Available = CharWidth != null && FindCharPos != null && GetWidth != null;
            }
            catch
            {
                Available = false; // engine internals moved: fall back to the original (laggy but correct) path
            }
        }

        /// <summary>
        ///   Runs instead of the engine method when the text is long; returning true defers to the original.
        /// </summary>
        public static bool Prefix(EditableText __instance, float dt, Vector2 mousePosition,
                                  ref int ____visibleStart, ref string ____realVisibleText)
        {
            if (!Available) return true;

            string full = __instance.VisibleText ?? "";
            if (full.Length <= LongTextThreshold) return true;
            if (__instance.HorizontalAlignment != TextHorizontalAlignment.Left) return true; // our boxes are Left; anything else keeps the engine path

            try
            {
                int cursor = __instance.CursorPosition;
                int boxWidth = GetWidth(__instance);
                bool selectionGestureActive = __instance.HighlightStart || !__instance.HighlightEnd;

                CacheEntry cache = Cache.GetOrCreateValue(__instance);
                if (!selectionGestureActive
                    && ReferenceEquals(cache.Text, full)
                    && cache.Cursor == cursor
                    && cache.Width == boxWidth
                    && cache.SelBegin == __instance.SelectedTextBegin
                    && cache.SelEnd == __instance.SelectedTextEnd)
                    return false; // nothing changed since last frame: the window on screen is already right

                // ── Same window arithmetic as the engine, without the per-character Substring storm ──
                // Engine: trim front up to the previous window start while too wide; trim the end down to the
                // cursor while too wide; then trim the front further until it fits. 10px pad each side.
                float budget = boxWidth - 20f;
                int clampedStart = Math.Min(____visibleStart, cursor);

                var total = 0f;
                for (var k = 0; k < full.Length; k++) total += CharWidth(__instance, full[k]);

                float w = total;
                var front = 0;
                while (front < clampedStart && w > budget) w -= CharWidth(__instance, full[front++]);

                int end = full.Length;
                while (end > cursor && end > front && w > budget) w -= CharWidth(__instance, full[--end]);

                while (front < end && w > budget) w -= CharWidth(__instance, full[front++]);

                ____visibleStart = Math.Min(front, cursor);
                string window = full.Substring(front, end - front);

                // ── Mouse selection, exactly as the engine does it (Left alignment: no offset) ──
                if (__instance.HighlightStart)
                {
                    int pos = FindCharPos(__instance, dt, full, window, FontScale(__instance), mousePosition, front);
                    __instance.HighlightStart = false;
                    __instance.SetCursor(pos);
                    __instance.BeginSelection();
                }

                if (!__instance.HighlightEnd)
                {
                    int pos = FindCharPos(__instance, dt, full, window, FontScale(__instance), mousePosition, front);
                    __instance.SetCursor(pos, true, true);
                }

                int selBegin = Math.Min(Math.Max(__instance.SelectedTextBegin - front, 0), window.Length);
                int selEnd = Math.Min(Math.Max(__instance.SelectedTextEnd - front, 0), window.Length);
                if (selBegin > selEnd) (selBegin, selEnd) = (selEnd, selBegin);

                // The engine always inserts the span markers, selection or not; keep its output byte-identical.
                string marked = window.Substring(0, selBegin)
                                + "<span style=\"Highlight\">" + window.Substring(selBegin, selEnd - selBegin) + "</span>"
                                + window.Substring(selEnd, window.Length - selEnd);

                ____realVisibleText = window;
                __instance.Value = marked;

                cache.Text = full;
                cache.Cursor = cursor; // as read at entry: a SetCursor above changes it and forces a recompute next frame
                cache.Width = boxWidth;
                cache.SelBegin = __instance.SelectedTextBegin;
                cache.SelEnd = __instance.SelectedTextEnd;

                return false;
            }
            catch
            {
                return true; // any surprise: let the engine do it the slow way rather than misrender
            }
        }

        /// <summary>FontSize/Font.Size ratio the engine feeds the mouse edge-scroll zones.</summary>
        private static float FontScale(EditableText instance)
        {
            StyleFontContainer.FontData fontData = instance.StyleFontContainer.GetFontData("Default");

            return fontData.FontSize / fontData.Font.Size;
        }

        private delegate float GetCharacterWidthDelegate(EditableText self, char character);

        private delegate int FindCharacterPositionDelegate(EditableText self, float dt, string fullText, string visibleText, float scale, Vector2 mousePosition, int omitCount);

        private sealed class CacheEntry
        {
            public int Cursor;
            public int SelBegin;
            public int SelEnd;
            public string Text;
            public int Width;
        }
    }
}
