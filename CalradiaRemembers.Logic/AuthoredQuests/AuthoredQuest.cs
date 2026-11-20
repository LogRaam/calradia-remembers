// Code written by Gabriel Mailhot, 03/10/2026.
// Authored quests (docs/design/AUTHORED_QUESTS.md; Gabriel, 03/10/2026): multi-stage quests written in JSON, which
// players and modders extend. These types are the file's shape, read as written; the loader validates them and the
// stage machine plays them. Field names follow the file (snake_case).
// 04/10/2026, branching tales (Gabriel: "with Twine you can give a quest several paths ... CR's quests must support
// that"): a stage may offer choices, a tale may end several ways, and simple flags remember the path taken. Every
// field added is optional, so every file written before still reads the same.

#region

using System.Collections.Generic;
using Newtonsoft.Json;

#endregion

namespace CalradiaRemembers.Logic.AuthoredQuests
{
   /// <summary>One authored quest, as its file describes it.</summary>
   public sealed class AuthoredQuest
   {
      [QuestField("Unique across every file. Prefix it with your mod's name.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("Shown in the journal as \"Tale: <title>\".")]
      [JsonProperty("title")] public string Title { get; set; }

      [QuestField("Who wrote it. Shown to nobody in game; for the reader of the file.")]
      [JsonProperty("author")] public string Author { get; set; }

      /// <summary>Played at most once per campaign (Gabriel: unique is better; a repeated quest bores).</summary>
      [QuestField("On (the default): played once per campaign, won or lost. Off: it may come back.")]
      [JsonProperty("unique")] public bool Unique { get; set; } = true;

      [QuestField("For a tale that is not unique: how many days after it ended before it may be offered again.")]
      [JsonProperty("repeat_cooldown_days")] public int RepeatCooldownDays { get; set; }

      [QuestField("When a character could give several tales, they are chosen in proportion to their weights. 0 is never offered. Default 10.")]
      [JsonProperty("weight")] public int Weight { get; set; } = 10;

      /// <summary>
      ///   Schmidt (Discord 18/11/2026) wanted his army in a tale; a scene written for a lone hero (the wolf's forest) comes
      ///   undone if a hundred men walk in. Gabriel (20/11/2026): "aussi party_may_join", a flag per tale, off by default,
      ///   the party coming in the telling only. See ScenePlayerText.
      /// </summary>
      [QuestField("On: in this tale's scenes the player may send for their companions and men. They come in the telling only: they help, nothing that happens to them is real (no one is hurt, killed or lost), and a fight stays the player's own. Off (the default): each scene is the player's alone.")]
      [JsonProperty("party_may_join")] public bool PartyMayJoin { get; set; }

      [QuestField("Who may give the tale, and what it needs of the world and of the player.")]
      [JsonProperty("requires")] public AuthoredRequires Requires { get; set; } = new();

      /// <summary>Gabriel (10/10/2026): a tale that begins by itself on the map, never in a conversation; see AuthoredMapTriggerPolicy.</summary>
      [QuestField("The tale begins by itself on the map, never in a conversation: when the player's party has stood still long enough, by day, in the giver's own lands, on a chance each day. The giver must travel with the player (requires.giver.with_player).")]
      [JsonProperty("on_map")] public AuthoredMapTrigger OnMap { get; set; }

      /// <summary>
      ///   Gabriel (24/10/2026, the night Rhagaea sends for her prisoner): a tale only a scenario opens (OPEN_TALE), never
      ///   offered in a conversation nor begun on the map; see AuthoredScenarioTrigger.
      /// </summary>
      [QuestField("The tale is opened only by a scenario rule (OPEN_TALE), never offered in a conversation nor begun on the map: the rule names the giver. With an invitation, a popup asks the player first; without, the tale begins at once (its second stage, a scene in the chat, can ask in role-play instead). A stage that is a conversation with someone opens by itself.")]
      [JsonProperty("on_scenario")] public AuthoredScenarioTrigger OnScenario { get; set; }

      /// <summary>The roles the story needs ({giver}, {village}...), with a word on each for the author.</summary>
      [QuestField("Every role your text uses, with a short description for the reader of the file. Each {role} is filled with a real name.")]
      [JsonProperty("binds")] public Dictionary<string, string> Binds { get; set; } = new();

      [QuestField("The steps of the story, the first being the request itself.")]
      [JsonProperty("stages")] public List<AuthoredStage> Stages { get; set; } = new();

      [QuestField("The single success ending: what the tale leaves when a path leads to \"reward\".")]
      [JsonProperty("reward")] public AuthoredOutcome Reward { get; set; } = new();

      /// <summary>The named endings a path may lead to ("next": "end:&lt;id&gt;"), each shaped like the reward.</summary>
      [QuestField("Named endings, each with its own regard, gold, memory and consequences. A path leads to one with \"end:<id>\".")]
      [JsonProperty("endings")] public Dictionary<string, AuthoredOutcome> Endings { get; set; } = new();

      [QuestField("What the tale leaves when its time runs out (days) before any ending is reached.")]
      [JsonProperty("failure")] public AuthoredOutcome Failure { get; set; } = new();

      /// <summary>The file it came from, for every report about it.</summary>
      [JsonIgnore] public string Source { get; set; }
   }

   /// <summary>When the quest may be offered, and by whom (increment 1: the shape the werewolf needs).</summary>
   public sealed class AuthoredRequires
   {
      [QuestField("The character who gives the tale.")]
      [JsonProperty("giver")] public GiverRequirement Giver { get; set; } = new();

      [QuestField("The village the tale happens in, bound as {village}.")]
      [JsonProperty("village")] public PlaceRequirement Village { get; set; }

      /// <summary>A second lord the story needs, bound as {rival} (branching tales, 04/10/2026: smoke_over_the_hills).</summary>
      [QuestField("A second lord the tale needs, bound as {rival}: the leader of another house of the giver's realm.")]
      [JsonProperty("rival")] public RivalRequirement Rival { get; set; }

      /// <summary>The giver's husband (the Troy-inspired tale, Gabriel 12/10/2026); needs requires.giver.married.</summary>
      [QuestField("The giver's husband, bound as {husband}. Needs requires.giver.married.")]
      [JsonProperty("husband")] public HusbandRequirement Husband { get; set; }

      [QuestField("What the player must be.")]
      [JsonProperty("player")] public PlayerRequirement Player { get; set; } = new();

      /// <summary>Gabriel (10/10/2026): the content level the tale needs; see AuthoredContentPolicy.</summary>
      [QuestField("The content level the tale needs, as the mod options name it: none (every player), mature, explicit or hardcore. A tale is offered only when the player's setting reaches it.", ClosedList = nameof(AuthoredQuestLoader.ContentLevels))]
      [JsonProperty("content")] public string Content { get; set; } = "none";
   }

   /// <summary>How a tale that begins on the map is set off, and the popup that asks the player (Gabriel, 10/10/2026).</summary>
   public sealed class AuthoredMapTrigger
   {
      [QuestField("Hours the player's party must have stood in the same place, out in the open. Default 3.")]
      [JsonProperty("still_hours")] public int StillHours { get; set; } = 3;

      [QuestField("On (the default): only by day.")]
      [JsonProperty("by_day")] public bool ByDay { get; set; } = true;

      [QuestField("On (the default): only when the nearest settlement is of the giver's own culture.")]
      [JsonProperty("in_givers_lands")] public bool InGiversLands { get; set; } = true;

      [QuestField("The chance the tale begins, once a day when everything else holds: above 0, at most 1. Default 0.15.")]
      [JsonProperty("chance_per_day")] public double ChancePerDay { get; set; } = 0.15;

      [QuestField("The popup's text, which sets the scene; {giver} is filled with the giver's name.")]
      [JsonProperty("invitation")] public string Invitation { get; set; }

      [QuestField("The popup's yes, which begins the tale and opens the conversation with the giver.")]
      [JsonProperty("accept")] public string Accept { get; set; } = "Go to them";

      [QuestField("The popup's no: nothing is lost, and the tale may come back after a season.")]
      [JsonProperty("decline")] public string Decline { get; set; } = "Not now";
   }

   /// <summary>The popup a scenario-opened tale shows (OPEN_TALE; Gabriel, 24/10/2026).</summary>
   public sealed class AuthoredScenarioTrigger
   {
      [QuestField("The popup's text, which sets the scene; {giver} is filled with the giver's name. Empty: no popup, the tale begins at once.")]
      [JsonProperty("invitation")] public string Invitation { get; set; }

      [QuestField("The popup's yes, which begins the tale and opens the conversation with the giver.")]
      [JsonProperty("accept")] public string Accept { get; set; } = "Go to them";

      [QuestField("The popup's no: nothing is lost, and the tale may come back after a season.")]
      [JsonProperty("decline")] public string Decline { get; set; } = "Not now";

      /// <summary>
      ///   Gabriel (25/10/2026): the player who will not follow the lady ends the tale, and Rhagaea is disappointed. A no
      ///   that is itself a way the story goes.
      /// </summary>
      [QuestField("An ending of \"endings\" the popup's no leads to: the tale is played to that ending at once (its memory, regard and consequences, its cooldown), instead of passing for a season. Needs an invitation.")]
      [JsonProperty("decline_ending")] public string DeclineEnding { get; set; }
   }

   public sealed class GiverRequirement
   {
      /// <summary>"lord", "notable", "gang_leader", "spouse", "companion", "any" (Gabriel: any character may offer a quest).</summary>
      [QuestField("lord, notable, gang_leader, spouse (the player's own husband or wife), companion (one of the player's companions), or any.", ClosedList = nameof(AuthoredQuestLoader.GiverRoles))]
      [JsonProperty("role")] public string Role { get; set; } = "lord";

      /// <summary>A role the giver must hold (a village of theirs), e.g. "{village}".</summary>
      [QuestField("\"{village}\": the village is picked among the giver's clan villages. Without it, the giver's home settlement is used, when it is a village.")]
      [JsonProperty("holds")] public string Holds { get; set; }

      [QuestField("The giver's regard for the player on Calradia Remembers' own ledger (the one built in conversation, not the game's relation).")]
      [JsonProperty("min_regard")] public int MinRegard { get; set; }

      [QuestField("On by default: a giver whose realm is at war with the player never offers it.")]
      [JsonProperty("not_at_war_with_player")] public bool NotAtWarWithPlayer { get; set; } = true;

      /// <summary>tashmetu (06/10/2026): a tale told only by givers of these cultures; empty for any.</summary>
      [QuestField("The giver's culture, one or a list (\"battania\", or [\"battania\", \"sturgia\"]); empty for any.")]
      [JsonProperty("culture")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Culture { get; set; } = new();

      /// <summary>
      ///   Gabriel (18/10/2026): the queens' tale is offered only in the realms whose queen has her face and pictures; the
      ///   three empires share one culture, so only the realm tells them apart. Empty for any.
      /// </summary>
      [QuestField("The giver's realm, one or a list, by its id or name (\"vlandia\", or [\"empire\", \"empire_w\"]); empty for any.")]
      [JsonProperty("realm")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Realm { get; set; } = new();

      /// <summary>Gabriel (10/10/2026): a tale of intimacy with the player's wife, whatever the player's sex; see AuthoredGiverPolicy.</summary>
      [QuestField("female or male: the giver must be a woman, or a man; empty for either.", ClosedList = nameof(AuthoredQuestLoader.GiverGenders))]
      [JsonProperty("gender")] public string Gender { get; set; }

      /// <summary>Gabriel (10/10/2026): "l'epouse doit voyager avec le joueur".</summary>
      [QuestField("On: the giver must be travelling in the player's own party.")]
      [JsonProperty("with_player")] public bool WithPlayer { get; set; }

      /// <summary>Gabriel (10/10/2026): "il faut aussi que la femme ne soit pas deja enceinte".</summary>
      [QuestField("On: never while the giver is with child.")]
      [JsonProperty("not_pregnant")] public bool NotPregnant { get; set; }

      /// <summary>The Troy-inspired tale (Gabriel, 12/10/2026): a giver with a husband to leave; see AuthoredGiverPolicy.</summary>
      [QuestField("On: the giver must be married, to someone other than the player (a tale that binds her husband needs it).")]
      [JsonProperty("married")] public bool Married { get; set; }

      [QuestField("On: never a giver who leads her house (she could not leave it).")]
      [JsonProperty("not_clan_leader")] public bool NotClanLeader { get; set; }

      /// <summary>Gabriel (12/10/2026): "jamais par la Reine de mon royaume".</summary>
      [QuestField("On: never a giver of the player's own realm.")]
      [JsonProperty("not_of_players_realm")] public bool NotOfPlayersRealm { get; set; }

      /// <summary>tashmetu (20/10/2026): "Players keep asking for tales from town merchants." A notable of these trades; empty for any.</summary>
      [QuestField("A notable's trade, one or a list (\"merchant\", or [\"merchant\", \"artisan\"]): merchant, artisan, ruralnotable, headman, preacher. Only a notable has one; empty for any.", ClosedList = nameof(AuthoredQuestLoader.GiverTrades))]
      [JsonProperty("trade")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Trade { get; set; } = new();
   }

   /// <summary>The giver's husband, bound as {husband} (the Troy-inspired tale, Gabriel 12/10/2026).</summary>
   public sealed class HusbandRequirement
   {
      [QuestField("spouse_of_giver: the giver's own husband or wife.", ClosedList = nameof(AuthoredQuestLoader.HusbandRoles))]
      [JsonProperty("role")] public string Role { get; set; } = "spouse_of_giver";

      [QuestField("On: the husband must rule a realm (he declares its wars himself). Off: any husband, who presses his ruler.")]
      [JsonProperty("rules_a_realm")] public bool RulesARealm { get; set; }
   }

   public sealed class PlaceRequirement
   {
      /// <summary>tashmetu (20/10/2026): a town as a bound place, for tales from town merchants. Still bound as {village}.</summary>
      [QuestField("village, or town (a town is still named {village} in the tale's lines and roles).", ClosedList = nameof(AuthoredQuestLoader.PlaceTypes))]
      [JsonProperty("type")] public string Type { get; set; } = "village";

      /// <summary>"forest", or null for anywhere.</summary>
      [QuestField("\"forest\", or leave it empty for anywhere.")]
      [JsonProperty("near")] public string Near { get; set; }

      /// <summary>tashmetu (06/10/2026): the village must be of these cultures; empty for any.</summary>
      [QuestField("The village's culture, one or a list (\"battania\", or [\"battania\", \"sturgia\"]); empty for any.")]
      [JsonProperty("culture")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Culture { get; set; } = new();
   }

   /// <summary>The {rival}: a clan leader of the giver's realm who is not the giver (branching tales, 04/10/2026).</summary>
   public sealed class RivalRequirement
   {
      [QuestField("clan_leader: the leader of another house of the giver's realm.", ClosedList = nameof(AuthoredQuestLoader.RivalRoles))]
      [JsonProperty("role")] public string Role { get; set; } = "clan_leader";

      [QuestField("\"{giver}\": the rival serves the same realm as the giver.")]
      [JsonProperty("same_realm_as")] public string SameRealmAs { get; set; } = "{giver}";
   }

   public sealed class PlayerRequirement
   {
      [QuestField("0 to 6.")]
      [JsonProperty("min_clan_tier")] public int MinClanTier { get; set; }

      /// <summary>tashmetu (20/10/2026): "requires.player.gender, like the giver's."</summary>
      [QuestField("female or male: the tale is offered only to a woman, or a man; empty for either.", ClosedList = nameof(AuthoredQuestLoader.GiverGenders))]
      [JsonProperty("gender")] public string Gender { get; set; }
   }

   /// <summary>One stage: a journal line, one verified objective, what the people involved know, and what follows.</summary>
   public sealed class AuthoredStage
   {
      [QuestField("The stage's name, used by \"next\" to lead here. Unique in the tale.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("The line logged in the quest journal when the stage begins.")]
      [JsonProperty("journal")] public string Journal { get; set; }

      /// <summary>Gabriel (08/10/2026): a picture of the stage, shown beside the chat when its scene opens.</summary>
      [QuestField("An image file of the tale's own folder (.png or .jpg), or a video (.crvid, whose first frame is its picture), opened beside the chat when this stage's scene opens, or a conversation of this stage (with its characters or the people of its place).")]
      [JsonProperty("image")] public string Image { get; set; }

      /// <summary>
      ///   Gabriel (24/10/2026): a tale that comes back, with a new video each time ("qu'on choisisse un nouveau vidéo parmi
      ///   les choix que je donnerai"); see TaleImagePool.
      /// </summary>
      [QuestField("Several pictures or videos for this stage: each time the tale is played, one the player has not seen yet is chosen (all seen, they start over), and it holds for the whole tale. It stands in for \"image\".")]
      [JsonProperty("image_pool")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> ImagePool { get; set; } = new();

      /// <summary>Gabriel (25/10/2026): "une option qui indique de choisir dans le pool au hasard".</summary>
      [QuestField("How the image_pool picks: \"unseen\" (the default) one not seen yet, starting over once all were; \"random\" any of them each time, a repeat included.", ClosedList = nameof(AuthoredQuestLoader.ImagePoolPicks))]
      [JsonProperty("image_pool_pick")] public string ImagePoolPick { get; set; }

      [QuestField("What the game checks to complete the stage. A stage with choices may have none.")]
      [JsonProperty("objective")] public AuthoredObjective Objective { get; set; }

      /// <summary>The beat, for the model to say in its own words (Gabriel: a canvas, rephrased).</summary>
      [QuestField("How the moment feels. Spoken by the giver, when this stage is theirs to say. A line may hold only under a flag.")]
      [JsonProperty("canvas")]
      [JsonConverter(typeof(AuthoredLinesConverter))]
      public List<AuthoredLine> Canvas { get; set; } = new();

      /// <summary>What must stay true in anything said: fixed in substance, free in wording.</summary>
      [QuestField("What the giver must convey, without changing it. A fact may hold only under a flag.")]
      [JsonProperty("facts")] public List<AuthoredLine> Facts { get; set; } = new();

      /// <summary>Who knows what at this stage ("{giver}", "locals_of:{village}"): injected into their prompt only.</summary>
      [QuestField("Lines for other characters, framed as what they believe at this stage. The key is a role (\"{giver}\") or \"locals_of:{place}\".")]
      [JsonProperty("knowers")] public Dictionary<string, List<AuthoredLine>> Knowers { get; set; } = new();

      /// <summary>The ways the player may take out of this stage (branching tales, 04/10/2026).</summary>
      [QuestField("Ways out of this stage: by an act, by the player's choice in a scene, or by words in a conversation. The first taken wins.")]
      [JsonProperty("choices")] public List<AuthoredChoice> Choices { get; set; } = new();

      [QuestField("What follows when the stage's own objective is met.")]
      [JsonProperty("on_complete")] public AuthoredOnComplete OnComplete { get; set; } = new();

      /// <summary>
      ///   tashmetu (15/10/2026): consequences only ran at an ending, so a lady could not leave midway and be given back
      ///   later in the same tale, which the Troy-inspired design (12/10/2026) needed. These run when the stage is entered.
      /// </summary>
      [QuestField("Consequences carried out when the tale enters this stage (the same kinds as an ending's, each with its own \"if\"), so a tale can carry someone away midway and bring them back later. Never on the first stage, which is the request.")]
      [JsonProperty("on_enter")] public List<AuthoredConsequence> OnEnter { get; set; } = new();
   }

   public sealed class AuthoredOnComplete
   {
      /// <summary>The next stage id, "reward", or "end:&lt;id&gt;".</summary>
      [QuestField("The next stage's id, \"reward\" to end the tale in success, or \"end:<id>\" for a named ending.")]
      [JsonProperty("next")] public string Next { get; set; }
   }

   /// <summary>
   ///   One way out of a stage (branching tales, 04/10/2026). It is taken in one of three ways: by an act (its
   ///   <see cref="Objective" />), in a scene (no objective, in an explore stage: the narrator writes its marker), or by
   ///   words (<see cref="Said" />: the action interpreter reports it in a conversation with that character).
   /// </summary>
   public sealed class AuthoredChoice
   {
      [QuestField("The choice's name, unique in its stage.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("What the player can do, in plain words. In a scene, it is listed under the narration and the narrator is told it.")]
      [JsonProperty("label")] public string Label { get; set; }

      [QuestField("A line added to the journal under the stage's own, while this choice is open.")]
      [JsonProperty("journal")] public string Journal { get; set; }

      [QuestField("Where this choice leads: a stage id, \"reward\", or \"end:<id>\".")]
      [JsonProperty("next")] public string Next { get; set; }

      /// <summary>Flags that must hold for the choice to be open: "name" set, "!name" not set; all of them.</summary>
      [QuestField("Flags that must hold for this choice to be open (\"name\", or \"!name\" for not set). A closed choice is never shown nor taken.")]
      [JsonProperty("if")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> If { get; set; } = new();

      [QuestField("Flags set when this choice is taken.")]
      [JsonProperty("sets")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Sets { get; set; } = new();

      /// <summary>In a scene: the way out when the scene closes with no choice taken and the stage has no on_complete.next.</summary>
      [QuestField("In a scene: taken when the scene closes with no choice made, if the stage has no on_complete.next.")]
      [JsonProperty("default")] public bool Default { get; set; }

      [QuestField("By an act: what the game checks (talk_to, return, talk_to_locals, be_at, defeat_party, wait). The first met wins.")]
      [JsonProperty("objective")] public AuthoredObjective Objective { get; set; }

      [QuestField("By words: taken when, in a conversation with that character, the player says or agrees to what it means.")]
      [JsonProperty("said")] public AuthoredSaid Said { get; set; }

      /// <summary>What the person whose conversation took this choice speaks from, in that conversation.</summary>
      [QuestField("How the moment feels, for the character in whose conversation this choice is taken (as a stage's canvas).")]
      [JsonProperty("canvas")]
      [JsonConverter(typeof(AuthoredLinesConverter))]
      public List<AuthoredLine> Canvas { get; set; } = new();

      [QuestField("What that character must convey when this choice is taken.")]
      [JsonProperty("facts")] public List<AuthoredLine> Facts { get; set; } = new();

      [QuestField("Lines for that character, in the conversation that took this choice.")]
      [JsonProperty("knowers")] public Dictionary<string, List<AuthoredLine>> Knowers { get; set; } = new();
   }

   /// <summary>A choice taken by words: what the player must say, and to whom.</summary>
   public sealed class AuthoredSaid
   {
      [QuestField("The character it must be said to, as a role: \"{giver}\" or \"{rival}\".")]
      [JsonProperty("to")] public string To { get; set; }

      [QuestField("What the player's words must mean, plainly: \"the player promises to keep silent for a price\".")]
      [JsonProperty("meaning")] public string Meaning { get; set; }
   }

   /// <summary>
   ///   A line an author may write plain ("text") or under flags ({"text": "...", "if": "flag"}): it reaches the model
   ///   only while its flags hold.
   /// </summary>
   [JsonConverter(typeof(AuthoredLineConverter))]
   public sealed class AuthoredLine
   {
      public AuthoredLine() { }

      public AuthoredLine(string text) => Text = text;

      [QuestField("The line itself.")]
      [JsonProperty("text")] public string Text { get; set; }

      [QuestField("Flags that must hold for the line to be said (\"name\", or \"!name\" for not set).")]
      [JsonProperty("if")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> If { get; set; } = new();

      public static implicit operator AuthoredLine(string text) => new(text);
   }

   /// <summary>One condition the engine verifies; which fields matter depends on <see cref="Type" />.</summary>
   public sealed class AuthoredObjective
   {
      [QuestField("What the game checks.", ClosedList = nameof(AuthoredQuestLoader.ObjectiveTypes))]
      [JsonProperty("type")] public string Type { get; set; }

      [QuestField("talk_to: the character, as a role (\"{giver}\"). war_ends, dies, weds_player: the person the world is watched for.")]
      [JsonProperty("hero")] public string Hero { get; set; }

      /// <summary>
      ///   Gabriel (24/10/2026): a night judged by what it left, so the stage ends when the conversation does, the regard the
      ///   giver gained or lost in it already counted (see TaleRegardFlags).
      /// </summary>
      [QuestField("talk_to: \"opened\" (the default) completes the stage as the conversation opens; \"closed\" when it ends, so the whole conversation belongs to the stage and its ending sees what came of it.", ClosedList = nameof(AuthoredQuestLoader.TalkMoments))]
      [JsonProperty("when")] public string When { get; set; }

      [QuestField("The place, as a role (\"{village}\").")]
      [JsonProperty("place")] public string Place { get; set; }

      [QuestField("talk_to_locals: how many different notables of the place. bring: how many of the goods (1 to 200).")]
      [JsonProperty("count")] public int Count { get; set; } = 1;

      [QuestField("be_at, keep_watch: how near the place the party must be (default 5).")]
      [JsonProperty("radius")] public float Radius { get; set; } = 5f;

      /// <summary>keep_watch: [first night the scene may open, night it opens for certain].</summary>
      [QuestField("keep_watch: [first night it may happen, night it surely happens].")]
      [JsonProperty("nights")] public int[] Nights { get; set; }

      /// <summary>keep_watch: the chance each eligible night opens the scene.</summary>
      [QuestField("keep_watch: the chance each night from the first opens the scene (0 to 1).")]
      [JsonProperty("chance")] public double Chance { get; set; }

      /// <summary>wait: the days that must pass with no other way out taken (branching tales, 04/10/2026).</summary>
      [QuestField("wait: how many days must pass with no other choice taken.")]
      [JsonProperty("days")] public int Days { get; set; }

      [QuestField("explore: the scene played in the chat.")]
      [JsonProperty("scene")] public AuthoredScene Scene { get; set; }

      [QuestField("defeat_party: the band spawned near the place.")]
      [JsonProperty("spawn")] public AuthoredSpawn Spawn { get; set; }

      /// <summary>bring (tashmetu, 20/10/2026): the goods asked; see TaleItemPolicy.BringKey for how the name is read.</summary>
      [QuestField("bring: the goods asked, as the game names them: a kind (horses, livestock, grain), an item category (wine, iron, tools) or one item's id.")]
      [JsonProperty("item")] public string Item { get; set; }

      [QuestField("bring: who the goods are handed to, a role ({giver} when empty).")]
      [JsonProperty("to")] public string To { get; set; }

      /// <summary>
      ///   Gabriel (25/10/2026), on the night in Rhagaea's chamber: "ca pourrait etre trop court ou trop long", then "Ce plan ou
      ///   cet arc devrait diriger l'experience que le LLM fait vivre au joueur." See TaleArc.
      /// </summary>
      [QuestField("talk_to with \"when\": \"closed\": the arc that paces the conversation, beat by beat, then closes it.")]
      [JsonProperty("arc")] public AuthoredArc Arc { get; set; }
   }

   /// <summary>The beats a stage's own conversation plays in order, each for a number of exchanges, then the close (TaleArc).</summary>
   public sealed class AuthoredArc
   {
      [QuestField("The beats, played in order; each lasts its number of exchanges.")]
      [JsonProperty("beats")] public List<AuthoredArcBeat> Beats { get; set; } = new();

      [QuestField("The most exchanges the whole arc may last before it goes to its final beat (0 or empty: no cap).")]
      [JsonProperty("max_exchanges")] public int MaxExchanges { get; set; }

      [QuestField("The beat the arc ends on, by id: once played, the conversation closes. Empty: the last beat.")]
      [JsonProperty("final")] public string Final { get; set; }

      /// <summary>Gabriel (25/10/2026): "melange des deux, signaux observes avec tirage en filet."</summary>
      [QuestField("Things the game watches for in the conversation; one seen sets a tale flag of the same name.")]
      [JsonProperty("signals")] public List<AuthoredArcSignal> Signals { get; set; } = new();

      [QuestField("A signal that turns the arc at once, whatever beat plays: the signal's id, then a beat's id or \"close\".")]
      [JsonProperty("on_signal")] public Dictionary<string, string> OnSignal { get; set; } = new();
   }

   /// <summary>Something the reader of the turn's actions reports when the reply shows it (TaleArc).</summary>
   public sealed class AuthoredArcSignal
   {
      [QuestField("The signal's id, also the flag it sets (\"npc_climaxed\").")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("What the reply must show for it to count, in plain words (\"{giver} reaches her climax in this reply\").")]
      [JsonProperty("means")] public string Means { get; set; }

      [QuestField("Optional: when not seen after some exchanges of listening, the game may decide it happens.")]
      [JsonProperty("net")] public AuthoredArcNet Net { get; set; }
   }

   /// <summary>The roll that makes a signal happen when the conversation has not shown it (TaleArc).</summary>
   public sealed class AuthoredArcNet
   {
      [QuestField("Exchanges of listening before the game may decide it.")]
      [JsonProperty("after")] public int After { get; set; }

      [QuestField("The chance, at the end of each listening beat from then on, that the game decides it happens (0 to 1).")]
      [JsonProperty("chance")] public double Chance { get; set; }
   }

   /// <summary>Where a beat leads when it is played out: the first whose flags hold.</summary>
   public sealed class AuthoredArcNext
   {
      [QuestField("Flags that must hold (\"name\", or \"!name\" for not set); empty always holds.")]
      [JsonProperty("if")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> If { get; set; } = new();

      [QuestField("A beat's id, or \"close\" to end the conversation.")]
      [JsonProperty("to")] public string To { get; set; }
   }

   /// <summary>One beat of an arc: what the character does in it, who leads, for how many exchanges.</summary>
   public sealed class AuthoredArcBeat
   {
      [QuestField("The beat's id (\"prelude\").")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("How many exchanges it lasts: one player message and the reply to it (1 to 20).")]
      [JsonProperty("exchanges")] public int Exchanges { get; set; }

      [QuestField("Who drives the beat: \"npc\" (the character acts and proposes, the default) or \"player\" (the character follows).", ClosedList = nameof(AuthoredQuestLoader.ArcLeads))]
      [JsonProperty("lead")] public string Lead { get; set; }

      [QuestField("The signals this beat watches for (their ids).")]
      [JsonProperty("listens")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Listens { get; set; } = new();

      [QuestField("Where the beat leads when played out, read top to bottom; none holding (or none written): the next beat in the list.")]
      [JsonProperty("next")] public List<AuthoredArcNext> Next { get; set; } = new();

      [QuestField("The direction for this beat, in the voice of a canvas (\"Slow preliminaries: ...\").")]
      [JsonProperty("do")] public string Do { get; set; }
   }

   /// <summary>An exploration scene: the chat with a narrator and no character, until someone is met.</summary>
   public sealed class AuthoredScene
   {
      /// <summary>The name over the chat window during the scene (branching tales, 04/10/2026: a scene is not always a wood).</summary>
      [QuestField("The name over the chat window during the scene (\"The burned barns of {village}\"). Empty: \"The wood by <village>\".")]
      [JsonProperty("title")] public string Title { get; set; }

      [QuestField("What the narrator opens the scene with.")]
      [JsonProperty("opening")] public AuthoredBeat Opening { get; set; }

      [QuestField("What there is to discover. Without choices, at least one must be required.")]
      [JsonProperty("findings")] public List<AuthoredFinding> Findings { get; set; } = new();

      [QuestField("What the player's choices can spring. Each springs once.")]
      [JsonProperty("hazards")] public List<AuthoredHazard> Hazards { get; set; } = new();

      [QuestField("People who may be met in the scene, voiced by the narrator.")]
      [JsonProperty("encounters")] public List<AuthoredEncounter> Encounters { get; set; } = new();

      /// <summary>What the player leaves knowing, whatever they did: stated at closing for any required finding missed.</summary>
      [QuestField("Shown when the scene closes, after any missed findings.")]
      [JsonProperty("closing")] public string Closing { get; set; }

      /// <summary>fkasad (07/10/2026): a captive in chains has no place in the wood unless the tale wants one.</summary>
      [QuestField("Off by default: the player's prisoners may be invited into this scene only when it is on (an interrogation in the wood).")]
      [JsonProperty("invite_prisoners")] public bool InvitePrisoners { get; set; }
   }

   public sealed class AuthoredBeat
   {
      [QuestField("The scene as the player comes upon it.")]
      [JsonProperty("canvas")] public string Canvas { get; set; }

      [QuestField("What must be true in the narration.")]
      [JsonProperty("facts")] public List<string> Facts { get; set; } = new();
   }

   public sealed class AuthoredFinding
   {
      [QuestField("The finding's name, used in its marker.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("An image file of the tale's own folder (.png or .jpg), or a video (.crvid, whose first frame is its picture), shown beside the chat when the narration reveals it.")]
      [JsonProperty("image")] public string Image { get; set; }

      [QuestField("A required finding the player never reached is told when the scene closes.")]
      [JsonProperty("required")] public bool Required { get; set; }

      [QuestField("What the player finds.")]
      [JsonProperty("text")] public string Text { get; set; }

      [QuestField("Flags set when it is found (or told at closing).")]
      [JsonProperty("sets")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Sets { get; set; } = new();
   }

   public sealed class AuthoredHazard
   {
      [QuestField("The hazard's name, used in its marker.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("An image file of the tale's own folder (.png or .jpg), or a video (.crvid, whose first frame is its picture), shown beside the chat when it springs.")]
      [JsonProperty("image")] public string Image { get; set; }

      /// <summary>What the player must do to spring it, written into the narrator's prompt.</summary>
      [QuestField("What the player must do to spring it.")]
      [JsonProperty("trigger")] public string Trigger { get; set; }

      [QuestField("What the game does when it springs.")]
      [JsonProperty("effect")] public AuthoredHazardEffect Effect { get; set; }
   }

   public sealed class AuthoredHazardEffect
   {
      /// <summary>injure, alert, lose_time (increment 1); ambush and the rest later.</summary>
      [QuestField("injure, alert or lose_time.", ClosedList = nameof(AuthoredQuestLoader.HazardEffects))]
      [JsonProperty("type")] public string Type { get; set; }

      [QuestField("injure: [fewest, most] hit points taken, never below 1 HP.")]
      [JsonProperty("hit_points")] public int[] HitPoints { get; set; }

      [QuestField("alert: read but not played yet (every alert warns the stage's band).", Played = false)]
      [JsonProperty("party")] public string Party { get; set; }

      [QuestField("lose_time: read but not played yet (the scene ends, the night is lost).", Played = false)]
      [JsonProperty("hours")] public int Hours { get; set; }
   }

   public sealed class AuthoredEncounter
   {
      [QuestField("The person's name in the file, used in its marker.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("An image file of the tale's own folder (.png or .jpg), or a video (.crvid, whose first frame is its picture), shown beside the chat when they are met.")]
      [JsonProperty("image")] public string Image { get; set; }

      [QuestField("How the narrator names them (\"a girl from {village}\").")]
      [JsonProperty("name")] public string Name { get; set; }

      [QuestField("Who they are and how they seem.")]
      [JsonProperty("persona")] public string Persona { get; set; }

      [QuestField("What they know, and say only that.")]
      [JsonProperty("knows")] public List<string> Knows { get; set; } = new();

      [QuestField("Read but not played yet.", Played = false)]
      [JsonProperty("may_materialize")] public bool MayMaterialize { get; set; }

      [QuestField("Read but not played yet.", Played = false)]
      [JsonProperty("materialize_when")] public string MaterializeWhen { get; set; }

      [QuestField("Read but not played yet.", Played = false)]
      [JsonProperty("escort_to")] public string EscortTo { get; set; }

      [QuestField("Flags set when they are met.")]
      [JsonProperty("sets")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> Sets { get; set; } = new();
   }

   /// <summary>A party spawned for the quest: real, on the map, beaten in a real battle.</summary>
   public sealed class AuthoredSpawn
   {
      [QuestField("The band's name in the file.")]
      [JsonProperty("id")] public string Id { get; set; }

      [QuestField("The bandit faction whose troops it is made of (looters by default), or \"local\": the bandits of the nearest hideout, a share of them seasoned as the player's men are.")]
      [JsonProperty("template")] public string Template { get; set; } = "looters";

      [QuestField("[fewest, most] men.")]
      [JsonProperty("size")] public int[] Size { get; set; }

      [QuestField("Its name on the map.")]
      [JsonProperty("name")] public string Name { get; set; }

      [QuestField("The place it patrols around, as a role.")]
      [JsonProperty("near")] public string Near { get; set; }

      [QuestField("Read but not played yet (every band patrols around its place).", Played = false)]
      [JsonProperty("behavior")] public string Behavior { get; set; } = "hide_near";

      [QuestField("The tale fails if the band is still alive that many days after the stage began.")]
      [JsonProperty("flee_after_days")] public int FleeAfterDays { get; set; }

      /// <summary>fkasad (07/10/2026): "no climax" for a lord with an army; see AuthoredBandSizePolicy.</summary>
      [QuestField("The band grows with the player's men (never below its size, up to a ceiling), so a climax stays one for a lord with an army.")]
      [JsonProperty("scale_with_player")] public bool ScaleWithPlayer { get; set; }

      /// <summary>Gabriel (10/10/2026): what a lost fight against this band makes of the tale; see AuthoredDefeat.</summary>
      [QuestField("What the tale makes of the player losing a fight to this band. Without it, a lost fight changes nothing: the player may try again until the tale's clocks run out.")]
      [JsonProperty("on_defeat")] public AuthoredDefeat OnDefeat { get; set; }
   }

   /// <summary>
   ///   A lost fight against a tale's band (Gabriel, 10/10/2026): an ending of its own, or, without a next, the tale goes on
   ///   (told once per stage: memories, news, a journal line), the band still there to beat before it flees.
   /// </summary>
   public sealed class AuthoredDefeat
   {
      [QuestField("An ending (\"end:<id>\") the tale reaches when the player loses to this band. Without it, the tale goes on.")]
      [JsonProperty("next")] public string Next { get; set; }

      [QuestField("Added to the giver's regard for the player (negative takes it away).")]
      [JsonProperty("regard")] public int Regard { get; set; }

      [QuestField("What the giver remembers of the defeat, in the giver's own voice.")]
      [JsonProperty("memory")] public string Memory { get; set; }

      [QuestField("The line written in the journal when the player loses to this band.")]
      [JsonProperty("journal")] public string Journal { get; set; }

      [QuestField("What else the defeat leaves in the world (memory, world_event, grudge). A consequence may hold only under a flag.")]
      [JsonProperty("consequences")] public List<AuthoredConsequence> Consequences { get; set; } = new();

      /// <summary>The defeat as an outcome, carried out the way an ending's is.</summary>
      public AuthoredOutcome AsOutcome() => new() {Regard = Regard, Memory = Memory, Consequences = Consequences ?? new List<AuthoredConsequence>()};
   }

   /// <summary>What success, failure or a named ending leaves behind.</summary>
   public sealed class AuthoredOutcome
   {
      [QuestField("failure: the tale fails that many days after it was accepted.")]
      [JsonProperty("days")] public int Days { get; set; }

      [QuestField("Added to the giver's regard for the player (negative takes it away).")]
      [JsonProperty("regard")] public int Regard { get; set; }

      [QuestField("Paid to the player by the game; negative, the player pays it (a fine, a bribe, a child's keep) to the giver. A choice leading straight to an ending the player must pay for is offered only while the player has that much.")]
      [JsonProperty("gold")] public int Gold { get; set; }

      [QuestField("What the giver remembers, in the giver's own voice.")]
      [JsonProperty("memory")] public string Memory { get; set; }

      /// <summary>The last journal line when the tale ends this way (branching tales, 04/10/2026).</summary>
      [QuestField("The last line written in the journal when the tale ends this way.")]
      [JsonProperty("journal")] public string Journal { get; set; }

      [QuestField("What else the ending leaves in the world. A consequence may hold only under a flag.")]
      [JsonProperty("consequences")] public List<AuthoredConsequence> Consequences { get; set; } = new();
   }

   public sealed class AuthoredConsequence
   {
      /// <summary>memory, world_event (increment 1); the rest of the closed list later.</summary>
      [QuestField("memory, world_event, grudge, conception, elope (she leaves her husband with the player), return_spouse (she goes back to him), item (the player is given an item) or regard (someone's regard for the player moves by amount).", ClosedList = nameof(AuthoredQuestLoader.ConsequenceTypes))]
      [JsonProperty("type")] public string Type { get; set; }

      [QuestField("memory: who remembers, a role or \"locals_of:{place}\". grudge: who holds it, a role. conception: the player's partner, a role ({giver} when empty). elope, return_spouse: the lady who leaves or goes back ({giver} when empty). regard: whose regard moves, a role ({giver} when empty).")]
      [JsonProperty("who")] public string Who { get; set; }

      [QuestField("memory: what they remember. grudge: the cause, in the holder's words.")]
      [JsonProperty("text")] public string Text { get; set; }

      [QuestField("world_event: the news that spreads through the realm.")]
      [JsonProperty("summary")] public string Summary { get; set; }

      [QuestField("world_event: how loud the news is, 0 to 100. grudge: how deep it runs, 0 to 100.")]
      [JsonProperty("magnitude")] public int Magnitude { get; set; }

      [QuestField("world_event: where it happened, as a role.")]
      [JsonProperty("place")] public string Place { get; set; }

      [QuestField("grudge: who it is held against, a role (Gabriel, 06/10/2026: a tale may set two lords at odds).")]
      [JsonProperty("against")] public string Against { get; set; }

      /// <summary>The Troy-inspired tale (Gabriel, 12/10/2026): the husband she leaves or goes back to; see TaleElopementPolicy.</summary>
      [QuestField("elope: the husband she leaves; return_spouse: the husband she goes back to. A role, {husband} when empty.")]
      [JsonProperty("spouse")] public string Spouse { get; set; }

      /// <summary>Gabriel (10/10/2026): "Une chance de tomber enceinte"; see TaleConceptionPolicy.</summary>
      [QuestField("conception: the chance of a child, 0 to 1; the game's own 0.15 when left out. The woman of the two carries it; two women or two men conceive nothing.")]
      [JsonProperty("chance")] public double Chance { get; set; } = TaleConceptionPolicy.DefaultChance;

      /// <summary>item (tashmetu, 20/10/2026): the item given to the player, by its id; see TaleItemPolicy.GiftItemId.</summary>
      [QuestField("item: the item given to the player, by its id in the game (\"wine\", \"sumpter_horse\").")]
      [JsonProperty("item")] public string Item { get; set; }

      [QuestField("item: how many are given (1 to 200).")]
      [JsonProperty("count")] public int Count { get; set; } = 1;

      /// <summary>Gabriel (24/10/2026): a regard only some endings of a path earn, under a flag (an ending's own regard always holds).</summary>
      [QuestField("regard: how far their regard for the player moves (negative lowers it), -100 to 100, never 0.")]
      [JsonProperty("amount")] public int Amount { get; set; }

      [QuestField("Flags that must hold for this consequence to be carried out.")]
      [JsonProperty("if")]
      [JsonConverter(typeof(OneOrManyConverter))]
      public List<string> If { get; set; } = new();
   }
}
