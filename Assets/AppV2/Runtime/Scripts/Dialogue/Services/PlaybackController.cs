using System.Collections.Generic;
using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;
using AppV2.Runtime.Scripts.Dialogue.Persistence;
// das braucht man für .All()
using System.Linq;

namespace AppV2.Runtime.Scripts.Dialogue.Services
{
    public class PlaybackController
    {

        private List<TakePlayer> players;
        private TakePlayer player;
        public List<RoleRig> roles;

        private SessionStore _store;
        public SessionModel _session;

        private int roleCount;
        public bool allStoppedPlaying;

        private SessionTakeIndex _takeIndex;
        private float _playerHeightCm;

        private float _roleScale;

        public void Initialize(List<RoleRig> roles, float playerHeigthCm, SessionStore sessionStore , SessionTakeIndex takeIndex){
            _playerHeightCm = playerHeigthCm;
            _store = sessionStore;
            _takeIndex = takeIndex;
            InitializePlayers(roles);
        }
        
        public void InitializeFromSession(List<RoleRig> roles, SessionStore sessionStore, SessionTakeIndex takeIndex, string sessionId)
        {
            _store = sessionStore;
            _takeIndex = takeIndex;

            _session = _store.LoadSessionModel(sessionId);
            _takeIndex.RebuildFromSession(_session);

            InitializePlayers(roles);
        }

        private void InitializePlayers(List<RoleRig> roles)
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
                    roles[i].audioSource
                );

               


                UnityEngine.Debug.Log(
                    $"InitializePlayers: roleIndex={i}, roleId={roles[i].roleId}, " +
                    $"heightOfRoleCm={roles[i].heightOfRoleCm}, playerHeightCm={_playerHeightCm}"
                );

                players.Add(player);
            }
        }

        public void PlaybackForIndexListBegin(List<int> roleIndices, float playerHeightCM, int sceneCount, string sessionId){

            foreach (var roleIndex in roleIndices){

                if(_takeIndex.TryGetTakeForScene(roleIndex, sceneCount, out TakeMeta takeMeta)){

                    TakeData take = _store.LoadTakeData(takeMeta);
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


            }
            
        }

        public void TickForIndexList(List<int> roleIndices){

            
            foreach (var roleIndex in roleIndices){
                    
                    players[roleIndex].Tick();;
            }
            //prüfen, ob alle playbacks gestoppt sind.
            allStoppedPlaying = roleIndices.All(roleIndex => !players[roleIndex]._playing);

        }

        // wird vom ConversationStage gerufen, um zu wissen, ob alle gestopped sind.. 
        public bool ArePlaybacksStopped()
        {
            return allStoppedPlaying;
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



    }
}