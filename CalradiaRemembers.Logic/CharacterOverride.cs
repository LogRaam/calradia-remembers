// Code written by Gabriel Mailhot, 22/06/2026.
// Player-authored character data: a hand-crafted personality (the five personality traits) plus an
// optional backstory for one NPC. Pure so the load-bearing rules — which traits are set, clamped to the
// engine's −2..+2 band — stay engine-agnostic and tested; the mod maps these onto Hero.SetTraitLevel.

#region

using System.Collections.Generic;
using NpcMemoryService.Core.Models;

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

        /// <summary>
        ///   What this character holds to be TRUE, and therefore what they pursue. The one authored field that
        ///   crosses the line <see cref="Backstory" /> deliberately refuses to cross: the backstory prompt tells
        ///   the model, in as many words, that it shapes "HOW you speak and WHO you are, NOT what you decide".
        ///   So a player who wrote "he believes he is secretly a necromancer" into a backstory got a character
        ///   who SAID so with great colour and then behaved exactly as before. The belief was a costume.
        ///   <para>
        ///     A conviction is a motive, not a costume. It may be false, and that is the point (a player asked for
        ///     a lord "who believes they were pulling the strings of my clan behind the scenes": Thoragoros1,
        ///     2026-07-12). It gives no powers and imports no lore. It cannot reach past the bridge: it may make
        ///     an NPC WANT to plot, never make a plot exist. The prompt proposes, the bridge rules.
        ///   </para>
        /// </summary>
        public string Conviction { get; set; }

        /// <summary>
        ///   Optional authored sexual orientation (a human word: straight, bi, gay/lesbian, pan, asexual...).
        ///   Overrides the NPC's rolled orientation, e.g. to enable a same-sex romance the roll blocked.
        /// </summary>
        public string Orientation { get; set; }

        /// <summary>
        ///   Optional authored kink lock (the same names <c>cr.kinks</c> lists as valid). When present, it
        ///   REPLACES the NPC's rolled/frozen kinks instead of the usual trait-derived roll, and (unlike
        ///   <see cref="Orientation" />) survives <c>cr.reload_lore</c>: a kink added at the table with
        ///   <c>cr.add_kink</c> was otherwise wiped the next time the lore reloaded (CharacterLoreBehavior
        ///   re-derives kinks from current traits on every reload). Null means the author left this field out
        ///   entirely (no lock, keep the rolled/frozen kinks); an empty list is a deliberate lock to NO kinks.
        /// </summary>
        public List<string> Kinks { get; set; }

        /// <summary>True when this override sets at least one trait.</summary>
        public bool HasTraits =>
            Valor.HasValue || Honor.HasValue || Mercy.HasValue || Generosity.HasValue || Calculating.HasValue;

        /// <summary>True when the backstory carries authored flavor.</summary>
        public bool HasBackstory => !string.IsNullOrWhiteSpace(Backstory);

        /// <summary>True when the author gave this character something they hold to be true.</summary>
        public bool HasConviction => !string.IsNullOrWhiteSpace(Conviction);

        /// <summary>True when the author pinned a sexual orientation.</summary>
        public bool HasOrientation => ParsedOrientation.HasValue;

        /// <summary>
        ///   True when the override carries ANY field the mod acts on: traits, a kink lock, a backstory, a
        ///   conviction, or an orientation. A character defined with ONLY a backstory (or conviction, or
        ///   orientation) is still authored content and must not be skipped as if the entry were empty (player
        ///   report: a character with no "kinks" entry was not processed by cr.reload_lore until kinks was added,
        ///   because the reload loop counted only traits and kinks as content).
        /// </summary>
        public bool HasAnyAuthoredContent =>
            HasTraits || ParsedKinks != null || HasBackstory || HasConviction || HasOrientation;

        /// <summary>The authored orientation parsed to an enum, or null when unset/unrecognized.</summary>
        public SexualOrientation? ParsedOrientation => OrientationParser.Parse(Orientation);

        /// <summary>
        ///   The authored kink lock, parsed. Null ONLY when <see cref="Kinks" /> itself is null (the author
        ///   omitted the field): a present-but-empty list parses to an empty (non-null) list, which is a
        ///   deliberate lock to no kinks at all, and callers must tell the two apart.
        /// </summary>
        public IReadOnlyList<Kink> ParsedKinks => Kinks == null ? null : KinkParser.Parse(Kinks);

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
