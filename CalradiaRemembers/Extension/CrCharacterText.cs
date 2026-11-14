// Code written by Gabriel Mailhot, 14/11/2026.
// Extension Surface: other mods write text about a character, for the prompt and, when they ask, the conversation panel.
// Bhelogan (Discord, 14/11/2026): his Detective's Notebook wrote 600 to 1,200 characters of description into
// character_overrides.json between markers, which showed in the panel. "The per-character text source you described, yes
// please. Something like: CrCharacterText.RegisterSource(sourceMod, (heroId, facts) => new CrCharacterTextEntry { Text,
// Section = Appearance | Backstory | Private, ShowInPanel, Lean }) ... the same for the player (in place of
// player_description.txt) ... stable within a conversation, so it can sit in the cached prefix. With that, the Notebook
// stops writing your file entirely." CharacterTextComposer decides what is kept. CR names no mod. Since 2.8.10.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using CalradiaRemembers.Foundation;
using CalradiaRemembers.Logic.Character;

#endregion

namespace CalradiaRemembers
{
   /// <summary>Where a piece of your text about a character belongs. Since 2.8.10.</summary>
   public enum CrCharacterTextSection
   {
      /// <summary>How they look: added after CR's own appearance in the prompt, and under Appearance in the panel when asked.</summary>
      Appearance = 0,

      /// <summary>More of who they are: given to the model, and under Character in the panel when asked.</summary>
      Backstory = 1,

      /// <summary>What they keep to themself (a secret): given to the model only, never shown. Ignored for the player.</summary>
      Private = 2
   }

   /// <summary>What CR tells your source when it asks for a character's text. Since 2.8.10.</summary>
   public sealed class CrCharacterTextFacts
   {
      /// <summary>The character's hero StringId.</summary>
      public string HeroId { get; set; }

      /// <summary>True when the character is the player's own hero.</summary>
      public bool IsPlayer { get; set; }

      /// <summary>
      ///   True when the text is read by someone else (the player as a character sees them, or the panel); false when the
      ///   character reads it of themself (their own appearance, in the second person in CR's own block).
      /// </summary>
      public bool SeenByOther { get; set; }

      /// <summary>True when the prompt is built Compact for a small model: only entries marked Lean are kept then.</summary>
      public bool Lean { get; set; }
   }

   /// <summary>One piece of your text about a character. Since 2.8.10.</summary>
   public sealed class CrCharacterTextEntry
   {
      /// <summary>The text. At most 1,200 characters (300 when <see cref="Lean" /> is meant for the Compact prompt); longer is left out whole.</summary>
      public string Text { get; set; }

      /// <summary>Where it belongs.</summary>
      public CrCharacterTextSection Section { get; set; }

      /// <summary>Whether to show it in the conversation panel (never for Private). Default true.</summary>
      public bool ShowInPanel { get; set; } = true;

      /// <summary>Whether to keep it in the Compact prompt for small models (then at most 300 characters). Default false.</summary>
      public bool Lean { get; set; }
   }

   /// <summary>The door through which other mods write text about a character, for the prompt and the panel. Since 2.8.10.</summary>
   public static class CrCharacterText
   {
      private static readonly SortedDictionary<string, Func<CrCharacterTextFacts, IEnumerable<CrCharacterTextEntry>>> Sources = new(StringComparer.Ordinal);
      private static readonly HashSet<string> Faulted = new(StringComparer.Ordinal);

      /// <summary>
      ///   Registers your source of text about characters, NPCs and the player alike. CR calls it each time it builds a prompt
      ///   or opens a character's panel (keep it cheap), with <see cref="CrCharacterTextFacts" />, and it returns the entries
      ///   for that character, or null or nothing. Return the same text for the same character until it really changes: it sits
      ///   in the cached part of the prompt. Pieces from all mods are ordered by source name, at most three per section, each
      ///   at most 1,200 characters. Appearance follows CR's own and never replaces it; Backstory and Private have sections of
      ///   their own, and a character keeps a Private text to themself and never lies about it. Registering the same
      ///   <paramref name="sourceMod" /> again replaces it. Call it on the game thread.
      /// </summary>
      public static void RegisterSource(string sourceMod, Func<CrCharacterTextFacts, IEnumerable<CrCharacterTextEntry>> source)
      {
         if (string.IsNullOrWhiteSpace(sourceMod) || source == null) return;

         lock (Sources)
         {
            Sources[sourceMod.Trim()] = source;
            Faulted.Remove(sourceMod.Trim());
         }

         ModLog.Info($"[CHARACTER TEXT API] source registered by '{sourceMod.Trim()}'");
      }

      /// <summary>Withdraws the source of <paramref name="sourceMod" />. Returns whether one was there.</summary>
      public static bool UnregisterSource(string sourceMod)
      {
         if (sourceMod == null) return false;

         lock (Sources) return Sources.Remove(sourceMod.Trim());
      }

      /// <summary>Every piece the registered sources give for one character. A source that throws gives nothing (logged once).</summary>
      internal static IReadOnlyList<CharacterTextPiece> PiecesFor(string heroId, bool isPlayer, bool seenByOther)
      {
         if (string.IsNullOrEmpty(heroId)) return Array.Empty<CharacterTextPiece>();

         List<KeyValuePair<string, Func<CrCharacterTextFacts, IEnumerable<CrCharacterTextEntry>>>> sources;
         lock (Sources)
         {
            if (Sources.Count == 0) return Array.Empty<CharacterTextPiece>();

            sources = Sources.ToList();
         }

         var facts = new CrCharacterTextFacts {HeroId = heroId, IsPlayer = isPlayer, SeenByOther = seenByOther, Lean = ModSettings.LeanPrompt};
         var pieces = new List<CharacterTextPiece>();
         foreach (KeyValuePair<string, Func<CrCharacterTextFacts, IEnumerable<CrCharacterTextEntry>>> source in sources)
            try
            {
               foreach (CrCharacterTextEntry entry in source.Value(facts) ?? Enumerable.Empty<CrCharacterTextEntry>())
               {
                  if (entry == null || (isPlayer && entry.Section == CrCharacterTextSection.Private)) continue;

                  pieces.Add(new CharacterTextPiece {
                     Source = source.Key, Section = (CharacterTextSection) (int) entry.Section, Text = entry.Text,
                     ShowInPanel = entry.ShowInPanel, Lean = entry.Lean
                  });
               }
            }
            catch (Exception ex)
            {
               bool first;
               lock (Sources) first = Faulted.Add(source.Key);
               if (first) ModLog.Error($"[CHARACTER TEXT API] the source of '{source.Key}' threw and gives nothing (logged once): {ex.GetType().Name}: {ex.Message}");
            }

         return pieces;
      }
   }
}
