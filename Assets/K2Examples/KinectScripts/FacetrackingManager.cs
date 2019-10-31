using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
//using System.Runtime.InteropServices;
using System.Text;


/// <summary>
/// Facetracking manager is the component that manages the head and face tracking.
/// </summary>
public class FacetrackingManager : MonoBehaviour 
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;
	
	[Tooltip("Whether to poll the HD-face model data or not.")]
	public bool getFaceModelData = false;

	[Tooltip("Whether to display the face rectangle over the color camera feed.")]
	public bool displayFaceRect = false;
	
	[Tooltip("Time tolerance in seconds, when the face may not to be tracked, without considering it lost.")]
	public float faceTrackingTolerance = 0.5f;
	
	[Tooltip("Game object that will be used to display the HD-face model mesh in the scene.")]
	public GameObject faceModelMesh = null;
	
	[Tooltip("Whether the HD-face model mesh should be mirrored or not.")]
	private bool mirroredModelMesh = true;

	//[Tooltip("Whether to skip the continuous updates of the HD-face model mesh, or not.")]
	//public bool dontUpdateModelMesh = false;

	[Tooltip("Whether to pause the updates of the HD-face model mesh.")]
	public bool pauseModelMeshUpdates = false;

	public enum TextureType : int { None, ColorMap, FaceRectangle }
	[Tooltip("How the HD-face model mesh should be textured.")]
	public TextureType texturedModelMesh = TextureType.ColorMap;

	[Tooltip("Whether to move the face model mesh, to be the same as user's head position.")]
	public bool moveModelMesh = false;

	[Tooltip("Camera used to overlay face mesh over the color background.")]
	public Camera foregroundCamera;

	[Tooltip("Scale factor for the face mesh.")]
	[Range(0.1f, 2.0f)]
	public float modelMeshScale = 1f;

	[Tooltip("Vertical offset of the mesh above the head (in meters).")]
	[Range(-0.5f, 0.5f)]
	public float verticalMeshOffset = 0f;

	[Tooltip("UI-Text to display the FT-manager debug messages.")]
	public UnityEngine.UI.Text debugText;

//	// nose and head transforms
//	public Transform noseTransform;
//	public Transform headTransform;
//	public UnityEngine.UI.Text debugText2;


	// Is currently tracking user's face
	private bool isTrackingFace = false;
	private float lastFaceTrackedTime = 0f;
	
	// Skeleton ID of the tracked face
	//private long faceTrackingID = 0;
	
	// Animation units
	private Dictionary<KinectInterop.FaceShapeAnimations, float> dictAU = new Dictionary<KinectInterop.FaceShapeAnimations, float>();
	private bool bGotAU = false;

	// Shape units
	private Dictionary<KinectInterop.FaceShapeDeformations, float> dictSU = new Dictionary<KinectInterop.FaceShapeDeformations, float>();
	private bool bGotSU = false;

	// whether the face model mesh was initialized
	private bool bFaceModelMeshInited = false;
	private Vector3[] vMeshVertices = null;

	// Vertices, UV and triangles of the face model
	private Vector3[] avModelVertices = null;
	private Vector2[] avModelUV = null;
	private bool bGotModelVertices = false;
	//private bool bGotModelVerticesFromDC = false;
	private bool bGotModelUV = false;

	private int[] avModelTriangles = null;
	private bool bGotModelTriangles = false;
	private bool bGotModelTrianglesFromDC = false;

	// Head position and rotation
	private Vector3 headPos = Vector3.zero;
	private bool bGotHeadPos = false;

	private Quaternion headRot = Quaternion.identity;
	private bool bGotHeadRot = false;

	// offset vector from head to face center
	private Vector3 faceHeadOffset = Vector3.zero;
	
	// Tracked face rectangle
	private Rect faceRect = new Rect();
	//private bool bGotFaceRect;

	// primary user ID, as reported by KinectManager
	private long primaryUserID = 0;
	private long lastUserID = 0;

	// primary sensor data structure
	private KinectInterop.SensorData sensorData = null;
	
	// Bool to keep track of whether face-tracking system has been initialized
	private bool isFacetrackingInitialized = false;
	private bool wasFacetrackingActive = false;
	
	// The single instance of FacetrackingManager
	private static FacetrackingManager instance;

	// update times
	private float facePosUpdateTime = 0f;
	private float faceMeshUpdateTime = 0f;

	// used when dontUpdateModelMesh is true
	//private bool faceMeshGotOnce = false;

	// whether UpdateFaceModelMesh() is running
	private bool updateFaceMeshStarted = false;

	private Material faceMeshMaterial = null;
	private RenderTexture faceMeshTexture = null;
	private Vector3 nosePos = Vector3.zero;

	/// <summary>
	/// Gets the single FacetrackingManager instance.
	/// </summary>
	/// <value>The FacetrackingManager instance.</value>
    public static FacetrackingManager Instance
    {
        get
        {
            return instance;
        }
    }
	
	/// <summary>
	/// Determines the facetracking system was successfully initialized, false otherwise.
	/// </summary>
	/// <returns><c>true</c> if the facetracking system was successfully initialized; otherwise, <c>false</c>.</returns>
	public bool IsFaceTrackingInitialized()
	{
		return isFacetrackingInitialized;
	}
	
	/// <summary>
	/// Determines whether this the sensor is currently tracking a face.
	/// </summary>
	/// <returns><c>true</c> if the sensor is tracking a face; otherwise, <c>false</c>.</returns>
	public bool IsTrackingFace()
	{
		return isTrackingFace;
	}

	/// <summary>
	/// Gets the current user ID, or 0 if no user is currently tracked.
	/// </summary>
	/// <returns>The face tracking I.</returns>
	public long GetFaceTrackingID()
	{
		return isTrackingFace ? primaryUserID : 0;
	}
	
	/// <summary>
	/// Determines whether the sensor is currently tracking the face of the specified user.
	/// </summary>
	/// <returns><c>true</c> if the sensor is currently tracking the face of the specified user; otherwise, <c>false</c>.</returns>
	/// <param name="userId">User ID</param>
	public bool IsTrackingFace(long userId)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			return sensorData.sensorInterface.IsFaceTracked(userId);
		}

		return false;
	}

	/// <summary>
	/// Gets the last face position & rotation update time, in seconds since game start.
	/// </summary>
	/// <returns>The last face position & rotation update time.</returns>
	public float GetFacePosUpdateTime()
	{
		return facePosUpdateTime;
	}

	/// <summary>
	/// Gets the last face mesh update time, in seconds since game start.
	/// </summary>
	/// <returns>The last face mesh update time.</returns>
	public float GetFaceMeshUpdateTime()
	{
		return faceMeshUpdateTime;
	}
	
	/// <summary>
	/// Gets the head position of the currently tracked user.
	/// </summary>
	/// <returns>The head position.</returns>
	/// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head position.</param>
	public Vector3 GetHeadPosition(bool bMirroredMovement)
	{
		Vector3 vHeadPos = headPos; // bGotHeadPos ? headPos : Vector3.zero;

		if(!bMirroredMovement)
		{
			vHeadPos.z = -vHeadPos.z;
		}
		
		return vHeadPos;
	}
	
	/// <summary>
	/// Gets the head position of the specified user.
	/// </summary>
	/// <returns>The head position.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head position.</param>
	public Vector3 GetHeadPosition(long userId, bool bMirroredMovement)
	{
		Vector3 vHeadPos = Vector3.zero;
		bool bGotPosition = sensorData.sensorInterface.GetHeadPosition(userId, ref vHeadPos);

		if(bGotPosition)
		{
			if(!bMirroredMovement)
			{
				vHeadPos.z = -vHeadPos.z;
			}
			
			return vHeadPos;
		}

		return Vector3.zero;
	}
	
	/// <summary>
	/// Gets the head rotation of the currently tracked user.
	/// </summary>
	/// <returns>The head rotation.</returns>
	/// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head rotation.</param>
	public Quaternion GetHeadRotation(bool bMirroredMovement)
	{
		Vector3 rotAngles = headRot.eulerAngles; // bGotHeadRot ? headRot.eulerAngles : Vector3.zero;

		if(bMirroredMovement)
		{
			rotAngles.x = -rotAngles.x;
			rotAngles.z = -rotAngles.z;
		}
		else
		{
			rotAngles.x = -rotAngles.x;
			rotAngles.y = -rotAngles.y;
		}
		
		return Quaternion.Euler(rotAngles);
	}
	
	/// <summary>
	/// Gets the head rotation of the specified user.
	/// </summary>
	/// <returns>The head rotation.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="bMirroredMovement">If set to <c>true</c> returns mirorred head rotation.</param>
	public Quaternion GetHeadRotation(long userId, bool bMirroredMovement)
	{
		Quaternion vHeadRot = Quaternion.identity;
		bool bGotRotation = sensorData.sensorInterface.GetHeadRotation(userId, ref vHeadRot);

		if(bGotRotation)
		{
			Vector3 rotAngles = vHeadRot.eulerAngles;
			
			if(bMirroredMovement)
			{
				rotAngles.x = -rotAngles.x;
				rotAngles.z = -rotAngles.z;
			}
			else
			{
				rotAngles.x = -rotAngles.x;
				rotAngles.y = -rotAngles.y;
			}
			
			return Quaternion.Euler(rotAngles);
		}

		return Quaternion.identity;
	}

	/// <summary>
	/// Gets the tracked face rectangle of the specified user in color image coordinates, or zero-rect if the user's face is not tracked.
	/// </summary>
	/// <returns>The face rectangle, in color image coordinates.</returns>
	/// <param name="userId">User ID</param>
	public Rect GetFaceColorRect(long userId)
	{
		Rect faceColorRect = new Rect();
		sensorData.sensorInterface.GetFaceRect(userId, ref faceColorRect);

		return faceColorRect;
	}
	
	/// <summary>
	/// Determines whether there are valid anim units.
	/// </summary>
	/// <returns><c>true</c> if there are valid anim units; otherwise, <c>false</c>.</returns>
	public bool IsGotAU()
	{
		return bGotAU;
	}
	
	/// <summary>
	/// Gets the animation unit value at given index, or 0 if the index is invalid.
	/// </summary>
	/// <returns>The animation unit value.</returns>
	/// <param name="faceAnimKey">Face animation unit.</param>
	public float GetAnimUnit(KinectInterop.FaceShapeAnimations faceAnimKey)
	{
		if(dictAU.ContainsKey(faceAnimKey))
		{
			return dictAU[faceAnimKey];
		}
		
		return 0.0f;
	}
	
	/// <summary>
	/// Gets all animation units for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="dictAnimUnits">Animation units dictionary, to get the results.</param>
	public bool GetUserAnimUnits(long userId, ref Dictionary<KinectInterop.FaceShapeAnimations, float> dictAnimUnits)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetAnimUnits(userId, ref dictAnimUnits);
			return bGotIt;
		}

		return false;
	}
	
	/// <summary>
	/// Determines whether there are valid shape units.
	/// </summary>
	/// <returns><c>true</c> if there are valid shape units; otherwise, <c>false</c>.</returns>
	public bool IsGotSU()
	{
		return bGotSU;
	}
	
	/// <summary>
	/// Gets the shape unit value at given index, or 0 if the index is invalid.
	/// </summary>
	/// <returns>The shape unit value.</returns>
	/// <param name="faceShapeKey">Face shape unit.</param>
	public float GetShapeUnit(KinectInterop.FaceShapeDeformations faceShapeKey)
	{
		if(dictSU.ContainsKey(faceShapeKey))
		{
			return dictSU[faceShapeKey];
		}
		
		return 0.0f;
	}
	
	/// <summary>
	/// Gets all animation units for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="dictShapeUnits">Shape units dictionary, to get the results.</param>
	public bool GetUserShapeUnits(long userId, ref Dictionary<KinectInterop.FaceShapeDeformations, float> dictShapeUnits)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetShapeUnits(userId, ref dictShapeUnits);
			return bGotIt;
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the count of face model vertices.
	/// </summary>
	/// <returns>The count of face model vertices.</returns>
	public int GetFaceModelVertexCount()
	{
		if (avModelVertices != null) 
		{
			return avModelVertices.Length;
		} 

		return 0;
	}

	/// <summary>
	/// Gets the face model vertex, if a face model is available and the index is in range; Vector3.zero otherwise.
	/// </summary>
	/// <returns>The face model vertex.</returns>
	/// <param name="index">Vertex index, or Vector3.zero</param>
	public Vector3 GetFaceModelVertex(int index)
	{
		if (avModelVertices != null) 
		{
			if(index >= 0 && index < avModelVertices.Length)
			{
				return avModelVertices[index];
			}
		}
		
		return Vector3.zero;
	}
	
	/// <summary>
	/// Gets all face model vertices, if a face model is available; null otherwise.
	/// </summary>
	/// <returns>The face model vertices, or null.</returns>
	public Vector3[] GetFaceModelVertices()
	{
		return avModelVertices;
	}

	/// <summary>
	/// Gets the count of face model vertices for the specified user
	/// </summary>
	/// <returns>The count of face model vertices.</returns>
	/// <param name="userId">User ID</param>
	public int GetUserFaceVertexCount(long userId)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			int iVertCount = sensorData.sensorInterface.GetFaceModelVerticesCount(userId);
			return iVertCount;
		}

		return 0;
	}

	/// <summary>
	/// Gets all face model vertices for the specified user.
	/// </summary>
	/// <returns><c>true</c>, if the user's face is tracked, <c>false</c> otherwise.</returns>
	/// <param name="userId">User ID</param>
	/// <param name="avVertices">Reference to array of vertices, to get the result.</param>
	public bool GetUserFaceVertices(long userId, ref Vector3[] avVertices)
	{
		if(sensorData != null && sensorData.sensorInterface != null)
		{
			bool bGotIt = sensorData.sensorInterface.GetFaceModelVertices(userId, ref avVertices);
			return bGotIt;
		}
		
		return false;
	}
	
	/// <summary>
	/// Gets the count of face model triangles.
	/// </summary>
	/// <returns>The count of face model triangles.</returns>
	public int GetFaceModelTriangleCount()
	{
		if (avModelTriangles != null) 
		{
			return avModelTriangles.Length;
		}

		return 0;
	}

	/// <summary>
	/// Gets the face model triangle indices, if a face model is available; null otherwise.
	/// </summary>
	/// <returns>The face model triangle indices, or null.</returns>
	/// <param name="bMirroredModel">If set to <c>true</c> gets mirorred model indices.</param>
	public int[] GetFaceModelTriangleIndices(bool bMirroredModel)
	{
		if (avModelTriangles != null) 
		{
			return avModelTriangles;
		}

		return null;
	}

	/// <summary>
	/// Gets the face model UV-array, if it is available; null otherwise
	/// </summary>
	/// <returns>The face model UV-array, or null.</returns>
	public Vector2[] GetFaceModelUV()
	{
		if (bGotModelUV) 
		{
			return avModelUV;
		}

		return null;
	}

	/// <summary>
	/// Resets the face model UV-array. This is to request new UV-array estimation, when the 'Textured model mesh' is set to FaceRectangle.
	/// </summary>
	public void ResetFaceModelUV()
	{
		bGotModelUV = false;
	}


	//----------------------------------- end of public functions --------------------------------------//

	void Awake()
	{
		instance = this;
	}

	void Start() 
	{
		try 
		{
			// get sensor data
			KinectManager kinectManager = KinectManager.Instance;
			if(kinectManager && kinectManager.IsInitialized())
			{
				sensorData = kinectManager.GetSensorData();
			}

			if(sensorData == null || sensorData.sensorInterface == null)
			{
				throw new Exception("Face tracking cannot be started, because KinectManager is missing or not initialized.");
			}

			if(debugText != null)
			{
				debugText.text = "Please, wait...";
			}
			
			// ensure the needed dlls are in place and face tracking is available for this interface
			bool bNeedRestart = false;
			if(sensorData.sensorInterface.IsFaceTrackingAvailable(ref bNeedRestart))
			{
				if(bNeedRestart)
				{
					KinectInterop.RestartLevel(gameObject, "FM");
					return;
				}
			}
			else
			{
				string sInterfaceName = sensorData.sensorInterface.GetType().Name;
				throw new Exception(sInterfaceName + ": Face tracking is not supported!");
			}

			// Initialize the face tracker
			wasFacetrackingActive = sensorData.sensorInterface.IsFaceTrackingActive();
			if(!wasFacetrackingActive)
			{
				if (!sensorData.sensorInterface.InitFaceTracking(getFaceModelData, displayFaceRect))
				{
					throw new Exception("Face tracking could not be initialized.");
				}
			}

			isFacetrackingInitialized = true;

			//DontDestroyOnLoad(gameObject);

			if(debugText != null)
			{
				debugText.text = "Ready.";
			}
		} 
		catch(DllNotFoundException ex)
		{
			Debug.LogError(ex.ToString());
			if(debugText != null)
				debugText.text = "Please check the Kinect and FT-Library installations.";
		}
		catch (Exception ex) 
		{
			Debug.LogError(ex.ToString());
			if(debugText != null)
				debugText.text = ex.Message;
		}
	}

	void OnDestroy()
	{
		if(isFacetrackingInitialized && !wasFacetrackingActive && sensorData != null && sensorData.sensorInterface != null)
		{
			// finish face tracking
			sensorData.sensorInterface.FinishFaceTracking();
		}

		if (faceMeshTexture != null) 
		{
			faceMeshTexture.Release();
			faceMeshTexture = null;
		}

//		// clean up
//		Resources.UnloadUnusedAssets();
//		GC.Collect();
		
		isFacetrackingInitialized = false;
		instance = null;
	}
	
	void Update() 
	{
		if(isFacetrackingInitialized)
		{
			KinectManager kinectManager = KinectManager.Instance;
			if(kinectManager && kinectManager.IsInitialized())
			{
				lastUserID = primaryUserID;
				primaryUserID = kinectManager.GetUserIdByIndex(playerIndex);

				if (primaryUserID != lastUserID && primaryUserID != 0) 
				{
					//faceMeshGotOnce = false;
				}
			}

			// update the face tracker
			isTrackingFace = false;

			bool bFacetrackingUpdated = !wasFacetrackingActive ? sensorData.sensorInterface.UpdateFaceTracking() : true;
			if(bFacetrackingUpdated)
			{
				// estimate the tracking state
				isTrackingFace = sensorData.sensorInterface.IsFaceTracked(primaryUserID);

				if(!isTrackingFace && (Time.realtimeSinceStartup - lastFaceTrackedTime) <= faceTrackingTolerance)
				{
					// allow tolerance in tracking
					isTrackingFace = true;
				}

				// get the facetracking parameters
				if(isTrackingFace)
				{
					lastFaceTrackedTime = Time.realtimeSinceStartup;
					facePosUpdateTime = Time.time;
					
					// get face rectangle
					/**bGotFaceRect =*/ sensorData.sensorInterface.GetFaceRect(primaryUserID, ref faceRect);
					
					// get head position
					bGotHeadPos = sensorData.sensorInterface.GetHeadPosition(primaryUserID, ref headPos);

					// get head rotation
					bGotHeadRot = sensorData.sensorInterface.GetHeadRotation(primaryUserID, ref headRot);

					// get the animation units
					bGotAU = sensorData.sensorInterface.GetAnimUnits(primaryUserID, ref dictAU);

					// get the shape units
					bGotSU = sensorData.sensorInterface.GetShapeUnits(primaryUserID, ref dictSU);

					//if(faceModelMesh != null && faceModelMesh.activeInHierarchy)
					{
						// apply model vertices to the mesh
						if(!bFaceModelMeshInited)
						{
							bFaceModelMeshInited = CreateFaceModelMesh();
						}
					}
					
					if (getFaceModelData && bFaceModelMeshInited && primaryUserID != 0) 
					{
						if (!pauseModelMeshUpdates && !updateFaceMeshStarted)
						{
							StartCoroutine(UpdateFaceModelMesh());
						}
					} 
				}
			}

//			// set mesh activity flag
//			bool bFaceMeshActive = isTrackingFace && primaryUserID != 0;
//			if(faceModelMesh != null && bFaceModelMeshInited && faceModelMesh.activeSelf != bFaceMeshActive)
//			{
//				faceModelMesh.SetActive(bFaceMeshActive);
//			}
		}
	}
	
	void OnGUI()
	{
		if(isFacetrackingInitialized)
		{
			if(debugText != null)
			{
				if(isTrackingFace)
				{
					debugText.text = "BodyID: " + primaryUserID;
				}
				else
				{
					debugText.text = "Not tracking...";
				}
			}
		}
	}


	protected bool CreateFaceModelMesh()
	{
//		if(faceModelMesh == null)
//			return false;

		if (avModelVertices == null /**&& !bGotModelVerticesFromDC*/) 
		{
			int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(0);
			if(iNumVertices <= 0)
				return false;

			avModelVertices = new Vector3[iNumVertices];
			bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(0, ref avModelVertices);

			avModelUV = new Vector2[iNumVertices];
			bGotModelUV = false;

			if(!bGotModelVertices)
				return false;
		}

		// estimate face mesh vertices with respect to the head joint
		Vector3[] vMeshVertices = new Vector3[avModelVertices.Length];

		//if (!bGotModelVerticesFromDC) 
		{
			Vector3 vFaceCenter = Vector3.zero;
			for (int i = 0; i < avModelVertices.Length; i++) 
			{
				vFaceCenter += avModelVertices[i];
			}

			vFaceCenter /= (float)avModelVertices.Length;

			faceHeadOffset = Vector3.zero;
			if (vFaceCenter.sqrMagnitude >= 1f) 
			{
				Vector3 vHeadToFace = (vFaceCenter - headPos);

				faceHeadOffset = Quaternion.Inverse(headRot) * vHeadToFace;
				faceHeadOffset.y += verticalMeshOffset;
			}

			vFaceCenter -= headRot * faceHeadOffset;

			for(int i = 0; i < avModelVertices.Length; i++)
			{
				//avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
				//avModelVertices[i] -= vFaceCenter;

				vMeshVertices[i] = avModelVertices[i] - vFaceCenter;
			}
		}

		if (avModelTriangles == null && !bGotModelTrianglesFromDC) 
		{
			int iNumTriangles = sensorData.sensorInterface.GetFaceModelTrianglesCount();
			if(iNumTriangles <= 0)
				return false;

			avModelTriangles = new int[iNumTriangles];
			bGotModelTriangles = sensorData.sensorInterface.GetFaceModelTriangles(mirroredModelMesh, ref avModelTriangles);

			if(!bGotModelTriangles)
				return false;
		}

		if (!faceMeshMaterial && faceModelMesh) 
		{
			faceMeshMaterial = faceModelMesh.GetComponent<MeshRenderer>().material;

			if (faceMeshMaterial && faceMeshMaterial.mainTexture) 
			{
				faceMeshMaterial.mainTexture.wrapMode = TextureWrapMode.Clamp;  // TextureWrapMode.Repeat; // 
			}
		}

		if (faceModelMesh) 
		{
			Mesh mesh = new Mesh();
			mesh.name = "FaceMesh";
			faceModelMesh.GetComponent<MeshFilter>().mesh = mesh;

			mesh.vertices = vMeshVertices; // avModelVertices;
			//mesh.uv = avModelUV;

			mesh.triangles = avModelTriangles;
			mesh.RecalculateNormals();

//			if (moveModelMesh) 
//			{
//				faceModelMesh.transform.position = headPos;
//				//faceModelMesh.transform.rotation = faceModelRot;
//			}

			SetFaceModelMeshTexture();
		}

		//bFaceModelMeshInited = true;
		return true;
	}

	// sets the proper face mesh texture
	protected void SetFaceModelMeshTexture()
	{
		if (texturedModelMesh == TextureType.ColorMap) 
		{
			KinectManager kinectManager = KinectManager.Instance;
			Texture texColorMap = kinectManager ? kinectManager.GetUsersClrTex() : null;

			if (!faceMeshTexture && kinectManager && texColorMap) 
			{
				faceMeshTexture = new RenderTexture (texColorMap.width, texColorMap.height, 0);
				faceMeshMaterial.mainTexture = faceMeshTexture;  // kinectManager.GetUsersClrTex();
			}

			if (faceMeshTexture && texColorMap) 
			{
				// update the color texture
				Graphics.Blit(texColorMap, faceMeshTexture);
			}
		}
		else if (texturedModelMesh == TextureType.FaceRectangle) 
		{
//			if (faceMeshTexture != null) 
//			{
//				faceMeshTexture.Release();
//				faceMeshTexture = null;
//			}
		}
		else if(texturedModelMesh == TextureType.None)
		{
			if (faceMeshMaterial.mainTexture != null) 
			{
				faceMeshMaterial.mainTexture = null;
			}

			if (faceMeshTexture != null) 
			{
				faceMeshTexture.Release();
				faceMeshTexture = null;
			}
		}
	}


	protected IEnumerator UpdateFaceModelMesh()
	{
		updateFaceMeshStarted = true;

		//if (!dontUpdateModelMesh || !faceMeshGotOnce /**&& !bGotModelVerticesFromDC*/) 
		{
			// init the vertices array if needed
			if(avModelVertices == null)
			{
				int iNumVertices = sensorData.sensorInterface.GetFaceModelVerticesCount(primaryUserID);
				avModelVertices = new Vector3[iNumVertices];
			}

			// get face model vertices
			bGotModelVertices = sensorData.sensorInterface.GetFaceModelVertices(primaryUserID, ref avModelVertices);
		}

		if(bGotModelVertices && faceModelMesh != null)
		{
			//Quaternion faceModelRot = faceModelMesh.transform.rotation;
			//faceModelMesh.transform.rotation = Quaternion.identity;

			bool bFaceMeshUpdated = false;
			//if (!dontUpdateModelMesh || !faceMeshGotOnce) 
			{
				AsyncTask<bool> task = new AsyncTask<bool>(() => {
					// estimate face mesh vertices with respect to the head joint
					vMeshVertices = null;

					KinectManager kinectManager = KinectManager.Instance;
					Matrix4x4 kinectToWorld = kinectManager ? kinectManager.GetKinectToWorldMatrix() : Matrix4x4.identity;
					Vector3 headPosWorld = kinectToWorld.MultiplyPoint3x4(headPos);
						
					Vector3 lastNosePos = nosePos;
					//if (!bGotModelVerticesFromDC) 
					{
//						Vector3 vFaceCenter = Vector3.zero;
//						for (int i = 0; i < avModelVertices.Length; i++) 
//						{
//							vFaceCenter += avModelVertices[i];
//						}
//
//						vFaceCenter /= (float)avModelVertices.Length;
//
//						Vector3 vHeadToFace = (vFaceCenter - headPos);
//						if (vHeadToFace.sqrMagnitude < 0.015f) // max 0.12 x 0.12
//						{
//							faceHeadOffset = Quaternion.Inverse(headRot) * vHeadToFace;
//							faceHeadOffset.y += verticalMeshOffset;
//						}

						nosePos = GetFaceModelNosePos();
						Vector3 vHeadToNose = Quaternion.Inverse(headRot) * (nosePos - headPos);
						float headToNoseLen = vHeadToNose.magnitude;

//						string sHeadToNose = string.Format("({0:F2}, {0:F2}, {0:F2})", vHeadToNose.x, vHeadToNose.y, vHeadToNose.z);
//						Debug.Log("U-Face nosePos: " + nosePos + ", headPos: " + headPos + "\noffset: " + sHeadToNose + ", len: " + headToNoseLen);

						if(headToNoseLen >= 0.08f && headToNoseLen <= 0.18f)
						{
							//vFaceCenter -= headRot * faceHeadOffset;

							vMeshVertices = new Vector3[avModelVertices.Length];
							for(int i = 0; i < avModelVertices.Length; i++)
							{
								//avModelVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld;
								//avModelVertices[i] -= vFaceCenter;

								//vMeshVertices[i] = avModelVertices[i] - vFaceCenter;
								vMeshVertices[i] = kinectToWorld.MultiplyPoint3x4(avModelVertices[i]) - headPosWorld; // avModelVertices[i] - headPos;
							}
						}	
					}

					if(vMeshVertices == null || lastNosePos == nosePos)
					{
						return false;
					}

					//if (!bGotModelVerticesFromDC) 
					{
						if(texturedModelMesh != TextureType.None)
						{
							float colorWidth = (float)kinectManager.GetColorImageWidth();
							float colorHeight = (float)kinectManager.GetColorImageHeight();

							//bool bGotFaceRect = sensorData.sensorInterface.GetFaceRect(userId, ref faceRect);
							bool faceRectValid = /**bGotFaceRect &&*/ faceRect.width > 0 && faceRect.height > 0;
							int lastValidUVIndex = -1;  // new code by Andrew Stern

							for(int i = 0; i < avModelVertices.Length; i++)
							{
								Vector2 posDepth = Vector2.zero;
								if(texturedModelMesh == TextureType.ColorMap || !bGotModelUV)
								{
									posDepth = kinectManager.MapSpacePointToDepthCoords(avModelVertices[i]);
								}

								bool bUvSet = false;
								if(posDepth != Vector2.zero)
								{
									ushort depth = kinectManager.GetDepthForPixel((int)posDepth.x, (int)posDepth.y);
									Vector2 posColor = kinectManager.MapDepthPointToColorCoords(posDepth, depth);

									if(posColor != Vector2.zero && !float.IsInfinity(posColor.x) && !float.IsInfinity(posColor.y))
									{
										if(texturedModelMesh == TextureType.ColorMap)
										{
											avModelUV[i] = new Vector2(posColor.x / colorWidth, posColor.y / colorHeight);
											lastValidUVIndex = i;   // new code by Andrew Stern
											bUvSet = true;
										}
										else if(texturedModelMesh == TextureType.FaceRectangle && faceRectValid)
										{
											if(!bGotModelUV)
											{
												avModelUV[i] = new Vector2(/**Mathf.Clamp01*/((posColor.x - faceRect.x) / faceRect.width), 
													/**Mathf.Clamp01*/(1f - (posColor.y - faceRect.y) / faceRect.height));
												lastValidUVIndex = i;   // new code by Andrew Stern
											}

											bUvSet = true;
										}
									}
								}

								if(texturedModelMesh == TextureType.ColorMap && !bUvSet)
								{
									if (lastValidUVIndex >= 0) // new code by Andrew Stern
									{
										avModelUV[i] = new Vector2(avModelUV[lastValidUVIndex].x, avModelUV[lastValidUVIndex].y);
									}
									else
									{
										// original code
										avModelUV[i] = Vector2.zero;
									}
								}
							}

							if(lastValidUVIndex >= 0)  // check for valid run
								bGotModelUV = true;
						}
					}

					return true;
				});

				task.Start();

				while (task.State == AsyncTaskState.Running)
				{
					yield return null;
				}

//				// show nose & head positions
//				Matrix4x4 kinectToWorld2 = KinectManager.Instance.GetKinectToWorldMatrix();
//				if (noseTransform)
//					noseTransform.position = kinectToWorld2.MultiplyPoint3x4(nosePos);
//				if(headTransform)
//					headTransform.position = kinectToWorld2.MultiplyPoint3x4(headPos);
//
//				Vector3 vHeadToNose2 = Quaternion.Inverse(headRot) * (nosePos - headPos);
//				string sHeadToNose2 = string.Format("({0:F2}, {0:F2}, {0:F2})", vHeadToNose2.x, vHeadToNose2.y, vHeadToNose2.z);
//				if(debugText2)
//					debugText2.text = "h2n: " + sHeadToNose2 + ", len: " + vHeadToNose2.magnitude;

				bFaceMeshUpdated = task.Result;
				if(bFaceMeshUpdated) 
				{
					Mesh mesh = faceModelMesh.GetComponent<MeshFilter>().mesh;
					mesh.vertices = vMeshVertices; // avModelVertices;
					vMeshVertices = null;

					if(texturedModelMesh != TextureType.None && avModelUV != null)
					{
						mesh.uv = avModelUV;
					}

					faceMeshUpdateTime = Time.time;
					//faceMeshGotOnce = true;

					mesh.RecalculateNormals();
					mesh.RecalculateBounds();

					// set the face mesh texture
					SetFaceModelMeshTexture();
				}
			}

			if (moveModelMesh) 
			{
				KinectManager kinectManager = KinectManager.Instance;
				Matrix4x4 kinectToWorld = kinectManager ? kinectManager.GetKinectToWorldMatrix() : Matrix4x4.identity;
				Vector3 newHeadPos = kinectToWorld.MultiplyPoint3x4(headPos);

				// check for head pos overlay
				if(foregroundCamera)
				{
					// get the background rectangle (use the portrait background, if available)
					Rect backgroundRect = foregroundCamera.pixelRect;
					PortraitBackground portraitBack = PortraitBackground.Instance;

					if(portraitBack && portraitBack.enabled)
					{
						backgroundRect = portraitBack.GetBackgroundRect();
					}

					if(kinectManager)
					{
						Vector3 posColorOverlay = kinectManager.GetJointPosColorOverlay(primaryUserID, (int)KinectInterop.JointType.Head, foregroundCamera, backgroundRect);

						if(posColorOverlay != Vector3.zero)
						{
							newHeadPos = posColorOverlay;
						}
					}
				}

				faceModelMesh.transform.position = newHeadPos; // Vector3.Lerp(faceModelMesh.transform.position, newHeadPos, 20f * Time.deltaTime);
				//faceModelMesh.transform.rotation = faceModelRot;
			}

			// don't rotate the transform - mesh follows the head rotation
			if (faceModelMesh.transform.rotation != Quaternion.identity) 
			{
				faceModelMesh.transform.rotation = Quaternion.identity;
			}

			// apply scale factor
			if(faceModelMesh.transform.localScale.x != modelMeshScale)
			{
				faceModelMesh.transform.localScale = new Vector3(modelMeshScale, modelMeshScale, modelMeshScale);
			}

			if(!faceModelMesh.activeSelf)
			{
				faceModelMesh.SetActive(true);
			}
		}
		else
		{
			if(faceModelMesh && faceModelMesh.activeSelf)
			{
				faceModelMesh.SetActive(false);
			}
		}

		updateFaceMeshStarted = false;
	}

	// returns the nose tip position, or Vector3.zero if not found
	private Vector3 GetFaceModelNosePos()
	{
		if (avModelVertices != null) 
		{
			int iNoseIndex = -1;
			if (sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv2 ||
			    sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectUWPv2 ||
			    sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.DummyK2) 
			{
				iNoseIndex = 18; // Microsoft.Kinect.Face.HighDetailFacePoints.NoseTip
			} 
			else if (sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.KinectSDKv1 ||
			        sensorData.sensorIntPlatform == KinectInterop.DepthSensorPlatform.DummyK1) 
			{
				iNoseIndex = 89; // 
			}

			if (iNoseIndex >= 0 && iNoseIndex < avModelVertices.Length) 
			{
				return avModelVertices[iNoseIndex];
			}
		}

		return Vector3.zero;
	}

	// gets face basic parameters as csv line
	public string GetFaceParamsAsCsv()
	{
		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		const char delimiter = ',';

		if (bGotHeadPos || bGotHeadRot)
		{
			sbBuf.Append("fp").Append(delimiter);

			// head pos
			sbBuf.Append (bGotHeadPos ? "1" : "0").Append(delimiter);

			if (bGotHeadPos) 
			{
				sbBuf.AppendFormat ("{0:F3}", headPos.x).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", headPos.y).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", headPos.z).Append (delimiter);
			}

			// head rot
			sbBuf.Append (bGotHeadRot ? "1" : "0").Append(delimiter);
			Vector3 vheadRot = headRot.eulerAngles;

			if (bGotHeadRot) 
			{
				sbBuf.AppendFormat ("{0:F3}", vheadRot.x).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", vheadRot.y).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", vheadRot.z).Append (delimiter);
			}

			// face rect
			sbBuf.Append ("1").Append(delimiter);  
			sbBuf.AppendFormat ("{0:F0}", faceRect.x).Append (delimiter);
			sbBuf.AppendFormat ("{0:F0}", faceRect.y).Append (delimiter);
			sbBuf.AppendFormat ("{0:F0}", faceRect.width).Append (delimiter);
			sbBuf.AppendFormat ("{0:F0}", faceRect.height).Append (delimiter);

			// animation units
			sbBuf.Append (bGotAU ? "1" : "0").Append(delimiter);

			if (bGotAU) 
			{
				int enumCount = Enum.GetNames (typeof(KinectInterop.FaceShapeAnimations)).Length;
				sbBuf.Append (enumCount).Append(delimiter);

				for (int i = 0; i < enumCount; i++) 
				{
					float dictValue = dictAU [(KinectInterop.FaceShapeAnimations)i];
					sbBuf.AppendFormat ("{0:F3}", dictValue).Append (delimiter);
				}
			}

			// shape units
			sbBuf.Append (bGotSU ? "1" : "0").Append(delimiter);

			if (bGotSU) 
			{
				int enumCount = Enum.GetNames (typeof(KinectInterop.FaceShapeDeformations)).Length;
				sbBuf.Append (enumCount).Append(delimiter);

				for (int i = 0; i < enumCount; i++) 
				{
					float dictValue = dictSU [(KinectInterop.FaceShapeDeformations)i];
					sbBuf.AppendFormat ("{0:F3}", dictValue).Append (delimiter);
				}
			}

			// any other parameters...
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets basic face parameters from a csv line
	public bool SetFaceParamsFromCsv(string sCsvLine)
	{
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 1 || alCsvParts[0] != "fp")
			return false;

		int iIndex = 1;
		int iLength = alCsvParts.Length;

		if (iLength < (iIndex + 1))
			return false;

		// head pos
		bGotHeadPos = (alCsvParts[iIndex] == "1");
		iIndex++;

		if (bGotHeadPos && iLength >= (iIndex + 3)) 
		{
			float x = 0f, y = 0f, z = 0f;

			float.TryParse(alCsvParts[iIndex], out x);
			float.TryParse(alCsvParts[iIndex + 1], out y);
			float.TryParse(alCsvParts[iIndex + 2], out z);
			iIndex += 3;

			headPos = new Vector3(x, y, z);
		}

		// head rot
		bGotHeadRot = (alCsvParts[iIndex] == "1");
		iIndex++;

		if (bGotHeadRot && iLength >= (iIndex + 3)) 
		{
			float x = 0f, y = 0f, z = 0f;

			float.TryParse(alCsvParts[iIndex], out x);
			float.TryParse(alCsvParts[iIndex + 1], out y);
			float.TryParse(alCsvParts[iIndex + 2], out z);
			iIndex += 3;

			headRot = Quaternion.Euler(x, y, z);
		}

		// face rect
		bool bGotFaceRect = (alCsvParts[iIndex] == "1");
		iIndex++;

		if (bGotFaceRect && iLength >= (iIndex + 4)) 
		{
			float x = 0f, y = 0f, w = 0f, h = 0f;

			float.TryParse(alCsvParts[iIndex], out x);
			float.TryParse(alCsvParts[iIndex + 1], out y);
			float.TryParse(alCsvParts[iIndex + 2], out w);
			float.TryParse(alCsvParts[iIndex + 3], out h);
			iIndex += 4;

			faceRect.x = x; faceRect.y = y;
			faceRect.width = w; faceRect.height = h;
		}

		// animation units
		bGotAU = (alCsvParts[iIndex] == "1");
		iIndex++;

		if (bGotAU && iLength >= (iIndex + 1)) 
		{
			int count = 0;
			int.TryParse(alCsvParts[iIndex], out count);
			iIndex++;

			for (int i = 0; i < count && iLength >= (iIndex + 1); i++) 
			{
				float v = 0;
				float.TryParse(alCsvParts[iIndex], out v);
				iIndex++;

				dictAU [(KinectInterop.FaceShapeAnimations)i] = v;
			}
		}

		// shape units
		bGotSU = (alCsvParts[iIndex] == "1");
		iIndex++;

		if (bGotSU && iLength >= (iIndex + 1)) 
		{
			int count = 0;
			int.TryParse(alCsvParts[iIndex], out count);
			iIndex++;

			for (int i = 0; i < count && iLength >= (iIndex + 1); i++) 
			{
				float v = 0;
				float.TryParse(alCsvParts[iIndex], out v);
				iIndex++;

				dictSU [(KinectInterop.FaceShapeDeformations)i] = v;
			}
		}

		// any other parameters here...

		// emulate face tracking
		lastFaceTrackedTime = Time.realtimeSinceStartup;
		facePosUpdateTime = Time.time;

		return true;
	}

	// gets face model vertices as csv line
	public string GetFaceVerticesAsCsv()
	{
		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		const char delimiter = ',';

		if (bGotModelVertices && avModelVertices != null)
		{
			sbBuf.Append("fv").Append(delimiter);

			// model vertices
			int vertCount = avModelVertices.Length;
			sbBuf.Append (vertCount).Append(delimiter);

			for (int i = 0; i < vertCount; i++) 
			{
				sbBuf.AppendFormat ("{0:F3}", avModelVertices[i].x).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", avModelVertices[i].y).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", avModelVertices[i].z).Append (delimiter);
			}
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets face model vertices from a csv line
	public bool SetFaceVerticesFromCsv(string sCsvLine)
	{
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 1 || alCsvParts[0] != "fv")
			return false;

		int iIndex = 1;
		int iLength = alCsvParts.Length;

		if (iLength < (iIndex + 1))
			return false;

		// model vertices
		int vertCount = 0;
		int.TryParse(alCsvParts[iIndex], out vertCount);
		iIndex++;

		if (vertCount > 0) 
		{
			if (avModelVertices == null || avModelVertices.Length != vertCount) 
			{
				avModelVertices = new Vector3[vertCount];
			}

			for (int i = 0; i < vertCount && iLength >= (iIndex + 3); i++) 
			{
				float x = 0f, y = 0f, z = 0f;

				float.TryParse(alCsvParts[iIndex], out x);
				float.TryParse(alCsvParts[iIndex + 1], out y);
				float.TryParse(alCsvParts[iIndex + 2], out z);
				iIndex += 3;

				avModelVertices[i] = new Vector3(x, y, z);
			}

			bGotModelVertices = true;
			//bGotModelVerticesFromDC = true;
		}

		faceMeshUpdateTime = Time.time;

		return true;
	}

	// gets face model UVs as csv line
	public string GetFaceUvsAsCsv()
	{
		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		const char delimiter = ',';

		if (bGotModelVertices && avModelUV != null)
		{
			sbBuf.Append("fu").Append(delimiter);

			// face rect width & height
			sbBuf.AppendFormat ("{0:F0}", faceRect.width).Append (delimiter);
			sbBuf.AppendFormat ("{0:F0}", faceRect.height).Append (delimiter);

			// model UVs
			int uvCount = avModelUV.Length;
			sbBuf.Append (uvCount).Append(delimiter);

			for (int i = 0; i < uvCount; i++) 
			{
				sbBuf.AppendFormat ("{0:F3}", avModelUV[i].x).Append (delimiter);
				sbBuf.AppendFormat ("{0:F3}", avModelUV[i].y).Append (delimiter);
			}
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets face model UVs from a csv line
	public bool SetFaceUvsFromCsv(string sCsvLine)
	{
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 1 || alCsvParts[0] != "fu")
			return false;

		int iIndex = 1;
		int iLength = alCsvParts.Length;

		if (iLength < (iIndex + 2))
			return false;

		// face width & height
		float w = 0f, h = 0f;

		float.TryParse(alCsvParts[iIndex], out w);
		float.TryParse(alCsvParts[iIndex + 1], out h);
		iIndex += 2;

		faceRect.width = w; faceRect.height = h;

		// model UVs
		int uvCount = 0;
		if (iLength >= (iIndex + 1)) 
		{
			int.TryParse(alCsvParts[iIndex], out uvCount);
			iIndex++;
		}

		if (uvCount > 0) 
		{
			if (avModelUV == null || avModelUV.Length != uvCount) 
			{
				avModelUV = new Vector2[uvCount];
			}

			for (int i = 0; i < uvCount && iLength >= (iIndex + 2); i++) 
			{
				float x = 0f, y = 0f;

				float.TryParse(alCsvParts[iIndex], out x);
				float.TryParse(alCsvParts[iIndex + 1], out y);
				iIndex += 2;

				avModelUV[i] = new Vector2(x, y);
			}

			bGotModelUV = true;
		}

		return true;
	}

	// gets face model triangles as csv line
	public string GetFaceTrianglesAsCsv()
	{
		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		const char delimiter = ',';

		if (avModelTriangles != null)
		{
			sbBuf.Append("ft").Append(delimiter);

			// model triangles
			int triCount = avModelTriangles.Length;
			sbBuf.Append (triCount).Append(delimiter);

			for (int i = 0; i < triCount; i++) 
			{
				sbBuf.Append(avModelTriangles[i]).Append (delimiter);
			}
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets face model model from a csv line
	public bool SetFaceTrianglesFromCsv(string sCsvLine)
	{
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 1 || alCsvParts[0] != "ft")
			return false;

		int iIndex = 1;
		int iLength = alCsvParts.Length;

		if (iLength < (iIndex + 1))
			return false;

		// model triangles
		int triCount = 0;
		int.TryParse(alCsvParts[iIndex], out triCount);
		iIndex++;

		if (triCount > 0) 
		{
			if (avModelTriangles == null || avModelTriangles.Length != triCount) 
			{
				avModelTriangles = new int[triCount];
			}

			for (int i = 0; i < triCount && iLength >= (iIndex + 1); i++) 
			{
				int v = 0;

				int.TryParse(alCsvParts[iIndex], out v);
				iIndex++;

				avModelTriangles[i] = v;
			}

			bGotModelTriangles = true;
			bGotModelTrianglesFromDC = true;
		}

		return true;
	}


}
