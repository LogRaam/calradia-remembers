// Code written by Gabriel Mailhot, 07/07/2026.
// Player appearance pillar, engine extraction: reads the reliably-available body facts off a live Hero into
// the engine-agnostic AppearanceFacts that the pure AppearanceComposer bands into words. Height, hair colour
// and a beard are baked into the packed static face code and not reliably readable, so they are omitted (the
// composer simply stays silent on them) per the ratified scope; gender, age, build and weight are read.
// CatWithFourEyes (Discord, 26/11/2026): a non-human race read as "a man". The race's look from races.json is applied
// here, the one door every appearance passes through (the prompt, the encyclopedia, cr.appearance, both voices).

#region

using System;
using CalradiaRemembers.Logic;
using TaleWorlds.CampaignSystem;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Appearance
{
    /// <summary>Maps a live <see cref="Hero" /> to the pure <see cref="AppearanceFacts" />. Never throws.</summary>
    internal static class AppearanceFactsExtractor
    {
        public static AppearanceFacts Extract(Hero hero)
        {
            if (hero == null) return null;

            double build = 0.5, weight = 0.5;
            try
            {
                BodyProperties bp = hero.BodyProperties;
                build = Clamp01(bp.Build);
                weight = Clamp01(bp.Weight);
            }
            catch { /* body properties unreadable: leave build/weight average (unvoiced) */ }

            // Hair + skin colour resolve to a real RGB from the face key's colour gradients; eye colour is banded
            // from its raw offset (no eye palette exists). Main-thread native read, cached. Height and a beard
            // live in the static face key with no reliable reader, so they stay omitted.
            FaceColors colors = FaceColorReader.Read(hero);

            var facts = new AppearanceFacts {IsFemale = hero.IsFemale, AgeYears = SafeAge(hero), HeightNormalized = 0.5, BuildNormalized = build, WeightNormalized = weight, HairColorDescriptor = colors.Hair, HasBeard = false, SkinToneDescriptor = colors.Skin, EyeColorDescriptor = colors.Eye};

            return RaceLookPolicy.Apply(facts, RaceLookLoader.LookOf(hero));
        }

        private static int SafeAge(Hero hero)
        {
            try { return (int) Math.Round(hero.Age); }
            catch { return 0; }
        }

        private static double Clamp01(double v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
