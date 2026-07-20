// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the opener / setting TEXT a captive scene is seeded with,
// the per-intent opening cue, the randomized scene setup (locale, captor state, prisoner presentation,
// driving focus), the resume cues, and the story-log location line. Pure relocation of working static code:
// no behaviour change, no engine state held. Stays mod-side (not Logic) because it uses MBRandom / settlements.
// Extended 01/07/2026: the scene focus pool is now split dark/positive by intent (Reward draws from the
// positive register, everything else from the dark one) and picked through the pure avoid-repeat
// CalradiaRemembers.Logic.SceneFocusSelector, so a captor no longer opens two rounds with the same focus.

#region

using CalradiaRemembers.Logic;
using NpcMemoryService.Core.Models;
using TaleWorlds.CampaignSystem;
using TaleWorlds.CampaignSystem.Party;
using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.Core;

#endregion

namespace CalradiaRemembers.Chat
{
   /// <summary>Builds the opening and setting text for a captive scene. Stateless; one job: scene flavour text.</summary>
   public static class CaptiveSceneText
   {
      private static readonly string[] CaptorStates = {
         "half-drunk on stolen wine", "still bloodied from the fight that took them", "bored and itching for sport",
         "counting the day's plunder", "in a foul mood after a lean, hungry week", "freshly woken and short-tempered",
         "flush with victory and generous with cruelty", "nursing a wound and meaner for it",
         "cold, unhurried, and entirely at ease", "restless and spoiling to break something"
      };

      private static readonly string[] PrisonerPresentations = {
         "dragged before you by the hair", "shoved stumbling into the firelight", "hauled up out of the baggage line",
         "thrown down at your feet", "marched up at spearpoint", "cut loose from the other captives and pushed forward",
         "dropped in the dirt, still half-stunned"
      };

      // A randomized driving FOCUS for the scene, so two scenes with the same intent still tell a
      // different story. Colors what this captor most wants out of the encounter tonight. This is the
      // DARK register, serving every sexual intent except Reward (see PositiveSceneFoci below).
      // Expanded 01/07/2026 from 14 to 26 entries, deliberately varied in DIRECTION (not just degree):
      // psychological manipulation, weaponized tenderness, corruption, forced complicity, theatre for
      // an audience, prolonged hope-and-denial, sensory play, ritual, and quieter dread alongside the
      // original physical/dominance/degradation entries.
      private static readonly string[] SceneFoci = {
         "breaking their spirit through humiliation more than anything",
         "the raw physical use of their body, fast and brutal",
         "making them beg, perform, and put on a show",
         "marking and claiming them as your property",
         "cold, methodical degradation that takes its time",
         "drawing out their fear and dread before you even touch them",
         "toying with them, false mercy, sudden cruelty, keeping them off balance",
         "sheer dominance: proving they are yours to command",
         "rewarding the smallest obedience to twist their mind",
         "a contest of wills, you want them to break, not just submit",
         "putting them wholly beneath you, smothering, riding, using their body as furniture",
         "the ultimate insult: claiming and marking them as your property, treating them as an animal",
         "a foot-fetish power game, making them worship and serve your feet, beneath you in every sense",
         "forcing them to pleasure themselves on your command, and punishing any hesitation or refusal with the whip or your fists",
         "turning their own past words back on them, until they cannot tell truth from trap",
         "a false, weaponized tenderness, gentle hands and softer words meant only to make the fall crueler",
         "corrupting whatever they are proudest of, their honor, their faith, their restraint, one small surrender at a time",
         "forcing them into a choice with no good answer, so their own decision does the breaking for you",
         "a theatrical humiliation staged for the benefit of everyone watching, not just the two of you",
         "a slow cat-and-mouse of hope offered and then snatched away, again and again",
         "sensory games, blindfolds, whispers, the unknown made into a weapon",
         "a ritual of ownership, formal and deliberate, as if branding them into a new station",
         "picking apart their composure piece by piece until nothing dignified is left standing",
         "using their own body against them, exhaustion, need, and shame turned into leverage",
         "an interrogation dressed as intimacy, so they cannot tell affection from extraction",
         "silence and stillness stretched past bearing, letting dread do the work before a hand is even raised"
      };

      // The POSITIVE register, drawn only when the intent is Reward: the carrot, not the stick.
      // Added 01/07/2026 so a rewarding scene reads as genuinely warmer, not just a milder version
      // of the dark pool.
      private static readonly string[] PositiveSceneFoci = {
         "rewarding their submission with real pleasure, given generously and without cruelty",
         "praising them warmly and letting them rest, comfortable, in your favor",
         "marking them, gently this time, as a favored possession rather than a captive",
         "granting a privilege they have earned: better quarters, a kindness, a moment of ease",
         "gentling them after the stick, all patience and low, approving words",
         "showing them what obedience buys: comfort, attention, and your genuine regard",
         "a slow, unhurried tenderness meant to teach them that yielding to you does not have to hurt"
      };

      // Randomized opening setting so no two scenes start the same way. Seeds the INTRO with a
      // concrete locale, the captor's immediate state, and how the prisoner is presented — the
      // LLM's first response then cements it and the conversation history carries it forward.
      // These WILDERNESS locales are for a captivity on the road / in a camp (no holding settlement).
      private static readonly string[] SceneLocales = {
         "at a muddy roadside camp", "in the mouth of a torchlit cave", "by the dying embers of a night fire",
         "in the shadow of a ruined watchtower", "at the edge of a reeking bog", "in the yard of a looted farmstead",
         "among the wagons of your raiding camp", "on a wind-scoured hilltop", "in a hollow thick with woodsmoke",
         "beside a cold forest stream", "in a half-collapsed barn", "at a ford strewn with old bones"
      };

      // When the player is held in a town or castle, the scene happens INSIDE it — a cell or a private
      // chamber of the captor's seat — not at a roadside camp. {0} is the settlement name.
      private static readonly string[] HeldLocales = {
         "in a cold stone cell beneath {0}", "in the torchlit dungeon of {0}",
         "in a dank holding cell under the keep of {0}", "in a private chamber off the dungeons of {0}",
         "in a bare, shuttered room within the walls of {0}", "in the lord's hall of {0}, the guards withdrawn to the door"
      };

      private static readonly string[] HeldPresentations = {
         "dragged up from the cells in chains", "hauled out of the dungeon and forced to kneel",
         "brought in under guard from the holding cell", "shoved through the door onto the cold flagstones",
         "marched up from below at spearpoint"
      };

      // A noble captor in their own seat is not a brigand counting plunder — a settlement scene draws from these.
      private static readonly string[] HeldCaptorStates = {
         "cold, unhurried, and entirely at ease", "calm, composed, and utterly in control",
         "nursing old grievances and meaner for it", "bored of the day's business and looking for diversion",
         "quietly furious beneath a polished surface", "savoring the leisure of having you wholly in their power"
      };

      public static string BuildCaptiveOpener(bool isCollective, CaptiveSceneIntent intent)
      {
         switch (intent)
         {
            case CaptiveSceneIntent.Interrogation:
               return isCollective
                  ? "[The guards drag the prisoner before you and force them to their knees. Others are present. You seek information or leverage. Begin the interrogation in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You seek information or leverage. Begin in your own voice.]";
            case CaptiveSceneIntent.PersonalDesire:
               return isCollective
                  ? "[You have had the prisoner brought to you. Others are present at your invitation. What you want from this person is personal — you intend to act on it, not merely speak about it. The scene is yours to begin.]"
                  : "[The bound prisoner stands before you. You are completely alone with them — no guards remain in the room. You want something from this person that goes beyond words — begin the encounter, and move toward it.]";
            case CaptiveSceneIntent.Domination:
               return isCollective
                  ? "[The prisoner is brought before you and an audience of your choosing. You intend to establish where power lies. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You intend to make clear what their position truly means. This is not a trial or a negotiation. Begin in your own voice.]";
            case CaptiveSceneIntent.Torture:
               return isCollective
                  ? "[The prisoner is brought in. Others are present — to witness, to participate, or both. What follows is not gentle. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — no one else is present. What you intend is not an interrogation. Begin in your own voice.]";
            case CaptiveSceneIntent.Training:
               return isCollective
                  ? "[The prisoner is brought before you and those you permit to attend. This is a training session — you are conditioning them toward obedience, building on what they have already been taught. Begin in your own voice.]"
                  : "[The bound prisoner stands before you — yours to shape. You are alone with them; the guards have withdrawn. This is a training session: continue conditioning them toward obedience. Begin in your own voice.]";
            case CaptiveSceneIntent.Reward:
               return isCollective
                  ? "[The prisoner is brought before you and a chosen audience. You mean to reward their cooperation — and perhaps to let others see your favor. Begin in your own voice.]"
                  : "[The bound prisoner stands before you. You are alone with them — the guards have withdrawn. You summoned them to reward their cooperation, not to punish. Begin in your own voice.]";
            case CaptiveSceneIntent.Extortion:
               return "[Your men drag the bound captive before you in the firelight of the bandit camp. They are disarmed and helpless, and you mean to wring some coin or advantage out of them. Open in your own voice — a brigand who smells money.]";
            case CaptiveSceneIntent.Intimidation:
               return "[The bound captive is shoved to their knees before you in the bandit camp. No lord, no law reaches them here. You mean to make them understand exactly how helpless they are. Begin in your own voice — all menace.]";
            case CaptiveSceneIntent.Revenge:
               return "[Your men throw the bound captive down before you. This one — or their kind — has cost your band men and blood, and now they will answer for it. Begin in your own voice, with the score you mean to settle.]";
            case CaptiveSceneIntent.Reckoning:
               return "[Your hired men have finally delivered the captured prisoner to you, bound and helpless. YOU are the one who paid to have them taken out of play. Open in your own voice: confront them face to face, let them realize who ordered this and why, reproach them for what they did to bring it on, and make plain they are yours to deal with now — their fate decided at your leisure, not tonight.]";
            default:
               return "[The bound prisoner stands before you. Begin in your own voice.]";
         }
      }

      /// <summary>
      ///   The user-turn nudge sent on each in-scene "Continue" beat (the stage directive in the system prompt
      ///   carries the actual content). Opens with a short DIEGETIC line, the scene simply waiting, because a
      ///   wall of meta-instructions here sends reasoning models (MiMo, GLM) into budget-eating deliberation
      ///   and they return no text at all; a player found that typing "*waiting to see what happens next*"
      ///   avoided the very failure this used to cause. Only the two load-bearing constraints stay (advance
      ///   without recap, never repeat); the full anti-repetition rules already live in the system prompt.
      ///   Shared with the quality self-test so the test exercises the exact runtime stimulus, never a
      ///   drifting copy.
      /// </summary>
      // The "Continue" button is a DIRECTOR cue, not a player turn: it means "carry the scene onward",
      // nothing more. The opener must NOT read as the prisoner doing something (Gabriel 2026-07-19: the old
      // "*The moment stretches in silence*" made the captor react to the prisoner CHOOSING silence, when the
      // player merely clicked Continue). The short diegetic line stays only because a bare wall of
      // meta-instructions makes reasoning models return no text (MiMo, GLM); it is now neutral time-passing,
      // attributed to no one, and the cue explicitly forbids reading it as the prisoner's silence or choice.
      // A player who WANTS to be silent in-fiction says so via "Try something else".
      public static string BuildContinuationStimulus()
         => "*Time moves; the scene carries on.* " +
            "[Continue: this is a director cue to move YOUR action forward, NOT a turn by the prisoner. The " +
            "prisoner has neither spoken nor acted this beat: do NOT treat this as their silence, submission, " +
            "defiance, or any choice on their part, and do not react to a non-existent action from them. Simply " +
            "carry your own action onward from the last thing that happened. No restart, no recap of your " +
            "history, grievances, or reasons; never reuse an image, phrase, or gesture from earlier in the " +
            "scene. Respect what is already established: if the prisoner is already bound, kneeling, or " +
            "stripped, do not stage it again, build onward from that state. Fresh, specific physical detail " +
            "in [NARRATION].]";

      public static string BuildResumeOpener()
         => "[You are NOT finished with this prisoner. The scene resumes — same prisoner, same place — and " +
            "you begin a new round with them. Open it fresh, picking up from where things stand, with vivid, " +
            "specific physical detail in [NARRATION]. Do NOT recap or repeat what already happened, and do NOT " +
            "reuse phrases or images from the earlier rounds — this round must read as its own distinct beat.]";

      /// <summary>The right focus pool for this intent: the positive register for Reward, the dark register for everything else.</summary>
      private static string[] FociPoolFor(CaptiveSceneIntent intent)
         => intent == CaptiveSceneIntent.Reward ? PositiveSceneFoci : SceneFoci;

      // A new driving focus only (the locale/people are unchanged from the prior round, which the
      // model already knows from the history), so the new cycle takes a different direction.
      // lastFocusIndex tracks the previously-chosen index (caller state, e.g. ChatViewModel) so
      // SceneFocusSelector can avoid picking the same focus two rounds running.
      public static string BuildResumeSetup(CaptiveSceneIntent intent, ref int lastFocusIndex)
         => BuildResumeSetup(intent, ref lastFocusIndex, out _);

      /// <summary>As the other overload, also exposing the new round's <paramref name="focus" /> to persist.</summary>
      public static string BuildResumeSetup(CaptiveSceneIntent intent, ref int lastFocusIndex, out string focus)
      {
         string[] pool = FociPoolFor(intent);
         int index = SceneFocusSelector.PickIndex(pool.Length, lastFocusIndex, MBRandom.RandomInt);
         lastFocusIndex = index;
         focus = pool[index];

         return $" [A NEW ROUND with the same prisoner. What drives you THIS time is {focus}. Take it somewhere " +
                "genuinely NEW, a different act, a different angle, and render it with fresh sensory description. " +
                "Never repeat an earlier act, image, or turn of phrase.]";
      }

      public static string BuildSceneSetup(CaptiveSceneIntent intent, ref int lastFocusIndex)
         => BuildSceneSetup(intent, ref lastFocusIndex, out _);

      /// <summary>
      ///   As <see cref="BuildSceneSetup(CaptiveSceneIntent, ref int)" />, also handing back the chosen
      ///   <paramref name="focus" /> so the caller can persist it into every later turn's context (the scene
      ///   throughline). The text embeds the focus for the OPENER; the out param is that same string, for the
      ///   ongoing prompt.
      /// </summary>
      public static string BuildSceneSetup(CaptiveSceneIntent intent, ref int lastFocusIndex, out string focus)
      {
         Settlement held = ResolveHoldingSettlement();

         string locale, state, pres;
         if (held != null) // held in a town/castle: a cell or a private hall of the captor's seat, not a road camp
         {
            locale = string.Format(HeldLocales[MBRandom.RandomInt(HeldLocales.Length)], held.Name);
            state = HeldCaptorStates[MBRandom.RandomInt(HeldCaptorStates.Length)];
            pres = HeldPresentations[MBRandom.RandomInt(HeldPresentations.Length)];
         }
         else // on the road / in a camp
         {
            locale = SceneLocales[MBRandom.RandomInt(SceneLocales.Length)];
            state = CaptorStates[MBRandom.RandomInt(CaptorStates.Length)];
            pres = PrisonerPresentations[MBRandom.RandomInt(PrisonerPresentations.Length)];
         }

         string[] pool = FociPoolFor(intent);
         int index = SceneFocusSelector.PickIndex(pool.Length, lastFocusIndex, MBRandom.RandomInt);
         lastFocusIndex = index;
         focus = pool[index];

         return $" [THIS SCENE'S SETTING, make the opening unique to it: it happens {locale}; you are {state}; " + $"the prisoner is {pres}. What drives you tonight is {focus}. Open from THIS specific situation in " + "your own fresh words, never a generic, reused capture opener. Ground the opening in a [NARRATION] " + "block of vivid, specific physical detail. Let this scene be its own distinct " + "story, not a repeat of any other.]";
      }

      /// <summary>
      ///   The town or castle the player is currently held in, or null when the captivity is on the road / in a
      ///   camp. Same resolution chain as <see cref="BuildStoryLocation" /> (which already names the place right in
      ///   the log), narrowed to a fortified seat — a village is not a prison, so it keeps the wilderness framing.
      /// </summary>
      private static Settlement ResolveHoldingSettlement()
      {
         try
         {
            Settlement s = MobileParty.MainParty?.CurrentSettlement
                           ?? Hero.MainHero?.CurrentSettlement
                           ?? Hero.MainHero?.StayingInSettlement;

            return s != null && (s.IsTown || s.IsCastle) ? s : null;
         }
         catch { return null; }
      }

      public static string BuildStoryLocation()
      {
         try
         {
            Settlement s =
               MobileParty.MainParty?.CurrentSettlement ?? Hero.MainHero?.CurrentSettlement ?? Hero.MainHero?.StayingInSettlement;

            if (s == null) return null;
            string kind = s.IsCastle ? "the castle of"
               : s.IsTown ? "the town of"
               : "the village of";

            return $"{kind} {s.Name}";
         }
         catch { return null; }
      }
   }
}
