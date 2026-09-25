using System;
using System.Collections.Generic;
using UnityEngine;

namespace AppV2.Runtime.Scripts.Config
{
    [Serializable]
    public class PresetConfig
    {
        // ------------------------------------------------------------
        // PRESET META DATA
        // ------------------------------------------------------------

        public string PresetId;
        public string PresetName;
        public int ConfigVersion = 1;


        // ------------------------------------------------------------
        // SETTINGS
        // ------------------------------------------------------------

        public EnvironmentSettings Environment = new();
        public ConversationSettings Conversation = new();
        public PlayerSettings Player = new();
        public ControlSettings Controls = new();
        public StartRolesSettings StartRoles = new();

        public List<NpcGroupSettings> NpcGroups = new();
    }


    // ================================================================
    // ENVIRONMENT
    // ================================================================

    [Serializable]
    public class EnvironmentSettings
    {
        public string EnvironmentId = "default";
        public string StageSpawnId = "default";
    }


    // ================================================================
    // CONVERSATION
    // ================================================================

    [Serializable]
    public class ConversationSettings
    {
        public bool LoseConversationPartnersIfTooFarAway = true;

        public float DistanceToLoseConversationPartner = 10f;
        public float DistanceToReactivatePassiveRole = 5f;

        public float RadiusNpcStartTalking = 4f;

        /// <summary>
        /// Maximum allowed vertical distance between player and NPC
        /// for triggering an NPC encounter.
        /// </summary>
        public float MaxHeightDifferenceForNpcTrigger = 2f;
    }


    // ================================================================
    // PLAYER
    // ================================================================

    [Serializable]
    public class PlayerSettings
    {
        public float HeightOfPlayerCm = 180;
        public float HeightOfSeatedPlayerCm = 133;

        public float FallbackRootSpacing = 0.25f;
        public float FallbackHipHeight = 1f;
        public float FallbackFootSpacing = 0.2f;

        public float AvatarBaseHeightCm = 180;
    }


    // ================================================================
    // CONTROLS / TRACKING
    // ================================================================

    [Serializable]
    public class ControlSettings
    {
        public bool SimpleInputMode = false;

        public bool FullBodyTrackers = false;
        public bool SeatedMode = false;

        public bool AllowTeleportation = true;
        public bool ProceduralHipAndFeetMove = true;

        public float SmoothAlignSeconds = 1f;

        public bool SelectableNext = true;
    }


    // ================================================================
    // START ROLES
    // ================================================================

    [Serializable]
    public class StartRolesSettings
    {
        public int RoleCount = 0;

        public List<StartRoleSettings> Roles = new();
    }


    [Serializable]
    public class StartRoleSettings
    {
        public string RoleId;

        public string AvatarId;

        public int HeightOfRoleCm = 180;
        public int HeightOfSeatedRoleCm = 133;

        public bool HasInitialStartPose = false;

        public Vector3 InitialStartPos = Vector3.zero;
        public float InitialStartYawDeg = 0f;
    }


    // ================================================================
    // NPC / PRE-RECORDED GROUPS
    // ================================================================

    [Serializable]
    public class NpcGroupSettings
    {
        /// <summary>
        /// Allows a configured NPC group to remain in the preset
        /// while being disabled for a particular workshop.
        /// </summary>
        public bool Enabled = true;

        public string NpcGroupId;

        /// <summary>
        /// Determines whether members of this NPC group may become
        /// active conversation partners after the prerecorded scene.
        /// </summary>
        public bool CanBecomeActiveConversationPartner = true;


        // ------------------------------------------------------------
        // SOURCE SESSION
        // ------------------------------------------------------------

        public string WorkshopFolderName;
        public string SessionFolderName;
        public string SessionId;


        // ------------------------------------------------------------
        // PLACEMENT / BEHAVIOUR
        // ------------------------------------------------------------

        public bool AlignWithPlayer = true;

        public string NpcGroupSpawnId;


        // ------------------------------------------------------------
        // OPTIONAL OVERRIDES
        // ------------------------------------------------------------

        /// <summary>
        /// Empty list means that the original AvatarIds stored in
        /// session.json are used.
        /// </summary>
        public List<AvatarOverride> AvatarOverrides = new();
    }


    // ================================================================
    // NPC AVATAR OVERRIDE
    // ================================================================

    [Serializable]
    public class AvatarOverride
    {
        /// <summary>
        /// RoleId from the source session, e.g. "A" or "Group01_A".
        /// </summary>
        public string RoleId;

        /// <summary>
        /// Avatar that should replace the AvatarId stored in session.json.
        /// </summary>
        public string AvatarId;

        public int HeightOfRoleCm;
        public int HeightOfSeatedRoleCm;
    }
}