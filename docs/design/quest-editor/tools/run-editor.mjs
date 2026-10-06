// Code written by Gabriel Mailhot, 04/10/2026.
// Runs the tale editor's own modules, taken out of the shipped HTML as they are, under Node: the checks
// (quest-validator) and the Twine import (twine-import). CalradiaRemembers.Tests calls it to hold the editor to the
// game: the same fixtures go through AuthoredQuestLoader in C# and through this, and the answers must match.
//
//   node run-editor.mjs check  <TaleEditor.html> <file-or-folder>...   ->  {"<file>": ["problem", ...], ...}
//   node run-editor.mjs twine  <TaleEditor.html> <file>...             ->  {"<file>": {"quest": {...}, "notes": [...]}}
//
// Files are read as File.ReadAllText reads them in the game (UTF-8, a leading byte-order mark dropped).

import fs from 'node:fs';
import path from 'node:path';
import vm from 'node:vm';

const [, , mode, htmlPath, ...targets] = process.argv;
if (!mode || !htmlPath) {
  process.stderr.write('usage: node run-editor.mjs check|twine <TaleEditor.html> <file-or-folder>...\n');
  process.exit(2);
}

const html = fs.readFileSync(htmlPath, 'utf8');

function block(id) {
  const m = new RegExp('<script id="' + id + '"[^>]*>([\\s\\S]*?)</script>').exec(html);
  if (!m) throw new Error(htmlPath + ' has no <script id="' + id + '"> block');
  return m[1];
}

const context = vm.createContext({console});
vm.runInContext(block('quest-validator'), context, {filename: 'quest-validator.js'});
vm.runInContext(block('twine-import'), context, {filename: 'twine-import.js'});
context.QuestValidator.init(JSON.parse(block('quest-schema')));

function files(target) {
  if (fs.statSync(target).isDirectory())
    return fs.readdirSync(target).filter(f => /\.(json|html|twee|tw)$/i.test(f)).sort().map(f => path.join(target, f));
  return [target];
}

function readText(file) {
  return fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, '');
}

const out = {};
if (mode === 'fields') {
  // Every field of every part the schema describes, and where the editor's registry stands on it.
  vm.runInContext(block('editor-fields'), context, {filename: 'editor-fields.js'});
  const schema = JSON.parse(block('quest-schema'));
  const registry = context.TaleEditorFields;
  const missing = [], unknown = [];
  for (const [def, body] of Object.entries(schema.$defs)) {
    if (!body.properties || def === 'line') continue;
    const listed = registry[def] || [];
    for (const field of Object.keys(body.properties)) if (!listed.includes(field)) missing.push(def + '.' + field);
    for (const field of listed) if (!(field in body.properties)) unknown.push(def + '.' + field);
  }
  process.stdout.write(JSON.stringify({missing, unknown}, null, 2));
  process.exit(0);
}
for (const target of targets) {
  for (const file of files(target)) {
    const name = path.basename(file);
    if (mode === 'check') {
      if (!/\.json$/i.test(name)) continue;
      out[name] = context.QuestValidator.parseAll([{source: name, text: readText(file)}]).problems;
    } else if (mode === 'roundtrip') {
      // Load as the editor loads, save as it saves, check again: the problems must not move.
      if (!/\.json$/i.test(name)) continue;
      const text = readText(file);
      const before = context.QuestValidator.parseAll([{source: name, text}]).problems;
      let saved;
      try { saved = context.QuestValidator.toText(context.QuestValidator.toDocument(text)); }
      catch (e) {
        if (!(e instanceof context.QuestValidator.ReadError)) throw e;
        out[name] = {readable: false, before, after: before};
        continue;
      }
      out[name] = {readable: true, before, after: context.QuestValidator.parseAll([{source: name, text: saved}]).problems, saved};
    } else if (mode === 'twine') {
      if (!/\.(html|twee|tw)$/i.test(name)) continue;
      const result = context.TwineImport.importText(readText(file), name);
      out[name] = {quest: result.quest, notes: result.notes};
    } else {
      throw new Error('unknown mode ' + mode);
    }
  }
}

process.stdout.write(JSON.stringify(out, null, 2));
