// Code written by Gabriel Mailhot, 14/11/2026.
// The MCM drop-down choices, translatable. D.C.Michael (Discord, 14/11/2026) offered to translate CR into Chinese and
// found it "doesn't seem to support full localization yet"; the setting names, hints and groups carry ids since 19/10/2026,
// the choices did not. MCM shows each choice through the game's TextObject (SettingsPropertyVM: new TextObject(x.ToString())),
// so a "{=id}text" is translated, but the code read the chosen value back as English and compared it ("Off", "Mature"...):
// an id on a plain string would have made every such comparison silently false. A choice therefore carries both: the
// "{=id}English" MCM draws, and the English the code reads (Value). MCM saves a drop-down by its index, never by its text,
// so a save made before this keeps its choices; the order of a list must never change.

#region

using System;
using System.Collections.Generic;
using System.Linq;
using MCM.Common;

#endregion

namespace CalradiaRemembers.Foundation
{
   /// <summary>One choice of an MCM drop-down: shown translated, read back in English.</summary>
   public sealed class McmChoice : IEquatable<McmChoice>
   {
      /// <summary>A choice from its "{=id}English" literal (a literal with no id is shown as it is).</summary>
      public McmChoice(string localized)
      {
         string s = localized ?? "";
         int close = s.StartsWith("{=", StringComparison.Ordinal) ? s.IndexOf('}') : -1;
         Id = close > 2 ? s.Substring(2, close - 2) : null;
         Value = close > 2 ? s.Substring(close + 1) : s;
      }

      /// <summary>The translation id, or null.</summary>
      public string Id { get; }

      /// <summary>The English text: what the code compares, whatever language the page is shown in.</summary>
      public string Value { get; }

      /// <summary>A drop-down of these choices, <paramref name="selected" /> chosen by default.</summary>
      public static Dropdown<McmChoice> List(int selected, params string[] localized)
         => new Dropdown<McmChoice>(localized.Select(x => new McmChoice(x)), selected);

      /// <summary>What MCM draws: the id and its English fallback, which the game's TextObject translates.</summary>
      public override string ToString() => Id == null ? Value : "{=" + Id + "}" + Value;

      /// <inheritdoc />
      public bool Equals(McmChoice other) => other != null && string.Equals(Value, other.Value, StringComparison.Ordinal);

      /// <inheritdoc />
      public override bool Equals(object obj) => Equals(obj as McmChoice);

      /// <inheritdoc />
      public override int GetHashCode() => Value?.GetHashCode() ?? 0;
   }

   /// <summary>Reading and setting a translated drop-down by its English value.</summary>
   public static class McmChoiceExtensions
   {
      /// <summary>The chosen choice's English value, or null.</summary>
      public static string Choice(this Dropdown<McmChoice> dropdown) => dropdown?.SelectedValue?.Value;

      /// <summary>Chooses the choice whose English value is <paramref name="value" /> (case ignored). Whether it exists.</summary>
      public static bool Pick(this Dropdown<McmChoice> dropdown, string value)
      {
         if (dropdown == null) return false;

         int i = 0;
         foreach (McmChoice c in (IEnumerable<McmChoice>) dropdown)
         {
            if (string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase))
            {
               dropdown.SelectedIndex = i;
               return true;
            }

            i++;
         }

         return false;
      }
   }
}
