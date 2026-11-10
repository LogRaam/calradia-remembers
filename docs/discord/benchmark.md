===== MESSAGE 1 =====
# 📊 Tutorial: benchmark AI models from the console

**What is it for?**
Before you settle on a model, you want to know: **what will it cost me, how long will I wait, and does it play well?** The mod can measure that for you, in your own game, with OpenRouter's real prices. No guessing from a price list.

The main tool is `cr.cost_bench`. It plays the **same conversation** on each model you list: the scene opening, a reply eight turns in, a reply eighteen turns in (after the conversation is summed up), and a letter. Then it gives you tables: **cost per reply, seconds waited**, and warning signs (repetitions, refusals, sermons, mentions of Earth, whether it remembered what was agreed). It also scores **interpret models** on how many deeds they read right.

**Nothing in your game changes**: nothing is saved, no letter is sent, and every setting it borrows is put back. It picks the character itself (your spouse, otherwise a lord of your realm).

**What you need**
- A loaded save.
- The **PROSE**, **Interpret** and **Background** roles all on **OpenRouter** (the prices come from OpenRouter's own bill).
- Some credit: **it spends real money**, up to a limit you set.

===== MESSAGE 2 =====
## Step 1: choose the models

Open the console (`Alt + ~`) and type `cr.cost_bench`. The first time, it writes the list of models to test here:
`Documents/Mount and Blade II Bannerlord/Configs/CalradiaRemembers_cost_bench.txt`
Open it in a text editor. It has four sections, one row per model, fields separated by `|`. Lines starting with `#` are ignored.
```
[prose]
Grok 4.20 | x-ai/grok-4.20 | Full | Low
DeepSeek V4.1 Flash | deepseek/deepseek-v4.1-flash | Full | Low
[interpret]
DeepSeek V4.1 Flash | deepseek/deepseek-v4.1-flash | 10
[standard]
Grok 4.20, Standard | x-ai/grok-4.20 | Full | Low
[fold]
DeepSeek V4 Flash | deepseek/deepseek-v4-flash
```
- **[prose]**: `name | model id | Full or Compact | reasoning effort | NSFW fallback model` (the last one optional).
- **[interpret]**: `name | model id | cases per difficulty`. A number tests a sample; blank tests the whole catalog (about 300 cases, dearer).
- **[standard]**: the same as prose, in Standard mode (one call instead of two).
- **[fold]**: background models, timed on summing up a long conversation.
The name is what the tables show; the model id is copied from the model's page on OpenRouter. **Delete the rows you don't need**: fewer rows, cheaper run.

===== MESSAGE 3 =====
## Step 2: check, then run

- `cr.cost_bench` (or `cr.cost_bench plan`) shows **what would run**, and spends nothing. A row with a typo is named and skipped.
- `cr.cost_bench run 2` runs it, spending **at most 2 dollars**. Without a number the limit is **10 dollars**: always give one, and start small.
The game waits while it runs. Long rows take a few minutes.

## Step 3: read the results

Everything goes to:
`Documents/Mount and Blade II Bannerlord/Logs/CalradiaRemembers_cost_bench.log`
- the **tables**: per prose model, the cost and the wait for the opening, an early reply, a long-talk reply and a letter; per interpret model, how many cases it read right (with a margin of error); then a grid of every prose + interpret pair, in dollars and in seconds;
- **every billed call**;
- the **transcripts**: what each model actually wrote. **Read them**: the price says nothing about the writing.

One run is one sample: one evening, one character, the prices of the day. Run it again before deciding on a close call.

===== MESSAGE 4 =====
## Other benches

Each uses the models set in your MCM, spends tokens, and writes a log in the same `Logs` folder:
- `cr.rp_bench` plays two scripted scenes (a lord affronted then appeased, and an **intimate scene at Hardcore**) and writes the transcript to `CalradiaRemembers_rp_bench.log`. No score: **you** judge, by reading. Change the prose model in MCM, run it again, compare.
- `cr.action_bench` runs your **interpret model** over every test case (a reply, and the deeds it should or must NOT produce) and writes a scoreboard to `CalradiaRemembers_action_bench.log`. `cr.action_bench 3` runs each case three times and keeps the majority, for a model that answers differently each time.
- `cr.memory_bench Name` asks a character six questions and checks whether they **used what they were told** and **kept quiet about what they were not**: a secret kept, no invented numbers. The transcript is in the mod's log.

Found a model that plays well and costs little? Share the table here! 🛡️
