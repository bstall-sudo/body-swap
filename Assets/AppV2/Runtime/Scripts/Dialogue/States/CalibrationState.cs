using UnityEngine;
using AppV2.Runtime.Scripts.DataStructures;

namespace AppV2.Runtime.Scripts.Dialogue.States
{
    public class CalibrationState : IState
    {
        private readonly FlowController _flow;
        private int _currentRoleIndexForCalibration;
        private bool selectableNext;
       

        private bool _seatedMode;

        //neu für ausrichtung an XR-Camera
        private bool XrOriginIsPlacedSoThatCameraIsAtVectorZero;

        private bool _isCalibrationFinished = false;

        private bool _rolesSetToPlayerPosition =false;

        private StagePose _playerPosRot;

        public DialogueMode Mode => DialogueMode.Calibration;

        public CalibrationState(FlowController flow)
        {
            _flow = flow;
            _currentRoleIndexForCalibration = 0;
        }

        public void Enter()
        {
            //UnityEngine.Debug.Log("[CalibrationState] Enter");

            selectableNext = _flow.Stage.selectableNext;

            _seatedMode = _flow.Stage.SeatedMode;

            //um während der Kalibrierung eine neutrale Umgebung zu haben.
            _flow.Stage.sceneLoader.EnterCalibrationEnvironment("default", "default");
            //UnityEngine.Debug.Log($"[Enter CalibrationState] Loaded environmentId is: default | stageSpawnId is: default");
            
            _flow.Stage.PlaceXrOriginAtStageOrigin();

            //alle Rollen auf Bodenhöhe Plazieren.
            for (int i = 0; i < _flow.Stage.roleCount; i++){

                
                Vector3 placement = Vector3.zero;
                placement.y = _flow.Stage.GetGroundYStageLocal(placement);

                //UnityEngine.Debug.Log($"[CalibrationState] placement is: {placement}");
                

                Quaternion rotation = Quaternion.identity;

                _flow.Stage.AvatarCalibration.PlaceRoleAt(
                    i,
                    placement,
                    rotation,
                    _flow.Stage._stageRoot
                );

            }

            /*_currentRoleIndexForCalibration = 0;
            
            
            _flow.Stage.RolesVisualsVisibilityHandler.SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            //make head invisible for rig that will be calibrated.
            _flow.Stage.AvatarCalibration.SetAvatarHeadVisible(_currentRoleIndexForCalibration,false);
            // Set XR-Cam to Role height
            _flow.Stage.ApplyActiveRoleEmbodimentHeight(_currentRoleIndexForCalibration, true);
            ShowCurrentRoleOrFinish();*/

            _flow.Stage.RolesVisualsVisibilityHandler.SetAllVisible(false);

            /*for (int i = 0; i < _flow.Stage.roles.Count; i++)
            {
                RoleRig role = _flow.Stage.roles[i];

                Debug.Log(
                    $"[VISIBILITY TEST] " +
                    $"i={i}, " +
                    $"role={role.roleId}, " +
                    $"preRecorded={role.hasPreRecordedTakes}, " +
                    $"avatarRootActive={role.avatarRoot?.gameObject.activeSelf}"
                );
            }*/
            _currentRoleIndexForCalibration = 0;



            ShowCurrentRoleOrFinish();
        }

        public void Tick(float dt)
        {
            // Sicherheit:
            // _currentRoleIndexForCalibration sollte hier immer
            // auf eine normale Rolle zeigen.
            if (_currentRoleIndexForCalibration >= _flow.Stage.roleCount)
                return;

            // Im CalibrationState ist der InputDriver in Standing Mode,
            // weil auch der Root mitverschoben werden muss.
            _flow.Stage.DriveActiveRoleFromInputStandingMode(
                _currentRoleIndexForCalibration,
                dt);

            // Visual und TechnicalRig folgen hier separat,
            // weil nicht recorded wird und proceduralMove noch nicht aktiv sein soll.
            _flow.Stage.ApplyFollowerCalibrationState(
                _currentRoleIndexForCalibration);


            if (!_rolesSetToPlayerPosition)
            {
                _flow.StatusUI.ShowCalibrationAlignHint();
            }


            if (_flow.ConsumePrimaryAction())
            {
                // -----------------------------------------------------
                // ERSTER TRIGGER:
                // Player zum Calibration-Nullpunkt ausrichten
                // -----------------------------------------------------

                if (!_rolesSetToPlayerPosition)
                {
                    _flow.Stage.PlayerAlignForCalibration(
                        _currentRoleIndexForCalibration);

                    _flow.Stage.PlaceMirrorInFrontOfPlayer();

                    _rolesSetToPlayerPosition = true;
                }

                // -----------------------------------------------------
                // WEITERE TRIGGER:
                // aktuelle normale Rolle kalibrieren
                // -----------------------------------------------------

                else
                {
                    _flow.Stage.AvatarCalibration
                        .CalibrateRole(_currentRoleIndexForCalibration);

                    // Kopf wieder sichtbar machen
                    _flow.Stage.AvatarCalibration
                        .SetAvatarHeadVisible(
                            _currentRoleIndexForCalibration,
                            true);

                    // Zur nächsten Rolle
                    _currentRoleIndexForCalibration++;

                    // Diese Methode:
                    // 1. kalibriert übersprungene PreRecorded Rollen
                    // 2. sucht nächste normale Rolle
                    // 3. zeigt diese an
                    // 4. oder beendet Calibration
                    ShowCurrentRoleOrFinish();
                }
            }


            if (_flow.ConsumeSecondaryAction())
            {
                if (_isCalibrationFinished)
                {
                    FinishCalibration();

                }
                else
                {
                    _flow.Stage.haptics.Error();
                    if (_flow.StatusUI != null)
                    {
                                   
                        _flow.StatusUI.ShowCustomCue(
                            "Beende Kalibrierung mit linkem Trigger, bevor du rechts klickst.\n"
                            +
                            "\n" +
                            "Überprüfe: \n linker Controller -> linke Hand, \nrechter Controller -> rechte Hand :)",
                            new Vector2(0f, 180f),
                            new Vector2(500f, 0f),
                            Color.red
                        );
                        
        
                    }

                }
                //UnityEngine.Debug.Log($"[CalibrationState] ConsumeSecondaryAction was called");
                
            }
        }

        public void Exit()
        {
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            _flow.Stage.AvatarCalibration.SetAllAvatarHeadsVisible(true);

            //set visibility of visualRig (Debug-) Cubes
            _flow.Stage.RolesVisualsVisibilityHandler.SetAllVisible(false);
            // reset XR-Cam position to level 0 again
            _flow.Stage.ResetEmbodimentHeight();
            _flow.Stage.sceneLoader.ExitCalibrationEnvironment();
        }

       /* private void ShowCurrentRoleOrFinish()
        {
            //UnityEngine.Debug.Log($"[CalibrationState] ShowCurrentRoleOrFinish() was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
            if (_currentRoleIndexForCalibration >= _flow.Stage.roleCount)
            {
                //UnityEngine.Debug.Log($"[CalibrationState] ShowCurrentRoleOrFinish() before FinishCalibration was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
                FinishCalibration();
                return;
            }

            _flow.Stage.AvatarCalibration.SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            //make head invisible for rig that will be calibrated.
            _flow.Stage.AvatarCalibration.SetAvatarHeadVisible(_currentRoleIndexForCalibration,false);
        }*/

        private void ShowCurrentRoleOrFinish()
        {
            // PreRecorded Rollen überspringen,
            // aber ihre gespeicherte Calibration anwenden.
            while (_currentRoleIndexForCalibration < _flow.Stage.roleCount &&
                _flow.Stage.roles[_currentRoleIndexForCalibration].hasPreRecordedTakes)
            {
                int preRecordedRoleIndex = _currentRoleIndexForCalibration;

                _flow.Stage.AvatarCalibration
                    .CalibrateRole(preRecordedRoleIndex);

                /*Debug.Log(
                    $"[CalibrationState] Applied pre-recorded calibration " +
                    $"for role {preRecordedRoleIndex} " +
                    $"({_flow.Stage.roles[preRecordedRoleIndex].roleId})"
                );*/

                _currentRoleIndexForCalibration++;
            }

            // Keine normalen Rollen mehr übrig
            if (_currentRoleIndexForCalibration >= _flow.Stage.roleCount)
            {
                FinishCalibration();
                return;
            }

            // VisualRig: nur aktuelle Rolle sichtbar
            _flow.Stage.RolesVisualsVisibilityHandler
                .SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            // Avatar: nur aktuelle Rolle sichtbar
            _flow.Stage.AvatarCalibration
                .SetOnlyRoleVisible(_currentRoleIndexForCalibration);

            // Kopf der aktuellen Rolle ausblenden
            _flow.Stage.AvatarCalibration
                .SetAvatarHeadVisible(
                    _currentRoleIndexForCalibration,
                    false);

            // XR-Höhe an aktuelle Rolle anpassen
            _flow.Stage.ApplyActiveRoleEmbodimentHeight(
                _currentRoleIndexForCalibration,
                true);
        }

        private void FinishCalibration()
        {
            //UnityEngine.Debug.Log($"[CalibrationState] FinishCalibration() was called _currentRoleIndexForCalibration = {_currentRoleIndexForCalibration}");
            _flow.Stage.AvatarCalibration.ShowAllRoles();

            _flow.Stage.MirrorSetVisibility.ActivateMirror(false);

            _flow.Stage.SaveTargetTransformsAfterCalibration();

          
            _flow.SetState(new AvatarPlacementState(_flow));

   


        }
    }
}