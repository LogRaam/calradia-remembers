// Code written by Gabriel Mailhot, 07/07/2026.
// Player appearance pillar, colour extraction: reads a hero's HAIR, SKIN and EYE colour off the packed face key
// via the engine's FaceGen (MBBodyProperties). Hair and skin resolve to a REAL RGB: their 0..1 offset is sampled
// against that race/culture's colour gradient, then classified by luminance/warmth. EYE colour has an offset but
// the engine exposes NO eye-colour gradient (verified by reflection), so it can only be BANDED heuristically from
// the raw offset (calibrate with cr.appearance, which prints the raw offsets). These are native engine calls, so
// callers must be on the MAIN THREAD (prompt build / chat open, both are); results are cached per hero (a body
// does not change) behind a full guard, so any failure degrades to no colour at all rather than throwing.

#region

using System;
using System.Collections.Generic;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;
using TaleWorlds.MountAndBlade;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>Resolved face colours plus their raw 0..1 offsets (the offsets are for calibration only).</summary>
    internal readonly struct FaceColors
    {
        public FaceColors(string hair, string skin, string eye, float hairOffset, float skinOffset, float eyeOffset)
        {
            Hair = hair;
            Skin = skin;
            Eye = eye;
            HairOffset = hairOffset;
            SkinOffset = skinOffset;
            EyeOffset = eyeOffset;
        }

        public string Hair { get; }
        public string Skin { get; }
        public string Eye { get; }
        public float HairOffset { get; }
        public float SkinOffset { get; }
        public float EyeOffset { get; }

        public static FaceColors None => new(null, null, null, -1f, -1f, -1f);
    }

    /// <summary>Resolves a hero's hair, skin and eye colour to plain words. Main-thread only; cached; never throws.</summary>
    internal static class FaceColorReader
    {
        private static readonly Dictionary<string, FaceColors> Cache = new();

        /// <summary>Face colours for a hero, or <see cref="FaceColors.None" /> when not derivable. Cached per hero StringId.</summary>
        public static FaceColors Read(Hero hero)
        {
            if (hero?.StringId == null) return FaceColors.None;
            if (Cache.TryGetValue(hero.StringId, out FaceColors cached)) return cached;

            FaceColors result = FaceColors.None;
            try
            {
                FaceGenerationParams p = default;
                BodyProperties bp = hero.BodyProperties;
                MBBodyProperties.GetParamsFromKey(ref p, bp, false, false);

                int race = hero.CharacterObject?.Race ?? 0;
                int gender = hero.IsFemale ? 1 : 0;
                int age = (int) hero.Age;

                string hair = ClassifyHair(SampleGradient(MBBodyProperties.GetHairColorGradientPoints(race, gender, age), p.CurrentHairColorOffset));
                string skin = ClassifySkin(SampleGradient(MBBodyProperties.GetSkinColorGradientPoints(race, gender, age), p.CurrentSkinColorOffset));
                string eye = ClassifyEye(p.CurrentEyeColorOffset);

                result = new FaceColors(hair, skin, eye, p.CurrentHairColorOffset, p.CurrentSkinColorOffset, p.CurrentEyeColorOffset);
            }
            catch (Exception ex)
            {
                ModLog.Info($"[APPEARANCE] colour read failed for {hero.Name}, omitting colours: {ex.GetType().Name}.");
            }

            Cache[hero.StringId] = result;

            return result;
        }

        /// <summary>Clears the per-hero colour cache (so a re-read picks up a tuning change or an appearance edit).</summary>
        public static void ClearCache() => Cache.Clear();

        /// <summary>Samples the ordered gradient (packed 0xAARRGGBB colours) at a 0..1 offset, lerping between neighbours.</summary>
        private static uint? SampleGradient(List<uint> points, float offset)
        {
            if (points == null || points.Count == 0) return null;
            if (points.Count == 1) return points[0];

            float o = offset < 0 ? 0 : offset > 1 ? 1 : offset;
            float pos = o * (points.Count - 1);
            int i = (int) pos;
            if (i >= points.Count - 1) return points[points.Count - 1];

            return LerpColor(points[i], points[i + 1], pos - i);
        }

        private static uint LerpColor(uint a, uint b, float t)
        {
            int ar = (int) ((a >> 16) & 0xFF), ag = (int) ((a >> 8) & 0xFF), ab = (int) (a & 0xFF);
            int br = (int) ((b >> 16) & 0xFF), bg = (int) ((b >> 8) & 0xFF), bb = (int) (b & 0xFF);
            int r = (int) (ar + (br - ar) * t), g = (int) (ag + (bg - ag) * t), bl = (int) (ab + (bb - ab) * t);

            return (uint) ((r << 16) | (g << 8) | bl);
        }

        private static void Decode(uint c, out int r, out int g, out int b, out double luma, out double sat)
        {
            r = (int) ((c >> 16) & 0xFF);
            g = (int) ((c >> 8) & 0xFF);
            b = (int) (c & 0xFF);
            luma = 0.299 * r + 0.587 * g + 0.114 * b;
            int max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            sat = max == 0 ? 0 : (max - min) / (double) max;
        }

        /// <summary>A plain hair-colour word (the composer appends " hair"), or null. Classified by luminance and warmth.</summary>
        private static string ClassifyHair(uint? color)
        {
            if (color == null) return null;

            Decode(color.Value, out int r, out int g, out int b, out double luma, out double sat);
            bool reddish = r > g && g >= b && sat > 0.35;

            if (luma < 50) return "dark";
            if (reddish && luma < 155) return "red";
            if (luma < 110) return "dark brown";
            if (sat < 0.15 && luma > 140) return "grey";
            if (luma > 155) return "fair";

            return "brown";
        }

        /// <summary>A plain skin-tone adjective (e.g. "fair-skinned"), or null. Classified by luminance.</summary>
        private static string ClassifySkin(uint? color)
        {
            if (color == null) return null;

            Decode(color.Value, out int _, out int _, out int _, out double luma, out double _);

            if (luma < 95) return "dark-skinned";
            if (luma < 135) return "brown-skinned";
            if (luma < 178) return "tanned";
            if (luma < 208) return "fair-skinned";

            return "pale";
        }

        /// <summary>
        ///   A plain eye-colour word (the composer appends " eyes"), banded from the raw 0..1 offset. There is
        ///   no eye-colour palette in the engine to resolve a true RGB, so the bands are calibrated against
        ///   photographs of the face editor's slider. The table itself lives in
        ///   <see cref="EyeColorBands" /> so it can be tested: it sat here, private and in net472, while it
        ///   drifted far enough to call Rhagaea's amber eyes green.
        /// </summary>
        private static string ClassifyEye(float offset) => EyeColorBands.Describe(offset);
    }
}
