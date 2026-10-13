# Realm queens (held back)

Gabriel, 13/10/2026: "ne livre pas les reines". The data for the six realm queens made in the character editor is kept
here, out of the shipped module, until the Troy-inspired tale that uses them ("The Guest of the King's Hall") is
written.

- `ModuleData/faces.json`: each realm's queen face (the face editor's line), with her chat portrait.
- `ModuleData/QueenPortraits/`: the chat portraits.
- `ModuleData/Quests/the_kings_hall/`: the tale pictures, by realm (`queen_{giver_realm}.jpg`).

The mechanism ships (RealmFaceBehavior, the portrait hook, `{giver_realm}` pictures): a player's own
`Custom/faces.json` or a mod's `ModuleData/cr_faces.json` still works. To ship the queens again, move these folders
back under `CalradiaRemembers/ModuleData/` and take them out of `Tools/Release/makezip.py`'s skip lists.
