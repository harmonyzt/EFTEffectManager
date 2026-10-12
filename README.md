Tabs {.tabset}
==============

Overview
------------

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/alert_red.png?raw=true)
**This is an API for modders, it doesn't add anything to the game by itself.**
![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/alert_red.png?raw=true)

A library that takes care of all the **plumbing** required to add **custom health effects** to SPT mods, making it as easy as it possibly can be. For free.

Navigate to **API Docs** to see how to work with it and make your own logic.

Setting Up
------------
You have to reference this API in your project by downloading the release and extracting .dll (or the project off GitHub) file whenever you like.

Then reference it inside your .csproj file. 

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

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_small.png?raw=true) The mod and the project are well documented and will also provide you useful tooltips inside your IDE.


API Docs and Examples
------------
### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Generic Calls
- `EffectsManager.Init()` - Installs all Harmony patches (idempotent). Should be called at the start before doing anything else inside your mod.
- `EffectsManager.RegisterEffect<T>(EffectOptions options = null)` - Registers one effect class and its lifecycle in every EFT registry.  
Constraint: `T : ActiveHealthController.Effect, new()`.  
Returns an `EffectRegistration`.
- `EffectsManager.RegisterEffect<T>(Assembly pluginAssembly, EffectOptions options = null)` - Same as above, but tells the API which assembly hosts your `IconFilePath` (pass `typeof(Plugin).Assembly`). This is the recommended to use if your mod is located in another folder inside Plugins dir.
- `EffectsManager.RegisterEffectsFrom(Assembly, Func<Type, EffectOptions> optionsFactory = null)` - Registers every concrete effect type in an assembly (the scanned assembly is used as the icon host).
`EffectsManager.MovementSpeedPenalties` - Registry of movement speed penalty providers (if there is any other mods providing movement speed penalties)
- `EffectsManager.ErgonomicsPenalties` - Registry of ergonomics penalty providers (if there is any other mods providing ergonomics penalties).
- `EffectsManager.IsLocalPlayer(Player)` - Helper. Does a bit more checks rather than just `IsYourPlayer` and backed by a `PlayerHealthController`.
- `EffectsManager.IsLocalController(IHealthController)` - True for the local player's health controller.
- `EffectsManager.RefreshPenalties(Player)` - Re-runs recalculations for a player that Effects Manager supports - `ErgonomicsPenalties` and `MovementSpeedPenalties` (done automatically, and is added just in case).

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

For more, please see  **"Custom Logic Example"** down below.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true)EffectOptions 

These options are used to register your effect.

- `DisplayName` - Localized name shown in the health UI tooltip (raw type name otherwise).
- `IconFilePath` - Path to the PNG icon - relative paths resolve next to the assembly hosting the icon (the plugin you passed to `RegisterEffect<T>(Assembly, ...)`, `IconAssembly`, or the effect type's own assembly). With a missing/broken image the effect stays hidden in the health UI and you'll get notified about it.
- `IconAssembly` - Assembly that hosts `IconFilePath`. Usually `typeof(Plugin).Assembly`.
- `IconSprite` - Existing sprite, takes priority over the file icon (`IconFilePath`).
- `InvalidatedBy` - Removes the custom effect when a matching effect starts on the same part. Matches classes and marker interfaces. (For example `typeof(IFracture)` would invalidate whatever custom effect you've had on a limb).
- `MedHealTriggers` - Effect types whose cure creates this effect on the same body part. Fed by **meds and stimulators** alike (a stim that force-residues a fracture counts exactly like a medkit).
- `StimHealTriggers` - Same idea, but only fires when the cure came from a stimulator - for mods that want to treat stims differently from medkits.
- `StimDebuffTriggers` - Effect types that, when a stimulator *adds/refreshes* them (Fracture, HeavyBleeding, Pain, ...), create this effect on the same body part. Nothing fires when the stim's chance roll fails or immunity absorbs it.
- `StimBuffTriggers` - `EStimulatorBuffType`s that, when a stimulator buffs with them (PainKiller, Stimulator, Adrenaline, ...), create this effect on `EBodyPart.Common`.
- `EffectKind` - `0` = regen, `1` = health loss. Set it together with the three rates below and the toolkit both APPLIES them every frame (real `ChangeHealth` / `ChangeEnergy` / `ChangeHydration`, one random body part per tick for health - 1:1 EFT replica) and advertises them through the EFT's `SetHealthRatesPerSecond` plumbing so the health UI shows them.
- `HealthRegenPerSecond` - Health per second. Positive = healing in REGEN mode; the sign flips in DEGRADATION mode (positive then means damage/sec). Body-wide (random part per tick), not limb specific.
- `EnergyDrainPerSecond` / `HydrationDrainPerSecond`(0) - Drains per second: positive = draining in **both** modes, negative = a gain.
- `Strength` - Effect strength passed to `AddEffect` (EFT's default `1`). Rate effects scale their pushed rates by it.
- `DelayTime` / `WorkTime` / `ResidueTime` - Lifetime overrides passed to `AddEffect`. `null` means "use the effect class' `Default*` values".
- `RemoveOnBodyPartDestroyed` - Cleanup when the limb is destroyed.
- `PersistAcrossRaids` - Only works if the effect implements `IRestorable` (warning will be logged otherwise).
- `OfflineEffectType` - `OfflineHealthController.Effect` counterpart - same class name and marker interface as the active effect. Required (together with `IRestorable`) for `PersistAcrossRaids = true`.
- `NetworkEffectType` - `NetworkHealthController.Effect` counterpart - same class name and marker interface as the active effect. **Required for Fika**, see the "Fika" section above.

Every property in **EffectOptions** is optional because the default to either null, 0, or an empty list.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Rate effects (regen / health loss)

**Q: How it works?**  
A: Exactly like EFT's rate effects (Health Rate stims, Dehydration): API ticks `ChangeHealth` / `ChangeEnergy` / `ChangeHydration` calls every frame while the effect is **Started**, and also advertises the rates through the EFT's `SetHealthRatesPerSecond` so the effect shows in the health UI like a stim buff.


- Health lands on **one random body part per tick** and skips full / destroyed parts - the same pattern EFT's HealthRate buffs use. Rate effects are therefore body-wide, not limb specific.
- Energy / hydration are global.
- Everything is multiplied by the effect's Strength param.
- Sign rules: `HealthRegenPerSecond` positive = healing in REGEN (0) mode and damage/sec in DEGRADATION (1) mode, energy/hydration positive = drain in **both** modes (negative = a gain).

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Example - Regeneration

**Effects/Regeneration.cs** - The effect:

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

**Plugin.cs** - Registration (icon + rates in one place):

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

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Example - Health Loss

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
    EffectKind               = 1,         // degradation - health sign flips so we lose health
    HealthRegenPerSecond     = 0.3f,      // -> 0.3 hp/s of DAMAGE to a random limb
    EnergyDrainPerSecond     = 0.2f,      // drains stay drains in both modes
    HydrationDrainPerSecond  = 0.2f,
    PersistAcrossRaids       = false,
});
```

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Example - Stim Buff Trigger + Rates altogether

Effects can be **UI + rates + triggers at once**.  
Let's see how'd we add, for example "Adrenaline crash" that the API would create when a stimulator buffs with `Adrenaline`, show our own icon, drains hydration while it lasts and disappears when painkiller effect starts. Should be easy?

**Effects/AdrenalineCrash.cs**:

```csharp
using EFT.HealthSystem;

public interface IAdrenalineCrash : IHealthEffect { }

public class AdrenalineCrash : ActiveHealthController.Effect, IAdrenalineCrash, IEffectTriggersUIPanel
{
    // We don't need anything else here
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
    InvalidatedBy           = { typeof(IPainKiller) },             // painkiller starts -> custom effect is gone
    EffectKind              = 1,
    HydrationDrainPerSecond = 0.5f,
    PersistAcrossRaids      = false,
});
```

#### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_requirement_fulfilled.png?raw=true) Example - Med Trigger + Regen altogether

Let's try making a slight heal when we heal a bleeding on one of our limbs:

```csharp
EffectsManager.RegisterEffect<BandageRegeneration>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName          = "Bandaged",
    IconFilePath         = "BandageRegeneration.png",
    MedHealTriggers      = { typeof(IBleeding) },   // a bleed was cured -> create effect
    EffectKind           = 0,
    HealthRegenPerSecond = 0.2f,
    Strength             = 1f,
    PersistAcrossRaids   = false,
});
```

The rates are applied while the effect is **Started** and stop by themselves when it
leaves that state (your `DefaultWorkTime` / `WorkTime` decides how long) - so there's nothing to
clean up on your side.

### ![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/notification_icon_note.png?raw=true) Custom Logic Example

**MedHealTriggers** and **InvalidatedBy** default to **empty lists**.

An empty list simply means "the EffectsManager does nothing here".
If you want your own creation/removal rules, leave them out entirely and use the raw events - or skip events altogether and do with the returned registration handle yourself:

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

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_small.png?raw=true) Values are clamped to `[0.01, 1]`, combined into one factor (strictest wins) and applied live by postfixing `MovementContext.ClampSpeed` (walk) and the `StateSprintSpeedLimit` getter (sprint) - only for the local raid player, so vanilla recalculation can never wipe them and bots are never touched.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_small.png?raw=true) Values are summed inside `Player.ErgonomicsPenalty` getter - the sum is never written into EFTs own fields, so EFTs rebuilds cannot wipe it either.

![](https://github.com/harmonyzt/BrokenLimbsDontHeal/blob/main/Preview/icon_foundinraid_small.png?raw=true) Both registries de-duplicate by name (re-Add replaces) and catch provider exceptions instead of letting them leak into game. So if you do anything wrong - don't worry!

How to properly make icons
------------

If you're about to use this API to make a mod, please FOLLOW THESE RULES:
- Health icon dimensions should be 24x22. Otherwise, something can go wrong, and I won't be resposible for it
- Your image should have 1 pixel margin on the LEFT (1 pixel of transparency).
- Your image should have a 1 pixel border (to not break the EFT's vibe).
- Yes, you can reuse any image EFT already has, as long as it would not confuse players.

Can't? You can pick an existing health UI icon and redraw anything on top of it, leaving a border untouched:
[Click Here]()

Working with Fika
------------

Fika replicates health effects between players through `NetworkHealthController`.

On the receiving client the game rebuilds every incoming effect by its **class name** via `HealthHelper.EffectActivator<NetworkHealthController>.Create(...)`, which needs a registered `NetworkHealthController.Effect` class. Without it the lookup fails (the exception will be logged) and the effect **silently never appears on other players** - your own client still works, which makes this very easy to miss.

Every effect needs one extra class - same class name, same marker interface, different namespace, inheriting `NetworkHealthController.Effect`:

```csharp
public interface IRegeneration : IHealthEffect { }

// ActiveHealthController - this is what you register at all times
public class Regeneration : ActiveHealthController.Effect, IRegeneration, IEffectTriggersUIPanel
{
    public override float DefaultWorkTime => 300f;
}

// Fika - use SAME class name, SAME marker interface
namespace MyCoolMod.Network
{
    public class Regeneration : NetworkHealthController.Effect, IRegeneration
    {
    }
}
```

Hand both to the API with one registration:

```csharp
EffectsManager.RegisterEffect<Regeneration>(typeof(Plugin).Assembly, new EffectOptions
{
    DisplayName        = "Regeneration",
    IconFilePath       = "Regeneration.png",
    NetworkEffectType  = typeof(MyCoolMod.Network.Regeneration),
});
```

Limitations and Head-ups
------------
- **Every Fika player must run the same custom effect mods.** Custom effect type codes are assigned in reg order, so a missing or different mod list shifts the codes and effects resolve to the wrong class (or not at all).
- **The network class only mirrors state for the other players clients** - none of your local logic (events, penalties, rates) needs to be duplicated on it.
- **Supports Fika** - if every effect provides its `NetworkEffectType` counterpart - see **Working with Fika** above.
- **PersistAcrossRaids = true + IRestorable** needs an `OfflineHealthController.Effect` counterpart via `OfflineEffectType`.
- **Marker interface required** - EFT UI identifies effects through their `IHealthEffect` interface - declare one per effect class (EffectsManager also warns if you don't).
- **Icons are your responsibility** - `DisplayName` is captured at plugin startup. If you don't know how to make icons, look into **"How to properly make icons".**
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

**Plugin.cs** - The core part where you register effect:

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

Creation, invalidation, limb-destroy cleanup, icon, UI name and the penalty refresh all happen in API - the mod only keeps its own game rule (for example, the chance to re-break a healed bone when it gets shot).

Easy as that. It can be as complicated as you'd like, with custom events, logic and patches that trigger/set off effects.  
You can make it as simple as it can be, or go crazy. Do what you wish.

And enjoy! <3

Installation
------------
Drag and drop the contents of 7z into your root game folder.

{.endtabset}
