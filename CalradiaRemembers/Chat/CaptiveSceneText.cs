// Code written by Gabriel Mailhot, 24/06/2026.
// SRP extraction from ChatViewModel (Phase 2): the opener / setting TEXT a captive scene is seeded with —
// the per-intent opening cue, the randomized scene setup (locale, captor state, prisoner presentation,
// driving focus), the resume cues, and the story-log location line. Pure relocation of working static code:
// no behaviour change, no engine state held. Stays mod-side (not Logic) because it uses MBRandom / settlements.

#region

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
      // different story. Colors what this captor most wants out of the encounter tonight.
      private static readonly string[] SceneFoci = {
         "breaking their spirit through humiliation more than anything",
         "the raw physical use of their body, fast and brutal",
         "making them beg, perform, and put on a show",
         "marking and claiming them as your property",
         "cold, methodical degradation that takes its time",
         "drawing out their fear and dread before you even touch them",
         "toying with them — false mercy, sudden cruelty, keeping them off balance",
         "sheer dominance: proving they are yours to command",
         "rewarding the smallest obedience to twist their mind",
         "a contest of wills — you want them to break, not just submit",
         "putting them wholly beneath you — smothering, riding, using their body as furniture",
         "the ultimate insult: claiming and marking them as your property, treating them as an animal",
         "a foot-fetish power game — making them worship and serve your feet, beneath you in every sense",
         "forcing them to pleasure themselves on your command, and punishing any hesitation or refusal with the whip or your fists"
      };

      // Randomized opening setting so no two scenes start the same way. Seeds the INTRO with a
      // concrete locale, the captor's immediate state, and how the prisoner is presented — the
      // LLM's first response then cements it and the conversation history carries it forward.
      private static readonly string[] SceneLocales = {
         "at a muddy roadside camp", "in the mouth of a torchlit cave", "by the dying embers of a night fire",
         "in the shadow of a ruined watchtower", "at the edge of a reeking bog", "in the yard of a looted farmstead",
         "among the wagons of your raiding camp", "on a wind-scoured hilltop", "in a hollow thick with woodsmoke",
         "beside a cold forest stream", "in a half-collapsed barn", "at a ford strewn with old bones"
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
            default:
               return "[The bound prisoner stands before you. Begin in your own voice.]";
         }
      }

      public static string BuildResumeOpener()
         => "[You are NOT finished with this prisoner. The scene resumes — same prisoner, same place — and " + "you begin a new round with them. Open it fresh, picking up from where things stand, and do NOT " + "repeat what already happened.]";

      // A new driving focus only (the locale/people are unchanged from the prior round, which the
      // model already knows from the history), so the new cycle takes a different direction.
      public static string BuildResumeSetup()
      {
         string focus = SceneFoci[MBRandom.RandomInt(SceneFoci.Length)];

         return $" [A NEW ROUND with the same prisoner. What drives you THIS time is {focus}. Take it somewhere " + "NEW — do not repeat earlier acts.]";
      }

      public static string BuildSceneSetup()
      {
         string locale = SceneLocales[MBRandom.RandomInt(SceneLocales.Length)];
         string state = CaptorStates[MBRandom.RandomInt(CaptorStates.Length)];
         string pres = PrisonerPresentations[MBRandom.RandomInt(PrisonerPresentations.Length)];
         string focus = SceneFoci[MBRandom.RandomInt(SceneFoci.Length)];

         return $" [THIS SCENE'S SETTING — make the opening unique to it: it happens {locale}; you are {state}; " + $"the prisoner is {pres}. What drives you tonight is {focus}. Open from THIS specific situation in " + "your own fresh words — never a generic, reused capture opener. Let this scene be its own distinct " + "story, not a repeat of any other.]";
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
