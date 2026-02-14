# RimWorld Manufactory

## Mod Idea

*Manufactory* is a Rimworld 1.6 mod designed to fit seamlessly into the vanilla workflow.

The usual vanilla Crashlanded start goes something like:
Logistics Setup → Shelter → Food → Power → Research → Defense → Stability → Expansion

I'm making *Manufactory* to fill in that fuzzy area between Defense and Stability, and it also contains (or will eventually contain) lots of content for the expansion phase.

Where *Manufactory* might fit into the Crashlanded start:
Logistics Setup → Shelter → Food → Power → Research → Defense → Stability → ***Manufactory*** → Expansion

The current factory/production mods in RimWorld seem unbalanced and kinda broken. I'll do my best to make this mod as vanilla-balanced without just piling up more things to worry about in the already-overflowing RimWorld task list.

## Status 
Early development. Like, **VERY** early.

## Expected Features
- Concrete
- Catwalks (I'm finding this to be very difficult to create)
- Conveyor Belts
- Logistical Tunnels (Conveyor belts and utilities only, so no pawns in tunnels)
- Tier 1 Machines
- Tier 1 Industrial Plants

## Major Progress So Far
- Curing System for concrete implemented and made more efficient
- Concrete Mixer added and made cool
- Finally starting to use "git push" less often (still might overload the history tho)
- Concrete Wall Textured added
- Paved Concrete and Concrete added

## What Needs Work
- Compatibility with other mods (no issues found so far)
- Implementing more than just concrete

## Random Stuff (Skip if you don't care)
Turns out RimWorld doesn't really have a way to change out TerrainDefs on a tick-based timer. You can kinda do that with ThingDefs, but it's basically impossible without a Harmony patch for TerrainDefs. So, I made a curing system that fixes that.

The 8% walk speed boost to concrete flooring is meant to incentivize people to use it more, especially in factories. Also, Google says you walk faster on concrete so yeah ¯\_(ツ)_/¯

I'm considering making it so that machines/plants require concrete flooring to be placed down, like how VFE-Mechanoids factory flooring works. But a hardlock like that is honestly annoying, so I might just incentivize it by making factories more efficient if they're built on concrete.

I know that the way concrete works and hardens in this mod isn't very realistic, but I don't really care. RimWorld isn't super realistic either.

For the catwalks, I'm thinking of creating a "2nd floor" system like those RimWorld Z-level mods, but I'll keep the map underneath rendering, and limit cell computing stuff to where the catwalks are built, and limit the catwalks explicity to traversing, so no building or anything while up on the walkways. Originally, I was gonna do a PawnDraw system like how Jump Packs and Caravaning does it, but it looks cheap and doesn't work the way I want it to.

**SHOUTOUT**: Thank you Poured Concrete, Fortifications-Industrial, VFE-Mechanoids, and Project RimFactory Revived for inspiring a lot of the features in this mod! Don't worry, I didn't steal their code (only took a peek).

## Note
I am using OpenAI Codex to assist with the creation of this mod. I might make this mod function as a "framework" that I can expand upon, rather than putting every idea into a single mod.



Thanks for reading!

\- Luke Porter