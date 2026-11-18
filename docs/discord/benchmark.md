===== MESSAGE 1 =====
# 📊 Tutorial: benchmark AI models from the console

**What is it for?**
Before you settle on a model, you want to know: **does it do what the game needs, how long will I wait, what will it cost me, and how much does its cache save?** The mod measures all four for you, in your own game, with OpenRouter's real prices. No guessing from a price list.

The tool is `cr.cost_bench`. For each model you list, it gives you:
- **Accuracy**: how many deeds it gets right (gold paid, a sword handed over, a lord joining your house...) and how many it **invents** (a promise for later counted as done), with a margin of error.
- **Speed**: the seconds you wait for each reply.
- **Cost**: the dollars of each reply, from OpenRouter's own bill.
- **Cache**: how much of each call the provider served from its cache. Cached tokens cost far less, so two models of the same list price can cost very different amounts in play.
- And warning signs in the writing: repetitions, refusals, sermons, mentions of Earth, whether it remembered what was agreed.

**Nothing in your game changes**: nothing is saved, no letter is sent, no deed is carried out, and every setting it borrows is put back. It picks the character itself (your spouse, otherwise a lord of your realm).

**What you need**
- A loaded save.
- The **PROSE**, **Interpret** and **Background** roles all on **OpenRouter** (the prices come from OpenRouter's own bill).
- Some credit: **it spends real money**, up to a limit you set (see the costs below).

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
Which section to use depends on your **Chat Composition** in the MCM:
- **Enhanced** (two calls per reply): test the writer in **[prose]** and the deed reader in **[interpret]**. `name | model id | cases per difficulty`: a number tests a sample, blank tests the whole catalog (about 300 cases, dearer).
- **Standard** (one call writes and tags): test it in **[standard]**. It plays the same conversation, then about **thirty short staged scenes** (a price agreed then paid, a sword handed over, a lesson given, a wedding, a lord joining your house, a payment only promised for later) to check that the model **tags the deed itself**, and only when it is truly done.
- **[fold]**: background models, timed on summing up a long conversation.

`Full or Compact` and the reasoning effort are your MCM choices. The name is what the tables show; the model id is copied from the model's page on OpenRouter. **Delete the rows you don't need**: fewer rows, cheaper run.

===== MESSAGE 3 =====
## Step 2: check, then run

- `cr.cost_bench` (or `cr.cost_bench plan`) shows **what would run and how many calls**, and spends nothing. A row with a typo is named and skipped.
- `cr.cost_bench run 1` runs it, spending **at most 1 dollar**. When the limit is reached, it stops cleanly and the rows it could not finish say so. Without a number the limit is **10 dollars**: always give one, and start small.
The game waits while it runs. A prose row takes a minute or two, a Standard row a few minutes, a whole interpret catalog up to ten.

## Step 3: read the results

Everything goes to:
`Documents/Mount and Blade II Bannerlord/Logs/CalradiaRemembers_cost_bench.log`
- **PROSE / STANDARD**: the cost and the wait for the opening, an early reply, a long-talk reply and a letter, the share spent thinking, the share **from cache**, and the warning signs.
- **INTERPRET**: deeds read right out of the cases, by difficulty (nothing done, plain, oblique, look-alike), with a margin of error; cost and wait per reading; share from cache.
- **STANDARD, deeds tagged in its own reply**: the same, for the staged scenes. A scene the game would not let that character do is **skipped and named**, never counted.
- A **grid** of every prose + interpret pair, in dollars and seconds.
- **Every billed call**, and the **transcripts**: what each model actually wrote, and for each staged scene what it tagged. **Read them**: the price says nothing about the writing.

One run is one sample: one evening, one character, the prices of the day. Run it again before deciding on a close call.

===== MESSAGE 4 =====
## 💰 What a run costs

From the prices measured on 24/10/2026 (OpenRouter's bill). A guide, not a quote: prices move, and a model that thinks longer costs more.
```
Row                           Calls     Cost
One [prose] row               about 9   $0.01 to $0.07
One [standard] row            about 39  $0.03 to $0.45
One [interpret] row, 10       40        $0.01 to $0.18
One [interpret] row, blank    about 300 $0.06 to $1.35
One [fold] row                1         under $0.01
```
The low end is a cheap model (DeepSeek V4.1 Flash, GPT-6 Luna), the high end Grok 4.20 or GLM 4.6. The Standard figure is an estimate: its thirty staged scenes were added in 2.8.16 and have not been measured on many models yet.

**The file the mod writes the first time** (7 prose, 3 whole-catalog interpret, 1 standard, 2 fold) comes to **about $2 to $3**, most of it Grok reading the whole catalog. You rarely need all of it.

**Keep it cheap**
- Test only **your own composition**: Enhanced players need [prose] + [interpret], Standard players need [standard].
- Give interpret rows a **number** (`10` is about 40 calls), not the whole catalog.
- Two or three models at a time, then `cr.cost_bench plan` to see the calls, then `cr.cost_bench run 1`.
- Your **OpenRouter Activity page** shows each call and its price, if you want to check the log against your bill.

===== MESSAGE 5 =====
## Other benches

Each uses the models set in your MCM, spends tokens, and writes a log in the same `Logs` folder:
- `cr.rp_bench` plays two scripted scenes (a lord affronted then appeased, and an **intimate scene at Hardcore**) and writes the transcript to `CalradiaRemembers_rp_bench.log`. No score: **you** judge, by reading. Change the prose model in MCM, run it again, compare.
- `cr.action_bench` runs your **interpret model** over every test case (a reply, and the deeds it should or must NOT produce) and writes a scoreboard to `CalradiaRemembers_action_bench.log`. `cr.action_bench 3` runs each case three times and keeps the majority, for a model that answers differently each time.
- `cr.memory_bench Name` asks a character six questions and checks whether they **used what they were told** and **kept quiet about what they were not**: a secret kept, no invented numbers. The transcript is in the mod's log.

Found a model that plays well and costs little? Share the table here! 🛡️
