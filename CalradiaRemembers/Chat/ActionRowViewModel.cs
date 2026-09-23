// Code written by Gabriel Mailhot, 22/09/2026.
// One line of the Actions panel: a heading, or something the player may ask for, or something that is shut.
//
// fkasad (22/09/2026) went to the LLM to ask what the mod could do, and got charm instead of facts, because a
// model asked about the rules reasons about its own prompt. The mod knows. This is the row that says so.
//
// ONE TEMPLATE FOR EVERY KIND OF LINE, because the alternative is three parallel lists that scroll apart. A
// heading has a label and nothing else; an open action has a label and a link; a shut one has a label, a reason
// in muted text, and a lock where the link would be. The prefab shows or hides the three parts, so the column
// down the right stays straight whatever the row is.
//
// THE LOCK MEANS SHUT, NOT UNCLICKABLE (Gabriel, 22/09/2026). A thing the NPC may do of their own accord is
// neither: it carries no link, because it is not the player's to ask, and no lock, because nothing is stopping
// it. Three states in one column, and each is honest about itself.

#region

using System;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>One row of the Actions panel, bound by the prefab's item template.</summary>
   public sealed class ActionRowViewModel : ViewModel
   {
      private readonly Action _activate;

      public ActionRowViewModel(string label, string reason, ActionRowKind kind, Action activate = null)
      {
         Label = label ?? "";
         Reason = reason ?? "";
         Kind = kind;
         _activate = activate;
      }

      /// <summary>What this row is, which decides which parts the template shows.</summary>
      public ActionRowKind Kind { get; }

      [DataSourceProperty] public string Label { get; }

      /// <summary>Why it is shut, in player language. Empty on a heading or an open action.</summary>
      [DataSourceProperty] public string Reason { get; }

      /// <summary>True on a heading, which the template renders larger and without a link or a lock.</summary>
      [DataSourceProperty] public bool IsHeading => Kind == ActionRowKind.Heading || Kind == ActionRowKind.Section;

      /// <summary>The big painted-style title that separates the two halves of the list.</summary>
      [DataSourceProperty] public bool IsSection => Kind == ActionRowKind.Section;

      /// <summary>Shows the link. Only ever true for something the player may ask for right now.</summary>
      [DataSourceProperty] public bool CanAsk => Kind == ActionRowKind.Askable;

      /// <summary>True for a real line rather than a title: the template lays those out side by side with their affordance.</summary>
      [DataSourceProperty] public bool IsLine => !IsHeading;

      /// <summary>True when there is a why to show under the line.</summary>
      [DataSourceProperty] public bool HasReason => !string.IsNullOrWhiteSpace(Reason);

      /// <summary>Shows the padlock. True for anything shut, in either half of the list.</summary>
      [DataSourceProperty] public bool IsShut => Kind == ActionRowKind.Shut;

      /// <summary>The link's words. Held here rather than in the prefab so the wording is changed in one place.</summary>
      [DataSourceProperty] public string AskText => "Ask this of them";

      /// <summary>
      ///   The padlock's file path, carried on the ROW rather than read from the chat view model, because the
      ///   item template binds against this object and would resolve a chat-level property to nothing.
      ///   Transparent PNG, so no CorrectRedBlue (the JPG backgrounds need it, this does not).
      /// </summary>
      [DataSourceProperty]
      public string LockPath => System.IO.Path.Combine(
         TaleWorlds.Library.BasePath.Name, "Modules", "CalradiaRemembers", "GUI", "Images", "cr_lock.png");

      /// <summary>The link, and the click on a foldable heading. Inert on every other row.</summary>
      public void ExecuteActivate() => _activate?.Invoke();
   }

   /// <summary>What a row IS, which is the only thing the template needs to know to lay it out.</summary>
   public enum ActionRowKind
   {
      /// <summary>One of the two big titles: what you may ask, and what may come of this.</summary>
      Section,

      /// <summary>A rank heading inside a section, including the foldable one at the bottom.</summary>
      Heading,

      /// <summary>The player may ask for this now: the only kind that carries a link.</summary>
      Askable,

      /// <summary>Shut, with a reason: carries the padlock.</summary>
      Shut,

      /// <summary>Something the NPC may do unasked. No link, because it is not the player's to ask; no lock, because nothing is stopping it.</summary>
      MayHappen
   }
}
