# Realm queens (held back)

Gabriel, 13/10/2026: "ne livre pas les reines". The data for the six realm queens made in the character editor is kept
here, out of the shipped module, until the Troy-inspired tale that uses them ("The Guest of the King's Hall") is
written.

- `ModuleData/faces.json`: each realm's queen face (the face editor's line), with her chat portrait.
- `ModuleData/QueenPortraits/`: the chat portraits.
- `ModuleData/Quests/the_kings_hall/`: the tale itself (`the_kings_hall.json`, written 18/10/2026) and its pictures, by
  realm (`queen_{giver_realm}.jpg`). Offered only in the realms whose queen has a face (`requires.giver.realm`); add
  `nord` and `empire_w` there with their faces and pictures. The scene pictures are Gabriel's to make.
- `ModuleData/scenarios/the_kings_hall.json`: the realm's answer (war when she leaves, peace when she is returned, the
  king's gratitude when told).

To try it without shipping it: copy `faces.json` into the module's `ModuleData/Custom/`, the tale folder into
`ModuleData/Custom/Quests/`, and the pack into `ModuleData/Custom/scenarios/`; turn on Authored Quests and a content
level of at least Mature in the MCM; then `cr.story_check <queen>` and `cr.story_start the_kings_hall <queen>`.

The mechanism ships (RealmFaceBehavior, the portrait hook, `{giver_realm}` pictures): a player's own
`Custom/faces.json` or a mod's `ModuleData/cr_faces.json` still works. To ship the queens again, move these folders
back under `CalradiaRemembers/ModuleData/` and take them out of `Tools/Release/makezip.py`'s skip lists (and
`verifyzip.py`'s queens guard).
