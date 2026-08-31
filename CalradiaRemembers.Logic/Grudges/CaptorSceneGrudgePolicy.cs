// Code written by Gabriel Mailhot, 31/08/2026.
// Player report (Mimo, Standard/Roleplay mode): holding a captive and ordering them kept SAFE and well treated
// still planted a CaptiveAbuse grudge, as if the player had tormented them. Root cause: the grudge formed from
// the scene's OPENING INTENT alone (Interrogation=1 already cleared the old formation floor of 1), never from
// what the player actually DID. This policy makes the grudge CONDUCT-gated: the intent sets a CEILING, but a
// scene the player carried out with restraint plants nothing. When the host can read the scene's actual conduct
// (a transcript verdict) it decides on that; when it cannot (Standard, no interpreter), it falls back to the
// conservative rule that only an inherently-abusive intent (Torture, physical torment by definition) forms a
// grudge, so a merciful interrogation or a restrained domination never resents the player who spared them.

#region

#endregion

namespace CalradiaRemembers.Logic.Grudges
{
   /// <summary>
   ///   Pure, engine-free decision for whether (and how hard) a closed CAPTOR scene, the player holding an NPC
   ///   prisoner, plants a CaptiveAbuse grudge. The opening intent's severity is the ceiling; the player's actual
   ///   conduct decides whether it forms. Feeds <see cref="Tier2GrudgeRules.CaptiveAbuseGrudge" /> through the
   ///   returned severity, so the downstream magnitude/formation rules are unchanged.
   /// </summary>
   public static class CaptorSceneGrudgePolicy
   {
      /// <summary>
      ///   The intent-severity at/above which the scene is abuse by its very nature, so a grudge forms even with
      ///   no conduct read available. Set to 3 (Torture): physical torment leaves a mark whatever else is said,
      ///   while a lesser intent (interrogation, domination, training, a coerced advance) CAN be carried out with
      ///   restraint and so must be CONFIRMED as abusive before it resents the player.
      /// </summary>
      public const int InherentlyAbusiveMinSeverity = 3;

      /// <summary>
      ///   The grudge severity a closed captor scene should carry, capped by the opening intent.
      /// </summary>
      /// <param name="intentSeverity">
      ///   The ceiling from the scene's opening intent (0 = Reward, 1 = Interrogation, 2 = Domination/Training/
      ///   PersonalDesire, 3 = Torture), as <c>CaptiveIntentRules.CaptiveAbuseSeverity</c> maps it.
      /// </param>
      /// <param name="abuseConfirmed">
      ///   A read of what the player actually DID: <c>true</c> the scene contained real abuse, <c>false</c> the
      ///   player showed restraint/mercy, <c>null</c> no reliable read was available (Standard, no interpreter).
      /// </param>
      public static int GrudgeSeverity(int intentSeverity, bool? abuseConfirmed)
      {
         if (intentSeverity <= 0) return 0; // Reward and the like never resent, whatever happened.

         if (abuseConfirmed == true) return intentSeverity;  // confirmed abuse: the full weight the intent allows.
         if (abuseConfirmed == false) return 0;              // confirmed mercy: conduct overrides intent, nothing forms.

         // No reliable read: only an inherently-abusive intent forms, the conservative fallback that fixes the
         // report (a merciful lesser scene must not plant a grudge just because of how it was framed at the start).
         return intentSeverity >= InherentlyAbusiveMinSeverity ? intentSeverity : 0;
      }
   }
}
