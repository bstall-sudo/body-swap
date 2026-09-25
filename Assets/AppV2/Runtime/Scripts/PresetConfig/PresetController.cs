using System;
using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.Dialogue;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Config
{
    public class PresetController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        private ConversationStage conversationStage;

        private PresetStore _presetStore;


        // ============================================================
        // UNITY
        // ============================================================

        private void Awake()
        {
            _presetStore = new PresetStore();

            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] ConversationStage is not assigned."
                );
            }
        }


        // ============================================================
        // CREATE CONFIG FROM CURRENT CONVERSATION STAGE
        // ============================================================

        public PresetConfig CreateConfigFromCurrentStage(
            string presetId,
            string presetName)
        {
            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] Cannot create preset: " +
                    "ConversationStage is null."
                );

                return null;
            }

            PresetConfig config = new PresetConfig
            {
                PresetId = presetId,
                PresetName = presetName,
                ConfigVersion = 1
            };


            ReadEnvironmentSettings(config.Environment);

            ReadConversationSettings(config.Conversation);

            ReadPlayerSettings(config.Player);

            ReadControlSettings(config.Controls);

            ReadStartRolesSettings(config.StartRoles);

            ReadNpcGroupSettings(config.NpcGroups);


            Debug.Log(
                $"[PresetController] Created config from current stage: " +
                $"'{presetName}' ({presetId})"
            );

            return config;
        }


        // ============================================================
        // ENVIRONMENT
        // ============================================================

        private void ReadEnvironmentSettings(
            EnvironmentSettings settings)
        {
            settings.EnvironmentId =
                conversationStage.EnvironmentId;

            settings.StageSpawnId =
                conversationStage.StageSpawnId;
        }


        // ============================================================
        // CONVERSATION
        // ============================================================

        private void ReadConversationSettings(
            ConversationSettings settings)
        {
            settings.LoseConversationPartnersIfTooFarAway =
                conversationStage.loseConversationPartnersIfTooFarAway;

            settings.DistanceToLoseConversationPartner =
                conversationStage.distanceToLoseActiveConversationPartner;

            settings.DistanceToReactivatePassiveRole =
                conversationStage.distanceToReactivatePassiveRole;

            settings.RadiusNpcStartTalking =
                conversationStage._radiusNpcStartTalking;

            settings.MaxHeightDifferenceForNpcTrigger =
                conversationStage._maxHeightDifferenceForNpcTrigger;
        }


        // ============================================================
        // PLAYER
        // ============================================================

        private void ReadPlayerSettings(
            PlayerSettings settings)
        {
            settings.HeightOfPlayerCm =
                conversationStage.heightOfPlayerCm;

            settings.HeightOfSeatedPlayerCm =
                conversationStage.heightOfSeatedPlayerCm;

            settings.FallbackRootSpacing =
                conversationStage.fallbackFootSpacing;

            settings.FallbackHipHeight =
                conversationStage.fallbackHipHeight;

            settings.FallbackFootSpacing =
                conversationStage.fallbackFootSpacing;

            settings.AvatarBaseHeightCm =
                conversationStage.avatarBaseHeightCm;
        }


        // ============================================================
        // CONTROLS
        // ============================================================

        private void ReadControlSettings(
            ControlSettings settings)
        {
            settings.SimpleInputMode =
                conversationStage.simpleInputMode;

            settings.FullBodyTrackers =
                conversationStage.FullBodyTrackers;

            settings.SeatedMode =
                conversationStage.SeatedMode;

            settings.AllowTeleportation =
                conversationStage.allowTeleportation;

            settings.ProceduralHipAndFeetMove =
                conversationStage.ProceduralHipAndFeetMove;

            settings.SmoothAlignSeconds =
                conversationStage.SmoothAlignSeconds;

            settings.SelectableNext =
                conversationStage.selectableNext;
        }


        // ============================================================
        // START ROLES
        // ============================================================

        private void ReadStartRolesSettings(
            StartRolesSettings settings)
        {
            settings.Roles.Clear();

            settings.RoleCount =
                conversationStage.roleCount;

            for (int i = 0; i < conversationStage.roleCount; i++)
            {
                RoleRig role =
                    conversationStage.roles[i];

                if (role == null)
                {
                    Debug.LogWarning(
                        $"[PresetController] Role {i} is null."
                    );

                    continue;
                }

                if (role.isActiveConversationPartner == false)
                {
                    continue;
                }

                StartRoleSettings roleSettings =
                    new StartRoleSettings
                    {
                        RoleId = role.roleId,

                        AvatarId = role.avatarId,

                        HeightOfRoleCm =
                            role.heightOfRoleCm,

                        HeightOfSeatedRoleCm =
                            role.heightOfSeatedRoleCm,

                        HasInitialStartPose =
                            role.hasInitialStartPose,

                        InitialStartPos =
                            role.initialStartPos,

                        InitialStartYawDeg =
                            role.initialStartYawDeg
                    };

                settings.Roles.Add(roleSettings);
            }

            settings.RoleCount = settings.Roles.Count;
        }


        // ============================================================
        // NPC GROUPS
        // ============================================================

        private void ReadNpcGroupSettings(
            List<NpcGroupSettings> settings)
        {
            settings.Clear();

            foreach (var npc in conversationStage.preRecordedScenes)
            {
                if (npc == null)
                    continue;

                NpcGroupSettings npcSettings =
                    new NpcGroupSettings
                    {
                        Enabled =
                            npc.enabled,

                        NpcGroupId =
                            npc.npcGroupId,

                        CanBecomeActiveConversationPartner =
                            npc.canBecomeActiveConversationPartner,

                        WorkshopFolderName =
                            npc.workshopFolderName,

                        SessionFolderName =
                            npc.sessionFolderName,

                        SessionId =
                            npc.sessionId,

                        AlignWithPlayer =
                            npc.alignWithPlayer,

                        NpcGroupSpawnId =
                            npc.npcGroupSpawnId
                    };

                settings.Add(npcSettings);
            }
        }


        // ============================================================
        // SAVE CURRENT STAGE AS PRESET
        // ============================================================

        public bool SaveCurrentStageAsPreset(
            string presetId,
            string presetName)
        {
            PresetConfig config =
                CreateConfigFromCurrentStage(
                    presetId,
                    presetName
                );

            if (config == null)
                return false;

            return _presetStore.SavePreset(config);
        }


        // ============================================================
        // LOAD PRESET
        // ============================================================

        public PresetConfig LoadPreset(string presetId)
        {
            return _presetStore.LoadPreset(presetId);
        }

        public bool ApplyPresetConfig(PresetConfig config)
        {
            if (config == null)
            {
                Debug.LogError(
                    "[PresetController] ApplyPresetConfig failed: config is null."
                );

                return false;
            }

            if (conversationStage == null)
            {
                Debug.LogError(
                    "[PresetController] ApplyPresetConfig failed: " +
                    "ConversationStage is null."
                );

                return false;
            }

            ApplyEnvironmentSettings(config.Environment);
            ApplyConversationSettings(config.Conversation);
            ApplyPlayerSettings(config.Player);
            ApplyControlSettings(config.Controls);

            //ApplyStartRoleSettings(config.StartRoles);
            //ApplyNpcGroupSettings(config.NpcGroups);

            Debug.Log(
                $"[PresetController] Applied preset " +
                $"'{config.PresetName}' ({config.PresetId})"
            );

            return true;
        }

        // ============================================================
        // APPLY ENVIRONMENT
        // ============================================================

        private void ApplyEnvironmentSettings(
            EnvironmentSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] EnvironmentSettings are null."
                );
                return;
            }

            conversationStage.SetEnvironmentId(settings.EnvironmentId);
                

            conversationStage.SetStageSpawnId(settings.StageSpawnId);
        }


        // ============================================================
        // APPLY CONVERSATION
        // ============================================================

        private void ApplyConversationSettings(
            ConversationSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] ConversationSettings are null."
                );
                return;
            }

            conversationStage.loseConversationPartnersIfTooFarAway =
                settings.LoseConversationPartnersIfTooFarAway;

            conversationStage.distanceToLoseActiveConversationPartner =
                settings.DistanceToLoseConversationPartner;

            conversationStage.distanceToReactivatePassiveRole =
                settings.DistanceToReactivatePassiveRole;

            conversationStage._radiusNpcStartTalking =
                settings.RadiusNpcStartTalking;

            conversationStage._maxHeightDifferenceForNpcTrigger =
                settings.MaxHeightDifferenceForNpcTrigger;
        }


        // ============================================================
        // APPLY PLAYER
        // ============================================================

        private void ApplyPlayerSettings(
            PlayerSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] PlayerSettings are null."
                );
                return;
            }

            conversationStage.heightOfPlayerCm =
                settings.HeightOfPlayerCm;

            conversationStage.heightOfSeatedPlayerCm =
                settings.HeightOfSeatedPlayerCm;

            conversationStage.fallbackFootSpacing =
                settings.FallbackRootSpacing;

            conversationStage.fallbackHipHeight =
                settings.FallbackHipHeight;

            conversationStage.fallbackFootSpacing =
                settings.FallbackFootSpacing;

            conversationStage.avatarBaseHeightCm =
                settings.AvatarBaseHeightCm;
        }


        // ============================================================
        // APPLY CONTROLS
        // ============================================================

        private void ApplyControlSettings(
            ControlSettings settings)
        {
            if (settings == null)
            {
                Debug.LogWarning(
                    "[PresetController] ControlSettings are null."
                );
                return;
            }

            conversationStage.simpleInputMode =
                settings.SimpleInputMode;

            conversationStage.FullBodyTrackers =
                settings.FullBodyTrackers;

            conversationStage.SeatedMode =
                settings.SeatedMode;

            conversationStage.allowTeleportation =
                settings.AllowTeleportation;

            conversationStage.ProceduralHipAndFeetMove =
                settings.ProceduralHipAndFeetMove;

            conversationStage.SmoothAlignSeconds =
                settings.SmoothAlignSeconds;

            conversationStage.selectableNext =
                settings.SelectableNext;
        }

        [ContextMenu("TEST - Save Current Stage As Preset")]
        private void TestSaveCurrentStageAsPreset()
        {
            bool success = SaveCurrentStageAsPreset(
                "test_001",
                "Test Preset"
            );

            Debug.Log(
                success
                    ? "[PresetController] TEST preset saved successfully."
                    : "[PresetController] TEST preset save FAILED."
            );
        }

        [ContextMenu("TEST - Load Preset")]
        private void TestLoadPreset()
        {
            PresetConfig config =
                LoadPreset("test_001");

            if (config == null)
            {
                Debug.LogError(
                    "[PresetController] TEST preset could not be loaded."
                );

                return;
            }
            ApplyPresetConfig(config);

            Debug.Log(
                $"[PresetController] TEST preset loaded:\n" +
                $"Name: {config.PresetName}\n" +
                $"Environment: {config.Environment.EnvironmentId}\n" +
                $"StageSpawn: {config.Environment.StageSpawnId}\n" +
                $"PlayerHeight: {config.Player.HeightOfPlayerCm}\n" +
                $"StartRoles: {config.StartRoles.Roles.Count}\n" +
                $"NpcGroups: {config.NpcGroups.Count}"
            );
        }
    }
}