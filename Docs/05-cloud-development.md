# Prototype and cloud development

## Current implementation boundary

This is the independent Unity project source. No Unity editor, licensed Photon SDK or account is bundled. The legacy browser project and Render remain fully separate. A successful Node check is not a Unity compile, CodeGen check, or playable match.

1. Install the Unity version in `ProjectSettings/ProjectVersion.txt` with URP and target build modules.
2. Obtain Quantum 3.1.0 (build 2313) through the official Photon download under your own license. Import it and install Quantum User Files via Quantum Hub. Alternatively, scaffold a Universal 3D project in the editor first and overlay this repository, preserving the SDK-generated assets, settings and assemblies.
3. Run the official QTN CodeGen command and resolve all errors before proceeding. Do not hand-edit generated source.
4. Use **Ash & Iron > Import reference rules and maps** to create deterministic assets from the checked-in generated JSON. Assign Rules and a selected map asset to the `AshRules` and `AshMap` fields of RuntimeConfig. Select both races and the second seat's computer flag. This bootstrap currently assumes two fixed seats and does not implement AI or actual player-join handling.
5. Add `AaiOpeningSystem`, `AaiCommandSystem`, then `AaiMovementSystem` to the SDK's user system setup (preserve the SDK's necessary systems). Register `AaiMoveUnitCommand` in the SDK's command setup. Use the actual generated SDK hooks, not a second partial method implementation. Verify the assets and scene configuration on both clients. This integration is not yet performed.
6. Run the `AshAndIron.Tests.AshAndIronPrototypeTests` EditMode tests in the Unity Test Runner. Test the SDK's own local example scene before attempting the Ash & Iron prototype. A scene, input bridge and entity presentation are still needed for a playable prototype.

## Cloud testing

The `Reference integrity` workflow checks the data in this repository on push. `Quantum Unity validation` is manual and requires a licensed SDK in the runner's project, SDK-generated user files, and valid Unity licensing. It deliberately fails its SDK preflight rather than pretending that an SDK-free build passes. For public repositories, keep the SDK in an authorized private build workspace or runner and configure its installation there. Do not commit licensed third-party packages or Photon app settings to public history. No paid subscription or cloud runner is ordered by this project.

An authorized maintainer must configure Unity licensing through GitHub's secrets or other supported secure license mechanism, not in a chat or source file. Consult the [GameCI licensing documentation](https://game.ci/docs/github/activation/) and the applicable Unity and Photon licensing terms before enabling the build. Photon app configuration is only necessary for online play, not the first offline tests.

## First playable gate

- Clean SDK import, QTN CodeGen and C# compilation.
- Opening test passes for every race, including mirrors and correct bank/population.
- Move/stop ownership, valid destinations and blocked terrain tests pass.
- Actual deterministic commands are registered and exercised through a running session.
- Create and view the first playable scene with camera and unit selection.
- Compare verified-frame checksums for identical command traces, then test two clients.

After this gate, port real grid navigation, combat, economy and queues from the legacy simulation in small tested increments. Do not call the existing straight-line movement system a pathfinder or claim SDK tests are passing until the runner has executed them. Add production art and mobile UI independently of gameplay rules, and benchmark the actual native build before replacing the browser game.
