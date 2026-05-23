# Phase 7.1 — Mod Skeleton Setup

Goal: prove the mod loads in Bannerlord, registers a campaign behavior, and
reaches the in-game lifecycle hooks. **No dialogue, no LLM, no persistence yet.**

---

## Step 1 — Project structure

Place files in your existing solution at:

```
V:\OneDrive\Programmation\GIT Workspace\LLM Roleplay Service\
├── NpcMemoryService.Core\                  (existing SDK)
├── NpcMemoryService.Core.Tests\            (existing tests)
├── NpcMemoryService.ConsoleRunner\         (existing runner)
└── NpcMemoryService.Bannerlord\            ← NEW
    ├── NpcMemoryService.Bannerlord.csproj
    ├── NpcMemoryServiceSubmodule.cs
    └── Behaviors\
        └── NpcMemoryCampaignBehavior.cs
```

Add the project to your `.sln`:

```sh
dotnet sln add NpcMemoryService.Bannerlord\NpcMemoryService.Bannerlord.csproj
```

Or add it via Visual Studio: right-click solution → Add → Existing Project.

---

## Step 2 — Create the Bannerlord module folder

```
C:\Program Files (x86)\Steam\steamapps\common\Mount & Blade II Bannerlord\
└── Modules\
    └── NpcMemoryService\
        └── SubModule.xml          ← copy from ModuleFolder/SubModule.xml
```

You may need administrator privileges to create folders in `Program Files`.
The `bin\Win64_Shipping_Client\` subfolder will be created automatically by
the build process.

---

## Step 3 — Verify the BannerlordInstallDir property

Open `NpcMemoryService.Bannerlord.csproj` and confirm this matches your install:

```xml
<BannerlordInstallDir>C:\Program Files (x86)\Steam\steamapps\common\Mount &amp; Blade II Bannerlord</BannerlordInstallDir>
```

If your install is elsewhere, edit this single line. Everything else derives
from it.

---

## Step 4 — Build

From the solution root in Visual Studio or command line:

```sh
dotnet build NpcMemoryService.Bannerlord -c Debug -p:Platform=x64
```

The post-build target `DeployToBannerlord` copies:

- `NpcMemoryService.Bannerlord.dll`
- `NpcMemoryService.Bannerlord.pdb`
- `NpcMemoryService.Core.dll`
- `NpcMemoryService.Core.pdb`

…into `Modules\NpcMemoryService\bin\Win64_Shipping_Client\`.

Expected console output:
```
  Deployed NpcMemoryService to C:\Program Files (x86)\...\Modules\NpcMemoryService
```

If you get "Access denied" — Visual Studio needs to run as administrator
(or change your Bannerlord install location to a user-writable folder).

---

## Step 5 — Enable the mod in the launcher

1. Launch Bannerlord
2. In the launcher, switch to the **Mods** tab
3. Find **NPC Memory Service** in the list
4. Check the box to enable it
5. Make sure it's loaded AFTER the Native, SandBoxCore, CustomBattle, SandBox,
   and StoryMode modules (the launcher should sort this automatically)
6. Click **Play**

---

## Step 6 — Validate

You should see these messages appear in the top-left in-game console at
launch and when starting a campaign:

```
[NPC Memory Service] NPC Memory Service v1.0.0 loaded.
[NPC Memory Service] NPC Memory Service ready (main menu reached).
[NPC Memory Service] NPC Memory Service campaign behavior registered.
[NPC Memory Service] SyncData (loading)
[NPC Memory Service] Campaign session launched.
```

If you see all five — **Phase 7.1 complete**.

If you only see some, the mod is partially loading. The most common causes:

- **Missing dependency DLL**: open `bin\Win64_Shipping_Client\` and confirm
  both `NpcMemoryService.Bannerlord.dll` AND `NpcMemoryService.Core.dll` are
  present.
- **Mod not enabled in launcher**: double-check the checkbox.
- **Load order**: NpcMemoryService should be at the bottom of the list (last
  to load).

---

## Step 7 — Troubleshooting checklist

If the mod doesn't appear in the launcher list at all:

- `SubModule.xml` must be in the **module root**, not in `bin/`.
- The `Id` in `SubModule.xml` must match the folder name (`NpcMemoryService`).

If the mod appears but checking it does nothing or crashes on launch:

- Look at `%USERPROFILE%\Documents\Mount and Blade II Bannerlord\Logs\` for
  exceptions. Bannerlord writes detailed crash logs there.
- A missing `Native` or `SandBox` dependency in `SubModule.xml` is a common
  cause.

If the mod loads but you see no messages:

- Check that `DLLName` in `SubModule.xml` exactly matches the built DLL name
  (case-sensitive).
- Check that `SubModuleClassType` matches `Namespace.ClassName` exactly.

---

## What's next (Phase 7.2)

Once 7.1 validates, Phase 7.2 will:

1. Implement `BannerlordNpcMemoryStore : INpcMemoryStore` using TaleWorlds'
   `IDataStore` for in-save persistence.
2. Wire the `NpcMemoryCampaignBehavior.SyncData` to load/save the store.
3. Configure the `NpcChatService` and `OpenRouterClient` at session launch.
4. Validate that an NPC profile survives a save/load cycle.

No dialogue hooks yet — that's Phase 7.3.
