// Code written by Gabriel Mailhot, 22/06/2026.
// Player-authored character data: a hand-crafted personality (the five personality traits) plus an
// optional backstory for one NPC. Pure so the load-bearing rules — which traits are set, clamped to the
// engine's −2..+2 band — stay engine-agnostic and tested; the mod maps these onto Hero.SetTraitLevel.

#region

using System.Collections.Generic;

#endregion

namespace CalradiaRemembers.Logic
{
    /// <summary>The five hand-authorable personality traits (mirrors the game's DefaultTraits set).</summary>
    public enum AuthoredTrait
    {
        Valor,
        Honor,
        Mercy,
        Generosity,
        Calculating
    }

    /// <summary>
    ///   One author's override for a single NPC: optional trait values (null = leave the vanilla value
    ///   untouched) and an optional backstory. <see cref="Hero"/> is the display-name key;
    ///   <see cref="Id"/> (a StringId) disambiguates homonyms when present.
    /// </summary>
    public sealed class CharacterOverride
    {
        /// <summary>The engine's personality-trait band — SetTraitLevel clamps to this too.</summary>
        public const int MinLevel = -2;
        public const int MaxLevel = 2;

        public string Hero { get; set; }
        public string Id { get; set; }

        public int? Valor { get; set; }
        public int? Honor { get; set; }
        public int? Mercy { get; set; }
        public int? Generosity { get; set; }
        public int? Calculating { get; set; }

        public string Backstory { get; set; }

        /// <summary>True when this override sets at least one trait.</summary>
        public bool HasTraits =>
            Valor.HasValue || Honor.HasValue || Mercy.HasValue || Generosity.HasValue || Calculating.HasValue;

        /// <summary>True when the backstory carries authored flavor.</summary>
        public bool HasBackstory => !string.IsNullOrWhiteSpace(Backstory);

        /// <summary>
        ///   The authored trait levels that are actually set, each clamped to the engine band, in a
        ///   stable order. Unset (null) traits are omitted so they keep their vanilla value.
        /// </summary>
        public IEnumerable<(AuthoredTrait Trait, int Level)> ClampedTraitLevels()
        {
            if (Valor.HasValue)       yield return (AuthoredTrait.Valor,       Clamp(Valor.Value));
            if (Honor.HasValue)       yield return (AuthoredTrait.Honor,       Clamp(Honor.Value));
            if (Mercy.HasValue)       yield return (AuthoredTrait.Mercy,       Clamp(Mercy.Value));
            if (Generosity.HasValue)  yield return (AuthoredTrait.Generosity,  Clamp(Generosity.Value));
            if (Calculating.HasValue) yield return (AuthoredTrait.Calculating, Clamp(Calculating.Value));
        }

        private static int Clamp(int v) => v < MinLevel ? MinLevel : v > MaxLevel ? MaxLevel : v;
    }
}
