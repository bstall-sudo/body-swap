# Automatic avatar calibration

The runtime follower is named `AvatarRigFollower` (there is no separate `RigFollower` class).
`CalibrationState` selects the automatic path when `ConversationStage.automatedCalibration`
is true. It waits for Primary Action so the participant can stand upright. On confirmation,
`automatedPlayerHeightRecognition` optionally measures standing body height from XR eye height
above the local ground, divided by the existing 0.92 eye/body ratio. This confirmed snapshot is
used for scaling; subsequent head movement does not change it. The synthetic avatar T-pose
samples no participant hand pose. False retains the manual trigger sequence, with optional
height measurement at the first alignment confirmation.
Both paths call `FinishCalibration`, `SaveTargetTransformsAfterCalibration` and the existing
role metadata provider. The Session JSON calibration schema is unchanged.

`AutomaticAvatarCalibration` resolves all six bones through the Humanoid Animator first,
then exact Mixamo names with optional namespace/prefix. Bone and IK-target references come from the same evaluated T-pose with rig weights
zeroed. The sampler restores local transforms, scales, rig weights and Animator culling
mode in finally, without rebuilding the graph. Mesh bind matrices are intentionally not
used: they can describe a different orientation or pose than the Humanoid Animator.
Without an active Animator/controller, the existing authored pose is sampled. The synthetic head reference uses the existing standing eye-height
ratio (0.92), and neutral controller rotations follow the avatar root. Existing authored
avatar IK targets remain intact; their differences from these references become the local
visual target offsets, including head/hand rotation offsets. Hip/foot references use the
standing fallback; procedural hip height/back offset are used when procedural motion is on.
Missing required references prevent completion and saving and produce a UI error.

## Settings

All four settings are persisted in `ControlSettings`, transferred in `PresetController`,
and mirrored by `WorkshopConfigurationUI.Settings` across discovered toggles, including
AllSettingsPanel. AllSettingsPanel is a scene UI hierarchy, not a separate C# class.

- AutomatedPlayerHeightRecognition: measure player height on Primary Action. The measured
  value updates the current configuration and all player-height menu fields, including hidden
  mirrors, and clears unfinished player-height drafts. False retains the configured height.
- AutomatedCalibration: wait for Primary Action, then use the automatic avatar calibration path.
- ActiveRolesHeightOverwrites: true replaces active role heights with participant heights;
  false keeps configured role heights (missing heights still receive the existing fallback).
- NpcHeightOverwrites: true replaces NPC heights with participant heights; false keeps source heights.

Height overrides are applied during preparation and again after the confirmed measurement,
before scaling and target-offset generation. Standing measurement does not measure seated
height: the configured seated participant height is used for seated role overrides.
The legacy `autoPlayerSizeRecognition` field no longer controls live resizing.

Fallback hip height is derived as `heightOfPlayerCm / 200f`, including in menu display.
Incomplete numeric menu edits remain visible across page changes; runtime keeps the last
valid numeric value until another valid edit or confirmed height measurement.

## Temporary NPC limitation

Imported takes use their original calibration reference when enabled; live takeover uses
an independent current-player profile (see dual profiles below). With complete profile metadata,
NPC height changes scale both avatar and visual tracking reference consistently. Motion frames
are not resampled or retargeted: root travel, ground contacts and interactions may still need
future conversion. Legacy source recordings do not contain the original tracking scale, so the
old playback scale is preserved as a compatibility fallback instead of guessing a source height.

## Verification

Compiled the runtime Assembly-CSharp sources using Unity 6000.3.8f1's generated compiler
response file and Roslyn, including the new class. No compiler errors. Existing project
warnings remain. XR tracking, avatar pose quality, manual trigger flow and NPC playback
still require a Unity Play Mode check on the target hardware.

## Hand orientation

Automatic calibration infers each controller reference's forward direction from wrist
and middle-finger positions in the evaluated reference pose, with forearm/wrist direction
as fallback. Up follows avatar
up projected perpendicular to that direction. This replaces the shared body rotation for
both hands, avoiding a systematic sideways T-pose reference error. Palm roll still depends
on the controller grip convention; geometry alone cannot determine that convention.

Optionally add `AvatarHandCalibrationSettings` to the loaded avatar prefab. Leave reference
overrides disabled initially. Use `leftTargetRotationCorrection` and
`rightTargetRotationCorrection` for remaining model-specific roll/yaw/pitch adjustments in
local IK target axes. Start at zero, change one axis at a time (for example +/-90 degrees),
and restart calibration after every change. Exact axis/sign depends on the target rig.
Enable `overrideReferenceRotations` only when a specific controller/pose convention requires
explicit reference Euler angles in avatar-local space. Default explicit references point
left/right along the outstretched arms. These settings affect newly generated automatic
calibration only; imported session calibration and manual calibration remain unchanged.

## Reference pose regression (2026-11-16)

The Play Mode test established that the Ritter mesh bind pose puts left/right hands
on opposite sides relative to its played Humanoid T-pose. Mixing these with the
prefab IK targets created hand offsets of 176-178 cm. SchneeMann had 30-35 cm offsets.
After using the evaluated reference pose for both bone and target samples, isolated
Play Mode tests generate offsets of 0.59/2.84 cm (Ritter), 4.35/4.35 cm (SchneeMann),
and 0.25/0.95 cm (Bart). Repeated calibration yields identical positional offsets;
rig weights and original hand position are restored. Detailed measurements and the
one-shot Editor regression harness are preserved under Diagnostics. The harness is
outside Assets so it has no effect on normal editor/runtime operation.
Existing recorded calibration data is not retroactively modified; recalibrate new sessions.

## Player-sized procedural hip reference (2026-11-16)

Standing input previously reset the hip to fallbackHipHeight every frame before the
procedural solver lerped toward its fixed 0.95m authored height. At 130cm player height,
that means repeatedly starting at 0.65m, reaching only about 0.704m at 60Hz while the
calibration reference remained 0.95m. This creates about 24.6cm of unintended downward
IK target displacement, especially severe for Ritter's unusually low native pelvis.

Automatic calibration now configures the solver neutral height to fallbackHipHeight
(player height / 200) before computing offsets, and snaps the technical hip to its
neutral pose once. Standing input leaves the hip position to the procedural solver
when present/enabled by the procedural setting, allowing smoothing to converge.
Preparing a new calibration clears this runtime override so manual calibration retains
the authored solver settings. Existing JSON calibration schema is unchanged.

Play Mode regression ran the production input/solver/follower path for 300 ticks for
Bart, Ritter, SchneeMann and BaerSchweinchen at 130 and 180cm. All hip target heights
remained at their calibrated values. See Diagnostics/HipHeightRegression-2026-11-16.txt.
Recalibrate after this fix; existing saved offsets are not migrated. To simulate standing
children by lowering the headset, keep SeatedMode off: actual seated embodiment has its
own lower-body animation behavior and is not exercised by the standing regression.

## Dual calibration profiles and mixed takes (2026-11-16)

Each role now stores LiveCalibration and PreRecordedCalibration profiles in Session JSON v2.
The legacy Calibration member remains as the live/default offset set for old readers. A profile
contains all six target poses, visual/avatar scale, standing player/role heights, and procedural
hip reference/usage. These are snapshots, not mutable links to the source session.

Automatic calibration generates a current-player profile for imported roles too, then restores
the source profile for their initial playback. Manual active-role calibration still follows the
manual trigger sequence; imported-role live profiles are generated automatically and refreshed
after the manually confirmed player-height measurement. A missing source profile is wrapped
using the previous playback scale. Old recordings cannot supply metadata never recorded.

SwitchRoleToCurrentSession, live alignment and RecordingBegin select the current-player profile.
The first live switch snaps a procedural hip to its current neutral reference. The role's logical
height stays unchanged. Alignment of a previous frame uses that frame's visual scale instead of
the new player's scale. Applying a playback profile never overwrites the stage's player height.

Every newly recorded TakeMeta has a CalibrationProfile snapshot captured at recording start.
The importer copies the source take's snapshot (including repeated/nested imports), with role
profiles and usesPreRecordedCalibration as legacy fallbacks. When source calibration is disabled,
imported takes instead snapshot the current-player calibration. Both playback entry points choose
the take-specific snapshot before Begin. Thus old/new/old takes for the same role can be replayed
without leaking the previous take's scale/offsets. Uniform NPC height overrides adjust both scales
in imported snapshots when the original role height is known; motion frames remain untouched.

The source data is not overwritten and completed legacy sessions are not retroactively repaired.
Start a new session and calibrate once to create both references for future live takeover.
The isolated Play Mode regression covers 180cm source / 130cm live tracking for Bart, Ritter,
SchneeMann, JSON roundtrip, legacy paths, flag selection, height override, actual session switching
and procedural hip snapping. See Diagnostics/CalibrationProfilesRegression-2026-11-16.txt.
