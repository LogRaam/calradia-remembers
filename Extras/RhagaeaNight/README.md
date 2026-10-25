# The Empress's Summons (held back)

Gabriel, 24/10/2026: a Hardcore tale. While the player is Rhagaea's prisoner, in the town or castle where she is, at
night, a lady of her household comes to the cell with two guards and the jailer: the Empress asks to see the player.
In her bedchamber she waits in her bed; she is restless and has chosen to share her intimacy with her captive. Before
dawn she asks for the secret. Open to both sexes, no child, and the player stays a prisoner.

How it went is judged by the game, never by the model: her regard for the player when the tale began against her
regard as the conversation closes. Higher: a warm memory, kept secret, and +5 regard. Unchanged: a neutral memory.
Lower (the player fled, struck her, refused her coldly): a bitter memory. The tale comes back (every 20 days at most),
with a new video each time out of the chamber's pool.

- `ModuleData/Quests/rhagaea_night/rhagaea_night.json`: the tale. Its chamber stage shows one of
  `rhagaea_night_1.crvid` to `rhagaea_night_10.crvid`, which go in the same folder (Gabriel makes them; add more names
  to `image_pool` for more videos). Make a `.crvid` with `python Tools/Video/make_crvid.py clip.mp4 rhagaea_night_1.crvid`.
- `ModuleData/scenarios/rhagaea_night.json`: the rule that opens it (`ON_CAPTIVE_NIGHT`, held by Rhagaea's house,
  Rhagaea there, a 25% chance a night; no message, so the realm hears nothing). The tale itself asks her regard above
  0 and the Hardcore level.
- `play_test_scenario.json`: the same rule at every chance, for a play test only. Never ship it.

To try it: copy the tale folder into the module's `ModuleData/Custom/Quests/` and `play_test_scenario.json` into
`ModuleData/Custom/scenarios/`; Authored Quests on and the content level at Hardcore in the MCM; make sure Rhagaea's
regard for the player is above 0 (`cr.rep_set 10 Rhagaea`, checked with `cr.info Rhagaea`); then
`cr.captive_talk_probe jail Rhagaea` (the player held in her house's nearest town or castle, Rhagaea brought there) and
`cr.scenario_fire ON_CAPTIVE_NIGHT`. The popup opens; accept, play the night, close the chat; `cr.story_status` and
Rhagaea's memories show how it ended. `cr.captive_talk_probe free` ends the captivity.

To ship it: move the two `ModuleData` folders under `CalradiaRemembers/ModuleData/` once the videos are there (the suite
checks every shipped tale's pictures exist).
