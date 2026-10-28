# Contes à embranchements et éditeur de contes : rapport

Gabriel, voici le bilan du travail demandé dans `goal.md`. Tout est sur la branche `quest-branches` des deux dépôts,
rebasée sur `origin/main` (mod) et `origin/master` (SDK) du 4 octobre 2026, poussée, les deux suites au vert :
mod 5580 tests, SDK 1814 tests. Rien n'a touché `main` ni `master`, rien n'a été déployé dans le jeu (toujours
`-p:NCrunch=1`), rien n'a été publié.

## Ce qui a été construit, commit par commit

### Mod (`Calradia Remembers`, branche `quest-branches`, les commits au-dessus de `main`)

| Commit | Contenu |
|---|---|
| `ebb17f8` (sur `main` depuis le 4 octobre) | Les tests de cartes reconnaissent un worktree git (le `.git` y est un fichier). Sans ça, ce worktree aurait sauté tous les tests de cartes et de contes livrés, et la suite aurait été verte pour n'avoir rien vérifié. |
| `c178381` | Incrément 1 : choix, fins et drapeaux dans le fichier (`choices`, `endings`, `sets`, `if`, `!nom`, objectif `wait`), et toutes les vérifications du loader (étape injoignable, impasse, fin inconnue, choix en double, scène sans sortie, drapeau jamais posé...). |
| `db953a3` | Incrément 2 : la machine à étapes suit les choix, les drapeaux et les fins; champs de sauvegarde additifs; journal avec la ligne de l'étape puis celle de chaque choix ouvert. |
| `9f9172c` | Incrément 3 : ce que le narrateur et les personnages se font dire (lignes sous drapeau, secret sur les étapes à venir et les choix fermés). |
| `82f5e93` | Incrément 4 : choix de scène par marqueur `[[choice:id]]`, et la liste numérotée cliquable sous la narration, dans les deux skins de chat. |
| `ade14fb` | Incrément 5 : `smoke_over_the_hills.json` réécrit (quatre fins), le conte de scène `the_bell_in_the_marsh.json` (non livré), le rôle `{rival}`. |
| `b6f8e43` | Incrément 6 : `cr.test authored_quest_branches`. |
| `ccb386f` | Éditeur, fondations : le schéma JSON généré du modèle C#, les vérifications en JavaScript, tenues au jeu par des fixtures partagées. |
| `65ccddf` | Éditeur : l'import Twine (Harlowe, SugarCube, Twee), tenu à ses fixtures. |
| `baf62a4` | Éditeur : la page elle-même, livrée dans `Modules/CalradiaRemembers/Docs/TaleEditor.html`. |
| `3b55513` | Guides : `HOWTO_Quests.md` (choix, fins, drapeaux, l'éditeur, « Writing tales in Twine ») et `AUTHORED_QUESTS.md` (le design final). |
| `12ccbe0` | Choix par les mots, pris par l'interpréteur d'actions (fait en DERNIER, comme demandé), plus `cr.test authored_quest_said_choice`. |

### SDK (`LLM Roleplay Service`, branche `quest-branches`, 1 commit au-dessus de `master`)

| Commit | Contenu |
|---|---|
| `83e112e` | `EncounterContext.StoryChoicesForInterpreter` : les choix par les mots ouverts dans CETTE conversation arrivent à l'interpréteur sous sa tête en cache, jamais dans le prompt du personnage. Le budget du prompt lean (`LeanPromptPolicyTests`) n'est pas touché : ce texte ne va que chez l'interpréteur, et seulement quand un tel choix est ouvert. |

### Rebasée sur `main` du 4 octobre au soir

La branche contient maintenant, par `main`, le repli NSFW sur un tour refusé (`1ab6bd5`) et l'annonce d'étape (`dbaf7a2`). Dans un conte à embranchements, l'annonce donne la ligne de journal de la nouvelle étape elle-même; les lignes des choix ouverts suivent dans le journal comme avant.

## Les décisions prises seul (à renverser au besoin)

Le détail, avec le pourquoi de chacune, est dans `DECISIONS.md`. En bref :

**Le format**
- F1. Un choix ne peut pas faire le guet (`keep_watch`) ni ouvrir une scène : il mène à une étape qui le fait.
- F2. La bande d'un choix ne fuit pas; elle reste jusqu'à la fin de l'étape.
- F3. Seules les étapes que le joueur peut atteindre doivent mener à une fin.
- F4. La première étape (la demande) ne peut pas offrir de choix.
- F5. Un `canvas` peut avoir plusieurs lignes, chacune sous un drapeau au besoin.
- F6. Un choix peut porter son propre `canvas`, ses `facts` et ses `knowers` (ce qui se dit dans la conversation qui le prend).
- F7. Une issue peut porter une dernière ligne de `journal`.
- F8. Toute fin nommée ferme l'entrée du journal comme un succès (le jeu n'a que succès ou échec; l'échec reste le délai dépassé).
- F9. Une scène à choix n'a pas besoin d'une trouvaille requise.
- F10. La sortie par défaut d'une scène ne peut pas dépendre d'un drapeau (sinon une scène pourrait n'avoir aucune sortie).
- F11. `if` et `sets` prennent un nom ou une liste.
- F12. L'id d'un choix de scène est un nom de marqueur (lettres, chiffres, `_`).
- F13. Une récompense écrite qu'aucun chemin n'atteint est signalée à l'auteur.
- F14. Le loader garde le texte qui ressemble à une date comme du texte.
- F15. Deux messages ont changé de libellé (plus clairs pour l'auteur).
- F16. Un fichier qui commence par un commentaire se charge maintenant (avant : zéro conte, sans un mot).
- F17. Une sortie brisée donne UN problème, pas une cascade d'échos.

**Le moteur**
- E1. Les tests de cartes voient un worktree (voir `ebb17f8`).
- E2. « La récompense a-t-elle du contenu » se compare à une issue vide, pas à `Regard != 0` (le ratchet des seuils l'aurait compté, avec raison).
- E3. Le personnage dont la conversation termine le conte garde son bloc jusqu'à la fin de cette conversation. Ça change ce que le seigneur se fait dire dans cette seule conversation (il paie la récompense en sachant pourquoi).
- E4. Une scène s'ouvre dès que son étape est atteinte, peu importe le chemin (avant : seulement après `keep_watch`).
- E5. Une étape quittée emporte ses bandes.
- E6. Une trouvaille requise est racontée à la fermeture de la scène, de quelque façon qu'elle ferme.
- E7. `cr.story_choose <id>` prend un choix ouvert comme le jeu le prendrait; `cr.story_status` montre drapeaux, chemin et choix ouverts.
- E8. Find-SwapRisk rapporte 15 membres sur `main` déjà; cette branche n'en ajoute aucun (le goal disait « doit rester à zéro », il n'y était pas).
- E9. Les choix par les mots passent par les faits du tour (`StoryChoicesForInterpreter`), pas par `CrActions` : une action externe est figée dans la tête en cache et serait enseignée dans toutes les conversations.
- E10. Le verbe `story_choice` est dispatché par le pont, hors de `GameActionCatalog` et de `HandledActionTypes` (sinon enseigné partout); `action_catalog_parity` reste vrai.
- E11. L'interpréteur se fait dire quand NE PAS rapporter (offre encore pesée, question, refus, blague, « j'y penserai »), un seul par réplique; le jeu n'accepte qu'un id déclaré, ouvert, dit à ce personnage-là.
- E12. Vérifié dans le code : le contexte est rebâti à chaque tour pour celui qui parle (un choix ouvert en cours de conversation est enseigné au tour suivant), l'action roule sur le fil principal, en conseil un `actor:` qui nomme un autre membre est refusé, et deux choix dans une même réplique n'en prennent qu'un (le premier).

**La scène et la liste numérotée**
- S1. Une scène peut avoir un `title` (affiché dans le chat).
- S2. Un chiffre tapé n'est lu que seul (« 1 », « 1. », « 1) »); « 1 et je cours » reste des mots.
- S3. « Something else... » laisse la liste en place et rend la main au clavier.
- S4. La liste est dans les messages qui défilent, sous la dernière narration.
- S5. Prendre une voie n'ajoute aucune ligne : ce sont les mots de la voie qui partent, comme si le joueur les avait tapés.
- S6. Les couleurs de la liste suivent chaque skin.

**Les exemples**
- W1. Le `{rival}` est le chef d'une autre maison du royaume du donneur, choisi au dé parmi ceux qui conviennent.
- W2. Pas de rancune PNJ à PNJ (aucune règle établie sur combien de temps deux seigneurs peuvent se haïr; à trancher par toi).
- W3. `smoke_over_the_hills` laisse tomber ce que le jeu ne peut pas lier (`{enemy_realm}` retiré).
- W4. `smoke_over_the_hills` a quatre fins, pas trois (exposé, confronté, acheté, silence; « acheté » est le choix par les mots du goal).
- W5. `the_bell_in_the_marsh` : le conte de scène, non livré.

**L'éditeur et sa garde**
- P1. Les fixtures passent par `ParseAll` (qui ajoute seulement la vérification d'id en double entre fichiers).
- P2. L'éditeur lit les fichiers exactement comme Newtonsoft (commentaires, virgules finales, guillemets simples, noms nus, hexa, NaN, arrondi bancaire...). Deux écarts laissés exprès, jamais vus dans un conte : les constructeurs `new Date(...)` et un caractère NUL au milieu du fichier.
- P3. Une ligne « pas du JSON valide » se compare sans le message du lecteur (qui est la formulation de Newtonsoft).
- P4. Sans Node, le test de parité échoue; il ne se saute jamais.

## Ce qui est gardé, et comment

- **Le loader** : `AuthoredQuestBranchLoaderTests` sur du vrai JSON passé au vrai loader.
- **Le moteur** : `AuthoredQuestBranchEngineTests` sur de vraies runs, chaque chemin marché jusqu'à sa fin.
- **Le secret** : `AuthoredQuestBranchPromptTests` (personnage, narrateur, interpréteur) et, côté SDK, `StoryChoicesForInterpreterTests`.
- **La liste numérotée** : `SceneChoiceMenuTests` (logique pure, le chat ne fait que l'afficher).
- **Les exemples** : `AuthoredQuestWorkedExamplesTests` marche chaque fin de `smoke_over_the_hills` et de `the_bell_in_the_marsh`.
- **L'éditeur tenu au jeu** : `QuestEditorParityTests` passe 88 fixtures (plus tous les contes du dépôt) dans le loader C# ET dans le validateur JavaScript de la page, sous Node, et exige les mêmes problèmes mot pour mot; vérifie aussi l'aller-retour ouvrir/sauvegarder/relire, que chaque champ lu par le jeu a son formulaire, et les quatre histoires Twine.
- **Le schéma** : `QuestEditorIsCurrentTests` régénère `quest.schema.json` depuis le modèle C# (modèle `CR_UPDATE_MAPS`) et exige que la copie dans la page soit identique, ainsi que le gabarit du loup-garou.
- **Dans le jeu** : `cr.test authored_quest_branches` (choix par acte, par marqueur, drapeaux, fin) et `cr.test authored_quest_said_choice` (choix par les mots par le VRAI pont : fermé refusé sans effet, ouvert pris jusqu'à sa fin). Les deux chargent un conte écrit dans le test, jamais un fichier livré, et nettoient après.
- **Les cartes** régénérées avec chaque changement; aucun ratchet élargi. Le map des dépendances montre une arête nouvelle `GameState -> Logic.AuthoredQuests` (pas de cycle : Logic ne dépend pas du mod).

### Les comportements et les tests qui les gardent

| Comportement | Test(s) |
|---|---|
| Le premier acte accompli décide, les autres se ferment | `GIVEN_a_stage_left_by_several_acts_WHEN_the_player_rides_to_the_village_first_THEN_that_path_is_taken_and_the_others_close`; `GIVEN_a_stage_left_by_several_acts_WHEN_the_player_goes_back_to_the_lord_THEN_the_tale_ends_its_own_way_and_remembers_it`; `GIVEN_days_passing_with_nothing_else_taken_WHEN_the_wait_is_over_THEN_the_waiting_path_is_taken_and_not_before`; `GIVEN_a_choice_that_questions_the_locals_WHEN_enough_of_them_are_heard_THEN_that_path_is_taken`; `GIVEN_a_choice_that_hunts_a_band_WHEN_the_player_beats_it_THEN_that_path_is_taken_and_no_other_band_counts` |
| Un choix fermé (son `if` non tenu) ne se prend jamais : par acte | `GIVEN_a_choice_by_act_closed_by_its_flag_WHEN_its_act_is_done_THEN_it_is_not_taken` |
| ... par marqueur | `GIVEN_a_scene_choice_closed_by_its_flag_WHEN_its_marker_is_written_THEN_it_is_refused` |
| ... par clic | `GIVEN_a_way_closed_by_its_flag_WHEN_listed_THEN_it_is_not_offered` |
| ... par les mots | `GIVEN_a_choice_by_words_closed_by_its_flag_WHEN_reported_THEN_it_is_not_taken`; `GIVEN_another_character_a_closed_choice_or_a_later_stage_WHEN_the_interpreter_is_told_THEN_it_learns_nothing`; `cr.test authored_quest_said_choice` |
| Deux choix par les mots dans une même réplique : seul le premier compte | `GIVEN_two_choices_by_words_reported_in_one_reply_WHEN_both_reach_the_game_THEN_only_the_first_is_taken` |
| Un choix par les mots ne compte que pour la bonne personne et un id déclaré | `GIVEN_a_choice_by_words_WHEN_reported_for_the_wrong_person_an_unknown_path_or_the_right_one_THEN_only_the_right_one_counts`; `GIVEN_a_choice_by_words_open_with_this_character_WHEN_the_interpreter_is_told_THEN_it_learns_the_id_the_meaning_and_the_report` |
| Un choix pris pose ses drapeaux | `GIVEN_a_stage_left_by_several_acts_WHEN_the_player_goes_back_to_the_lord_THEN_the_tale_ends_its_own_way_and_remembers_it`; `GIVEN_the_light_found_in_the_scene_WHEN_the_left_marker_is_written_THEN_the_story_follows_the_light_on_closing`; `GIVEN_an_undeclared_marker_WHEN_written_THEN_nothing_changes_and_closing_follows_the_default` |
| Un choix de scène compte seulement par son marqueur, jamais par le clic seul | `GIVEN_a_way_clicked_WHEN_its_words_are_sent_THEN_the_story_moves_only_on_the_narrators_marker` |
| Un marqueur non déclaré ou fermé ne change rien | `GIVEN_an_undeclared_marker_WHEN_written_THEN_nothing_changes_and_closing_follows_the_default`; `GIVEN_a_scene_choice_closed_by_its_flag_WHEN_its_marker_is_written_THEN_it_is_refused`; `GIVEN_a_path_already_chosen_in_the_scene_WHEN_another_marker_follows_THEN_the_first_stands` |
| Une scène fermée sans choix va à sa sortie par défaut | `GIVEN_an_undeclared_marker_WHEN_written_THEN_nothing_changes_and_closing_follows_the_default`; `GIVEN_the_bell_WHEN_the_player_walks_away_from_the_fork_THEN_they_wait_for_another_night_and_can_come_back` |
| « 1 » tapé = le premier choix OUVERT, pas le premier déclaré | `GIVEN_the_first_declared_way_closed_WHEN_1_is_typed_THEN_it_sends_the_first_open_way` |
| Un chiffre sans choix derrière reste du texte libre | `GIVEN_a_number_with_no_way_behind_it_or_among_words_WHEN_typed_THEN_it_goes_as_typed`; `GIVEN_no_list_shown_WHEN_a_number_is_typed_THEN_it_goes_as_typed` |
| La liste numérotée : ordre, « Something else... », cachée quand il ne faut pas | `GIVEN_open_ways_under_a_fresh_narration_WHEN_listed_THEN_they_are_numbered_in_order_and_end_with_something_else`; `GIVEN_the_player_answered_the_narrator_is_writing_or_the_scene_is_over_WHEN_listed_THEN_nothing_is_shown`; `GIVEN_a_scene_with_no_open_way_WHEN_listed_THEN_nothing_is_shown` |
| Chaque chemin atteint sa propre fin (regard, mémoire, conséquences) | `GIVEN_each_path_of_the_tale_WHEN_walked_to_its_end_THEN_each_leaves_its_own_ending`; `GIVEN_a_consequence_under_a_flag_never_set_on_this_path_WHEN_the_ending_is_reached_THEN_it_is_not_carried_out`; les cinq `GIVEN_smoke_...` et trois `GIVEN_the_bell_...` de `AuthoredQuestWorkedExamplesTests` |
| `reward` reste la fin de succès d'un vieux fichier | `GIVEN_a_tale_written_with_one_reward_WHEN_it_ends_THEN_the_reward_is_its_one_success_ending` |
| Le délai dépassé fait encore échouer le conte | `GIVEN_a_branching_tale_past_its_time_WHEN_a_day_passes_THEN_it_fails_and_leaves_the_failure` |
| Ligne de knower, canvas ou fact sous `if` : dite seulement si le drapeau tient; `!drapeau` l'inverse | `GIVEN_a_knower_line_under_a_flag_WHEN_the_flag_holds_or_not_THEN_the_character_is_told_it_only_when_it_holds`; `GIVEN_a_canvas_line_under_a_flag_WHEN_the_lord_hears_the_report_THEN_he_asks_about_the_light_only_if_it_was_seen`; `GIVEN_smoke_WHEN_the_tracks_were_seen_or_not_THEN_the_lord_hears_of_them_only_when_they_were`; `GIVEN_lines_written_plain_and_under_flags_WHEN_loaded_THEN_each_keeps_its_words_and_its_flags` |
| Un drapeau jamais posé est signalé à l'auteur | `GIVEN_a_flag_tested_but_never_set_WHEN_loaded_THEN_the_author_is_told_and_the_tale_still_loads` |
| Secret : aucun personnage ni narrateur ne se fait dire un choix fermé, une étape à venir, une ligne sous drapeau non posé | `GIVEN_a_stage_with_choices_WHEN_the_lord_speaks_THEN_he_is_told_none_of_the_ways_ahead_nor_what_lies_down_them`; `GIVEN_a_line_of_a_later_stage_WHEN_a_villager_speaks_earlier_THEN_they_are_told_nothing_of_it`; `GIVEN_a_scene_with_ways_out_WHEN_the_narrator_is_told_THEN_it_knows_only_the_open_ways_each_with_its_marker`; `GIVEN_a_scene_before_its_paths_WHEN_the_narrator_is_told_THEN_nothing_of_the_stages_beyond_is_in_it`; `GIVEN_choices_for_the_interpreter_WHEN_the_characters_own_prompt_is_built_THEN_the_character_is_not_told_them` (SDK) |
| Le narrateur ne prend une voie que sur les mots du joueur | `GIVEN_a_scene_with_ways_out_WHEN_the_narrator_is_told_THEN_it_takes_one_only_on_the_players_words_and_ends_there` |
| Le personnage de la conversation qui a pris un choix parle depuis ce choix, puis plus après | `GIVEN_the_conversation_that_took_a_choice_WHEN_the_lord_speaks_in_it_THEN_he_speaks_from_that_choice`; `GIVEN_the_conversation_that_ended_the_tale_closed_WHEN_the_lord_speaks_again_THEN_the_tale_is_no_longer_his_to_speak` |
| Loader : étape injoignable | `GIVEN_a_stage_no_path_reaches_WHEN_loaded_THEN_the_author_is_told_it_is_never_played` |
| Loader : impasse | `GIVEN_two_stages_leading_only_to_each_other_WHEN_loaded_THEN_the_tale_is_refused_as_a_dead_end` (et `GIVEN_a_path_that_loops_back_but_has_a_way_out_WHEN_loaded_THEN_the_tale_loads` pour la boucle permise) |
| Loader : fin inconnue / fin que rien n'atteint | `GIVEN_a_choice_leading_to_an_ending_the_file_does_not_define_WHEN_loaded_THEN_the_tale_is_refused`; `GIVEN_an_ending_no_path_leads_to_WHEN_loaded_THEN_the_author_is_told_and_the_tale_still_loads`; `GIVEN_a_written_reward_no_path_reaches_WHEN_loaded_THEN_the_author_is_told` |
| Loader : id de choix en double | `GIVEN_two_choices_of_one_stage_with_the_same_name_WHEN_loaded_THEN_the_tale_is_refused_naming_them` |
| Loader : scène à choix sans sortie | `GIVEN_a_scene_with_choices_but_no_way_out_when_closed_with_none_WHEN_loaded_THEN_the_tale_is_refused`; `GIVEN_the_default_way_out_of_a_scene_under_a_flag_WHEN_loaded_THEN_the_tale_is_refused` |
| Loader : les autres règles d'un choix | `GIVEN_a_choice_that_keeps_watch_...`; `GIVEN_a_choice_outside_a_scene_with_no_act_and_no_words_...`; `GIVEN_a_choice_in_a_scene_without_a_label_...`; `GIVEN_the_request_itself_offering_choices_...`; `GIVEN_a_wait_of_no_days_...`; `GIVEN_a_choice_said_to_a_rival_the_tale_does_not_bind_...`; `GIVEN_a_choice_with_both_an_act_and_words_...`; `GIVEN_a_way_out_that_leads_nowhere_WHEN_loaded_THEN_the_author_is_told_that_one_problem_and_not_its_echoes`; `GIVEN_a_flag_written_as_a_number_WHEN_loaded_THEN_it_is_reported_as_invalid_not_thrown` |
| Tout fichier qui se chargeait se charge pareil | `GIVEN_the_drafted_tales_WHEN_loaded_THEN_the_branching_ones_load_clean_and_the_lost_banner_is_refused_as_before`; les copies des anciens contes dans les fixtures (`legacy_*`) via `GIVEN_each_fixture_WHEN_the_game_reads_it_THEN_it_reports_what_the_fixture_expects`; toute la suite existante des contes, inchangée |
| Sauvegarde d'avant ce changement : se charge et continue | `GIVEN_a_run_saved_before_branches_existed_WHEN_loaded_THEN_it_plays_on_from_where_it_was` |
| Run à embranchements sauvegardée en pleine scène : drapeaux et chemin survivent | `GIVEN_a_branching_run_saved_mid_scene_WHEN_reloaded_THEN_its_flags_and_chosen_path_survive` |
| Journal : la ligne de l'étape puis celle de chaque choix ouvert | `GIVEN_a_stage_with_choices_WHEN_it_begins_THEN_the_journal_shows_its_line_then_each_open_choices_line` |
| Le `{rival}` | `GIVEN_the_head_of_another_house_of_the_givers_realm_WHEN_a_rival_is_chosen_THEN_he_is_bound`; `GIVEN_lords_who_cannot_be_the_rival_WHEN_a_rival_is_chosen_THEN_none_of_them_is_and_the_reason_is_said`; `GIVEN_several_fitting_lords_WHEN_the_dice_fall_THEN_one_of_them_is_chosen_by_the_roll` |
| App : les fixtures de parité (C# et Node) couvrent chaque règle du loader | `GIVEN_each_fixture_WHEN_the_game_reads_it_THEN_it_reports_what_the_fixture_expects`; `GIVEN_each_fixture_WHEN_the_editor_checks_it_THEN_it_reports_exactly_what_the_game_reports`; `GIVEN_every_tale_in_the_repository_WHEN_checked_by_the_game_and_by_the_editor_THEN_they_agree` |
| App : ouvrir puis sauvegarder ne change pas les problèmes | `GIVEN_every_tale_and_fixture_WHEN_the_editor_opens_and_saves_it_THEN_the_game_reports_the_same_problems_as_before` |
| App : chaque champ lu par le jeu a son formulaire | `GIVEN_every_field_the_game_reads_WHEN_the_editor_lists_its_forms_THEN_none_is_missing_or_unknown` |
| App : chaque fixture Twine s'importe en son conte attendu, que le jeu charge | `GIVEN_each_twine_story_WHEN_imported_THEN_it_becomes_its_expected_tale_with_its_notes`; `GIVEN_each_twine_story_WHEN_imported_THEN_the_game_loads_the_tale_with_nothing_to_report` |
| App : une macro que l'import ne sait pas traduire est nommée à l'auteur, jamais perdue | `GIVEN_macros_and_tags_the_import_cannot_map_WHEN_imported_THEN_each_is_named_to_the_author` |
| App : le schéma suit le modèle, la copie dans la page aussi | `GIVEN_the_quest_model_as_it_stands_WHEN_the_schema_is_regenerated_THEN_it_matches_the_committed_schema`; `GIVEN_the_quest_model_as_it_stands_WHEN_the_schema_is_regenerated_THEN_the_copy_inside_the_editor_matches`; `GIVEN_the_werewolf_that_ships_WHEN_the_editor_offers_it_as_a_template_THEN_it_is_the_same_tale` |

## Ce qui n'a PAS été fait, ou pas de façon sûre

- **L'archive de release** : `makerar.py` et `verifyzip` ne sont pas dans le dépôt, je n'ai donc pas pu les modifier. Au moment de couper la version qui contiendra cette branche, il faut que l'archive inclue `Modules/CalradiaRemembers/Docs/TaleEditor.html` (le csproj le copie déjà avec les guides; verifyzip doit l'exiger).
- **Rancune PNJ à PNJ** (W2) : laissée de côté, faute de règle établie. `smoke_over_the_hills` s'en passe.
- **`{enemy_realm}`** retiré de `smoke_over_the_hills` (W3) : le jeu ne sait pas le lier.
- **`the_lost_banner`** reste refusé comme avant (`deliver_item` n'existe toujours pas); je ne l'ai pas « réparé » en douce.
- **Find-SwapRisk** : 15 membres déjà présents sur `main`; aucun ajouté ici, mais ce n'est pas zéro (E8).
- **Deux écarts de lecture entre l'éditeur et le jeu** (P2) : `new Date(...)` et le caractère NUL au milieu d'un fichier. Jamais vus dans un conte.
- **Choix par les mots** : fait, et sûr au sens où le jeu refuse tout ce qui n'est pas déclaré, ouvert, et dit à la bonne personne. Ce qu'aucun test ne peut garantir, c'est la fidélité d'un vrai modèle à rapporter le bon moment (voir la liste de jeu ci-dessous).

### Trouvé en chemin, sur `main`, pas corrigé ici (hors du périmètre)

En traçant le chemin de l'interpréteur pour les choix par les mots :
- **Une action externe enregistrée tard est invisible pour l'interpréteur.** `ActionInterpreterPromptBuilder` calcule sa tête stable une seule fois, dans des champs statiques, au premier usage; rien ne la recalcule après un `CrActions.Register` plus tardif. Je n'ai pas tracé l'ordre de chargement réel d'un mod tiers, donc je ne sais pas si ça arrive en pratique.
- **`ExternalActionSpec.TeachingText` et `IsEligible` n'atteignent jamais l'interpréteur** (seulement le prompt de prose), alors que l'en-tête de `ExternalActionSpec.cs` dit l'action « taught to the interpreter and the 1:1 prose prompt » sans préciser que ces deux-là n'y vont pas; et `IsEligible` n'est pas revérifié avant `Execute`. Côté prose, seul `Guidance` est garanti pour une action externe.
- **Aucun test de budget ne mesure un prompt avec une action externe enregistrée.**

Ça touche l'Extension Surface pour les moddeurs, pas les contes; à toi de dire si on ouvre un chantier.

## Ce que seule une partie peut confirmer (liste de jeu courte)

Pour charger un conte non livré : copier `docs/design/quests/smoke_over_the_hills.json` ou `the_bell_in_the_marsh.json` dans `ModuleData/Quests/Custom/`, puis `cr.story_check`, `cr.story_start <id>`, `cr.story_status`, `cr.story_choose <id>`, `cr.story_step`.

1. `cr.test authored_quest_branches` et `cr.test authored_quest_said_choice` : verts.
2. La liste numérotée sous la narration, dans les DEUX skins (Classic et Manuscript) : lisible, cliquable, bien placée sous la dernière narration, cachée pendant que le narrateur écrit et après la réponse du joueur; « Something else... » rend le clavier.
3. Taper « 2 » seul envoie la deuxième voie ouverte; « 2 chevaux » part comme du texte.
4. Avec un vrai modèle, dans `the_bell_in_the_marsh` : le narrateur écrit `[[choice:...]]` seulement quand tes mots prennent la voie, et la voie fermée (avant la trouvaille) n'est jamais offerte.
5. Avec un vrai modèle, dans `smoke_over_the_hills` : dire au rival « d'accord, je me tais pour un prix » prend `bought`; une question (« combien tu offres? ») ne le prend PAS.
6. Le journal : la ligne de l'étape puis une ligne par choix ouvert; la ligne de fin.
7. Un choix qui chasse une bande : la bande apparaît, et disparaît si un autre chemin est pris.
8. Sauvegarder en pleine scène, recharger : la scène se rouvre, drapeaux et voie choisie intacts.
9. Une vieille sauvegarde avec `wolf_of_the_wood` en cours : continue comme avant.

## Comment essayer l'éditeur

Ouvrir directement dans un navigateur (hors ligne, aucun serveur) :
`_quest-branches\Calradia Remembers\CalradiaRemembers\Extension\TaleEditor.html`
(une fois la branche fusionnée et compilée normalement, il est aussi copié dans `Modules\CalradiaRemembers\Docs\TaleEditor.html`).

À essayer : « New » (conte vierge) ou « From the werewolf »; « Open... » sur `docs/design/quests/smoke_over_the_hills.json`; ajouter une étape et des choix, regarder la carte du conte et les vérifications en direct (BLOCKING en rouge, COSMETIC en jaune, à côté du champ fautif); « Save » écrit du JSON indenté que le jeu charge; « Import Twine... » sur `docs/design/quest-editor/fixtures/twine/harlowe_ferryman.html` (ou les versions SugarCube et Twee), avec la liste de ce que l'import n'a pas pu traduire. Vérifié en navigateur, thème clair et sombre.

## Comment fusionner, quand tu seras satisfait (c'est toi qui décides)

La branche s'appelle `quest-branches` dans les deux dépôts. Le mod a besoin du SDK (la nouvelle propriété `StoryChoicesForInterpreter`), donc le SDK d'abord :

```
cd "...\LLM Roleplay Service"        (sur master)
git fetch origin
git merge --ff-only origin/quest-branches
cd "...\Calradia Remembers"          (sur main)
git fetch origin
git merge --ff-only origin/quest-branches
```

Si `main` ou `master` a bougé depuis, `--ff-only` refusera : rebase d'abord la branche (`git rebase origin/main` dans le worktree; sur un conflit de carte, prendre celle de main puis `.\Tools\Update-Maps.ps1`), relance les deux suites, puis fusionne. Ensuite, pour ranger : `git worktree remove` sur les deux dossiers de `_quest-branches`, puis `git branch -d quest-branches` dans chaque dépôt.
