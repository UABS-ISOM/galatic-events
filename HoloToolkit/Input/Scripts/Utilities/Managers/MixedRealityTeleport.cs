﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.


using System;
using UnityEngine;
using UnityEngine.XR;

#if UNITY_WSA
using UnityEngine.XR.WSA.Input;
#endif

namespace HoloToolkit.Unity.InputModule
{
    /// <summary>
    /// Script teleports the user to the location being gazed at when Y was pressed on a Gamepad.
    /// </summary>
    [RequireComponent(typeof(SetGlobalListener))]
    public class MixedRealityTeleport : Singleton<MixedRealityTeleport>, IControllerInputHandler
    {
        [Tooltip("Name of the thumbstick axis to check for teleport and strafe.")]
        public string LeftThumbstickX = "CONTROLLER_LEFT_STICK_HORIZONTAL";

        [Tooltip("Name of the thumbstick axis to check for teleport and strafe.")]
        public string LeftThumbstickY = "CONTROLLER_LEFT_STICK_VERTICAL";

        [Tooltip("Name of the thumbstick axis to check for rotation.")]
        public string RightThumbstickX = "CONTROLLER_RIGHT_STICK_HORIZONTAL";

        [Tooltip("Name of the thumbstick axis to check for rotation.")]
        public string RightThumbstickY = "CONTROLLER_RIGHT_STICK_VERTICAL";

        public bool EnableTeleport = true;
        public bool EnableRotation = true;
        public bool EnableStrafe = true;

        public float RotationSize = 45.0f;
        public float StrafeAmount = 0.5f;

        public GameObject TeleportMarker;
        private Animator animationController;

        /// <summary>
        /// The fade control allows us to fade out and fade in the scene.
        /// This is done to improve comfort when using an immersive display.
        /// </summary>
        private FadeScript fadeControl;

        private GameObject teleportMarker;
        private bool isTeleportValid;
        private IPointingSource currentPointingSource;
        private uint currentSourceId;

        private void Start()
        {
            fadeControl = FadeScript.Instance;

            if (!XRDevice.isPresent || fadeControl == null)
            {
                if (fadeControl == null)
                {
                    Debug.LogError("The MixedRealityTeleport script on " + name + " requires a FadeScript object.");
                }

                Destroy(this);
                return;
            }

            if (TeleportMarker != null)
            {
                teleportMarker = Instantiate(TeleportMarker);
                teleportMarker.SetActive(false);

                animationController = teleportMarker.GetComponentInChildren<Animator>();
                if (animationController != null)
                {
                    animationController.StopPlayback();
                }
            }
        }

        private void Update()
        {
#if UNITY_WSA
            if (InteractionManager.numSourceStates == 0)
            {
                HandleGamepad();
            }
#endif

            if (currentPointingSource != null)
            {
                PositionMarker();
            }
        }

        private void HandleGamepad()
        {
            if (EnableTeleport && !fadeControl.Busy)
            {
                float leftX = Input.GetAxis(LeftThumbstickX);
                float leftY = Input.GetAxis(LeftThumbstickY);

                if (currentPointingSource == null && leftY > 0.8 && Math.Abs(leftX) < 0.3)
                {
                    if (FocusManager.Instance.TryGetSinglePointer(out currentPointingSource))
                    {
                        StartTeleport();
                    }
                }
                else if (currentPointingSource != null && new Vector2(leftX, leftY).magnitude < 0.2)
                {
                    FinishTeleport();
                }
            }

            if (EnableStrafe && currentPointingSource == null && !fadeControl.Busy)
            {
                float leftX = Input.GetAxis(LeftThumbstickX);
                float leftY = Input.GetAxis(LeftThumbstickY);

                if (leftX < -0.8 && Math.Abs(leftY) < 0.3)
                {
                    DoStrafe(Vector3.left * StrafeAmount);
                }
                else if (leftX > 0.8 && Math.Abs(leftY) < 0.3)
                {
                    DoStrafe(Vector3.right * StrafeAmount);
                }
                else if (leftY < -0.8 && Math.Abs(leftX) < 0.3)
                {
                    DoStrafe(Vector3.back * StrafeAmount);
                }
            }

            if (EnableRotation && currentPointingSource == null && !fadeControl.Busy)
            {
                float rightX = Input.GetAxis(RightThumbstickX);
                float rightY = Input.GetAxis(RightThumbstickY);

                if (rightX < -0.8 && Math.Abs(rightY) < 0.3)
                {
                    DoRotation(-RotationSize);
                }
                else if (rightX > 0.8 && Math.Abs(rightY) < 0.3)
                {
                    DoRotation(RotationSize);
                }
            }
        }

        void IControllerInputHandler.OnInputPositionChanged(InputPositionEventData eventData)
        {
            if (eventData.PressType == InteractionSourcePressInfo.Thumbstick)
            {
                if (EnableTeleport)
                {
                    if (currentPointingSource == null && eventData.Position.y > 0.8 && Math.Abs(eventData.Position.x) < 0.3)
                    {
                        if (FocusManager.Instance.TryGetPointingSource(eventData, out currentPointingSource))
                        {
                            currentSourceId = eventData.SourceId;
                            StartTeleport();
                        }
                    }
                    else if (currentPointingSource != null && currentSourceId == eventData.SourceId && eventData.Position.magnitude < 0.2)
                    {
                        FinishTeleport();
                    }
                }

                if (EnableStrafe && currentPointingSource == null)
                {
                    if (eventData.Position.y < -0.8 && Math.Abs(eventData.Position.x) < 0.3)
                    {
                        DoStrafe(Vector3.back * StrafeAmount);
                    }
                }

                if (EnableRotation && currentPointingSource == null)
                {
                    if (eventData.Position.x < -0.8 && Math.Abs(eventData.Position.y) < 0.3)
                    {
                        DoRotation(-RotationSize);
                    }
                    else if (eventData.Position.x > 0.8 && Math.Abs(eventData.Position.y) < 0.3)
                    {
                        DoRotation(RotationSize);
                    }
                }
            }
        }

        public void StartTeleport()
        {
            if (currentPointingSource != null && !fadeControl.Busy)
            {
                EnableMarker();
                PositionMarker();
            }
        }

        private void FinishTeleport()
        {
            if (currentPointingSource != null)
            {
                currentPointingSource = null;

                if (isTeleportValid)
                {
                    RaycastHit hitInfo;
                    Vector3 hitPos = teleportMarker.transform.position + Vector3.up * (Physics.Raycast(Camera.main.transform.position, Vector3.down, out hitInfo, 5.0f) ? hitInfo.distance : 2.6f);

                    fadeControl.DoFade(0.25f, 0.5f, () =>
                    {
                        SetWorldPosition(hitPos);
                    }, null);
                }

                DisableMarker();
            }
        }

        public void DoRotation(float rotationAmount)
        {
            if (rotationAmount != 0 && !fadeControl.Busy)
            {
                fadeControl.DoFade(
                    0.25f, // Fade out time
                    0.25f, // Fade in time
                    () => // Action after fade out
                    {
                        transform.RotateAround(Camera.main.transform.position, Vector3.up, rotationAmount);
                    }, null); // Action after fade in
            }
        }

        public void DoStrafe(Vector3 strafeAmount)
        {
            if (strafeAmount.magnitude != 0 && !fadeControl.Busy)
            {
                fadeControl.DoFade(
                    0.25f, // Fade out time
                    0.25f, // Fade in time
                    () => // Action after fade out
                    {
                        Transform transformToRotate = Camera.main.transform;
                        transformToRotate.rotation = Quaternion.Euler(0, transformToRotate.rotation.eulerAngles.y, 0);
                        transform.Translate(strafeAmount, Camera.main.transform);
                    }, null); // Action after fade in
            }
        }

        /// <summary>
        /// Places the player in the specified position of the world
        /// </summary>
        /// <param name="worldPosition"></param>
        public void SetWorldPosition(Vector3 worldPosition)
        {
            // There are two things moving the camera: the camera parent (that this script is attached to)
            // and the user's head (which the MR device is attached to. :)). When setting the world position,
            // we need to set it relative to the user's head in the scene so they are looking/standing where 
            // we expect.
            transform.position = worldPosition - (Camera.main.transform.position - transform.position);
        }

        private void EnableMarker()
        {
            teleportMarker.SetActive(true);
            if (animationController != null)
            {
                animationController.StartPlayback();
            }
        }

        private void DisableMarker()
        {
            if (animationController != null)
            {
                animationController.StopPlayback();
            }
            teleportMarker.SetActive(false);
        }

        private void PositionMarker()
        {
            FocusDetails focusDetails = FocusManager.Instance.GetFocusDetails(currentPointingSource);

            if (focusDetails.Object != null && (Vector3.Dot(focusDetails.Normal, Vector3.up) > 0.90f))
            {
                isTeleportValid = true;

                teleportMarker.transform.position = focusDetails.Point;
            }
            else
            {
                isTeleportValid = false;
            }

            animationController.speed = isTeleportValid ? 1 : 0;
        }
    }
}