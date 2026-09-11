// Code written by Gabriel Mailhot, 05/07/2026.
// Player appearance pillar, composer increment (ratified): plain carrier for the body facts fed to
// PlayerAppearanceComposer. Deliberately engine-agnostic — reading TaleWorlds.Core.BodyProperties is a
// SEPARATE future step (the mod-side extraction), out of scope here. This type only carries the plain
// primitives the composer needs to band into words.

namespace CalradiaRemembers.Logic
{
    /// <summary>
    ///   Plain, engine-agnostic carrier of the player's derivable body facts, as fed to
    ///   <see cref="PlayerAppearanceComposer"/>. Every numeric slider is 0..1 and will eventually be read
    ///   from the engine's <c>BodyProperties</c> key/DNA sliders by a future mod-side extraction step;
    ///   this type does not know or care where the numbers came from.
    /// </summary>
    public sealed class AppearanceFacts
    {
        /// <summary>True for a female player; drives the noun ("woman"/"man") and possessive pronoun ("her"/"his").</summary>
        public bool IsFemale { get; init; }

        /// <summary>The player's age in years. Zero or negative means "not known/derivable" — the composer omits the age clause.</summary>
        public int AgeYears { get; init; }

        /// <summary>
        ///   0 (shortest) .. 1 (tallest), from the engine's body Height slider. Values in the middle band
        ///   read as "average" and are not voiced.
        /// </summary>
        public double HeightNormalized { get; init; }

        /// <summary>
        ///   0 (slightest) .. 1 (most muscular), from the engine's body Build slider. Averaged together
        ///   with <see cref="WeightNormalized"/> by the composer into one build/weight phrase.
        /// </summary>
        public double BuildNormalized { get; init; }

        /// <summary>
        ///   0 (lightest) .. 1 (heaviest), from the engine's body Weight slider. Averaged together with
        ///   <see cref="BuildNormalized"/> by the composer into one build/weight phrase.
        /// </summary>
        public double WeightNormalized { get; init; }

        /// <summary>
        ///   A short hair-color word (e.g. "dark", "flaxen", "grey"), or null/empty when not derivable
        ///   (e.g. a hairstyle with no clear color reading). The composer omits the hair clause entirely
        ///   rather than ever say "unknown".
        /// </summary>
        public string HairColorDescriptor { get; init; }

        /// <summary>True when the player wears a beard. Only ever voiced for a male player (<see cref="IsFemale"/> false).</summary>
        public bool HasBeard { get; init; }

        /// <summary>
        ///   A short skin-tone word (e.g. "fair-skinned", "tan", "dark-skinned"), or null/empty when not
        ///   derivable. Voiced as an adjective alongside height and build; omitted entirely rather than "unknown".
        /// </summary>
        public string SkinToneDescriptor { get; init; } = null;

        /// <summary>
        ///   A short eye-colour word (e.g. "blue", "brown", "hazel"), or null/empty when not derivable. Voiced as
        ///   a "with ... eyes" clause alongside hair; omitted entirely rather than "unknown".
        /// </summary>
        public string EyeColorDescriptor { get; init; } = null;
    }
}
