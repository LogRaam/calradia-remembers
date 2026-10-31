// Code written by Gabriel Mailhot, 31/10/2026.
// The killing window's console door (zhenja1, 31/10/2026; Gabriel: the chance in the window, a rolling number, the
// scene told by the model): play the window in the open chat with a chosen chance and roll, so both outcomes can be
// seen and judged without coaxing the model into an attempt. Nobody is harmed; the scene and what follows are real.

#region

using System.Collections.Generic;
using CalradiaRemembers.Chat;
using CalradiaRemembers.Logic.Violence;
using CalradiaRemembers.Violence;
using TaleWorlds.Library;

#endregion

namespace CalradiaRemembers
{
   public static partial class ModCheats
   {
      /// <summary>cr.kill_roll [chance] [roll]: plays the killing window in the open chat; nobody is harmed.</summary>
      [CommandLineFunctionality.CommandLineArgumentFunction("kill_roll", "cr")]
      public static string KillRollCommand(List<string> args)
      {
         ChatViewModel chat = ChatManager.ActiveViewModel;
         if (chat?.KillRollPartner == null) return "Open a conversation with a character first, then run cr.kill_roll [chance] [roll].";

         int chance = args != null && args.Count > 0 && int.TryParse(args[0], out int c) ? c : 50;
         int shown = args != null && args.Count > 1 && int.TryParse(args[1], out int r) ? r : chance - 10;
         chance = System.Math.Min(System.Math.Max(chance, 1), 100);
         shown = System.Math.Min(System.Math.Max(shown, 1), 100);
         bool killed = KillAttemptPolicy.Succeeds(shown, chance);

         ViolenceFacts facts = ViolenceSceneReader.FactsFor(chat.KillRollPartner);
         chat.KillRollForcedRoll = shown - 1;
         bool offered = chat.OfferKillAttempt(chat.KillRollPartner, facts, chance, roll =>
            new KillAttemptResult {Verdict = killed ? ViolenceVerdict.Succeeded : ViolenceVerdict.Failed, Killed = killed, Message = "(cr.kill_roll: nobody was harmed)"});

         if (!offered) return "This chat cannot hold the killing window now (another attempt open, the conversation over, a council, or not a character's conversation).";

         return $"The killing window is open: chance {chance}, the roll will land on {shown} ({(killed ? "a kill" : "a failure")}). Nobody is harmed, but the scene and what follows are real: "
                + (killed ? "the conversation will end." : "the character will answer it.");
      }
   }
}
