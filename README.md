Tabs {.tabset}
==============

Overview
------------

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/alert_red.png?raw=true)
**This is an API for modders, it doesn't add anything to the game by itself.**
![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/alert_red.png?raw=true)

Introducing a library that takes care of all the **plumbing** required to add **custom health effects** to SPT mods, making it as easy as it possibly can be. For free.

Navigate to **API Docs** to see how to work with it and make your own logic.

API Docs and Examples
------------
### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) The start
Before starting, like with any modding toolkit, first you have to reference it in your project by downloading the release and extracting .dll file whenever you like.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_quest_small.png?raw=true) The mod and the project are well documented and will also provide you useful tooltips inside your IDE.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_quest_small.png?raw=true) [**Recommended**] Example (With Project):
```xml
<ItemGroup>
   <ProjectReference Include="..\EFTEffectManager\EFTEffectManager.csproj" />
</ItemGroup>
```


![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_small.png?raw=true) Example (With .dll):
``` xml
<Reference Include="EFTEffectManager">
   <HintPath>..\..\dev\ModFolder\EFTEffectManager.dll</HintPath>
</Reference>
```

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Generic Calls
- `EffectsManager.Init()` - Installs all Harmony patches (idempotent). Must be called at the start before doing anything else.
- `EffectsManager.RegisterEffect<T>(EffectOptions options = null)` - Registers one effect class and its lifecycle in every EFT registry.  
Constraint: `T : ActiveHealthController.Effect, new()`.  
Returns an `EffectRegistration`.

- `EffectsManager.RegisterEffect<T>(Assembly pluginAssembly, EffectOptions options = null)` - Same as above, but tells the toolkit which assembly hosts your `IconFilePath` (pass `typeof(Plugin).Assembly`). Mods sit in their own folder next to their icons, so this is the recommended overload - an explicit `EffectOptions.IconAssembly` still wins over it.

- `EffectsManager.RegisterEffectsFrom(Assembly, Func<Type, EffectOptions> optionsFactory = null)` - Registers every concrete effect type in an assembly (the scanned assembly is used as the icon host).
`EffectsManager.MovementSpeedPenalties` - Registry of movement speed penalty providers (if there is any other mods providing movement speed penalties)
- `EffectsManager.ErgonomicsPenalties` - Registry of ergonomics penalty providers.
- `EffectsManager.IsLocalPlayer(Player)` - Helper. Does a bit more checks rather than just `IsYourPlayer` or backed by a `PlayerHealthController`.
- `EffectsManager.IsLocalController(IHealthController)` - True for the local player's health controller only.
- `EffectsManager.RefreshPenalties(Player)` - Re-runs recalculation logic for a player that Effects Manager supports - `ErgonomicsPenalties` and `MovementSpeedPenalties` (done automatically, and is added just in case).

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Events You can Subscribe to

```csharp

// Works like strength update notification - useful in some cases.
EffectsManager.LocalEffectUpdated     += (player, IHealthEffect) => { };

// Fires when a chosen effect has been added, including delayed ones.
EffectsManager.LocalEffectAdded       += (player, IHealthEffect) => { };

// Fires when a chosen effect has started.
EffectsManager.LocalEffectStarted     += (player, IHealthEffect) => { };

// Native removal. ForceRemove can also produce residue first.
EffectsManager.LocalEffectRemoved     += (player, IHealthEffect) => { };

// Fires when a chosen effect has residued.
EffectsManager.LocalEffectResidued    += (player, IHealthEffect) => { };

// Fires when a chosen body part was destroyed.
// Also includes optional damageType.
EffectsManager.LocalBodyPartDestroyed += (player, bodyPart, damageType) => { };

// This Event fires per cured effect (fracture, bleed, etc) after `MedHealTriggers` ran
// A handler can already observe newly created effects.
// Events run before the penalty refresh for registered effects.
// Includes vanilla EFT effects!
EffectsManager.MedCuredEffect         += (controller, effect) => { };

// Stim cure only.
EffectsManager.StimCuredEffect        += (ActiveHealthController, IHealthEffect) => { };

// Accepted stim creates or refreshes a native effect.
EffectsManager.StimDebuffedEffect     += (player, bodyPart, damageType) => { };

// Accepted native activation/deactivation
EffectsManager.StimBuffChanged        += (player, bodyPart, damageType) => { };

```

One-shot stim entries can be accepted with `buff.Active == false`. Observe StimBuffChanged for these.

Set `EffectsManager.DiagnosticsEnabled = true` for API trace logs.

Please see down below **"Custom Logic Example"**

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true)EffectOptions

- `DisplayName`(null) - Localized name shown in the health UI tooltip (raw type name otherwise).
- `IconFilePath`(null) - Path to the PNG icon - relative paths resolve next to the assembly hosting the icon (the plugin you passed to `RegisterEffect<T>(Assembly, ...)`, `IconAssembly`, or the effect type's own assembly). A missing/broken image is a user error - the effect stays hidden in the health UI and you'll get notified if there's anything wrong.
- `IconAssembly`(null) - Assembly that hosts `IconFilePath`. Usually `typeof(Plugin).Assembly`.
- `IconSprite`(null) - Existing sprite, takes priority over the file icon.
- `InvalidatedBy`(empty) - Removes the custom effect when a matching effect starts on the same part. Matches classes and marker interfaces. (For example `typeof(IFracture)` would invalidate whatever custom effect you'd have).
- `MedHealTriggers`(empty) - Effect types whose cure creates this effect on the same body part. Fed by **meds and stimulators** alike (a stim that force-residues a fracture counts exactly like a medkit).
- `StimHealTriggers`(empty) - Same idea, but only fires when the cure came from a stimulator - for mods that want to treat stims differently from medkits.
- `StimDebuffTriggers`(empty) - Effect types that, when a stimulator *adds/refreshes* them (Fracture, HeavyBleeding, Pain, ...), create this effect on the same body part. Nothing fires when the stim's chance roll fails or immunity absorbs it.
- `StimBuffTriggers`(empty) - `EStimulatorBuffType`s that, when a stimulator buffs with them (PainKiller, Stimulator, Adrenaline, ...), create this effect on `EBodyPart.Common`.
- `EffectKind`(null) - `0` = regen, `1` = degradation. Set it together with the three rates below and the toolkit both APPLIES them every frame (real `ChangeHealth` / `ChangeEnergy` / `ChangeHydration`, one random body part per tick for health - 1:1 EFT replica) and advertises them through the EFT's `SetHealthRatesPerSecond` plumbing so the health UI shows them.
- `HealthRegenPerSecond`(0) - Health per second. Positive = healing in REGEN mode; the sign flips in DEGRADATION mode (positive then means damage/sec). Body-wide (random part per tick), not limb specific.
- `EnergyDrainPerSecond`(0) / `HydrationDrainPerSecond`(0) - Drains per second: positive = draining in **both** modes, negative = a gain.
- `Strength`(null) - Effect strength passed straight to `AddEffect` (EFT's default `1`). Rate effects scale their pushed rates by it.
- `DelayTime` / `WorkTime` / `ResidueTime`(null) - Lifetime overrides passed straight to `AddEffect`. `null` means "use the effect class' `Default*` values".
- `RemoveOnBodyPartDestroyed`(true) - Cleanup when the limb is destroyed.
- `PersistAcrossRaids`(true) - Only works if the effect implements `IRestorable` (warning will be logged otherwise).
- OfflineEffectType - 
- NetworkEffectType - 

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Rate effects (regen / degradation)

**How it works** - exactly like EFT's rate-carrying effects (HealthRate stims, Dehydration, ...): the toolkit ticks the real `ChangeHealth` / `ChangeEnergy` / `ChangeHydration` calls every frame while the effect is **Started**, and also advertises the rates through the EFT `SetHealthRatesPerSecond` so the effect shows in the health UI like a stim buff.

- Health lands on **one random body part per tick** (skips full / destroyed parts) - the same pattern EFT's HealthRate buffs use. Rate effects are therefore body-wide, not limb specific.
- Energy / hydration are global.
- Everything is multiplied by the effect's `Strength`.
- Sign rules: **health flips, drains stay drains** - `HealthRegenPerSecond` positive = healing in REGEN (0) mode and damage/sec in DEGRADATION (1) mode, energy/hydration positive = drain in **both** modes (negative = a gain).

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Full example - Regeneration

**Effects/Regeneration.cs** - the effect:

```csharp
using EFT.HealthSystem;

public interface IRegeneration : IHealthEffect { }

public class Regeneration : ActiveHealthController.Effect, IRegeneration, IEffectTriggersUIPanel
{
    public override float DefaultDelayTime   => 0f;
    public override float DefaultWorkTime    => 300f;   // 5 minutes
    public override float DefaultResidueTime => 0f;
}
```

**Plugin.cs** - registration (icon + rates in one place):

```csharp
EffectRegistration regeneration = EffectsManager.RegisterEffect<Regeneration>(
    typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName             = "Regeneration",
    IconFilePath            = "Regeneration.png",   // next to YOUR plugin dll or + folder
    EffectKind              = 0,                     // regen
    HealthRegenPerSecond    = 0.4f,                  // +0.4 hp/s
    EnergyDrainPerSecond    = 0.15f,                 // the healing costs energy
    HydrationDrainPerSecond = 0.1f,
    PersistAcrossRaids      = false,                 // recovery belongs to this raid
});

// ...and grant it from your own logic
regeneration.Add(myPlayer.HealthController, EBodyPart.Common);
```

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Full example - degradation (health loss)

**Effects/RadSickness.cs**:

```csharp
using EFT.HealthSystem;

public interface IRadSickness : IHealthEffect { }

public class RadSickness : ActiveHealthController.Effect, IRadSickness, IEffectTriggersUIPanel
{
    public override float DefaultWorkTime => 120f; // 2 minutes
}
```

**Plugin.cs**:

```csharp
EffectsManager.RegisterEffect<RadSickness>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName              = "Radiation sickness",
    IconFilePath             = "RadSickness.png",
    EffectKind               = 1,         // degradation - health sign flips
    HealthRegenPerSecond     = 0.3f,      // -> 0.3 hp/s of DAMAGE to a random limb
    EnergyDrainPerSecond     = 0.2f,      // drains stay drains in both modes
    HydrationDrainPerSecond  = 0.2f,
    PersistAcrossRaids       = false,
});
```

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Full example - stim buff trigger + rates together

Effects can be **UI + rates + triggers at once** - an "Adrenaline crash" that the game creates when a stimulator buffs with `Adrenaline`, shows its own icon, drains hydration while it lasts and disappears when painkiller effect starts:

**Effects/AdrenalineCrash.cs**:

```csharp
using EFT.HealthSystem;

public interface IAdrenalineCrash : IHealthEffect { }

public class AdrenalineCrash : ActiveHealthController.Effect, IAdrenalineCrash, IEffectTriggersUIPanel
{
    public override float DefaultWorkTime => 60f;
}
```

**Plugin.cs**:

```csharp
EffectsManager.RegisterEffect<AdrenalineCrash>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName             = "Adrenaline crash",
    IconFilePath            = "AdrenalineCrash.png",
    StimBuffTriggers        = { EStimulatorBuffType.Adrenaline },  // stim buffed -> create on Common
    InvalidatedBy           = { typeof(IPainKiller) },             // painkiller starts -> gone
    EffectKind              = 1,
    HydrationDrainPerSecond = 0.5f,
    PersistAcrossRaids      = false,
});
```

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Full example - med trigger + regen together

"BANDAGE - healing a bleed gives you a short regeneration on that limb" (body-wide regen rates, limb is only where the effect lives):

```csharp
EffectsManager.RegisterEffect<BandageRegeneration>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName          = "Bandaged",
    IconFilePath         = "BandageRegeneration.png",
    MedHealTriggers      = { typeof(IBleeding) },   // a bleed was cured -> create    
    EffectKind           = 0,
    HealthRegenPerSecond = 0.2f,
    Strength             = 1f,
    PersistAcrossRaids   = false,
});
```

The rates are applied while the effect is **Started** and stop by themselves when it
leaves that state (your `DefaultWorkTime` / `WorkTime` decides how long) - nothing to
clean up on your side.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Custom Logic Example

Every property in **EffectOptions** is optional. **MedHealTriggers** and **InvalidatedBy** default to **empty lists**. An empty list simply means "the EffectsManager does nothing here". If you want your own creation/removal rules, leave them out entirely and use the raw events - or skip events altogether and do with the returned registration handle yourself:

```csharp
// Registration
EffectRegistration reg = EffectsManager.RegisterEffect<MyEffect>(new EffectOptions
{
    DisplayName  = "My effect",
    IconFilePath = "MyEffect.png",
    // MedHealTriggers / InvalidatedBy intentionally omitted 
    RemoveOnBodyPartDestroyed = false,   // optional: handle this yourself too
});

// ...your own logic here
EffectsManager.MedCuredEffect += (controller, effect) =>
{
    // "create it when a med stops a bleed"
    if (effect is IBleeding)
        reg.Add(controller, effect.BodyPart);
};

EffectsManager.LocalEffectStarted += (player, effect) =>
{
    // "remove it when it happens again"
    if (effect is IHeavyBleeding)
        reg.Remove(player.HealthController, effect.BodyPart);
};

EffectsManager.LocalBodyPartDestroyed += (player, bodyPart, damageType) =>
{
    // "remove it when the body part gets blacked out"
    reg.Remove(player.HealthController, bodyPart);
};
```

**Things to know here:**
- `reg.Add` / `reg.Remove` are idempotent, theyskip dead controllers and log their actions - prefer them over raw `AddEffect` / `ForceRemove` calls.
- Effects created or removed manually still raise the EFT's event chain, so **registered effects get automatic penalty refresh exactly like declarative ones** (the EffectsManager reacts to the effect's own start/residue and NOT to who caused it).
- Declarative and manual styles can be mixed freely per effect - for example if you wanna keep `MedHealTriggers` but write a custom removal logic.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) EffectRegistration (returned by RegisterEffect)

- `Find(controller, bodyPart)` - active instance or null
- `Add(controller, bodyPart)` - creates it (idempotent)
- `Remove(controller, bodyPart)` - force-removes it
- `Options`, `EffectType` and `InterfaceType` expose the registration too.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Penalty Providers

**Speed** - `EffectsManager.MovementSpeedPenalties.Add(name, provider)`

```csharp
   // return speed fraction or null - no penalty
   Func<IHealthController, float?>
```

**float?** - `1f` = untouched, `0.3f` (for example) = one broken leg (the value is the fraction of normal max speed).

**Ergonomics** - `EffectsManager.ErgonomicsPenalties.Add(name, provider)`

```csharp
   // 0 = no penalty; (-0.1f = -10%)
   Func<IHealthController, float>
```

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Values are clamped to `[0.01, 1]`, combined into one factor (strictest wins) and applied live by postfixing `MovementContext.ClampSpeed` (walk) and the `StateSprintSpeedLimit` getter (sprint) - only for the local raid player, so vanilla recalculation can never wipe them and bots are never touched.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Values are summed live inside the `Player.ErgonomicsPenalty` getter - the sum is never written into EFTs own fields, so vanilla rebuilds cannot wipe it either.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Both registries de-duplicate by name (re-Add replaces) and catch provider exceptions instead of letting them leak into game. So if you do anything wrong - don't worry!

How to properly make icons
------------

If you're about to use this API to make a mod, please FOLLOW THESE RULES:
- Health icon dimensions should be 24x22. Otherwise, something can go wrong, and I won't be resposible for it
- Your image should have 1 pixel margin on the LEFT (1 pixel of transparency).
- Your image should have a 1 pixel border (to not break the EFT's vibe).
- Yes, you can reuse any image EFT already has, as long as it would not confuse players.

Can't? You can pick an existing health UI icon and redraw anything on top of it, leaving a border untouched:
[Click Here]()

Limitations and Head-ups
------------
- Currently supports **local player** and is not tested with Fika Project.
- **Marker interface required.** EFT UI identifies effects through their `IHealthEffect` interface - declare one per effect class (EffectsManager also warns if you don't).
- `IRestorable` is only required for `PersistAcrossRaids = true` - raid-local effects don't need it (a warning is logged otherwise). Keep names stable across mod versions or old versions of your mod might/will reference an unknown effect.
- **Icons are your responsibility**. `DisplayName` is captured at registration time (plugin startup), so config changes need a restart.
- **Byte space**: custom effect type codes are assigned by scanning **down from 255** for a code nobody uses yet (EFT or another mod), so registrations can never collide - up to 256 registrations. (for now)
- **Rate effects are body-wide** - health lands on one random body part per tick (vanilla HealthRate stim pattern), so a regen/health loss effect cannot target a single limb Limb-specific rules (like healed-fracture penalties) belong in penalty providers or your own logic.
- **Provider/handler exceptions** are caught and logged under the EffectManager so a broken provider cannot kill the game.


Example Setup
------------

### Broken Limbs Dont Heal

Everything that EFT related is declarative - we only keep the effect, its config, the penalty math and the re-break roll on bullet hits.

**Effects/HealedFracture.cs** - the effect itself:

```csharp
public interface IHealedFracture : IHealthEffect { }

public class HealedFracture : ActiveHealthController.Effect, IHealedFracture, IEffectTriggersUIPanel
{
    // can also be custom plugin value, just like everything else
    public override float DefaultWorkTime    => DurationSeconds;
    public override float DefaultDelayTime   => 0f;
    public override float DefaultResidueTime => 0f;
}
```

**Effects/HealedFractureRules.cs** - custom logic (shown only a part of the logic, not the entire file):

```csharp
public static float? GetMovementSpeedFraction(IHealthController hc)
{
    if (!ModConfig.LegPenaltyEnabled.Value) return null;

    int legs = CountHealed(hc, EBodyPart.LeftLeg) + CountHealed(hc, EBodyPart.RightLeg);
    if (legs == 0) return null;

    float percent = Mathf.Clamp(ModConfig.LegSpeedPenaltyPercent.Value, 0f, 95f);
    return Mathf.Max(0.05f, 1f - percent / 100f * legs);   // 15% per healed leg
}
```

**Plugin.cs** - wiring:

```csharp
EffectsManager.RegisterEffect<HealedFracture>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName     = "Healed fracture",
    IconFilePath    = "HealedFracture.png",      // lives next to YOUR plugin dll
    PersistAcrossRaids = false,                  // recovery belongs to this raid
    MedHealTriggers = { typeof(IFracture) },     // med/stim cured a fracture -> gain the effect
    InvalidatedBy   = { typeof(IFracture) },     // bone re-breaks -> lose it
});

EffectsManager.MovementSpeedPenalties.Add("HealedLegFracture", HealedFractureRules.SpeedFraction);
EffectsManager.ErgonomicsPenalties.Add("HealedArmFracture",    HealedFractureRules.ErgonomicsPenalty);
```

Creation, invalidation, limb-destroy cleanup, icon, UI name and the penalty refresh all happen in the toolkit - the mod only keeps its own game rule (the chance to re-break a healed bone when it gets shot, a normal Harmony patch on `ApplyDamage`).

Easy as that. It can be as complicated as you want, with custom events, logic and patches.
You can make it as simple as it can be, or go crazy. Do what you wish.

And enjoy! <3

Installation
------------
Drag and drop the contents of 7z into your root game folder.

{.endtabset}
