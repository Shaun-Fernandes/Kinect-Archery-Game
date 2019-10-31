using UnityEngine;
using System.Collections;

/// <summary>
/// Avatar scaler is the component that scales avatar's body, according to body measures of the user.
/// </summary>
[RequireComponent(typeof(Animator))]
public class AvatarScaler : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("Whether the avatar is facing the player or not.")]
	public bool mirroredAvatar = false;

	[Tooltip("Body scale factor (incl. arms and legs) that may be used for fine tuning of model-scale.")]
	[Range(0.0f, 2.0f)]
	public float bodyScaleFactor = 1.0f;

	[Tooltip("Body width scale factor that may be used for fine tuning of model-width scale.")]
	[Range(0.0f, 2.0f)]
	public float bodyWidthFactor = 1.0f;

	[Tooltip("Additional scale factor for arms that may be used for fine tuning of model arm-scale.")]
	[Range(0.0f, 2.0f)]
	public float armScaleFactor = 1.0f;

	[Tooltip("Additional scale factor for legs that may be used for fine tuning of model leg-scale.")]
	[Range(0.0f, 2.0f)]
	public float legScaleFactor = 1.0f;
	
	[Tooltip("Whether the scale is updated continuously or just after the calibration pose.")]
	public bool continuousScaling = true;
	
	[Tooltip("Scale smoothing factor used in case of continuous scaling.")]
	public float smoothFactor = 5f;

	[Tooltip("Camera used to overlay the model over the background.")]
	public Camera foregroundCamera;

	[Tooltip("Plane used to render the color camera background to overlay.")]
	public Transform backgroundPlane;

//	[Tooltip("Whether to put the clothing model hip and shoulder joints where the user joints are.")]
//	public bool fixModelHipsAndShoulders = false;

	[Tooltip("UI-Text to display the avatar-scaler debug messages.")]
	public UnityEngine.UI.Text debugText;
	
	// used by category selector
	[System.NonSerialized]
	public long currentUserId = 0;

	// used by category selector
	[System.NonSerialized]
	public bool scalerInited = false;

	// class references
	private KinectManager kinectManager = null;
	private AvatarController avtController = null;

	// model transforms for scaling
	private Transform bodyScaleTransform;
	//private Transform bodyHipsTransform;
	
	private Transform leftShoulderScaleTransform;
	private Transform leftElbowScaleTransform;
	private Transform rightShoulderScaleTransform;
	private Transform rightElbowScaleTransform;
	private Transform leftHipScaleTransform;
	private Transform leftKneeScaleTransform;
	private Transform rightHipScaleTransform;
	private Transform rightKneeScaleTransform;

	private Vector3 modelBodyScale = Vector3.one;
	private Vector3 modelLeftShoulderScale = Vector3.one;
	private Vector3 modelLeftElbowScale = Vector3.one;
	private Vector3 modelRightShoulderScale = Vector3.one; 
	private Vector3 modelRightElbowScale = Vector3.one; 
	private Vector3 modelLeftHipScale = Vector3.one; 
	private Vector3 modelLeftKneeScale = Vector3.one; 
	private Vector3 modelRightHipScale = Vector3.one;
	private Vector3 modelRightKneeScale = Vector3.one;
	
	// model bone sizes and original scales
	private float modelBodyHeight = 0f;
	private float modelBodyWidth = 0f;
	private float modelLeftUpperArmLength = 0f;
	private float modelLeftLowerArmLength = 0f;
	private float modelRightUpperArmLength = 0f;
	private float modelRightLowerArmLength = 0f;
	private float modelLeftUpperLegLength = 0f;
	private float modelLeftLowerLegLength = 0f;
	private float modelRightUpperLegLength = 0f;
	private float modelRightLowerLegLength = 0f;
	
	// user bone sizes
	private float userBodyHeight = 0f;
	private float userBodyWidth = 0f;
	private float leftUpperArmLength = 0f; 
	private float leftLowerArmLength = 0f; 
	private float rightUpperArmLength = 0f;
	private float rightLowerArmLength = 0f;
	private float leftUpperLegLength = 0f; 
	private float leftLowerLegLength = 0f; 
	private float rightUpperLegLength = 0f;
	private float rightLowerLegLength = 0f;

	// user bone scale factors
	private float fScaleBodyHeight = 0f;
	private float fScaleBodyWidth = 0f;
	private float fScaleLeftUpperArm = 0f;
	private float fScaleLeftLowerArm = 0f;
	private float fScaleRightUpperArm = 0f;
	private float fScaleRightLowerArm = 0f;
	private float fScaleLeftUpperLeg = 0f;
	private float fScaleLeftLowerLeg = 0f;
	private float fScaleRightUpperLeg = 0f;
	private float fScaleRightLowerLeg = 0f;

	// background plane rectangle
	private Rect planeRect = new Rect();
	private bool planeRectSet = false;


	public void Start () 
	{
		// get references to other components
		kinectManager = KinectManager.Instance;
		avtController = gameObject.GetComponent<AvatarController>();

		// get model transforms
		Animator animatorComponent = GetComponent<Animator>();
		AvatarController avatarController = GetComponent<AvatarController>();

		// use the root transform for body scale
		bodyScaleTransform = transform;

		if (animatorComponent && animatorComponent.GetBoneTransform (HumanBodyBones.Hips)) 
		{
			//bodyHipsTransform = animatorComponent.GetBoneTransform (HumanBodyBones.Hips);

			leftShoulderScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.LeftUpperArm);
			leftElbowScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.LeftLowerArm);
			rightShoulderScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.RightUpperArm);
			rightElbowScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.RightLowerArm);

			leftHipScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.LeftUpperLeg);
			leftKneeScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.LeftLowerLeg);
			rightHipScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.RightUpperLeg);
			rightKneeScaleTransform = animatorComponent.GetBoneTransform (HumanBodyBones.RightLowerLeg);
		} 
		else if (avatarController) 
		{
			//bodyHipsTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.SpineBase, false));

			leftShoulderScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ShoulderLeft, false));
			leftElbowScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ElbowLeft, false));
			rightShoulderScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ShoulderRight, false));
			rightElbowScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ElbowRight, false));

			leftHipScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.HipLeft, false));
			leftKneeScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.KneeLeft, false));
			rightHipScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.HipRight, false));
			rightKneeScaleTransform = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.KneeRight, false));
		} 
		else 
		{
			// needed transforms could not be found
			return;
		}

		// get model bone scales
		modelBodyScale = bodyScaleTransform ? bodyScaleTransform.localScale : Vector3.one;

		modelLeftShoulderScale = leftShoulderScaleTransform ? leftShoulderScaleTransform.localScale : Vector3.one;
		modelLeftElbowScale = leftElbowScaleTransform ? leftElbowScaleTransform.localScale : Vector3.one;
		modelRightShoulderScale = rightShoulderScaleTransform ? rightShoulderScaleTransform.localScale : Vector3.one;
		modelRightElbowScale = rightElbowScaleTransform ? rightElbowScaleTransform.localScale : Vector3.one;

		modelLeftHipScale = leftHipScaleTransform ? leftHipScaleTransform.localScale : Vector3.one;
		modelLeftKneeScale = leftKneeScaleTransform ? leftKneeScaleTransform.localScale : Vector3.one;
		modelRightHipScale = rightHipScaleTransform ? rightHipScaleTransform.localScale : Vector3.one;
		modelRightKneeScale = rightKneeScaleTransform ? rightKneeScaleTransform.localScale : Vector3.one;

		if (animatorComponent && animatorComponent.GetBoneTransform (HumanBodyBones.Hips)) 
		{
			GetModelBodyHeight(animatorComponent, ref modelBodyHeight, ref modelBodyWidth);
			//Debug.Log (string.Format("MW: {0:F3}, MH: {1:F3}", modelBodyWidth, modelBodyHeight));

			GetModelBoneLength(animatorComponent, HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, ref modelLeftUpperArmLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, ref modelLeftLowerArmLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm, ref modelRightUpperArmLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, ref modelRightLowerArmLength);

			GetModelBoneLength(animatorComponent, HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, ref modelLeftUpperLegLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot, ref modelLeftLowerLegLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, ref modelRightUpperLegLength);
			GetModelBoneLength(animatorComponent, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot, ref modelRightLowerLegLength);

			scalerInited = true;
		}
		else if (avatarController) 
		{
			GetModelBodyHeight(avatarController, ref modelBodyHeight, ref modelBodyWidth);
			//Debug.Log (string.Format("MW: {0:F3}, MH: {1:F3}", modelBodyWidth, modelBodyHeight));

			GetModelBoneLength(avatarController, KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.ElbowLeft, ref modelLeftUpperArmLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.ElbowLeft, KinectInterop.JointType.WristLeft, ref modelLeftLowerArmLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.ElbowRight, ref modelRightUpperArmLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.ElbowRight, KinectInterop.JointType.WristRight, ref modelRightLowerArmLength);

			GetModelBoneLength(avatarController, KinectInterop.JointType.HipLeft, KinectInterop.JointType.KneeLeft, ref modelLeftUpperLegLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.KneeLeft, KinectInterop.JointType.AnkleLeft, ref modelLeftLowerLegLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.HipRight, KinectInterop.JointType.KneeRight, ref modelRightUpperLegLength);
			GetModelBoneLength(avatarController, KinectInterop.JointType.KneeRight, KinectInterop.JointType.AnkleRight, ref modelRightLowerLegLength);

			scalerInited = true;
		}
	}
	
	void Update () 
	{
		if (scalerInited && kinectManager && kinectManager.IsInitialized()) 
		{
			// get the plane rectangle to be used for object overlay
			if (backgroundPlane && !planeRectSet) 
			{
				planeRectSet = true;

				planeRect.width = 10f * Mathf.Abs(backgroundPlane.localScale.x);
				planeRect.height = 10f * Mathf.Abs(backgroundPlane.localScale.z);
				planeRect.x = backgroundPlane.position.x - planeRect.width / 2f;
				planeRect.y = backgroundPlane.position.y - planeRect.height / 2f;
			}

			long userId = kinectManager.GetUserIdByIndex(playerIndex);

			if (userId != currentUserId) 
			{
				currentUserId = userId;

				if (userId != 0) 
				{
					GetUserBodySize (true, true, true);

//					if (fixModelHipsAndShoulders)
//						FixJointsBeforeScale();
					ScaleAvatar(0f);
				}
			}
		}
		
		if(continuousScaling)
		{
			GetUserBodySize(true, true, true);
			ScaleAvatar(smoothFactor);
		}
	}

	// gets the the actual sizes of the user bones
	public void GetUserBodySize(bool bBody, bool bArms, bool bLegs)
	{
		//KinectManager kinectManager = KinectManager.Instance;
		if(kinectManager == null)
			return;
		
		if(bBody)
		{
			GetUserBodyHeight(kinectManager, bodyScaleFactor, bodyWidthFactor, ref userBodyHeight, ref userBodyWidth);
		}
		
		if(bArms)
		{
			GetUserBoneLength(kinectManager, KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.ElbowLeft, armScaleFactor, ref leftUpperArmLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.ElbowLeft, KinectInterop.JointType.WristLeft, armScaleFactor, ref leftLowerArmLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.ElbowRight, armScaleFactor, ref rightUpperArmLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.ElbowRight, KinectInterop.JointType.WristRight, armScaleFactor, ref rightLowerArmLength);

			EqualizeBoneLength(ref leftUpperArmLength, ref rightUpperArmLength);
			EqualizeBoneLength(ref leftLowerArmLength, ref rightLowerArmLength);
		}
		
		if(bLegs)
		{
			GetUserBoneLength(kinectManager, KinectInterop.JointType.HipLeft, KinectInterop.JointType.KneeLeft, legScaleFactor, ref leftUpperLegLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.KneeLeft, KinectInterop.JointType.AnkleLeft, legScaleFactor, ref leftLowerLegLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.HipRight, KinectInterop.JointType.KneeRight, legScaleFactor, ref rightUpperLegLength);
			GetUserBoneLength(kinectManager, KinectInterop.JointType.KneeRight, KinectInterop.JointType.AnkleRight, legScaleFactor, ref rightLowerLegLength);
			
			EqualizeBoneLength(ref leftUpperLegLength, ref rightUpperLegLength);
			EqualizeBoneLength(ref leftLowerLegLength, ref rightLowerLegLength);
		}
	}
	
	// scales the avatar as needed
	public void ScaleAvatar(float fSmooth)
	{
//		KinectManager manager = KinectManager.Instance;
//		if(!manager)
//			return;
//		
//		if(fSmooth != 0f && manager.IsUserTurnedAround(currentUserId))
//			return;

		// scale body
		if (bodyScaleFactor > 0f) 
		{
//			SetupBoneScale(bodyScaleTransform, modelBodyScale, modelBodyHeight, 
//			               userBodyHeight, 0f, fSmooth, ref fScaleBodyHeight);
			SetupBodyScale(bodyScaleTransform, modelBodyScale, modelBodyHeight, modelBodyWidth, userBodyHeight, userBodyWidth, 
				fSmooth, ref fScaleBodyHeight, ref fScaleBodyWidth);

			if (avtController) 
			{
				// recalibrate avatar position due to transform scale change
				avtController.offsetCalibrated = false;

				// set AC smooth-factor to 0 to prevent flickering (r618-issue)
				if (avtController.smoothFactor != 0f) 
				{
					avtController.smoothFactor = 0f;
				}
			}
		}

		// scale arms
		if (armScaleFactor > 0f) 
		{
			float fLeftUpperArmLength = !mirroredAvatar ? leftUpperArmLength : rightUpperArmLength;
			SetupBoneScale(leftShoulderScaleTransform, modelLeftShoulderScale, modelLeftUpperArmLength, 
				fLeftUpperArmLength, fScaleBodyHeight, fSmooth, ref fScaleLeftUpperArm);

			float fLeftLowerArmLength = !mirroredAvatar ? leftLowerArmLength : rightLowerArmLength;
			SetupBoneScale(leftElbowScaleTransform, modelLeftElbowScale, modelLeftLowerArmLength, 
				fLeftLowerArmLength, fScaleLeftUpperArm, fSmooth, ref fScaleLeftLowerArm);

			float fRightUpperArmLength = !mirroredAvatar ? rightUpperArmLength : leftUpperArmLength;
			SetupBoneScale(rightShoulderScaleTransform, modelRightShoulderScale, modelRightUpperArmLength, 
				fRightUpperArmLength, fScaleBodyHeight, fSmooth, ref fScaleRightUpperArm);

			float fRightLowerArmLength = !mirroredAvatar ? rightLowerArmLength : leftLowerArmLength;
			SetupBoneScale(rightElbowScaleTransform, modelRightElbowScale, modelLeftLowerArmLength, 
				fRightLowerArmLength, fScaleRightUpperArm, fSmooth, ref fScaleRightLowerArm);
		}

		// scale legs
		if (legScaleFactor > 0) 
		{
			float fLeftUpperLegLength = !mirroredAvatar ? leftUpperLegLength : rightUpperLegLength;
			SetupBoneScale(leftHipScaleTransform, modelLeftHipScale, modelLeftUpperLegLength, 
				fLeftUpperLegLength, fScaleBodyHeight, fSmooth, ref fScaleLeftUpperLeg);

			float fLeftLowerLegLength = !mirroredAvatar ? leftLowerLegLength : rightLowerLegLength;
			SetupBoneScale(leftKneeScaleTransform, modelLeftKneeScale, modelLeftLowerLegLength, 
				fLeftLowerLegLength, fScaleLeftUpperLeg, fSmooth, ref fScaleLeftLowerLeg);

			float fRightUpperLegLength = !mirroredAvatar ? rightUpperLegLength : leftUpperLegLength;
			SetupBoneScale(rightHipScaleTransform, modelRightHipScale, modelRightUpperLegLength, 
				fRightUpperLegLength, fScaleBodyHeight, fSmooth, ref fScaleRightUpperLeg);

			float fRightLowerLegLength = !mirroredAvatar ? rightLowerLegLength : leftLowerLegLength;
			SetupBoneScale(rightKneeScaleTransform, modelRightKneeScale, modelRightLowerLegLength, 
				fRightLowerLegLength, fScaleRightUpperLeg, fSmooth, ref fScaleRightLowerLeg);
		}

		if(debugText != null)
		{
			string sDebug = string.Format("BW: {0:F2}/{1:F3}, BH: {2:F2}/{3:F3}\nLUA: {4:F3}, LLA: {5:F3}; RUA: {6:F3}, RLA: {7:F3}\nLUL: {8:F3}, LLL: {9:F3}; RUL: {10:F3}, RLL: {11:F3}",
				                          userBodyWidth, fScaleBodyWidth, userBodyHeight, fScaleBodyHeight,
			                              fScaleLeftUpperArm, fScaleLeftLowerArm,
			                              fScaleRightUpperArm, fScaleRightLowerArm,
			                              fScaleLeftUpperLeg, fScaleLeftLowerLeg,
			                              fScaleRightUpperLeg, fScaleRightLowerLeg);
			debugText.text = sDebug;
		}
		
	}
	
	private bool GetModelBodyHeight(Animator animatorComponent, ref float height, ref float width)
	{
		height = 0f;
		
		if(animatorComponent)
		{
			//Transform hipCenter = animatorComponent.GetBoneTransform(HumanBodyBones.Hips);

			Transform leftUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			Transform rightUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperArm);
			
			Transform leftUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform rightUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			
			if(leftUpperArm && rightUpperArm && leftUpperLeg && rightUpperLeg)
			{
				Vector3 posShoulderCenter = (leftUpperArm.position + rightUpperArm.position) / 2f;
				Vector3 posHipCenter = (leftUpperLeg.position + rightUpperLeg.position) / 2f;  // hipCenter.position

				//height = (posShoulderCenter.y - posHipCenter.y);
				height = (posShoulderCenter - posHipCenter).magnitude;
				width = (rightUpperArm.position - leftUpperArm.position).magnitude;
				
				return true;
			}
		}
		
		return false;
	}
	
	private bool GetModelBodyHeight(AvatarController avatarController, ref float height, ref float width)
	{
		height = 0f;

		if(avatarController)
		{
			Transform leftUpperArm = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ShoulderLeft, false));
			Transform rightUpperArm = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.ShoulderRight, false));

			Transform leftUpperLeg = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.HipLeft, false));
			Transform rightUpperLeg = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(KinectInterop.JointType.HipRight, false));

			if(leftUpperArm && rightUpperArm && leftUpperLeg && rightUpperLeg)
			{
				Vector3 posShoulderCenter = (leftUpperArm.position + rightUpperArm.position) / 2f;
				Vector3 posHipCenter = (leftUpperLeg.position + rightUpperLeg.position) / 2f;  // hipCenter.position

				//height = (posShoulderCenter.y - posHipCenter.y);
				height = (posShoulderCenter - posHipCenter).magnitude;
				width = (rightUpperArm.position - leftUpperArm.position).magnitude;

				return true;
			}
		}

		return false;
	}

//	private bool GetModelBoxHeight(ref float height, ref float width)
//	{
//		SkinnedMeshRenderer renderer = GetComponentInChildren<SkinnedMeshRenderer>();
//
//		if (renderer && bodyScaleTransform) 
//		{
//			renderer.updateWhenOffscreen = true;
//			Bounds bounds = renderer.localBounds;
//			Vector3 scale = bodyScaleTransform.localScale;
//			//renderer.updateWhenOffscreen = false;
//
//			height = bounds.size.z / scale.y;
//			width = bounds.size.x / scale.x;
//
//			return true;
//		}
//
//		return false;
//	}

	private bool GetModelBoneLength(Animator animatorComponent, HumanBodyBones baseJoint, HumanBodyBones endJoint, ref float length)
	{
		length = 0f;
		
		if(animatorComponent)
		{
			Transform joint1 = animatorComponent.GetBoneTransform(baseJoint);
			Transform joint2 = animatorComponent.GetBoneTransform(endJoint);
			
			if(joint1 && joint2)
			{
				length = (joint2.position - joint1.position).magnitude;
				return true;
			}
		}
		
		return false;
	}
	
	private bool GetModelBoneLength(AvatarController avatarController, KinectInterop.JointType baseJoint, KinectInterop.JointType endJoint, ref float length)
	{
		length = 0f;

		if(avatarController)
		{
			Transform joint1 = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(baseJoint, false));
			Transform joint2 = avatarController.GetBoneTransform(avatarController.GetBoneIndexByJoint(endJoint, false));

			if(joint1 && joint2)
			{
				length = (joint2.position - joint1.position).magnitude;
				return true;
			}
		}

		return false;
	}

	private bool GetUserBodyHeight(KinectManager manager, float scaleFactor, float widthFactor, ref float height, ref float width)
	{
		height = 0f;
		width = 0f;

		Vector3 posHipLeft = GetJointPosition(manager, (int)KinectInterop.JointType.HipLeft);
		Vector3 posHipRight = GetJointPosition(manager, (int)KinectInterop.JointType.HipRight);
		Vector3 posShoulderLeft = GetJointPosition(manager, (int)KinectInterop.JointType.ShoulderLeft);
		Vector3 posShoulderRight = GetJointPosition(manager, (int)KinectInterop.JointType.ShoulderRight);

		if(posHipLeft != Vector3.zero && posHipRight != Vector3.zero &&
		   posShoulderLeft != Vector3.zero && posShoulderRight != Vector3.zero)
		{
			Vector3 posHipCenter = (posHipLeft + posHipRight) / 2f;
			Vector3 posShoulderCenter = (posShoulderLeft + posShoulderRight) / 2f;
			//height = (posShoulderCenter.y - posHipCenter.y) * scaleFactor;

			height = (posShoulderCenter - posHipCenter).magnitude * scaleFactor;
			width = (posShoulderRight - posShoulderLeft).magnitude * widthFactor;
			
			return true;
		}

		return false;
	}
	
	private bool GetUserBoneLength(KinectManager manager, KinectInterop.JointType baseJoint, KinectInterop.JointType endJoint, float scaleFactor, ref float length)
	{
		length = 0f;
		
		Vector3 vPos1 = GetJointPosition(manager, (int)baseJoint);
		Vector3 vPos2 = GetJointPosition(manager, (int)endJoint);
		
		if(vPos1 != Vector3.zero && vPos2 != Vector3.zero)
		{
			length = (vPos2 - vPos1).magnitude * scaleFactor;
			return true;
		}
		
		return false;
	}

	private void EqualizeBoneLength(ref float boneLen1, ref float boneLen2)
	{
		if(boneLen1 < boneLen2)
		{
			boneLen1 = boneLen2;
		}
		else
		{
			boneLen2 = boneLen1;
		}
	}
	
	private bool SetupBodyScale(Transform scaleTrans, Vector3 modelBodyScale, float modelHeight, float modelWidth, float userHeight, float userWidth, 
		float fSmooth, ref float heightScale, ref float widthScale)
	{
		if(modelHeight > 0f && userHeight > 0f)
		{
			heightScale = userHeight / modelHeight;
		}

		if (modelWidth > 0f && userWidth > 0f) 
		{
			widthScale = userWidth / modelWidth;
		}
		else 
		{
			widthScale = heightScale;
		}

		if(scaleTrans && heightScale > 0f && widthScale > 0f)
		{
			float depthScale = heightScale; // (heightScale + widthScale) / 2f;
			Vector3 newLocalScale = new Vector3 (modelBodyScale.x * widthScale, modelBodyScale.y * heightScale, modelBodyScale.z * depthScale);

			if(fSmooth != 0f)
				scaleTrans.localScale = Vector3.Lerp(scaleTrans.localScale, newLocalScale, fSmooth * Time.deltaTime);
			else
				scaleTrans.localScale = newLocalScale;

			return true;
		}

		return false;
	}


	private bool SetupBoneScale(Transform scaleTrans, Vector3 modelBoneScale, float modelBoneLen, float userBoneLen, float parentScale, float fSmooth, ref float boneScale)
	{
		if(modelBoneLen > 0f && userBoneLen > 0f)
		{
			boneScale = userBoneLen / modelBoneLen;
		}

		float localScale = boneScale;
		if(boneScale > 0f && parentScale > 0f)
		{
			localScale = boneScale / parentScale;
		}
		
		if(scaleTrans && localScale > 0f)
		{
			if(fSmooth != 0f)
				scaleTrans.localScale = Vector3.Lerp(scaleTrans.localScale, modelBoneScale * localScale, fSmooth * Time.deltaTime);
			else
				scaleTrans.localScale = modelBoneScale * localScale;

			return true;
		}

		return false;
	}


	public bool FixJointsBeforeScale()
	{
		Animator animatorComponent = GetComponent<Animator>();
		KinectManager manager = KinectManager.Instance;

		if(animatorComponent && modelBodyHeight > 0f && userBodyHeight > 0f)
		{
			Transform hipCenter = animatorComponent.GetBoneTransform(HumanBodyBones.Hips);
			if((hipCenter.localScale - Vector3.one).magnitude > 0.01f)
				return false;

			Transform leftUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
			Transform rightUpperLeg = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperLeg);
			
			Transform leftUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.LeftUpperArm);
			Transform rightUpperArm = animatorComponent.GetBoneTransform(HumanBodyBones.RightUpperArm);
			
			if(leftUpperArm && rightUpperArm && leftUpperLeg && rightUpperLeg)
			{
				Vector3 posHipCenter = GetJointPosition(manager, (int)KinectInterop.JointType.SpineBase);
				
				Vector3 posHipLeft = GetJointPosition(manager, (int)KinectInterop.JointType.HipLeft);
				Vector3 posHipRight = GetJointPosition(manager, (int)KinectInterop.JointType.HipRight);
				
				Vector3 posShoulderLeft = GetJointPosition(manager, (int)KinectInterop.JointType.ShoulderLeft);
				Vector3 posShoulderRight = GetJointPosition(manager, (int)KinectInterop.JointType.ShoulderRight);
				
				if(posHipCenter != Vector3.zero && posHipLeft != Vector3.zero && posHipRight != Vector3.zero &&
				   posShoulderLeft != Vector3.zero && posShoulderRight != Vector3.zero)
				{
					SetupUnscaledJoint(hipCenter, leftUpperLeg, posHipCenter, (!mirroredAvatar ? posHipLeft : posHipRight), modelBodyHeight, userBodyHeight);
					SetupUnscaledJoint(hipCenter, rightUpperLeg, posHipCenter, (!mirroredAvatar ? posHipRight : posHipLeft), modelBodyHeight, userBodyHeight);

					SetupUnscaledJoint(hipCenter, leftUpperArm, posHipCenter, (!mirroredAvatar ? posShoulderLeft : posShoulderRight), modelBodyHeight, userBodyHeight);
					SetupUnscaledJoint(hipCenter, rightUpperArm, posHipCenter, (!mirroredAvatar ? posShoulderRight : posShoulderLeft), modelBodyHeight, userBodyHeight);

					// recalculate model joints
					Start();

					return true;
				}
			}
		}
		
		return false;
	}


	// gets the joint position in space
	private Vector3 GetJointPosition(KinectManager manager, int joint)
	{
		Vector3 vPosJoint = Vector3.zero;

		if(manager.IsJointTracked(currentUserId, joint))
		{
			if(backgroundPlane && planeRectSet)
			{
				// get the plane overlay position
				vPosJoint = manager.GetJointPosColorOverlay(currentUserId, joint, planeRect);
				vPosJoint.z = backgroundPlane.position.z;
			}	
			else if(foregroundCamera)
			{
				// get the background rectangle (use the portrait background, if available)
				Rect backgroundRect = foregroundCamera.pixelRect;
				PortraitBackground portraitBack = PortraitBackground.Instance;
				
				if(portraitBack && portraitBack.enabled)
				{
					backgroundRect = portraitBack.GetBackgroundRect();
				}

				// get the color overlay position
				vPosJoint = manager.GetJointPosColorOverlay(currentUserId, joint, foregroundCamera, backgroundRect);
			}

//			else
			if(vPosJoint == Vector3.zero)
			{
				vPosJoint = manager.GetJointPosition(currentUserId, joint);
			}
		}

		return vPosJoint;
	}


	// sets the joint position before scaling
	private bool SetupUnscaledJoint(Transform hipCenter, Transform joint, Vector3 posHipCenter, Vector3 posJoint, float modelBoneLen, float userBoneLen)
	{
		float boneScale = 0f;

		if(modelBoneLen > 0f && userBoneLen > 0f)
		{
			boneScale = userBoneLen / modelBoneLen;
			//boneScale = 1f;
		}

		if(boneScale > 0f)
		{
			Vector3 posDiff = (posJoint - posHipCenter) / boneScale;
			if(foregroundCamera == null && backgroundPlane == null)
				posDiff.z = 0f;  // ignore difference in z (non-overlay mode)

			Vector3 posJointNew = hipCenter.position + posDiff;
			joint.position = posJointNew;

			return true;
		}

		return false;
	}



}
