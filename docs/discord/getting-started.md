===== MESSAGE 1 =====
# 🔑 Tutorial: getting started, the key, the models and the costs

**How does the mod talk?**
Calradia Remembers does not run an AI by itself: it sends each conversation to a **language model** (an LLM) through a **provider**, and the provider bills you for what you use. Three ways:
- **OpenRouter** (default, recommended to start): one key, hundreds of models, pay as you go.
- **NanoGPT**: the same idea, another shop.
- **OpenAI-Compatible**: any server that speaks the OpenAI format, including a **model on your own PC** (LM Studio, Ollama, KoboldCpp...). Free, but you need a strong graphics card.

## The fastest start (OpenRouter)
1. Create an account on **openrouter.ai**, add a few dollars of credit (5 $ lasts a long time, see the costs below).
2. Create a key at **openrouter.ai/keys** and copy it.
3. In game: MCM → Calradia Remembers → **OpenRouter** → **OpenRouter API Key**: paste it.
4. Talk to anyone. That's it: the default models are set.

⚠️ **Your key is like a credit card.** It is stored only on your PC. **Never** post a screenshot of that field or your config file, here or anywhere. If it leaks, delete it on openrouter.ai/keys and make a new one.

===== MESSAGE 2 =====
## The roles: why several models?

MCM → Calradia Remembers → **Mode & Models**. Each role has a **model** and a **provider** (`(default)` = the same provider as PROSE):
- **PROSE model**: writes what the characters say. The one that matters most for the quality of the roleplay. Default: `x-ai/grok-4.20`.
- **Interpret model**: reads each reply once more to find what was really **done** (a gift, a promise kept, a quest given, a duel accepted) so the game can carry it out. Default: `deepseek/deepseek-v4.1-flash`, cheap and very accurate.
- **NSFW model**: for explicit content when the prose model will not write it. Default: `x-ai/grok-4.20`; blank = the prose model.
- **Background model** (optional): summaries and memory folding. Blank = the prose model. Useful mostly on a local server.

**Chat Composition**: **Enhanced** (default) = the prose model writes, the interpret model reads: the richest. **Standard** = a single call: cheaper and faster, a little less faithful. **Dramaturge** = Enhanced plus an inner life and a keeper that reads each reply before you see it (experimental, a few cheap calls more per reply; its own Dramaturge model, blank = the prose model, used from group scenes on: setting it changes nothing yet). You can also switch it in the chat window.

**Model ids** are written in the provider's own format: on OpenRouter, copy the id from the model's page (`deepseek/deepseek-v4.1-flash`).

===== MESSAGE 3 =====
## What it costs (measured on v2.7.20, OpenRouter's own bill)

One ordinary reply, Enhanced, with the default interpreter:
```
Prose model          One reply     100 replies
Grok 4.20 (default)  $0.0044 3 s   $0.44
Grok 4.20, thinking  $0.0085 22 s  $0.85
Gemini 3.8 Flash     $0.0030 5 s   $0.30
DeepSeek V4.1 Flash  $0.0008 2 s   $0.08
GPT-6 Luna           $0.0006 3 s   $0.06
Gemma 4 31B          $0.0025 12 s  $0.25
GLM 4.6              $0.0037 23 s  $0.37
```
The seconds are what you wait. Each conversation also pays its **opening** (the scene and the first reply, the dearest two, around 2 cents on Grok), and now and then a letter or a summary.
A guide, not a quote: one evening, one lord, prices of the day. OpenRouter's **Activity** page shows your real bill, call by call.

**Cheaper:** a cheaper prose model; **Reasoning Effort (PROSE)** on Low or Off (thinking costs, and is slow); Chat Composition on Standard.
**Adult content:** mainstream Claude and GPT models write beautifully but refuse Explicit and Hardcore scenes. Fine at Off or Mature.
**Want to test a model before choosing?** See the benchmark tutorial.

===== MESSAGE 4 =====
## A model on your own PC

1. Run your local server (LM Studio, Ollama...) with a model loaded, and note its address.
2. MCM → **Mode & Models** → **Roleplay / Prose provider**: **OpenAI-Compatible**. Leave the other roles on `(default)`.
3. MCM → **OpenAI-Compatible** → **Endpoint URL (Prose / shared)**: the address **with `/v1`**, e.g. `http://localhost:1234/v1` (LM Studio) or `http://localhost:11434/v1` (Ollama). API Key: usually blank.
4. **PROSE model**: the model's name as your server knows it (e.g. `llama3.1:8b`).

**Then fit the mod to your model** (Prompt & Memory):
- **Local model context (tokens)**: the context length set in your server. The mod trims its prompt to fit.
- **Compact prompt for small models**: on for a 4k to 8k model, or if the AI stays **silent**. Characters then offer fewer tasks, so leave it off when the full prompt fits.
- **Reply Token Budget**: raise it if a thinking model is **cut off mid-thought**.
- **LLM Response Timeout**: raise it for a slow model.
- **Presence Penalty**: 0 if your model writes badly with it.
- **Memories in the prompt**: lower it if your model drowns in a long history.
A small model works, but expect less faithful actions and a thinner memory than a large cloud model.

Stuck? Post your **settings** (never your key!) and your mod version here. 🛡️
