# Tales and scenarios that answer each other

Status: DESIGN, for Gabriel's decisions (12/10/2026). Nothing built yet.

## Why

fkasad (Nexus, 12/10/2026): "Can scenarios and quests interact? Like a scenario launching a quest?" Gabriel (12/10/2026):
a parameter decides whether a scenario starts a tale at once or only makes it available; then the case that showed the
real need: Troy. Helen is married to the king of Sparta, leaves with Paris, and a war follows. "Dans CR, si le joueur
partait avec Helene, il faudrait alors qu'une guerre se declare en consequence." And: "on peut faire un conte inspire de
Troy pour aider a batir la mecanique."

So both directions are wanted, and the more important one is the reverse of the question: a **tale's ending sets off
scenario rules** (the player leaves with the queen; the realm answers with war). A scenario opening a tale is the second
direction.

## What exists (12/10/2026)

- **Tales** (`ModuleData/Quests`, `cr_quests`): roles bound at the start (`{giver}`, `{village}`, `{rival}`, `{player}`),
  stages, choices, flags, endings with `consequences` (`memory`, `world_event`, `grudge`, `conception`).
- **Scenarios** (`ModuleData/scenarios`, `cr_scenarios`, `Custom/scenarios`): triggers `ON_DEATH`, `ON_MARRIAGE`,
  `ON_WAR_DECLARED`, `ON_PEACE`; guards (`IF_HERO_IS`, `IF_AT_WAR`, `IF_AT_PEACE`, `IF_ALIVE`, `IF_KINGDOM_EXISTS`,
  `IF_RULES`...); consequences `DECLARE_WAR`, `MAKE_PEACE`, `CHANGE_RELATION`, `CHANGE_INFLUENCE`, `INJECT_MEMORY`; every
  ready rule plays, `group` for alternatives, a rule plays only when all it does can happen (`CanApply`). Rules name
  heroes and kingdoms by fixed ids.
- **Stolen Love** (MCM, default off): seduce a married character, the marriage frays, a divorce
  (`MarriageDissolutionService`), a grudge on the jilted spouse (`GrudgeSource.StolenSpouse`). Ruled "a personal grudge,
  no clan or faction fallout, no war" for that ORGANIC mechanic; a written tale is an author's story and may want more.
- **War is the leader's decision** (Gabriel, 02/07/2026): a lord who does not rule cannot declare war.

## The reference tale: inspired by Troy

Working title: **The Guest of the King's Hall**. Content level `mature` (a love story, nothing explicit; the conversation
goes as far as the player's setting allows, as for every tale).

- **The giver**: a queen, the wife of a realm's ruler, unhappy in her marriage. **Her husband**: the king, bound as
  `{husband}`.
- **Opening**: the player is a guest at the king's court (the tale is offered in his capital). A feast; the queen speaks
  to the player.
- **Middle**: meetings that are states of one conversation (the garden, the walls at night, the chapel), the queen
  weighing her life; a servant who may see them (a flag).
- **The choice, before the king's feast ends**:
  - **eloped**: she leaves with the player. Her marriage is dissolved, she rides in the player's party, the king holds a
    deep grudge, and the tale's ending sets off the scenario rule: **the king's realm declares war on the player's**.
  - **refused**: the player honours the host. The queen remembers; the king never knows; perhaps his regard rises.
  - **told**: the player tells the king. The king's gratitude, the queen's grudge.
- **The scenario pack beside it** answers `ON_TALE_ENDED` for the `eloped` ending: war when the king rules and the
  realms are at peace; alternatives in one group for the cases where war cannot be (below).

## The mechanics it needs, in the order to build them

1. **A role for the husband.** `requires.husband: { "role": "spouse_of", "of": "{giver}" }`, and giver conditions
   `married: true`, `not_players_spouse: true`, `not_clan_leader: true` (a clan leader cannot leave her house: Stolen
   Love's own limit), optionally `married_to_ruler: true`. Checked by the loader and the Tale Editor like every role.
2. **The consequence `elope`** (`who`: a role, `{giver}` by default; `from`: the spouse role): her marriage dissolved
   through `MarriageDissolutionService`, she joins the player's party, the spouse holds a `StolenSpouse` grudge against
   the player (sized like Stolen Love's), news spreads (`world_event`). Marrying her later goes through the normal
   marriage, relaxed as for a Stolen Love divorcee. Refused (and reported) if she cannot leave: a clan leader, a
   prisoner, already gone.
3. **Every tale ending fires `ON_TALE_ENDED:<tale id>:<ending id>`**, carrying the tale's bound roles. No consequence
   to write: the ending IS the event. A rule with `ON_TALE_ENDED:<tale id>` alone answers every ending of that tale.
4. **Roles in scenario rules.** A rule answering a tale may write a role where it writes an id: `IF_RULES:{husband}`,
   `IF_AT_PEACE:{husband}:{player}`, `DECLARE_WAR:{husband}:{player}` (a hero stands for his realm where a realm is
   meant). A role the event does not carry makes the rule not ready, like an unknown id today.
5. **`OPEN_TALE:<tale id>:now|offer`** (the scenario-to-tale direction): `now` starts the tale at once with its
   announcement, when every condition of the tale holds (otherwise the rule does not play, as any consequence that
   cannot happen); `offer` only unlocks it, and the tale then follows its own triggers. A tale that waits for a scenario
   says `requires.unlocked_by_scenario: true`. Unlocks are kept in the save (one additive key).
6. **Later, not first**: an ultimatum before the war (an envoy from the king: send her back, or war), built on the
   existing envoys.

Each step is pure logic tested first, then the engine, a `cr.testall` test, the Tale Editor and the Scenario Composer
taught the new pieces, and HOWTO_Quests.md / EXTENSION_API.md. The reference tale is written (with the `cr-tale` skill)
once steps 1 to 4 exist, and is the first thing `cr.testall` plays through them.

## Gabriel's decisions (12/10/2026)

1. **A husband who does not rule**: he holds the grudge AND presses his king toward war ("rancune et pousse son roi vers
   la guerre"). The king still decides (the leader-only rule): a small pure policy weighs the husband's standing with his
   king, the king's open wars and the strength of the two realms, and the king declares, or not, within a few days. Its
   numbers are marked TUNE.
2. **Never offered by the queen of the player's own realm** ("jamais par la Reine de mon royaume"): a requirement of the
   tale, not a scenario alternative.
3. **She rides as a guest, and may be married** ("Invitee et epousable"): free and single in the player's party; a
   marriage later goes through conversation, relaxed as for a Stolen Love divorcee.
4. **The war is declared because of the player's act, and everyone knows it**; **the queen's return to her husband is
   the condition of peace** ("Le retour de la Reine a son mari est la condition de paix").

What 4 changes in the mechanics:
- The elopement is not an ending: the tale goes ON through the war. The choice leads to a stage "the war", with no time
  limit while the war lasts, whose ways out are endings: **returned** (the player gives her back: her marriage restored,
  she goes home, and the scenario answers with peace), and the ends the world may bring (the war ends some other way;
  she or the king dies; the player marries her, which closes the way back).
- So scenarios answer tale STAGES as well as endings: `ON_TALE:<tale id>:<stage or ending id>`, with the tale's roles.
- The war carries its reason: a `world_event` everyone hears ("{husband}'s realm goes to war over {giver}, taken by
  {player}"), and every lord of both realms remembers why. Peace terms in conversation name the queen's return.

New pieces, added to the list above: `requires.giver.not_of_players_realm`; a consequence `return_spouse` (marriage
restored through the game's own marriage, she leaves the party for her husband's); `PRESS_FOR_WAR:{husband}` (the
king's decision, point 1); `ON_TALE` firing on stages as well as endings.

## A character with a chosen face (Gabriel, 12/10/2026)

"est-ce possible de creer un NPC avec une apparence precise? Je pourrais creer l'epouse dans le character editor et
ressortir les valeurs." Yes: the game reads a face from the editor's own key (`BodyProperties.FromString` on the
`<BodyProperties version=... age=... weight=... build=... key=... />` the face editor copies with Ctrl+C), and a hero's
`StaticBodyProperties` is settable and saved with the campaign. Hair and beard are in the key; clothes are not.

The question is WHO wears it. Re-dressing an existing queen changes her face for the whole campaign (encyclopedia
included). Proposed instead: the tale CREATES its queen (`HeroCreator.CreateSpecialHero` from a lady of her culture),
with the tale's name, age and face, married at the tale's start to a lord or king who has no wife; the tale is offered
only where such a husband exists. A reusable piece: any tale may create its own characters (`"creates": {...}`), the
wife tales included.

## Questions for Gabriel (first round, answered above)

1. **A husband who does not rule.** The leader-only rule says he cannot declare war. Proposed: the `elope` grudge only
   (as Stolen Love), and later, when vassal pressure exists, he presses his king. Or should the tale simply require a
   husband who rules?
2. **The same realm.** If the king is the player's own liege, war is impossible. Proposed alternatives for the pack:
   the king's regard collapses and the player's standing at court falls (`CHANGE_RELATION`, `CHANGE_INFLUENCE`); exile
   or rebellion would be new consequences, later. Or should the tale not be offered by the player's own queen at all?
3. **Where she goes.** In the player's party as a guest (free, single, may be married later through conversation), or
   straight into the player's clan?
4. **The war's size.** `DECLARE_WAR` today is a plain declaration. Should a war started by a tale carry anything more
   (a war reason the realm's lords know, a memory of the insult for every lord of the king's realm)?
