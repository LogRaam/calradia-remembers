// Code written by Gabriel Mailhot, 01/11/2026.
// What the letters need to know of a call for help (DepaHN, Nexus, 27/10/2026): the letter window offers the answer and the
// courier reports the arrival. The behaviour that keeps the pleas needs the letters to send one, so it stands behind this door
// rather than the two knowing each other.

namespace CalradiaRemembers.Letters
{
   /// <summary>A call for help as its letter shows it, while it waits for an answer.</summary>
   public sealed class PleaOnLetter
   {
      /// <summary>The player can do what is asked; otherwise only "I cannot" is offered.</summary>
      public bool CanAccept { get; set; }

      /// <summary>The words on the button that says yes.</summary>
      public string AcceptText { get; set; }

      /// <summary>The words on the button that says no.</summary>
      public string DeclineText { get; set; }

      /// <summary>The mod's own line beside the buttons: what each answer costs.</summary>
      public string Note { get; set; }
   }

   /// <summary>The side of a call for help that the letters deal with.</summary>
   public interface ILetterPleaDesk
   {
      /// <summary>The plea from this writer waiting for an answer, or null.</summary>
      PleaOnLetter AwaitingAnswerFrom(string senderId);

      /// <summary>The courier handed the letter over.</summary>
      void OnLetterReached(string senderId, int today);

      /// <summary>The letter arrived about a subject the player had turned off: it was never seen, and asks nothing of them.</summary>
      void OnLetterStopped(string senderId);

      /// <summary>True while this writer has a plea open: its letter is not stale.</summary>
      bool StillStands(string senderId);

      /// <summary>The player said yes.</summary>
      void Accept(string senderId);

      /// <summary>The player said no.</summary>
      void Refuse(string senderId);
   }

   /// <summary>The door to the pleas, set by the campaign behaviour that keeps them.</summary>
   public static class LetterPleas
   {
      /// <summary>The desk, or null outside a campaign.</summary>
      public static ILetterPleaDesk Desk { get; set; }
   }
}
