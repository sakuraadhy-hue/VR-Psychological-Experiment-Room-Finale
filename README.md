# VR Psychological Experiment Room — Calm vs. Stress vs. Neutral

Final project for **Fundamentals of eXtended Reality** (Master HCI, Université Paris-Saclay, 2026).

A virtual experiment room for psychological research. A participant performs a timed colour-sorting task while the room is set to one of three environmental conditions. The application records task performance and a short questionnaire to a CSV file, so a researcher can compare how the environment affects speed, accuracy and perceived stress.

**Engine:** Unity 6.3 LTS (`6000.3.25f1`), Universal Render Pipeline · **Platform:** desktop (keyboard + mouse)

---

## Group members and contributions

| Member | Role | Contributions |
| --- | --- | --- |
| **Vida Kayla Bensing** (Member A) | Room, environments & lighting | Git repository and Unity project setup; room shell modelling (walls, floor, ceiling, table); `MainExperiment` scene; Neutral / Calm / Stress environment sets (furniture, plants, lighting, audio); condition switching (`EnvironmentManager.cs`); flickering fluorescent light (`LightFlicker.cs`); sourcing of 3D models and audio |
| **Anastasia Ivanova** (Member B) | Locomotion, manipulation & physics | Gliding/flying navigation (`GlidingLocomotion.cs`); ray-cast pickup at an offset distance (`RaycastGrabber.cs`); grabbable balls and sorting bins (`SortingItem.cs`, `SortingBin.cs`, `SortingStation.cs`); physics materials and Rigidbody tuning; 3D feedback sounds; demonstration video |
| **Sakura Adhikary** (Member C) | Experiment flow, UI & data logging | Experiment state machine (`ExperimentManager.cs`); 60 s countdown timer and live score (3D TextMeshPro); world-space briefing, survey and completion canvases; clickable 3D push buttons (`RemoteButton.cs`); researcher control panel; Likert questionnaire (`SurveyPanel.cs`, `LikertQuestion.cs`); CSV logging (`DataLogger.cs`); reset routine; editor tools that build the UI and connect the environment sets (`ExperimentUIBuilder.cs`); bin-hover assist in the grabber; integration of the environment sets with the panel; this README |

All members: integration in the main scene, end-to-end testing and code comments.

---

## Experimental design

The participant sorts 15 coloured balls (red, green, blue) into the bin of the matching colour within **60 seconds**. Before the trial, the researcher selects one of three conditions:

| Condition | Environment | Purpose |
| --- | --- | --- |
| **Neutral** | Static neutral-white ceiling light (4500 K), plain office furniture, silence | Control baseline |
| **Calm** | Warm amber lights (2700 K), potted plants and wooden decoration, calm nature soundtrack (3D) | Restorative environment |
| **Stress** | Daylight switched off, cold flickering fluorescent light (6500 K), red lighting, alarm music (3D) and a ticking clock | High cognitive load |

**Measured variables**

- **Score**: balls placed in the correct bin (task throughput)
- **Errors**: balls placed in a wrong bin (accuracy)
- **Time remaining**: seconds left if all balls were sorted before the timer ran out
- **Stress, Focus and Comfort ratings**: 1–5 Likert questionnaire after each trial

The condition can only be changed before a trial starts, so each participant experiences a single condition for the whole trial.

---

## How the application works

1. **Briefing.** The room starts in the Neutral condition. A canvas on the north wall shows the instructions and controls.
2. **Condition.** The researcher clicks **NEUTRAL**, **CALM** or **STRESS** on the panel on the west wall (or presses 1 / 2 / 3). The `ExperimentManager` asks the `EnvironmentManager` to activate the matching environment set and deactivate the other two. The selected button stays pushed in.
3. **Start.** The participant clicks the green **START EXPERIMENT** button. The canvas is hidden, revealing a 3D digital clock that counts down from `01:00.0` with a live `Score | Errors` display. Grabbing is unlocked.
4. **Sorting.** The participant picks up balls with the mouse and drops them into the bins. Each bin plays a chime (correct) or a buzzer (wrong) from its position in 3D space.
5. **End of trial.** When the timer reaches zero, or all 15 balls are sorted, grabbing is locked and the questionnaire appears.
6. **Data.** After the participant answers the three questions and clicks **SUBMIT ANSWERS**, one row is added to `results.csv`.
7. **Reset.** The researcher clicks **RESET**. All balls return to the table, the timer and counters are reset, the room returns to Neutral and the next participant ID (P001, P002, …) is prepared.

---

## Implemented features

- **Navigation (gliding/flying metaphor):** smooth, accelerated gliding in the look direction with WASD, rising/descending with E/Q; a CharacterController prevents passing through walls and the table.
- **Manipulation by ray casting at an offset distance:** a ray from the camera through the cursor picks up a ball, which then floats along the ray; the scroll wheel changes its distance. When the cursor points at a bin, the held ball hovers above the bin's opening so that releasing it drops it in.
- **Activation / deactivation at runtime:** environment sets per condition, the participant canvas, the START button, and sorted balls (hidden, then restored on reset).
- **Canvas + 3D TextMeshPro:** world-space canvases (briefing, questionnaire with UI buttons, completion message) and 3D TextMeshPro text (countdown clock, score, researcher status screen, button labels).
- **Physics:** Rigidbody balls with physics materials (bounce and friction), trigger colliders in the bins, kinematic holding while carried.
- **Lighting controlled during the interaction:** a different lighting setup per condition, including a scripted flickering fluorescent light in the Stress condition.
- **3D audio:** spatialised feedback sounds on each bin and ambient sound sources for the Calm and Stress conditions.
- **Countdown timer:** 60 s timer driven by `Time.deltaTime`, turning red in the last 10 seconds.
- **Scripting of behaviours and logic:** experiment state machine, event-based scoring (`SortingBin.OnItemSorted`), survey validation, CSV logging and a complete reset.
- **Reset button:** returns every interactive object, the timer, the UI and the environment to their initial state for the next participant.

---

## Controls

| Input | Action |
| --- | --- |
| W / A / S / D | Glide forward / left / back / right (in the look direction) |
| E / Q | Rise / descend |
| Left Shift | Glide faster |
| Hold right mouse button + move mouse (or arrow keys) | Look around |
| Hold left mouse button on a ball | Pick it up; release to drop |
| Scroll wheel | Move the held ball nearer / further |
| Left click | Press 3D buttons and questionnaire buttons |

**Researcher controls**

| Control | Action |
| --- | --- |
| NEUTRAL / CALM / STRESS buttons (west wall), or keys 1 / 2 / 3 | Select the condition (only before a trial starts) |
| RESET button (west wall) | Prepare the room for the next participant (also aborts a running trial without saving it) |
| Status screen (west wall) | Shows the current condition, trial state and participant ID |

---

## Running the project

1. Open the project folder in **Unity 6.3 LTS (6000.3.25f1)** through Unity Hub.
2. Open `Assets/Scenes/MainExperiment.unity`.
3. Press **Play**.

If the experiment UI is missing from the scene, it can be rebuilt with **Tools → Experiment → Build Experiment UI**.

---

## Output data (`results.csv`)

In the Unity Editor the file is written to the project folder (next to `Assets`); in a built application it is written to `Application.persistentDataPath`. The full path is printed in the Console when Play mode starts. One row is added per completed trial:

```
ParticipantID,Condition,Score,Errors,TimeRemaining,StressRating,FocusRating,ComfortRating,Timestamp
P001,Stress,14,3,0.0,4,2,1,2026-10-19 18:29:32
```

| Column | Meaning |
| --- | --- |
| `ParticipantID` | P001, P002, … (increases after each saved trial) |
| `Condition` | Neutral, Calm or Stress |
| `Score` | Balls sorted into the correct bin |
| `Errors` | Balls sorted into a wrong bin |
| `TimeRemaining` | Seconds left when the trial ended (0.0 if the timer ran out) |
| `StressRating` | 1 (none) to 5 (extreme) |
| `FocusRating` | 1 (distracted) to 5 (very focused) |
| `ComfortRating` | 1 (uncomfortable) to 5 (very comfortable) |
| `Timestamp` | Date and time the answers were submitted |

Numbers always use a dot as the decimal separator, so the file opens correctly in Excel, R or Python on any system language. Participant numbering continues from the rows already in the file.

---

## Main scripts

| Script | Description |
| --- | --- |
| `ExperimentManager.cs` | Trial state machine, conditions, timer, score, survey and reset |
| `DataLogger.cs` | Appends trial results to `results.csv` |
| `RemoteButton.cs` | Clickable 3D push button with press animation |
| `SurveyPanel.cs` / `LikertQuestion.cs` | Post-trial questionnaire |
| `Editor/ExperimentUIBuilder.cs` | Editor menu tools that build the experiment UI and connect the environment sets |
| `EnvironmentManager.cs` | Activates the Neutral, Calm or Stress set and controls the daylight |
| `GlidingLocomotion.cs` | Gliding/flying navigation |
| `RaycastGrabber.cs` | Ray-cast pickup at an offset distance, with bin-hover assist |
| `SortingItem.cs` / `SortingBin.cs` / `SortingStation.cs` | Balls, bins with trigger detection, station reset and grabbing control |
| `LightFlicker.cs` | Erratic fluorescent flicker for the Stress condition |

---

## Asset sources

| Asset | Author / source | Licence |
| --- | --- | --- |
| [Low-Poly Office Set #1 [+140 Models]](https://assetstore.unity.com/packages/3d/props/low-poly-office-set-1-140-models-vnb-327126) | VNB Productions, Unity Asset Store | Standard Unity Asset Store EULA |
| [Office Pack - Free](https://assetstore.unity.com/packages/3d/props/interior/office-pack-free-258600) | nappin, Unity Asset Store | Standard Unity Asset Store EULA |
| [Furniture Set (FREE)](https://assetstore.unity.com/packages/3d/props/furniture/furniture-set-free-242389) | DEXSOFT, Unity Asset Store | Standard Unity Asset Store EULA |
| [Stylized Potted Plants](https://assetstore.unity.com/packages/3d/vegetation/plants/stylized-potted-plants-290047) | SandoraKa, Unity Asset Store | Standard Unity Asset Store EULA |
| ["Hacker Alarm"](https://pixabay.com/music/electronic-hacker-alarm-124960/) (Stress music) | Pixabay user 4379051 | Pixabay Content License |
| ["Clock Ticking SFX"](https://pixabay.com/sound-effects/clock-ticking-sfx-467486/) | DRAGON-STUDIO, Pixabay | Pixabay Content License |
| "Beneath the Ancient Boughs" (Calm music) | AI-generated by the team with Google Gemini (October 2026), prompt: "generate a calm, nature ambient soundtrack" | Generated for this project; no third-party rights claimed |

## Demonstration video

[Watch the demonstration video (Google Drive)](https://drive.google.com/file/d/11mBtSWeg66gGcRYjUdlnh5osl2c3sULh/view?usp=sharing)

The video shows navigation, object pickup, condition switching, the sorting task, survey completion, the new row in `results.csv`, and the room reset.

---

## Use of AI tools

Claude (Anthropic) was used as an assistant to help write and debug the experiment UI, timer, questionnaire and data-logging scripts, the editor UI-building tool, and the bin-hover assist, and to help draft this README. All features were tested in Unity by the group.
