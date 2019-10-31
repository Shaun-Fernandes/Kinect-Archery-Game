// comment out the following #define, if you want to use the depth sensor and the KinectManager on per-scene basis
#define USE_SINGLE_KM_IN_MULTIPLE_SCENES


using UnityEngine;
using UnityEngine.Networking;

using System;
using System.Collections;
using System.Collections.Generic;
//using System.Linq;

/// <summary>
/// KinectManager is the main and the most basic Kinect-related component. It is used to control the sensor and poll the data streams.
/// </summary>
public class KinectManager : MonoBehaviour 
{
	[Tooltip("How high above the ground is the sensor, in meters.")]
	//********Changed from 1.0f to ..... **********
	public float sensorHeight = 3.0f;

	[Tooltip("Kinect elevation angle (in degrees). May be positive or negative.")]
	public float sensorAngle = 0f;
	
	public enum AutoHeightAngle : int { DontUse, ShowInfoOnly, AutoUpdate, AutoUpdateAndShowInfo }
	[Tooltip("Whether to automatically set the sensor height and angle or not. The user must stay in front of the sensor, in order the automatic detection to work.")]
	public AutoHeightAngle autoHeightAngle = AutoHeightAngle.DontUse;

	[Tooltip("Whether to flip left and right, relative to the sensor.")]
	private bool flipLeftRight = false;

	public enum UserMapType : int { None, RawUserDepth, BodyTexture, UserTexture, CutOutTexture }
	[Tooltip("Whether and how to utilize the user and depth images.")]
	public UserMapType computeUserMap = UserMapType.RawUserDepth;
	
	[Tooltip("Whether to utilize the color camera image.")]
	public bool computeColorMap = false;
	
	[Tooltip("Whether to utilize the IR camera image.")]
	public bool computeInfraredMap = false;
	
	[Tooltip("Whether to display the user map on the screen.")]
	public bool displayUserMap = false;
	
	[Tooltip("Whether to display the color camera image on the screen.")]
	public bool displayColorMap = false;
	
	[Tooltip("Whether to display skeleton lines over the the user map.")]
	public bool displaySkeletonLines = false;
	
	// if percent is zero, it is calculated internally to match the selected width and height of the depth image
	[Tooltip("Depth and color image width on the screen, as percent of the screen width. The image height is calculated depending on the width.")]
	public float DisplayMapsWidthPercent = 20f;
	
	[Tooltip("Whether to use the multi-source reader, if one is available (K2-only feature).")]
	public bool useMultiSourceReader = false;

	// Public Bool to determine whether to use sensor's audio source, if available
	//public bool useAudioSource = false;
	
	[Tooltip("Minimum distance to a user, in order to be considered for skeleton data processing.")]
	public float minUserDistance = 0.5f;
	
	[Tooltip("Maximum distance to a user, in order to be considered for skeleton data processing. Value of 0 means no maximum distance limitation.")]
	public float maxUserDistance = 0f;
	
	[Tooltip("Maximum left or right distance to a user, in order to be considered for skeleton data processing. Value of 0 means no left/right distance limitation.")]
	public float maxLeftRightDistance = 0f;
	
	[Tooltip("Maximum number of users, which may be tracked simultaneously.")]
	public int maxTrackedUsers = 6;

	[Tooltip("Whether to display the tracked users within the allowed distance only, or all users (higher fps).")]
	public bool showTrackedUsersOnly = true;

	public enum UserDetectionOrder : int { Appearance = 0, Distance = 1, LeftToRight = 2 }
	[Tooltip("How to assign users to player indices - by order of appearance, distance or left-to-right.")]
	public UserDetectionOrder userDetectionOrder = UserDetectionOrder.Appearance;

	[Tooltip("Whether to utilize only the really tracked joints (and ignore the inferred ones) or not.")]
	public bool ignoreInferredJoints = false;
	
	[Tooltip("Whether to ignore the Z-coordinates of the joints (i.e. to use them in 2D-scenes) or not.")]
	public bool ignoreZCoordinates = false;
	
	[Tooltip("Whether to update the AvatarControllers in LateUpdate(), instead of in Update(). Needed for Mecanim animation blending.")]
	public bool lateUpdateAvatars = false;
	
	[Tooltip("Whether to skip the remote avatar controllers in multiplayer games.")]
	public bool skipRemoteAvatars = false; 

//	[Tooltip("Uses own thread for processing kinect data.")]
//	public bool useOwnThread = false; 

	public enum Smoothing : int { None, Default, Light, Medium, Aggressive }
	[Tooltip("Set of joint smoothing parameters.")]
	public Smoothing smoothing = Smoothing.Default;
	
	[Tooltip("Whether to apply the bone orientation constraints.")]
	public bool useBoneOrientationConstraints = false;
	//public bool useBoneOrientationsFilter = false;

    [Tooltip("Whether to estimate the body joints velocities.")]
	public bool estimateJointVelocities = false; 

	[Tooltip("Set of joint velocity smoothing parameters.")]
    public Smoothing velocitySmoothing = Smoothing.Light;

	[Tooltip("Whether to allow detection of body turn arounds or not.")]
	public bool allowTurnArounds = false;
	
	public enum AllowedRotations : int { None = 0, Default = 1, All = 2 }
	[Tooltip("Allowed wrist and hand rotations: None - no hand rotations are allowed, Default - hand rotations are allowed except for the twists, All - all rotations are allowed.")]
	public AllowedRotations allowedHandRotations = AllowedRotations.Default;

	[Tooltip("Wait time in seconds, before a lost user gets removed. This is to prevent sporadical user switches.")]
	public float waitTimeBeforeRemove = 1f;

	[Tooltip("List of the avatar controllers in the scene. If the list is empty, the available avatar controllers are detected at the scene start up.")]
	public List<AvatarController> avatarControllers = new List<AvatarController>();
	
	[Tooltip("Calibration pose required, to turn on the tracking of respective player.")]
	public KinectGestures.Gestures playerCalibrationPose;
	
	[Tooltip("List of common gestures, to be detected for each player.")]
	public List<KinectGestures.Gestures> playerCommonGestures = new List<KinectGestures.Gestures>();

	[Tooltip("Minimum time between gesture detections (in seconds).")]
	public float minTimeBetweenGestures = 0.7f;
	
	[Tooltip("Gesture manager, used to detect programmatic Kinect gestures.")]
	public KinectGestures gestureManager;
	
	[Tooltip("List of the gesture listeners in the scene. If the list is empty, the available gesture listeners will be detected at the scene start up.")]
	public List<MonoBehaviour> gestureListeners = new List<MonoBehaviour>();

	[Tooltip("UI-Text to display user detection messages.")]
	public UnityEngine.UI.Text calibrationText;
	
	[Tooltip("UI-Text to display debug messages for the currently tracked gestures.")]
	public UnityEngine.UI.Text gesturesDebugText;


	// Bool to keep track of whether Kinect has been initialized
	protected bool kinectInitialized = false; 
	
	// The singleton instance of KinectManager
	protected static KinectManager instance = null;

	// available sensor interfaces
	protected List<DepthSensorInterface> sensorInterfaces = null;
	// primary SensorData structure
	protected KinectInterop.SensorData sensorData = null;

	// Depth and user maps
	//protected KinectInterop.DepthBuffer depthImage;
	//protected KinectInterop.BodyIndexBuffer bodyIndexImage;
	//protected KinectInterop.UserHistogramBuffer userHistogramImage;
	protected Color32[] usersHistogramImage;
	protected ushort[] usersPrevState;
	protected float[] usersHistogramMap;

	protected RenderTexture usersLblTex = null;
	protected Texture2D usersLblTex2D = null;
	protected Rect usersMapRect;
	protected int usersMapSize;
	//protected int minDepth;
	//protected int maxDepth;
	
	// Color map
	//protected KinectInterop.ColorBuffer colorImage;
	//protected Texture2D usersClrTex;
	protected Rect usersClrRect;
	protected int usersClrSize;
	
	// Kinect body frame data
	protected KinectInterop.BodyFrameData bodyFrame;
	//private Int64 lastBodyFrameTime = 0;
	
	// List of all users
	protected List<Int64> alUserIds = new List<Int64>();
	protected Dictionary<Int64, int> dictUserIdToIndex = new Dictionary<Int64, int>();
	protected Int64[] aUserIndexIds = new Int64[KinectInterop.Constants.MaxBodyCount];
	protected Dictionary<Int64, float> dictUserIdToTime = new Dictionary<Int64, float>();

	// Whether the users are limited by number or distance
	protected bool bLimitedUsers = false;
	
	// Primary (first or closest) user ID
	protected Int64 liPrimaryUserId = 0;
	
	// Kinect to world matrix
	protected Matrix4x4 kinectToWorld = Matrix4x4.zero;
	//private Matrix4x4 mOrient = Matrix4x4.zero;

	// Calibration gesture data for each player
	protected Dictionary<Int64, KinectGestures.GestureData> playerCalibrationData = new Dictionary<Int64, KinectGestures.GestureData>();
	
	// gestures data and parameters
	protected Dictionary<Int64, List<KinectGestures.GestureData>> playerGesturesData = new Dictionary<Int64, List<KinectGestures.GestureData>>();
	protected Dictionary<Int64, float> gesturesTrackingAtTime = new Dictionary<Int64, float>();
	
	//// List of Gesture Listeners. They must implement KinectGestures.GestureListenerInterface
	//public List<KinectGestures.GestureListenerInterface> gestureListenerInts;
	
	// Body filter instances
	protected JointPositionsFilter jointPositionFilter = null;
	protected BoneOrientationsConstraint boneConstraintsFilter = null;
	//protected BoneOrientationsFilter boneOrientationFilter = null;

    protected JointVelocitiesFilter jointVelocityFilter = null; 


	// background kinect thread
	//protected System.Threading.Thread kinectReaderThread = null;
	protected bool kinectReaderRunning = false;

    // if the background removal was used before pause
    protected bool backgroundRemovalInited = false;
    protected bool backgroundRemovalHiRes = false;


	/// <summary>
	/// Gets the single KinectManager instance.
	/// </summary>
	/// <value>The KinectManager instance.</value>
    public static KinectManager Instance
    {
        get
        {
            return instance;
        }
    }

	/// <summary>
	/// Determines if the sensor and KinectManager-component are initialized and ready to use.
	/// </summary>
	/// <returns><c>true</c> if Kinect is initialized; otherwise, <c>false</c>.</returns>
	public static bool IsKinectInitialized()
	{
		return instance != null ? instance.kinectInitialized : false;
	}
	
	/// <summary>
	/// Determines if the sensor and KinectManager-component are initialized and ready to use.
	/// </summary>
	/// <returns><c>true</c> if Kinect is initialized; otherwise, <c>false</c>.</returns>
	public bool IsInitialized()
	{
		return kinectInitialized;
	}

	/// <summary>
	/// Gets the sensor data structure (this structure should not be modified and must be used only internally).
	/// </summary>
	/// <returns>The sensor data.</returns>
	internal KinectInterop.SensorData GetSensorData()
	{
		return sensorData;
	}

	/// <summary>
	/// Gets the selected depth-sensor platform.
	/// </summary>
	/// <returns>The selected depth-sensor platform.</returns>
	public KinectInterop.DepthSensorPlatform GetSensorPlatform()
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetSensorPlatform();
		}
		
		return KinectInterop.DepthSensorPlatform.None;
	}
	
	/// <summary>
	/// Gets the number of bodies, tracked by the sensor.
	/// </summary>
	/// <returns>The body count.</returns>
	public int GetBodyCount()
	{
		return sensorData != null ? sensorData.bodyCount : 0;
	}
	
	/// <summary>
	/// Gets the the number of body joints, tracked by the sensor.
	/// </summary>
	/// <returns>The count of joints.</returns>
	public int GetJointCount()
	{
		return sensorData != null ? sensorData.jointCount : 0;
	}

	/// <summary>
	/// Gets the index of the joint in the joint's array
	/// </summary>
	/// <returns>The joint's index in the array.</returns>
	/// <param name="joint">Joint.</param>
	public int GetJointIndex(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetJointIndex(joint);
		}
		
		// fallback - index matches the joint
		return (int)joint;
	}
	
//	// returns the joint at given index
//	public KinectInterop.JointType GetJointAtIndex(int index)
//	{
//		if(sensorData != null && sensorData.sensorInterface != null)
//		{
//			return sensorData.sensorInterface.GetJointAtIndex(index);
//		}
//		
//		// fallback - index matches the joint
//		return (KinectInterop.JointType)index;
//	}
	
	/// <summary>
	/// Gets the parent joint of the given joint.
	/// </summary>
	/// <returns>The parent joint.</returns>
	/// <param name="joint">Joint.</param>
	public KinectInterop.JointType GetParentJoint(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetParentJoint(joint);
		}

		// fall back - return the same joint (i.e. end-joint)
		return joint;
	}

	/// <summary>
	/// Gets the next joint of the given joint.
	/// </summary>
	/// <returns>The next joint.</returns>
	/// <param name="joint">Joint.</param>
	public KinectInterop.JointType GetNextJoint(KinectInterop.JointType joint)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.GetNextJoint(joint);
		}
		
		// fall back - return the same joint (i.e. end-joint)
		return joint;
	}
	
	/// <summary>
	/// Gets the width of the color image, returned by the sensor.
	/// </summary>
	/// <returns>The color image width.</returns>
	public int GetColorImageWidth()
	{
		return sensorData != null ? sensorData.colorImageWidth : 0;
	}
	
	/// <summary>
	/// Gets the height of the color image, returned by the sensor.
	/// </summary>
	/// <returns>The color image height.</returns>
	public int GetColorImageHeight()
	{
		return sensorData != null ? sensorData.colorImageHeight : 0;
	}

	/// <summary>
	/// Gets the color image scale.
	/// </summary>
	/// <returns>The color image scale.</returns>
	public Vector3 GetColorImageScale()
	{
		return sensorData != null ? sensorData.colorImageScale : Vector3.one;
	}

	/// <summary>
	/// Gets the width of the depth image, returned by the sensor.
	/// </summary>
	/// <returns>The depth image width.</returns>
	public int GetDepthImageWidth()
	{
		return sensorData != null ? sensorData.depthImageWidth : 0;
	}
	
	/// <summary>
	/// Gets the height of the depth image, returned by the sensor.
	/// </summary>
	/// <returns>The depth image height.</returns>
	public int GetDepthImageHeight()
	{
		return sensorData != null ? sensorData.depthImageHeight : 0;
	}
	
	/// <summary>
	/// Gets the raw body index data, if ComputeUserMap is true.
	/// </summary>
	/// <returns>The raw body index data.</returns>
	public byte[] GetRawBodyIndexMap()
	{
		return sensorData != null ? sensorData.bodyIndexImage : null;
	}
	
	/// <summary>
	/// Gets the raw depth data, if ComputeUserMap is true.
	/// </summary>
	/// <returns>The raw depth map.</returns>
	public ushort[] GetRawDepthMap()
	{
		return sensorData != null ? sensorData.depthImage : null;
	}

	/// <summary>
	/// Gets the raw infrared data, if ComputeInfraredMap is true.
	/// </summary>
	/// <returns>The raw infrared map.</returns>
	public ushort[] GetRawInfraredMap()
	{
		return sensorData != null ? sensorData.infraredImage : null;
	}

	
	/// <summary>
	/// Gets the users texture, if ComputeUserMap is true
	/// </summary>
	/// <returns>The users texture.</returns>
    public Texture GetUsersLblTex()
    { 
		return usersLblTex;
	}
	
	/// <summary>
	/// Gets the users 2d-texture, if ComputeUserMap is true
	/// </summary>
	/// <returns>The users 2d-texture.</returns>
	public Texture2D GetUsersLblTex2D()
	{
		bool bNewTexture = false;

		if (usersLblTex2D == null) 
		{
			usersLblTex2D = new Texture2D(sensorData.depthImageWidth, sensorData.depthImageHeight, TextureFormat.ARGB32, false);
			bNewTexture = true;
		}

		if (bNewTexture && usersLblTex) 
		{
			KinectInterop.RenderTex2Tex2D(usersLblTex, ref usersLblTex2D);
		}

		return usersLblTex2D;
	}

	/// <summary>
	/// Gets the color image texture,if ComputeColorMap is true
	/// </summary>
	/// <returns>The color image texture.</returns>
	public Texture GetUsersClrTex()
	{ 
		if(sensorData != null)
		{
			return sensorData.colorImageTexture2D ? sensorData.colorImageTexture2D : sensorData.colorImageTexture;
		}

		return null;
	}

	/// <summary>
	/// Gets the color image 2d-texture,if ComputeColorMap is true
	/// </summary>
	/// <returns>The color image 2d-texture.</returns>
	public Texture2D GetUsersClrTex2D()
	{
		if(sensorData != null)
		{
			if(!sensorData.colorImageTexture2D)
			{
				sensorData.colorImageTexture2D = new Texture2D(sensorData.colorImageWidth, sensorData.colorImageHeight, TextureFormat.RGBA32, false);
			}

			return sensorData.colorImageTexture2D;
		}

		return null;
	}

	/// <summary>
	/// Determines whether at least one user is currently detected by the sensor
	/// </summary>
	/// <returns><c>true</c> if at least one user is detected; otherwise, <c>false</c>.</returns>
	public bool IsUserDetected()
	{
		return kinectInitialized && (alUserIds.Count > 0);
	}
	
	/// <summary>
	/// Determines whether the user with the specified index is currently detected by the sensor
	/// </summary>
	/// <returns><c>true</c> if the user is detected; otherwise, <c>false</c>.</returns>
	/// <param name="i">The user index.</param>
	public bool IsUserDetected(int i)
	{
		if(i >= 0 && i < KinectInterop.Constants.MaxBodyCount)
		{
			return (aUserIndexIds[i] != 0);
		}

		return false;
	}

	/// <summary>
	/// Determines whether the user with the specified userId is in the list of tracked users or not.
	/// </summary>
	/// <returns><c>true</c> if the user with the specified userId is tracked; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User identifier.</param>
	public bool IsUserTracked(Int64 userId)
	{
		return dictUserIdToIndex.ContainsKey(userId);
	}
	
	/// <summary>
	/// Gets the number of currently detected users.
	/// </summary>
	/// <returns>The users count.</returns>
	public int GetUsersCount()
	{
		return alUserIds.Count;
	}

	/// <summary>
	/// Gets IDs of all currently tracked users.
	/// </summary>
	/// <returns>The list of all currently tracked users.</returns>
	public List<long> GetAllUserIds()
	{
		return new List<long>(alUserIds);
	}
	
	/// <summary>
	/// Gets the user ID by the specified user index.
	/// </summary>
	/// <returns>The user ID by index.</returns>
	/// <param name="i">The user index.</param>
	public Int64 GetUserIdByIndex(int i)
	{
//		if(i >= 0 && i < alUserIds.Count)
//		{
//			return alUserIds[i];
//		}
		
		if(i >= 0 && i < KinectInterop.Constants.MaxBodyCount)
		{
			return aUserIndexIds[i];
		}
		
		return 0;
	}

	/// <summary>
	/// Gets the user index by the specified user ID.
	/// </summary>
	/// <returns>The user index by user ID.</returns>
	/// <param name="userId">User ID</param>
	public int GetUserIndexById(Int64 userId)
	{
//		for(int i = 0; i < alUserIds.Count; i++)
//		{
//			if(alUserIds[i] == userId)
//			{
//				return i;
//			}
//		}

		if(userId == 0)
			return -1;

		for(int i = 0; i < aUserIndexIds.Length; i++)
		{
			if(aUserIndexIds[i] == userId)
			{
				return i;
			}
		}
		
		return -1;
	}
	
	/// <summary>
	/// Gets the body index by the specified user ID, or -1 if the user ID does not exist.
	/// </summary>
	/// <returns>The body index by user ID.</returns>
	/// <param name="userId">User ID</param>
	public int GetBodyIndexByUserId(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			return index;
		}
		
		return -1;
	}

	/// <summary>
	/// Gets the list of tracked body indices.
	/// </summary>
	/// <returns>The list of body indices.</returns>
	public List<int> GetTrackedBodyIndices()
	{
		List<int> alBodyIndices = new List<int>(dictUserIdToIndex.Values);
		return alBodyIndices;
	}

	/// <summary>
	/// Determines whether the tracked users are limited by their number or distance or not.
	/// </summary>
	/// <returns><c>true</c> if the users are limited by number or distance; otherwise, <c>false</c>.</returns>
	public bool IsTrackedUsersLimited()
	{
		return bLimitedUsers;
	}
	
	/// <summary>
	/// Gets the UserID of the primary user (the first or the closest one), or 0 if no user is detected.
	/// </summary>
	/// <returns>The primary user ID.</returns>
	public Int64 GetPrimaryUserID()
	{
		return liPrimaryUserId;
	}

	/// <summary>
	/// Sets the primary user ID, in order to change the active user.
	/// </summary>
	/// <returns><c>true</c>, if primary user ID was set, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	public bool SetPrimaryUserID(Int64 userId)
	{
		bool bResult = false;

		if(alUserIds.Contains(userId) || (userId == 0))
		{
			liPrimaryUserId = userId;
			bResult = true;
		}

		return bResult;
	}

	/// <summary>
	/// Gets the body index [0-5], if there is single body selected to be displayed on the user map, or -1 if all bodies are displayed.
	/// </summary>
	/// <returns>The displayed body index [0-5], or -1 if all bodies are displayed.</returns>
	public int GetDisplayedBodyIndex()
	{
		if(sensorData != null)
		{
			return sensorData.selectedBodyIndex != 255 ? sensorData.selectedBodyIndex : -1;
		}

		return -1;
	}

	/// <summary>
	/// Sets the body index [0-5], if a single body must be displayed on the user map, or -1 if all bodies must be displayed.
	/// </summary>
	/// <returns><c>true</c>, if the change was successful, <c>false</c> otherwise.</returns>
	/// <param name="iBodyIndex">The single body index, or -1 if all bodies must be displayed.</param>
	public bool SetDisplayedBodyIndex(int iBodyIndex)
	{
		if(sensorData != null)
		{
			sensorData.selectedBodyIndex = (byte)(iBodyIndex >= 0 ? iBodyIndex : 255);
		}

		return false;
	}
	
	/// <summary>
	/// Gets the last body frame timestamp.
	/// </summary>
	/// <returns>The last body frame timestamp.</returns>
	public long GetBodyFrameTimestamp()
	{
		return bodyFrame.liRelativeTime;
	}
	
	// do not change the data in the structure directly
	/// <summary>
	/// Gets the user body data (for internal purposes only).
	/// </summary>
	/// <returns>The user body data.</returns>
	/// <param name="userId">User ID</param>
	internal KinectInterop.BodyData GetUserBodyData(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount)
			{
				return bodyFrame.bodyData[index];
			}
		}
		
		return new KinectInterop.BodyData();
	}

	/// <summary>
	/// Gets the kinect to world matrix.
	/// </summary>
	/// <returns>The kinect to world matrix.</returns>
	public Matrix4x4 GetKinectToWorldMatrix()
	{
		return kinectToWorld;
	}

	/// <summary>
	/// Updates the kinect to world transform matrix, according to the current values of SensorHeight, SensorAngle and FlipLeftRight.
	/// </summary>
	public void UpdateKinectToWorldMatrix()
	{
		//create the transform matrix - kinect to world
		Vector3 vSensorPos = new Vector3(0f, sensorHeight, 0f);
		Quaternion qSensorRot = Quaternion.Euler(-sensorAngle, 0f, 0f);
		Vector3 vSensorScale = !flipLeftRight ? Vector3.one : new Vector3(-1f, 1f, 1f);

		kinectToWorld.SetTRS(vSensorPos, qSensorRot, vSensorScale);
	}

	/// <summary>
	/// Sets the kinect to world matrix.
	/// </summary>
	/// <param name="sensorPos">Sensor position.</param>
	/// <param name="sensorRot">Sensor rotation.</param>
	/// <param name="sensorScale">Position scale (could be used to flip left-right).</param>
	public void SetKinectToWorldMatrix(Vector3 sensorPos, Quaternion sensorRot, Vector3 sensorScale)
	{
		kinectToWorld.SetTRS(sensorPos, sensorRot, sensorScale);

		sensorHeight = sensorPos.y;
		sensorAngle = -sensorRot.eulerAngles.x;
		flipLeftRight = sensorScale.x < 0f;

		// enable or disable getting height and angle info
		autoHeightAngle = AutoHeightAngle.DontUse;
		sensorData.hintHeightAngle = (autoHeightAngle != AutoHeightAngle.DontUse);
	}
	
	/// <summary>
	/// Gets the user position, relative to the sensor, in meters.
	/// </summary>
	/// <returns>The user position.</returns>
	/// <param name="userId">User ID</param>
	public Vector3 GetUserPosition(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].position;
			}
		}
		
		return Vector3.zero;
	}
	
	/// <summary>
	/// Gets the user orientation.
	/// </summary>
	/// <returns>The user rotation.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="flip">If set to <c>true</c>, this means non-mirrored rotation.</param>
	public Quaternion GetUserOrientation(Int64 userId, bool flip)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(flip)
					return bodyFrame.bodyData[index].normalRotation;
				else
					return bodyFrame.bodyData[index].mirroredRotation;
			}
		}
		
		return Quaternion.identity;
	}
	
	/// <summary>
	/// Gets the tracking state of the joint.
	/// </summary>
	/// <returns>The joint tracking state.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public KinectInterop.TrackingState GetJointTrackingState(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					return  bodyFrame.bodyData[index].joint[joint].trackingState;
				}
			}
		}
		
		return KinectInterop.TrackingState.NotTracked;
	}
	
	/// <summary>
	/// Determines whether the given joint of the specified user is being tracked.
	/// </summary>
	/// <returns><c>true</c> if this instance is joint tracked the specified userId joint; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public bool IsJointTracked(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					
					return ignoreInferredJoints ? (jointData.trackingState == KinectInterop.TrackingState.Tracked) : 
						(jointData.trackingState != KinectInterop.TrackingState.NotTracked);
				}
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the joint position of the specified user, in Kinect coordinate system, in meters.
	/// </summary>
	/// <returns>The joint kinect position.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public Vector3 GetJointKinectPosition(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					return jointData.kinectPos;
				}
			}
		}
		
		return Vector3.zero;
	}

	/// <summary>
	/// Gets the joint position of the specified user, in meters.
	/// </summary>
	/// <returns>The joint position.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public Vector3 GetJointPosition(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					return jointData.position;
				}
			}
		}
		
		return Vector3.zero;
	}

	/// <summary>
	/// Gets the joint position of the specified user with flipped x-coordinate, in meters.
	/// </summary>
	/// <returns>The joint position.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public Vector3 GetJointPositionFlipX(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];

			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 jointPos = jointData.position;
					jointPos.x = -jointPos.x;

					return jointPos;
				}
			}
		}

		return Vector3.zero;
	}

	/// <summary>
	/// Gets the joint velocity for the specified user and joint, in meters/s.
	/// </summary>
	/// <returns>The joint velocity.</returns>
	/// <param name="userId">User ID.</param>
	/// <param name="joint">Joint index.</param>
	public Vector3 GetJointVelocity(Int64 userId, int joint)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
                    return bodyFrame.bodyData[index].joint[joint].posVel;
				}
			}
		}
		
	    return Vector3.zero;
    }
	
	/// <summary>
	/// Gets the joint direction of the specified user, relative to its parent joint.
	/// </summary>
	/// <returns>The joint direction.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	/// <param name="flipX">If set to <c>true</c> flips the X-coordinate</param>
	/// <param name="flipZ">If set to <c>true</c> flips the Z-coordinate</param>
	public Vector3 GetJointDirection(Int64 userId, int joint, bool flipX, bool flipZ)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 jointDir = jointData.direction;

					if(flipX)
						jointDir.x = -jointDir.x;
					
					if(flipZ)
						jointDir.z = -jointDir.z;
					
					return jointDir;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	/// <summary>
	/// Gets the direction between the given joints of the specified user.
	/// </summary>
	/// <returns>The direction between joints.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="firstJoint">First joint index</param>
	/// <param name="secondJoint">Second joint index</param>
	/// <param name="flipX">If set to <c>true</c> flips the X-coordinate</param>
	/// <param name="flipZ">If set to <c>true</c> flips the Z-coordinate</param>
	public Vector3 GetDirectionBetweenJoints(Int64 userId, int firstJoint, int secondJoint, bool flipX, bool flipZ)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				
				if(firstJoint >= 0 && firstJoint < sensorData.jointCount &&
					secondJoint >= 0 && secondJoint < sensorData.jointCount)
				{
					Vector3 firstJointPos = bodyData.joint[firstJoint].position;
					Vector3 secondJointPos = bodyData.joint[secondJoint].position;
					Vector3 jointDir = secondJointPos - firstJointPos;

					if(flipX)
						jointDir.x = -jointDir.x;
					
					if(flipZ)
						jointDir.z = -jointDir.z;
					
					return jointDir;
				}
			}
		}
		
		return Vector3.zero;
	}
	
	/// <summary>
	/// Gets the joint orientation of the specified user.
	/// </summary>
	/// <returns>The joint rotation.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	/// <param name="flip">If set to <c>true</c>, this means non-mirrored rotation</param>
	public Quaternion GetJointOrientation(Int64 userId, int joint, bool flip)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(flip)
					return bodyFrame.bodyData[index].joint[joint].normalRotation;
				else
					return bodyFrame.bodyData[index].joint[joint].mirroredRotation;
			}
		}
		
		return Quaternion.identity;
	}

	/// <summary>
	/// Gets the angle between bones at the given joint.
	/// </summary>
	/// <returns>The angle at joint.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public float GetAngleAtJoint(Int64 userId, int joint)
	{
		int pjoint = (int)GetParentJoint((KinectInterop.JointType)joint);
		int njoint = (int)GetNextJoint((KinectInterop.JointType)joint);

		if (pjoint != joint && njoint != joint) 
		{
			Vector3 pos1 = GetJointPosition(userId, pjoint);
			Vector3 pos2 = GetJointPosition(userId, joint);
			Vector3 pos3 = GetJointPosition(userId, njoint);

			if (pos1 != Vector3.zero && pos2 != Vector3.zero && pos3 != Vector3.zero) 
			{
				Vector3 dirP = pos1 - pos2;
				Vector3 dirN = pos3 - pos2;
				float fAngle = Vector3.Angle(dirP, dirN);

				return fAngle;
			}
		}
	
		return 0f;
	}

	/// <summary>
	/// Gets the 3d overlay position of the given joint over the depth-image.
	/// </summary>
	/// <returns>The joint position for depth overlay.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	/// <param name="camera">Camera used to visualize the 3d overlay position</param>
	/// <param name="imageRect">Depth image rectangle on the screen</param>
	public Vector3 GetJointPosDepthOverlay(Int64 userId, int joint, Camera camera, Rect imageRect)
	{
		if(dictUserIdToIndex.ContainsKey(userId) && camera != null)
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 posJointRaw = jointData.kinectPos;
					
					if(posJointRaw != Vector3.zero)
					{
						// 3d position to depth
						Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);

						if(posDepth != Vector2.zero && sensorData != null)
						{
							if(!float.IsInfinity(posDepth.x) && !float.IsInfinity(posDepth.y))
							{
								float xScaled = (float)posDepth.x * imageRect.width / sensorData.depthImageWidth;
								float yScaled = (float)posDepth.y * imageRect.height / sensorData.depthImageHeight;

								float xScreen = imageRect.x + xScaled;
								//float yScreen = camera.pixelHeight - (imageRect.y + yScaled);
								float yScreen = imageRect.y + imageRect.height - yScaled;
								
								Plane cameraPlane = new Plane(camera.transform.forward, camera.transform.position);
								float zDistance = cameraPlane.GetDistanceToPoint(posJointRaw);

								Vector3 vPosJoint = camera.ScreenToWorldPoint(new Vector3(xScreen, yScreen, zDistance));
								
								return vPosJoint;
							}
						}
					}
				}
			}
		}
		
		return Vector3.zero;
	}

	/// <summary>
	/// Gets the 3d overlay position of the given joint over the color-image.
	/// </summary>
	/// <returns>The joint position for color overlay.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	/// <param name="camera">Camera used to visualize the 3d overlay position</param>
	/// <param name="imageRect">Color image rectangle on the screen</param>
	public Vector3 GetJointPosColorOverlay(Int64 userId, int joint, Camera camera, Rect imageRect)
	{
		if(dictUserIdToIndex.ContainsKey(userId) && camera != null)
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 posJointRaw = jointData.kinectPos;
					
					if(posJointRaw != Vector3.zero)
					{
						// 3d position to depth
						Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);
						ushort depthValue = GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
						
						if(posDepth != Vector2.zero && depthValue > 0 && sensorData != null)
						{
							// depth pos to color pos
							Vector2 posColor = MapDepthPointToColorCoords(posDepth, depthValue);

							if(!float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
							{
								float xScaled = (float)posColor.x * imageRect.width / sensorData.colorImageWidth;
								float yScaled = (float)posColor.y * imageRect.height / sensorData.colorImageHeight;
								
								float xScreen = imageRect.x + xScaled;
								//float yScreen = camera.pixelHeight - (imageRect.y + yScaled);
								float yScreen = imageRect.y + imageRect.height - yScaled;

								Plane cameraPlane = new Plane(camera.transform.forward, camera.transform.position);
								float zDistance = cameraPlane.GetDistanceToPoint(posJointRaw);
								//float zDistance = (jointData.kinectPos - camera.transform.position).magnitude;

								//Vector3 vPosJoint = camera.ViewportToWorldPoint(new Vector3(xNorm, yNorm, zDistance));
								Vector3 vPosJoint = camera.ScreenToWorldPoint(new Vector3(xScreen, yScreen, zDistance));

								return vPosJoint;
							}
						}
					}
				}
			}
		}

		return Vector3.zero;
	}

	/// <summary>
	/// Gets the 2d overlay position of the given joint over the given image.
	/// </summary>
	/// <returns>The 2d joint position for color overlay.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	/// <param name="imageRect">Color image rectangle on the screen</param>
	public Vector2 GetJointPosColorOverlay(Int64 userId, int joint, Rect imageRect)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];

			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				if(joint >= 0 && joint < sensorData.jointCount)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[index].joint[joint];
					Vector3 posJointRaw = jointData.kinectPos;

					if(posJointRaw != Vector3.zero)
					{
						// 3d position to depth
						Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);
						ushort depthValue = GetDepthForPixel((int)posDepth.x, (int)posDepth.y);

						if(posDepth != Vector2.zero && depthValue > 0 && sensorData != null)
						{
							// depth pos to color pos
							Vector2 posColor = MapDepthPointToColorCoords(posDepth, depthValue);

							if(!float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
							{
								float xScaled = (float)posColor.x * imageRect.width / sensorData.colorImageWidth;
								float yScaled = (float)posColor.y * imageRect.height / sensorData.colorImageHeight;

								float xImage = imageRect.x + xScaled;
								float yImage = imageRect.y + imageRect.height - yScaled;

								return new Vector2(xImage, yImage);
							}
						}
					}
				}
			}
		}

		return Vector2.zero;
	}

	/// <summary>
	/// Gets the joint position on the depth map texture.
	/// </summary>
	/// <returns>The joint position in texture coordinates.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public Vector2 GetJointDepthMapPos(Int64 userId, int joint)
	{
		Vector2 posDepth = Vector2.zero;

		Vector3 posJointRaw = GetJointKinectPosition(userId, joint);
		if(posJointRaw != Vector3.zero)
		{
			posDepth = MapSpacePointToDepthCoords(posJointRaw);

			if(posDepth != Vector2.zero)
			{
				float xScaled = (float)posDepth.x / GetDepthImageWidth();
				float yScaled = (float)posDepth.y / GetDepthImageHeight();
				
				posDepth = new Vector2(xScaled, 1f - yScaled);
			}
		}
		
		return posDepth;
	}
	
	/// <summary>
	/// Gets the joint position on the color map texture.
	/// </summary>
	/// <returns>The joint position in texture coordinates.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="joint">Joint index</param>
	public Vector2 GetJointColorMapPos(Int64 userId, int joint)
	{
		Vector2 posColor = Vector2.zero;

		Vector3 posJointRaw = GetJointKinectPosition(userId, joint);
		if(posJointRaw != Vector3.zero)
		{
			// 3d position to depth
			Vector2 posDepth = MapSpacePointToDepthCoords(posJointRaw);
			ushort depthValue = GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
			
			if(posDepth != Vector2.zero && depthValue > 0)
			{
				// depth pos to color pos
				posColor = MapDepthPointToColorCoords(posDepth, depthValue);
				
				if(!float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
				{
					float xScaled = (float)posColor.x / GetColorImageWidth();
					float yScaled = (float)posColor.y / GetColorImageHeight();
					
					posColor = new Vector2(xScaled, 1f - yScaled);
				}
				else
				{
					posColor = Vector2.zero;
				}
			}
		}
		
		return posColor;
	}
	
	/// <summary>
	/// Determines whether the given user is turned around or not.
	/// </summary>
	/// <returns><c>true</c> if the user is turned around; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User ID</param>
	public bool IsUserTurnedAround(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
			   bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].isTurnedAround;
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Determines whether the left hand confidence is high for the specified user.
	/// </summary>
	/// <returns><c>true</c> if the left hand confidence is high; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User ID</param>
	public bool IsLeftHandConfidenceHigh(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return (bodyFrame.bodyData[index].leftHandConfidence == KinectInterop.TrackingConfidence.High);
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Determines whether the right hand confidence is high for the specified user.
	/// </summary>
	/// <returns><c>true</c> if the right hand confidence is high; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User ID</param>
	public bool IsRightHandConfidenceHigh(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return (bodyFrame.bodyData[index].rightHandConfidence == KinectInterop.TrackingConfidence.High);
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the left hand state for the specified user.
	/// </summary>
	/// <returns>The left hand state.</returns>
	/// <param name="userId">User ID</param>
	public KinectInterop.HandState GetLeftHandState(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].leftHandState;
			}
		}
		
		return KinectInterop.HandState.NotTracked;
	}
	
	/// <summary>
	/// Gets the right hand state for the specified user.
	/// </summary>
	/// <returns>The right hand state.</returns>
	/// <param name="userId">User ID</param>
	public KinectInterop.HandState GetRightHandState(Int64 userId)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				return bodyFrame.bodyData[index].rightHandState;
			}
		}
		
		return KinectInterop.HandState.NotTracked;
	}
	
	/// <summary>
	/// Gets the left hand interaction box for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if left hand interaction box was gotten, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="leftBotBack">Vector containing the left, bottom and back coordinates, in meters</param>
	/// <param name="rightTopFront">Vector containing the right, top and front coordinates, in meters</param>
	/// <param name="bValidBox">If set to <c>true</c>, the previously set coordinates are valid</param>
	public bool GetLeftHandInteractionBox(Int64 userId, ref Vector3 leftBotBack, ref Vector3 rightTopFront, bool bValidBox)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				bool bResult = true;
				
				if(bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					rightTopFront.x = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position.x;
					leftBotBack.x = rightTopFront.x - 2 * (rightTopFront.x - bodyData.joint[(int)KinectInterop.JointType.HipLeft].position.x);
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.y = bodyData.joint[(int)KinectInterop.JointType.HipRight].position.y;
					rightTopFront.y = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position.y;
					
					float fDelta = (rightTopFront.y - leftBotBack.y) * 0.35f; // * 2 / 3;
					leftBotBack.y += fDelta;
					rightTopFront.y += fDelta;
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					//leftBotBack.z = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z;
					leftBotBack.z = !ignoreZCoordinates ? bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z :
						(bodyData.joint[(int)KinectInterop.JointType.HandLeft].position.z + 0.1f);
					rightTopFront.z = leftBotBack.z - 0.5f;
				}
				else
				{
					bResult = bValidBox;
				}
				
				return bResult;
			}
		}
		
		return false;
	}
	
	// returns the interaction box for the right hand of the specified user, in meters
	/// <summary>
	/// Gets the right hand interaction box for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if right hand interaction box was gotten, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="leftBotBack">Vector containing the left, bottom and back coordinates, in meters</param>
	/// <param name="rightTopFront">ector containing the right, top and front coordinates, in meters</param>
	/// <param name="bValidBox">If set to <c>true</c>, the previously set coordinates are valid</param>
	public bool GetRightHandInteractionBox(Int64 userId, ref Vector3 leftBotBack, ref Vector3 rightTopFront, bool bValidBox)
	{
		if(dictUserIdToIndex.ContainsKey(userId))
		{
			int index = dictUserIdToIndex[userId];
			
			if(index >= 0 && index < sensorData.bodyCount && 
				bodyFrame.bodyData[index].bIsTracked != 0)
			{
				KinectInterop.BodyData bodyData = bodyFrame.bodyData[index];
				bool bResult = true;
				
				if(bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.x = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position.x;
					rightTopFront.x = leftBotBack.x + 2 * (bodyData.joint[(int)KinectInterop.JointType.HipRight].position.x - leftBotBack.x);
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
				   bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					leftBotBack.y = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position.y;
					rightTopFront.y = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position.y;
					
					float fDelta = (rightTopFront.y - leftBotBack.y) * 0.35f; // * 2 / 3;
					leftBotBack.y += fDelta;
					rightTopFront.y += fDelta;
				}
				else
				{
					bResult = bValidBox;
				}
					
				if(bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState != KinectInterop.TrackingState.NotTracked)
				{
					//leftBotBack.z = bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z;
					leftBotBack.z = !ignoreZCoordinates ? bodyData.joint[(int)KinectInterop.JointType.SpineBase].position.z :
						(bodyData.joint[(int)KinectInterop.JointType.HandRight].position.z + 0.1f);
					rightTopFront.z = leftBotBack.z - 0.5f;
				}
				else
				{
					bResult = bValidBox;
				}
				
				return bResult;
			}
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the depth value for the specified pixel, if ComputeUserMap is true.
	/// </summary>
	/// <returns>The depth value.</returns>
	/// <param name="x">The X coordinate of the pixel.</param>
	/// <param name="y">The Y coordinate of the pixel.</param>
	public ushort GetDepthForPixel(int x, int y)
	{
		if(sensorData != null && sensorData.depthImage != null)
		{
			int index = y * sensorData.depthImageWidth + x;
			
			if(index >= 0 && index < sensorData.depthImage.Length)
			{
				return sensorData.depthImage[index];
			}
		}

		return 0;
	}

	/// <summary>
	/// Gets the depth value for the specified pixel, if ComputeUserMap is true.
	/// </summary>
	/// <returns>The depth value.</returns>
	/// <param name="index">Depth index.</param>
	public ushort GetDepthForIndex(int index)
	{
		if(sensorData != null && sensorData.depthImage != null)
		{
			if(index >= 0 && index < sensorData.depthImage.Length)
			{
				return sensorData.depthImage[index];
			}
		}
		
		return 0;
	}
	
	/// <summary>
	/// Returns the space coordinates of a depth-map point, or Vector3.zero if the sensor is not initialized
	/// </summary>
	/// <returns>The space coordinates.</returns>
	/// <param name="posPoint">Depth point coordinates</param>
	/// <param name="depthValue">Depth value</param>
	/// <param name="bWorldCoords">If set to <c>true</c>, applies the sensor height and angle to the space coordinates.</param>
	public Vector3 MapDepthPointToSpaceCoords(Vector2 posPoint, ushort depthValue, bool bWorldCoords)
	{
		Vector3 posKinect = Vector3.zero;
		
		if(kinectInitialized)
		{
			posKinect = KinectInterop.MapDepthPointToSpaceCoords(sensorData, posPoint, depthValue);
			
			if(bWorldCoords)
			{
				posKinect = kinectToWorld.MultiplyPoint3x4(posKinect);
			}
		}
		
		return posKinect;
	}
	
	/// <summary>
	/// Maps the depth frame to space coordinates.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="avSpaceCoords">Buffer for the depth-to-space coordinates.</param>
	public bool MapDepthFrameToSpaceCoords(ref Vector3[] avSpaceCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.depthImage != null)
		{
			if(avSpaceCoords == null || avSpaceCoords.Length == 0)
			{
				avSpaceCoords = new Vector3[sensorData.depthImageWidth * sensorData.depthImageHeight];
			}
			
			bResult = KinectInterop.MapDepthFrameToSpaceCoords(sensorData, ref avSpaceCoords);
		}
		
		return bResult;
	}
	
	/// <summary>
	/// Returns the depth-map coordinates of a space point, or Vector2.zero if Kinect is not initialized
	/// </summary>
	/// <returns>The depth-map coordinates.</returns>
	/// <param name="posPoint">Space point coordinates</param>
	public Vector2 MapSpacePointToDepthCoords(Vector3 posPoint)
	{
		Vector2 posDepth = Vector2.zero;
		
		if(kinectInitialized)
		{
			posDepth = KinectInterop.MapSpacePointToDepthCoords(sensorData, posPoint);
		}
		
		return posDepth;
	}
	
	/// <summary>
	/// Returns the color-map coordinates of a depth point.
	/// </summary>
	/// <returns>The color-map coordinates.</returns>
	/// <param name="posPoint">Depth point coordinates</param>
	/// <param name="depthValue">Depth value</param>
	public Vector2 MapDepthPointToColorCoords(Vector2 posPoint, ushort depthValue)
	{
		Vector2 posColor = Vector2.zero;
		
		if(kinectInitialized)
		{
			posColor = KinectInterop.MapDepthPointToColorCoords(sensorData, posPoint, depthValue);
		}
		
		return posColor;
	}

	/// <summary>
	/// Maps the depth frame to color coordinates.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="avColorCoords">Buffer for depth-to-color coordinates.</param>
	public bool MapDepthFrameToColorCoords(ref Vector2[] avColorCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.depthImage != null && sensorData.colorImage != null)
		{
			if(avColorCoords == null || avColorCoords.Length == 0)
			{
				avColorCoords = new Vector2[sensorData.depthImageWidth * sensorData.depthImageHeight];
			}
			
			bResult = KinectInterop.MapDepthFrameToColorCoords(sensorData, ref avColorCoords);
		}
		
		return bResult;
	}

	/// <summary>
	/// Maps the color frame to depth coordinates.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="avDepthCoords">Buffer for color-to-depth coordinates.</param>
	public bool MapColorFrameToDepthCoords(ref Vector2[] avDepthCoords)
	{
		bool bResult = false;
		
		if(kinectInitialized && sensorData.colorImage != null && sensorData.depthImage != null)
		{
			if(avDepthCoords == null || avDepthCoords.Length == 0)
			{
				avDepthCoords = new Vector2[sensorData.colorImageWidth * sensorData.colorImageWidth];
			}
			
			bResult = KinectInterop.MapColorFrameToDepthCoords(sensorData, ref avDepthCoords);
		}
		
		return bResult;
	}

	/// <summary>
	/// Returns the depth-map coordinates of a color point.
	/// </summary>
	/// <returns>The depth coords.</returns>
	/// <param name="colorPos">Color position.</param>
	/// <param name="bReadDepthCoordsIfNeeded">If set to <c>true</c> allows reading of depth coords, if needed.</param>
	public Vector2 MapColorPointToDepthCoords(Vector2 colorPos, bool bReadDepthCoordsIfNeeded)
	{
		Vector2 posDepth = Vector2.zero;
		
		if(kinectInitialized && sensorData.colorImage != null && sensorData.depthImage != null)
		{
			posDepth = KinectInterop.MapColorPointToDepthCoords(sensorData, colorPos, bReadDepthCoordsIfNeeded);
		}
		
		return posDepth;
	}
	
	/// <summary>
	/// Removes all currently detected users, allowing new user-detection process to start.
	/// </summary>
	public void ClearKinectUsers()
	{
		if(!kinectInitialized)
			return;

		// remove current users
		for(int i = alUserIds.Count - 1; i >= 0; i--)
		{
			Int64 userId = alUserIds[i];
			RemoveUser(userId);
		}
		
		ResetFilters();
	}

	/// <summary>
	/// Resets the Kinect data filters.
	/// </summary>
	public void ResetFilters()
	{
		if(jointPositionFilter != null)
		{
			jointPositionFilter.Reset();
		}
		
        if( jointVelocityFilter != null) 
        {
            jointVelocityFilter.Reset();
        }
	}
	
	/// <summary>
	/// Adds a gesture to the list of detected gestures for the specified user.
	/// </summary>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public void DetectGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : new List<KinectGestures.GestureData>();
		int index = GetGestureIndex(gesture, ref gesturesData);

		if(index >= 0)
		{
			DeleteGesture(UserId, gesture);
		}
		
		KinectGestures.GestureData gestureData = new KinectGestures.GestureData();
		
		gestureData.userId = UserId;
		gestureData.gesture = gesture;
		gestureData.state = 0;
		gestureData.joint = 0;
		gestureData.progress = 0f;
		gestureData.complete = false;
		gestureData.cancelled = false;
		
		gestureData.checkForGestures = new List<KinectGestures.Gestures>();
		switch(gesture)
		{
			case KinectGestures.Gestures.ZoomIn:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomOut);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.Wheel);			
				break;
				
			case KinectGestures.Gestures.ZoomOut:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomIn);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.Wheel);			
				break;
				
			case KinectGestures.Gestures.Wheel:
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomIn);
				gestureData.checkForGestures.Add(KinectGestures.Gestures.ZoomOut);			
				break;
		}

		gesturesData.Add(gestureData);
		playerGesturesData[UserId] = gesturesData;
		
		if(!gesturesTrackingAtTime.ContainsKey(UserId))
		{
			gesturesTrackingAtTime[UserId] = 0f;
		}
	}
	
	/// <summary>
	/// Resets the gesture state for the given gesture of the specified user.
	/// </summary>
	/// <returns><c>true</c>, if gesture was reset, <c>false</c> otherwise.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public bool ResetGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;
		if(index < 0)
			return false;
		
		KinectGestures.GestureData gestureData = gesturesData[index];
		
		gestureData.state = 0;
		gestureData.joint = 0;
		gestureData.progress = 0f;
		gestureData.complete = false;
		gestureData.cancelled = false;
		gestureData.startTrackingAtTime = Time.realtimeSinceStartup + KinectInterop.Constants.MinTimeBetweenSameGestures;

		gesturesData[index] = gestureData;
		playerGesturesData[UserId] = gesturesData;

		return true;
	}
	
	/// <summary>
	/// Resets the gesture states for all gestures of the specified user.
	/// </summary>
	/// <param name="UserId">User ID</param>
	public void ResetPlayerGestures(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			int listSize = gesturesData.Count;
			
			for(int i = 0; i < listSize; i++)
			{
				ResetGesture(UserId, gesturesData[i].gesture);
			}
		}
	}
	
	/// <summary>
	/// Deletes the gesture for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if gesture was deleted, <c>false</c> otherwise.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public bool DeleteGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;
		if(index < 0)
			return false;
		
		gesturesData.RemoveAt(index);
		playerGesturesData[UserId] = gesturesData;

		return true;
	}
	
	/// <summary>
	/// Deletes all gestures for the specified user.
	/// </summary>
	/// <param name="UserId">User ID</param>
	public void ClearGestures(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			gesturesData.Clear();
			playerGesturesData[UserId] = gesturesData;
		}
	}
	
	/// <summary>
	/// Gets the list of gestures for the specified user.
	/// </summary>
	/// <returns>The gestures list.</returns>
	/// <param name="UserId">User ID</param>
	public List<KinectGestures.Gestures> GetGesturesList(Int64 UserId)
	{
		List<KinectGestures.Gestures> list = new List<KinectGestures.Gestures>();
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		
		if(gesturesData != null)
		{
			foreach(KinectGestures.GestureData data in gesturesData)
				list.Add(data.gesture);
		}
		
		return list;
	}
	
	/// <summary>
	/// Gets the gestures count for the specified user.
	/// </summary>
	/// <returns>The gestures count.</returns>
	/// <param name="UserId">User ID</param>
	public int GetGesturesCount(Int64 UserId)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;

		if(gesturesData != null)
		{
			return gesturesData.Count;
		}

		return 0;
	}
	
	/// <summary>
	/// Gets the gesture at the specified index for the given user.
	/// </summary>
	/// <returns>The gesture at specified index.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="i">Index</param>
	public KinectGestures.Gestures GetGestureAtIndex(Int64 UserId, int i)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		
		if(gesturesData != null)
		{
			if(i >= 0 && i < gesturesData.Count)
			{
				return gesturesData[i].gesture;
			}
		}
		
		return KinectGestures.Gestures.None;
	}
	
	/// <summary>
	/// Determines whether the given gesture is in the list of gestures for the specified user.
	/// </summary>
	/// <returns><c>true</c> if the gesture is in the list of gestures for the specified user; otherwise, <c>false</c>.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public bool IsTrackingGesture(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		return index >= 0;
	}
	
	/// <summary>
	/// Determines whether the given gesture for the specified user is complete.
	/// </summary>
	/// <returns><c>true</c> if the gesture is complete; otherwise, <c>false</c>.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	/// <param name="bResetOnComplete">If set to <c>true</c>, resets the gesture state.</param>
	public bool IsGestureComplete(Int64 UserId, KinectGestures.Gestures gesture, bool bResetOnComplete)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			
			if(bResetOnComplete && gestureData.complete)
			{
				ResetPlayerGestures(UserId);
				return true;
			}
			
			return gestureData.complete;
		}
		
		return false;
	}
	
	/// <summary>
	/// Determines whether the given gesture for the specified user is canceled.
	/// </summary>
	/// <returns><c>true</c> if the gesture is canceled; otherwise, <c>false</c>.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public bool IsGestureCancelled(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.cancelled;
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the progress (in range [0, 1]) of the given gesture for the specified user.
	/// </summary>
	/// <returns>The gesture progress.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public float GetGestureProgress(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.progress;
		}
		
		return 0f;
	}
	
	/// <summary>
	/// Gets the normalized screen position of the given gesture for the specified user.
	/// </summary>
	/// <returns>The normalized screen position.</returns>
	/// <param name="UserId">User ID</param>
	/// <param name="gesture">Gesture type</param>
	public Vector3 GetGestureScreenPos(Int64 UserId, KinectGestures.Gestures gesture)
	{
		List<KinectGestures.GestureData> gesturesData = playerGesturesData.ContainsKey(UserId) ? playerGesturesData[UserId] : null;
		int index = gesturesData != null ? GetGestureIndex(gesture, ref gesturesData) : -1;

		if(index >= 0)
		{
			KinectGestures.GestureData gestureData = gesturesData[index];
			return gestureData.screenPos;
		}
		
		return Vector3.zero;
	}


	/// <summary>
	/// Gets the world matrix data as one csv line.
	/// </summary>
	/// <returns>The world matrix data as a csv line.</returns>
	public string GetWorldMatrixData(char delimiter)
	{
		return KinectInterop.GetMatrixAsCsv(ref kinectToWorld, delimiter);
	}


	/// <summary>
	/// Gets the body hand as one csv line, or returns empty string if there is no new body frame.
	/// </summary>
	/// <returns>The body hand as a csv line.</returns>
	/// <param name="liRelTime">Reference to variable, used to compare frame times.</param>
	public string GetBodyHandData(ref long liRelTime, char delimiter)
	{
		return KinectInterop.GetHandsDataAsCsv(sensorData, ref bodyFrame, ref liRelTime, delimiter);
	}


	/// <summary>
	/// Gets the body frame as one csv line, or returns empty string if there is no new body frame.
	/// </summary>
	/// <returns>The body frame as a csv line.</returns>
	/// <param name="liRelTime">Reference to variable, used to compare frame times.</param>
	/// <param name="fUnityTime">Reference to variable, used to save the current Unity time.</param>
	public string GetBodyFrameData(ref long liRelTime, ref float fUnityTime, char delimiter)
	{
		return KinectInterop.GetBodyFrameAsCsv(sensorData, ref bodyFrame, ref liRelTime, ref fUnityTime, delimiter);
	}


	/// <summary>
	/// Determines whether the play mode is enabled or not.
	/// </summary>
	/// <returns><c>true</c> if the play mode is enabled; otherwise, <c>false</c>.</returns>
	public bool IsPlayModeEnabled()
	{
		if(sensorData != null)
		{
			return sensorData.isPlayModeEnabled;
		}

		return false;
	}


	/// <summary>
	/// Enables or displables the play mode.
	/// </summary>
	/// <param name="bEnabled">If set to <c>true</c> enables the play mode.</param>
	public void EnablePlayMode(bool bEnabled)
	{
		if(sensorData != null)
		{
			sensorData.isPlayModeEnabled = bEnabled;
			sensorData.playModeData = string.Empty;
		}
	}
	
	/// <summary>
	/// Sets the world matrix data from the given csv line.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="sLine">The kinect2world data as csv line.</param>
	public bool SetWorldMatrixData(string sLine)
	{
		if(sensorData != null && sensorData.isPlayModeEnabled)
		{
			return KinectInterop.SetMatrixFromCsv(sLine, ref kinectToWorld);
		}

		return false;
	}

	/// <summary>
	/// Sets the body hand data from the given csv line.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="sLine">The hand data as csv line.</param>
	public bool SetBodyHandData(string sLine)
	{
		if(sensorData != null && sensorData.isPlayModeEnabled)
		{
			sensorData.playModeHandData = sLine;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Sets the body frame from the given csv line.
	/// </summary>
	/// <returns><c>true</c> on success, <c>false</c> otherwise.</returns>
	/// <param name="sLine">The body frame as csv line.</param>
	public bool SetBodyFrameData(string sLine)
	{
		if(sensorData != null && sensorData.isPlayModeEnabled)
		{
			sensorData.playModeData = sLine;
			return true;
		}

		return false;
	}


	// KinectManager's Internal Methods


	void Awake()
	{
		// set the singleton instance
		//instance = this;
		if (instance == null) 
		{
			instance = this;
		}
		else if (instance != this) 
		{
			Destroy(this);
			return;
		}

		try
		{
			bool bOnceRestarted = false;
			if(System.IO.File.Exists("KMrestart.txt"))
			{
				bOnceRestarted = true;

				try 
				{
					System.IO.File.Delete("KMrestart.txt");
				} 
				catch(Exception ex)
				{
					Debug.LogError("Error deleting KMrestart.txt");
					Debug.LogError(ex.ToString());
				}
			}

			// init the available sensor interfaces
			bool bNeedRestart = false;
			sensorInterfaces = KinectInterop.InitSensorInterfaces(bOnceRestarted, ref bNeedRestart);

			if(bNeedRestart)
			{
				System.IO.File.WriteAllText("KMrestart.txt", "Restarting level...");
				KinectInterop.RestartLevel(gameObject, "KM");
				return;
			}
			else
			{
				// set graphics shader level
				KinectInterop.SetGraphicsShaderLevel(SystemInfo.graphicsShaderLevel);

				// start the sensor
				StartKinect();
			}
		} 
		catch (Exception ex) 
		{
			Debug.LogError(ex.ToString());
			
			if(calibrationText != null)
			{
				calibrationText.text = ex.Message;
			}
		}

	}

	private void StartKinect() 
	{
		try
		{
			// try to initialize the default Kinect2 sensor
			KinectInterop.FrameSource dwFlags = KinectInterop.FrameSource.TypeBody;

			if(computeUserMap != UserMapType.None)
				dwFlags |= KinectInterop.FrameSource.TypeDepth | KinectInterop.FrameSource.TypeBodyIndex;
			if(computeColorMap)
				dwFlags |= KinectInterop.FrameSource.TypeColor;
			if(computeInfraredMap)
				dwFlags |= KinectInterop.FrameSource.TypeInfrared;
//			if(useAudioSource)
//				dwFlags |= KinectInterop.FrameSource.TypeAudio;

			// open the default sensor
			BackgroundRemovalManager brManager = gameObject.GetComponentInChildren<BackgroundRemovalManager>();
			sensorData = KinectInterop.OpenDefaultSensor(sensorInterfaces, dwFlags, sensorAngle, useMultiSourceReader, computeUserMap, brManager);

			if (sensorData == null)
			{
				if(sensorInterfaces == null || sensorInterfaces.Count == 0)
					throw new Exception("No sensor found. Make sure you have installed the SDK and the sensor is connected.");
				else
					throw new Exception("OpenDefaultSensor failed.");
			}

			// enable or disable getting height and angle info
			sensorData.hintHeightAngle = (autoHeightAngle != AutoHeightAngle.DontUse);

			//create the transform matrix - kinect to world
//			Quaternion quatTiltAngle = Quaternion.Euler(-sensorAngle, 0.0f, 0.0f);
//			kinectToWorld.SetTRS(new Vector3(0.0f, sensorHeight, 0.0f), quatTiltAngle, Vector3.one);
			UpdateKinectToWorldMatrix();
		}
		catch(DllNotFoundException ex)
		{
			string message = ex.Message + " cannot be loaded. Please check the Kinect SDK installation.";
			
			Debug.LogError(message);
			Debug.LogException(ex);
			
			if(calibrationText != null)
			{
				calibrationText.text = message;
			}
			
			return;
		}
		catch(Exception ex)
		{
			string message = ex.Message;

			Debug.LogError(message);
			Debug.LogException(ex);
			
			if(calibrationText != null)
			{
				calibrationText.text = message;
			}
			
			return;
		}

		// init skeleton structures
		bodyFrame = new KinectInterop.BodyFrameData(sensorData.bodyCount, KinectInterop.Constants.MaxJointCount); // sensorData.jointCount
		bodyFrame.bJointVelocities = estimateJointVelocities /**|| allowTurnArounds*/;       // bJointVelocities is needed to turn on velocities computing ( setup jointData.posDrv in KinectInterop.cs ) -> done this way to not modify other files 

		// init data filters
        KinectInterop.SmoothParameters positionSmoothParams = InitSmoothParameters( smoothing );		
		jointPositionFilter = new JointPositionsFilter();
		jointPositionFilter.Init(positionSmoothParams);

        KinectInterop.SmoothParameters velocitySmoothParams = InitSmoothParameters( velocitySmoothing );
        jointVelocityFilter = new JointVelocitiesFilter();
        jointVelocityFilter.Init(velocitySmoothParams);
		
		// init the bone orientation constraints
		if(useBoneOrientationConstraints)
		{
			boneConstraintsFilter = new BoneOrientationsConstraint();
			boneConstraintsFilter.AddDefaultConstraints();
			boneConstraintsFilter.SetDebugText(calibrationText);
		}

		if(computeUserMap != UserMapType.None && computeUserMap != UserMapType.RawUserDepth)
		{
			// Initialize depth & label map related stuff
			//usersLblTex = new Texture2D(sensorData.depthImageWidth, sensorData.depthImageHeight, TextureFormat.ARGB32, false);

			usersMapSize = sensorData.depthImageWidth * sensorData.depthImageHeight;
			usersHistogramImage = new Color32[usersMapSize];
			usersPrevState = new ushort[usersMapSize];
	        usersHistogramMap = new float[5001];
		}
		
		if(computeColorMap)
		{
			// Initialize color map related stuff
			//usersClrTex = new Texture2D(sensorData.colorImageWidth, sensorData.colorImageHeight, TextureFormat.RGBA32, false);
			usersClrSize = sensorData.colorImageWidth * sensorData.colorImageHeight;
		}

		// try to automatically use the available avatar controllers in the scene
		if(avatarControllers.Count == 0)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

			foreach(MonoBehaviour monoScript in monoScripts)
			{
//				if(typeof(AvatarController).IsAssignableFrom(monoScript.GetType()) && monoScript.enabled)
				if((monoScript is AvatarController) && monoScript.enabled)
				{
					AvatarController avatar = (AvatarController)monoScript;
					avatarControllers.Add(avatar);
				}
			}
		}

		// set up the gesture manager, if not already set
		if(gestureManager == null)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			
			foreach(MonoBehaviour monoScript in monoScripts)
			{
//				if(typeof(KinectGestures).IsAssignableFrom(monoScript.GetType()) && monoScript.enabled)
				if((monoScript is KinectGestures) && monoScript.enabled)
				{
					gestureManager = (KinectGestures)monoScript;
					break;
				}
			}

		}

		// try to automatically use the available gesture listeners in the scene
		if(gestureListeners.Count == 0)
		{
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			
			foreach(MonoBehaviour monoScript in monoScripts)
			{
//				if(typeof(KinectGestures.GestureListenerInterface).IsAssignableFrom(monoScript.GetType()) &&
//				   monoScript.enabled)
				if((monoScript is KinectGestures.GestureListenerInterface) && monoScript.enabled)
				{
					//KinectGestures.GestureListenerInterface gl = (KinectGestures.GestureListenerInterface)monoScript;
					gestureListeners.Add(monoScript);
				}
			}
		}
		
        // Initialize user list to contain all users.
        //alUserIds = new List<Int64>();
        //dictUserIdToIndex = new Dictionary<Int64, int>();

//		// start the background reader
//        if(useOwnThread)
//        { 
//		    kinectReaderThread = new System.Threading.Thread(UpdateKinectStreamsThread);
//		    kinectReaderThread.Name = "KinectReaderThread";
//		    kinectReaderThread.IsBackground = true;
//		    kinectReaderThread.Start();
//		    kinectReaderRunning = true;
//        }

		kinectInitialized = true;

#if USE_SINGLE_KM_IN_MULTIPLE_SCENES
		DontDestroyOnLoad(gameObject);
#endif
		
		// GUI Text.
		if(calibrationText != null)
		{
			calibrationText.text = "WAITING FOR USERS";
		}
		
		Debug.Log("Waiting for users.");
	}

    private KinectInterop.SmoothParameters InitSmoothParameters( Smoothing smoothing )
    {
		KinectInterop.SmoothParameters smoothParameters = new KinectInterop.SmoothParameters();
		
		switch(smoothing)
		{
        case Smoothing.Light:
			smoothParameters.smoothing = 0.3f;
			smoothParameters.correction = 0.35f;
			smoothParameters.prediction = 0.35f;
			smoothParameters.jitterRadius = 0.15f;
			smoothParameters.maxDeviationRadius = 0.15f;
			break;
		case Smoothing.Default:
			smoothParameters.smoothing = 0.5f;
			smoothParameters.correction = 0.5f;
			smoothParameters.prediction = 0.5f;
			smoothParameters.jitterRadius = 0.05f;
			smoothParameters.maxDeviationRadius = 0.04f;
			break;
		case Smoothing.Medium:
			smoothParameters.smoothing = 0.5f;
			smoothParameters.correction = 0.1f;
			smoothParameters.prediction = 0.5f;
			smoothParameters.jitterRadius = 0.1f;
			smoothParameters.maxDeviationRadius = 0.1f;
			break;
		case Smoothing.Aggressive:
			smoothParameters.smoothing = 0.7f;
			smoothParameters.correction = 0.3f;
			smoothParameters.prediction = 1.0f;
			smoothParameters.jitterRadius = 1.0f;
			smoothParameters.maxDeviationRadius = 1.0f;
			break;
		}
		
        return smoothParameters;
    }

#if UNITY_WSA
    //void OnApplicationFocus(bool hasFocus)
    void OnApplicationPause(bool isPaused)
    {
		if (instance != null && instance != this)
			return;

        //bool isPaused = !hasFocus;
        Debug.Log("Application-Pause: " + isPaused);

        if (isPaused)
        {
            // pause
            if (kinectInitialized)
            {
                backgroundRemovalInited = sensorData != null ? sensorData.backgroundRemovalInited : false;
                backgroundRemovalHiRes = sensorData != null ? sensorData.backgroundRemovalHiRes : false;

                OnDestroy();
            }
        }
        else
        {
            // resume
            if (!kinectInitialized)
            {
                instance = this;
                StartKinect();

                if (backgroundRemovalInited)
                {
                    //KinectInterop.InitBackgroundRemoval(sensorData, backgroundRemovalHiRes);
					BackgroundRemovalManager backManager = BackgroundRemovalManager.Instance;
					if (backManager) 
					{
						backManager.Start();
					}
                }
            }
        }

        //Debug.Log("OnApplicationPause() finished. isPaused was " + isPaused);
    }
#endif

    void OnApplicationQuit()
    {
        OnDestroy();
    }

	void OnDestroy() 
	{
		if (instance == null || instance != this)
			return;
		
		//Debug.Log("KM was destroyed");

		// shut down the Kinect on quitting.
		if(kinectInitialized)
		{
			// stop the background thread
			kinectReaderRunning = false;
			
//            if(useOwnThread)
//            { 
//                // set below false to avoid external thread stack in while(x) { Sleep(1); } in many places in KinectInterop.cs 
//                //  ( previously this coused that after one start unity/game crashes/not respondes becouse this thread was still runing and app had to be killed and restarteted almost every time after start and stop KinectManager with external thread )
//                sensorData.bodyFrameReady = false; 
//                sensorData.bodyIndexBufferReady = false; 
//                sensorData.colorImageBufferReady = false; 
//                sensorData.depthCoordsBufferReady = false; 
//                sensorData.depthImageBufferReady = false; 
//                sensorData.spaceCoordsBufferReady = false;
//                
//                // wait for thread stops 
//                kinectReaderThread.Join( 2000 );
//                
//                if(  kinectReaderThread.ThreadState != System.Threading.ThreadState.Stopped )
//                {
//                        Debug.LogError( "KinectThread stop timeout! Aborting...");
//                        kinectReaderThread.Abort();
//                }
//                
//			    kinectReaderThread = null;            
//            }
            
			// close the sensor
			KinectInterop.CloseSensor(sensorData);
//			KinectInterop.ShutdownKinectSensor();

            kinectInitialized = false;
        }

		instance = null;
	}

	void OnGUI()
    {
		if(kinectInitialized)
		{
			if(displayUserMap && !sensorData.color2DepthTexture &&
			   (computeUserMap != UserMapType.None && computeUserMap != UserMapType.RawUserDepth))
	        {
				if(usersMapRect.width == 0 || usersMapRect.height == 0)
				{
					// get the main camera rectangle
					Rect cameraRect = Camera.main != null ? Camera.main.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
					
					// calculate map width and height in percent, if needed
					if(DisplayMapsWidthPercent == 0f)
					{
						DisplayMapsWidthPercent = (sensorData.depthImageWidth / 2) * 100 / cameraRect.width;
					}
					
					float displayMapsWidthPercent = DisplayMapsWidthPercent / 100f;
					float displayMapsHeightPercent = displayMapsWidthPercent * sensorData.depthImageHeight / sensorData.depthImageWidth;
					
					float displayWidth = cameraRect.width * displayMapsWidthPercent;
					float displayHeight = cameraRect.width * displayMapsHeightPercent;
					
					usersMapRect = new Rect(cameraRect.width - displayWidth, cameraRect.height, displayWidth, -displayHeight);
				}

				if (usersLblTex != null) 
				{
					GUI.DrawTexture(usersMapRect, usersLblTex2D != null ? (Texture)usersLblTex2D : usersLblTex);
				}
	        }
			else if(computeColorMap && displayColorMap)
			{
				if(usersClrRect.width == 0 || usersClrRect.height == 0)
				{
					// get the main camera rectangle
					Rect cameraRect = Camera.main != null ? Camera.main.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
					
					// calculate map width and height in percent, if needed
					if(DisplayMapsWidthPercent == 0f)
					{
						DisplayMapsWidthPercent = (sensorData.depthImageWidth / 2) * 100 / cameraRect.width;
					}
					
					float displayMapsWidthPercent = DisplayMapsWidthPercent / 100f;
					float displayMapsHeightPercent = displayMapsWidthPercent * sensorData.colorImageHeight / sensorData.colorImageWidth;

					float displayWidth = cameraRect.width * displayMapsWidthPercent;
					float displayHeight = cameraRect.width * displayMapsHeightPercent;

					float displayX = cameraRect.width - displayWidth;
					float displayY = cameraRect.height - displayHeight;

					Vector3 clrImageScale = sensorData.colorImageScale;
					if(clrImageScale.x < 0)
					{
						displayX = cameraRect.width;
						displayWidth = -displayWidth;
					}
					if(clrImageScale.y < 0)
					{
						displayY = cameraRect.height;
						displayHeight = -displayHeight;
					}

					//usersClrRect = new Rect(cameraRect.width - displayWidth, cameraRect.height, displayWidth, -displayHeight);
					usersClrRect = new Rect(displayX, displayY, displayWidth, displayHeight);
						
//					if(computeUserMap && displayColorMap)
//					{
//						usersMapRect.x -= cameraRect.width * displayMapsWidthPercent;
//					}
				}

				if (sensorData.colorImageTexture2D != null || sensorData.colorImageTexture != null) 
				{
					//GUI.DrawTexture(usersClrRect, usersClrTex);
					GUI.DrawTexture(usersClrRect, sensorData.colorImageTexture2D ? sensorData.colorImageTexture2D : sensorData.colorImageTexture);
				}
			}
		}
    }

	// updates Kinect streams and structures
	private void UpdateKinectStreams()
	{
		if(kinectInitialized)
		{
			KinectInterop.UpdateSensorData(sensorData);

			// check user limits and update sensor data
			bLimitedUsers = showTrackedUsersOnly && ((maxTrackedUsers < 6 /**&& (alUserIds.Count > maxTrackedUsers)*/) || 
				minUserDistance >= 0.51f || maxUserDistance >= 0.51f || maxLeftRightDistance >= 0.01f);
			//Debug.Log ("Limited-users: " + bLimitedUsers);

			if(useMultiSourceReader)
			{
				KinectInterop.GetMultiSourceFrame(sensorData);
			}
			
			// poll color map
			if(computeColorMap)
			{
				if((sensorData.newColorImage = KinectInterop.PollColorFrame(sensorData)))
				{
					//UpdateColorMap();
				}
			}
			
			// poll user map
			if(computeUserMap != UserMapType.None)
			{
				sensorData.firstUserIndex = liPrimaryUserId != 0 && dictUserIdToIndex.ContainsKey(liPrimaryUserId) ? 
					dictUserIdToIndex[liPrimaryUserId] : -1;
				
				if((sensorData.newDepthImage = KinectInterop.PollDepthFrame(sensorData, computeUserMap, bLimitedUsers, dictUserIdToIndex.Values)))
				{
					//UpdateUserMap(computeUserMap);
				}
			}
			
			// poll infrared map
			if(computeInfraredMap)
			{
				if((sensorData.newInfraredImage = KinectInterop.PollInfraredFrame(sensorData)))
				{
					//UpdateInfraredMap();
				}
			}
			
			// poll or play body frame
			sensorData.newBodyFrame = false;
			if(sensorData == null || !sensorData.isPlayModeEnabled)
			{
				sensorData.newBodyFrame = KinectInterop.PollBodyFrame(sensorData, ref bodyFrame, ref kinectToWorld, ignoreZCoordinates);
			}
			else
			{
				if(!string.IsNullOrEmpty(sensorData.playModeData))
				{
					sensorData.newBodyFrame = KinectInterop.SetBodyFrameFromCsv(sensorData.playModeData, sensorData, ref bodyFrame, ref kinectToWorld);
					sensorData.playModeData = string.Empty;
				}

				if(!string.IsNullOrEmpty(sensorData.playModeHandData))
				{
					KinectInterop.SetHandsDataFromCsv(sensorData.playModeHandData, sensorData, ref bodyFrame);
					sensorData.playModeHandData = string.Empty;
				}
			}
			
			// process the body frame
			if(sensorData.newBodyFrame)
			{
				// filter the tracked joint positions
				if(smoothing != Smoothing.None)
				{
					jointPositionFilter.UpdateFilter(ref bodyFrame);
				}
				
				//ProcessBodyFrameData();
				
                if(estimateJointVelocities && velocitySmoothing != Smoothing.None)
                {
                    jointVelocityFilter.UpdateFilter(ref bodyFrame);
                }
			}
			
			if(useMultiSourceReader)
			{
				KinectInterop.FreeMultiSourceFrame(sensorData);
			}
		}
	}

	// background kinect thread procedure
	private void UpdateKinectStreamsThread()
	{
		while(kinectReaderRunning)
		{
			UpdateKinectStreams();
			KinectInterop.Sleep(5);
		}
	}
	
	// process data from Kinect streams
	private void ProcessKinectStreams()
	{
		// render color texture
		if(sensorData.colorImageBufferReady)
		{
			KinectInterop.RenderColorTexture(sensorData);
			UpdateColorMap();
		}
		
		// render body-index texture
		bool newDepthImage = sensorData.bodyIndexBufferReady || sensorData.depthImageBufferReady;

		if(sensorData.bodyIndexBufferReady)
		{
			KinectInterop.RenderBodyIndexTexture(sensorData, computeUserMap);
		}

		// render depth-image texture
		if(sensorData.depthImageBufferReady)
		{
			KinectInterop.RenderDepthImageTexture(sensorData);
		}

		// update user map
		if(newDepthImage)
		{
			UpdateUserMap(computeUserMap);
		}

		// update infrared map
		UpdateInfraredMap();

		if(sensorData.bodyFrameReady)
		{
			ProcessBodyFrameData();
			
			// frame is released
			lock(sensorData.bodyFrameLock)
			{
				sensorData.bodyFrameReady = false;
			}
		}
	}
	
	void Update() 
	{
		if(kinectInitialized)
		{
			if(!kinectReaderRunning)
			{
				// update Kinect streams and structures
				UpdateKinectStreams();
			}

			// process the data from Kinect streams
			ProcessKinectStreams();

			// update the avatars
			if(!lateUpdateAvatars)
			{
				foreach (AvatarController controller in avatarControllers)
				{
					//int userIndex = controller ? controller.playerIndex : -1;
					Int64 userId = controller ? controller.playerId : 0;
					
					//if((userIndex >= 0) && (userIndex < alUserIds.Count))
					if(userId != 0 && dictUserIdToIndex.ContainsKey(userId))
					{
						//Int64 userId = alUserIds[userIndex];
						controller.UpdateAvatar(userId);
					}
				}
			}

			// check for gestures
			foreach(Int64 userId in alUserIds)
			{
				if(!playerGesturesData.ContainsKey(userId))
					continue;

				// Check for player's gestures
				CheckForGestures(userId);
				
				// Check for complete gestures
				List<KinectGestures.GestureData> gesturesData = playerGesturesData[userId];
				int userIndex = GetUserIndexById(userId);
				
				//foreach(KinectGestures.GestureData gestureData in gesturesData)
				for(int g = 0; g < gesturesData.Count; g++)
				{
					KinectGestures.GestureData gestureData = gesturesData[g];

					if(gestureData.complete)
					{
//						if(gestureData.gesture == KinectGestures.Gestures.Click)
//						{
//							if(controlMouseCursor)
//							{
//								MouseControl.MouseClick();
//							}
//						}
				
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null && listener.GestureCompleted(userId, userIndex, gestureData.gesture, (KinectInterop.JointType)gestureData.joint, gestureData.screenPos))
							{
								ResetPlayerGestures(userId);
							}
						}
					}
					else if(gestureData.cancelled)
					{
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null && listener.GestureCancelled(userId, userIndex, gestureData.gesture, (KinectInterop.JointType)gestureData.joint))
							{
								ResetGesture(userId, gestureData.gesture);
							}
						}
					}
					else if(gestureData.progress >= 0.1f)
					{
//						if((gestureData.gesture == KinectGestures.Gestures.RightHandCursor || 
//						    gestureData.gesture == KinectGestures.Gestures.LeftHandCursor) && 
//						   gestureData.progress >= 0.5f)
//						{
//							if(handCursor != null)
//							{
//								handCursor.transform.position = Vector3.Lerp(handCursor.transform.position, gestureData.screenPos, 3 * Time.deltaTime);
//							}
//							
//							if(controlMouseCursor)
//							{
//								MouseControl.MouseMove(gestureData.screenPos);
//							}
//						}
						
						foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
						{
							if(listener != null)
							{
								listener.GestureInProgress(userId, userIndex, gestureData.gesture, gestureData.progress, 
								                           (KinectInterop.JointType)gestureData.joint, gestureData.screenPos);
							}
						}
					}

					//gesturesData[g] = gestureData;
				}
			}
			
		}
	}

	void LateUpdate()
	{
		// late update the avatars
		if(lateUpdateAvatars)
		{
			foreach (AvatarController controller in avatarControllers)
			{
				//int userIndex = controller ? controller.playerIndex : -1;
				Int64 userId = controller ? controller.playerId : 0;
				
				//if((userIndex >= 0) && (userIndex < alUserIds.Count))
				if(userId != 0 && dictUserIdToIndex.ContainsKey(userId))
				{
					//Int64 userId = alUserIds[userIndex];
					controller.UpdateAvatar(userId);
				}
			}
		}
	}
	
	// Update the color image
	void UpdateColorMap()
	{
		//usersClrTex.LoadRawTextureData(sensorData.colorImage);

		if(sensorData != null && sensorData.sensorInterface != null && sensorData.colorImageTexture2D != null)
		{
			if(sensorData.sensorInterface.IsFaceTrackingActive() &&
			   sensorData.sensorInterface.IsDrawFaceRect())
			{
				// visualize face tracker (face rectangles)
				//sensorData.sensorInterface.VisualizeFaceTrackerOnColorTex(usersClrTex);
				sensorData.sensorInterface.VisualizeFaceTrackerOnColorTex(sensorData.colorImageTexture2D);
				sensorData.colorImageTexture2D.Apply();
			}
		}

		//usersClrTex.Apply();
	}
	
	// Update the user histogram
	void UpdateUserMap(UserMapType userMapType)
    {
		if(sensorData != null && sensorData.sensorInterface != null && 
		   !sensorData.sensorInterface.IsBackgroundRemovalActive())
		{
			if(!KinectInterop.IsDirectX11Available())
			{
				if(userMapType != UserMapType.RawUserDepth)
				{
					UpdateUserHistogramImage(userMapType);

					// apply it on the 2d-texture
					GetUsersLblTex2D();
					if (usersLblTex2D) 
					{
						usersLblTex2D.SetPixels32(usersHistogramImage);
					}
				}
			}
			else
			{
				if(userMapType == UserMapType.CutOutTexture)
				{
					if(!sensorData.color2DepthTexture && sensorData.depth2ColorTexture && 
					   KinectInterop.RenderDepth2ColorTex(sensorData))
					{
						usersLblTex = sensorData.depth2ColorTexture;
						if(usersLblTex2D != null)
							KinectInterop.RenderTex2Tex2D(sensorData.depth2ColorTexture, ref usersLblTex2D);
					}
					else if(!sensorData.color2DepthTexture)
					{
						usersLblTex = sensorData.bodyIndexTexture;
						if(usersLblTex2D != null)
							KinectInterop.RenderTex2Tex2D(sensorData.bodyIndexTexture, ref usersLblTex2D);
					}
				}
				else if(userMapType == UserMapType.BodyTexture && sensorData.bodyIndexTexture)
				{
					usersLblTex = sensorData.bodyIndexTexture;
					if(usersLblTex2D != null)
						KinectInterop.RenderTex2Tex2D(sensorData.bodyIndexTexture, ref usersLblTex2D);
				}
				else if(userMapType == UserMapType.UserTexture && sensorData.depthImageTexture)
				{
					usersLblTex = sensorData.depthImageTexture;
					if(usersLblTex2D != null)
						KinectInterop.RenderTex2Tex2D(sensorData.depthImageTexture, ref usersLblTex2D);
				}
			}
			
			if(userMapType != UserMapType.RawUserDepth)
			{
				// draw skeleton lines
				if(displaySkeletonLines)
				{
					GetUsersLblTex2D();

					for(int i = 0; i < alUserIds.Count; i++)
					{
						Int64 liUserId = alUserIds[i];
						int index = dictUserIdToIndex[liUserId];
						
						if(index >= 0 && index < sensorData.bodyCount)
						{
							DrawSkeleton(usersLblTex2D, ref bodyFrame.bodyData[index]);
						}
					}
				}

				if (usersLblTex2D) 
				{
					usersLblTex2D.Apply();
				}
			}
		}
    }

	// Update the user infrared map
	void UpdateInfraredMap()
	{
		// does nothing at the moment
	}
	
	// Update the user histogram map
	void UpdateUserHistogramImage(UserMapType userMapType)
	{
		int numOfPoints = 0;
		Array.Clear(usersHistogramMap, 0, usersHistogramMap.Length);
		
		// Calculate cumulative histogram for depth
		for (int i = 0; i < usersMapSize; i++)
		{
			// Only calculate for depth that contains users
			if (sensorData.bodyIndexImage[i] != 255)
			{
				ushort depth = sensorData.depthImage[i];
				if(depth > 5000)
					depth = 5000;

				usersHistogramMap[depth]++;
				numOfPoints++;
			}
		}
		
		if (numOfPoints > 0)
		{
			for (int i = 1; i < usersHistogramMap.Length; i++)
			{   
				usersHistogramMap[i] += usersHistogramMap[i - 1];
			}
			
			for (int i = 0; i < usersHistogramMap.Length; i++)
			{
				usersHistogramMap[i] = 1.0f - (usersHistogramMap[i] / numOfPoints);
			}
		}

		//List<int> alTrackedIndexes = new List<int>(dictUserIdToIndex.Values);
		byte btSelBI = sensorData.selectedBodyIndex;
		Color32 clrClear = Color.clear;

		// convert the body indices to string
		string sTrackedIndices = string.Empty;

		if (bLimitedUsers) 
		{
			foreach(int bodyIndex in dictUserIdToIndex.Values)
			{
				sTrackedIndices += (char)(0x30 + bodyIndex);
			}
		}
		
		// Create the actual users texture based on label map and depth histogram
		for (int i = 0; i < usersMapSize; i++)
		{
			ushort userMap = sensorData.bodyIndexImage[i];
			ushort userDepth = sensorData.depthImage[i];

			if(userDepth > 5000)
				userDepth = 5000;
			
			ushort nowUserPixel = userMap != 255 ? (ushort)((userMap << 13) | userDepth) : userDepth;
			ushort wasUserPixel = usersPrevState[i];
			
			// draw only the changed pixels
			if(nowUserPixel != wasUserPixel)
			{
				usersPrevState[i] = nowUserPixel;

				bool bUserTracked = btSelBI != 255 ? btSelBI == (byte)userMap : 
					//(bLimitedUsers ? alTrackedIndexes.Contains(userMap): userMap != 255);
					(bLimitedUsers ? sTrackedIndices.IndexOf((char)(0x30 + userMap)) >= 0 : userMap != 255);

				if(!bUserTracked)
				{
					usersHistogramImage[i] = clrClear;
				}
				else
				{
					if(userMapType == UserMapType.CutOutTexture && sensorData.colorImage != null)
					{
						Vector2 vColorPos = Vector2.zero;

						if(sensorData.depth2ColorCoords != null)
						{
							vColorPos = sensorData.depth2ColorCoords[i];
						}
						else
						{
							Vector2 vDepthPos = Vector2.zero;
							vDepthPos.x = i % sensorData.depthImageWidth;
							vDepthPos.y = i / sensorData.depthImageWidth;

							vColorPos = KinectInterop.MapDepthPointToColorCoords(sensorData, vDepthPos, userDepth);
						}

						if(!float.IsInfinity(vColorPos.x) && !float.IsInfinity(vColorPos.y))
						{
							int cx = (int)vColorPos.x;
							int cy = (int)vColorPos.y;
							int colorIndex = cx + cy * sensorData.colorImageWidth;

							if(colorIndex >= 0 && colorIndex < usersClrSize)
							{
								int ci = colorIndex << 2;
								Color32 colorPixel = new Color32(sensorData.colorImage[ci], sensorData.colorImage[ci + 1], sensorData.colorImage[ci + 2], 255);
								
								usersHistogramImage[i] = colorPixel;
							}
						}
					}
					else
					{
						// Create a blending color based on the depth histogram
						float histDepth = usersHistogramMap[userDepth];
						Color c = new Color(histDepth, histDepth, histDepth, 0.9f);
						
						switch(userMap % 4)
						{
						case 0:
							usersHistogramImage[i] = Color.red * c;
							break;
						case 1:
							usersHistogramImage[i] = Color.green * c;
							break;
						case 2:
							usersHistogramImage[i] = Color.blue * c;
							break;
						case 3:
							usersHistogramImage[i] = Color.magenta * c;
							break;
						}
					}
				}
				
			}
		}
		
	}
	
	// Processes body frame data
	private void ProcessBodyFrameData()
	{
		List<Int64> addedUsers = new List<Int64>();
		List<int> addedIndexes = new List<int>();

		List<Int64> lostUsers = new List<Int64>();
		lostUsers.AddRange(alUserIds);

		if((autoHeightAngle == AutoHeightAngle.ShowInfoOnly || autoHeightAngle == AutoHeightAngle.AutoUpdateAndShowInfo) && 
		   (sensorData.sensorHgtDetected != 0f || sensorData.sensorRotDetected.eulerAngles.x != 0f) &&
		   calibrationText != null)
		{
			float angle = sensorData.sensorRotDetected.eulerAngles.x;
			angle = angle > 180f ? (angle - 360f) : angle;

			calibrationText.text = string.Format("Sensor Height: {0:F2} m, Angle: {1:F0} deg", sensorData.sensorHgtDetected, -angle);
		}

		if((autoHeightAngle == AutoHeightAngle.AutoUpdate || autoHeightAngle == AutoHeightAngle.AutoUpdateAndShowInfo) && 
		   (sensorData.sensorHgtDetected != 0f || sensorData.sensorRotDetected.eulerAngles.x != 0f))
		{
			float angle = sensorData.sensorRotDetected.eulerAngles.x;
			angle = angle > 180f ? (angle - 360f) : angle;
			sensorAngle = -angle;

			float height = sensorData.sensorHgtDetected > 0f ? sensorData.sensorHgtDetected : sensorHeight;
			sensorHeight = height;

			// update the kinect to world matrix
//			Quaternion quatTiltAngle = Quaternion.Euler(-sensorAngle, 0.0f, 0.0f);
//			kinectToWorld.SetTRS(new Vector3(0.0f, sensorHeight, 0.0f), quatTiltAngle, Vector3.one);
			UpdateKinectToWorldMatrix();
		}
		
		//int trackedUsers = 0;
		
		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			KinectInterop.BodyData bodyData = bodyFrame.bodyData[i];
			Int64 userId = bodyData.liTrackingID;
			
			if(bodyData.bIsTracked != 0 && Mathf.Abs(bodyData.position.z) >= minUserDistance &&
			   (maxUserDistance < 0.51f || Mathf.Abs(bodyData.position.z) <= maxUserDistance) &&
			   (maxLeftRightDistance < 0.01f || Mathf.Abs(bodyData.position.x) <= maxLeftRightDistance))
			   // && (maxTrackedUsers < 0 || trackedUsers < maxTrackedUsers))
			{
				// get the body position
				Vector3 bodyPos = bodyData.position;

//				if(liPrimaryUserId == 0)
//				{
//					// check if this is the closest user
//					bool bClosestUser = true;
//					int iClosestUserIndex = i;
//					
//					if(detectClosestUser)
//					{
//						for(int j = 0; j < sensorData.bodyCount; j++)
//						{
//							if(j != i)
//							{
//								KinectInterop.BodyData bodyDataOther = bodyFrame.bodyData[j];
//								
//								if((bodyDataOther.bIsTracked != 0) && 
//									(Mathf.Abs(bodyDataOther.position.z) < Mathf.Abs(bodyPos.z)))
//								{
//									bClosestUser = false;
//									iClosestUserIndex = j;
//									break;
//								}
//							}
//						}
//					}
//					
//					if(bClosestUser)
//					{
//						// add the first or closest userId to the list of new users
//						if(!addedUsers.Contains(userId))
//						{
//							addedUsers.Insert(0, userId);
//							addedIndexes.Insert(0, iClosestUserIndex);
//							trackedUsers++;
//						}
//					}
//				}
				
				// add userId to the list of new users
				if(!addedUsers.Contains(userId))
				{
					addedUsers.Add(userId);
					addedIndexes.Add(i);
					//trackedUsers++;
				}

				// convert Kinect positions to world positions
				bodyFrame.bodyData[i].position = bodyPos;
				//string debugText = String.Empty;

				// process special cases
				ProcessBodySpecialData(ref bodyData);
				ProcessBodySpecialDirs(ref bodyData);

////// 		turnaround mode start
				// determine if the user is turned around
				//float bodyTurnAngle = 0f;
				//float neckTiltAngle = 0f;

				if(allowTurnArounds)
				{
					if (!alUserIds.Contains(userId)) 
					{
						// init time stamps
						bodyData.turnFaceLastTrackedTime = Time.realtimeSinceStartup;
						bodyData.turnLeftShoulderTrackedTime = Time.realtimeSinceStartup;
						bodyData.turnRightShoulderTrackedTime = Time.realtimeSinceStartup;
						bodyData.turnShoulderDistTrackedTime = Time.realtimeSinceStartup;
					}

					bool bFaceTrackingActive = sensorData.sensorInterface.IsFaceTrackingActive();
					bool bFaceTracked = sensorData.sensorInterface.IsFaceTracked(bodyData.liTrackingID);
					if (bFaceTrackingActive && bFaceTracked) 
					{
						bodyData.turnFaceLastTrackedTime = Time.realtimeSinceStartup;
					}

					bool bLeftShoulderTracked = (bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState == KinectInterop.TrackingState.Tracked);
					if (bLeftShoulderTracked) 
					{
						bodyData.turnLeftShoulderTrackedTime = Time.realtimeSinceStartup;
					}

					bool bRightShoulderTracked = (bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState == KinectInterop.TrackingState.Tracked);
					if (bRightShoulderTracked) 
					{
						bodyData.turnRightShoulderTrackedTime = Time.realtimeSinceStartup;
					}

					Vector3 posShoulderL = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position;
					Vector3 posShoulderR = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position;
					float distLR = Mathf.Abs(posShoulderR.x - posShoulderL.x);

					if (distLR >= 0.2f) // determined experimentally
					{
						bodyData.turnShoulderDistTrackedTime = Time.realtimeSinceStartup;
					}

					float timeSinceTrackedFace = Time.realtimeSinceStartup - bodyData.turnFaceLastTrackedTime;
					float timeSinceTrackedLShoulder = Time.realtimeSinceStartup - bodyData.turnLeftShoulderTrackedTime;
					float timeSinceTrackedRShoulder = Time.realtimeSinceStartup - bodyData.turnRightShoulderTrackedTime;
					float timeSinceTrackedShoulderDist = Time.realtimeSinceStartup - bodyData.turnShoulderDistTrackedTime;

					// allow up to 0.5 second deviation
					bodyData.isTurnedAround = bFaceTrackingActive && (timeSinceTrackedFace > 0.5f) && (timeSinceTrackedLShoulder < 0.5f) && (timeSinceTrackedRShoulder < 0.5f);
					if (!bodyData.isTurnedAround && bFaceTrackingActive && (timeSinceTrackedFace > 0.5f)) 
					{
						//if(distLC > 0.07f && distRC > 0.07f)
						if(timeSinceTrackedShoulderDist < 0.5)
						{
							bodyData.isTurnedAround = true;
						}

						//Debug.Log(string.Format("d-LR: {0:F3}, turned: {1}", distLR, bodyData.isTurnedAround));
					}

					//bodyData.turnAroundFactor = bodyData.isTurnedAround ? 1f : 0f;
				
					if(bodyData.isTurnedAround)
					{
						// switch left and right joints
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ShoulderLeft, (int)KinectInterop.JointType.ShoulderRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ElbowLeft, (int)KinectInterop.JointType.ElbowRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.WristLeft, (int)KinectInterop.JointType.WristRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HandLeft, (int)KinectInterop.JointType.HandRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.ThumbLeft, (int)KinectInterop.JointType.ThumbRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HandTipLeft, (int)KinectInterop.JointType.HandTipRight);
						
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.HipLeft, (int)KinectInterop.JointType.HipRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.KneeLeft, (int)KinectInterop.JointType.KneeRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.AnkleLeft, (int)KinectInterop.JointType.AnkleRight);
						SwitchJointsData(ref bodyData, (int)KinectInterop.JointType.FootLeft, (int)KinectInterop.JointType.FootRight);

						// recalculate the bone dirs and special data
						KinectInterop.RecalcBoneDirs(sensorData, ref bodyData);
						ProcessBodySpecialDirs(ref bodyData);
					}
				}
				
//				if(allowTurnArounds && calibrationText)
//				{
//					calibrationText.text = string.Format("{0} - BodyAngle: {1:000}", 
//					    (!bodyData.isTurnedAround ? "FACE" : "BACK"), bodyData.bodyTurnAngle);
//				}

////// 		turnaround mode end

				// calculate world orientations of the body joints
				CalculateJointOrients(ref bodyData);

				if(sensorData != null && sensorData.sensorInterface != null)
				{
					// do sensor-specific fixes of joint positions and orientations
					sensorData.sensorInterface.FixJointOrientations(sensorData, ref bodyData);
				}

				// filter orientation constraints
				if(useBoneOrientationConstraints && boneConstraintsFilter != null)
				{
					boneConstraintsFilter.Constrain(ref bodyData);
				}
				
				lostUsers.Remove(userId);
				bodyFrame.bodyData[i] = bodyData;
				dictUserIdToTime[userId] = Time.time;
			}
			else
			{
				// consider body as not tracked
				bodyFrame.bodyData[i].bIsTracked = 0;
			}
		}
		
		// remove the lost users if any
		if(lostUsers.Count > 0)
		{
			foreach(Int64 userId in lostUsers)
			{
				// prevent user removal upon sporadical tracking failures
				if((Time.time - dictUserIdToTime[userId]) > waitTimeBeforeRemove)
				{
					RemoveUser(userId);
				}
			}
			
			lostUsers.Clear();
		}

		// calibrate newly detected users
		if(addedUsers.Count > 0)
		{
			for(int i = 0; i < addedUsers.Count; i++)
			{
				if (alUserIds.Count < maxTrackedUsers) 
				{
					Int64 userId = addedUsers[i];
					int userIndex = addedIndexes[i];

					CalibrateUser(userId, userIndex);
				}
			}
			
			addedUsers.Clear();
			addedIndexes.Clear();
		}
	}

	// estimates some joint positions from the tracked body data, as needed
	private void ProcessBodySpecialData(ref KinectInterop.BodyData bodyData)
	{
		// infer shoulder and hips positions, if needed
		if((bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.Tracked &&
		    bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState == KinectInterop.TrackingState.Tracked &&
		    bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineMid].trackingState == KinectInterop.TrackingState.Tracked))
		{
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState = KinectInterop.TrackingState.Inferred;
			
			Vector3 posShoulderR = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].kinectPos;
			Vector3 posShoulderC = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos;
			Vector3 posHipC = bodyData.joint[(int)KinectInterop.JointType.SpineMid].kinectPos;

			Vector3 dirSpine = posShoulderC - posHipC;
			Vector3 dirShoulder = posShoulderR - posShoulderC;
			dirShoulder -= Vector3.Project(dirShoulder, dirSpine);
			Vector3 posShoulderL = posShoulderR - (2f * dirShoulder);

			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].kinectPos = posShoulderL;
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position = kinectToWorld.MultiplyPoint3x4(posShoulderL);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].direction = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position;
		}
		
		if((bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.Tracked &&
		    bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState == KinectInterop.TrackingState.Tracked &&
		    bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineMid].trackingState == KinectInterop.TrackingState.Tracked))
		{
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState = KinectInterop.TrackingState.Inferred;

			Vector3 posShoulderL = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].kinectPos;
			Vector3 posShoulderC = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].kinectPos;
			Vector3 posHipC = bodyData.joint[(int)KinectInterop.JointType.SpineMid].kinectPos;

			Vector3 dirSpine = posShoulderC - posHipC;
			Vector3 dirShoulder = posShoulderL - posShoulderC;
			dirShoulder -= Vector3.Project(dirShoulder, dirSpine);
			Vector3 posShoulderR = posShoulderL - (2f * dirShoulder);

			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].kinectPos = posShoulderR;
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position = kinectToWorld.MultiplyPoint3x4(posShoulderR);
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].direction = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].position;
		}
		
		if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineMid].trackingState == KinectInterop.TrackingState.Tracked)
		{
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState = KinectInterop.TrackingState.Inferred;

			Vector3 posHipR = bodyData.joint[(int)KinectInterop.JointType.HipRight].kinectPos;
			Vector3 posShoulderC = bodyData.joint[(int)KinectInterop.JointType.SpineMid].kinectPos;
			Vector3 posHipC = bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos;

			Vector3 dirSpine = posShoulderC - posHipC;
			Vector3 dirHips = posHipR - posHipC;
			dirHips -= Vector3.Project(dirHips, dirSpine);
			Vector3 posHipL = posHipR - (2f * dirHips);

			bodyData.joint[(int)KinectInterop.JointType.HipLeft].kinectPos = posHipL;
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].position = kinectToWorld.MultiplyPoint3x4(posHipL);
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].direction = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineBase].position;
		}

		if(bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineBase].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyData.joint[(int)KinectInterop.JointType.SpineMid].trackingState == KinectInterop.TrackingState.Tracked)
		{
			bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState = KinectInterop.TrackingState.Inferred;

			Vector3 posHipL = bodyData.joint[(int)KinectInterop.JointType.HipLeft].kinectPos;
			Vector3 posShoulderC = bodyData.joint[(int)KinectInterop.JointType.SpineMid].kinectPos;
			Vector3 posHipC = bodyData.joint[(int)KinectInterop.JointType.SpineBase].kinectPos;

			Vector3 dirSpine = posShoulderC - posHipC;
			Vector3 dirHips = posHipL - posHipC;
			dirHips -= Vector3.Project(dirHips, dirSpine);
			Vector3 posHipR = posHipL - (2f * dirHips);

			bodyData.joint[(int)KinectInterop.JointType.HipRight].kinectPos = posHipR;
			bodyData.joint[(int)KinectInterop.JointType.HipRight].position = kinectToWorld.MultiplyPoint3x4(posHipR);
			bodyData.joint[(int)KinectInterop.JointType.HipRight].direction = bodyData.joint[(int)KinectInterop.JointType.HipRight].position -
				bodyData.joint[(int)KinectInterop.JointType.SpineBase].position;
		}

//				if(bodyData.joint[(int)KinectInterop.JointType.ElbowLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristLeft].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.ElbowLeft].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.WristLeft].position;
//					
//					bodyData.leftArmDirection = pos2 - pos1;
//				}

//				if(allowHandRotations && bodyData.leftArmDirection != Vector3.zero &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.ThumbLeft].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.WristLeft].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.ThumbLeft].position;
//
//					Vector3 armDir = bodyData.leftArmDirection;
//					armDir.z = -armDir.z;
//					
//					bodyData.leftThumbDirection = pos2 - pos1;
//					bodyData.leftThumbDirection.z = -bodyData.leftThumbDirection.z;
//					bodyData.leftThumbDirection -= Vector3.Project(bodyData.leftThumbDirection, armDir);
//					
//					bodyData.leftThumbForward = Quaternion.AngleAxis(bodyData.bodyTurnAngle, Vector3.up) * Vector3.forward;
//					bodyData.leftThumbForward -= Vector3.Project(bodyData.leftThumbForward, armDir);
//
//					if(bodyData.leftThumbForward.sqrMagnitude < 0.01f)
//					{
//						bodyData.leftThumbForward = Vector3.zero;
//					}
//				}
//				else
//				{
//					if(bodyData.leftThumbDirection != Vector3.zero)
//					{
//						bodyData.leftThumbDirection = Vector3.zero;
//						bodyData.leftThumbForward = Vector3.zero;
//					}
//				}

//				if(bodyData.joint[(int)KinectInterop.JointType.ElbowRight].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristRight].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.ElbowRight].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.WristRight].position;
//					
//					bodyData.rightArmDirection = pos2 - pos1;
//				}

//				if(allowHandRotations && bodyData.rightArmDirection != Vector3.zero &&
//				   bodyData.joint[(int)KinectInterop.JointType.WristRight].trackingState != KinectInterop.TrackingState.NotTracked &&
//				   bodyData.joint[(int)KinectInterop.JointType.ThumbRight].trackingState != KinectInterop.TrackingState.NotTracked)
//				{
//					Vector3 pos1 = bodyData.joint[(int)KinectInterop.JointType.WristRight].position;
//					Vector3 pos2 = bodyData.joint[(int)KinectInterop.JointType.ThumbRight].position;
//
//					Vector3 armDir = bodyData.rightArmDirection;
//					armDir.z = -armDir.z;
//					
//					bodyData.rightThumbDirection = pos2 - pos1;
//					bodyData.rightThumbDirection.z = -bodyData.rightThumbDirection.z;
//					bodyData.rightThumbDirection -= Vector3.Project(bodyData.rightThumbDirection, armDir);
//
//					bodyData.rightThumbForward = Quaternion.AngleAxis(bodyData.bodyTurnAngle, Vector3.up) * Vector3.forward;
//					bodyData.rightThumbForward -= Vector3.Project(bodyData.rightThumbForward, armDir);
//
//					if(bodyData.rightThumbForward.sqrMagnitude < 0.01f)
//					{
//						bodyData.rightThumbForward = Vector3.zero;
//					}
//				}
//				else
//				{
//					if(bodyData.rightThumbDirection != Vector3.zero)
//					{
//						bodyData.rightThumbDirection = Vector3.zero;
//						bodyData.rightThumbForward = Vector3.zero;
//					}
//				}
		
		if(bodyData.joint[(int)KinectInterop.JointType.KneeLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.FootLeft].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 vFootProjected = Vector3.Project(bodyData.joint[(int)KinectInterop.JointType.FootLeft].direction, bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].direction);
			
			bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].kinectPos += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.AnkleLeft].position += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.FootLeft].direction -= vFootProjected;
		}
		
		if(bodyData.joint[(int)KinectInterop.JointType.KneeRight].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.AnkleRight].trackingState != KinectInterop.TrackingState.NotTracked &&
		   bodyData.joint[(int)KinectInterop.JointType.FootRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 vFootProjected = Vector3.Project(bodyData.joint[(int)KinectInterop.JointType.FootRight].direction, bodyData.joint[(int)KinectInterop.JointType.AnkleRight].direction);
			
			bodyData.joint[(int)KinectInterop.JointType.AnkleRight].kinectPos += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.AnkleRight].position += vFootProjected;
			bodyData.joint[(int)KinectInterop.JointType.FootRight].direction -= vFootProjected;
		}
	}
	
	// estimates some joint directions from the tracked body data, as needed
	private void ProcessBodySpecialDirs(ref KinectInterop.BodyData bodyData)
	{
		if(bodyData.joint[(int)KinectInterop.JointType.HipLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
			bodyData.joint[(int)KinectInterop.JointType.HipRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 posRHip = bodyData.joint[(int)KinectInterop.JointType.HipRight].position;
			Vector3 posLHip = bodyData.joint[(int)KinectInterop.JointType.HipLeft].position;

			bodyData.hipsDirection = posRHip - posLHip;
			bodyData.hipsDirection -= Vector3.Project(bodyData.hipsDirection, Vector3.up);
		}

		if(bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].trackingState != KinectInterop.TrackingState.NotTracked &&
			bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].trackingState != KinectInterop.TrackingState.NotTracked)
		{
			Vector3 posRShoulder = bodyData.joint[(int)KinectInterop.JointType.ShoulderRight].position;
			Vector3 posLShoulder = bodyData.joint[(int)KinectInterop.JointType.ShoulderLeft].position;

			bodyData.shouldersDirection = posRShoulder - posLShoulder;
			bodyData.shouldersDirection -= Vector3.Project(bodyData.shouldersDirection, Vector3.up);

			Vector3 shouldersDir = bodyData.shouldersDirection;
			shouldersDir.z = -shouldersDir.z;

			Quaternion turnRot = Quaternion.FromToRotation(Vector3.right, shouldersDir);
			bodyData.bodyTurnAngle = turnRot.eulerAngles.y;
		}
	}

	// switches the positional data of two joints
	private void SwitchJointsData(ref KinectInterop.BodyData bodyData, int jointL, int jointR)
	{
		KinectInterop.TrackingState trackingStateL = bodyData.joint[jointL].trackingState;
		Vector3 kinectPosL = bodyData.joint[jointL].kinectPos;
		Vector3 positionL = bodyData.joint[jointL].position;

		KinectInterop.TrackingState trackingStateR = bodyData.joint[jointR].trackingState;
		Vector3 kinectPosR = bodyData.joint[jointR].kinectPos;
		Vector3 positionR = bodyData.joint[jointR].position;

		bodyData.joint[jointL].trackingState = trackingStateR;
		bodyData.joint[jointL].kinectPos = kinectPosR; // new Vector3(kinectPosR.x, kinectPosL.y, kinectPosL.z);
		bodyData.joint[jointL].position = positionR; // new Vector3(positionR.x, positionL.y, positionL.z);

		bodyData.joint[jointR].trackingState = trackingStateL;
		bodyData.joint[jointR].kinectPos = kinectPosL; // new Vector3(kinectPosL.x, kinectPosR.y, kinectPosR.z);
		bodyData.joint[jointR].position = positionL; // new Vector3(positionL.x, positionR.y, positionR.z);
	}

	/// <summary>
	/// Rearranges the user indices, according to the current criteria
	/// </summary>
	public virtual void RearrangeUserIndices()
	{
		
		if (userDetectionOrder != UserDetectionOrder.Appearance) 
		{
			// get current user positions
			Vector3[] userPos = new Vector3[aUserIndexIds.Length];
			for (int i = 0; i < aUserIndexIds.Length; i++) 
			{
				Int64 userId = aUserIndexIds[i];
				userPos[i] = userId != 0 ? GetUserPosition(userId) : Vector3.zero;
			}

			// bubble sort
			bool reorderDone = false;
			for(int i = aUserIndexIds.Length - 1; i >= 1; i--)
			{
				bool switchDone = false;

				for (int j = 0; j < i; j++) 
				{
					float userDist1 = 0f;
					if(userDetectionOrder == UserDetectionOrder.Distance)
						userDist1 = Mathf.Abs(userPos[j].x) + Mathf.Abs(userPos[j].z);
					else if(userDetectionOrder == UserDetectionOrder.LeftToRight)
						userDist1 = userPos[j].x;
					
					if (Mathf.Abs (userDist1) < 0.01f)
						userDist1 = 1000f;  // far away

					float userDist2 = 0f;
					if(userDetectionOrder == UserDetectionOrder.Distance)
						userDist2 = Mathf.Abs(userPos[j + 1].x) + Mathf.Abs(userPos[j + 1].z);
					else if(userDetectionOrder == UserDetectionOrder.LeftToRight)
						userDist2 = userPos[j + 1].x;
					
					if (Mathf.Abs (userDist2) < 0.01f)
						userDist2 = 1000f;  // far away

					if (userDist1 > userDist2) 
					{
						// switch them
						Int64 tmpUserId = aUserIndexIds[j];
						aUserIndexIds[j] = aUserIndexIds[j + 1];
						aUserIndexIds[j + 1] = tmpUserId;

						reorderDone = switchDone = true;
					}
				}

				if (!switchDone)  // check for sorted array
					break;
			}

			if (reorderDone) 
			{
				System.Text.StringBuilder sbUsersOrder = new System.Text.StringBuilder();
				sbUsersOrder.Append("Users reindexed: ");

				for (int i = 0; i < aUserIndexIds.Length; i++) 
				{
					if (aUserIndexIds [i] != 0) 
					{
						sbUsersOrder.Append(i).Append(":").Append(aUserIndexIds[i]).Append("  ");
					}
				}

				Debug.Log(sbUsersOrder.ToString());
			}
		} 

	}

	// Returns empty user slot for the given user Id
	protected virtual int GetEmptyUserSlot(Int64 userId, int bodyIndex)
	{
		// rearrange current users
		RearrangeUserIndices();
		int uidIndex = -1;

		if (userDetectionOrder != UserDetectionOrder.Appearance) 
		{
			// add the new user, depending on the distance
			Vector3 userPos = bodyFrame.bodyData[bodyIndex].position;

			float userDist = 0f;
			if(userDetectionOrder == UserDetectionOrder.Distance)
				userDist = Mathf.Abs(userPos.x) + Mathf.Abs(userPos.z);
			else if(userDetectionOrder == UserDetectionOrder.LeftToRight)
				userDist = userPos.x;

			for(int i = 0; i < aUserIndexIds.Length; i++)
			{
				if(aUserIndexIds[i] == 0)
				{
					// free user slot
					uidIndex = i;
					break;
				}
				else
				{
					Int64 uidUserId = aUserIndexIds[i];
					Vector3 uidUserPos = GetUserPosition (uidUserId);

					float uidUserDist = 0;
					if(userDetectionOrder == UserDetectionOrder.Distance)
						uidUserDist = Mathf.Abs(uidUserPos.x) + Mathf.Abs(uidUserPos.z);
					else if(userDetectionOrder == UserDetectionOrder.LeftToRight)
						uidUserDist = uidUserPos.x;

					if(userDist <= uidUserDist)
					{
						// current user is left to the compared one
						for(int u = (aUserIndexIds.Length - 2); u >= i ; u--)
						{
							aUserIndexIds[u + 1] = aUserIndexIds[u];

							if (aUserIndexIds[u] != 0) 
							{
								Debug.Log(string.Format("Reindexing user {0} to {1}, ID: {2}.", u, u + 1, aUserIndexIds[u]));
							}
						}

						aUserIndexIds[i] = 0; // cleanup current index
						uidIndex = i;
						break;
					}
				}
			}

		} 
		else 
		{
			// look for the 1st available slot
			for(int i = 0; i < aUserIndexIds.Length; i++)
			{
				if(aUserIndexIds[i] == 0)
				{
					uidIndex = i;
					break;
				}
			}

//			for(int i = aUserIndexIds.Length - 1; i >= 0; i--)
//			{
//				if(aUserIndexIds[i] == 0)
//				{
//					uidIndex = i;
//				}
//				else if(uidIndex >= 0)
//				{
//					break;
//				}
//			}
		}

		return uidIndex;
	}
	
	// releases the user slot. rearranges the remaining users.
	protected virtual void FreeEmptyUserSlot(int uidIndex)
	{
		aUserIndexIds[uidIndex] = 0;

		if (userDetectionOrder != UserDetectionOrder.Appearance) 
		{
			// rearrange the remaining users
			for(int u = uidIndex; u < (aUserIndexIds.Length - 1); u++)
			{
				aUserIndexIds[u] = aUserIndexIds[u + 1];

				if (aUserIndexIds[u + 1] != 0) 
				{
					Debug.Log(string.Format("Reindexing user {0} to {1}, ID: {2}.", u + 1, u, aUserIndexIds[u + 1]));
				}
			}

			// make sure the last slot is free
			aUserIndexIds[aUserIndexIds.Length - 1] = 0;
		}

		// rearrange the remaining users
		RearrangeUserIndices();
	}

	/// <summary>
	/// Refreshs the userIds of all available avatar controllers, after new user appears in front of the sensor, or when some user gets lost.
	/// </summary>
	public void RefreshAvatarUserIds()
	{
		for(int i = 0; i < avatarControllers.Count; i++)
		{
			AvatarController avatar = avatarControllers[i];
			if(!avatar)
				continue;

			long oldUserId = avatar.playerId;
			long newUserId = GetUserIdByIndex(avatar.playerIndex);

			if(oldUserId != newUserId)
			{
				avatar.playerId = newUserId;

				if (oldUserId == 0 && newUserId != 0)
					avatar.SuccessfulCalibration(newUserId, false);
				else if (oldUserId != 0 && newUserId == 0)
					avatar.ResetToInitialPosition();
			}
		}
	}

	// Adds UserId to the list of users
    protected virtual void CalibrateUser(Int64 userId, int bodyIndex)
    {
		if(!alUserIds.Contains(userId))
		{
			if(CheckForCalibrationPose(userId, bodyIndex, playerCalibrationPose))
			{
				//int uidIndex = alUserIds.Count;
				int uidIndex = GetEmptyUserSlot(userId, bodyIndex);

				if(uidIndex >= 0)
				{
					aUserIndexIds[uidIndex] = userId;
				}
				else
				{
					// no empty user-index slot
					return;
				}
				
				Debug.Log("Adding user " + uidIndex + ", ID: " + userId + ", Body: " + bodyIndex);

				dictUserIdToIndex[userId] = bodyIndex;
				dictUserIdToTime[userId] = Time.time;
				alUserIds.Add(userId);

				// set primary user-id, if there is none
				if(liPrimaryUserId == 0 && aUserIndexIds.Length > 0)
				{
					liPrimaryUserId = aUserIndexIds[0];  // userId
					
					if(liPrimaryUserId != 0)
					{
						if(calibrationText != null && calibrationText.text != "")
						{
							calibrationText.text = "";
						}
					}
				}

				// update userIds of the avatar controllers
				RefreshAvatarUserIds();
//				for(int i = 0; i < avatarControllers.Count; i++)
//				{
//					AvatarController avatar = avatarControllers[i];
//
//					//if(avatar && avatar.playerIndex == uidIndex)
//					if(avatar && avatar.playerIndex == uidIndex && avatar.playerId == 0)
//					{
//						avatar.playerId = userId;
//						avatar.SuccessfulCalibration(userId, false);
//					}
//				}
				
				// add the gestures to be detected by all users, if any
				foreach(KinectGestures.Gestures gesture in playerCommonGestures)
				{
					DetectGesture(userId, gesture);
				}
				
				// notify all gesture listeners for the newly detected user
				foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
				{
					if(listener != null)
					{
						listener.UserDetected(userId, uidIndex);
					}
				}
				
				ResetFilters();
			}
		}
    }
	
	// Remove a lost UserId
	protected virtual void RemoveUser(Int64 userId)
	{
		//int uidIndex = alUserIds.IndexOf(userId);
		int uidIndex = Array.IndexOf(aUserIndexIds, userId);
		Debug.Log("Removing user " + uidIndex + ", ID: " + userId + ", Body: " + dictUserIdToIndex[userId]);

//		// reset the respective avatar controllers
//		for(int i = 0; i < avatarControllers.Count; i++)
//		{
//			AvatarController avatar = avatarControllers[i];
//
//			//if(avatar && avatar.playerIndex >= uidIndex && avatar.playerIndex < alUserIds.Count)
//			if(avatar && avatar.playerId == userId)
//			{
//				avatar.ResetToInitialPosition();
//				avatar.playerId = 0;
//			}
//		}

		// notify all gesture listeners for losing this user
		foreach(KinectGestures.GestureListenerInterface listener in gestureListeners)
		{
			if(listener != null)
			{
				listener.UserLost(userId, uidIndex);
			}
		}

		// clear gestures list for this user
		ClearGestures(userId);

		// clear calibration data for this user
		if(playerCalibrationData.ContainsKey(userId))
		{
			playerCalibrationData.Remove(userId);
		}

		// clean up the outdated calibration data in the data dictionary
		List<Int64> alCalDataKeys = new List<Int64>(playerCalibrationData.Keys);

		foreach(Int64 calUserID in alCalDataKeys)
		{
			KinectGestures.GestureData gestureData = playerCalibrationData[calUserID];

			if((gestureData.timestamp + 60f) < Time.realtimeSinceStartup)
			{
				playerCalibrationData.Remove(calUserID);
			}
		}

		alCalDataKeys.Clear();
		
		// remove user-id from the global users lists
		dictUserIdToIndex.Remove(userId);
		dictUserIdToTime.Remove(userId);
		alUserIds.Remove(userId);

		if(uidIndex >= 0)
		{
			FreeEmptyUserSlot(uidIndex);
		}

		// update userIds of the avatar controllers
		RefreshAvatarUserIds();

		// if this was the primary user, update the primary user-id
		if(liPrimaryUserId == userId)
		{
			if(aUserIndexIds.Length > 0)
			{
				liPrimaryUserId = aUserIndexIds[0];
			}
			else
			{
				liPrimaryUserId = 0;
			}
		}
		
		if(alUserIds.Count == 0)
		{
			Debug.Log("Waiting for users.");
			
			if(calibrationText != null)
			{
				calibrationText.text = "WAITING FOR USERS";
			}
		}
	}
	
	// draws the skeleton in the given texture
	private void DrawSkeleton(Texture2D aTexture, ref KinectInterop.BodyData bodyData)
	{
		if (aTexture == null)
			return;
		
		int jointsCount = sensorData.jointCount;
		for(int i = 0; i < jointsCount; i++)
		{
			int parent = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)i);
			
			if(bodyData.joint[i].trackingState != KinectInterop.TrackingState.NotTracked && 
			   bodyData.joint[parent].trackingState != KinectInterop.TrackingState.NotTracked)
			{
				Vector2 posParent = KinectInterop.MapSpacePointToDepthCoords(sensorData, bodyData.joint[parent].kinectPos);
				Vector2 posJoint = KinectInterop.MapSpacePointToDepthCoords(sensorData, bodyData.joint[i].kinectPos);
				
				if(posParent != Vector2.zero && posJoint != Vector2.zero)
				{
					//Color lineColor = playerJointsTracked[i] && playerJointsTracked[parent] ? Color.red : Color.yellow;
					KinectInterop.DrawLine(aTexture, (int)posParent.x, (int)posParent.y, (int)posJoint.x, (int)posJoint.y, Color.yellow);
				}
			}
		}
		
		//aTexture.Apply();
	}

	// calculates orientations of the body joints
	private void CalculateJointOrients(ref KinectInterop.BodyData bodyData)
	{
		int jointCount = bodyData.joint.Length;

		for(int j = 0; j < jointCount; j++)
		{
			int joint = j;

			KinectInterop.JointData jointData = bodyData.joint[joint];
			bool bJointValid = ignoreInferredJoints ? jointData.trackingState == KinectInterop.TrackingState.Tracked : jointData.trackingState != KinectInterop.TrackingState.NotTracked;

			if(bJointValid)
			{
				int nextJoint = (int)sensorData.sensorInterface.GetNextJoint((KinectInterop.JointType)joint);
				if(nextJoint != joint && nextJoint >= 0 && nextJoint < sensorData.jointCount)
				{
					KinectInterop.JointData nextJointData = bodyData.joint[nextJoint];
					bool bNextJointValid = ignoreInferredJoints ? nextJointData.trackingState == KinectInterop.TrackingState.Tracked : nextJointData.trackingState != KinectInterop.TrackingState.NotTracked;

					Vector3 baseDir = KinectInterop.JointBaseDir[nextJoint];
					Vector3 jointDir = nextJointData.direction.normalized;
					jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;

					Quaternion jointOrientNormal = jointData.normalRotation;
					if(bNextJointValid)
					{
						jointOrientNormal = Quaternion.FromToRotation(baseDir, jointDir);
					}

					if ((joint == (int)KinectInterop.JointType.ShoulderLeft) ||
						(joint == (int)KinectInterop.JointType.ShoulderRight)) 
					{
//						float angle = -bodyData.bodyTurnAngle;
//						Vector3 axis = jointDir;
//						Quaternion armTurnRotation = Quaternion.AngleAxis (angle, axis);
//
//						jointData.normalRotation = armTurnRotation * jointOrientNormal;

						Vector3 baseDir2 = Vector3.right;
						Vector3 jointDir2 = !bodyData.isTurnedAround ? bodyData.shouldersDirection : -bodyData.shouldersDirection; //Vector3.Lerp(bodyData.shouldersDirection, -bodyData.shouldersDirection, bodyData.turnAroundFactor);
						jointDir2.z = -jointDir2.z;

						jointData.normalRotation = Quaternion.FromToRotation(baseDir2, jointDir2) * jointOrientNormal;
					} 
					else if ((joint == (int)KinectInterop.JointType.ElbowLeft) ||
						(joint == (int)KinectInterop.JointType.WristLeft) ||
						(joint == (int)KinectInterop.JointType.HandLeft)) 
					{
						if (bNextJointValid && jointData.direction != Vector3.zero && jointDir != Vector3.zero)
						{
							Vector3 parJointDir = jointData.direction.normalized;
							parJointDir = new Vector3 (parJointDir.x, parJointDir.y, -parJointDir.z).normalized;

							if (joint == (int)KinectInterop.JointType.WristLeft) 
							{
								// for wrist, take the finger direction into account, too
								int fingerJoint = (int)sensorData.sensorInterface.GetNextJoint((KinectInterop.JointType)nextJoint);

								if (fingerJoint != joint && fingerJoint >= 0 && fingerJoint < sensorData.jointCount) 
								{
									KinectInterop.JointData fingerData = bodyData.joint [fingerJoint];
									if (fingerData.trackingState != KinectInterop.TrackingState.NotTracked) 
									{
										jointDir = (nextJointData.direction + fingerData.direction).normalized;
										jointDir = new Vector3 (jointDir.x, jointDir.y, -jointDir.z).normalized;
									}
								}
							}

							float parDotJoint = Vector3.Dot (parJointDir, jointDir);
							//Debug.Log (joint + ": " + parDotJoint);

							if (Mathf.Abs(parDotJoint) <= 0.9f ||
								bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState == KinectInterop.TrackingState.NotTracked) 
							{
								Vector3 upDir = -Vector3.Cross (-parJointDir, jointDir).normalized;
								Vector3 fwdDir = Vector3.Cross (-jointDir, upDir).normalized;
								jointOrientNormal = Quaternion.LookRotation (fwdDir, upDir);
							} 
							else 
							{
								KinectInterop.JointData shCenterData = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder];

								Vector3 spineDir = shCenterData.direction.normalized;
								spineDir = new Vector3 (spineDir.x, spineDir.y, -spineDir.z).normalized;

								Vector3 fwdDir = Vector3.Cross (-jointDir, spineDir).normalized;
								Vector3 upDir = Vector3.Cross (fwdDir, -jointDir).normalized;
								jointOrientNormal = Quaternion.LookRotation (fwdDir, upDir);
							}

							jointData.normalRotation = jointOrientNormal;
						}

						// check for 'allowedHandRotations = None'
						bool bRotated = (allowedHandRotations == AllowedRotations.None) && (joint != (int)KinectInterop.JointType.ElbowLeft);
						if (bRotated) 
						{
							// in case of 'allowedHandRotations = None' take the parent's orientation
							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)joint);

							if (prevJoint != joint && prevJoint >= 0 && prevJoint < sensorData.jointCount) 
							{
								jointData.normalRotation = bodyData.joint[prevJoint].normalRotation;
							}
						}

						if (((allowedHandRotations == AllowedRotations.All && joint != (int)KinectInterop.JointType.ElbowLeft) || 
							(joint == (int)KinectInterop.JointType.HandLeft && !bRotated)) &&
							(sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv2 ||
							sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.DummyK2))
						{						   
							KinectInterop.JointData thumbData = bodyData.joint [(int)KinectInterop.JointType.ThumbLeft];

							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)joint);
							KinectInterop.JointData prevJointData = bodyData.joint [prevJoint];

							if (thumbData.trackingState != KinectInterop.TrackingState.NotTracked &&
								prevJointData.trackingState != KinectInterop.TrackingState.NotTracked) 
							{
								Vector3 rightDir = -jointDir; // -(handData.direction + handTipData.direction);
								//rightDir = new Vector3 (rightDir.x, rightDir.y, -rightDir.z).normalized;

								Vector3 fwdDir = thumbData.direction.normalized;
								fwdDir = new Vector3 (fwdDir.x, fwdDir.y, -fwdDir.z).normalized;

								if (joint == (int)KinectInterop.JointType.HandLeft) 
								{
									Vector3 prevBaseDir = -KinectInterop.JointBaseDir[prevJoint];
									Vector3 prevOrthoDir = new Vector3 (prevBaseDir.y, prevBaseDir.z, prevBaseDir.x);
									fwdDir = prevJointData.normalRotation * prevOrthoDir;
									//rightDir -= Vector3.Project(rightDir, fwdDir);
								}

								if (rightDir != Vector3.zero && fwdDir != Vector3.zero) 
								{
									Vector3 upDir = Vector3.Cross (fwdDir, rightDir).normalized;
									fwdDir = Vector3.Cross (rightDir, upDir).normalized;

									//jointData.normalRotation = Quaternion.LookRotation(fwdDir, upDir);
									Quaternion jointOrientThumb = Quaternion.LookRotation (fwdDir, upDir);
									jointOrientNormal = (joint == (int)KinectInterop.JointType.WristLeft) ?
										Quaternion.RotateTowards (prevJointData.normalRotation, jointOrientThumb, 80f) : jointOrientThumb;
									
									jointData.normalRotation = jointOrientNormal;
									//bRotated = true;
								}
							}

							bRotated = true;
						}

						if (joint != (int)KinectInterop.JointType.ElbowLeft) 
						{
							// limit wrist and hand twist
							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)joint);
							KinectInterop.JointData prevJointData = bodyData.joint [prevJoint];

							if (prevJointData.trackingState != KinectInterop.TrackingState.NotTracked) 
							{
								jointData.normalRotation = Quaternion.RotateTowards (prevJointData.normalRotation, jointData.normalRotation, 70f);
							}
						}

					} 
					else if ((joint == (int)KinectInterop.JointType.ElbowRight) ||
						(joint == (int)KinectInterop.JointType.WristRight) ||
						(joint == (int)KinectInterop.JointType.HandRight)) 
					{
						if (bNextJointValid && jointData.direction != Vector3.zero && jointDir != Vector3.zero)
						{
							Vector3 parJointDir = jointData.direction.normalized;
							parJointDir = new Vector3 (parJointDir.x, parJointDir.y, -parJointDir.z).normalized;

							if (joint == (int)KinectInterop.JointType.WristRight) 
							{
								// for wrist, take the finger direction into account, too
								int fingerJoint = (int)sensorData.sensorInterface.GetNextJoint((KinectInterop.JointType)nextJoint);

								if (fingerJoint != joint && fingerJoint >= 0 && fingerJoint < sensorData.jointCount) 
								{
									KinectInterop.JointData fingerData = bodyData.joint [fingerJoint];
									if (fingerData.trackingState != KinectInterop.TrackingState.NotTracked) 
									{
										jointDir = (nextJointData.direction + fingerData.direction).normalized;
										jointDir = new Vector3 (jointDir.x, jointDir.y, -jointDir.z).normalized;
									}
								}
							}

							// estimate the joint orientation
							float parDotJoint = Vector3.Dot (parJointDir, jointDir);
							//Debug.Log (joint + ": " + parDotJoint);

							if (Mathf.Abs(parDotJoint) <= 0.9f ||
								bodyData.joint[(int)KinectInterop.JointType.SpineShoulder].trackingState == KinectInterop.TrackingState.NotTracked) 
							{
								Vector3 upDir = -Vector3.Cross (parJointDir, jointDir).normalized;
								Vector3 fwdDir = Vector3.Cross (jointDir, upDir).normalized;
								jointOrientNormal = Quaternion.LookRotation (fwdDir, upDir);
							} 
							else 
							{
								KinectInterop.JointData shCenterData = bodyData.joint[(int)KinectInterop.JointType.SpineShoulder];

								Vector3 spineDir = shCenterData.direction.normalized;
								spineDir = new Vector3 (spineDir.x, spineDir.y, -spineDir.z).normalized;

								Vector3 fwdDir = Vector3.Cross (jointDir, spineDir).normalized;
								Vector3 upDir = Vector3.Cross (fwdDir, jointDir).normalized;
								jointOrientNormal = Quaternion.LookRotation (fwdDir, upDir);
							}

							jointData.normalRotation = jointOrientNormal;
						}

						// check for 'allowedHandRotations = None'
						bool bRotated = (allowedHandRotations == AllowedRotations.None) && (joint != (int)KinectInterop.JointType.ElbowRight);
						if (bRotated) 
						{
							// in case of 'allowedHandRotations = None' take the parent's orientation
							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)joint);

							if (prevJoint != joint && prevJoint >= 0 && prevJoint < sensorData.jointCount) 
							{
								jointData.normalRotation = bodyData.joint[prevJoint].normalRotation;
							}
						}

						if (((allowedHandRotations == AllowedRotations.All && joint != (int)KinectInterop.JointType.ElbowRight) || 
							(joint == (int)KinectInterop.JointType.HandRight && !bRotated)) &&
							(sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv2 ||
							 sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.DummyK2))
						{
							KinectInterop.JointData thumbData = bodyData.joint [(int)KinectInterop.JointType.ThumbRight];

							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)joint);
							KinectInterop.JointData prevJointData = bodyData.joint [prevJoint];

							if (thumbData.trackingState != KinectInterop.TrackingState.NotTracked &&
								prevJointData.trackingState != KinectInterop.TrackingState.NotTracked) 
							{
								Vector3 rightDir = jointDir; // handData.direction + handTipData.direction;
								//rightDir = new Vector3 (rightDir.x, rightDir.y, -rightDir.z).normalized;

								Vector3 fwdDir = thumbData.direction.normalized;
								fwdDir = new Vector3 (fwdDir.x, fwdDir.y, -fwdDir.z).normalized;

								if (joint == (int)KinectInterop.JointType.HandRight) 
								{
									Vector3 prevBaseDir = KinectInterop.JointBaseDir[prevJoint];
									Vector3 prevOrthoDir = new Vector3 (prevBaseDir.y, prevBaseDir.z, prevBaseDir.x);
									fwdDir = prevJointData.normalRotation * prevOrthoDir;
									//rightDir -= Vector3.Project(rightDir, fwdDir);
								}

								if (rightDir != Vector3.zero && fwdDir != Vector3.zero) 
								{
									Vector3 upDir = Vector3.Cross (fwdDir, rightDir).normalized;
									fwdDir = Vector3.Cross (rightDir, upDir).normalized;

									Quaternion jointOrientThumb = Quaternion.LookRotation (fwdDir, upDir);
									jointOrientNormal = (joint == (int)KinectInterop.JointType.WristRight) ?
										Quaternion.RotateTowards (prevJointData.normalRotation, jointOrientThumb, 80f) : jointOrientThumb;
									
									jointData.normalRotation = jointOrientNormal;
									//bRotated = true;
								}
							}

							bRotated = true;
						}

						if (joint != (int)KinectInterop.JointType.ElbowRight) 
						{
							// limit wrist and hand twist
							int prevJoint = (int)sensorData.sensorInterface.GetParentJoint ((KinectInterop.JointType)joint);
							KinectInterop.JointData prevJointData = bodyData.joint [prevJoint];

							if (prevJointData.trackingState != KinectInterop.TrackingState.NotTracked) 
							{
								jointData.normalRotation = Quaternion.RotateTowards (prevJointData.normalRotation, jointData.normalRotation, 70f);
							}
						}

					}
					else
					{
						jointData.normalRotation = jointOrientNormal;
					}

					if((joint == (int)KinectInterop.JointType.SpineMid) || 
						(joint == (int)KinectInterop.JointType.SpineShoulder) || 
						(joint == (int)KinectInterop.JointType.Neck))
					{
						Vector3 baseDir2 = Vector3.right;
						Vector3 jointDir2 = bodyData.shouldersDirection; // Vector3.Lerp(bodyData.shouldersDirection, -bodyData.shouldersDirection, bodyData.turnAroundFactor);
						jointDir2.z = -jointDir2.z;

						jointData.normalRotation *= Quaternion.FromToRotation(baseDir2, jointDir2);
					}
					else if((joint == (int)KinectInterop.JointType.SpineBase) ||
						(joint == (int)KinectInterop.JointType.HipLeft) || (joint == (int)KinectInterop.JointType.HipRight) ||
						(joint == (int)KinectInterop.JointType.KneeLeft) || (joint == (int)KinectInterop.JointType.KneeRight) ||
						(joint == (int)KinectInterop.JointType.AnkleLeft) || (joint == (int)KinectInterop.JointType.AnkleRight))
					{
						Vector3 baseDir2 = Vector3.right;
						Vector3 jointDir2 = bodyData.hipsDirection; // Vector3.Lerp(bodyData.hipsDirection, -bodyData.hipsDirection, bodyData.turnAroundFactor);
						jointDir2.z = -jointDir2.z;

						jointData.normalRotation *= Quaternion.FromToRotation(baseDir2, jointDir2);
					}

					if(joint == (int)KinectInterop.JointType.Neck && 
						sensorData != null && sensorData.sensorInterface != null)
					{
						if(sensorData.sensorInterface.IsFaceTrackingActive() && 
							sensorData.sensorInterface.IsFaceTracked(bodyData.liTrackingID))
						{
							KinectInterop.JointData neckData = bodyData.joint[(int)KinectInterop.JointType.Neck];
							KinectInterop.JointData headData = bodyData.joint[(int)KinectInterop.JointType.Head];

							if(neckData.trackingState == KinectInterop.TrackingState.Tracked &&
								headData.trackingState == KinectInterop.TrackingState.Tracked)
							{
								Quaternion headRotation = Quaternion.identity;
								if(sensorData.sensorInterface.GetHeadRotation(bodyData.liTrackingID, ref headRotation))
								{
									if (headRotation != Quaternion.identity) 
									{
										Vector3 rotAngles = headRotation.eulerAngles;
										rotAngles.x = -rotAngles.x;
										rotAngles.y = -rotAngles.y;

										rotAngles.x += sensorAngle;

										bodyData.headOrientation = bodyData.headOrientation != Quaternion.identity ?
											Quaternion.Slerp(bodyData.headOrientation, Quaternion.Euler(rotAngles), 10f * Time.deltaTime) :
											Quaternion.Euler(rotAngles);

										//jointData.normalRotation = bodyData.headOrientation;
									}
								}
							}
						}

						if (bodyData.headOrientation != Quaternion.identity) 
						{
							jointData.normalRotation = bodyData.headOrientation;
						}
					}

					Vector3 mirroredAngles = jointData.normalRotation.eulerAngles;
					mirroredAngles.y = -mirroredAngles.y;
					mirroredAngles.z = -mirroredAngles.z;

					jointData.mirroredRotation = Quaternion.Euler(mirroredAngles);
				}
				else
				{
					// get the orientation of the parent joint
					int prevJoint = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)joint);
					if(prevJoint != joint && prevJoint >= 0 && prevJoint < sensorData.jointCount &&
						joint != (int)KinectInterop.JointType.ThumbLeft && joint != (int)KinectInterop.JointType.ThumbRight)
					{
//						if((allowedHandRotations == AllowedRotations.All) && 
//						   (joint == (int)KinectInterop.JointType.ThumbLeft ||
//						    joint == (int)KinectInterop.JointType.ThumbRight))
//						{
//							Vector3 jointDir = jointData.direction;
//							jointDir = new Vector3(jointDir.x, jointDir.y, -jointDir.z).normalized;
//
//							Vector3 baseDir = KinectInterop.JointBaseDir[joint];
//							jointData.normalRotation = Quaternion.FromToRotation(baseDir, jointDir);
//
//							Vector3 mirroredAngles = jointData.normalRotation.eulerAngles;
//							mirroredAngles.y = -mirroredAngles.y;
//							mirroredAngles.z = -mirroredAngles.z;
//							
//							jointData.mirroredRotation = Quaternion.Euler(mirroredAngles);
//						}
//						else
						{
							jointData.normalRotation = bodyData.joint[prevJoint].normalRotation;
							jointData.mirroredRotation = bodyData.joint[prevJoint].mirroredRotation;
						}
					}
					else
					{
						jointData.normalRotation = Quaternion.identity;
						jointData.mirroredRotation = Quaternion.identity;
					}
				}
			}

			bodyData.joint[joint] = jointData;

			if(joint == (int)KinectInterop.JointType.SpineBase)
			{
				bodyData.normalRotation = jointData.normalRotation;
				bodyData.mirroredRotation = jointData.mirroredRotation;
			}
		}
	}

	// Estimates the current state of the defined gestures
	private void CheckForGestures(Int64 UserId)
	{
		if(!gestureManager || !playerGesturesData.ContainsKey(UserId) || !gesturesTrackingAtTime.ContainsKey(UserId))
			return;
		
		// check for gestures
		if(Time.realtimeSinceStartup >= gesturesTrackingAtTime[UserId])
		{
			// get joint positions and tracking
			int iAllJointsCount = sensorData.jointCount;
			bool[] playerJointsTracked = new bool[iAllJointsCount];
			Vector3[] playerJointsPos = new Vector3[iAllJointsCount];
			
			int[] aiNeededJointIndexes = gestureManager.GetNeededJointIndexes(instance);
			int iNeededJointsCount = aiNeededJointIndexes.Length;
			
			for(int i = 0; i < iNeededJointsCount; i++)
			{
				int joint = aiNeededJointIndexes[i];
				
				if(joint >= 0)
				{
					playerJointsTracked[joint] = IsJointTracked(UserId, joint);
					playerJointsPos[joint] = GetJointPosition(UserId, joint);

					if (!playerJointsTracked[joint] && (joint == (int)KinectInterop.JointType.SpineShoulder) && 
						IsJointTracked(UserId, (int)KinectInterop.JointType.ShoulderLeft) && IsJointTracked(UserId, (int)KinectInterop.JointType.ShoulderRight)) 
					{
						playerJointsTracked[joint] = true;
						playerJointsPos[joint] = (GetJointPosition(UserId, (int)KinectInterop.JointType.ShoulderLeft) + GetJointPosition(UserId, (int)KinectInterop.JointType.ShoulderRight)) / 2f;
					}
				}
			}
			
			// check for gestures
			List<KinectGestures.GestureData> gesturesData = playerGesturesData[UserId];
			
			int listGestureSize = gesturesData.Count;
			float timestampNow = Time.realtimeSinceStartup;
			string sDebugGestures = string.Empty;  // "Tracked Gestures:\n";
			
			for(int g = 0; g < listGestureSize; g++)
			{
				KinectGestures.GestureData gestureData = gesturesData[g];
				
				if((timestampNow >= gestureData.startTrackingAtTime) && 
					!IsConflictingGestureInProgress(gestureData, ref gesturesData))
				{
					gestureManager.CheckForGesture(UserId, ref gestureData, Time.realtimeSinceStartup, 
						ref playerJointsPos, ref playerJointsTracked);
					gesturesData[g] = gestureData;

					if(gestureData.complete)
					{
						gesturesTrackingAtTime[UserId] = timestampNow + minTimeBetweenGestures;
					}
					
					if(UserId == liPrimaryUserId)
					{
						sDebugGestures += string.Format("{0} - state: {1}, time: {2:F1}, progress: {3}%\n", 
														gestureData.gesture, gestureData.state, 
						                                gestureData.timestamp,
														(int)(gestureData.progress * 100 + 0.5f));
					}
				}
			}
			
			playerGesturesData[UserId] = gesturesData;
			
			if(gesturesDebugText && (UserId == liPrimaryUserId))
			{
				for(int i = 0; i < iNeededJointsCount; i++)
				{
					int joint = aiNeededJointIndexes[i];

					sDebugGestures += string.Format("\n {0}: {1}", (KinectInterop.JointType)joint,
					                                playerJointsTracked[joint] ? playerJointsPos[joint].ToString() : "");
				}

				gesturesDebugText.text = sDebugGestures;
			}
		}
	}
	
	private bool IsConflictingGestureInProgress(KinectGestures.GestureData gestureData, ref List<KinectGestures.GestureData> gesturesData)
	{
		foreach(KinectGestures.Gestures gesture in gestureData.checkForGestures)
		{
			int index = GetGestureIndex(gesture, ref gesturesData);
			
			if(index >= 0)
			{
				if(gesturesData[index].progress > 0f)
					return true;
			}
		}
		
		return false;
	}
	
	// return the index of gesture in the list, or -1 if not found
	private int GetGestureIndex(KinectGestures.Gestures gesture, ref List<KinectGestures.GestureData> gesturesData)
	{
		int listSize = gesturesData.Count;
	
		for(int i = 0; i < listSize; i++)
		{
			if(gesturesData[i].gesture == gesture)
				return i;
		}
		
		return -1;
	}
	
	// check if the calibration pose is complete for given user
	protected virtual bool CheckForCalibrationPose(Int64 UserId, int bodyIndex, KinectGestures.Gestures calibrationGesture)
	{
		if(calibrationGesture == KinectGestures.Gestures.None)
			return true;
		if(!gestureManager)
			return false;

		KinectGestures.GestureData gestureData = playerCalibrationData.ContainsKey(UserId) ? 
			playerCalibrationData[UserId] : new KinectGestures.GestureData();
		
		// init gesture data if needed
		if(gestureData.userId != UserId)
		{
			gestureData.userId = UserId;
			gestureData.gesture = calibrationGesture;
			gestureData.state = 0;
			gestureData.timestamp = Time.realtimeSinceStartup;
			gestureData.joint = 0;
			gestureData.progress = 0f;
			gestureData.complete = false;
			gestureData.cancelled = false;
		}
		
		// get joint positions and tracking
		int iAllJointsCount = sensorData.jointCount;
		bool[] playerJointsTracked = new bool[iAllJointsCount];
		Vector3[] playerJointsPos = new Vector3[iAllJointsCount];
		
		int[] aiNeededJointIndexes = gestureManager.GetNeededJointIndexes(instance);
		int iNeededJointsCount = aiNeededJointIndexes.Length;
		
		for(int i = 0; i < iNeededJointsCount; i++)
		{
			int joint = aiNeededJointIndexes[i];
			
			if(joint >= 0)
			{
				KinectInterop.JointData jointData = bodyFrame.bodyData[bodyIndex].joint[joint];
				
				playerJointsTracked[joint] = jointData.trackingState != KinectInterop.TrackingState.NotTracked;
				playerJointsPos[joint] = jointData.kinectPos;

				if (!playerJointsTracked[joint] && (joint == (int)KinectInterop.JointType.SpineShoulder)) 
				{
					KinectInterop.JointData lShoulderData = bodyFrame.bodyData[bodyIndex].joint[(int)KinectInterop.JointType.ShoulderLeft];
					KinectInterop.JointData rShoulderData = bodyFrame.bodyData[bodyIndex].joint[(int)KinectInterop.JointType.ShoulderRight];

					if (lShoulderData.trackingState != KinectInterop.TrackingState.NotTracked && rShoulderData.trackingState != KinectInterop.TrackingState.NotTracked) 
					{
						playerJointsTracked[joint] = true;
						playerJointsPos[joint] = (lShoulderData.kinectPos + rShoulderData.kinectPos) / 2f;
					}
				}
			}
		}
		
		// estimate the gesture progess
		gestureManager.CheckForGesture(UserId, ref gestureData, Time.realtimeSinceStartup, 
			ref playerJointsPos, ref playerJointsTracked);
		playerCalibrationData[UserId] = gestureData;

		// check if gesture is complete
		if(gestureData.complete)
		{
			gestureData.userId = 0;
			playerCalibrationData[UserId] = gestureData;

			return true;
		}

		return false;
	}

	/// <summary>
	/// Refreshs the gesture listeners' list.
	/// </summary>
	public void refreshGestureListeners()
	{
		
		// locate the available gesture listeners
		gestureListeners.Clear();

		MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

		foreach(MonoBehaviour monoScript in monoScripts)
		{
//			if(typeof(KinectGestures.GestureListenerInterface).IsAssignableFrom(monoScript.GetType()) &&
//				monoScript.enabled)
			if((monoScript is KinectGestures.GestureListenerInterface) && monoScript.enabled)
			{
				//KinectGestures.GestureListenerInterface gl = (KinectGestures.GestureListenerInterface)monoScript;
				gestureListeners.Add(monoScript);
			}
		}

		// locate Kinect gesture manager, if any
		gestureManager = null;
		foreach(MonoBehaviour monoScript in monoScripts)
		{
//			if(typeof(KinectGestures).IsAssignableFrom(monoScript.GetType()) && 
//				monoScript.enabled)
			if((monoScript is KinectGestures) && monoScript.enabled)
			{
				// if skipRemoteAvatars - add only the local gesture managers
                if( skipRemoteAvatars && monoScript is NetworkBehaviour )
                {
                    if( false == ( monoScript as NetworkBehaviour ).isLocalPlayer )
                    {
						Debug.Log( "KM: KinectGestures not registered because is not a local object!" );
                        continue;   // skip network objects from other clients -> they are controlled by the KM on other machine
                    }
                }
                
				gestureManager = (KinectGestures)monoScript;
				break;
			}
		}
	}

	/// <summary>
	/// Refreshs the avatar controllers' list.
	/// </summary>
	public void refreshAvatarControllers()
	{
		// remove all users, filters and avatar controllers
		avatarControllers.Clear();
		//ClearKinectUsers();

		// get the mono scripts. avatar controllers and gesture listeners are among them
		MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];

		// locate the available avatar controllers
		foreach(MonoBehaviour monoScript in monoScripts)
		{
//			if(typeof(AvatarController).IsAssignableFrom(monoScript.GetType()) &&
//				monoScript.enabled)
			if((monoScript is AvatarController) && monoScript.enabled)
			{
				// if skipRemoteAvatars - add only the local avatar controllers
                if( skipRemoteAvatars && monoScript is NetworkBehaviour )
                {
                    if( false == ( monoScript as NetworkBehaviour ).isLocalPlayer )
                    {
                        Debug.Log( "KM: AvatarController not registered because is not a local object!" );
                        continue;   // skip network objects from other clients -> there are controlled by kinect in other computer 
                    }
                }
                
				AvatarController avatar = (AvatarController)monoScript;
				avatarControllers.Add(avatar);
			}
		}
	}

}

