using UnityEngine;
//using Windows.Kinect;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text; 


/// <summary>
/// Avatar controller is the component that transfers the captured user motion to a humanoid model (avatar).
/// </summary>
[RequireComponent(typeof(Animator))]
public class AvatarController : MonoBehaviour
{	
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;
	
	[Tooltip("Whether the avatar is facing the player or not.")]
	public bool mirroredMovement = false;
	
	[Tooltip("Whether the avatar is allowed to move vertically or not.")]
	public bool verticalMovement = false;

	[Tooltip("Whether the avatar's root motion is applied by other component or script.")]
	public bool externalRootMotion = false;

    [Tooltip("Whether the head rotation is controlled externally (e.g. by VR-headset)." )]
    public bool externalHeadRotation = false;
	
	[Tooltip("Whether the hand and finger rotations are controlled externally (e.g. by LeapMotion controller)" )]
	public bool externalHandRotations = false;

	[Tooltip("Whether the finger orientations are allowed or not.")]
	public bool fingerOrientations = false;
	
	[Tooltip("Rate at which the avatar will move through the scene.")]
	public float moveRate = 1f;
	
	[Tooltip("Smooth factor used for avatar movements and joint rotations.")]
	public float smoothFactor = 10f;
	
	[Tooltip("Game object this transform is relative to (optional).")]
	public GameObject offsetNode;

	[Tooltip("If enabled, makes the avatar position relative to this camera to be the same as the player's position to the sensor.")]
	public Camera posRelativeToCamera;

	[Tooltip("Whether the avatar's position should match the color image (in Pos-rel-to-camera mode only).")]
	public bool posRelOverlayColor = false;
	
	[Tooltip("Plane used to render the color camera background to overlay.")]
	public Transform backgroundPlane;

	[Tooltip("Whether z-axis movement needs to be inverted (Pos-Relative mode only).")]
	public bool posRelInvertedZ = false;
	
	[Tooltip("Whether the avatar's feet must stick to the ground.")]
	public bool groundedFeet = false;

	[Tooltip("Whether to apply the humanoid model's muscle limits or not.")]
	public bool applyMuscleLimits = false;

	[Tooltip("Whether to flip left and right, relative to the sensor.")]
	public bool flipLeftRight = false;


	[Tooltip("Vertical offset of the avatar with respect to the position of user's spine-base.")]
	[Range(-0.5f, 0.5f)]
	public float verticalOffset = 0f;

	[Tooltip("Forward offset of the avatar with respect to the position of user's spine-base.")]
	[Range(-0.5f, 0.5f)]
	public float forwardOffset = 0f;

	// userId of the player
	[NonSerialized]
	public Int64 playerId = 0;


	// The body root node
	protected Transform bodyRoot;

	// Variable to hold all them bones. It will initialize the same size as initialRotations.
	protected Transform[] bones;
	protected Transform[] fingerBones;

	// Rotations of the bones when the Kinect tracking starts.
	protected Quaternion[] initialRotations;
	protected Quaternion[] localRotations;
	protected bool[] isBoneDisabled;

	// Local rotations of finger bones
	protected Dictionary<HumanBodyBones, Quaternion> fingerBoneLocalRotations = new Dictionary<HumanBodyBones, Quaternion>();
	protected Dictionary<HumanBodyBones, Vector3> fingerBoneLocalAxes = new Dictionary<HumanBodyBones, Vector3>();

	// Initial position and rotation of the transform
	protected Vector3 initialPosition;
	protected Quaternion initialRotation;
	protected Vector3 initialHipsPosition;
	protected Quaternion initialHipsRotation;

	protected Vector3 offsetNodePos;
	protected Quaternion offsetNodeRot;
	protected Vector3 bodyRootPosition;
	
	// Calibration Offset Variables for Character Position.
	[NonSerialized]
	public bool offsetCalibrated = false;
	protected Vector3 offsetPos = Vector3.zero;
	//protected float xOffset, yOffset, zOffset;
	//private Quaternion originalRotation;

	private Animator animatorComponent = null;
	private HumanPoseHandler humanPoseHandler = null;
	private HumanPose humanPose = new HumanPose();

	// whether the parent transform obeys physics
	protected bool isRigidBody = false;
	
	// private instance of the KinectManager
	protected KinectManager kinectManager;

	// last hand events
	private InteractionManager.HandEventType lastLeftHandEvent = InteractionManager.HandEventType.Release;
	private InteractionManager.HandEventType lastRightHandEvent = InteractionManager.HandEventType.Release;

	// fist states
	private bool bLeftFistDone = false;
	private bool bRightFistDone = false;

	// grounder constants and variables
	private const int raycastLayers = ~2;  // Ignore Raycast
	private const float maxFootDistanceGround = 0.02f;  // maximum distance from lower foot to the ground
	private const float maxFootDistanceTime = 0.2f; // 1.0f;  // maximum allowed time, the lower foot to be distant from the ground
	private Transform leftFoot, rightFoot;

	private float fFootDistanceInitial = 0f;
	private float fFootDistance = 0f;
	private float fFootDistanceTime = 0f;

	// background plane rectangle
	private Rect planeRect = new Rect();
	private bool planeRectSet = false;


	/// <summary>
	/// Gets the number of bone transforms (array length).
	/// </summary>
	/// <returns>The number of bone transforms.</returns>
	public int GetBoneTransformCount()
	{
		return bones != null ? bones.Length : 0;
	}

	/// <summary>
	/// Gets the bone transform by index.
	/// </summary>
	/// <returns>The bone transform.</returns>
	/// <param name="index">Index</param>
	public Transform GetBoneTransform(int index)
	{
		if(index >= 0 && index < bones.Length)
		{
			return bones[index];
		}

		return null;
	}

    /// <summary>
    /// Get joint position with respect of player world and kinect offsets   ( //!!still some problems with accurate Y pos, probably connected with kinect sensor height estimation ) 
    /// </summary>
    /// <param name="jointType"></param>
    /// <returns></returns>
    public Vector3 GetJointWorldPos( KinectInterop.JointType jointType )
    {
        if( !kinectManager )
        {
            return Vector3.zero;
        }
        
        Vector3 jointPosition = kinectManager.GetJointPosition( playerId, (int)jointType );
        Vector3 worldPosition = new Vector3(
            jointPosition.x - offsetPos.x, 
//            jointPosition.y - offsetPos.y + kinectManager.sensorHeight,  //!! this should be better investigated .. 
            jointPosition.y + offsetPos.y - kinectManager.sensorHeight,  //!! this workds better on my example 
            !mirroredMovement && !posRelativeToCamera ? (-jointPosition.z - offsetPos.z) : (jointPosition.z - offsetPos.z));

        Quaternion posRotation = mirroredMovement ? Quaternion.Euler (0f, 180f, 0f) * initialRotation : initialRotation;
        worldPosition = posRotation * worldPosition;

        return bodyRootPosition + worldPosition;
    }

	/// <summary>
	/// Disables the bone and optionally resets its orientation.
	/// </summary>
	/// <param name="index">Bone index.</param>
	/// <param name="resetBone">If set to <c>true</c> resets bone orientation.</param>
	public void DisableBone(int index, bool resetBone)
	{
		if(index >= 0 && index < bones.Length)
		{
			isBoneDisabled[index] = true;

			if (resetBone && bones[index] != null) 
			{
				bones[index].rotation = localRotations[index];
			}
		}
	}

	/// <summary>
	/// Enables the bone, so AvatarController could update its orientation.
	/// </summary>
	/// <param name="index">Bone index.</param>
	public void EnableBone(int index)
	{
		if(index >= 0 && index < bones.Length)
		{
			isBoneDisabled[index] = false;
		}
	}

	/// <summary>
	/// Determines whether the bone orientation update is enabled or not.
	/// </summary>
	/// <returns><c>true</c> if the bone update is enabled; otherwise, <c>false</c>.</returns>
	/// <param name="index">Bone index.</param>
	public bool IsBoneEnabled(int index)
	{
		if(index >= 0 && index < bones.Length)
		{
			return !isBoneDisabled[index];
		}

		return false;
	}

	/// <summary>
	/// Gets the bone index by joint type.
	/// </summary>
	/// <returns>The bone index.</returns>
	/// <param name="joint">Joint type</param>
	/// <param name="bMirrored">If set to <c>true</c> gets the mirrored joint index.</param>
	public int GetBoneIndexByJoint(KinectInterop.JointType joint, bool bMirrored)
	{
		int boneIndex = -1;
		
		if(jointMap2boneIndex.ContainsKey(joint))
		{
			boneIndex = !bMirrored ? jointMap2boneIndex[joint] : mirrorJointMap2boneIndex[joint];
		}
		
		return boneIndex;
	}
	
	/// <summary>
	/// Gets the special index by two joint types.
	/// </summary>
	/// <returns>The spec index by joint.</returns>
	/// <param name="joint1">Joint 1 type.</param>
	/// <param name="joint2">Joint 2 type.</param>
	/// <param name="bMirrored">If set to <c>true</c> gets the mirrored joint index.</param>
	public int GetSpecIndexByJoint(KinectInterop.JointType joint1, KinectInterop.JointType joint2, bool bMirrored)
	{
		int boneIndex = -1;
		
		if((joint1 == KinectInterop.JointType.ShoulderLeft && joint2 == KinectInterop.JointType.SpineShoulder) ||
		   (joint2 == KinectInterop.JointType.ShoulderLeft && joint1 == KinectInterop.JointType.SpineShoulder))
		{
			return (!bMirrored ? 25 : 26);
		}
		else if((joint1 == KinectInterop.JointType.ShoulderRight && joint2 == KinectInterop.JointType.SpineShoulder) ||
		        (joint2 == KinectInterop.JointType.ShoulderRight && joint1 == KinectInterop.JointType.SpineShoulder))
		{
			return (!bMirrored ? 26 : 25);
		}
		else if((joint1 == KinectInterop.JointType.HandTipLeft && joint2 == KinectInterop.JointType.HandLeft) ||
		        (joint2 == KinectInterop.JointType.HandTipLeft && joint1 == KinectInterop.JointType.HandLeft))
		{
			return (!bMirrored ? 27 : 28);
		}
		else if((joint1 == KinectInterop.JointType.HandTipRight && joint2 == KinectInterop.JointType.HandRight) ||
		        (joint2 == KinectInterop.JointType.HandTipRight && joint1 == KinectInterop.JointType.HandRight))
		{
			return (!bMirrored ? 28 : 27);
		}
		else if((joint1 == KinectInterop.JointType.ThumbLeft && joint2 == KinectInterop.JointType.HandLeft) ||
		        (joint2 == KinectInterop.JointType.ThumbLeft && joint1 == KinectInterop.JointType.HandLeft))
		{
			return (!bMirrored ? 29 : 30);
		}
		else if((joint1 == KinectInterop.JointType.ThumbRight && joint2 == KinectInterop.JointType.HandRight) ||
		        (joint2 == KinectInterop.JointType.ThumbRight && joint1 == KinectInterop.JointType.HandRight))
		{
			return (!bMirrored ? 30 : 29);
		}

		return boneIndex;
	}
	
	/// <summary>
	/// Gets the number of finger bone transforms (array length).
	/// </summary>
	/// <returns>The number of finger bone transforms.</returns>
	public int GetFingerTransformCount()
	{
		return fingerBones != null ? fingerBones.Length : 0;
	}

	/// <summary>
	/// Gets the finger bone transform by index.
	/// </summary>
	/// <returns>The finger bone transform.</returns>
	/// <param name="index">Index</param>
	public Transform GetFingerTransform(int index)
	{
		if(index >= 0 && index < fingerBones.Length)
		{
			return fingerBones[index];
		}

		return null;
	}


	// transform caching gives performance boost since Unity calls GetComponent<Transform>() each time you call transform 
	private Transform _transformCache;
	public new Transform transform
	{
		get
		{
			if (!_transformCache) 
			{
				_transformCache = base.transform;
			}

			return _transformCache;
		}
	}


	public void Awake()
    {	
		// check for double start
		if(bones != null)
			return;
		if(!gameObject.activeInHierarchy) 
			return;

		// inits the bones array
		bones = new Transform[31];
		
		// get the animator reference
		animatorComponent = GetComponent<Animator>();

		// Map bones to the points the Kinect tracks
		MapBones();

		// Set model's arms to be in T-pose, if needed
		SetModelArmsInTpose();

		// Initial rotations and directions of the bones.
		initialRotations = new Quaternion[bones.Length];
		localRotations = new Quaternion[bones.Length];
		isBoneDisabled = new bool[bones.Length];

		// Get initial bone rotations
		GetInitialRotations();

		// enable all bones
		for(int i = 0; i < bones.Length; i++)
		{
			isBoneDisabled[i] = false;
		}

		// get initial distance to ground
		fFootDistanceInitial = GetDistanceToGround();
		fFootDistance = 0f;
		fFootDistanceTime = 0f;

		// if parent transform uses physics
		isRigidBody = (gameObject.GetComponent<Rigidbody>() != null);

		// get the pose handler reference
		if (animatorComponent && animatorComponent.avatar && animatorComponent.avatar.isHuman) 
		{
			//Transform hipsTransform = animator.GetBoneTransform(HumanBodyBones.Hips);
			//Transform rootTransform = hipsTransform.parent;
			Transform rootTransform = transform;

			humanPoseHandler = new HumanPoseHandler(animatorComponent.avatar, rootTransform);
			humanPoseHandler.GetHumanPose(ref humanPose);

			initialHipsPosition = (humanPose.bodyPosition - rootTransform.position);  // hipsTransform.position
			initialHipsRotation = humanPose.bodyRotation;
		}
	}


	// applies the muscle limits for humanoid avatar
	private void CheckMuscleLimits()
	{
		if (humanPoseHandler == null)
			return;

		humanPoseHandler.GetHumanPose(ref humanPose);

		//Debug.Log(playerId + " - Trans: " + transform.position + ", body: " + humanPose.bodyPosition);

		bool isPoseChanged = false;

		float muscleMin = -1f;
		float muscleMax = 1f;

		for (int i = 0; i < humanPose.muscles.Length; i++) 
		{
			if (float.IsNaN(humanPose.muscles[i])) 
			{
				//humanPose.muscles[i] = 0f;
				continue;
			}

			if (humanPose.muscles[i] < muscleMin) 
			{
				humanPose.muscles[i] = muscleMin;
				isPoseChanged = true;
			}
			else if (humanPose.muscles[i] > muscleMax) 
			{
				humanPose.muscles[i] = muscleMax;
				isPoseChanged = true;
			}
		}

		if (isPoseChanged) 
		{
			//Quaternion localBodyRot = Quaternion.Inverse(transform.rotation) * humanPose.bodyRotation;
			Quaternion localBodyRot = Quaternion.Inverse(initialHipsRotation) * humanPose.bodyRotation;

			// recover the body position & orientation
			//humanPose.bodyPosition = Vector3.zero;
			//humanPose.bodyPosition.y = initialHipsPosition.y;
			humanPose.bodyPosition = initialHipsPosition;
			humanPose.bodyRotation = localBodyRot; // Quaternion.identity;

			humanPoseHandler.SetHumanPose(ref humanPose);
			//Debug.Log("  Human pose updated.");
		}

	}

	
	/// <summary>
	/// Updates the avatar each frame.
	/// </summary>
	/// <param name="UserID">User ID</param>
    public void UpdateAvatar(Int64 UserID)
    {	
		if(!gameObject.activeInHierarchy) 
			return;

		// Get the KinectManager instance
		if(kinectManager == null)
		{
			kinectManager = KinectManager.Instance;
		}

		// get the background plane rectangle if needed 
		if (backgroundPlane && !planeRectSet && kinectManager && kinectManager.IsInitialized ()) 
		{
			planeRectSet = true;

			planeRect.width = 10f * Mathf.Abs(backgroundPlane.localScale.x);
			planeRect.height = 10f * Mathf.Abs(backgroundPlane.localScale.z);
			planeRect.x = backgroundPlane.position.x - planeRect.width / 2f;
			planeRect.y = backgroundPlane.position.y - planeRect.height / 2f;
		}
		
		// move the avatar to its Kinect position
		if(!externalRootMotion)
		{
			MoveAvatar(UserID);
		}

		// get the left hand state and event
		if(kinectManager && kinectManager.GetJointTrackingState(UserID, (int)KinectInterop.JointType.HandLeft) != KinectInterop.TrackingState.NotTracked)
		{
			KinectInterop.HandState leftHandState = kinectManager.GetLeftHandState(UserID);
			InteractionManager.HandEventType leftHandEvent = InteractionManager.HandStateToEvent(leftHandState, lastLeftHandEvent);

			if(leftHandEvent != InteractionManager.HandEventType.None)
			{
				lastLeftHandEvent = leftHandEvent;
			}
		}

		// get the right hand state and event
		if(kinectManager && kinectManager.GetJointTrackingState(UserID, (int)KinectInterop.JointType.HandRight) != KinectInterop.TrackingState.NotTracked)
		{
			KinectInterop.HandState rightHandState = kinectManager.GetRightHandState(UserID);
			InteractionManager.HandEventType rightHandEvent = InteractionManager.HandStateToEvent(rightHandState, lastRightHandEvent);
			
			if(rightHandEvent != InteractionManager.HandEventType.None)
			{
				lastRightHandEvent = rightHandEvent;
			}
		}
		
		// rotate the avatar bones
		for (var boneIndex = 0; boneIndex < bones.Length; boneIndex++)
		{
			if (!bones[boneIndex] || isBoneDisabled[boneIndex]) 
				continue;

			if(boneIndex2JointMap.ContainsKey(boneIndex))
			{
				KinectInterop.JointType joint = !(mirroredMovement ^ flipLeftRight) ? 
					boneIndex2JointMap[boneIndex] : boneIndex2MirrorJointMap[boneIndex];
				
				if(externalHeadRotation && joint == KinectInterop.JointType.Head)   // skip head if moved externally
				{
					continue;
				}

				if(externalHandRotations &&    // skip hands if moved externally
					(joint == KinectInterop.JointType.WristLeft || joint == KinectInterop.JointType.WristRight ||
						joint == KinectInterop.JointType.HandLeft || joint == KinectInterop.JointType.HandRight))
				{
					continue;
				}

				TransformBone(UserID, joint, boneIndex, !(mirroredMovement ^ flipLeftRight));
			}
			else if(specIndex2JointMap.ContainsKey(boneIndex))
			{
				// special bones (clavicles)
				List<KinectInterop.JointType> alJoints = !(mirroredMovement ^ flipLeftRight) ? 
					specIndex2JointMap[boneIndex] : specIndex2MirrorMap[boneIndex];

				if(alJoints.Count >= 2)
				{
					//Debug.Log(alJoints[0].ToString());
					Vector3 baseDir = alJoints[0].ToString().EndsWith("Left") ? Vector3.left : Vector3.right;
					TransformSpecialBone(UserID, alJoints[0], alJoints[1], boneIndex, baseDir, !(mirroredMovement ^ flipLeftRight));
				}
			}
		}

		if (applyMuscleLimits && kinectManager && kinectManager.IsUserTracked(UserID)) 
		{
			// check for limits
			CheckMuscleLimits();
		}
	}
	
	/// <summary>
	/// Resets bones to their initial positions and rotations. This also releases avatar control from KM, by settings playerId to 0 
	/// </summary>
	public virtual void ResetToInitialPosition()
	{
		playerId = 0;

		if(bones == null)
			return;
		
		// For each bone that was defined, reset to initial position.
		transform.rotation = Quaternion.identity;

		for(int pass = 0; pass < 2; pass++)  // 2 passes because clavicles are at the end
		{
			for(int i = 0; i < bones.Length; i++)
			{
				if(bones[i] != null)
				{
					bones[i].rotation = initialRotations[i];
				}
			}
		}

		// reset finger bones to initial position
		//Animator animatorComponent = GetComponent<Animator>();
		foreach(HumanBodyBones bone in fingerBoneLocalRotations.Keys)
		{
			Transform boneTransform = animatorComponent ? animatorComponent.GetBoneTransform(bone) : null;
			
			if(boneTransform)
			{
				boneTransform.localRotation = fingerBoneLocalRotations[bone];
			}
		}

//		if(bodyRoot != null)
//		{
//			bodyRoot.localPosition = Vector3.zero;
//			bodyRoot.localRotation = Quaternion.identity;
//		}

		// Restore the offset's position and rotation
		if(offsetNode != null)
		{
			offsetNode.transform.position = offsetNodePos;
			offsetNode.transform.rotation = offsetNodeRot;
		}

		transform.position = initialPosition;
		transform.rotation = initialRotation;

//		if (bones[0]) 
//		{
//			bones[0].localPosition = initialHipsPosition;
//			bones[0].localRotation = initialHipsRotation;
//		}
    }
	
	/// <summary>
	/// Invoked on the successful calibration of the player.
	/// </summary>
	/// <param name="userId">User identifier.</param>
	public virtual void SuccessfulCalibration(Int64 userId, bool resetInitialTransform)
	{
		playerId = userId;

		// reset the models position
		if(offsetNode != null)
		{
			offsetNode.transform.position = offsetNodePos;
			offsetNode.transform.rotation = offsetNodeRot;
		}
		
        // reset initial position / rotation if needed 
        if(resetInitialTransform)
        {
            bodyRootPosition = transform.position;
            initialPosition = transform.position;
            initialRotation = transform.rotation;
        }

		transform.position = initialPosition;
		transform.rotation = initialRotation;

//		// enable all bones
//		for(int i = 0; i < bones.Length; i++)
//		{
//			isBoneDisabled[i] = false;
//		}

		// re-calibrate the position offset
		offsetCalibrated = false;
	}

    /// <summary>
    /// Moves the avatar to its initial/base position 
    /// </summary>
    /// <param name="position"> world position </param>
    /// <param name="rotation"> rotation offset </param>
    public void resetInitialTransform( Vector3 position, Vector3 rotation )
    {
        bodyRootPosition = position;
        initialPosition = position;
        initialRotation = Quaternion.Euler( rotation );
        
        transform.position = initialPosition;
        transform.rotation = initialRotation;
        
        offsetCalibrated = false;       // this cause also calibrating kinect offset in moveAvatar function 
    }
	
	// Apply the rotations tracked by kinect to the joints.
	protected void TransformBone(Int64 userId, KinectInterop.JointType joint, int boneIndex, bool flip)
    {
		Transform boneTransform = bones[boneIndex];
		if(boneTransform == null || kinectManager == null)
			return;
		
		int iJoint = (int)joint;
		if(iJoint < 0 || !kinectManager.IsJointTracked(userId, iJoint))
			return;
		
		// Get Kinect joint orientation
		Quaternion jointRotation = kinectManager.GetJointOrientation(userId, iJoint, flip);
		if(jointRotation == Quaternion.identity)
			return;

		// calculate the new orientation
		Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

		if(externalRootMotion)
		{
			newRotation = transform.rotation * newRotation;
		}

		// Smoothly transition to the new rotation
		if(smoothFactor != 0f)
        	boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
		else
			boneTransform.rotation = newRotation;
	}
	
	// Apply the rotations tracked by kinect to a special joint
	protected void TransformSpecialBone(Int64 userId, KinectInterop.JointType joint, KinectInterop.JointType jointParent, int boneIndex, Vector3 baseDir, bool flip)
	{
		Transform boneTransform = bones[boneIndex];
		if(boneTransform == null || kinectManager == null)
			return;
		
		if(!kinectManager.IsJointTracked(userId, (int)joint) || 
		   !kinectManager.IsJointTracked(userId, (int)jointParent))
		{
			return;
		}

		if(boneIndex >= 27 && boneIndex <= 30)
		{
			// fingers or thumbs
			if(fingerOrientations && !externalHandRotations)
			{
				TransformSpecialBoneFingers(userId, (int)joint, boneIndex, flip);
			}

			return;
		}
		
		// if the user is turned, tracking of special bones may be incorrect 
		bool userTurned = kinectManager.IsUserTurnedAround(userId);
		if (userTurned) 
		{
			return;
		}
		
		Vector3 jointDir = kinectManager.GetJointDirection(userId, (int)joint, userTurned, true);
		Quaternion jointRotation = jointDir != Vector3.zero ? Quaternion.FromToRotation(baseDir, jointDir) : Quaternion.identity;
		
		if(!flip)
		{
			Vector3 mirroredAngles = jointRotation.eulerAngles;
			mirroredAngles.y = -mirroredAngles.y;
			mirroredAngles.z = -mirroredAngles.z;

			jointRotation = Quaternion.Euler(mirroredAngles);
		}
		
		if(jointRotation != Quaternion.identity)
		{
			// Smoothly transition to the new rotation
			Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);
			
			if(externalRootMotion)
			{
				newRotation = transform.rotation * newRotation;
			}
			
			if(smoothFactor != 0f)
				boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
			else
				boneTransform.rotation = newRotation;
		}
		
	}

	// Apply the rotations tracked by kinect to fingers (one joint = multiple bones)
	protected void TransformSpecialBoneFingers(Int64 userId, int joint, int boneIndex, bool flip)
	{
		// check for hand grips
		if(joint == (int)KinectInterop.JointType.HandTipLeft || joint == (int)KinectInterop.JointType.ThumbLeft)
		{
			if(lastLeftHandEvent == InteractionManager.HandEventType.Grip)
			{
				if(!bLeftFistDone && !kinectManager.IsUserTurnedAround(userId))
				{
					float angleSign = !mirroredMovement /**(boneIndex == 27 || boneIndex == 29)*/ ? -1f : -1f;
					float angleRot = angleSign * 60f;
					
					TransformSpecialBoneFist(boneIndex, angleRot);
					bLeftFistDone = (boneIndex >= 29);
				}
				
				return;
			}
			else if(bLeftFistDone && lastLeftHandEvent == InteractionManager.HandEventType.Release)
			{
				TransformSpecialBoneUnfist(boneIndex);
				bLeftFistDone = !(boneIndex >= 29);
			}
		}
		else if(joint == (int)KinectInterop.JointType.HandTipRight || joint == (int)KinectInterop.JointType.ThumbRight)
		{
			if(lastRightHandEvent == InteractionManager.HandEventType.Grip)
			{
				if(!bRightFistDone && !kinectManager.IsUserTurnedAround(userId))
				{
					float angleSign = !mirroredMovement /**(boneIndex == 27 || boneIndex == 29)*/ ? -1f : -1f;
					float angleRot = angleSign * 60f;
					
					TransformSpecialBoneFist(boneIndex, angleRot);
					bRightFistDone = (boneIndex >= 29);
				}

				return;
			}
			else if(bRightFistDone && lastRightHandEvent == InteractionManager.HandEventType.Release)
			{
				TransformSpecialBoneUnfist(boneIndex);
				bRightFistDone = !(boneIndex >= 29);
			}
		}

		// get the animator component
		//Animator animatorComponent = GetComponent<Animator>();
		if(!animatorComponent)
			return;
		
		// Get Kinect joint orientation
		Quaternion jointRotation = kinectManager.GetJointOrientation(userId, joint, flip);
		if(jointRotation == Quaternion.identity)
			return;

		// calculate the new orientation
		Quaternion newRotation = Kinect2AvatarRot(jointRotation, boneIndex);

		if(externalRootMotion)
		{
			newRotation = transform.rotation * newRotation;
		}

		// get the list of bones
		//List<HumanBodyBones> alBones = flip ? specialIndex2MultiBoneMap[boneIndex] : specialIndex2MirrorBoneMap[boneIndex];
		List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];
		
		// Smoothly transition to the new rotation
		for(int i = 0; i < alBones.Count; i++)
		{
			Transform boneTransform = animatorComponent.GetBoneTransform(alBones[i]);
			if(!boneTransform)
				continue;

			if(smoothFactor != 0f)
				boneTransform.rotation = Quaternion.Slerp(boneTransform.rotation, newRotation, smoothFactor * Time.deltaTime);
			else
				boneTransform.rotation = newRotation;
		}
	}

	// Apply the rotations needed to transform fingers to fist
	protected void TransformSpecialBoneFist(int boneIndex, float angle)
	{
//		// do fist only for fingers
//		if(boneIndex != 27 && boneIndex != 28)
//			return;

		// get the animator component
		//Animator animatorComponent = GetComponent<Animator>();
		if(!animatorComponent)
			return;
		
		// get the list of bones
		List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];

		for(int i = 0; i < alBones.Count; i++)
		{
			if(i < 1 && (boneIndex == 29 || boneIndex == 30))  // skip the first two thumb bones
				continue;
			
			HumanBodyBones bone = alBones[i];
			Transform boneTransform = animatorComponent.GetBoneTransform(bone);

			// set the fist rotation
			if(boneTransform && fingerBoneLocalAxes[bone] != Vector3.zero)
			{
				Quaternion qRotFinger = Quaternion.AngleAxis(angle, fingerBoneLocalAxes[bone]);
				boneTransform.localRotation = fingerBoneLocalRotations[bone] * qRotFinger;
			}
		}

	}
	
	// Apply the initial rotations fingers
	protected void TransformSpecialBoneUnfist(int boneIndex)
	{
//		// do fist only for fingers
//		if(boneIndex != 27 && boneIndex != 28)
//			return;
		
		// get the animator component
		//Animator animatorComponent = GetComponent<Animator>();
		if(!animatorComponent)
			return;
		
		// get the list of bones
		List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];
		
		for(int i = 0; i < alBones.Count; i++)
		{
			HumanBodyBones bone = alBones[i];
			Transform boneTransform = animatorComponent.GetBoneTransform(bone);

			// set the initial rotation
			if(boneTransform)
			{
				boneTransform.localRotation = fingerBoneLocalRotations[bone];
			}
		}
	}
	
	// Moves the avatar - gets the tracked position of the user and applies it to avatar.
	protected void MoveAvatar(Int64 UserID)
	{
		if((moveRate == 0f) || !kinectManager ||
		   !kinectManager.IsJointTracked(UserID, (int)KinectInterop.JointType.SpineBase))
		{
			return;
		}
		
		// get the position of user's spine base
		Vector3 trans = kinectManager.GetUserPosition(UserID);
		if(flipLeftRight)
			trans.x = -trans.x;

		// use the color overlay position if needed
		if(posRelativeToCamera && posRelOverlayColor)
		{
			if(backgroundPlane && planeRectSet)
			{
				// get the plane overlay position
				trans = kinectManager.GetJointPosColorOverlay(UserID, (int)KinectInterop.JointType.SpineBase, planeRect);
				trans.z = backgroundPlane.position.z - posRelativeToCamera.transform.position.z - 0.1f;  // 10cm offset
			}
			else 
			{
				Rect backgroundRect = posRelativeToCamera.pixelRect;
				PortraitBackground portraitBack = PortraitBackground.Instance;

				if(portraitBack && portraitBack.enabled)
				{
					backgroundRect = portraitBack.GetBackgroundRect();
				}

				trans = kinectManager.GetJointPosColorOverlay(UserID, (int)KinectInterop.JointType.SpineBase, posRelativeToCamera, backgroundRect);
			}

			if(flipLeftRight)
				trans.x = -trans.x;
		}

		// invert the z-coordinate, if needed
		if(posRelativeToCamera && posRelInvertedZ)
		{
			trans.z = -trans.z;
		}
		
		if(!offsetCalibrated)
		{
			offsetCalibrated = true;
			
			offsetPos.x = trans.x;  // !mirroredMovement ? trans.x * moveRate : -trans.x * moveRate;
			offsetPos.y = trans.y;  // trans.y * moveRate;
			offsetPos.z = !mirroredMovement && !posRelativeToCamera ? -trans.z : trans.z;  // -trans.z * moveRate;

			if(posRelativeToCamera)
			{
				Vector3 cameraPos = posRelativeToCamera.transform.position;
				Vector3 bodyRootPos = bodyRoot != null ? bodyRoot.position : transform.position;
				Vector3 hipCenterPos = bodyRoot != null ? bodyRoot.position : (bones != null && bones.Length > 0 && bones[0] != null ? bones[0].position : Vector3.zero);

				float yRelToAvatar = 0f;
				if(verticalMovement)
				{
					yRelToAvatar = (trans.y - cameraPos.y) - (hipCenterPos - bodyRootPos).magnitude;
				}
				else
				{
					yRelToAvatar = bodyRootPos.y - cameraPos.y;
				}

				Vector3 relativePos = new Vector3(trans.x, yRelToAvatar, trans.z);
				Vector3 newBodyRootPos = cameraPos + relativePos;

//				if(offsetNode != null)
//				{
//					newBodyRootPos += offsetNode.transform.position;
//				}

				if(bodyRoot != null)
				{
					bodyRoot.position = newBodyRootPos;
				}
				else
				{
					transform.position = newBodyRootPos;
				}

				bodyRootPosition = newBodyRootPos;
			}
		}
	
		// transition to the new position
		Vector3 targetPos = bodyRootPosition + Kinect2AvatarPos(trans, verticalMovement);

		if(isRigidBody && !verticalMovement)
		{
			// workaround for obeying the physics (e.g. gravity falling)
			targetPos.y = bodyRoot != null ? bodyRoot.position.y : transform.position.y;
		}

		if (verticalMovement && verticalOffset != 0f && 
			bones[0] != null && bones[3] != null) 
		{
			Vector3 dirSpine = bones[3].position - bones[0].position;
			targetPos += dirSpine.normalized * verticalOffset;
		}

		if (forwardOffset != 0f && 
			bones[0] != null && bones[3] != null && bones[5] != null && bones[11] != null) 
		{
			Vector3 dirSpine = (bones[3].position - bones[0].position).normalized;
			Vector3 dirShoulders = (bones[11].position - bones[5].position).normalized;
			Vector3 dirForward = Vector3.Cross(dirShoulders, dirSpine).normalized;

			targetPos += dirForward * forwardOffset;
		}

		if(groundedFeet)
		{
			// keep the current correction
			float fLastTgtY = targetPos.y;
			targetPos.y += fFootDistance;

			float fNewDistance = GetDistanceToGround();
			float fNewDistanceTime = Time.time;

//			Debug.Log(string.Format("PosY: {0:F2}, LastY: {1:F2},  TgrY: {2:F2}, NewDist: {3:F2}, Corr: {4:F2}, Time: {5:F2}", bodyRoot != null ? bodyRoot.position.y : transform.position.y,
//				fLastTgtY, targetPos.y, fNewDistance, fFootDistance, fNewDistanceTime));
			
			if(Mathf.Abs(fNewDistance) >= 0.01f && Mathf.Abs(fNewDistance - fFootDistanceInitial) >= maxFootDistanceGround)
			{
				if((fNewDistanceTime - fFootDistanceTime) >= maxFootDistanceTime)
				{
					fFootDistance += (fNewDistance - fFootDistanceInitial);
					fFootDistanceTime = fNewDistanceTime;

					targetPos.y = fLastTgtY + fFootDistance;

//					Debug.Log(string.Format("   >> change({0:F2})! - Corr: {1:F2}, LastY: {2:F2},  TgrY: {3:F2} at time {4:F2}", 
//								(fNewDistance - fFootDistanceInitial), fFootDistance, fLastTgtY, targetPos.y, fFootDistanceTime));
				}
			}
			else
			{
				fFootDistanceTime = fNewDistanceTime;
			}
		}
		
		if(bodyRoot != null)
		{
			bodyRoot.position = smoothFactor != 0f ? 
				Vector3.Lerp(bodyRoot.position, targetPos, smoothFactor * Time.deltaTime) : targetPos;
		}
		else
		{
			transform.position = smoothFactor != 0f ? 
				Vector3.Lerp(transform.position, targetPos, smoothFactor * Time.deltaTime) : targetPos;
		}
	}
	
	// Set model's arms to be in T-pose
	protected void SetModelArmsInTpose()
	{
		Vector3 vTposeLeftDir = transform.TransformDirection(Vector3.left);
		Vector3 vTposeRightDir = transform.TransformDirection(Vector3.right);

		Transform transLeftUarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ShoulderLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
		Transform transLeftLarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ElbowLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
		Transform transLeftHand = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.WristLeft, false)); // animator.GetBoneTransform(HumanBodyBones.LeftHand);
		
		if(transLeftUarm != null && transLeftLarm != null)
		{
			Vector3 vUarmLeftDir = transLeftLarm.position - transLeftUarm.position;
			float fUarmLeftAngle = Vector3.Angle(vUarmLeftDir, vTposeLeftDir);
			
			if(Mathf.Abs(fUarmLeftAngle) >= 5f)
			{
				Quaternion vFixRotation = Quaternion.FromToRotation(vUarmLeftDir, vTposeLeftDir);
				transLeftUarm.rotation = vFixRotation * transLeftUarm.rotation;
			}
			
			if(transLeftHand != null)
			{
				Vector3 vLarmLeftDir = transLeftHand.position - transLeftLarm.position;
				float fLarmLeftAngle = Vector3.Angle(vLarmLeftDir, vTposeLeftDir);
				
				if(Mathf.Abs(fLarmLeftAngle) >= 5f)
				{
					Quaternion vFixRotation = Quaternion.FromToRotation(vLarmLeftDir, vTposeLeftDir);
					transLeftLarm.rotation = vFixRotation * transLeftLarm.rotation;
				}
			}
		}
		
		Transform transRightUarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ShoulderRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
		Transform transRightLarm = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.ElbowRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
		Transform transRightHand = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.WristRight, false)); // animator.GetBoneTransform(HumanBodyBones.RightHand);
		
		if(transRightUarm != null && transRightLarm != null)
		{
			Vector3 vUarmRightDir = transRightLarm.position - transRightUarm.position;
			float fUarmRightAngle = Vector3.Angle(vUarmRightDir, vTposeRightDir);
			
			if(Mathf.Abs(fUarmRightAngle) >= 5f)
			{
				Quaternion vFixRotation = Quaternion.FromToRotation(vUarmRightDir, vTposeRightDir);
				transRightUarm.rotation = vFixRotation * transRightUarm.rotation;
			}
			
			if(transRightHand != null)
			{
				Vector3 vLarmRightDir = transRightHand.position - transRightLarm.position;
				float fLarmRightAngle = Vector3.Angle(vLarmRightDir, vTposeRightDir);
				
				if(Mathf.Abs(fLarmRightAngle) >= 5f)
				{
					Quaternion vFixRotation = Quaternion.FromToRotation(vLarmRightDir, vTposeRightDir);
					transRightLarm.rotation = vFixRotation * transRightLarm.rotation;
				}
			}
		}
		
	}
	
	// If the bones to be mapped have been declared, map that bone to the model.
	protected virtual void MapBones()
	{
//		// make OffsetNode as a parent of model transform.
//		offsetNode = new GameObject(name + "Ctrl") { layer = transform.gameObject.layer, tag = transform.gameObject.tag };
//		offsetNode.transform.position = transform.position;
//		offsetNode.transform.rotation = transform.rotation;
//		offsetNode.transform.parent = transform.parent;
		
//		// take model transform as body root
//		transform.parent = offsetNode.transform;
//		transform.localPosition = Vector3.zero;
//		transform.localRotation = Quaternion.identity;
		
		//bodyRoot = transform;

		// get bone transforms from the animator component
		//Animator animatorComponent = GetComponent<Animator>();
				
		for (int boneIndex = 0; boneIndex < bones.Length; boneIndex++)
		{
			if (!boneIndex2MecanimMap.ContainsKey(boneIndex)) 
				continue;
			
			bones[boneIndex] = animatorComponent ? animatorComponent.GetBoneTransform(boneIndex2MecanimMap[boneIndex]) : null;
		}

		// map finger bones, too
		fingerBones = new Transform[fingerIndex2MecanimMap.Count];

		for (int boneIndex = 0; boneIndex < fingerBones.Length; boneIndex++)
		{
			if (!fingerIndex2MecanimMap.ContainsKey(boneIndex)) 
				continue;

			fingerBones[boneIndex] = animatorComponent ? animatorComponent.GetBoneTransform(fingerIndex2MecanimMap[boneIndex]) : null;
		}
	}
	
	// Capture the initial rotations of the bones
	protected void GetInitialRotations()
	{
		// save the initial rotation
		if(offsetNode != null)
		{
			offsetNodePos = offsetNode.transform.position;
			offsetNodeRot = offsetNode.transform.rotation;
		}

		initialPosition = transform.position;
		initialRotation = transform.rotation;

//		initialHipsPosition = bones[0] ? bones[0].localPosition : Vector3.zero;
//		initialHipsRotation = bones[0] ? bones[0].localRotation : Quaternion.identity;

//		if(offsetNode != null)
//		{
//			initialRotation = Quaternion.Inverse(offsetNodeRot) * initialRotation;
//		}

		transform.rotation = Quaternion.identity;

		// save the body root initial position
		if(bodyRoot != null)
		{
			bodyRootPosition = bodyRoot.position;
		}
		else
		{
			bodyRootPosition = transform.position;
		}

		if(offsetNode != null)
		{
			bodyRootPosition = bodyRootPosition - offsetNodePos;
		}
		
		// save the initial bone rotations
		for (int i = 0; i < bones.Length; i++)
		{
			if (bones[i] != null)
			{
				initialRotations[i] = bones[i].rotation;
				localRotations[i] = bones[i].localRotation;
			}
		}

		// get finger bones' local rotations
		//Animator animatorComponent = GetComponent<Animator>();
		foreach(int boneIndex in specialIndex2MultiBoneMap.Keys)
		{
			List<HumanBodyBones> alBones = specialIndex2MultiBoneMap[boneIndex];
			//Transform handTransform = animatorComponent.GetBoneTransform((boneIndex == 27 || boneIndex == 29) ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
			
			for(int b = 0; b < alBones.Count; b++)
			{
				HumanBodyBones bone = alBones[b];
				Transform boneTransform = animatorComponent ? animatorComponent.GetBoneTransform(bone) : null;

				// get the finger's 1st transform
				Transform fingerBaseTransform = animatorComponent ? animatorComponent.GetBoneTransform(alBones[b - (b % 3)]) : null;
				//Vector3 vBoneDirParent = handTransform && fingerBaseTransform ? (handTransform.position - fingerBaseTransform.position).normalized : Vector3.zero;

				// get the finger's 2nd transform
				Transform baseChildTransform = fingerBaseTransform && fingerBaseTransform.childCount > 0 ? fingerBaseTransform.GetChild(0) : null;
				Vector3 vBoneDirChild = baseChildTransform && fingerBaseTransform ? (baseChildTransform.position - fingerBaseTransform.position).normalized : Vector3.zero;
				Vector3 vOrthoDirChild = Vector3.Cross(vBoneDirChild, Vector3.up).normalized;

				if(boneTransform)
				{
					fingerBoneLocalRotations[bone] = boneTransform.localRotation;

					if (vBoneDirChild != Vector3.zero) 
					{
						fingerBoneLocalAxes[bone] = boneTransform.InverseTransformDirection(vOrthoDirChild).normalized;
					} 
					else 
					{
						fingerBoneLocalAxes [bone] = Vector3.zero;
					}

//					Transform bparTransform = boneTransform ? boneTransform.parent : null;
//					Transform bchildTransform = boneTransform && boneTransform.childCount > 0 ? boneTransform.GetChild(0) : null;
//
//					// get the finger base transform (1st joint)
//					Transform fingerBaseTransform = animatorComponent.GetBoneTransform(alBones[b - (b % 3)]);
//					Vector3 vBoneDir2 = (handTransform.position - fingerBaseTransform.position).normalized;
//
//					// set the fist rotation
//					if(boneTransform && fingerBaseTransform && handTransform)
//					{
//						Vector3 vBoneDir = bchildTransform ? (bchildTransform.position - boneTransform.position).normalized :
//							(bparTransform ? (boneTransform.position - bparTransform.position).normalized : Vector3.zero);
//
//						Vector3 vOrthoDir = Vector3.Cross(vBoneDir2, vBoneDir).normalized;
//						fingerBoneLocalAxes[bone] = boneTransform.InverseTransformDirection(vOrthoDir);
//					}
				}
			}
		}

		// Restore the initial rotation
		transform.rotation = initialRotation;
	}
	
	// Converts kinect joint rotation to avatar joint rotation, depending on joint initial rotation and offset rotation
	protected Quaternion Kinect2AvatarRot(Quaternion jointRotation, int boneIndex)
	{
		Quaternion newRotation = jointRotation * initialRotations[boneIndex];
		//newRotation = initialRotation * newRotation;

//		if(offsetNode != null)
//		{
//			newRotation = offsetNode.transform.rotation * newRotation;
//		}
//		else
		if (!externalRootMotion)  // fix by Mathias Parger
		{
			newRotation = initialRotation * newRotation;
		}
		
		return newRotation;
	}
	
	// Converts Kinect position to avatar skeleton position, depending on initial position, mirroring and move rate
	protected Vector3 Kinect2AvatarPos(Vector3 jointPosition, bool bMoveVertically)
	{
		float xPos = (jointPosition.x - offsetPos.x) * moveRate;
		float yPos = (jointPosition.y - offsetPos.y) * moveRate;
		float zPos = !mirroredMovement && !posRelativeToCamera ? (-jointPosition.z - offsetPos.z) * moveRate : (jointPosition.z - offsetPos.z) * moveRate;
		
		Vector3 newPosition = new Vector3(xPos, bMoveVertically ? yPos : 0f, zPos);

		Quaternion posRotation = mirroredMovement ? Quaternion.Euler (0f, 180f, 0f) * initialRotation : initialRotation;
		newPosition = posRotation * newPosition;

		if(offsetNode != null)
		{
			//newPosition += offsetNode.transform.position;
			newPosition = offsetNode.transform.position;
		}
		
		return newPosition;
	}

	// returns distance from the given transform to the underlying object. The player must be in IgnoreRaycast layer.
	protected virtual float GetTransformDistanceToGround(Transform trans)
	{
		if(!trans)
			return 0f;

//		RaycastHit hit;
//		if(Physics.Raycast(trans.position, Vector3.down, out hit, 2f, raycastLayers))
//		{
//			return -hit.distance;
//		}
//		else if(Physics.Raycast(trans.position, Vector3.up, out hit, 2f, raycastLayers))
//		{
//			return hit.distance;
//		}
//		else
//		{
//			if (trans.position.y < 0)
//				return -trans.position.y;
//			else
//				return 1000f;
//		}

		return -trans.position.y;
	}

	// returns the lower distance distance from left or right foot to the ground, or 1000f if no LF/RF transforms are found
	protected virtual float GetDistanceToGround()
	{
		if(leftFoot == null && rightFoot == null)
		{
//			Animator animatorComponent = GetComponent<Animator>();
//
//			if(animatorComponent)
//			{
//				leftFoot = animatorComponent.GetBoneTransform(HumanBodyBones.LeftFoot);
//				rightFoot = animatorComponent.GetBoneTransform(HumanBodyBones.RightFoot);
//			}

			leftFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.FootLeft, false));
			rightFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.FootRight, false));

			if (leftFoot == null || rightFoot == null) 
			{
				leftFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.AnkleLeft, false));
				rightFoot = GetBoneTransform(GetBoneIndexByJoint(KinectInterop.JointType.AnkleRight, false));
			}
		}

		float fDistMin = 1000f;
		float fDistLeft = leftFoot ? GetTransformDistanceToGround(leftFoot) : fDistMin;
		float fDistRight = rightFoot ? GetTransformDistanceToGround(rightFoot) : fDistMin;
		fDistMin = Mathf.Abs(fDistLeft) < Mathf.Abs(fDistRight) ? fDistLeft : fDistRight;

		if(fDistMin == 1000f)
		{
			fDistMin = 0f; // fFootDistanceInitial;
		}

//		Debug.Log (string.Format ("LFootY: {0:F2}, Dist: {1:F2}, RFootY: {2:F2}, Dist: {3:F2}, Min: {4:F2}", leftFoot ? leftFoot.position.y : 0f, fDistLeft,
//						rightFoot ? rightFoot.position.y : 0f, fDistRight, fDistMin));

		return fDistMin;
	}

//	protected void OnCollisionEnter(Collision col)
//	{
//		Debug.Log("Collision entered");
//	}
//
//	protected void OnCollisionExit(Collision col)
//	{
//		Debug.Log("Collision exited");
//	}
	
	// dictionaries to speed up bones' processing
	// the author of the terrific idea for kinect-joints to mecanim-bones mapping
	// along with its initial implementation, including following dictionary is
	// Mikhail Korchun (korchoon@gmail.com). Big thanks to this guy!
	protected static readonly Dictionary<int, HumanBodyBones> boneIndex2MecanimMap = new Dictionary<int, HumanBodyBones>
	{
		{0, HumanBodyBones.Hips},
		{1, HumanBodyBones.Spine},
//        {2, HumanBodyBones.Chest},
		{3, HumanBodyBones.Neck},
//		{4, HumanBodyBones.Head},
		
		{5, HumanBodyBones.LeftUpperArm},
		{6, HumanBodyBones.LeftLowerArm},
		{7, HumanBodyBones.LeftHand},
//		{8, HumanBodyBones.LeftIndexProximal},
//		{9, HumanBodyBones.LeftIndexIntermediate},
//		{10, HumanBodyBones.LeftThumbProximal},
		
		{11, HumanBodyBones.RightUpperArm},
		{12, HumanBodyBones.RightLowerArm},
		{13, HumanBodyBones.RightHand},
//		{14, HumanBodyBones.RightIndexProximal},
//		{15, HumanBodyBones.RightIndexIntermediate},
//		{16, HumanBodyBones.RightThumbProximal},
		
		{17, HumanBodyBones.LeftUpperLeg},
		{18, HumanBodyBones.LeftLowerLeg},
		{19, HumanBodyBones.LeftFoot},
//		{20, HumanBodyBones.LeftToes},
		
		{21, HumanBodyBones.RightUpperLeg},
		{22, HumanBodyBones.RightLowerLeg},
		{23, HumanBodyBones.RightFoot},
//		{24, HumanBodyBones.RightToes},
		
		{25, HumanBodyBones.LeftShoulder},
		{26, HumanBodyBones.RightShoulder},
		{27, HumanBodyBones.LeftIndexProximal},
		{28, HumanBodyBones.RightIndexProximal},
		{29, HumanBodyBones.LeftThumbProximal},
		{30, HumanBodyBones.RightThumbProximal},
	};
	
	protected static readonly Dictionary<int, KinectInterop.JointType> boneIndex2JointMap = new Dictionary<int, KinectInterop.JointType>
	{
		{0, KinectInterop.JointType.SpineBase},
		{1, KinectInterop.JointType.SpineMid},
		{2, KinectInterop.JointType.SpineShoulder},
		{3, KinectInterop.JointType.Neck},
		{4, KinectInterop.JointType.Head},
		
		{5, KinectInterop.JointType.ShoulderLeft},
		{6, KinectInterop.JointType.ElbowLeft},
		{7, KinectInterop.JointType.WristLeft},
		{8, KinectInterop.JointType.HandLeft},
		
		{9, KinectInterop.JointType.HandTipLeft},
		{10, KinectInterop.JointType.ThumbLeft},
		
		{11, KinectInterop.JointType.ShoulderRight},
		{12, KinectInterop.JointType.ElbowRight},
		{13, KinectInterop.JointType.WristRight},
		{14, KinectInterop.JointType.HandRight},
		
		{15, KinectInterop.JointType.HandTipRight},
		{16, KinectInterop.JointType.ThumbRight},
		
		{17, KinectInterop.JointType.HipLeft},
		{18, KinectInterop.JointType.KneeLeft},
		{19, KinectInterop.JointType.AnkleLeft},
		{20, KinectInterop.JointType.FootLeft},
		
		{21, KinectInterop.JointType.HipRight},
		{22, KinectInterop.JointType.KneeRight},
		{23, KinectInterop.JointType.AnkleRight},
		{24, KinectInterop.JointType.FootRight},
	};
	
	protected static readonly Dictionary<int, List<KinectInterop.JointType>> specIndex2JointMap = new Dictionary<int, List<KinectInterop.JointType>>
	{
		{25, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder} },
		{26, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder} },
		{27, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipLeft, KinectInterop.JointType.HandLeft} },
		{28, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipRight, KinectInterop.JointType.HandRight} },
		{29, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbLeft, KinectInterop.JointType.HandLeft} },
		{30, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbRight, KinectInterop.JointType.HandRight} },
	};
	
	protected static readonly Dictionary<int, KinectInterop.JointType> boneIndex2MirrorJointMap = new Dictionary<int, KinectInterop.JointType>
	{
		{0, KinectInterop.JointType.SpineBase},
		{1, KinectInterop.JointType.SpineMid},
		{2, KinectInterop.JointType.SpineShoulder},
		{3, KinectInterop.JointType.Neck},
		{4, KinectInterop.JointType.Head},
		
		{5, KinectInterop.JointType.ShoulderRight},
		{6, KinectInterop.JointType.ElbowRight},
		{7, KinectInterop.JointType.WristRight},
		{8, KinectInterop.JointType.HandRight},
		
		{9, KinectInterop.JointType.HandTipRight},
		{10, KinectInterop.JointType.ThumbRight},
		
		{11, KinectInterop.JointType.ShoulderLeft},
		{12, KinectInterop.JointType.ElbowLeft},
		{13, KinectInterop.JointType.WristLeft},
		{14, KinectInterop.JointType.HandLeft},
		
		{15, KinectInterop.JointType.HandTipLeft},
		{16, KinectInterop.JointType.ThumbLeft},
		
		{17, KinectInterop.JointType.HipRight},
		{18, KinectInterop.JointType.KneeRight},
		{19, KinectInterop.JointType.AnkleRight},
		{20, KinectInterop.JointType.FootRight},
		
		{21, KinectInterop.JointType.HipLeft},
		{22, KinectInterop.JointType.KneeLeft},
		{23, KinectInterop.JointType.AnkleLeft},
		{24, KinectInterop.JointType.FootLeft},
	};
	
	protected static readonly Dictionary<int, List<KinectInterop.JointType>> specIndex2MirrorMap = new Dictionary<int, List<KinectInterop.JointType>>
	{
		{25, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderRight, KinectInterop.JointType.SpineShoulder} },
		{26, new List<KinectInterop.JointType> {KinectInterop.JointType.ShoulderLeft, KinectInterop.JointType.SpineShoulder} },
		{27, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipRight, KinectInterop.JointType.HandRight} },
		{28, new List<KinectInterop.JointType> {KinectInterop.JointType.HandTipLeft, KinectInterop.JointType.HandLeft} },
		{29, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbRight, KinectInterop.JointType.HandRight} },
		{30, new List<KinectInterop.JointType> {KinectInterop.JointType.ThumbLeft, KinectInterop.JointType.HandLeft} },
	};
	
	protected static readonly Dictionary<KinectInterop.JointType, int> jointMap2boneIndex = new Dictionary<KinectInterop.JointType, int>
	{
		{KinectInterop.JointType.SpineBase, 0},
		{KinectInterop.JointType.SpineMid, 1},
		{KinectInterop.JointType.SpineShoulder, 2},
		{KinectInterop.JointType.Neck, 3},
		{KinectInterop.JointType.Head, 4},
		
		{KinectInterop.JointType.ShoulderLeft, 5},
		{KinectInterop.JointType.ElbowLeft, 6},
		{KinectInterop.JointType.WristLeft, 7},
		{KinectInterop.JointType.HandLeft, 8},
		
		{KinectInterop.JointType.HandTipLeft, 9},
		{KinectInterop.JointType.ThumbLeft, 10},
		
		{KinectInterop.JointType.ShoulderRight, 11},
		{KinectInterop.JointType.ElbowRight, 12},
		{KinectInterop.JointType.WristRight, 13},
		{KinectInterop.JointType.HandRight, 14},
		
		{KinectInterop.JointType.HandTipRight, 15},
		{KinectInterop.JointType.ThumbRight, 16},
		
		{KinectInterop.JointType.HipLeft, 17},
		{KinectInterop.JointType.KneeLeft, 18},
		{KinectInterop.JointType.AnkleLeft, 19},
		{KinectInterop.JointType.FootLeft, 20},
		
		{KinectInterop.JointType.HipRight, 21},
		{KinectInterop.JointType.KneeRight, 22},
		{KinectInterop.JointType.AnkleRight, 23},
		{KinectInterop.JointType.FootRight, 24},
	};
	
	protected static readonly Dictionary<KinectInterop.JointType, int> mirrorJointMap2boneIndex = new Dictionary<KinectInterop.JointType, int>
	{
		{KinectInterop.JointType.SpineBase, 0},
		{KinectInterop.JointType.SpineMid, 1},
		{KinectInterop.JointType.SpineShoulder, 2},
		{KinectInterop.JointType.Neck, 3},
		{KinectInterop.JointType.Head, 4},
		
		{KinectInterop.JointType.ShoulderRight, 5},
		{KinectInterop.JointType.ElbowRight, 6},
		{KinectInterop.JointType.WristRight, 7},
		{KinectInterop.JointType.HandRight, 8},
		
		{KinectInterop.JointType.HandTipRight, 9},
		{KinectInterop.JointType.ThumbRight, 10},
		
		{KinectInterop.JointType.ShoulderLeft, 11},
		{KinectInterop.JointType.ElbowLeft, 12},
		{KinectInterop.JointType.WristLeft, 13},
		{KinectInterop.JointType.HandLeft, 14},
		
		{KinectInterop.JointType.HandTipLeft, 15},
		{KinectInterop.JointType.ThumbLeft, 16},
		
		{KinectInterop.JointType.HipRight, 17},
		{KinectInterop.JointType.KneeRight, 18},
		{KinectInterop.JointType.AnkleRight, 19},
		{KinectInterop.JointType.FootRight, 20},
		
		{KinectInterop.JointType.HipLeft, 21},
		{KinectInterop.JointType.KneeLeft, 22},
		{KinectInterop.JointType.AnkleLeft, 23},
		{KinectInterop.JointType.FootLeft, 24},
	};


	protected static readonly Dictionary<int, List<HumanBodyBones>> specialIndex2MultiBoneMap = new Dictionary<int, List<HumanBodyBones>>
	{
		{27, new List<HumanBodyBones> {  // left fingers
				HumanBodyBones.LeftIndexProximal,
				HumanBodyBones.LeftIndexIntermediate,
				HumanBodyBones.LeftIndexDistal,
				HumanBodyBones.LeftMiddleProximal,
				HumanBodyBones.LeftMiddleIntermediate,
				HumanBodyBones.LeftMiddleDistal,
				HumanBodyBones.LeftRingProximal,
				HumanBodyBones.LeftRingIntermediate,
				HumanBodyBones.LeftRingDistal,
				HumanBodyBones.LeftLittleProximal,
				HumanBodyBones.LeftLittleIntermediate,
				HumanBodyBones.LeftLittleDistal,
			}},
		{28, new List<HumanBodyBones> {  // right fingers
				HumanBodyBones.RightIndexProximal,
				HumanBodyBones.RightIndexIntermediate,
				HumanBodyBones.RightIndexDistal,
				HumanBodyBones.RightMiddleProximal,
				HumanBodyBones.RightMiddleIntermediate,
				HumanBodyBones.RightMiddleDistal,
				HumanBodyBones.RightRingProximal,
				HumanBodyBones.RightRingIntermediate,
				HumanBodyBones.RightRingDistal,
				HumanBodyBones.RightLittleProximal,
				HumanBodyBones.RightLittleIntermediate,
				HumanBodyBones.RightLittleDistal,
			}},
		{29, new List<HumanBodyBones> {  // left thumb
				HumanBodyBones.LeftThumbProximal,
				HumanBodyBones.LeftThumbIntermediate,
				HumanBodyBones.LeftThumbDistal,
			}},
		{30, new List<HumanBodyBones> {  // right thumb
				HumanBodyBones.RightThumbProximal,
				HumanBodyBones.RightThumbIntermediate,
				HumanBodyBones.RightThumbDistal,
			}},
	};


	protected static readonly Dictionary<int, HumanBodyBones> fingerIndex2MecanimMap = new Dictionary<int, HumanBodyBones>
	{
		{0, HumanBodyBones.LeftThumbProximal},
		{1, HumanBodyBones.LeftThumbIntermediate},
		{2, HumanBodyBones.LeftThumbDistal},

		{3, HumanBodyBones.LeftIndexProximal},
		{4, HumanBodyBones.LeftIndexIntermediate},
		{5, HumanBodyBones.LeftIndexDistal},

		{6, HumanBodyBones.LeftMiddleProximal},
		{7, HumanBodyBones.LeftMiddleIntermediate},
		{8, HumanBodyBones.LeftMiddleDistal},

		{9, HumanBodyBones.LeftRingProximal},
		{10, HumanBodyBones.LeftRingIntermediate},
		{11, HumanBodyBones.LeftRingDistal},

		{12, HumanBodyBones.LeftLittleProximal},
		{13, HumanBodyBones.LeftLittleIntermediate},
		{14, HumanBodyBones.LeftLittleDistal},

		{15, HumanBodyBones.RightThumbProximal},
		{16, HumanBodyBones.RightThumbIntermediate},
		{17, HumanBodyBones.RightThumbDistal},

		{18, HumanBodyBones.RightIndexProximal},
		{19, HumanBodyBones.RightIndexIntermediate},
		{20, HumanBodyBones.RightIndexDistal},

		{21, HumanBodyBones.RightMiddleProximal},
		{22, HumanBodyBones.RightMiddleIntermediate},
		{23, HumanBodyBones.RightMiddleDistal},

		{24, HumanBodyBones.RightRingProximal},
		{25, HumanBodyBones.RightRingIntermediate},
		{26, HumanBodyBones.RightRingDistal},

		{27, HumanBodyBones.RightLittleProximal},
		{28, HumanBodyBones.RightLittleIntermediate},
		{29, HumanBodyBones.RightLittleDistal}
	};

}

