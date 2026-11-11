# Physiological Maintenance level

This level is a self-contained, educational gameplay approximation. It is not medical advice, a validated physiological model, or an official NASA exercise protocol.

## Open and play

1. Open `Assets/PhysiologicalMaintenance/Scenes/PhysiologicalMaintenance.unity` in Unity 6000.3.26f1.
2. Enter Play Mode. The scene's level controller builds the habitat, equipment, runtime HUD, and local event system.
3. Use the Operations Canvas or click either exercise station in the habitat to select it. UI panels are mouse/touch friendly.
4. Press **START** to begin the selected exercise. Adjust intensity and target duration before starting. Use **PAUSE / RESUME** or **STOP** to manage the session.
5. Manage oxygen regulation, thermal cooling, and waste processing with the three life-support controls. Pause the simulation separately with **PAUSE SIM**.
6. The mission ends at the configured timer, on a critical life-support condition, or when **END MISSION** is selected. Restart and the mission summary are level-local.

The scene is intentionally not listed in `EditorBuildSettings.asset`; this avoids changing shared startup/build configuration. Opening the scene directly is the supported standalone workflow.

## Mission loop and assumptions

The default mission lasts 10 simulated minutes at 6 simulated seconds per real second. The objectives are two completed exercise sessions and at least three simulated minutes of exercise. The player chooses resistance or cycle work, exercise intensity (25–100%), and 30–240 second session durations. Resistance definitions target muscle/bone metrics; cycle work targets cardiovascular fitness. Adaptations are gradual, scaled by intensity, fatigue, and training consistency. Inactivity gradually reduces metrics; idle time recovers fatigue.

Exercise draws oxygen and electrical power, adds heat, waste, and fatigue. Oxygen regulation increases reserves while drawing power; cooling lowers temperature while drawing power; waste processing frees storage while drawing power. Solar supply offsets a portion of the habitat load. If electrical delivery is insufficient, powered exercise is suspended and the enabled life-support rates are scaled by the available supply. A depleted resource does not silently recover.

The ScriptableObject defaults in `ScriptableObjects/` configure session objectives, resource capacities/rates, warning/critical thresholds, and exercise-specific effects. Thresholds are gameplay warning bands rather than real-world safety limits:

- Oxygen warns at 35% and becomes critical at 10%, leaving a visible reserve-management interval.
- Temperature warnings begin outside 18–29 °C; 14 °C and 36 °C are critical gameplay boundaries.
- Waste warns at 80% of capacity and a full store ends the mission.
- Power warns at 18 units and is critical at 3 units; power limitation has direct effects on equipment and regulation.
- Fatigue warns at 80 and strongly limits session starts near 96, making recovery and intensity trade-offs legible.

These values are intentionally exposed for tuning and should not be interpreted as biomedical or spacecraft operating specifications.

## Architecture

- `Core/PhysiologicalMaintenanceController` is the scene-local entry point, owns pause/restart and exposes a completion event/result.
- `Core/SimulationCoordinator` advances systems using simulated elapsed seconds, allocates a shared power factor, evaluates objectives and critical conditions, and produces `SimulationSnapshot`/`MissionResult` data.
- `Simulation/AstronautPhysiologySystem` and the oxygen, temperature, waste, and power models contain simulation state and calculations independently of presentation.
- `Exercise/ExerciseSession` owns one session lifecycle; `ExerciseDefinition` assets configure the training effects and resource costs.
- `UI/PhysiologicalMaintenanceUI` reads snapshots and results and sends player commands to the controller; it does not own duplicate resource/health values.
- `Core/HabitatBuilder` creates level-local placeholder geometry and `Interaction/EquipmentStation` maps the exercise equipment to the selected exercise mode.

## Integration contract

There was no shared level loader, mission manager, player-data interface, or completion contract in the inspected project. The level therefore introduces no global manager and changes no shared scene or project configuration.

A future caller can load the scene by path/name (`PhysiologicalMaintenance`) using `SceneManager.LoadScene` after adding it to that caller's chosen build configuration. Once the scene is active, obtain its `PhysiologicalMaintenanceController` and subscribe to its instance `MissionCompleted` event. The event provides a serializable `MissionResult` with the outcome, assessment, session/time totals, final physiology, life-support values, and power use. For a scene loaded additively, the caller should retain the controller reference and unload the level after receiving the result. The level does not make assumptions about returning to a particular menu.

## Verification

`Tests/Editor/SimulationVerification.cs` is an Editor-only verification runner. Run **Tools > Physiological Maintenance > Run Simulation Checks** in the Editor. It checks system rates, power limitation, physiology/fatigue, exercise progression/pause/reset, coordinated effects, and critical failure. This runner should be executed in a disposable project copy when testing is automated, so generated Unity `Library/` data does not enter the shared working tree.
