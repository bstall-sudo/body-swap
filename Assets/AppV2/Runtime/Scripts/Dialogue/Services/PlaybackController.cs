using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
using AppV2.Runtime.Scripts.Rig;
// das braucht man für .All()
using System.Linq;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class PlaybackController
    {

        private List<TakePlayer> players;
        private TakePlayer player;
        public List<RoleRig> roles;

        public Transform _stageRoot;

        private SessionStore _store;
        public SessionModel _session;

        private int roleCount;
        public bool allStoppedPlaying;

        private SessionTakeIndex _takeIndex;
        private float _playerHeightCm;

        //wichtig für die Anpassung des y-position zum Terrain
        private GroundHeightProvider _groundHeightProvider;

        private float _roleScale;

        public void Initialize(Transform stageRoot, List<RoleRig> roles, float playerHeigthCm, SessionStore sessionStore , SessionTakeIndex takeIndex, GroundHeightProvider groundHeightProvider){
            _stageRoot = stageRoot;
            _playerHeightCm = playerHeigthCm;
            _store = sessionStore;
            _takeIndex = takeIndex;
            _groundHeightProvider = groundHeightProvider;
            InitializePlayers(roles, _groundHeightProvider);
        }
        
        public void InitializeFromSession(Transform stageRoot, List<RoleRig> roles, SessionStore sessionStore, SessionTakeIndex takeIndex, SessionModel session, GroundHeightProvider groundHeightProvider)
        {
            _stageRoot = stageRoot;
            _store = sessionStore;
            _takeIndex = takeIndex;

            _session = session;

            //Debug.Log($"InitializeFrom Session: _session.SessionId is: {_session.SessionId}");
            
            _groundHeightProvider = groundHeightProvider;
            InitializePlayers(roles, _groundHeightProvider);
        }

        private void InitializePlayers(List<RoleRig> roles, GroundHeightProvider groundHeightProvider)
        {
            players = new List<TakePlayer>();

            this.roles = roles;
            roleCount = roles.Count;
            allStoppedPlaying = false;

            for (int i = 0; i < roleCount; i++)
            {
                var player = new TakePlayer(
                    roles[i].root,
                    roles[i].head,
                    roles[i].leftHand,
                    roles[i].rightHand,
                    roles[i].hip,
                    roles[i].leftFoot,
                    roles[i].rightFoot,
                    roles[i].audioSource,
                    groundHeightProvider
                );


               

                /*
                UnityEngine.Debug.Log(
                    $"InitializePlayers: roleIndex={i}, roleId={roles[i].roleId}, " +
                    $"heightOfRoleCm={roles[i].heightOfRoleCm}, playerHeightCm={_playerHeightCm}"
                );
                */
                players.Add(player);
            }
        }

        //Um die Bezugspunkte für die PreRecordedScenes anzupassen
        public void SetPlaybackOriginForIndex(int roleIndex, Transform origin)
        {
            if (players == null || roleIndex < 0 || roleIndex >= players.Count)
                return;

            players[roleIndex].SetPlaybackOrigin(origin);
        }

        public void PlaybackForIndexListBegin(List<int> roleIndices, float playerHeightCM, int sceneCount, string sessionId){

            foreach (var roleIndex in roleIndices){

                PlaybackForIndexBegin(roleIndex, playerHeightCM, sceneCount, sessionId);
            }
            
        }

        public void PlaybackForIndexBegin(int roleIndex, float playerHeightCM, int sceneCount, string sessionId)
        {
            if(_takeIndex.TryGetTakeForScene(roleIndex, sceneCount, out TakeMeta takeMeta)){

                    //UnityEngine.Debug.Log($"[PlaybackForIndexBegin] sessionId is: {sessionId}");

                    TakeData take = _store.LoadTakeData(takeMeta, sessionId);

                    if (playerHeightCM > 0.01f)
                    {
                        _roleScale = (float)roles[roleIndex].heightOfRoleCm /playerHeightCM;
                    }
                    else{
                        _roleScale = 1f;
                    }
                    players[roleIndex].SetRoleScale(_roleScale);
                    players[roleIndex].Begin(take);
                    /*
                    UnityEngine.Debug.Log(
                        $"Loaded take for roleIndex {roleIndex}: " +
                        $"frames={(take?.Frames != null ? take.Frames.Count : -1)}, " +
                        $"audio={(take?.AudioClip != null ? take.AudioClip.name : "null")}, " +
                        $"duration={take?.DurationSec}"
                    );
                    */

                }
                if (roles[roleIndex].sittingIdle)
                {
                    roles[roleIndex].avatar.SetRigModeRecordPlayback();
                    roles[roleIndex].avatar.PlayIdleAnimation(true);
                    roles[roleIndex].avatar.SetLowerBodyIKWeight(0.0f);

                }
        }

        //das ist jetzt neu für das Abspielen der PreRecorded Takes ersetzt vermutlich PlaybackForIndexBegin
        public void PlaybackForIndexBeginFromTake(
            int targetRoleIndex,
            TakeMeta takeMeta,
            SessionStore store,
            string sessionId,
            float playerHeightCM)
        {
            Debug.Log(
                $"[Turn NPC to Player] [PlaybackForIndexBeginFromTake] " +
                $"take={takeMeta.TakeId}, " +
                $"frames={takeMeta.FramesFile}, " +
                $"session={sessionId}, " +
                $"role={targetRoleIndex}"
            );
            TakeData take = store.LoadTakeData(takeMeta, sessionId);

            float roleScale = 1f;

            if (playerHeightCM > 0.01f)
                roleScale = (float)roles[targetRoleIndex].heightOfRoleCm / playerHeightCM;

            players[targetRoleIndex].SetRoleScale(roleScale);
            players[targetRoleIndex].Begin(take);

            if (roles[targetRoleIndex].sittingIdle)
            {
                roles[targetRoleIndex].avatar.SetRigModeRecordPlayback();
                roles[targetRoleIndex].avatar.PlayIdleAnimation(true);
                roles[targetRoleIndex].avatar.SetLowerBodyIKWeight(0.0f);
            }
        }

        public void TickForIndexList(List<int> roleIndices){

            
            foreach (var roleIndex in roleIndices){
                    
                    players[roleIndex].Tick();;
            }
            //prüfen, ob alle playbacks gestoppt sind.
            allStoppedPlaying = roleIndices.All(roleIndex => !players[roleIndex]._playing);

        }

        public void StopClipsForIndices(List<int> roleIndices)
        {
            foreach (var roleIndex in roleIndices)
            {
                players[roleIndex].Stop();
            }
            
        }
/*
        // wird vom ConversationStage gerufen, um zu wissen, ob alle gestopped sind.. 
        public bool ArePlaybacksStopped()
        {
            return allStoppedPlaying;
        }
    */    
// wird vom ConversationStage gerufen, um zu wissen, ob alle gestopped sind.. 
        public bool ArePlaybacksStoppedForIndices(List<int> roleIndices)
        {
            if (roleIndices == null || players == null)
                return true;

            foreach (int roleIndex in roleIndices)
            {
                if (roleIndex < 0 || roleIndex >= players.Count)
                {
                    //UnityEngine.Debug.Log($"[PlaybackController][ArePlaybacksStoppedForIndices] roleIndex '{roleIndex}' is out of Range");
                    continue;
                }
                    

                if (players[roleIndex].IsPlaying)
                {
                    
                    return false;
                }
                    
            }
            //UnityEngine.Debug.Log($"[PlaybackController][ArePlaybacksStoppedForIndices] roleIndex '{roleIndices[0]}' has stopped playing");
            return true;
        }

        public bool HasTakeForScene(int roleIndex, int sceneCount)
        {
            return _takeIndex != null && _takeIndex.HasTakeForScene(roleIndex, sceneCount);
        }

        public bool HasAnyTakeForScene(int sceneCount)
        {
            if (_takeIndex == null || roles == null)
                return false;

            for (int i = 0; i < roles.Count; i++)
            {
                if (_takeIndex.HasTakeForScene(i, sceneCount))
                    return true;
            }

            return false;
        }

        public void AlignPreRecordedNpcTakesToPlayer(
            List<int> npcRoleIndices,
            Transform player)
        {
            if (npcRoleIndices == null || player == null)
                return;

            foreach (int roleIndex in npcRoleIndices)
            {
                if (roleIndex < 0 || roleIndex >= roles.Count)
                    continue;

                RoleRig role = roles[roleIndex];

                if (role == null ||
                    !role.hasPreRecordedTakes ||
                    !role.alignWithPlayer)
                {
                    continue;
                }

                AlignSinglePreRecordedNpcToPlayer(
                    roleIndex,
                    role,
                    player
                );
            }
        }

         //Die folgenden Funktionen drehen den NPC so, dass er in Richtung player schaut, beim Playback:

        private void AlignSinglePreRecordedNpcToPlayer(
            int roleIndex,
            RoleRig role,
            Transform player)
        {
            SessionModel session = _store.LoadSessionModel(
                _store.CurrentSessionId
            );

            if (session == null)
            {
                Debug.LogError(
                    "[NPC Align] Current session could not be loaded."
                );
                return;
            }

            ConversationRoleMeta roleMeta =
                session.Roles?.Find(
                    r => r.RoleIndex == roleIndex
                );

            if (roleMeta?.StartRootPose == null)
            {
                Debug.LogWarning(
                    $"[NPC Align] No StartRootPose for role {role.roleId}"
                );
                return;
            }


            // --------------------------------------------------
            // NPC STARTPOSITION IN WORLD
            // --------------------------------------------------

            Vector3 npcStageLocalPos =
                roleMeta.StartRootPose.LocalPosition;

            Vector3 npcWorldPos =
                _stageRoot.TransformPoint(npcStageLocalPos);


            // --------------------------------------------------
            // RICHTUNG ZUM PLAYER
            // --------------------------------------------------

            Vector3 direction =
                player.position - npcWorldPos;

            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return;


            // --------------------------------------------------
            // GEWÜNSCHTE ROTATION
            // --------------------------------------------------

            Quaternion desiredWorldRotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );

            Quaternion currentWorldRotation =
                _stageRoot.rotation *
                roleMeta.StartRootPose.LocalRotation;


            float yawOffset =
                Mathf.DeltaAngle(
                    currentWorldRotation.eulerAngles.y,
                    desiredWorldRotation.eulerAngles.y
                );


            Debug.Log(
                $"[NPC Align] {role.roleId}: " +
                $"yawOffset={yawOffset:F1}"
            );


            // --------------------------------------------------
            // ALLE TAKES DIESER ROLE ROTIEREN
            // --------------------------------------------------




            // --------------------------------------------------
            // START ROOT POSE ROTIEREN
            // --------------------------------------------------

            RotateStartRootPose(
                roleMeta,
                yawOffset
            );


            // --------------------------------------------------
            // SESSION SPEICHERN
            // --------------------------------------------------

            _store.SaveSessionModel(session);
        }

        private void RotateStartRootPose(
            ConversationRoleMeta roleMeta,
            float yawOffset)
        {
            if (roleMeta?.StartRootPose == null)
                return;

            Quaternion rotation =
                Quaternion.Euler(
                    0f,
                    yawOffset,
                    0f
                );

            roleMeta.StartRootPose.LocalRotation =
                rotation *
                roleMeta.StartRootPose.LocalRotation;
        }

       
        



    }
}