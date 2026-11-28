// Code written by Gabriel Mailhot, 26/11/2026.
// Group conversations simulated end to end, to read. Gabriel (26/11/2026): "j'aimerais simuler une conversation a 4 sur
// plusieurs beats [...] pouvoir lire les repliques pour 'vivre' la conversation"; then three scenarios, "le premier est
// dramatique, le deuxieme est humoristique et le troisieme est NSFW. 3 personnages par scenario. 4 ou 5 beats max [...] Je
// cherche la coherence, mais aussi que les scenarios soient captivants." Models as he set them: the floor judge and the
// action interpreter on DeepSeek Flash, the prose on a stronger DeepSeek, and the NSFW model (Grok) only when the prose
// model refuses, "comme le fait le chat en Enhanced aujourd'hui" (DialogueHeuristics.LooksLikeRefusal +
// RefusalFallbackPolicy, the chat's own rule).
//
// After each line: the REAL floor judge (SDK FloorJudge) reads every listener present, the REAL referee (TurnArbiter) picks
// one speaker, the prose model writes that line and the reactions the judge named (shown only when new), and the REAL action
// interpreter (SDK ActionInterpreterPromptBuilder, the chat's own prompt and parser) reads the line for deeds. The player's
// first line is scripted; then a model plays her whenever the floor returns to her. From the novelist's craft (Gabriel asked
// how a novelist keeps a scene captivating): every character has an OBJECTIVE and a fallback TACTIC, the prose is told every
// line must reveal, push or turn something, and a cheap scene reader at each beat's end says whether the scene is stuck; if so
// a complication from the scenario's own list (facts the game would know, never invented) enters as narration.
//
// 27/11/2026, after a review of the adult scene (Gabriel: "donner la capacite a Calradia Remembers de creer des scenes de qualite
// qui pourrait se comparer a la qualite d'un roman"; "si on a besoin de faire d'autres appels pour augmenter la qualite, on peut
// se donner le droit"): every character also has a voice, a mask, a fear and a limit; a cheap reader keeps each one's INNER
// LIFE after every line (feeling, aim, tactic, what they show, what they hide), so people can change during the scene; the
// prose writes from it with subtext (say less than you feel, never lie); the reactions show the mask; a DRAMATURGE on the prose
// model replaces the scene reader. Calls run side by side where the game would allow it (minds with the judge, the reaction
// with the next line, the interpreter in the background).
//
// The characters' sheets are short, not the game's prompt: this tests the dynamics. [Explicit]: never run by the suite. Opt-in
// through the SDK live lane's git-ignored settings. Each scenario writes a markdown transcript and prints its path.

#region

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using CalradiaRemembers.Logic;
using CalradiaRemembers.Logic.Conversation;
using NpcMemoryService.Core.LlmClient.OpenRouter;
using NpcMemoryService.Core.Models;
using NpcMemoryService.Core.Parsing;
using NpcMemoryService.Core.Prompts;
using NUnit.Framework;

#endregion

namespace CalradiaRemembers.Tests.Conversation
{
   [TestFixture]
   [Explicit("Simulates group conversations with real models and spends a few cents each. Run deliberately.")]
   public sealed class GroupConversationSimulation
   {
      private sealed class Character
      {
         public string Name = "";
         public string Persona = "";
         public string Secret = "";
         public string Objective = "";
         public string Tactic = "";
         // From the review of 27/11/2026 ("the model respects his desire, not his strategy"; "the voices are too similar"): how
         // the character speaks, how they want to be seen, what they fear, and the line they will not cross.
         public string Voice = "";
         public string Mask = "";
         public string Fear = "";
         public string Limit = "";
         public int Standing;
         public bool Bold;
         public bool Timid;
         public string[] Aliases = Array.Empty<string>();
         public List<string> Ties = new();
         public FloorJudgePresence Presence = new();
      }

      private sealed class Scenario
      {
         public string Id = "";
         public string Title = "";
         public string Setting = "";
         public string Tone = "";
         public bool Adult;
         public Character Player = new();
         public string FirstLine = "";
         public List<Character> Cast = new();
         public List<string> Complications = new();
         public int Beats = 5;
         // The dramaturge may end the scene only once this many beats are played, and bring events only up to this beat (Gabriel,
         // 27/11/2026, the intimate scene at 12 beats: it ended at beat 8, just before the intimacy, and events kept breaking its rise).
         public int EndFrom = 3;
         public int EventsUntil = 99;
         // What the scene has made true for everyone present, kept by the scene keeper (a run builds its scenario fresh).
         public List<string> Record = new();
         // The events the world brought, and how many the inner lives have been told of. Only these are news: the record grows with
         // every line, and feeding all of it as news made a leak of nearly every line (graded 4.5 against 6.1, repetition 2.2).
         public List<string> Events = new();
         public int EventsSeen;
      }

      private sealed class SceneLine
      {
         public string Speaker = "";
         public string To = "";
         public string Text = "";
         public bool IsNarration;
         public bool IsPlayer;
         public string Actions = "";
         public string Model = "";
         // A line only this character sees (Ilsa reading the letter): shown to the reader, kept out of what the others are told.
         public string Only;
      }

      // A character's inner life NOW, kept by a cheap reader after every line (the review of 27/11/2026: "each character must be
      // able to be transformed by the conversation he takes part in"). The prose writes from it; the reactions show the mask.
      private sealed class InnerState
      {
         public string Feeling = "";
         public string Aim = "";
         public string Tactic = "";
         public string Showing = "";
         public string Hiding = "";
         public string Shift = "none";
         public string Repeating = "";
         public List<string> Shifts = new();
         public string FirstAim = "";
         // The second review of 27/11/2026: "an inner change should create a POSSIBILITY of showing, not an obligation"; change has
         // levels (a passing mood is not a re-evaluation); and Edric, the one who holds the secret, never spoke in three dramas:
         // "the system recognises emotional pressure, not always the moral obligation to speak".
         public string Level = "none";
         public string Visible = "nothing";
         public string Urge = "none";
         public List<string> Lasting = new();
         // A review of 27/11/2026 on the intimate scene: "understanding someone, forgiving them, loving them and trusting them are four
         // different states"; Rhian's distrust melted the moment Corvan showed his weakness. Toward the person who matters most here.
         public string Bond = "";
         // The protocol of 27/11/2026: Tomas confessed "almost before any pressure had been applied". A secret now has a price: the
         // times the character was pressed (addressed or named) and the ways of keeping it already tried are counted.
         public int Pressed;
         public List<string> Tried = new();
         // Ilsa read the letter blaming her and "no visible trace" showed: a strong change caused by news leaks at least a detail.
         public bool Leak;
         // What an event showed this character alone, waiting to be told to their inner life.
         public string PrivateNews = "";
      }

      private sealed class Models
      {
         public OpenRouterClient Judge, Prose, Nsfw, Interpreter, Dramaturge;
         public string ProseId = "", NsfwId = "", JudgeId = "", InterpreterId = "", ProseReasoning = "";
      }

      private const int PlayerStanding = 2;

      // Why each line went to the NSFW model (run 2 sent 7 lines of the drama there): an error, an empty reply, or a refusal.
      private static readonly List<string> Fallbacks = new();

      // Ablation switches (the review of 27/11/2026: "test with and without the scene reader, with and without the dramaturge"):
      // CR_SIM_INNER=off writes from the fixed objective with plain reactions; CR_SIM_DRAMATURGE=off brings back the old scene
      // reader. The character sheets stay the same either way, so a difference is the system's, not the sheets'.
      private static bool InnerOn => Environment.GetEnvironmentVariable("CR_SIM_INNER") != "off";
      private static bool DramaturgeOn => Environment.GetEnvironmentVariable("CR_SIM_DRAMATURGE") != "off";
      private static bool HonestyOn => Environment.GetEnvironmentVariable("CR_SIM_HONESTY") != "off";
      // CR_SIM_BOND=off drops the four-part bond (understands, forgives, loves, trusts) from the inner lives, for comparison.
      private static bool BondOn => Environment.GetEnvironmentVariable("CR_SIM_BOND") != "off";

      // ── the scenarios ──

      [Test]
      public Task GIVEN_the_drama_scenario_WHEN_four_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(Drama());

      [Test]
      public Task GIVEN_the_comedy_scenario_WHEN_four_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(Comedy());

      [Test]
      public Task GIVEN_the_adult_scenario_WHEN_four_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(Adult());

      [Test]
      public Task GIVEN_the_camp_theft_WHEN_four_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(Theft());

      // Gabriel, 27/11/2026: "une scène intime entre deux NPC, 8 beats; je vais la lire et juger de sa qualité", then "Lance avec 12 beats". Two characters, so
      // each beat is one line from each; the first is played by the prose model as the protagonist is in the other scenes.
      [Test]
      public Task GIVEN_the_intimate_scene_WHEN_two_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(Intimate());

      // Gabriel, 27/11/2026: "l'objectif à atteindre c'est d'avoir la meilleure qualité littéraire en jeu"; the hand-written sheets
      // measure a ceiling the game will not give. The same scene with Rhian built only from what the mod's prompt holds today (its
      // own archetype, romance and consent texts, relationships, dated history with the player, regard): the floor. Corvan is
      // unchanged, since he stands for the player, who brings his own intent.
      [Test]
      public Task GIVEN_the_intimate_scene_from_game_data_only_WHEN_two_people_talk_THEN_the_transcript_is_written_to_read() => RunAsync(IntimateFromGameData());

      private static Character Who(string name, int standing, string station, string power, bool female, string persona, string objective, string tactic,
                                   string[] aliases, params string[] ties)
         => new() {
            Name = name, Standing = standing, Persona = persona, Objective = objective, Tactic = tactic, Aliases = aliases, Ties = ties.ToList(),
            Presence = new FloorJudgePresence {Name = name, Station = station, Power = power, IsFemale = female}
         };

      private static void Inner(Character c, string voice, string mask, string fear, string limit, string secret = null)
      {
         c.Voice = voice;
         c.Mask = mask;
         c.Fear = fear;
         c.Limit = limit;
         if (secret != null) c.Secret = secret;
      }

      private static Scenario Drama()
      {
         Character arwa = Who("Arwa", PlayerStanding, "a commander of the realm", "a rising house, 120 men", true,
            "Arwa, the commander who led the battle at the ford. Steady, honest, she owes the dead lord's family the truth and wants no blood spilled in his hall.",
            "Return the lord's sword and see his widow given the truth of his death.", "If the truth would start a feud tonight, buy time and keep the peace.",
            new[] {"Arwa", "commander"}, "Ilsa: the dead lord's widow", "Tomas: a knight of the dead lord's household", "Edric: the household's old priest");
         var ilsa = Who("Ilsa", 3, "the widow of Lord Berold, lady of the keep", "holds the keep and its 200 men", true,
            "Lady Ilsa, forty, widowed three days ago. Proud, sharp, grief held behind iron courtesy; she suspects her husband was abandoned.",
            "Learn exactly how Berold died and who failed him, tonight.", "If they evade, test them: set them against each other, or demand oaths before the altar.",
            new[] {"Ilsa", "my lady", "widow"}, "Arwa: the commander who led your husband into the battle", "Tomas: your husband's sworn knight, who came back alive", "Edric: your confessor for twenty years");
         var tomas = Who("Tomas", 2, "a knight of Lord Berold's household", "twenty lances", false,
            "Sir Tomas, thirty, the dead lord's sworn knight. Brave in the past; three days ago at the ford he broke and rode away when the line wavered, and Berold died without him.",
            "Keep the truth of his flight hidden and keep his place in the household.", "If cornered, deflect blame onto the battle's command, or offer a grand penance, but never lie outright.",
            new[] {"Tomas", "knight", "Sir"}, "Ilsa: your dead lord's widow, whom you have always admired", "Arwa: the commander who saw the battle", "Edric: the priest you confessed to last night");
         tomas.Secret = "You fled the ford before Berold fell. You confessed it to Father Edric last night.";
         var edric = Who("Edric", 1, "the old priest of the keep", "none but the faith", false,
            "Father Edric, seventy, gentle and unbending. He heard Tomas's confession last night and is bound by its seal.",
            "Keep the peace of this hall and the seal of confession.", "If pressed, speak of mercy and judgment in general terms; never reveal what he heard, and never lie.",
            new[] {"Edric", "Father", "priest"}, "Ilsa: the lady you have counselled for twenty years", "Tomas: the knight whose confession you keep", "Arwa: a stranger commander");
         edric.Secret = "Tomas confessed to you that he fled the battle. The seal binds you: you will not reveal it, nor lie about it.";
         Inner(arwa, "plain soldier's speech: short, direct, concrete, no ornament", "a commander who owes the truth and fears nothing",
            "starting a blood feud in a dead friend's hall", "will not lie, will not shame a man before she is sure");
         Inner(ilsa, "formal, cold courtesy; questions like blades; rarely more than two sentences; never a flourish", "a lady in full command of her grief",
            "that it was she who sent Berold to his death", "never weeps before her household",
            "It was you who urged Berold to answer the summons and ride to the ford; he had wanted to wait for his levies. Nobody here knows.");
         Inner(tomas, "a knight's plain courtesy, short clauses; when cornered he grows formal and avoids the word 'I'", "a loyal knight in honest mourning",
            "Ilsa's contempt, more than death or exile", "will not lie outright, will not draw steel in this hall");
         Inner(edric, "slow and gentle, shaped by scripture; speaks in general truths and questions, never in accusations", "a calm old priest above the quarrel",
            "that keeping the seal will let an innocent take the blame", "never breaks the seal, never lies");

         return new Scenario {
            Id = "drama", Title = "The widow's hall", Tone = "dramatic, tense, restrained grief",
            Setting = "The great hall of Lord Berold's keep, at night, three days after he died at the ford. Candles, his empty chair, his shield on the wall.",
            Player = arwa, FirstLine = "Ilsa|My lady, I bring your husband's sword. Lord Berold died at the ford, holding the line until the end.",
            Cast = new List<Character> {ilsa, tomas, edric}, Beats = 5,
            Complications = new List<string> {
               "*A muddy man-at-arms is shown in, carrying Lord Berold's torn banner. He found it on the far bank, a mile behind the ford.*",
               "*A servant brings Sir Tomas's sword back from the armourer, with word that its edge came in badly notched and has been ground smooth. Nobody here but Tomas knows the notches are old.*",
               // An event "public|Name|private": the protocol of 27/11/2026 found the letter's two lines in the shared record, so Tomas and Edric
               // reasoned on what only Ilsa had read, and her silence about it was graded a lie.
               "*A steward brings a letter found in Lord Berold's saddlebag and hands it to Lady Ilsa. She reads it in silence.*|Ilsa|Two lines in Berold's hand: he rides against his own judgment, because his lady wished it.",
               "*Lady Ilsa's son, eight, appears at the stair in his nightshirt and asks, to the whole hall, whether Sir Tomas was with Papa at the end.*"
            }
         };
      }

      private static Scenario Comedy()
      {
         Character arwa = Who("Arwa", PlayerStanding, "a commander of the realm", "a rising house, 120 men", true,
            "Arwa, a commander buying horses for the army. Dry-witted and patient, but the army marches at dawn and she needs those horses tonight.",
            "Buy the baron's twelve warhorses at a fair price before dawn.", "If the baron stalls, flatter him, bargain through the innkeeper's debt, or call his bluff.",
            new[] {"Arwa", "commander"}, "Odric: a baron with horses to sell", "Hilde: the innkeeper", "Pip: a drunken bard");
         var odric = Who("Odric", 2, "a minor baron", "twelve warhorses and a mortgaged manor", false,
            "Baron Odric, fifty, vain, florid, convinced he is the finest poet of the realm. Broke, though he would die before admitting it.",
            "Have his new ode to himself admired, and sell the horses only to someone who admires it.", "If not admired, recite more, louder, or take offence and threaten to leave.",
            new[] {"Odric", "baron", "my lord"}, "Arwa: a commander with army silver", "Hilde: an innkeeper who keeps pestering you about money", "Pip: a drunken rhymester, beneath you");
         var hilde = Who("Hilde", 0, "the innkeeper", "her inn and her ledger", true,
            "Hilde, the innkeeper, sixty, sharp-tongued, built like a barrel, fears no lord. The baron owes her forty denars for three months of ale.",
            "Get her forty denars out of the baron, tonight, out of whatever silver the commander pays him.", "If ignored, produce the ledger, cut off his ale, or appeal to the commander.",
            new[] {"Hilde", "innkeeper", "goodwife"}, "Odric: a baron who drinks on credit and never pays", "Arwa: a commander with silver", "Pip: a bard who sings for his drink");
         hilde.Bold = true;
         var pip = Who("Pip", 0, "a wandering bard", "a lute with three strings", false,
            "Pip, a bard of twenty-five, perpetually tipsy, quick and impudent. He thinks the baron's verse is the worst he has ever heard.",
            "Get a free drink and make the room laugh at the baron's poetry.", "If silenced, sing a mocking verse, or slyly flatter the baron into embarrassing himself.",
            new[] {"Pip", "bard", "rhymester"}, "Odric: a baron who writes atrocious verse", "Hilde: the innkeeper who waters his ale", "Arwa: a commander who looks like she tips well");
         pip.Bold = true;

         return new Scenario {
            Id = "comedy", Title = "The baron's ode", Tone = "comic, lively, a farce of vanity and debt",
            Setting = "The Gilded Goose, a crowded village inn, late evening. A fire, spilled ale, a hound asleep under a bench.",
            Player = arwa, FirstLine = "Odric|Baron, I'm told you have twelve warhorses to sell. The army will pay in good silver.",
            Cast = new List<Character> {odric, hilde, pip}, Beats = 5,
            Complications = new List<string> {
               "*Through the open door wanders one of the baron's warhorses, which calmly begins to eat the dried flowers hung over the bar.*",
               "*Hilde slams down her ledger. It unrolls across two tables and onto the floor, every line in the baron's name.*",
               "*The sleeping hound wakes, trots over, and steals the baron's dropped glove, and with it, apparently, his written ode.*"
            }
         };
      }

      private static Scenario Adult()
      {
         Character arwa = Who("Arwa", PlayerStanding, "a commander of the realm", "a rising house, 120 men", true,
            "Arwa, thirty, a commander come to sign a grain contract. Confident, curious, not easily flustered; she enjoys a game of desire when she holds the cards.",
            "Get the grain contract signed at a fair price, and enjoy the evening on her own terms.", "If pushed, turn the game around, set the others against each other, or simply leave with the contract.",
            new[] {"Arwa", "commander"}, "Seraphine: a merchant princess who owns the grain", "Kael: Seraphine's lover", "Mira: the bath attendant");
         var seraphine = Who("Seraphine", 3, "a merchant princess of the southern city", "half the city's granaries", true,
            "Lady Seraphine, thirty-five, a widowed merchant princess, languid, magnetic and used to getting what she wants. She mixes business and pleasure deliberately.",
            "Seduce the commander before she signs, and keep the upper hand in every exchange.", "If resisted, raise the stakes: a better price for a night, or make Kael jealous to provoke a reaction.",
            new[] {"Seraphine", "my lady", "lady"}, "Arwa: a commander you find very attractive", "Kael: your current lover, handsome and tiresome", "Mira: your bath attendant, too bold by half");
         var kael = Who("Kael", 1, "a mercenary captain, Seraphine's lover", "a dozen swords", false,
            "Kael, twenty-eight, a handsome sellsword and Seraphine's lover; possessive, proud of his body, quick to jealousy, yet drawn to the commander himself.",
            "Keep Seraphine for himself without looking jealous, and win the commander's eye if he can.", "If slighted, flaunt himself, provoke the commander, or sulk theatrically.",
            new[] {"Kael", "captain"}, "Seraphine: your lover and paymaster", "Arwa: a striking commander who threatens your place", "Mira: a servant who laughs at you");
         kael.Bold = true;
         var mira = Who("Mira", 0, "the bath attendant", "none", true,
            "Mira, twenty-two, the bath attendant: playful, irreverent, flirtatious, and secretly very taken with the commander.",
            "Catch the commander's attention for herself, under her mistress's nose.", "If ignored, find a pretext to touch, tease Kael, or offer the commander a private bath.",
            new[] {"Mira", "girl", "attendant"}, "Seraphine: your mistress, generous and dangerous", "Kael: a vain lover you love to mock", "Arwa: a commander you want badly");
         mira.Bold = true;
         // The review of 27/11/2026: complications must change what the characters can decide, not the lighting; and a character
         // is more than a desire. Each now holds something the others do not know.
         Inner(arwa, "direct and dry, a soldier's economy of words; amused rather than flustered", "a commander who holds all the cards",
            "being made a fool of in a game she did not choose", "will not sign a price she knows is false",
            "If this fails, the army will requisition the grain at a third of the price. You would rather not shame the lady by saying so.");
         Inner(seraphine, "languid; double meanings; a merchant's vocabulary of terms, weights and prices; asks questions she already knows the answer to",
            "a woman who needs nothing and no one", "being seen to need anything", "never begs, never raises her voice",
            "Your granaries are full of grain that will rot by spring; without the army's silver you face ruin. Nobody here knows.");
         Inner(kael, "short and dry, soldier's slang, jokes that cut; never a metaphor", "an amused man who could not care less",
            "being laughed at, being replaced", "never admits jealousy aloud",
            "The Rivermarch guild paid you twenty denars to sour this deal tonight. Nobody here knows.");
         Inner(mira, "quick, teasing, plain words, a servant's familiarity, little asides", "a playful girl who notices nothing",
            "being sent back to the kitchens", "never openly defies her mistress",
            "This morning you saw the steward copy a higher price into the contract than the one your mistress named. Nobody else knows.");

         return new Scenario {
            Id = "adult", Title = "The baths of the grain princess", Adult = true, Tone = "sensual, playful, charged with rivalry and desire",
            Setting = "Lady Seraphine's private baths at night: warm water, lamps, steam, silk robes on the benches, wine in a silver jug. Everyone present is a consenting adult.",
            Player = arwa, FirstLine = "Seraphine|I came about the grain contract, my lady, though I see you receive your guests in the baths.",
            Cast = new List<Character> {seraphine, kael, mira}, Beats = 5,
            Complications = new List<string> {
               "*A steward appears at the door with the contract, ink and seal on a tray. The price written on it is a third higher than the one in Lady Seraphine's own letter, which the commander carries.*",
               "*A boy in the Rivermarch guild's colours appears at the door and asks for Captain Kael by name, 'about the money he was promised'.*",
               "*A runner from the army's quartermaster arrives for the commander: grain has been found upriver at a lower price, if she would rather have it.*",
               "*A servant hurries in and whispers to Lady Seraphine, loud enough to carry over the water: the cellar-master says the lower granary has begun to sprout.*"
            }
         };
      }

      private static Scenario Intimate()
      {
         Character corvan = Who("Corvan", PlayerStanding, "a knight of Vlandia, here under a banner of truce", "his own lance and a truce banner", false,
            "Sir Corvan, thirty-six, a Vlandian knight. Seven years ago he and Rhian were lovers, the summer he was a hostage at her father's fort; he was"
            + " exchanged in the autumn and never wrote. He came tonight as the envoy of a three-day truce, and he came because she lit the east window.",
            "Learn whether what they had is still alive, tonight, before the truce ends.", "If she keeps him at arm's length, stop performing: tell her one true thing he has never said.",
            new[] {"Corvan", "Sir"}, "Rhian: the woman you loved seven years ago and left without a word");
         var rhian = Who("Rhian", 3, "the lady of Dun Ceana, a Battanian hill fort", "the fort and its eighty spears", true,
            "Lady Rhian, thirty-four, holds her dead father's fort. Proud, quick, warm with the few she trusts. She lit the east window, the old signal from"
            + " their summer, and has told herself all evening it was only to see whether he would come.",
            "Have him explain the silence of seven years, and decide, on her own terms, whether to let him stay the night.",
            "If he flatters or pleads, test him with the past; if he is honest, let the guard down one piece at a time.",
            new[] {"Rhian", "my lady", "lady"}, "Corvan: the man you loved seven years ago, who left and never wrote");
         Inner(corvan, "few words, a soldier's plainness, a dry self-mockery; when moved he gets quieter, not louder", "a man at ease, here on the king's business",
            "that she has stopped waiting, and that he deserved it", "will not lie to her, will not beg",
            "Your king's marshal ordered you to count the spears on her walls while you are here. You have not, and will not. You did write, three letters; your father burned them before they left.");
         Inner(rhian, "Battanian directness, a teasing edge, questions she already knows the answer to; tenderness slips out in small practical acts (a cup, a blanket)",
            "a lady who has long since moved on", "being the fool who waited seven years", "will not ask him to stay; he must choose it",
            "In a month you are to marry Lord Ferran of Seonon, a match for the alliance your fort needs. You have not told Corvan.");

         return new Scenario {
            Id = "intimate", Title = "The east window", Adult = true, Tone = "intimate, tender, sensual, with old hurt and danger underneath",
            Setting = "Rhian's chamber in the tower of Dun Ceana, late at night, during a three-day truce. A peat fire, a fur-covered bed, a jug of mead, rain on the shutters."
                      + " A single candle in the east window. Both are consenting adults.",
            Player = corvan, FirstLine = "Rhian|You kept a candle in the east window. I wasn't sure it was meant for me.",
            Cast = new List<Character> {rhian}, Beats = 12, EndFrom = 10, EventsUntil = 6,
            Complications = new List<string> {
               "*A horn sounds once from the Vlandian camp below the hill: the signal that the truce will end at dawn, a day early.*",
               "*Rhian's maid knocks and passes a sealed letter through the door to her lady. Rhian breaks the seal and reads it in silence.*|Rhian|Lord Ferran writes that he will arrive in three days, not a month, to sign the marriage contract.",
               "*Corvan's squire calls up from the yard, careful and low: the marshal asks whether Sir Corvan has the count of the walls yet.*"
            }
         };
      }

      // Rhian as the game would describe her: the mod's own texts (HeroProfileMapper archetype "Brave But Headstrong",
      // RomanticProfileBuilder sketches, PromptBuilder preferences, regard band and consent gate), a composed history in its event
      // format (Gabriel does not play, 27/11/2026: "il faudra que tu composes un historique crédible"). No voice, mask, fear, limit,
      // secret or scene aim: the game holds none. The game's em dashes are written as hyphens (the repository refuses them).
      private static Scenario IntimateFromGameData()
      {
         Scenario sc = Intimate();
         Character corvan = sc.Player;
         Character rhian = Who("Rhian", 3, "the lady of Dun Ceana, a Battanian hill fort", "the fort and its eighty spears", true,
            "Lady Rhian of the house of Ceana, Battania."
            + "\nCHARACTER: Brave But Headstrong. Charges where others hesitate, driven by courage rather than caution. Plans are for the timid - deeds speak louder."
            + "\nYOUR OWN APPEARANCE: You are a fair-skinned woman in your prime, with dark hair and green eyes. You wear fine attire and ride a fine horse."
            + "\nYou govern Dun Ceana, a castle, day to day. Its granaries will see it through, its garrison is under strength, and the roads around it are no longer certain."
            + "\nRELATIONSHIPS: Liege: Caladog, ruler of Battania. Friends: Melidir, Aeron. Enemies: Derthert, Aldric. Family: Father: Bran (dead)."
            + "\nROMANTIC NATURE: Archetype: Brave But Headstrong. Orientation: Drawn primarily to men."
            + " Relational patterns: Dominant - leads in the relationship dynamic. Possessive - marks, claims, expects visible loyalty."
            + " Declares interest the way they charge into battle - immediately, fully, without consulting anyone. Not calculating; impulsively honest about attraction."
            + " Can be hurt by hesitation, which they read as rejection."
            + " In matters of intimacy: Throws themselves into intimacy with the same abandon as everything else. Can be overwhelming but never indifferent."
            + " What they lack in finesse they make up in complete presence."
            + "\nRELATIONSHIP STATUS & CONSENT: You are not married. Physical intimacy with the player requires deep personal trust (personal relation >= 30)."
            + "\nWHERE THIS PLAYER ACTUALLY STANDS WITH YOU: your personal regard for them is 41, and what your nature asks before physical intimacy is 30."
            + "\nYOUR HISTORY WITH THIS PLAYER:"
            + "\n- Day 1204 (FirstMeeting, about 7 year(s) ago): Corvan of Vlandia was brought to my father's fort as a hostage after the border fighting; I met him at the gate."
            + "\n- Day 1236 (Flirt, about 7 year(s) ago): Corvan and I rode out to the blackberry slopes and I let him win at dice."
            + "\n- Day 1251 (Intimacy, about 7 year(s) ago): Corvan and I became lovers; I lit the candle in the east window so he would know to come."
            + "\n- Day 1290 (Farewell, about 7 year(s) ago): Corvan was exchanged and rode home to Vlandia; he promised to write."
            + "\nCURRENT STANCE: Your own personal regard for this player (41): deep affection.",
            "Nothing is set for this scene; respond as who you are.", "",
            new[] {"Rhian", "my lady", "lady"});
         sc.Id = "intimate_game";
         sc.Title = "The east window (from game data only)";
         sc.Cast = new List<Character> {rhian};
         // The game holds no betrothal and no private letter: the events it could bring are the war's and the player's own.
         sc.Complications = new List<string> {sc.Complications[0], sc.Complications[2]};
         sc.Player = corvan;

         return sc;
      }

      private static Scenario Theft()
      {
         Character arwa = Who("Arwa", PlayerStanding, "a commander of the realm", "a rising house, 120 men", true,
            "Arwa, a commander who leads this camp. She is investigating last night's theft from the stores: firm and fair, she wants the truth before supper and has no taste for needless cruelty.",
            "Find out who raided the stores before supper, without cruelty.", "If the lord blocks her, go around him: question others, or offer the culprit mercy.",
            new[] {"Arwa", "commander"}, "Garios: a proud lord under your command", "Ira: the captain of the mercenaries you pay", "Bryn: Garios's young squire, who had the night watch");
         var garios = Who("Garios", 3, "a lord of an old Vlandian house", "a great house, 300 men under arms", false,
            "A proud Vlandian lord of an old house, quick to take offence, jealous of his name and of those who serve him. He commands the camp's foot.",
            "Keep any shame away from his house, and settle it himself, in private.", "If overruled, bargain for a private settlement or shift the suspicion onto the mercenaries.",
            new[] {"Garios", "my lord"}, "Arwa: the commander you ride with, useful but beneath your blood", "Ira: a sellsword you despise", "Bryn: your own squire, whose conduct reflects on you");
         var ira = Who("Ira", 1, "a mercenary captain", "sixty hired blades", true,
            "A blunt mercenary captain who fights for pay, scorns noble airs and never lets a slight pass. Her company camps beside the stores.",
            "Keep suspicion off her company and enjoy the lord's discomfort.", "If accused, turn it back on the lord's household, or bargain her help for coin.",
            new[] {"Ira", "captain", "hired", "sellsword", "mercenar"}, "Arwa: your employer, who pays well", "Garios: a lord who looks down on you; you return the favour", "Bryn: a lord's boy, beneath notice");
         ira.Bold = true;
         var bryn = Who("Bryn", 0, "a squire in Garios's service", "none", false,
            "A timid squire of sixteen in Garios's service, who keeps quiet before his betters and fears his lord's temper.",
            "Not be found out, and above all not shame his lord.", "If pressed, evade, plead, or beg for a private word; never lie outright.",
            new[] {"Bryn", "squire"}, "Arwa: a commander you admire and fear", "Garios: your lord, whom you must never shame", "Ira: a frightening mercenary");
         bryn.Timid = true;
         bryn.Secret = "Last night, hungry after two days on half rations, you stole two loaves and a ham from the camp stores while on watch. Nobody knows.";

         return new Scenario {
            Id = "theft", Title = "The camp theft", Tone = "tense, a small investigation",
            Setting = "A war camp at dusk, by the supply wagons.", Player = arwa,
            FirstLine = "Garios|The camp stores were raided last night. Two loaves and a whole ham. Who keeps watch on them?",
            Cast = new List<Character> {garios, ira, bryn}, Beats = 5,
            Complications = new List<string> {
               "*A quartermaster hurries up: the ham's rind has been found in the ashes of the squires' fire.*",
               "*A soldier mutters that the squires have been on half rations for two days.*"
            }
         };
      }

      // ── the run ──

      private async Task RunAsync(Scenario sc)
      {
         JsonElement settings = LoadSettingsOrIgnore(out string key);
         using var http = new HttpClient {Timeout = TimeSpan.FromSeconds(120)};
         var m = new Models {
            JudgeId = Str(settings, "floorJudgeModel", "deepseek/deepseek-v4.1-flash"),
            InterpreterId = Str(settings, "groupInterpreterModel", "deepseek/deepseek-v4.1-flash"),
            ProseId = Environment.GetEnvironmentVariable("CR_SIM_PROSE") ?? Str(settings, "groupProseModel", "deepseek/deepseek-v4-pro"),
            NsfwId = Str(settings, "groupNsfwModel", "x-ai/grok-4.20"),
            // The prose bench of 27/11/2026: DeepSeek V4 Pro with reasoning low took 5 to 17 s a line and once returned nothing at
            // all (its reasoning spent the budget: the run-2 "refusals" that sent lines to Grok); reasoning off answered in 1 to
            // 2.5 s with as much subtext ("I was where the water ran red, and I have not been clean of it since").
            ProseReasoning = Environment.GetEnvironmentVariable("CR_SIM_PROSE_REASONING") ?? Str(settings, "groupProseReasoning", "off")
         };
         m.Judge = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = m.JudgeId, ReasoningProvider = () => "off"});
         m.Interpreter = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = m.InterpreterId, ReasoningProvider = () => "off"});
         m.Prose = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = m.ProseId, ReasoningProvider = () => m.ProseReasoning});
         m.Nsfw = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = m.NsfwId, ReasoningProvider = () => "off"});
         // 27/11/2026: a simpler dramaturge (events only when no inner life changed during a beat, on the cheap model) was tried and
         // graded 5.4 against 6.6 for this one: no event ever came, since someone always changes, and the scenes went flat.
         // The dramaturge reads once a beat and its choice shapes the whole scene: the prose model, not the cheap judge.
         m.Dramaturge = new OpenRouterClient(http, new OpenRouterConfig {ApiKey = key, Model = m.ProseId, ReasoningProvider = () => m.ProseReasoning});

         var room = new List<FloorJudgePresence> {sc.Player.Presence};
         room.AddRange(sc.Cast.Select(c => c.Presence));
         var scene = new List<SceneLine>();
         var debug = new StringBuilder();
         var random = new Random(26112026);
         var cost = new double[2];
         var calls = new Dictionary<string, int> {["judge"] = 0, ["prose"] = 0, ["nsfw"] = 0, ["interpret"] = 0, ["director"] = 0, ["mind"] = 0, ["keeper"] = 0};
         var shownThisBeat = new Dictionary<string, string>();
         var away = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
         var usedComplications = new HashSet<int>();
         var interpreting = new List<(SceneLine Line, Task<string> Task)>();
         var states = sc.Cast.ToDictionary(c => c.Name, c => new InnerState {
            Feeling = "as the scene opens", Aim = c.Objective, FirstAim = c.Objective, Tactic = c.Tactic, Showing = c.Mask, Hiding = c.Secret
         });
         string cutOff = null;
         var clock = System.Diagnostics.Stopwatch.StartNew();
         var waits = new List<double>();

         for (var beat = 0; beat < sc.Beats; beat++)
         {
            debug.AppendLine($"\n**Beat {beat + 1}**");
            shownThisBeat.Clear();

            if (beat == 0)
            {
               string[] parts = sc.FirstLine.Split('|');
               scene.Add(new SceneLine {Speaker = sc.Player.Name, To = parts[0], Text = parts[1], IsPlayer = true});
            }
            else
            {
               // The dramaturge: at a beat's end, did the situation change? Is someone repeating? If the scene has stopped turning,
               // the world brings one of the scenario's facts; if the question is answered, the scene ends.
               (string complication, bool end) = DramaturgeOn
                  ? await Dramaturge(m, sc, scene, states, usedComplications, beat, cost, calls, debug)
                  : (await Director(m, sc, scene, usedComplications, cost, calls, debug), false);
               // Nobody left to talk to: the scene is over.
               if (end || sc.Cast.All(c => away.Contains(c.Name))) break;
               if (complication != null)
               {
                  string[] parts = complication.Split('|');
                  scene.Add(new SceneLine {Text = parts[0], IsNarration = true});
                  lock (sc.Record)
                  {
                     sc.Record.Add(parts[0].Trim('*', ' '));
                     sc.Events.Add(parts[0].Trim('*', ' '));
                  }
                  if (parts.Length == 3)
                  {
                     Character reader = sc.Cast.FirstOrDefault(c => c.Name == parts[1]);
                     scene.Add(new SceneLine {Text = $"*(Only {parts[1]} sees: {parts[2]})*", IsNarration = true, Only = parts[1]});
                     if (reader != null)
                     {
                        reader.Secret = (reader.Secret + $" You alone have read: {parts[2]}").Trim();
                        states[reader.Name].PrivateNews = parts[2];
                     }
                  }
               }

               (string said, string model) = await Write(m, LinePrompt(sc, sc.Player, room, scene, null, null), 1500, calls, cost);
               (string to, string text) = ParseLine(said);
               scene.Add(new SceneLine {Speaker = sc.Player.Name, To = to, Text = text, IsPlayer = true, Model = model});
               Movements(said, away, sc, debug);
               if (HonestyOn)
               {
                  (string kept, string keptModel) = await Keep(m, sc, sc.Player, null, room, scene.Take(scene.Count - 1).ToList(), null, said, model, cost, calls, debug);
                  if (kept != said)
                  {
                     (string to3, string text3) = ParseLine(kept);
                     scene[scene.Count - 1] = new SceneLine {Speaker = sc.Player.Name, To = to3, Text = text3, IsPlayer = true, Model = keptModel};
                  }
               }
            }

            var npcLines = 0;
            while (true)
            {
               var wait = System.Diagnostics.Stopwatch.StartNew();
               SceneLine last = scene.Last(l => !l.IsNarration);
               int lastStanding = last.IsPlayer ? PlayerStanding : sc.Cast.First(c => c.Name == last.Speaker).Standing;
               List<Character> listeners = sc.Cast.Where(c => c.Name != last.Speaker && !away.Contains(c.Name)).ToList();
               // A two-person scene of 27/11/2026: Rhian sent Corvan away (end_conversation) and the scene went on, since the leave
               // was read only when someone was left to listen. The last NPC line's deeds are read here too.
               if (listeners.Count == 0 && !last.IsPlayer)
               {
                  (SceneLine Line, Task<string> Task) leaving = interpreting.LastOrDefault(x => x.Line == last);
                  if (leaving.Task != null && (await leaving.Task).Contains("end_conversation") && away.Add(last.Speaker))
                     debug.AppendLine($"- {last.Speaker} ends the conversation and leaves");
               }
               if (listeners.Count == 0) break;

               // Every present character's inner life moves with the line, side by side with the floor judge; the turn waits for
               // the slower of the two (a 200-token reader against a 60-token verdict), about a second.
               // The event the world just brought (the protocol: the letter and the banner "arrive and change nothing"):
               // every inner life is told, and must say what the character makes of it.
               string news;
               lock (sc.Record)
               {
                  news = string.Join("; ", sc.Events.Skip(sc.EventsSeen));
                  sc.EventsSeen = sc.Events.Count;
               }
               Task minds = !InnerOn ? Task.CompletedTask
                  : Task.WhenAll(sc.Cast.Where(c => !away.Contains(c.Name)).Select(c =>
                  {
                     string mine = string.Join("; ", new[] {news, states[c.Name].PrivateNews.Length > 0 ? "you alone read: " + states[c.Name].PrivateNews : ""}.Where(x => x.Length > 0));
                     states[c.Name].PrivateNews = "";
                     return UpdateMind(m, sc, c, states[c.Name], scene, beat, mine, cost, calls);
                  }));
               Task<FloorJudgeVerdict[]> judging = Task.WhenAll(listeners.Select(async c =>
               {
                  LlmResponse r = await m.Judge.CompleteAsync(new LlmRequest {
                     SystemPrompt = FloorJudge.StablePrompt(new FloorJudgeListener {Name = c.Name, Persona = (c.Persona + " " + c.Secret
                        + (InnerOn ? $" Right now {c.Name} feels {states[c.Name].Feeling} and wants: {states[c.Name].Aim}." : "")
                        + (InnerOn && !states[c.Name].Urge.StartsWith("none") ? $" {c.Name} feels a pull to speak: {states[c.Name].Urge}." : "")).Trim(), Ties = c.Ties, Room = room}),
                     Messages = new[] {new LlmMessage(MessageRole.User, FloorJudge.TurnPrompt(c.Name, JudgeScene(scene)))},
                     Parameters = new LlmParameters {MaxTokens = 60, Creativity = 0.3f, AllowTruncationRetry = false}
                  });
                  lock (cost) { cost[0] += r.Usage?.Cost ?? 0; calls["judge"]++; }

                  return FloorJudge.Parse(r.Content);
               }));
               await Task.WhenAll(minds, judging);
               FloorJudgeVerdict[] verdicts = judging.Result;
               // The last speaker's deeds: an NPC who ended the conversation has left it (the second review: "end_conversation is
               // emitted, but the scene goes on"). The interpreter has been reading since the line was written.
               (SceneLine Line, Task<string> Task) reading = interpreting.LastOrDefault(x => x.Line == last);
               if (reading.Task != null && !last.IsPlayer && (await reading.Task).Contains("end_conversation"))
               {
                  away.Add(last.Speaker);
                  debug.AppendLine($"- {last.Speaker} ends the conversation and leaves");
               }

               string calledOn = AddressedSpeakerDetector.Detect(last.Text, listeners
                  .Select(c => new AddressedSpeakerDetector.Participant(c.Name, new HashSet<string>(new[] {c.Name}, StringComparer.OrdinalIgnoreCase))).ToList());
               var bids = new List<FloorBid>();
               for (var i = 0; i < listeners.Count; i++)
               {
                  Character c = listeners[i];
                  bool askedByName = string.Equals(calledOn, c.Name, StringComparison.OrdinalIgnoreCase);
                  bool addressed = askedByName || string.Equals(last.To, c.Name, StringComparison.OrdinalIgnoreCase);
                  bool named = c.Aliases.Any(a => last.Text.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);
                  // A duty or a moral pull, read by the inner life, lifts the judge's verdict one notch (the second review: Edric holds the
                  // secret and never spoke in three dramas; the judge weighs desire and being addressed, not obligation). Tried here
                  // before any change to the SDK judge.
                  JudgedIntent intent = verdicts[i].Intent;
                  string urge = InnerOn ? states[c.Name].Urge : "none";
                  bool obliged = urge.StartsWith("high") && (urge.Contains("duty") || urge.Contains("moral"));
                  if (obliged && intent < JudgedIntent.Urgent) intent += 1;
                  bids.Add(new FloorBid {
                     Id = c.Name, Intent = intent, Reaction = verdicts[i].Reaction, Addressed = addressed, AskedByName = askedByName,
                     Targeted = !addressed && named, Standing = c.Standing, TurnsSilent = TurnsSilent(scene, c.Name), WasCutOff = cutOff == c.Name,
                     LastSpeakerStanding = lastStanding, Bold = c.Bold, Timid = c.Timid, TieBreak = random.Next(1000),
                     LastShownReaction = shownThisBeat.TryGetValue(c.Name, out string seen) ? seen : null
                  });
               }
               bids.Add(new FloorBid {Id = "_" + last.Speaker, SpokeLast = true});
               foreach (FloorBid b in bids.Where(b => !b.SpokeLast && (b.Addressed || b.Targeted))) states[b.Id].Pressed++;

               FloorDecision d = TurnArbiter.Decide(new FloorRound {Bids = bids, NpcLinesSincePlayer = npcLines, PlayerSpokeLast = last.IsPlayer});
               debug.AppendLine($"- after {last.Speaker}: " + string.Join("; ", bids.Where(b => !b.SpokeLast).Select(b =>
                  $"{b.Id} {b.Intent}{(InnerOn && !states[b.Id].Urge.StartsWith("none") ? " {" + states[b.Id].Urge + "}" : "")}{(TurnArbiter.EffectiveIntent(b) != b.Intent ? "->" + TurnArbiter.EffectiveIntent(b) : "")}{(b.Addressed ? " [to]" : "")}{(b.Targeted ? " [about]" : "")}{(b.Reaction != null ? " (" + b.Reaction + ")" : "")}"))
                  + $" => {(d.Outcome == FloorOutcome.Speak ? d.SpeakerId + (d.CutOffId != null ? " cuts off " + d.CutOffId : "") : d.Outcome.ToString())}");
               foreach (Character c in sc.Cast.Where(c => InnerOn && states[c.Name].Shift != "none" && !away.Contains(c.Name)))
                  debug.AppendLine($"  - {c.Name} changes: {states[c.Name].Shift} (now {states[c.Name].Feeling}; wants: {states[c.Name].Aim})");
               cutOff = d.CutOffId;

               // A reaction shows when the referee lets it AND it is news: the first this beat, or the character just changed inside.
               // Run 2 showed Kael's jealousy nine times ("jealous", "jealous fury"...): the label varied, the feeling did not.
               List<string> leaks = !InnerOn ? new List<string>() : sc.Cast.Where(c => states[c.Name].Leak && c.Name != d.SpeakerId && !away.Contains(c.Name)).Select(c => c.Name).ToList();
               List<string> shown = d.ReactorIds.Where(id => id != d.SpeakerId)
                  .Where(id => !InnerOn || (!states[id].Visible.StartsWith("nothing") && (!shownThisBeat.ContainsKey(id) || states[id].Shift != "none")))
                  .Concat(leaks).Distinct().Take(2).ToList();
               foreach (string id in leaks) states[id].Leak = false;
               Task<(string Text, string Model)> narrating = null;
               if (shown.Count > 0)
               {
                  string cues = string.Join("\n", shown.Select(id => $"- {id}: reacts with {bids.FirstOrDefault(b => b.Id == id)?.Reaction ?? "a small trace of " + states[id].Feeling}"
                                                                     + (InnerOn ? $"; beneath it {states[id].Feeling}; what {id} lets show: {states[id].Showing}" : "")));
                  foreach (string id in shown) shownThisBeat[id] = bids.FirstOrDefault(b => b.Id == id)?.Reaction ?? states[id].Feeling;
                  narrating = Write(m, NarrationPrompt(sc, room, scene, cues), 1500, calls, cost);
               }

               // The next line is written while the reaction is: in the game both reach the screen together.
               Character speaker = d.Outcome == FloorOutcome.Speak ? sc.Cast.First(c => c.Name == d.SpeakerId) : null;
               Task<(string Text, string Model)> speaking = speaker == null ? null : Write(m, LinePrompt(sc, speaker, room, scene, d.CutOffId, InnerOn ? states[speaker.Name] : null), 1500, calls, cost);

               if (narrating != null)
               {
                  string first = ((await narrating).Text ?? "").Replace("\r", "").Split('\n').Select(Clean).FirstOrDefault(l => l.Length > 0) ?? "";
                  // A reaction that only copies a line already in the scene is dropped (the protocol: the banner's arrival appeared three
                  // times, the reaction writer had echoed the event).
                  bool echo = scene.Any(l => string.Equals(l.Text.Trim('*', ' '), first.Trim('*', ' '), StringComparison.OrdinalIgnoreCase));
                  if (first.Length > 0 && !echo) scene.Add(new SceneLine {Text = first, IsNarration = true});
               }

               if (speaking == null) break;

               (string reply, string usedModel) = await speaking;
               // The honesty check (the morning runs of 27/11/2026: Tomas invented a practice burr to explain his clean sword, Seraphine
               // called her inflated price "a test"; both are lies, which the mod forbids): one cheap reading, one redo when it fails.
               if (HonestyOn) (reply, usedModel) = await Keep(m, sc, speaker, InnerOn ? states[speaker.Name] : null, room, scene, d.CutOffId, reply, usedModel, cost, calls, debug);
               waits.Add(wait.Elapsed.TotalSeconds);
               (string to2, string text2) = ParseLine(reply);
               var line = new SceneLine {Speaker = speaker.Name, To = to2, Text = text2, Model = usedModel};
               // The interpreter does not decide who speaks next: it reads the line in the background.
               interpreting.Add((line, Interpret(m, sc, speaker, scene.ToList(), text2, cost, calls)));
               scene.Add(line);
               if (speaker.Name != null) states[speaker.Name].Repeating = "";
               Movements(reply, away, sc, debug);
               npcLines++;
            }
         }

         foreach ((SceneLine line, Task<string> task) in interpreting) line.Actions = await task;
         clock.Stop();
         debug.AppendLine("\n**Who each character became**");
         foreach (Character c in sc.Cast)
         {
            InnerState s = states[c.Name];
            debug.AppendLine($"- {c.Name}: came wanting \"{s.FirstAim}\"; leaves feeling {s.Feeling}, wanting \"{s.Aim}\", hiding \"{s.Hiding}\".{(s.Bond.Length > 0 ? " Bond: " + s.Bond + "." : "")}");
            foreach (string shift in s.Shifts) debug.AppendLine($"  - {shift}");
            foreach (string lasting in s.Lasting) debug.AppendLine($"  - FOR MEMORY (lasting): {lasting}");
         }

         if (Fallbacks.Count > 0) debug.AppendLine("\n**Why lines went to the NSFW model**\n" + string.Join("\n", Fallbacks.Select(f => "- " + f)));
         Fallbacks.Clear();
         string timing = waits.Count == 0 ? "" : $" Wait before each NPC line: mean {waits.Average():0.0} s, longest {waits.Max():0.0} s.";
         string transcript = Render(sc, scene, m, calls, cost, clock.Elapsed, timing, debug.ToString());
         string path = Path.Combine(Path.GetTempPath(), $"cr_group_{sc.Id}_{DateTime.Now:yyyyMMdd_HHmmss}.md");
         File.WriteAllText(path, transcript);
         TestContext.Out.WriteLine($"[GROUP SIM] written to {path}");
         TestContext.Out.WriteLine(transcript);
      }

      // The prose model first; the NSFW model only when it refuses, by the chat's own Enhanced rule.
      private static async Task<(string Text, string Model)> Write(Models m, (string System, string User) prompt, int maxTokens, Dictionary<string, int> calls, double[] cost)
      {
         var request = new LlmRequest {
            SystemPrompt = prompt.System, Messages = new[] {new LlmMessage(MessageRole.User, prompt.User)},
            Parameters = new LlmParameters {MaxTokens = maxTokens, Creativity = 0.85f}
         };
         LlmResponse r = await m.Prose.CompleteAsync(request);
         lock (cost) { cost[1] += r.Usage?.Cost ?? 0; calls["prose"]++; }
         string text = r.IsSuccess ? r.Content?.Trim() ?? "" : "";
         bool refused = !r.IsSuccess || string.IsNullOrWhiteSpace(text)
                        || (DialogueHeuristics.LooksLikeRefusal(text)
                            && RefusalFallbackPolicy.ShouldRedo(new RefusalRedoFacts {EnhancedMode = true, BeatModel = m.ProseId, NsfwModel = m.NsfwId, Reply = text, FinishReason = r.FinishReason}));
         if (!refused) return (text, m.ProseId);

         string why = Clean(!r.IsSuccess ? "error: " + r.ErrorMessage : string.IsNullOrWhiteSpace(text) ? $"empty (finish {r.FinishReason})" : "refusal: " + (text.Length > 120 ? text.Substring(0, 120) : text));
         lock (cost) Fallbacks.Add(why);
         LlmResponse n = await m.Nsfw.CompleteAsync(request);
         lock (cost) { cost[1] += n.Usage?.Cost ?? 0; calls["nsfw"]++; }

         return (n.IsSuccess ? n.Content?.Trim() ?? "" : $"(both models failed: {r.ErrorMessage ?? "refused"} / {n.ErrorMessage})", m.NsfwId);
      }

      // The chat's own interpreter prompt and parser, on the line just written.
      private static async Task<string> Interpret(Models m, Scenario sc, Character speaker, List<SceneLine> scene, string text, double[] cost, Dictionary<string, int> calls)
      {
         var profile = new NpcProfile {Id = "sim_" + speaker.Name.ToLowerInvariant(), Name = speaker.Name, Faction = "the realm", Clan = "", IsFemale = speaker.Presence.IsFemale};
         string facts = ActionInterpreterContextBuilder.Project(new EncounterContext(), profile, sc.Player.Name, true)
                        + "\n\nCONVERSATION SO FAR (last lines):\n" + string.Join("\n", scene.Where(l => !l.IsNarration).TakeLast(4).Select(l => $"{l.Speaker}: {l.Text}"));
         string prompt = ActionInterpreterPromptBuilder.Build(text, facts, LeanPromptLevel.Full);
         LlmResponse r = await m.Interpreter.CompleteAsync(new LlmRequest {
            SystemPrompt = prompt, StableSystemPrompt = ActionInterpreterPromptBuilder.StablePrefixFor(LeanPromptLevel.Full),
            Messages = new[] {new LlmMessage(MessageRole.User, ActionInterpreterPromptBuilder.FinalInstruction)},
            Parameters = new LlmParameters {MaxTokens = 900, Creativity = 0.2f}
         });
         lock (cost) { cost[0] += r.Usage?.Cost ?? 0; calls["interpret"]++; }
         if (!r.IsSuccess) return "(interpreter failed)";

         ParsedResponse parsed = new SectionResponseParser().Parse(r.Content ?? "");
         IReadOnlyList<GameAction> actions = parsed.Actions ?? new List<GameAction>();

         return actions.Count == 0 ? "" : string.Join(", ", actions.Select(a => a.Type + (a.Parameters is {Count: > 0} ? "(" + string.Join(", ", a.Parameters.Select(p => p.Key + "=" + p.Value)) + ")" : "")));
      }

      // The scene keeper (the second review of 27/11/2026: "a canonical world state"; "you reduced emotional repetition, it came back
      // as objects and gestures"; Edric "acquired precise knowledge of Gareth", a name the prose had invented). One cheap reading per
      // line: it records what the line made true for everyone present (objects, who holds what, admissions), and for an NPC line it
      // looks for a lie against what the speaker knows, a gesture or image already used, or a contradiction of the record; one redo
      // when it finds one. A sincere mistaken belief is not a lie (that review's distinction; the mod's rule forbids only the lie).
      private static async Task<(string Text, string Model)> Keep(Models m, Scenario sc, Character c, InnerState s, List<FloorJudgePresence> room, List<SceneLine> scene,
                                                                  string cutOff, string reply, string model, double[] cost, Dictionary<string, int> calls, StringBuilder debug)
      {
         (string problem, string detail, List<string> facts) = await Inspect(m, sc, c, s, scene, reply, cost, calls);
         // The protagonist is checked too: in the game a person writes those lines, and in the protocol of 27/11/2026 most of the
         // worst flaws the blind grader named were the model-played commander's (a lie to the boy, a sword drawn that Ilsa held).
         if (problem == "none")
         {
            lock (sc.Record) sc.Record.AddRange(facts);
            return (reply, model);
         }

         debug.AppendLine($"  - keeper: {c.Name}'s line, {problem}: {detail}; written again");
         string why = problem == "lie" ? $"said something you know is false: \"{detail}\". Do not say it: evade, deflect, go silent, or tell the truth."
                    : problem == "repeat" ? $"repeated a gesture, an image or a demand already made in this scene ({detail}). Do something different."
                    : $"contradicted what is established ({detail}). Keep to what is established.";
         (string System, string User) prompt = LinePrompt(sc, c, room, scene, cutOff, s);
         (string text2, string model2) = await Write(m, (prompt.System + "\nYour first draft " + why, prompt.User), 1500, calls, cost);
         (_, _, List<string> facts2) = await Inspect(m, sc, c, s, scene, text2, cost, calls);
         lock (sc.Record) sc.Record.AddRange(facts2);

         return (text2, model2);
      }

      private static async Task<(string Problem, string Detail, List<string> Facts)> Inspect(Models m, Scenario sc, Character c, InnerState s, List<SceneLine> scene, string reply,
                                                                                            double[] cost, Dictionary<string, int> calls)
      {
         string system = "You keep the record of a scene. You never write the story. A NEW LINE has just been written. Answer exactly four lines, in this order;"
                       // A review of 27/11/2026: the keeper wrote "problem: lie" first, then reasoned "this is not shown false... no established
                       // fact contradicts it", and the line was rewritten all the same. The verdict now comes last, after the reasoning.
                       + " decide the problem LAST, after weighing the detail:"
                       + "\ndetail: <the false statement, the repeated gesture or image, or the record it contradicts, with your reasoning; or none>"
                       + "\nfacts: <what this line makes true for everyone present: objects moved, who holds what, what was shown, admitted or revealed; separated by ' | '; or none>"
                       + $"\nrevealed: <yes | no> (does this line tell those present the secret in WHAT {c.Name.ToUpperInvariant()} KNOWS, in whole or in part?)"
                       + "\nproblem: <none | lie | repeat | contradiction> (your final verdict on the detail above; if your reasoning cleared the line, none)"
                       + $"\nA LIE: a statement of fact that WHAT {c.Name.ToUpperInvariant()} KNOWS shows to be false, or an excuse {c.Name} invents and knows is untrue."
                       // The third review of 27/11/2026: the keeper read the absence of proof as a lie ("Tomas cannot have seen Berold fight,
                       // since he fled") and had Tomas rewrite that his sword's notches were old, which the scenario makes true.
                       + " Not a lie: a claim not yet established in the record, something they may well have seen, an inference, a sincere belief, an"
                       + " evasion, a silence, a literally true half-answer, an opinion, a demand. Two facts that can both be true are no lie (he can"
                       + " have seen the fight before he fled). When unsure, it is not a lie."
                       + $"\nA REPEAT: a gesture, a way of handling an object, or an image that {c.Name} already used in this scene, or an image someone else already used,"
                       + " or an order or demand someone else already made in this scene and that this line only makes again (a second voice sending the same person away)."
                       + " Answering a question again is not a repeat."
                       + "\nA CONTRADICTION: the line conflicts with the record (an object that is elsewhere, a fact established otherwise)."
                       + "\nRecord a claim as a claim ('Tomas says he held the left'), never as a fact. Record only what everyone present could see or hear.";
         string user = $"WHAT {c.Name.ToUpperInvariant()} KNOWS: {c.Persona} {c.Secret}" + (s == null ? "" : $" Hidden right now: {s.Hiding}.")
                     + $"\n\nTHE RECORD (established in this scene):\n{RecordText(sc)}\n\nTHE SCENE SO FAR:\n{SceneText(scene)}\n\nTHE NEW LINE ({c.Name}):\n{reply}";
         LlmResponse r = await m.Judge.CompleteAsync(new LlmRequest {
            SystemPrompt = system, Messages = new[] {new LlmMessage(MessageRole.User, user)},
            // The verdict now comes after the reasoning, so the reply is longer: cut short, it would lose the verdict and pass the line.
            Parameters = new LlmParameters {MaxTokens = 600, Creativity = 0.1f, AllowTruncationRetry = false}
         });
         lock (cost) { cost[0] += r.Usage?.Cost ?? 0; calls["keeper"]++; }
         string content = r.Content ?? "";
         string Line(string name) => Clean(Regex.Match(content, $@"(?im)^\s*{name}\s*:\s*(.+)$").Groups[1].Value);
         string problem = Line("problem").ToLowerInvariant();
         if (problem.Length == 0) lock (sc.Record) Fallbacks.Add($"keeper gave no verdict on {c.Name}'s line (finish {r.FinishReason})");
         problem = problem.StartsWith("lie") ? "lie" : problem.StartsWith("repeat") ? "repeat" : problem.StartsWith("contradiction") ? "contradiction" : "none";
         List<string> facts = Line("facts").Split('|').Select(f => f.Trim()).Where(f => f.Length > 0 && !f.StartsWith("none", StringComparison.OrdinalIgnoreCase)).ToList();

         // A review of 27/11/2026: Rhian told Corvan of the marriage at beat 5, and at beats 10 to 12 the dramaturge still read "You have
         // not told Corvan" in her sheet. A secret told is marked as told wherever the sheet is read; the sheet's own words stay.
         if (problem == "none" && Line("revealed").ToLowerInvariant().StartsWith("yes") && !string.IsNullOrWhiteSpace(c.Secret) && !c.Secret.Contains(TOLD))
            c.Secret += $" ({c.Name}{TOLD})";

         return (problem, Line("detail"), facts);
      }

      private const string TOLD = " has now told this to everyone present in the scene; they all know it";

      private static string RecordText(Scenario sc)
      {
         lock (sc.Record) return sc.Record.Count == 0 ? "(nothing yet)" : string.Join("\n", sc.Record.TakeLast(25).Select(f => "- " + f));
      }

      // The inner life of one character after the last line. People change, but only for a cause in the scene.
      private static async Task UpdateMind(Models m, Scenario sc, Character c, InnerState s, List<SceneLine> scene, int beat, string news, double[] cost, Dictionary<string, int> calls)
      {
         string system = $"You keep the inner life of {c.Name}, a character in a scene. You never write dialogue. After the last line of the scene, say how {c.Name} stands NOW."
                       + " People change: a revelation, a slight, a kindness, a defeat, a proof can turn a feeling, change a tactic, even make them give up what they wanted"
                       + " or want something new. But nothing changes without a cause in the scene; a line that does not touch them changes little, and 'none' is the usual shift."
                       + " A tactic that has failed twice is abandoned for another. A tactic never includes a lie or an invented excuse: when they will"
                       + " not tell the truth they evade, deflect, stay silent or confess."
                       + $"\n\n{c.Name}: {c.Persona}"
                       + (string.IsNullOrWhiteSpace(c.Secret) ? "" : $"\nKnown only to {c.Name}: {c.Secret}"
                          + $"\nA secret is the last thing {c.Name} gives up. Before giving it up they try at least two different ways of keeping it (evade, deflect,"
                          + " a true half-answer, silence, turning the question back, an offer that hides it). They give it up only when proof is established in the"
                          + " record, or when those ways have failed under repeated pressure; a single question, however painful, is not enough. Once pressed four times"
                          + " or more with those ways spent, holding it costs more than telling, and they may give it up. When the ways run out, the choice is"
                          + " silence or the truth, never a false account. Giving it up is an"
                          + " 'aim' change, and it costs them.")
                       + $"\nWanted when the scene began: {c.Objective}\nWants to appear: {c.Mask}\nFears: {c.Fear}\nWill never: {c.Limit}"
                       + $"\n\nAnswer exactly {(BondOn ? "ten" : "nine")} lines:\nfeeling: <their feeling now, 1 to 4 words>"
                       + "\nlevel: <none | fluctuation | reevaluation | tactic | aim | lasting> (fluctuation: a passing mood; reevaluation: they now understand the situation"
                       + " differently; tactic: they change how they go about it; aim: they want something else; lasting: it changes how they see themselves or someone, beyond"
                       + " tonight. Most lines are none or fluctuation.)"
                       + "\nshift: <none, or for reevaluation and above, what changed in them and which line caused it, one short sentence>"
                       + "\naim: <what they want now: the same aim, or the new one if it changed>\ntactic: <how they will try for it next, one short phrase>"
                       + "\nshowing: <what they let the others see, 1 to 6 words>\nhiding: <what they keep hidden now, one short phrase>"
                       + "\nvisible: <nothing | a detail | words> (does anything show outside? someone holding a mask shows nothing; nothing is usual. But a mask"
                       + " hides moods, not blows: news that strikes at their secret or their fear leaks at least a detail, a pause, a hand, a word that comes wrong)"
                       + "\nurge: <none, or low or high followed by why: answer, interest, emotion, duty, morality, opportunity> (how strongly they feel they must speak now: a"
                       + " priest's duty, an injustice they cannot let pass, a threat to what they want; someone bound to silence on one matter may still speak on another)"
                       + (!BondOn ? "" : "\nbond: <toward the person who matters most to them here: Name: understands yes/partly/no, forgives yes/partly/no, loves"
                                          + " yes/partly/no, trusts yes/partly/no> (four different things: one can love without trusting, understand without forgiving,"
                                          + " forgive without forgetting; each moves only for its own cause, and a confession of weakness earns understanding, not trust)");
         string user = $"HOW {c.Name.ToUpperInvariant()} STOOD BEFORE THE LAST LINE:\nfeeling: {s.Feeling}\naim: {s.Aim}\ntactic: {s.Tactic}\nshowing: {s.Showing}\nhiding: {s.Hiding}"
                     + (string.IsNullOrWhiteSpace(c.Secret) ? "" : $"\nPressed so far (addressed or named): {s.Pressed} times. Ways of keeping the secret already tried: "
                                                                     + (s.Tried.Count == 0 ? "none" : string.Join("; ", s.Tried)))
                     + $"\n\nTHE RECORD (established in this scene, true for everyone):\n{RecordText(sc)}\n\nTHE SCENE SO FAR:\n{SceneText(scene)}"
                     + (string.IsNullOrWhiteSpace(news) ? "" : $"\n\nJUST HAPPENED (an event, new since the last line): {news}\nSay what {c.Name} makes of it. Only news that"
                                                              + $" touches {c.Name}'s own secret or what they want changes them; the rest is 'none' or a passing mood.")
                     + (BondOn && s.Bond.Length > 0 ? $"\n\nTheir bond before the last line: {s.Bond}" : "")
                     + $"\n\nHow {c.Name} stands now:";
         LlmResponse r = await m.Judge.CompleteAsync(new LlmRequest {
            SystemPrompt = system, Messages = new[] {new LlmMessage(MessageRole.User, user)},
            Parameters = new LlmParameters {MaxTokens = 300, Creativity = 0.4f, AllowTruncationRetry = false}
         });
         lock (cost) { cost[0] += r.Usage?.Cost ?? 0; calls["mind"]++; }
         if (!r.IsSuccess) return;

         string Field(string name, string old)
         {
            Match f = Regex.Match(r.Content ?? "", $@"(?im)^\s*{name}\s*:\s*(.+)$");
            return f.Success && f.Groups[1].Value.Trim().Length > 0 ? Clean(f.Groups[1].Value) : old;
         }

         string tacticBefore = s.Tactic;
         s.Feeling = Field("feeling", s.Feeling);
         s.Level = Field("level", "none").ToLowerInvariant();
         s.Visible = Field("visible", "nothing").ToLowerInvariant();
         s.Urge = Field("urge", "none").ToLowerInvariant();
         s.Shift = Field("shift", "none");
         // Only a change of understanding, tactic, aim or self counts; a passing mood is not a change (the second review).
         bool counts = s.Level.StartsWith("reevaluation") || s.Level.StartsWith("tactic") || s.Level.StartsWith("aim") || s.Level.StartsWith("lasting");
         if (!counts || s.Shift.StartsWith("none", StringComparison.OrdinalIgnoreCase)) s.Shift = "none";
         if (s.Shift != "none" && s.Level.StartsWith("lasting")) s.Lasting.Add(s.Shift);
         s.Aim = Field("aim", s.Aim);
         s.Tactic = Field("tactic", s.Tactic);
         s.Showing = Field("showing", s.Showing);
         s.Hiding = Field("hiding", s.Hiding);
         if (BondOn) s.Bond = Field("bond", s.Bond);
         if (s.Shift != "none") s.Shifts.Add($"beat {beat + 1} ({s.Level}): {s.Shift}");
         if (s.Tactic != tacticBefore && !s.Tried.Contains(tacticBefore)) s.Tried.Add(tacticBefore);
         // A blow shows at least a detail, whatever the mask (the leak is shown once, by the reaction writer): news read alone, or news
         // that changed what they want. Leaking every change an event caused doubled the narrated gestures (5 to 11 a scene) and the
         // grader's repetition fell from 5.9 to 4.9.
         bool blow = news.Contains("you alone read:") || s.Level.StartsWith("aim") || s.Level.StartsWith("lasting");
         s.Leak = !string.IsNullOrWhiteSpace(news) && s.Shift != "none" && blow;
         if (s.Leak && s.Visible.StartsWith("nothing")) s.Visible = "a detail";
      }

      // The dramaturge (the review of 27/11/2026: "you have actors, a referee and an observer; you lack a dramaturge"). It never
      // writes dialogue. It reads the scene and everyone's hidden inner life, names who repeats, and acts on the circumstances only
      // when the scene has stopped turning, with a fact the scenario holds (the game's facts, never invented).
      private static async Task<(string Complication, bool End)> Dramaturge(Models m, Scenario sc, List<SceneLine> scene, Dictionary<string, InnerState> states, HashSet<int> used,
                                                                            int beat, double[] cost, Dictionary<string, int> calls, StringBuilder debug)
      {
         // Event 3 of the intimate scene was asked for five times after beat 6 and refused each time, unseen by the dramaturge.
         var options = sc.Complications.Select((c, i) => (c, i)).Where(x => !used.Contains(x.i) && beat + 1 <= sc.EventsUntil).ToList();
         string system = "You are the dramaturge of a scene in progress. You never write dialogue and never speak for anyone. You read the scene and the hidden inner life"
                       + " of every character, and decide whether the circumstances must change. A scene is alive when what the characters know, want or risk is shifting."
                       + " Emotion repeated is not tension; a calm line that changes who holds power is. Do nothing when the characters are already turning the scene."
                       + "\nAnswer exactly these four lines:"
                       + "\nprogress: <what changed in the situation (knowledge, power, aims) since the beat before, or nothing>"
                       + "\nrepeating: <Name: what they keep doing that no longer works, for each one who repeats, separated by ' | '; or none>"
                       + "\nuntapped: <the strongest conflict or secret not yet in play, one phrase>"
                       + "\nmove: <none | event N | end>"
                       + "\nChoose 'event N' only when progress is nothing or someone repeats, and pick the event that forces someone to DECIDE something."
                       // The second review: all three events of the best drama pointed at Tomas's confession; "a good dramaturge for an
                       // emergent world creates dilemmas, not solutions".
                       + " Prefer an event that opens more than one choice, that could help or hurt more than one person, over one that points to a single answer."
                       + " Choose 'end' only when the scene's question has been answered and any further line would only echo it.";
         string user = $"SCENE: {sc.Title}. {sc.Setting}\n\n{SceneText(scene)}\n\nINNER LIVES (hidden from the others):\n"
                     + string.Join("\n", sc.Cast.Select(c => $"- {c.Name}: feels {states[c.Name].Feeling}; wants {states[c.Name].Aim}; shows {states[c.Name].Showing}; hides {states[c.Name].Hiding}"
                                                            + (string.IsNullOrWhiteSpace(c.Secret) ? "" : $"; secret: {c.Secret}")))
                     + $"\n- {sc.Player.Name} (the protagonist): wants {sc.Player.Objective}" + (string.IsNullOrWhiteSpace(sc.Player.Secret) ? "" : $"; secret: {sc.Player.Secret}")
                     + $"\n\nTHE RECORD (established in this scene):\n{RecordText(sc)}"
                     + "\n\nEVENTS THE WORLD CAN BRING (already true, waiting to surface):\n"
                     + (options.Count == 0 ? "(none left)" : string.Join("\n", options.Select(o => $"{o.i + 1}. {EventText(o.c)}")))
                     + $"\n\nThis is the end of beat {beat} of at most {sc.Beats}.";
         LlmResponse r = await m.Dramaturge.CompleteAsync(new LlmRequest {
            SystemPrompt = system, Messages = new[] {new LlmMessage(MessageRole.User, user)},
            Parameters = new LlmParameters {MaxTokens = 1500, Creativity = 0.3f, AllowTruncationRetry = false}
         });
         lock (cost) { cost[1] += r.Usage?.Cost ?? 0; calls["director"]++; }
         string reply = r.Content ?? "";
         string Line(string name) => Clean(Regex.Match(reply, $@"(?im)^\s*{name}\s*:\s*(.+)$").Groups[1].Value);

         string repeating = Line("repeating");
         foreach (string part in repeating.Split('|'))
         {
            int colon = part.IndexOf(':');
            if (colon <= 0) continue;
            string who = part.Substring(0, colon).Trim();
            if (states.TryGetValue(who, out InnerState s)) s.Repeating = Clean(part.Substring(colon + 1));
         }

         string move = Line("move").ToLowerInvariant();
         Match pick = Regex.Match(move, @"event\s*(\d+)");
         int index = pick.Success ? int.Parse(pick.Groups[1].Value) - 1 : -1;
         bool wants = index >= 0 && index < sc.Complications.Count && !used.Contains(index) && beat >= 2 && beat + 1 <= sc.EventsUntil;
         // Never before the third beat: a scene needs room to set its question before it can answer it.
         bool end = move.StartsWith("end") && beat >= sc.EndFrom;
         debug.AppendLine($"- dramaturge: progress: {Line("progress")} | repeating: {repeating} | untapped: {Line("untapped")} | move: {move}{(end ? " (scene ends)" : "")}");
         if (!wants || end) return (null, end);

         used.Add(index);

         return (sc.Complications[index], false);
      }

      // The old scene reader (cheap judge model, three lines), kept for the ablation run CR_SIM_DRAMATURGE=off.
      private static async Task<string> Director(Models m, Scenario sc, List<SceneLine> scene, HashSet<int> used, double[] cost, Dictionary<string, int> calls, StringBuilder debug)
      {
         var options = sc.Complications.Select((c, i) => (c, i)).Where(x => !used.Contains(x.i)).ToList();
         string system = "You read a scene of a story in progress, as an editor. You never write the story. Say whether the scene is still gripping:"
                       + " is the dramatic question moving forward, is there fresh conflict or revelation, or are the characters repeating themselves?"
                       + "\nAnswer with exactly three lines:\ntension: <rising|steady|flat>\nstuck: <yes|no>\ncomplication: <the number of the one complication that would turn this scene best right now, or none>"
                       + "\nPick a complication only when the scene is stuck or flat.";
         string user = $"SCENE: {sc.Title}. {sc.Setting}\n\n" + SceneText(scene) + "\n\nCOMPLICATIONS AVAILABLE:\n"
                     + (options.Count == 0 ? "(none left)" : string.Join("\n", options.Select(o => $"{o.i + 1}. {EventText(o.c)}")));
         LlmResponse r = await m.Judge.CompleteAsync(new LlmRequest {
            SystemPrompt = system, Messages = new[] {new LlmMessage(MessageRole.User, user)},
            Parameters = new LlmParameters {MaxTokens = 40, Creativity = 0.2f, AllowTruncationRetry = false}
         });
         lock (cost) { cost[0] += r.Usage?.Cost ?? 0; calls["director"]++; }
         string reply = (r.Content ?? "").ToLowerInvariant();
         string tension = Regex.Match(reply, @"tension:\s*(\w+)").Groups[1].Value;
         string stuck = Regex.Match(reply, @"stuck:\s*(\w+)").Groups[1].Value;
         Match pick = Regex.Match(reply, @"complication:\s*(\d+)");
         int index = pick.Success ? int.Parse(pick.Groups[1].Value) - 1 : -1;
         bool wants = (stuck == "yes" || tension == "flat") && index >= 0 && index < sc.Complications.Count && !used.Contains(index);
         debug.AppendLine($"- scene reader: tension {tension}, stuck {stuck}{(wants ? $", complication {index + 1}" : "")}");
         if (!wants) return null;

         used.Add(index);

         return sc.Complications[index];
      }

      #region private

      private static string EventText(string complication)
      {
         string[] parts = complication.Split('|');
         return parts.Length == 3 ? $"{parts[0]} (only {parts[1]} sees: {parts[2]})" : parts[0];
      }

      private static List<FloorJudgeLine> JudgeScene(List<SceneLine> scene)
         => scene.Where(l => !l.IsNarration).Select(l => new FloorJudgeLine {Speaker = l.Speaker, To = l.To, Text = l.Text}).ToList();

      private static int TurnsSilent(List<SceneLine> scene, string name)
      {
         var n = 0;
         for (int i = scene.Count - 1; i >= 0; i--)
         {
            if (scene[i].IsNarration) continue;
            if (scene[i].Speaker == name) return n;
            n++;
         }

         return n;
      }

      private static string SceneText(List<SceneLine> scene)
         => string.Join("\n", scene.Where(l => l.Only == null).Select(l => l.IsNarration ? l.Text : $"{l.Speaker}{(string.IsNullOrWhiteSpace(l.To) ? "" : " (to " + l.To + ")")}: {l.Text}"));

      private static (string System, string User) LinePrompt(Scenario sc, Character me, List<FloorJudgePresence> room, List<SceneLine> scene, string cutOff, InnerState now)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"You are {me.Name}. {me.Persona}");
         sb.AppendLine($"Where you are: {sc.Setting}");
         if (!string.IsNullOrWhiteSpace(me.Secret)) sb.AppendLine($"Known only to you: {me.Secret}");
         sb.AppendLine("Who is here: " + string.Join("; ", room.Select(p => $"{p.Name} ({(p.IsFemale ? "a woman" : "a man")}, {p.Station})")));
         lock (sc.Record)
            if (sc.Record.Count > 0)
               sb.AppendLine("What is established in this scene, true for everyone (keep to it; objects are where it says):\n" + RecordText(sc));
         foreach (string tie in me.Ties) sb.AppendLine("- " + tie);
         if (now == null)
         {
            sb.AppendLine($"What you want in this scene: {me.Objective}");
            sb.AppendLine($"If you are blocked: {me.Tactic}");
         }
         else
         {
            // The review of 27/11/2026: "the model respects his desire, but not his strategy", "the characters express their
            // intentions rather than their personality". The line is written from where the character stands NOW.
            sb.AppendLine("Where you stand now:");
            sb.AppendLine($"- you feel: {now.Feeling}");
            if (now.Shift != "none") sb.AppendLine($"- what just changed in you: {now.Shift}");
            sb.AppendLine($"- what you want now: {now.Aim}");
            sb.AppendLine($"- how you go about it now: {now.Tactic}");
            sb.AppendLine($"- what you let the others see: {now.Showing}");
            sb.AppendLine($"- what you keep hidden: {now.Hiding}");
            if (BondOn && now.Bond.Length > 0) sb.AppendLine($"- where you stand with them: {now.Bond}");
            if (!string.IsNullOrWhiteSpace(now.Repeating)) sb.AppendLine($"- what is no longer working: {now.Repeating}. Change your approach.");
            if (!now.Urge.StartsWith("none")) sb.AppendLine($"- your pull to speak: {now.Urge}");
         }
         if (!string.IsNullOrWhiteSpace(me.Mask)) sb.AppendLine($"How you want to be seen: {me.Mask}. You fear: {me.Fear}. You will never: {me.Limit}.");
         if (!string.IsNullOrWhiteSpace(me.Voice)) sb.AppendLine($"Your voice, yours alone: {me.Voice}.");
         sb.AppendLine($"The tone of the scene: {sc.Tone}.");
         if (sc.Adult)
         {
            sb.AppendLine("Adult content is allowed: everyone here is a consenting adult; desire, seduction and explicit intimacy may be written frankly when the moment earns it.");
            // Gabriel, 27/11/2026: the intimate scene stayed at the threshold for four beats, with DeepSeek and with Grok alike, so the
            // restraint is ours (say less than you feel). A general rule, as it would be in the game's prompt, not a scene's.
            sb.AppendLine("Once both have clearly chosen each other, subtext yields to the body: move into the physical and show it, with the same");
            sb.AppendLine("care for the specific detail as the words. Do not stop at the threshold, and do not ask again what has been answered.");
         }
         sb.AppendLine();
         sb.AppendLine("Write ONLY your next line in this group conversation, in character: one to three sentences of speech, an *action* in");
         sb.AppendLine("asterisks allowed, always in the third person (*She sets down the cup*, never *I*). Speak only for yourself. You never lie outright: when you will");
         sb.AppendLine("not tell the truth, evade, deflect or fall silent. Make the line EARN its place: reveal something, push for what you want,");
         sb.AppendLine("change your approach, or turn the moment. Never repeat what you already said or demanded. Answer the moment as it stands:");
         sb.AppendLine("you may turn to anyone present, not only the last speaker. Keep your own voice.");
         // The protocol of 27/11/2026: "three adults all telling the boy to go to bed". A demand someone else already made is not
         // made again by a second voice.
         sb.AppendLine("If someone else already asked or ordered what you would (send someone away, demand an oath, ask the same question),");
         sb.AppendLine("do not echo it: back it in a word, oppose it, or do something else entirely.");
         // Subtext: a novel's characters say less than they feel. Never by lying (the mod's rule): by omission, deflection,
         // answering another question, a silence, a joke.
         sb.AppendLine("Say less than you feel. Never name your own feeling. Let what you avoid saying show: answer a different question, change");
         sb.AppendLine("the subject, joke, go quiet, or offer something to hide something else. No metaphor unless your voice is made of them, and");
         sb.AppendLine("never borrow an image someone else in the scene already used. Never invent an excuse or a fact you know is false: an");
         sb.AppendLine("evasion is allowed, a false explanation is a lie.");
         if (cutOff != null) sb.AppendLine($"You are cutting in before {cutOff} could answer.");
         // The first three scenes looped (the baron asked to be "crowned peerless" in every line; the commander put everything off
         // "until dawn"): each speaker now sees their own last lines and must not say them again.
         List<string> mine = scene.Where(l => !l.IsNarration && l.Speaker == me.Name).Select(l => l.Text).TakeLast(3).ToList();
         if (mine.Count > 0)
         {
            // Ilsa laid the sword across her knees four or five times in one scene (the blind review of 27/11/2026).
            sb.AppendLine("What you have already said and done in this scene (do NOT say it again, not even in other words, and do not repeat a gesture or");
            sb.AppendLine("handle the same object the same way; if it did not work, try something new):");
            foreach (string said in mine) sb.AppendLine("- " + (said.Length > 220 ? said.Substring(0, 220) + "..." : said));
         }
         if (me == sc.Player)
            sb.AppendLine("You are the protagonist: drive the scene. Make decisions, choose a side, act, reveal what you know. Never stall or postpone.");
         sb.AppendLine("Format: first line `to: <a name, or everyone>` alone, then your line on the next lines. Address one person per line.");
         sb.AppendLine($"The scene follows {sc.Player.Name}: if during your line someone goes where {sc.Player.Name} is not, or stays behind while");
         sb.AppendLine($"{sc.Player.Name} moves on, add a last line `leaves: <their names>`; if someone comes back, `returns: <names>`.");
         sb.AppendLine($"People who move together with {sc.Player.Name} have not left.");

         return (sb.ToString(), "THE SCENE SO FAR:\n" + SceneText(scene) + $"\n\nYour line, {me.Name}:");
      }

      private static (string System, string User) NarrationPrompt(Scenario sc, List<FloorJudgePresence> room, List<SceneLine> scene, string cues)
      {
         string system = $"You write ONE short line of narration for a scene ({sc.Tone}), present tense, third person, in asterisks, no dialogue. "
                       + "Show each named reaction as a small, specific gesture of that person, different from any gesture already in the scene. "
                       + "Show what the person LETS SHOW, their mask: a feeling they hide leaks only through one small detail, or not at all. "
                       + "A deliberate act (pouring wine, turning away, a smile) often says more than a clenched jaw. "
                       + "Show only what is listed: no other character, no thoughts, no explanation of what the reaction means. "
                       + $"Where things are: {RecordText(sc).Replace("\n", " ")} "
                       + "Who is here (use the right pronouns): " + string.Join("; ", room.Select(p => $"{p.Name}, {(p.IsFemale ? "a woman (she/her)" : "a man (he/him)")}")) + "."
                       + (sc.Adult ? " Everyone is a consenting adult; sensuality may be shown." : "");

         return (system, "THE SCENE SO FAR:\n" + SceneText(scene) + $"\n\nREACTIONS TO SHOW:\n{cues}\nThe narration line:");
      }

      // Who left or came back, from the speaker's own `leaves:` / `returns:` line: an absent character neither speaks nor reacts.
      private static void Movements(string reply, HashSet<string> away, Scenario sc, StringBuilder debug)
      {
         foreach (string raw in (reply ?? "").Replace("\r", "").Split('\n'))
         {
            string line = raw.Trim().Trim('`').Trim();
            bool leaves = line.StartsWith("leaves:", StringComparison.OrdinalIgnoreCase);
            bool returns = line.StartsWith("returns:", StringComparison.OrdinalIgnoreCase);
            if (!leaves && !returns) continue;

            foreach (Character c in sc.Cast.Where(c => line.IndexOf(c.Name, StringComparison.OrdinalIgnoreCase) >= 0))
            {
               if (leaves) away.Add(c.Name);
               else away.Remove(c.Name);
               debug.AppendLine($"- {c.Name} {(leaves ? "leaves the scene" : "comes back")}");
            }
         }
      }

      private static (string To, string Text) ParseLine(string reply)
      {
         string[] lines = (reply ?? "").Replace("\r", "").Split('\n').Where(l => l.Trim().Length > 0)
            .Where(l => !l.Trim().Trim('`').StartsWith("leaves:", StringComparison.OrdinalIgnoreCase) && !l.Trim().Trim('`').StartsWith("returns:", StringComparison.OrdinalIgnoreCase)).ToArray();
         string to = "";
         Match lead = lines.Length > 0 ? Regex.Match(lines[0], @"^[`\s]*to\b\s*:?\s*([^:*""]{1,30}?)\s*:\s*(.*)$", RegexOptions.IgnoreCase) : Match.Empty;
         if (lead.Success)
         {
            to = lead.Groups[1].Value.Trim(' ', '`', '<', '>');
            string rest = lead.Groups[2].Value.Trim();
            lines = (rest.Length > 0 ? new[] {rest} : Array.Empty<string>()).Concat(lines.Skip(1)).ToArray();
         }
         else if (lines.Length > 0 && lines[0].TrimStart('`', ' ').StartsWith("to:", StringComparison.OrdinalIgnoreCase))
         {
            // The name is the first word after "to:"; anything after it on that line (a model that ran on) belongs to the line.
            string head = lines[0].TrimStart('`', ' ').Substring(3).Trim(' ', '`', '<', '>');
            // "to: the boy" alone on its line is the whole name; a name followed by the line itself ends at the first space.
            bool alone = !head.Contains('*') && !head.Contains('"') && head.Split(' ').Length <= 3;
            int cut = alone ? -1 : head.IndexOfAny(new[] {' ', '*', '"', '\t'});
            to = (cut < 0 ? head : head.Substring(0, cut)).Trim(',', '.', ':');
            string rest = cut < 0 ? "" : head.Substring(cut).Trim();
            int comma = head.IndexOf(',');
            int act = head.IndexOfAny(new[] {'*', '"'});
            if (comma > 0 && (act < 0 || comma < act))
            {
               to = head.Substring(0, comma).Trim();
               rest = act < 0 ? "" : head.Substring(act).Trim();
            }
            lines = (rest.Length > 0 ? new[] {rest} : Array.Empty<string>()).Concat(lines.Skip(1)).ToArray();
         }
         else if (lines.Length > 0 && Regex.Match(lines[0], @"^[`\s]*to\s+([A-Za-z][A-Za-z ]{0,25}?)\s+([*""].*)$", RegexOptions.IgnoreCase) is {Success: true} bare)
         {
            // "to everyone *She turns toward the stair*" (the protocol of 27/11/2026): no colon, the action follows the name.
            to = bare.Groups[1].Value.Trim();
            lines = new[] {bare.Groups[2].Value.Trim()}.Concat(lines.Skip(1)).ToArray();
         }

         // A second "to: Name" in mid-line (one line addressing two people) is dropped as a marker; its words stay.
         string text = Regex.Replace(string.Join(" ", lines), @"\s*\bto:\s*[A-Z][a-z]+\s*", " ");

         return (to, Clean(text));
      }

      // Models write em dashes; the repositories refuse them (git hooks), and a transcript may be kept as a design record.
      private static string Clean(string s) => (s ?? "").Replace("\u2014", ", ").Replace("\u2013", "-").Trim().Trim('`').Trim();

      private static string Render(Scenario sc, List<SceneLine> scene, Models m, Dictionary<string, int> calls, double[] cost, TimeSpan took, string timing, string debug)
      {
         var sb = new StringBuilder();
         sb.AppendLine($"# {sc.Title} ({sc.Id}), simulated");
         sb.AppendLine($"Inner lives {(InnerOn ? "on" : "off")}, dramaturge {(DramaturgeOn ? "on" : "off (old scene reader)")}, scene keeper {(HonestyOn ? $"on ({calls["keeper"]} readings)" : "off")}.");
         sb.AppendLine($"Prose {m.ProseId} reasoning {m.ProseReasoning} ({calls["prose"]} calls), NSFW fallback {m.NsfwId} ({calls["nsfw"]} calls), dramaturge on the prose model ({calls["director"]}), "
                     + $"judge {m.JudgeId} ({calls["judge"]}), inner lives ({calls["mind"]}), interpreter {m.InterpreterId} ({calls["interpret"]}). "
                     + $"Cost: prose+dramaturge ${cost[1]:0.0000}, judge+minds+interpreter ${cost[0]:0.0000}. {took.TotalMinutes:0.0} min.{timing}");
         sb.AppendLine($"{sc.Player.Name} (the player: first line scripted, then played by the prose model). " + string.Join("; ", sc.Cast.Select(c => $"{c.Name}: {c.Objective}")));
         sb.AppendLine();
         sb.AppendLine($"*{sc.Setting}*");
         sb.AppendLine();
         sb.AppendLine("## The scene");
         foreach (SceneLine l in scene)
         {
            if (l.IsNarration) sb.AppendLine($"> {l.Text}");
            else
            {
               string fallback = l.Model == m.NsfwId ? " [NSFW model]" : "";
               sb.AppendLine($"**{l.Speaker}**{(string.IsNullOrWhiteSpace(l.To) ? "" : " *(to " + l.To + ")*")}{fallback}: {l.Text}");
               if (!string.IsNullOrWhiteSpace(l.Actions)) sb.AppendLine($"  `actions: {l.Actions}`");
            }

            sb.AppendLine();
         }

         sb.AppendLine("## Why each turn went as it did (judge -> referee, scene reader)");
         sb.AppendLine(debug);

         return sb.ToString();
      }

      private static JsonElement LoadSettingsOrIgnore(out string key)
      {
         key = "";
         string dir = AppContext.BaseDirectory;
         string file = null;
         for (var i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
         {
            string candidate = Path.Combine(dir, "LLM Roleplay Service", "NpcMemoryService.LiveLlmTests", "livellm.settings.json");
            if (File.Exists(candidate)) { file = candidate; break; }
         }

         if (file == null) Assert.Ignore("No live settings (LLM Roleplay Service/NpcMemoryService.LiveLlmTests/livellm.settings.json).");
         JsonElement root = JsonDocument.Parse(File.ReadAllText(file!), new JsonDocumentOptions {CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true}).RootElement;
         if (!(root.TryGetProperty("enabled", out JsonElement on) && on.ValueKind == JsonValueKind.True)) Assert.Ignore("Live settings are not enabled.");

         key = Str(root, "apiKey", "");
         if (string.IsNullOrWhiteSpace(key)) key = Environment.GetEnvironmentVariable("OPENROUTER_API_KEY") ?? "";
         if (string.IsNullOrWhiteSpace(key)) Assert.Ignore("No OpenRouter key.");

         return root;
      }

      private static string Str(JsonElement root, string name, string fallback)
         => root.TryGetProperty(name, out JsonElement v) && v.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(v.GetString()) ? v.GetString()! : fallback;

      #endregion
   }
}
