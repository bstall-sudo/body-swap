# Role start placement and initial participation

RoleSettingsRolePrefab now binds the spawn dropdown, the look-target dropdown and
isActiveConversationPartnerToggle independently (the two dropdown GameObjects originally
share the same name). WorkshopConfigurationUI supplies the EnvironmentLoader explicitly,
so spawn choices do not depend on canvas ancestry. Changing environment or StageSpawnPoint
refreshes the choices. A missing saved ID stays visible rather than silently changing a preset.

StartRoleSettings persists SpawnPointId, LookAtSpawnPointId and the initial conversation
partner flag. Empty SpawnPointId follows the currently selected StageSpawnPoint. Empty
LookAtSpawnPointId keeps normal placement facing. Looking direction means looking toward
the selected point's position, projected onto the ground plane, rather than copying its rotation.
HasConversationPartnerSetting distinguishes a legacy missing toggle (active) from explicit false.

## Placement

RolePlacementUtility.BuildStartPoses groups all configured non-imported roles by spawn point.
Initially passive roles are still physically placed/calibrated and belong to their spawn group.
Imported NPCs retain their source-scene/group-anchor placement path.

- Default selection and explicit selection of the current StageSpawnPoint share one group.
- Each other spawn point has its own group, independent of all other group sizes/order.
- One member is placed at the group's center; multiple members retain the existing 1.5m
  circle arrangement, oriented in that spawn's yaw frame and facing their group center.
- An explicit look target overrides facing only; it does not remove the member from its
  circle slot or create overlapping positions.
- Terrain height is resolved at each final position. Missing spawn IDs fall back to the
  stage group, missing/degenerate look targets retain default facing.

AvatarPlacementState applies these poses and seeds hasInitialStartPose, initialStartPos
and initialStartYawDeg through AvatarCalibrationController.PlaceRoleAt. RecordingController
already uses those values when starting the first take or aligning before any previous take.
Imported NPC placement is applied directly after source-to-stage conversion so it is not
resolved again, and its ground-adjusted first pose is seeded too.

## Runtime participation

isActiveConversationPartner is a RoleRig field (one flag per role), not a global stage switch.
False keeps the figure visible/placed/calibrated but out of speaker/listener selection.
Existing proximity activation can recruit passive non-imported figures during runtime. Explicit
scene interactions can use FlowController.SetConversationPartnerActive(roleIndex, true),
which updates active/passive membership and listener availability. Imported NPCs still must
finish their prerecorded phase before explicit activation is allowed.

While choosing a role, inspector flag changes are reflected too. All-passive startup waits in
ChooseSpeakerState until a role is activated. Sparse indices (for example only B and D active)
are preserved; indices are not renumbered or treated as 0..activeCount-1. BuildActiveRoles
refreshes references without resetting flags or overwriting an imported source role index.

## Playback and persistence

Presets store point IDs, so starting a new recording recomputes groups for the chosen environment.
Session metadata instead stores the final technical-root start position and rotation relative to
the stage, plus the authored point IDs and initial participation flag for traceability. The original
initial flag is preserved even when a role becomes active later in the recording.

Playback loads StartRootPose as authoritative and does not circle-place roles again. Body frames
already store positions/yaws in stage coordinates, so distant initial positions work with the same
TakePlayer.ApplyFrame path. The old spawn-only loader fallback remains for sessions without a
start pose. Full-conversation playback considers all roles, including those initially passive.

When the entire recorded scene is imported as an NPC group, its stored stage-local role layout
is transformed once relative to the chosen NPC group anchor. Individual authored spawn IDs are
not reapplied on top of those offsets. Changing a spawn marker later does not rewrite old takes.

## Verification

The isolated Play Mode regression exercises production placement, transformed stage/group frames,
ground height, explicit facing, yaw seeding, imported NPC placement, sparse/all-passive membership,
runtime activation, reference rebuild, actual extended prefab bindings/events, environment option
refresh, preset/session roundtrip and TakePlayer frame placement. The fixture uses synthetic spawn
points/input and does not exercise a complete headset/audio recording. Source prefabs are not
rewritten by the fixture; the only intended prefab change is the three new component references.
The one-shot regression source/report are archived in Diagnostics outside Assets.
