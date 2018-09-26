﻿using System;
using Cinemachine;
using StandardAssets.Characters.Attributes;
using StandardAssets.Characters.CharacterInput;
using StandardAssets.Characters.Common;
using StandardAssets.Characters.Physics;
using UnityEngine;

namespace StandardAssets.Characters.ThirdPerson
{
	/// <summary>
	/// Implementation of <see cref="CameraAnimationManager"/> to manage third person camera states 
	/// </summary>
	public class ThirdPersonCameraAnimationManager : CameraAnimationManager
	{
		public event Action forwardUnlockedModeStarted, forwardLockedModeStarted;

		[SerializeField, Tooltip("Third person character brain")]
		protected ThirdPersonBrain thirdPersonBrain;
		
		[DisableEditAtRuntime(), SerializeField, Tooltip("Define the starting camera mode")]
		protected ThirdPersonCameraType startingCameraMode = ThirdPersonCameraType.Exploration;

		[SerializeField, Tooltip("Input Response for changing camera mode and camera recenter")]
		protected InputResponse cameraModeInput, recenterCameraInput;

		[SerializeField, Tooltip("State Driven Camera state names")]
		protected string[] explorationCameraStates, strafeCameraStates;

		[SerializeField, Tooltip("Game objects to toggle when switching camera modes")]
		protected GameObject[] explorationCameraObjects, strafeCameraObjects;

		[SerializeField, Tooltip("Cinemachine State Driven Camera")]
		protected CinemachineStateDrivenCamera explorationStateDrivenCamera;

		[SerializeField, Tooltip("Cinemachine State Driven Camera")]
		protected CinemachineStateDrivenCamera strafeStateDrivenCamera;
		
		[SerializeField, Tooltip("This is the free look camera that will be able to get recentered")]
		protected CinemachineFreeLook idleCamera;
		
		private string[] currentCameraModeStateNames;

		private int cameraIndex;

		private bool isForwardUnlocked;

		private CinemachineStateDrivenCamera thirdPersonStateDrivenCamera;

		private bool isChangingMode;

		/// <inheritdoc/>
		protected override void Start()
		{
			base.Start();
			
			isForwardUnlocked = startingCameraMode == ThirdPersonCameraType.Exploration;
			SetForwardModeArray();
			SetAnimation(currentCameraModeStateNames[cameraIndex]);
			PlayForwardModeEvent();
		}
		
		private void Awake()
		{
			thirdPersonStateDrivenCamera = GetComponent<CinemachineStateDrivenCamera>();

			CheckBrain();

			if (cameraModeInput != null)
			{
				cameraModeInput.Init();
			}
			
			if (recenterCameraInput != null)
			{
				recenterCameraInput.Init();
			}
		}

		/// <summary>
		/// Helper for checking if the brain has been assigned - otherwise looks for it in the scene
		/// </summary>
		private void CheckBrain()
		{
			if (thirdPersonBrain == null)
			{
				Debug.Log("No ThirdPersonBrain setup - using FindObjectOfType");
				ThirdPersonBrain[] brainsInScene = FindObjectsOfType<ThirdPersonBrain>();
				if (brainsInScene.Length == 0)
				{
					Debug.LogError("No ThirdPersonBrain objects in scene!");
					return;
				}
				
				if (brainsInScene.Length > 1)
				{
					Debug.LogWarning("Too many ThirdPersonBrain objects in scene - using the first instance");
				}

				thirdPersonBrain = brainsInScene[0];
			}
		}
		
		/// <summary>
		/// Subscribe to input and <see cref="IThirdPersonMotor.landed"/> events.
		/// </summary>
		private void OnEnable()
		{
			if (cameraModeInput != null)
			{
				cameraModeInput.started += ChangeCameraMode;
				cameraModeInput.ended += ChangeCameraMode;
			}
			
			if (recenterCameraInput != null)
			{
				recenterCameraInput.started += RecenterCamera;
				recenterCameraInput.ended += RecenterCamera;
			}
			
			if (thirdPersonBrain != null)
			{
				thirdPersonBrain.currentMotor.landed += OnLanded;
			}
		}
		
		/// <summary>
		/// Unsubscribe from input and <see cref="IThirdPersonMotor.landed"/> events.
		/// </summary>
		private void OnDisable()
		{
			if (cameraModeInput != null)
			{
				cameraModeInput.started -= ChangeCameraMode;
				cameraModeInput.ended -= ChangeCameraMode;
			}
			
			if (recenterCameraInput != null)
			{
				recenterCameraInput.started -= RecenterCamera;
				recenterCameraInput.ended -= RecenterCamera;
			}
			
			if (thirdPersonBrain != null)
			{
				thirdPersonBrain.currentMotor.landed -= OnLanded;
			}
		}
		
		private void ChangeCameraMode()
		{
			isChangingMode = true;

			if (thirdPersonBrain.physicsForCharacter != null && thirdPersonBrain.physicsForCharacter.isGrounded)
			{
				PerformCameraModeChange();
			}
		}
		
		private void RecenterCamera()
		{
			if (!thirdPersonBrain.inputForCharacter.hasMovementInput)
			{
				RecenterFreeLookCam(idleCamera);
			}
		}
		
		private void OnLanded()
		{
			if (isChangingMode)
			{
				PerformCameraModeChange();
			}
		}

		private void PerformCameraModeChange()
		{
			isForwardUnlocked = !isForwardUnlocked;
			SetForwardModeArray();
			cameraIndex = -1;
			SetCameraState();
			PlayForwardModeEvent();
			isChangingMode = false;
		}

		private void SetForwardModeArray()
		{
			currentCameraModeStateNames = isForwardUnlocked
				? explorationCameraStates
				: strafeCameraStates;
		}

		private void PlayForwardModeEvent()
		{
			if (isForwardUnlocked)
			{
				SetCameraObjectsActive(explorationCameraObjects);

				SetCameraObjectsActive(strafeCameraObjects, false);

				if (forwardUnlockedModeStarted != null)
				{
					forwardUnlockedModeStarted();
				}
			}
			else
			{
				SetCameraObjectsActive(explorationCameraObjects, false);

				if (forwardLockedModeStarted != null)
				{
					forwardLockedModeStarted();
				}
			}
		}
		
		private void Update()
		{
			if (!isForwardUnlocked)
			{
				if (!thirdPersonStateDrivenCamera.IsBlending)
				{
					SetCameraObjectsActive(strafeCameraObjects);
				}
			}

			if (thirdPersonBrain.inputForCharacter.hasMovementInput ||
			    thirdPersonBrain.inputForCharacter.lookInput != Vector2.zero)
			{
				TurnOffFreeLookCamRecenter(idleCamera);
			}	
		}
		
		private void SetCameraState()
		{
			cameraIndex++;
			
			if (cameraIndex >= currentCameraModeStateNames.Length)
			{
				cameraIndex = 0;
			}

			if (isForwardUnlocked)
			{
				SetCameraAxes(strafeStateDrivenCamera, explorationStateDrivenCamera);
			}
			else
			{
				SetCameraAxes(explorationStateDrivenCamera, strafeStateDrivenCamera);
			}

			SetAnimation(currentCameraModeStateNames[cameraIndex]);
		}
		
		private void RecenterFreeLookCam(CinemachineFreeLook freeLook)
		{
			freeLook.m_RecenterToTargetHeading.m_enabled = true;
			freeLook.m_YAxisRecentering.m_enabled = true;
		}

		private void TurnOffFreeLookCamRecenter(CinemachineFreeLook freeLook)
		{
			freeLook.m_RecenterToTargetHeading.m_enabled = false;
			freeLook.m_YAxisRecentering.m_enabled = false;
		}
		
		/// <summary>
		/// Keep virtual camera children of a state driven camera all
		/// pointing in the same direction when changing between state driven cameras
		/// </summary>
		/// <param name="sourceStateDrivenCamera">The state driven camera that is being transitioned from</param>
		/// <param name="destinationStateDrivenCamera">The state driven camera that is being transitioned to</param>
		private void SetCameraAxes(CinemachineStateDrivenCamera sourceStateDrivenCamera,
		                           CinemachineStateDrivenCamera destinationStateDrivenCamera)
		{
			foreach (CinemachineVirtualCameraBase camera in sourceStateDrivenCamera.ChildCameras)
			{
				if (sourceStateDrivenCamera.IsLiveChild(camera))
				{
					float cameraX = camera.GetComponent<CinemachineFreeLook>().m_XAxis.Value;
					float cameraY = camera.GetComponent<CinemachineFreeLook>().m_YAxis.Value;
					SetChildCameraAxes(destinationStateDrivenCamera, cameraX, cameraY);
				}
			}
		}
		
		private void SetChildCameraAxes(CinemachineStateDrivenCamera stateDrivenCamera, float xAxis, float yAxis)
		{
			foreach (CinemachineVirtualCameraBase childCamera in stateDrivenCamera.ChildCameras)
			{
				childCamera.GetComponent<CinemachineFreeLook>().m_XAxis.Value = xAxis;
				childCamera.GetComponent<CinemachineFreeLook>().m_YAxis.Value = yAxis;
			}
		}

		private void SetCameraObjectsActive(GameObject[] cameraObjects, bool isActive = true)
		{
			foreach (GameObject cameraObject in cameraObjects)
			{
				cameraObject.SetActive(isActive);
			}
		}
	}
}