# DressSaver

<img src="images/icon.png" width="128" align="right" alt="DressSaver icon">

A Dalamud plugin for FINAL FANTASY XIV that saves the outfit your character is currently wearing as a clean, well-organized **Penumbra mod**, together with its **Glamourer design**.

Tired of a Penumbra mod list full of loose clothing mods? Put an outfit on, type `/drss`, click **Analyze**, then **Create mod**. You get one tidy mod per outfit.

---

## Features

- **One mod per outfit**: clothes and their metadata, plus optional **Accessories**, **Hairstyle** and **Makeup** groups you can toggle in Penumbra.
- **Glamourer design** for each outfit (gear, dyes, hairstyle, makeup only), saved directly in Glamourer's design list, and optionally applied to your character right after creation. It's also copied to your clipboard and saved next to the mod.
- **Automatic install** in Penumbra.
- **Conflict detection**: compares each piece with your installed mods.
  - Identical files are marked as **Shared**: they are harmless.
  - Different files are marked as **Conflict**, with a searchable list of **free items** (not touched by any of your mods or syncs) to move the piece onto. You can also choose to ignore the conflict.
- **Credits**: the description of every created mod lists each piece with its original mod and modder (`Piece - Mod - Author`).
- **Disable original mods** (optional): once the new mod is installed and enabled, the mods the outfit came from are disabled. **They are never deleted**, so you can turn them back on at any time.
- **My outfits** tab: lists every outfit made with DressSaver.
  - **Wear** enables the outfit's mod and applies its Glamourer design in one click. The design is found automatically even if its name is slightly different (accents, dashes, shorter name); otherwise you can pick it from the list.
  - **Update** replaces a saved outfit with what you're wearing now (same name, same place in Penumbra and Glamourer).
- **Name check**: warns you if an outfit with the same name already exists, and offers to replace it. A mod that wasn't made by DressSaver is never replaced.
- **Pre-Dawntrail warning**: pieces (and hairstyles) still using the old model format are flagged, because they may show up invisible or broken in game.
- **Dependency check**: makes sure Penumbra and Glamourer are installed, enabled and up to date before doing anything.
- **4 languages**: English, French, Spanish and German. By default, the plugin uses Dalamud's language.
- **Diagnostics** panel to help troubleshoot.

DressSaver only reads what **your own character** is wearing, using the mods **you** have installed. It never touches other players' appearances or MCDF files.

---

## Requirements

- [Penumbra](https://github.com/xivdev/Penumbra)
- [Glamourer](https://github.com/Ottermandias/Glamourer)

## Installation

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste:
   `https://raw.githubusercontent.com/Lenile/DressSaver/main/repo.json`
3. Click **+**, tick the **Enabled** box, then **Save and Close**.
4. Type `/xlplugins`, search for **DressSaver** and install it.

## Usage

1. Put the outfit on your character (with Glamourer and your mods enabled).
2. Type `/drss` (or `/dresssaver`).
3. Give the outfit a name and choose what to include (accessories, hairstyle, makeup).
4. Click **Analyze current outfit** and check the pieces, conflicts and credits.
5. Click **Create mod**.

To update a saved outfit later: put it on, change what you want, open the **My outfits** tab and click **Update**.

> **Note:** if "Automatically disable the original mods" is enabled, your game may freeze for a few seconds while Penumbra applies the changes. Just wait!

## Good to know

- Nude body pieces (Bibo+/TBSE body slots `e0000` and `e0279`), face, skin and tail are **never** included: they belong in your body mod.
- A hairstyle can't be moved to another item. If two outfits use different files for the same hairstyle, DressSaver warns you.
- Updating or replacing an outfit deletes **only the old DressSaver mod and its design**. Your original mods are never deleted.
- When a piece is moved to a free item, DressSaver copies its settings (hidden body parts, material variant) and forces the right model for your race. Check the result in game the first time.

---

## Building from source

The plugin is built automatically by GitHub Actions on every push to `main` (see `.github/workflows/build.yml`), and published as a release.

To release a new version:

1. Increase `Version` in `DressSaver/DressSaver.csproj` (for example `0.2.5.0`).
2. Set the same value for `AssemblyVersion` in `repo.json`.
3. Push your changes. Dalamud will offer the update once the build is done (allow a few minutes for GitHub's cache).

## Credits

Made by Lénile.
