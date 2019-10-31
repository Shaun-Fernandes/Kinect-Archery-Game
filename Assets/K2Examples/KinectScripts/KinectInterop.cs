using UnityEngine;
//using Windows.Kinect;

using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System;
using System.IO;
using System.Text;
#if (UNITY_STANDALONE_WIN)
using ICSharpCode.SharpZipLib.Zip;
#endif
//using OpenCvSharp;
using UnityEngine.SceneManagement;


/// <summary>
/// KinectInterop is a class containing utility and interop functions, that call the proper sensor interface.
/// </summary>
public class KinectInterop
{
	// order of depth sensor interfaces
//	public static Type[] SensorInterfaceOrder = new Type[] { 
//		typeof(Kinect2Interface), typeof(Kinect1Interface), typeof(OpenNI2Interface)
//	};
	public static DepthSensorInterface[] SensorInterfaceOrder = new DepthSensorInterface[] { 
#if (UNITY_WSA_10_0 && NETFX_CORE)
		new Kinect2UwpInterface()
#elif (UNITY_STANDALONE_WIN)
		new Kinect2Interface(), new Kinect1Interface(), new OrbbecAstraInterface(), new DummyK2Interface()
#else
		new DummyK2Interface()
#endif
	};

	// graphics shader level
	private static int graphicsShaderLevel = 0;

	
	/// <summary>
	/// Constants used by this class and other K2-components
	/// </summary>
	public static class Constants
	{
		public const int MaxBodyCount = 6;
		public const int MaxJointCount = 25;

		public const float MinTimeBetweenSameGestures = 0.0f;
		public const float PoseCompleteDuration = 1.0f;
		public const float ClickMaxDistance = 0.05f;
		public const float ClickStayDuration = 2.0f;
	}

	// Types of depth sensor platforms
	public enum DepthSensorPlatform : int
	{
		None = 0,
		KinectSDKv1 = 1,
		KinectSDKv2 = 2,
		OpenNIv2 = 3,
		RealSense = 4,
		KinectUWPv2 = 5,
		OrbbecAstra = 5,

		DummyK1 = 101,
		DummyK2 = 102
	}
	
	// Data structures for interfacing C# with the native wrappers

    [Flags]
    public enum FrameSource : uint
    {
		TypeNone = 0x0,
        TypeColor = 0x1,
        TypeInfrared = 0x2,
        TypeDepth = 0x8,
        TypeBodyIndex = 0x10,
        TypeBody = 0x20,
        TypeAudio = 0x40
    }
	
    public enum JointType : int
    {
		SpineBase = 0,
		SpineMid = 1,
        Neck = 2,
        Head = 3,
        ShoulderLeft = 4,
        ElbowLeft = 5,
        WristLeft = 6,
        HandLeft = 7,
        ShoulderRight = 8,
        ElbowRight = 9,
        WristRight = 10,
        HandRight = 11,
        HipLeft = 12,
        KneeLeft = 13,
        AnkleLeft = 14,
        FootLeft = 15,
        HipRight = 16,
        KneeRight = 17,
        AnkleRight = 18,
        FootRight = 19,
        SpineShoulder = 20,
        HandTipLeft = 21,
        ThumbLeft = 22,
        HandTipRight = 23,
        ThumbRight = 24
		//Count = 25
    }

    public static readonly Vector3[] JointBaseDir =
    {
        Vector3.zero,
        Vector3.up,
        Vector3.up,
        Vector3.up,
        Vector3.left,
        Vector3.left,
        Vector3.left,
        Vector3.left,
        Vector3.right,
        Vector3.right,
        Vector3.right,
        Vector3.right,
        Vector3.down,
        Vector3.down,
        Vector3.down,
        Vector3.forward,
        Vector3.down,
        Vector3.down,
        Vector3.down,
        Vector3.forward,
        Vector3.up,
        Vector3.left,
        Vector3.forward,
        Vector3.right,
        Vector3.forward
    };

    public enum TrackingState
    {
        NotTracked = 0,
        Inferred = 1,
        Tracked = 2
    }

	public enum HandState
    {
        Unknown = 0,
        NotTracked = 1,
        Open = 2,
        Closed = 3,
        Lasso = 4
    }
	
	public enum TrackingConfidence
    {
        Low = 0,
        High = 1
    }

//    [Flags]
//    public enum ClippedEdges
//    {
//        None = 0,
//        Right = 1,
//        Left = 2,
//        Top = 4,
//        Bottom = 8
//    }

	public enum FaceShapeAnimations : int
	{
		JawOpen                                  =0,
		LipPucker                                =1,
		JawSlideRight                            =2,
		LipStretcherRight                        =3,
		LipStretcherLeft                         =4,
		LipCornerPullerLeft                      =5,
		LipCornerPullerRight                     =6,
		LipCornerDepressorLeft                   =7,
		LipCornerDepressorRight                  =8,
		LeftcheekPuff                            =9,
		RightcheekPuff                           =10,
		LefteyeClosed                            =11,
		RighteyeClosed                           =12,
		RighteyebrowLowerer                      =13,
		LefteyebrowLowerer                       =14,
		LowerlipDepressorLeft                    =15,
		LowerlipDepressorRight                   =16,
	}
	
	public enum FaceShapeDeformations : int
	{
		PCA01                                    =0,
		PCA02                                    =1,
		PCA03                                    =2,
		PCA04                                    =3,
		PCA05                                    =4,
		PCA06                                    =5,
		PCA07                                    =6,
		PCA08                                    =7,
		PCA09                                    =8,
		PCA10                                    =9,
		Chin03                                   =10,
		Forehead00                               =11,
		Cheeks02                                 =12,
		Cheeks01                                 =13,
		MouthBag01                               =14,
		MouthBag02                               =15,
		Eyes02                                   =16,
		MouthBag03                               =17,
		Forehead04                               =18,
		Nose00                                   =19,
		Nose01                                   =20,
		Nose02                                   =21,
		MouthBag06                               =22,
		MouthBag05                               =23,
		Cheeks00                                 =24,
		Mask03                                   =25,
		Eyes03                                   =26,
		Nose03                                   =27,
		Eyes08                                   =28,
		MouthBag07                               =29,
		Eyes00                                   =30,
		Nose04                                   =31,
		Mask04                                   =32,
		Chin04                                   =33,
		Forehead05                               =34,
		Eyes06                                   =35,
		Eyes11                                   =36,
		Nose05                                   =37,
		Mouth07                                  =38,
		Cheeks08                                 =39,
		Eyes09                                   =40,
		Mask10                                   =41,
		Mouth09                                  =42,
		Nose07                                   =43,
		Nose08                                   =44,
		Cheeks07                                 =45,
		Mask07                                   =46,
		MouthBag09                               =47,
		Nose06                                   =48,
		Chin02                                   =49,
		Eyes07                                   =50,
		Cheeks10                                 =51,
		Rim20                                    =52,
		Mask22                                   =53,
		MouthBag15                               =54,
		Chin01                                   =55,
		Cheeks04                                 =56,
		Eyes17                                   =57,
		Cheeks13                                 =58,
		Mouth02                                  =59,
		MouthBag12                               =60,
		Mask19                                   =61,
		Mask20                                   =62,
		Forehead06                               =63,
		Mouth13                                  =64,
		Mask25                                   =65,
		Chin05                                   =66,
		Cheeks20                                 =67,
		Nose09                                   =68,
		Nose10                                   =69,
		MouthBag27                               =70,
		Mouth11                                  =71,
		Cheeks14                                 =72,
		Eyes16                                   =73,
		Mask29                                   =74,
		Nose15                                   =75,
		Cheeks11                                 =76,
		Mouth16                                  =77,
		Eyes19                                   =78,
		Mouth17                                  =79,
		MouthBag36                               =80,
		Mouth15                                  =81,
		Cheeks25                                 =82,
		Cheeks16                                 =83,
		Cheeks18                                 =84,
		Rim07                                    =85,
		Nose13                                   =86,
		Mouth18                                  =87,
		Cheeks19                                 =88,
		Rim21                                    =89,
		Mouth22                                  =90,
		Nose18                                   =91,
		Nose16                                   =92,
		Rim22                                    =93,
	}
	

	/// <summary>
	/// Container for the sensor data, including color, depth, ir and body frames.
	/// </summary>
	public class SensorData
	{
		public DepthSensorInterface sensorInterface;
		public DepthSensorPlatform sensorIntPlatform;

		public int bodyCount;
		public int jointCount;

		public float depthCameraOffset;
		public float depthCameraFOV;
		public float colorCameraFOV;
		public float faceOverlayOffset;

		public int colorImageWidth;
		public int colorImageHeight;

		public byte[] colorImage;
		public long lastColorFrameTime = 0;

		public int depthImageWidth;
		public int depthImageHeight;

		public ushort[] depthImage;
		public long lastDepthFrameTime = 0;

		public ushort[] infraredImage;
		public long lastInfraredFrameTime = 0;

		public byte[] bodyIndexImage;
		public long lastBodyIndexFrameTime = 0;

		public byte selectedBodyIndex = 255;

		public bool hintHeightAngle = false;
		public Quaternion sensorRotDetected = Quaternion.identity;
		public float sensorHgtDetected = 0f;
		
		public RenderTexture bodyIndexTexture;
		public Material bodyIndexMaterial;
		public ComputeBuffer bodyIndexBuffer;

		public float[] bodyIndexBufferData = null;
		public bool bodyIndexBufferReady = false;
		public object bodyIndexBufferLock = new object();

		public RenderTexture depthImageTexture;
		public Material depthImageMaterial;
		public ComputeBuffer depthImageBuffer;
		public ComputeBuffer depthHistBuffer;

		public float[] depthImageBufferData = null;
		public int[] depthHistBufferData = null;
		public float[] equalHistBufferData = null;
		public int depthHistTotalPoints = 0;
		public int firstUserIndex = -1;

		public bool depthImageBufferReady = false;
		public object depthImageBufferLock = new object();
		public bool depthCoordsBufferReady = false;
		public object depthCoordsBufferLock = new object();
		public bool newDepthImage = false;
		
		public Texture colorImageTexture = null;
		public Texture2D colorImageTexture2D = null;
		public Vector3 colorImageScale = Vector3.one;

		public bool colorImageBufferReady = false;
		public object colorImageBufferLock = new object();
		public bool newColorImage = false;

		public RenderTexture depth2ColorTexture;
		public Material depth2ColorMaterial;
		public ComputeBuffer depth2ColorBuffer;
		public Vector2[] depth2ColorCoords;
		public long lastDepth2ColorCoordsTime = 0;

		public Vector3[] depth2SpaceCoords;
		public bool spaceCoordsBufferReady = false;
		public object spaceCoordsBufferLock = new object();
		public long lastDepth2SpaceCoordsTime = 0;

        public bool backgroundRemovalInited = false;
        public bool backgroundRemovalHiRes = false;
		public bool invertAlphaColorMask = false;

		public RenderTexture color2DepthTexture;
		public Material color2DepthMaterial;
		public ComputeBuffer color2DepthBuffer;
		public Vector2[] color2DepthCoords;
		public long lastColor2DepthCoordsTime = 0;
		
		public RenderTexture alphaBodyTexture;
		public Material alphaBodyMaterial;
		public Material erodeBodyMaterial, dilateBodyMaterial, medianBodyMaterial, blurBodyMaterial;

		public int erodeIterations;
		public int dilateIterations;
		public bool applyMedianFilter = false;

		public RenderTexture colorBackgroundTexture;
		public Material colorBackgroundMaterial;

		public bool newInfraredImage = false;

		public bool bodyFrameReady = false;
		public object bodyFrameLock = new object();
		public bool newBodyFrame = false;
		
		public bool isPlayModeEnabled;
		public string playModeData;
		public string playModeHandData;
	}

	/// <summary>
	/// Parameters used for smoothing of the body-joint positions between frames.
	/// </summary>
	public struct SmoothParameters
	{
		public float smoothing;
		public float correction;
		public float prediction;
		public float jitterRadius;
		public float maxDeviationRadius;
	}

	/// <summary>
	/// Container for the body-joint data.
	/// </summary>
	public struct JointData
    {
		// parameters filled in by the sensor interface
		//public JointType jointType;
    	public TrackingState trackingState;
    	public Vector3 kinectPos;
    	public Vector3 position;
		public Quaternion orientation;  // deprecated

		public Vector3 posPrev;
		public Vector3 posRel;
		public Vector3 posVel;

		// KM calculated parameters
		public Vector3 direction;
		public Quaternion normalRotation;
		public Quaternion mirroredRotation;
		
		// Constraint parameters
		public float lastAngle;
    }

	/// <summary>
	/// Container for the body data.
	/// </summary>
	public struct BodyData
    {
		// parameters filled in by the sensor interface
        public Int64 liTrackingID;
        public Vector3 position;
		public Quaternion orientation;  // deprecated

		public JointData[] joint;

		// KM calculated parameters
		public Quaternion normalRotation;
		public Quaternion mirroredRotation;
		
		public Vector3 hipsDirection;
		public Vector3 shouldersDirection;
		public float bodyTurnAngle;
		//public float bodyFullAngle;
		//public float turnAroundFactor;
		public bool isTurnedAround;
		public float turnFaceLastTrackedTime;
		public float turnLeftShoulderTrackedTime;
		public float turnRightShoulderTrackedTime;
		public float turnShoulderDistTrackedTime;

		public Quaternion leftHandOrientation;
		public Quaternion rightHandOrientation;

		public Quaternion headOrientation;

//		public Vector3 leftArmDirection;
//		public Vector3 leftThumbForward;
//		public Vector3 leftThumbDirection;
//		//public float leftThumbAngle;
//
//		public Vector3 rightArmDirection;
//		public Vector3 rightThumbForward;
//		public Vector3 rightThumbDirection;
//		//public float rightThumbAngle;

		//public Vector3 leftLegDirection;
		//public Vector3 leftFootDirection;
		//public Vector3 rightLegDirection;
		//public Vector3 rightFootDirection;

		public HandState leftHandState;
		public TrackingConfidence leftHandConfidence;
		public HandState rightHandState;
		public TrackingConfidence rightHandConfidence;
		
        public uint dwClippedEdges;
        public short bIsTracked;
		public short bIsRestricted;
    }

	/// <summary>
	/// Container for the body frame data.
	/// </summary>
    public struct BodyFrameData
    {
        public Int64 liRelativeTime, liPreviousTime;
        [MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = 6, ArraySubType = UnmanagedType.Struct)]
        public BodyData[] bodyData;
        //public UnityEngine.Vector4 floorClipPlane;
		public bool bJointVelocities;
		
		public BodyFrameData(int bodyCount, int jointCount)
		{
			liRelativeTime = liPreviousTime = 0;
			//floorClipPlane = UnityEngine.Vector4.zero;
			bJointVelocities = false;

			bodyData = new BodyData[bodyCount];

			for(int i = 0; i < bodyCount; i++)
			{
				bodyData[i].joint = new JointData[jointCount];

				bodyData[i].leftHandOrientation = Quaternion.identity;
				bodyData[i].rightHandOrientation = Quaternion.identity;
				bodyData[i].headOrientation = Quaternion.identity;
			}
		}
    }
	

	// initializes the available sensor interfaces
	public static List<DepthSensorInterface> InitSensorInterfaces(bool bOnceRestarted, ref bool bNeedRestart)
	{
		List<DepthSensorInterface> listInterfaces = new List<DepthSensorInterface>();

		//var typeInterface = typeof(DepthSensorInterface);

		for(int pass = 0; pass <= 1; pass++)
		{
			bool bCopyLibs = (pass != 0);

			//foreach(Type type in SensorInterfaceOrder)
			for(int i = 0; i < SensorInterfaceOrder.Length; i++)
			{
				DepthSensorInterface sensorInt = SensorInterfaceOrder[i];

				//if(typeInterface.IsAssignableFrom(type) && type != typeInterface)
				if(sensorInt != null)
				{
					//DepthSensorInterface sensorInt = null;
					
					try 
					{
						//sensorInt = (DepthSensorInterface)Activator.CreateInstance(type);
						
						bool bIntNeedRestart = bOnceRestarted;
						if(sensorInt.InitSensorInterface(bCopyLibs, ref bIntNeedRestart))
						{
							bNeedRestart |= bIntNeedRestart;
						}
						else
						{
							sensorInt.FreeSensorInterface(bCopyLibs);
							sensorInt = null;

							continue;
						}
						
						if(!bNeedRestart && !sensorInt.IsSensorAvailable())
						{
							sensorInt.FreeSensorInterface(false);
							sensorInt = null;
						}
					}
					catch (Exception /**ex*/) 
					{
						//Debug.Log(ex);
						
						if(sensorInt != null)
						{
							try 
							{
								sensorInt.FreeSensorInterface(bCopyLibs);
							}
							catch (Exception) 
							{
								// do nothing
							}
							finally
							{
								sensorInt = null;
							}
						}
					}
					
					if(sensorInt != null)
					{
						listInterfaces.Add(sensorInt);
					}
				}
			}

			if(listInterfaces.Count > 0)
			{
				// we found working interface(s), don't go any further
				break;
			}

			if(bOnceRestarted)
			{
				// we have restarted once, don't do it again
				break;
			}
		}

		return listInterfaces;
	}

	// opens the default sensor and needed readers
	public static SensorData OpenDefaultSensor(List<DepthSensorInterface> listInterfaces, FrameSource dwFlags, float sensorAngle, bool bUseMultiSource,  
		KinectManager.UserMapType userMapType, BackgroundRemovalManager brManager)
	{
		SensorData sensorData = null;
		if(listInterfaces == null)
			return sensorData;

		foreach(DepthSensorInterface sensorInt in listInterfaces)
		{
			try 
			{
				if(sensorData == null)
				{
					sensorData = sensorInt.OpenDefaultSensor(dwFlags, sensorAngle, bUseMultiSource);

					if(sensorData != null)
					{
						sensorData.sensorInterface = sensorInt;
						sensorData.sensorIntPlatform = sensorInt.GetSensorPlatform();
						Debug.Log("Interface used: " + sensorInt.GetType().Name);

						Debug.Log("Shader level: " + SystemInfo.graphicsShaderLevel);
						if(sensorData.bodyIndexImage != null && IsDirectX11Available())
						{
							Shader bodyIndexShader = Shader.Find("Kinect/BodyShader");

							if(bodyIndexShader != null)
							{
                                if(sensorData.bodyIndexTexture == null || sensorData.bodyIndexTexture.width != sensorData.depthImageWidth || sensorData.bodyIndexTexture.height != sensorData.depthImageHeight)
                                {
                                    sensorData.bodyIndexTexture = new RenderTexture(sensorData.depthImageWidth, sensorData.depthImageHeight, 0);
                                    sensorData.bodyIndexTexture.wrapMode = TextureWrapMode.Clamp;
                                    sensorData.bodyIndexTexture.filterMode = FilterMode.Point;
                                    //Debug.Log(sensorData.bodyIndexTexture.format);
                                }

                                sensorData.bodyIndexMaterial = new Material(bodyIndexShader);
								
								sensorData.bodyIndexMaterial.SetFloat("_TexResX", (float)sensorData.depthImageWidth);
								sensorData.bodyIndexMaterial.SetFloat("_TexResY", (float)sensorData.depthImageHeight);
								
								sensorData.bodyIndexBuffer = new ComputeBuffer(sensorData.bodyIndexImage.Length, sizeof(float));
								sensorData.bodyIndexMaterial.SetBuffer("_BodyIndexBuffer", sensorData.bodyIndexBuffer);
							}
						}
						
						if(sensorData.depthImage != null && IsDirectX11Available() &&
						    userMapType == KinectManager.UserMapType.UserTexture)
						{
							Shader depthImageShader = Shader.Find("Kinect/DepthShader");

							if(depthImageShader != null)
							{
                                if (sensorData.depthImageTexture == null || sensorData.depthImageTexture.width != sensorData.depthImageWidth || sensorData.depthImageTexture.height != sensorData.depthImageHeight)
                                {
                                    sensorData.depthImageTexture = new RenderTexture(sensorData.depthImageWidth, sensorData.depthImageHeight, 0);
                                    sensorData.depthImageTexture.wrapMode = TextureWrapMode.Clamp;
                                    sensorData.depthImageTexture.filterMode = FilterMode.Point;
                                }

                                sensorData.depthImageMaterial = new Material(depthImageShader);
								
								sensorData.depthImageMaterial.SetTexture("_MainTex", sensorData.bodyIndexTexture);
								
								sensorData.depthImageMaterial.SetFloat("_TexResX", (float)sensorData.depthImageWidth);
								sensorData.depthImageMaterial.SetFloat("_TexResY", (float)sensorData.depthImageHeight);
								
								sensorData.depthImageBuffer = new ComputeBuffer(sensorData.depthImage.Length, sizeof(float));
								sensorData.depthImageMaterial.SetBuffer("_DepthBuffer", sensorData.depthImageBuffer);
								
								sensorData.depthHistBuffer = new ComputeBuffer(5001, sizeof(float));
								sensorData.depthImageMaterial.SetBuffer("_HistBuffer", sensorData.depthHistBuffer);

								// use body index buffer to overcome the linear color correction
								sensorData.depthImageMaterial.SetBuffer("_BodyIndexBuffer", sensorData.bodyIndexBuffer);
							}
						}

						if(sensorData.colorImage != null && sensorData.colorImage.Length > 0)
						{
                            if (sensorData.colorImageTexture2D == null || sensorData.colorImageTexture2D.width != sensorData.colorImageWidth || sensorData.colorImageTexture2D.height != sensorData.colorImageHeight)
                            {
                                sensorData.colorImageTexture2D = new Texture2D(sensorData.colorImageWidth, sensorData.colorImageHeight, TextureFormat.RGBA32, false);
                            }
                        }
						
						// check if background removal requires cut-out image
						bool bBrRequiresCutOut = brManager && (!brManager.colorCameraResolution || !sensorInt.IsBRHiResSupported());
						
						if(sensorData.bodyIndexImage != null && sensorData.colorImage != null && IsDirectX11Available() &&
						   (userMapType == KinectManager.UserMapType.CutOutTexture || bBrRequiresCutOut))
						{
							Shader depth2ColorShader = Shader.Find("Kinect/Depth2ColorShader");

							if(depth2ColorShader != null)
							{
                                if (sensorData.depth2ColorTexture == null || sensorData.depth2ColorTexture.width != sensorData.depthImageWidth || sensorData.depth2ColorTexture.height != sensorData.depthImageHeight)
                                {
                                    sensorData.depth2ColorTexture = new RenderTexture(sensorData.depthImageWidth, sensorData.depthImageHeight, 0);
                                    sensorData.depth2ColorTexture.wrapMode = TextureWrapMode.Clamp;
                                    sensorData.depth2ColorTexture.filterMode = FilterMode.Point;
                                }

                                sensorData.depth2ColorMaterial = new Material(depth2ColorShader);
								
								sensorData.depth2ColorMaterial.SetFloat("_ColorResX", (float)sensorData.colorImageWidth);
								sensorData.depth2ColorMaterial.SetFloat("_ColorResY", (float)sensorData.colorImageHeight);
								sensorData.depth2ColorMaterial.SetFloat("_DepthResX", (float)sensorData.depthImageWidth);
								sensorData.depth2ColorMaterial.SetFloat("_DepthResY", (float)sensorData.depthImageHeight);
								
								sensorData.depth2ColorBuffer = new ComputeBuffer(sensorData.depthImage.Length, sizeof(float) * 2);
								sensorData.depth2ColorMaterial.SetBuffer("_ColorCoords", sensorData.depth2ColorBuffer);

								sensorData.depth2ColorCoords = new Vector2[sensorData.depthImage.Length];
							}
						}

//						if(sensorData.bodyIndexImage != null && sensorData.colorImage != null && IsDirectX11Available() &&
//						   (userMapType == KinectManager.UserMapType.CutOutTexture || bBrRequiresCutOut))
//						{
//							sensorData.depth2ColorCoords = new Vector2[sensorData.depthImage.Length];
//						}
						
					}
				}
				else
				{
					sensorInt.FreeSensorInterface(false);
				}
			}
			catch (Exception ex) 
			{
				Debug.LogError("Initialization of the sensor failed.");
				Debug.LogError(ex.ToString());

				try 
				{
					sensorInt.FreeSensorInterface(false);
				} 
				catch (Exception) 
				{
					// do nothing
				}
			}
		}

		return sensorData;
	}

	// closes opened readers and closes the sensor
	public static void CloseSensor(SensorData sensorData)
	{
		FinishBackgroundRemoval(sensorData);
		//FinishColorBackground(sensorData);

		if(sensorData != null && sensorData.sensorInterface != null)
		{
			sensorData.sensorInterface.CloseSensor(sensorData);
		}

		if(sensorData.depthImageTexture)
		{
			sensorData.depthImageTexture.Release();
			sensorData.depthImageTexture = null;
		}

		if(sensorData.bodyIndexBuffer != null)
		{
			sensorData.bodyIndexBuffer.Release();
			sensorData.bodyIndexBuffer = null;
		}
		
		if(sensorData.depthImageBuffer != null)
		{
			sensorData.depthImageBuffer.Release();
			sensorData.depthImageBuffer = null;
		}

		if(sensorData.depthHistBuffer != null)
		{
			sensorData.depthHistBuffer.Release();
			sensorData.depthHistBuffer = null;
		}
		
		if(sensorData.depth2ColorBuffer != null)
		{
			sensorData.depth2ColorBuffer.Release();
			sensorData.depth2ColorBuffer = null;
		}

        //if (sensorData.depth2ColorTexture != null)
        //{
        //    sensorData.depth2ColorTexture.Release();
        //    sensorData.depth2ColorTexture = null;
        //}

        //sensorData.depth2ColorMaterial = null;
        //sensorData.depth2ColorCoords = null;
    }

    // invoked periodically to update sensor data, if needed
    public static bool UpdateSensorData(SensorData sensorData)
	{
		bool bResult = false;

		if(sensorData.sensorInterface != null)
		{
			bResult = sensorData.sensorInterface.UpdateSensorData(sensorData);
		}

		return bResult;
	}
	
	// returns the mirror joint of the given joint
	public static JointType GetMirrorJoint(JointType joint)
	{
		switch(joint)
		{
			case JointType.ShoulderLeft:
				return JointType.ShoulderRight;
	        case JointType.ElbowLeft:
				return JointType.ElbowRight;
	        case JointType.WristLeft:
				return JointType.WristRight;
	        case JointType.HandLeft:
				return JointType.HandRight;
					
	        case JointType.ShoulderRight:
				return JointType.ShoulderLeft;
	        case JointType.ElbowRight:
				return JointType.ElbowLeft;
	        case JointType.WristRight:
				return JointType.WristLeft;
	        case JointType.HandRight:
				return JointType.HandLeft;
					
	        case JointType.HipLeft:
				return JointType.HipRight;
	        case JointType.KneeLeft:
				return JointType.KneeRight;
	        case JointType.AnkleLeft:
				return JointType.AnkleRight;
	        case JointType.FootLeft:
				return JointType.FootRight;
					
	        case JointType.HipRight:
				return JointType.HipLeft;
	        case JointType.KneeRight:
				return JointType.KneeLeft;
	        case JointType.AnkleRight:
				return JointType.AnkleLeft;
	        case JointType.FootRight:
				return JointType.FootLeft;
					
	        case JointType.HandTipLeft:
				return JointType.HandTipRight;
	        case JointType.ThumbLeft:
				return JointType.ThumbRight;
			
	        case JointType.HandTipRight:
				return JointType.HandTipLeft;
	        case JointType.ThumbRight:
				return JointType.ThumbLeft;
		}
	
		return joint;
	}

	// gets new multi source frame
	public static bool GetMultiSourceFrame(SensorData sensorData)
	{
		bool bResult = false;

		if(sensorData.sensorInterface != null)
		{
			bResult = sensorData.sensorInterface.GetMultiSourceFrame(sensorData);
		}

		return bResult;
	}

	// frees last multi source frame
	public static void FreeMultiSourceFrame(SensorData sensorData)
	{
		if(sensorData.sensorInterface != null)
		{
			sensorData.sensorInterface.FreeMultiSourceFrame(sensorData);
		}
	}

	// gets matrix values as csv line
	public static string GetMatrixAsCsv(ref Matrix4x4 mat, char delimiter)
	{
		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		//const char delimiter = ',';

		sbBuf.Append("km").Append(delimiter);

		for(int i = 0; i < 4; i++)
		{
			Vector4 vRow = mat.GetRow(i);

			sbBuf.AppendFormat("{0:F3}", vRow.x).Append(delimiter);
			sbBuf.AppendFormat("{0:F3}", vRow.y).Append(delimiter);
			sbBuf.AppendFormat("{0:F3}", vRow.z).Append(delimiter);
			sbBuf.AppendFormat("{0:F3}", vRow.w).Append(delimiter);
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets matrix values from a csv line
	public static bool SetMatrixFromCsv(string sCsvLine, ref Matrix4x4 mat)
	{
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 17)
			return false;
		if(alCsvParts[0] != "km")
			return false;

		int iIndex = 1;
		for(int i = 0; i < 4; i++)
		{
			float x = 0f, y = 0f, z = 0f, w = 0f;

			float.TryParse(alCsvParts[iIndex], out x);
			float.TryParse(alCsvParts[iIndex + 1], out y);
			float.TryParse(alCsvParts[iIndex + 2], out z);
			float.TryParse(alCsvParts[iIndex + 3], out w);
			iIndex += 4;

			Vector4 vRow = new Vector4(x, y, z, w);
			mat.SetRow(i, vRow);
		}
	
		return true;
	}

	// converts current hand data to a single csv line. returns empty string if there is no new data
	public static string GetHandsDataAsCsv(SensorData sensorData, ref BodyFrameData bodyFrame, ref long liRelTime, char delimiter)
	{
		// check for invalid sensor data and if the frame is still the same
		if(sensorData == null)
			return string.Empty;
//		if(bodyFrame.liRelativeTime == liRelTime)
//			return string.Empty;

		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		//const char delimiter = ',';

		sbBuf.Append("kh").Append(delimiter);
		sbBuf.Append(bodyFrame.liRelativeTime).Append(delimiter);
		sbBuf.Append(sensorData.bodyCount).Append(delimiter);

		// add information for all bodies
		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			sbBuf.Append(bodyFrame.bodyData[i].bIsTracked).Append(delimiter);

			if(bodyFrame.bodyData[i].bIsTracked != 0)
			{
				// add information for the tracked body - body-id and hand states
				sbBuf.Append(bodyFrame.bodyData[i].liTrackingID).Append(delimiter);

				sbBuf.AppendFormat("{0}", (int)bodyFrame.bodyData[i].leftHandState).Append(delimiter);
				sbBuf.AppendFormat("{0}", (int)bodyFrame.bodyData[i].leftHandConfidence).Append(delimiter);

				sbBuf.AppendFormat("{0}", (int)bodyFrame.bodyData[i].rightHandState).Append(delimiter);
				sbBuf.AppendFormat("{0}", (int)bodyFrame.bodyData[i].rightHandConfidence).Append(delimiter);
			}
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets current hand data from the given csv line. returns true on success, false otherwise
	public static bool SetHandsDataFromCsv(string sCsvLine, SensorData sensorData, ref BodyFrameData bodyFrame)
	{
		// check for invalid sensor data and for same frame time
		if(sensorData == null)
			return false;
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 3)
			return false;

		// check the id & body count
		int bodyCount = 0;
		int.TryParse(alCsvParts[2], out bodyCount);

		long liRelTime = 0;
		long.TryParse(alCsvParts[1], out liRelTime);

		if(alCsvParts[0] != "kh" || bodyCount == 0 || liRelTime == 0)
			return false;

		// check rel time
		if(bodyFrame.liRelativeTime > liRelTime)
			return false;

		int iIndex = 3;
		for(int i = 0; i < bodyCount; i++)
		{
			if(alCsvParts.Length < (iIndex + 1))
				return false;

			// update the tracked-flag and body id
			short bIsTracked = 0;
			long liTrackingID = 0;

			short.TryParse(alCsvParts[iIndex], out bIsTracked);
			iIndex++;

			if(bIsTracked != 0 && alCsvParts.Length >= (iIndex + 5))
			{
				long.TryParse(alCsvParts[iIndex], out liTrackingID);
				iIndex++;

				int lhState = 0, lhConf = 0, rhState = 0, rhConf = 0;

				int.TryParse(alCsvParts[iIndex++], out lhState);
				int.TryParse(alCsvParts[iIndex++], out lhConf);

				int.TryParse(alCsvParts[iIndex++], out rhState);
				int.TryParse(alCsvParts[iIndex++], out rhConf);

				if(i < sensorData.bodyCount && bodyFrame.bodyData[i].bIsTracked != 0 && 
					bodyFrame.bodyData[i].liTrackingID == liTrackingID)
				{
					bodyFrame.bodyData[i].leftHandState = (HandState)lhState;
					bodyFrame.bodyData[i].leftHandConfidence = (TrackingConfidence)lhConf;

					bodyFrame.bodyData[i].rightHandState = (HandState)rhState;
					bodyFrame.bodyData[i].rightHandConfidence = (TrackingConfidence)rhConf;
				}
			}
		}

		return true;
	}

	// converts current body frame to a single csv line. returns empty string if there is no new data
	public static string GetBodyFrameAsCsv(SensorData sensorData, ref BodyFrameData bodyFrame, ref long liRelTime, ref float fUnityTime, char delimiter)
	{
		// check for invalid sensor data and if the frame is still the same
		if(sensorData == null)
			return string.Empty;
		if(bodyFrame.liRelativeTime == liRelTime)
			return string.Empty;

		// create the output string
		StringBuilder sbBuf = new StringBuilder();
		//const char delimiter = ',';

		sbBuf.Append("kb").Append(delimiter);
		sbBuf.Append(bodyFrame.liRelativeTime).Append(delimiter);

		liRelTime = bodyFrame.liRelativeTime;
		fUnityTime = Time.time;

		sbBuf.Append(sensorData.bodyCount).Append(delimiter);
		sbBuf.Append(sensorData.jointCount).Append(delimiter);

		// add information for all bodies
		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			sbBuf.Append(bodyFrame.bodyData[i].bIsTracked).Append(delimiter);

			if(bodyFrame.bodyData[i].bIsTracked != 0)
			{
				// add information for the tracked body - body-id and joints
				sbBuf.Append(bodyFrame.bodyData[i].liTrackingID).Append(delimiter);

				for(int j = 0; j < sensorData.jointCount; j++)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];

					sbBuf.Append((int)jointData.trackingState).Append(delimiter);

					if(jointData.trackingState != TrackingState.NotTracked)
					{
						sbBuf.AppendFormat("{0:F3}", jointData.kinectPos.x).Append(delimiter);
						sbBuf.AppendFormat("{0:F3}", jointData.kinectPos.y).Append(delimiter);
						sbBuf.AppendFormat("{0:F3}", jointData.kinectPos.z).Append(delimiter);
					}
				}
			}
		}

		// remove the last delimiter
		if(sbBuf.Length > 0 && sbBuf[sbBuf.Length - 1] == delimiter)
		{
			sbBuf.Remove(sbBuf.Length - 1, 1);
		}

		return sbBuf.ToString();
	}

	// sets current body frame from the given csv line. returns true on success, false otherwise
	public static bool SetBodyFrameFromCsv(string sCsvLine, SensorData sensorData, ref BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld)
	{
		// check for invalid sensor data and for same frame time
		if(sensorData == null)
			return false;
		if(sCsvLine.Length == 0)
			return false;

		// split the csv line in parts
		char[] delimiters = { ',' };
		string[] alCsvParts = sCsvLine.Split(delimiters);

		if(alCsvParts.Length < 4)
			return false;

		//// wait for buffer release
		//while(sensorData.bodyFrameReady)
		//{
		//	Sleep(1);
		//}

		// check the id, body count & joint count
		int bodyCount = 0, jointCount = 0;
		int.TryParse(alCsvParts[2], out bodyCount);
		int.TryParse(alCsvParts[3], out jointCount);

		long liRelTime = 0;
		long.TryParse(alCsvParts[1], out liRelTime);

		if(alCsvParts[0] != "kb" || bodyCount == 0 || jointCount == 0 || liRelTime == 0)
			return false;
		//		if(bodyCount != sensorData.bodyCount || jointCount != sensorData.jointCount)
		//			return false;

		if (bodyCount != sensorData.bodyCount) 
		{
			// set the other bodies as not tracked
			for (int i = bodyCount; i < sensorData.bodyCount; i++) 
			{
				bodyFrame.bodyData[i].bIsTracked = 0;
			}
		}

		// update body frame data
		bodyFrame.liPreviousTime = bodyFrame.liRelativeTime;
		bodyFrame.liRelativeTime = liRelTime;

		int iIndex = 4;
		for(int i = 0; i < bodyCount; i++)
		{
			if(alCsvParts.Length < (iIndex + 1))
				return false;

			// update the tracked-flag and body id
			short bIsTracked = 0;
			long liTrackingID = 0;

			short.TryParse(alCsvParts[iIndex], out bIsTracked);
			iIndex++;

			if(bIsTracked != 0 && alCsvParts.Length >= (iIndex + 1))
			{
				long.TryParse(alCsvParts[iIndex], out liTrackingID);
				iIndex++;

				if(liTrackingID == 0)
				{
					bIsTracked = 0;
				}
			}

			bodyFrame.bodyData[i].bIsTracked = bIsTracked;
			bodyFrame.bodyData[i].liTrackingID = liTrackingID;

			if(bIsTracked != 0)
			{
				if (jointCount != sensorData.jointCount) 
				{
					// set the other joints as not tracked
					for (int j = jointCount; j < sensorData.jointCount; j++) 
					{
						bodyFrame.bodyData [i].joint [j].trackingState = TrackingState.NotTracked;
					}
				}

				// update joints' data
				for(int j = 0; j < jointCount; j++)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];
					int iTrackingState = 0;

					if(alCsvParts.Length >= (iIndex + 1))
					{
						int.TryParse(alCsvParts[iIndex], out iTrackingState);
						iIndex++;

						jointData.trackingState = (KinectInterop.TrackingState)iTrackingState;

						if(iTrackingState != (int)TrackingState.NotTracked && alCsvParts.Length >= (iIndex + 3))
						{
							float x = 0f, y = 0f, z = 0f;

							float.TryParse(alCsvParts[iIndex], out x);
							float.TryParse(alCsvParts[iIndex + 1], out y);
							float.TryParse(alCsvParts[iIndex + 2], out z);
							iIndex += 3;

							jointData.kinectPos = new Vector3(x, y, z);
						}
						else
						{
							jointData.kinectPos = Vector3.zero;
						}

						jointData.position = kinectToWorld.MultiplyPoint3x4(jointData.kinectPos);
						jointData.orientation = Quaternion.identity;

						if(j == 0)
						{
							// set body position
							bodyFrame.bodyData[i].position = jointData.position;
							bodyFrame.bodyData[i].orientation = jointData.orientation;
						}
					}

					bodyFrame.bodyData[i].joint[j] = jointData;
				}
			}
		}

		// calculate bone directions
		CalcBodyFrameBoneDirs(sensorData, ref bodyFrame);

		// frame is ready
		lock(sensorData.bodyFrameLock)
		{
			sensorData.bodyFrameReady = true;
		}

		return true;
	}

	// Polls for new skeleton data
	public static bool PollBodyFrame(SensorData sensorData, ref BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
	{
		bool bNewFrame = false;

		if(sensorData.sensorInterface != null)
		{
			//// wait for buffer release
			//while(sensorData.bodyFrameReady)
			//{
			//	Sleep(1);
			//}
			
			bNewFrame = sensorData.sensorInterface.PollBodyFrame(sensorData, ref bodyFrame, ref kinectToWorld, bIgnoreJointZ);

			if(bNewFrame)
			{
				if(bodyFrame.bJointVelocities && bodyFrame.liPreviousTime > 0)
				{
					CalcBodyFrameJointVels(sensorData, ref bodyFrame);
				}

				CalcBodyFrameBoneDirs(sensorData, ref bodyFrame);
				
				// frame is ready
				lock(sensorData.bodyFrameLock)
				{
					sensorData.bodyFrameReady = true;
				}
			}
		}
		
		return bNewFrame;
	}

	// calculates joint velocities in a body frame
	private static void CalcBodyFrameJointVels(SensorData sensorData, ref BodyFrameData bodyFrame)
	{
		// calculate the inter-frame time
		float frameTime = (float)(bodyFrame.liRelativeTime - bodyFrame.liPreviousTime) / 100000000000f;

		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			if(bodyFrame.bodyData[i].bIsTracked != 0)
			{
				for(int j = 0; j < sensorData.jointCount; j++)
				{
					KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];

					int p = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)j);
					Vector3 parentPos = bodyFrame.bodyData[i].joint[p].position;

					jointData.posRel = jointData.position - parentPos;
					jointData.posVel = frameTime > 0f ? (jointData.position - jointData.posPrev) / frameTime : Vector3.zero;
					jointData.posPrev = jointData.position;

					bodyFrame.bodyData[i].joint[j] = jointData;
				}
			}
		}

	}

	// Calculates all valid bone directions in a body frame
	private static void CalcBodyFrameBoneDirs(SensorData sensorData, ref BodyFrameData bodyFrame)
	{
		for(int i = 0; i < sensorData.bodyCount; i++)
		{
			if(bodyFrame.bodyData[i].bIsTracked != 0)
			{
				for(int j = 0; j < sensorData.jointCount; j++)
				{
					if(j == 0)
					{
						bodyFrame.bodyData[i].joint[j].direction = Vector3.zero;
					}
					else
					{
						int jParent = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)j);
						
						if(bodyFrame.bodyData[i].joint[j].trackingState != TrackingState.NotTracked && 
						   bodyFrame.bodyData[i].joint[jParent].trackingState != TrackingState.NotTracked)
						{
							bodyFrame.bodyData[i].joint[j].direction = 
								bodyFrame.bodyData[i].joint[j].position - bodyFrame.bodyData[i].joint[jParent].position;
						}
					}
				}
			}
		}

	}
	
	// Recalculates bone directions for the given body
	public static void RecalcBoneDirs(SensorData sensorData, ref BodyData bodyData)
	{
		for(int j = 0; j < bodyData.joint.Length; j++)
		{
			if(j == 0)
			{
				bodyData.joint[j].direction = Vector3.zero;
			}
			else
			{
				int jParent = (int)sensorData.sensorInterface.GetParentJoint((KinectInterop.JointType)j);
				
				if(bodyData.joint[j].trackingState != TrackingState.NotTracked && 
				   bodyData.joint[jParent].trackingState != TrackingState.NotTracked)
				{
					bodyData.joint[j].direction = bodyData.joint[j].position - bodyData.joint[jParent].position;
				}
			}
		}
	}
	
	// Polls for new color frame data
	public static bool PollColorFrame(SensorData sensorData)
	{
		bool bNewFrame = false;

		if(sensorData.sensorInterface != null && !sensorData.isPlayModeEnabled)
		{
			//// wait for buffer release
			//while(sensorData.colorImageBufferReady)
			//{
			//	Sleep(1);
			//}
			
			bNewFrame = sensorData.sensorInterface.PollColorFrame(sensorData);

			if(bNewFrame)
			{
				// buffer is ready
				lock(sensorData.colorImageBufferLock)
				{
					sensorData.colorImageBufferReady = true;
				}
			}
		}

		return bNewFrame;
	}

	// Renders color texture
	public static void RenderColorTexture(SensorData sensorData)
	{
		if(sensorData.colorImageBufferReady && sensorData.colorImageTexture2D)
		{
			sensorData.colorImageTexture2D.LoadRawTextureData(sensorData.colorImage);
			sensorData.colorImageTexture2D.Apply();
		
			// buffer is released
			lock(sensorData.colorImageBufferLock)
			{
				sensorData.colorImageBufferReady = false;
			}
		}
	}
	
	// Polls for new depth frame data
	public static bool PollDepthFrame(SensorData sensorData, KinectManager.UserMapType userMapType,
	                                  bool bLimitedUsers, ICollection<int> alTrackedIndexes)
	{
		bool bNewFrame = false;

		if(sensorData.sensorInterface != null && !sensorData.isPlayModeEnabled)
		{
			//// wait for buffer releases
			//while(sensorData.bodyIndexBufferReady || sensorData.depthImageBufferReady)
			//{
			//	Sleep(1);
			//}
			
			bNewFrame = sensorData.sensorInterface.PollDepthFrame(sensorData);

			if(bNewFrame)
			{
				if(userMapType != KinectManager.UserMapType.RawUserDepth && sensorData.bodyIndexBuffer != null)
				{
					byte btSelBI = sensorData.selectedBodyIndex;
					int iBodyIndexLength = sensorData.bodyIndexImage.Length;

					// convert the body indices to string
					string sTrackedIndices = string.Empty;

					if (bLimitedUsers) 
					{
						foreach(int bodyIndex in alTrackedIndexes)
						{
							sTrackedIndices += (char)(0x30 + bodyIndex);
						}

						//Debug.Log ("Tracked indices: " + sTrackedIndices);
					}

					// create body index texture
					if(sensorData.bodyIndexBufferData == null)
					{
						sensorData.bodyIndexBufferData = new float[iBodyIndexLength];
					}

					for (int i = 0; i < iBodyIndexLength; i++)
					{
						byte btBufBI = sensorData.bodyIndexImage[i];

						bool bUserTracked = btSelBI != 255 ? btSelBI == btBufBI : 
								//(bLimitedUsers ? alTrackedIndexes.Contains((int)btBufBI) : btBufBI != 255);
								(bLimitedUsers ? sTrackedIndices.IndexOf((char)(0x30 + btBufBI)) >= 0 : btBufBI != 255);

						if(bUserTracked)
						{
							sensorData.bodyIndexBufferData[i] = (float)btBufBI;
						}
						else
						{
							sensorData.bodyIndexBufferData[i] = 255f;
						}
					}

					// buffer is ready
					lock(sensorData.bodyIndexBufferLock)
					{
						sensorData.bodyIndexBufferReady = true;
					}
				}
				
				if(sensorData.depthImageBuffer != null && sensorData.depthHistBuffer != null &&
				   userMapType == KinectManager.UserMapType.UserTexture)
				{
					// create depth texture
					if(sensorData.depthImageBufferData == null)
					{
						sensorData.depthImageBufferData = new float[sensorData.depthImage.Length];
						sensorData.depthHistBufferData = new int[5001];
						sensorData.equalHistBufferData = new float[sensorData.depthHistBufferData.Length];
					}

					Array.Clear(sensorData.depthHistBufferData, 0, sensorData.depthHistBufferData.Length);
					Array.Clear(sensorData.equalHistBufferData, 0, sensorData.equalHistBufferData.Length);
					sensorData.depthHistTotalPoints = 0;

					for (int i = 0; i < sensorData.depthImage.Length; i++)
					{
						int depth = sensorData.depthImage[i] < 5000 ? (int)sensorData.depthImage[i] : 5000;
						sensorData.depthImageBufferData[i] = (float)depth;

						if(sensorData.bodyIndexImage[i] != 255)
						{
							sensorData.depthHistBufferData[depth]++;
							sensorData.depthHistTotalPoints++;
						}
					}

					sensorData.equalHistBufferData[0] = (float)sensorData.depthHistBufferData[0];
					for(int i = 1; i < sensorData.depthHistBufferData.Length; i++)
					{
						sensorData.equalHistBufferData[i] = sensorData.equalHistBufferData[i - 1] + (float)sensorData.depthHistBufferData[i];
					}

					// buffer is ready
					lock(sensorData.depthImageBufferLock)
					{
						sensorData.depthImageBufferReady = true;
					}
				}

				if(sensorData.color2DepthCoords != null)
				{
					//// wait for buffer release
					//while(sensorData.depthCoordsBufferReady)
					//{
					//	Sleep(1);
					//}
					
					if(!MapColorFrameToDepthCoords(sensorData, ref sensorData.color2DepthCoords))
					{
						sensorData.color2DepthCoords = null;
					}

					if (sensorData.color2DepthCoords != null) 
					{
						sensorData.lastColor2DepthCoordsTime = DateTime.Now.Ticks;
					}

					// buffer is ready
					lock(sensorData.depthCoordsBufferLock)
					{
						sensorData.depthCoordsBufferReady = (sensorData.color2DepthCoords != null);
					}
				}
				else if(sensorData.depth2ColorCoords != null && (userMapType == KinectManager.UserMapType.CutOutTexture || 
					    (sensorData.sensorInterface.IsBackgroundRemovalActive() && 
						 sensorData.sensorInterface.GetSensorPlatform() != KinectInterop.DepthSensorPlatform.KinectSDKv1)))
				{
					//// wait for buffer release
					//while(sensorData.depthCoordsBufferReady)
					//{
					//	Sleep(1);
					//}
					
					if(!MapDepthFrameToColorCoords(sensorData, ref sensorData.depth2ColorCoords))
					{
						sensorData.depth2ColorCoords = null;
					}

					if (sensorData.depth2ColorCoords != null) 
					{
						sensorData.lastDepth2ColorCoordsTime = DateTime.Now.Ticks;
					}
					
					// buffer is ready
					lock(sensorData.depthCoordsBufferLock)
					{
						sensorData.depthCoordsBufferReady = (sensorData.depth2ColorCoords != null);
					}
				}

				if(sensorData.depth2SpaceCoords != null)
				{
					//// wait for buffer release
					//while(sensorData.spaceCoordsBufferReady)
					//{
					//	Sleep(1);
					//}
					
					if(!MapDepthFrameToSpaceCoords(sensorData, ref sensorData.depth2SpaceCoords))
					{
						sensorData.depth2SpaceCoords = null;
					}

					if (sensorData.depth2SpaceCoords != null) 
					{
						sensorData.lastDepth2SpaceCoordsTime = DateTime.Now.Ticks;
					}
					
					// buffer is ready
					lock(sensorData.spaceCoordsBufferLock)
					{
						sensorData.spaceCoordsBufferReady = (sensorData.depth2SpaceCoords != null);
					}
				}

			}
		}
		
		return bNewFrame;
	}

	// Renders body-index texture
	public static void RenderBodyIndexTexture(SensorData sensorData, KinectManager.UserMapType userMapType)
	{
		// check if buffer is ready
		if(sensorData.bodyIndexBufferReady)
		{
			sensorData.bodyIndexBuffer.SetData(sensorData.bodyIndexBufferData);
			Graphics.Blit(null, sensorData.bodyIndexTexture, sensorData.bodyIndexMaterial);

			if(userMapType != KinectManager.UserMapType.UserTexture || !sensorData.depthImageBufferReady)
			{
				// buffer is released
				lock(sensorData.bodyIndexBufferLock)
				{
					sensorData.bodyIndexBufferReady = false;
				}
			}
		}
	}

	// Renders depth image texture
	public static void RenderDepthImageTexture(SensorData sensorData)
	{
		if(sensorData.depthImageBufferReady)
		{
			sensorData.depthImageMaterial.SetFloat("_TotalPoints", (float)sensorData.depthHistTotalPoints);
			sensorData.depthImageMaterial.SetInt("_FirstUserIndex", sensorData.firstUserIndex);
			sensorData.depthImageBuffer.SetData(sensorData.depthImageBufferData);
			sensorData.depthHistBuffer.SetData(sensorData.equalHistBufferData);
			
			Graphics.Blit(sensorData.bodyIndexTexture, sensorData.depthImageTexture, sensorData.depthImageMaterial);
			
			// release the buffers for the next poll
			lock(sensorData.depthImageBufferLock)
			{
				sensorData.depthImageBufferReady = false;
			}

			lock(sensorData.bodyIndexBufferLock)
			{
				sensorData.bodyIndexBufferReady = false;
			}
		}
	}

	// renders depth2color texture
	public static bool RenderDepth2ColorTex(SensorData sensorData)
	{
		if(sensorData.depth2ColorMaterial != null && sensorData.depth2ColorCoords != null && sensorData.depthCoordsBufferReady)
		{
			sensorData.depth2ColorBuffer.SetData(sensorData.depth2ColorCoords);
			
			sensorData.depth2ColorMaterial.SetTexture("_BodyTex", sensorData.bodyIndexTexture);
			sensorData.depth2ColorMaterial.SetTexture("_ColorTex", sensorData.colorImageTexture2D ? sensorData.colorImageTexture2D : sensorData.colorImageTexture);
			
			Graphics.Blit(null, sensorData.depth2ColorTexture, sensorData.depth2ColorMaterial);
			
			// buffer is released
			lock(sensorData.depthCoordsBufferLock)
			{
				sensorData.depthCoordsBufferReady = false;
			}

			return true;
		}

		return false;
	}

	// Polls for new infrared frame data
	public static bool PollInfraredFrame(SensorData sensorData)
	{
		bool bNewFrame = false;

		if(sensorData.sensorInterface != null && !sensorData.isPlayModeEnabled)
		{
			bNewFrame = sensorData.sensorInterface.PollInfraredFrame(sensorData);
		}

		return bNewFrame;
	}

	// returns depth frame coordinates for the given 3d Kinect-space point
	public static Vector2 MapSpacePointToDepthCoords(SensorData sensorData, Vector3 kinectPos)
	{
		Vector2 vPoint = Vector2.zero;

		if(sensorData.sensorInterface != null)
		{
			vPoint = sensorData.sensorInterface.MapSpacePointToDepthCoords(sensorData, kinectPos);
		}

		return vPoint;
	}

	// returns 3d coordinates for the given depth-map point
	public static Vector3 MapDepthPointToSpaceCoords(SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		Vector3 vPoint = Vector3.zero;

		if (sensorData.depth2SpaceCoords != null) 
		{
			int pIndex = (int)depthPos.y * sensorData.depthImageWidth + (int)depthPos.x;
			if (pIndex >= 0 && pIndex < sensorData.depth2SpaceCoords.Length)
				return sensorData.depth2SpaceCoords[pIndex];
		}

		if(sensorData.sensorInterface != null)
		{
			vPoint = sensorData.sensorInterface.MapDepthPointToSpaceCoords(sensorData, depthPos, depthVal);
		}

		return vPoint;
	}

	// estimates space coordinates for the current depth frame
	public static bool MapDepthFrameToSpaceCoords(SensorData sensorData, ref Vector3[] vSpaceCoords)
	{
		bool bResult = false;
		
		if(sensorData.sensorInterface != null)
		{
			bResult = sensorData.sensorInterface.MapDepthFrameToSpaceCoords(sensorData, ref vSpaceCoords);
		}
		
		return bResult;
	}
	
	// returns color-map coordinates for the given depth point
	public static Vector2 MapDepthPointToColorCoords(SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		Vector2 vPoint = Vector2.zero;

		if (sensorData.depth2ColorCoords != null &&
			(sensorData.sensorIntPlatform == DepthSensorPlatform.KinectSDKv2 || sensorData.sensorIntPlatform == DepthSensorPlatform.KinectUWPv2))
		{
			int pIndex = (int)depthPos.y * sensorData.depthImageWidth + (int)depthPos.x;
			if (pIndex >= 0 && pIndex < sensorData.depth2ColorCoords.Length)
				return sensorData.depth2ColorCoords[pIndex];
		}

		if(sensorData.sensorInterface != null)
		{
			vPoint = sensorData.sensorInterface.MapDepthPointToColorCoords(sensorData, depthPos, depthVal);
		}

		return vPoint;
	}

	// estimates color-map coordinates for the current depth frame
	public static bool MapDepthFrameToColorCoords(SensorData sensorData, ref Vector2[] vColorCoords)
	{
		bool bResult = false;
		
		if(sensorData.sensorInterface != null)
		{
			bResult = sensorData.sensorInterface.MapDepthFrameToColorCoords(sensorData, ref vColorCoords);
		}
		
		return bResult;
	}
	
	// estimates depth-map coordinates for the current color frame
	public static bool MapColorFrameToDepthCoords(SensorData sensorData, ref Vector2[] vDepthCoords)
	{
		bool bResult = false;
		
		if(sensorData.sensorInterface != null)
		{
			bResult = sensorData.sensorInterface.MapColorFrameToDepthCoords(sensorData, ref vDepthCoords);
		}
		
		return bResult;
	}

	// estimates depth-map coordinates for the given color coords
	public static Vector2 MapColorPointToDepthCoords(SensorData sensorData, Vector2 colorPos, bool bReadDepthCoordsIfNeeded)
	{
		Vector2 vPoint = Vector2.zero;

		if(sensorData.sensorInterface != null && !float.IsInfinity(colorPos.x) && !float.IsInfinity(colorPos.y))
		{
			int cIndex = (int)colorPos.y * sensorData.colorImageWidth + (int)colorPos.x;

			if(sensorData.color2DepthCoords != null &&
				(sensorData.sensorIntPlatform == DepthSensorPlatform.KinectSDKv2 || sensorData.sensorIntPlatform == DepthSensorPlatform.KinectUWPv2))
			{
				if (cIndex >= 0 && cIndex < sensorData.color2DepthCoords.Length) 
				{
					vPoint = sensorData.color2DepthCoords[cIndex];
				}
			}
			else if(bReadDepthCoordsIfNeeded)
			{
				Vector2[] vDepthCoords = new Vector2[sensorData.colorImageWidth * sensorData.colorImageHeight];

				if(MapColorFrameToDepthCoords(sensorData, ref vDepthCoords))
				{
					if (cIndex >= 0 && cIndex < vDepthCoords.Length) 
					{
						vPoint = vDepthCoords[cIndex];
					}
				}

				vDepthCoords = null;
			}
		}
		
		return vPoint;
	}

	// draws a rectangle on texture-2d
	public static void DrawRect(Texture2D a_Texture, Rect a_rect, Color a_Color)
	{
		Vector2 pt1, pt2;

		// bottom
		pt1.x = a_rect.x; pt1.y = a_rect.y;
		pt2.x = a_rect.x + a_rect.width - 1; pt2.y = pt1.y;
		DrawLine(a_Texture, (int)pt1.x, (int)pt1.y, (int)pt2.x, (int)pt2.y, a_Color);

		// right
		pt1.x = pt2.x; pt1.y = pt2.y;
		pt2.x = pt1.x; pt2.y = a_rect.y + a_rect.height - 1;
		DrawLine(a_Texture, (int)pt1.x, (int)pt1.y, (int)pt2.x, (int)pt2.y, a_Color);

		// top
		pt1.x = pt2.x; pt1.y = pt2.y;
		pt2.x = a_rect.x; pt2.y = pt1.y;
		DrawLine(a_Texture, (int)pt1.x, (int)pt1.y, (int)pt2.x, (int)pt2.y, a_Color);

		// left
		pt1.x = pt2.x; pt1.y = pt2.y;
		pt2.x = pt1.x; pt2.y = a_rect.y;
		DrawLine(a_Texture, (int)pt1.x, (int)pt1.y, (int)pt2.x, (int)pt2.y, a_Color);
	}

	// draws a line on texture-2d
	public static void DrawLine(Texture2D a_Texture, int x1, int y1, int x2, int y2, Color a_Color)
	{
		int width = a_Texture.width;
		int height = a_Texture.height;
		
		int dy = y2 - y1;
		int dx = x2 - x1;
		
		int stepy = 1;
		if (dy < 0) 
		{
			dy = -dy; 
			stepy = -1;
		}
		
		int stepx = 1;
		if (dx < 0) 
		{
			dx = -dx; 
			stepx = -1;
		}
		
		dy <<= 1;
		dx <<= 1;
		
		if(x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
			for(int x = -1; x <= 1; x++)
				for(int y = -1; y <= 1; y++)
					a_Texture.SetPixel(x1 + x, y1 + y, a_Color);
		
		if (dx > dy) 
		{
			int fraction = dy - (dx >> 1);
			
			while (x1 != x2) 
			{
				if (fraction >= 0) 
				{
					y1 += stepy;
					fraction -= dx;
				}
				
				x1 += stepx;
				fraction += dy;
				
				if(x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
					for(int x = -1; x <= 1; x++)
						for(int y = -1; y <= 1; y++)
							a_Texture.SetPixel(x1 + x, y1 + y, a_Color);
			}
		}
		else 
		{
			int fraction = dx - (dy >> 1);
			
			while (y1 != y2) 
			{
				if (fraction >= 0) 
				{
					x1 += stepx;
					fraction -= dy;
				}
				
				y1 += stepy;
				fraction += dx;
				
				if(x1 >= 0 && x1 < width && y1 >= 0 && y1 < height)
					for(int x = -1; x <= 1; x++)
						for(int y = -1; y <= 1; y++)
							a_Texture.SetPixel(x1 + x, y1 + y, a_Color);
			}
		}
		
	}

	// copy source file to the target
	public static bool CopyFile(string sourceFilePath, string targetFilePath, ref bool bOneCopied, ref bool bAllCopied)
	{
#if !UNITY_WSA
		FileInfo sourceFile = new FileInfo(sourceFilePath);
		if(!sourceFile.Exists)
		{
			return false;
		}

		FileInfo targetFile = new FileInfo(targetFilePath);
		if(!targetFile.Directory.Exists)
		{
			targetFile.Directory.Create();
		}
		
		if(!targetFile.Exists || targetFile.Length !=  sourceFile.Length)
		{
			Debug.Log("Copying " + sourceFile.Name + "...");
			File.Copy(sourceFilePath, targetFilePath);
			
			bool bFileCopied = File.Exists(targetFilePath);
			
			bOneCopied = bOneCopied || bFileCopied;
			bAllCopied = bAllCopied && bFileCopied;
			
			return bFileCopied;
		}
#endif

		return false;
	}
	
	// Copy a resource file to the target
	public static bool CopyResourceFile(string targetFilePath, string resFileName, ref bool bOneCopied, ref bool bAllCopied)
	{
#if !UNITY_WSA
		TextAsset textRes = Resources.Load(resFileName, typeof(TextAsset)) as TextAsset;
		if(textRes == null)
		{
			bOneCopied = false;
			bAllCopied = false;
			
			return false;
		}
		
		FileInfo targetFile = new FileInfo(targetFilePath);
		if(!targetFile.Directory.Exists)
		{
			targetFile.Directory.Create();
		}
		
		if(!targetFile.Exists || targetFile.Length !=  textRes.bytes.Length)
		{
			Debug.Log("Copying " + resFileName + "...");

			if(textRes != null)
			{
				using (FileStream fileStream = new FileStream (targetFilePath, FileMode.Create, FileAccess.Write, FileShare.Read))
				{
					fileStream.Write(textRes.bytes, 0, textRes.bytes.Length);
				}
				
				bool bFileCopied = File.Exists(targetFilePath);
				
				bOneCopied = bOneCopied || bFileCopied;
				bAllCopied = bAllCopied && bFileCopied;
				
				return bFileCopied;
			}
		}
#endif

		return false;
	}

	// Unzips resource file to the target path
	public static bool UnzipResourceDirectory(string targetDirPath, string resZipFileName, string checkForDir)
	{
#if (UNITY_STANDALONE_WIN)
		if(checkForDir != string.Empty && Directory.Exists(checkForDir))
		{
			return false;
		}

		TextAsset textRes = Resources.Load(resZipFileName, typeof(TextAsset)) as TextAsset;
		if(textRes == null || textRes.bytes.Length == 0)
		{
			return false;
		}

		Debug.Log("Unzipping " + resZipFileName + "...");

		// get the resource steam
		MemoryStream memStream = new MemoryStream(textRes.bytes);

		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;

		using(ZipInputStream s = new ZipInputStream(memStream))
		{
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry()) != null) 
			{
				//Debug.Log(theEntry.Name);
				
				string directoryName = targetDirPath + Path.GetDirectoryName(theEntry.Name);
				string fileName = Path.GetFileName(theEntry.Name);

				if(!Directory.Exists(directoryName))
				{
					// create directory
					Directory.CreateDirectory(directoryName);
				}

				if (fileName != string.Empty && !fileName.EndsWith(".meta")) 
				{
					string targetFilePath = directoryName + "/" + fileName;

					using (FileStream streamWriter = File.Create(targetFilePath)) 
					{
						int size = 2048;
						byte[] data = new byte[2048];
						
						while (true) 
						{
							size = s.Read(data, 0, data.Length);
							
							if (size > 0) 
							{
								streamWriter.Write(data, 0, size);
							} 
							else 
							{
								break;
							}
						}
					}
				}
			}
		}

		// close the resource stream
		//memStream.Close();
		memStream.Dispose();

		return true;
#else
		return false;
#endif	
	}

	// Unzips resource file to the target path
	public static bool UnzipResourceFiles(Dictionary<string, string> dictFilesToUnzip, string resZipFileName, 
	                                      ref bool bOneCopied, ref bool bAllCopied)
	{
#if (UNITY_STANDALONE_WIN)		
		TextAsset textRes = Resources.Load(resZipFileName, typeof(TextAsset)) as TextAsset;
		if(textRes == null || textRes.bytes.Length == 0)
		{
			bOneCopied = false;
			bAllCopied = false;

			return false;
		}
		
		//Debug.Log("Unzipping " + resZipFileName + "...");
		
		// get the resource steam
		MemoryStream memStream = new MemoryStream(textRes.bytes);
		
		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;
		
		using(ZipInputStream s = new ZipInputStream(memStream))
		{
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry()) != null) 
			{
				//Debug.Log(theEntry.Name);

				if(dictFilesToUnzip.ContainsKey(theEntry.Name))
			   	{
					string targetFilePath = dictFilesToUnzip[theEntry.Name];

					string directoryName = Path.GetDirectoryName(targetFilePath);
					string fileName = Path.GetFileName(theEntry.Name);
					
					if(!Directory.Exists(directoryName))
					{
						// create directory
						Directory.CreateDirectory(directoryName);
					}

					FileInfo targetFile = new FileInfo(targetFilePath);
					bool bTargetFileNewOrUpdated = !targetFile.Exists || targetFile.Length !=  theEntry.Size;
					
					if (fileName != string.Empty && bTargetFileNewOrUpdated) 
					{
						using (FileStream streamWriter = File.Create(targetFilePath)) 
						{
							int size = 2048;
							byte[] data = new byte[2048];
							
							while (true) 
							{
								size = s.Read(data, 0, data.Length);
								
								if (size > 0) 
								{
									streamWriter.Write(data, 0, size);
								} 
								else 
								{
									break;
								}
							}
						}
						
						bool bFileCopied = File.Exists(targetFilePath);
						
						bOneCopied = bOneCopied || bFileCopied;
						bAllCopied = bAllCopied && bFileCopied;
					}
				}

			}
		}
		
		// close the resource stream
		//memStream.Close();
		memStream.Dispose();
		
		return true;
#else
		return false;
#endif
	}
	
	// returns the unzipped file size in bytes, or -1 if the entry is not found in the zip
	public static long GetUnzippedEntrySize(string resZipFileName, string sEntryName)
	{
#if (UNITY_STANDALONE_WIN)
		TextAsset textRes = Resources.Load(resZipFileName, typeof(TextAsset)) as TextAsset;
		if(textRes == null || textRes.bytes.Length == 0)
		{
			return -1;
		}
		
		// get the resource steam
		MemoryStream memStream = new MemoryStream(textRes.bytes);
		
		// fix invalid code page 437 error
		ZipConstants.DefaultCodePage = 0;
		long entryFileSize = -1;
		
		using(ZipInputStream s = new ZipInputStream(memStream))
		{
			ZipEntry theEntry;
			while ((theEntry = s.GetNextEntry()) != null) 
			{
				if(theEntry.Name == sEntryName)
				{
					entryFileSize = theEntry.Size;
					break;
				}
				
			}
		}
		
		// close the resource stream
		//memStream.Close();
		memStream.Dispose();
		
		return entryFileSize;
#else
		return -1;
#endif
	}
	
	// returns true if the project is running on 64-bit architecture, false if 32-bit
	public static bool Is64bitArchitecture()
	{
		int sizeOfPtr = Marshal.SizeOf(typeof(IntPtr));
		return (sizeOfPtr > 4);
	}

	// returns the target dll path for the current platform (x86 or x64)
	public static string GetTargetDllPath(string sAppPath, bool bIs64bitApp)
	{
		string sTargetPath = sAppPath;
//		string sPluginsPath = Application.dataPath + "/Plugins";
//		
//		if(Directory.Exists(sPluginsPath))
//		{
//			sTargetPath = sPluginsPath;
//			
//			//if(Application.isEditor)
//			{
//				string sPlatformPath = sPluginsPath + "/" + (!bIs64bitApp ? "x86" : "x86_64");
//				
//				if(Directory.Exists(sPlatformPath))
//				{
//					sTargetPath = sPlatformPath;
//				}
//			}
//		}
		
		return sTargetPath;
	}

	// cleans up objects and restarts the current level
	public static void RestartLevel(GameObject parentObject, string callerName)
	{
		Debug.Log(callerName + " is restarting level...");

		// destroy parent object if any
		if(parentObject)
		{
			GameObject.Destroy(parentObject);
		}

		// clean up memory assets
		Resources.UnloadUnusedAssets();
		GC.Collect();

		//if(Application.HasProLicense() && Application.isEditor)
		{
#if UNITY_EDITOR
			// refresh the assets database
			UnityEditor.AssetDatabase.Refresh();
#endif
		}
		
		// reload the same level
		SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
		//SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
	}

	// sets the graphics shader level
	public static void SetGraphicsShaderLevel(int shaderLevel)
	{
		graphicsShaderLevel = shaderLevel;
	}

	// checks if DirectX11/Direct3D-11 is turned on or not
	public static bool IsDirectX11Available()
	{
		return (graphicsShaderLevel >= 50);
	}

	// copies open-cv dlls to the root folder, if needed
	public static bool IsOpenCvAvailable(ref bool bNeedRestart)
	{
		bNeedRestart = false;

		if(IsDirectX11Available())
		{
			// use shaders
			return true;
		}

//		bool bOneCopied = false, bAllCopied = true;
//		string sTargetPath = GetTargetDllPath(".", Is64bitArchitecture()) + "/";
//		//string sTargetPath = ".";
//		
//		if(!Is64bitArchitecture())
//		{
//			// 32 bit architecture
//			sTargetPath = GetTargetDllPath(".", false) + "/";
//			
//			Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string>();
//			dictFilesToUnzip["opencv_core2410.dll"] = sTargetPath + "opencv_core2410.dll";
//			dictFilesToUnzip["opencv_imgproc2410.dll"] = sTargetPath + "opencv_imgproc2410.dll";
//			dictFilesToUnzip["msvcp120.dll"] = sTargetPath + "msvcp120.dll";
//			dictFilesToUnzip["msvcr120.dll"] = sTargetPath + "msvcr120.dll";
//			
//			UnzipResourceFiles(dictFilesToUnzip, "opencv.x86.zip", ref bOneCopied, ref bAllCopied);
//		}
//		else
//		{
//			// 64 bit architecture
//			sTargetPath = GetTargetDllPath(".", true) + "/";
//			
//			Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string>();
//			dictFilesToUnzip["opencv_core2410.dll"] = sTargetPath + "opencv_core2410.dll";
//			dictFilesToUnzip["opencv_imgproc2410.dll"] = sTargetPath + "opencv_imgproc2410.dll";
//			dictFilesToUnzip["msvcp120.dll"] = sTargetPath + "msvcp120.dll";
//			dictFilesToUnzip["msvcr120.dll"] = sTargetPath + "msvcr120.dll";
//			
//			UnzipResourceFiles(dictFilesToUnzip, "opencv.x64.zip", ref bOneCopied, ref bAllCopied);
//		}
//
//		bNeedRestart = (bOneCopied && bAllCopied);
		
		return true;
	}

	// initializes background removal with shaders
	public static bool InitBackgroundRemoval(SensorData sensorData, bool isHiResPrefered)
	{
		if(sensorData != null && sensorData.bodyIndexImage != null && sensorData.colorImage != null
		   && IsDirectX11Available())
		{
			Shader erodeBodyShader = Shader.Find("Custom/Erode");
			sensorData.erodeBodyMaterial = new Material(erodeBodyShader);
			sensorData.erodeBodyMaterial.SetFloat("_TexResX", (float)sensorData.depthImageWidth);
			sensorData.erodeBodyMaterial.SetFloat("_TexResY", (float)sensorData.depthImageHeight);
			//sensorData.erodeBodyMaterial.SetTexture("_MainTex", sensorData.bodyIndexTexture);
			
			Shader dilateBodyShader = Shader.Find("Custom/Dilate");
			sensorData.dilateBodyMaterial = new Material(dilateBodyShader);
			sensorData.dilateBodyMaterial.SetFloat("_TexResX", (float)sensorData.depthImageWidth);
			sensorData.dilateBodyMaterial.SetFloat("_TexResY", (float)sensorData.depthImageHeight);
			//sensorData.dilateBodyMaterial.SetTexture("_MainTex", sensorData.bodyIndexTexture);

			Shader medianBodyShader = Shader.Find("Custom/Median5x5");
			sensorData.medianBodyMaterial = new Material(medianBodyShader);
			sensorData.medianBodyMaterial.SetFloat("_Amount", 1.0f);

			Shader blurBodyShader = Shader.Find("Custom/BlurShader5");
			sensorData.blurBodyMaterial = new Material(blurBodyShader);
			//sensorData.blurBodyMaterial.SetFloat("_Amount", 1.5f);
			//sensorData.blurBodyMaterial.SetFloat("_BlurSizeXY", 2f);

			if(isHiResPrefered)
			{
                if (sensorData.alphaBodyTexture == null || sensorData.alphaBodyTexture.width != sensorData.colorImageWidth || sensorData.alphaBodyTexture.height != sensorData.colorImageHeight)
                {
                    sensorData.alphaBodyTexture = new RenderTexture(sensorData.colorImageWidth, sensorData.colorImageHeight, 0);
                    sensorData.alphaBodyTexture.wrapMode = TextureWrapMode.Clamp;
                    sensorData.alphaBodyTexture.filterMode = FilterMode.Point;
                }

                Shader alphaBodyShader = Shader.Find("Kinect/Color2BodyShader");
				if(alphaBodyShader)
				{
					sensorData.alphaBodyMaterial = new Material(alphaBodyShader);
					
					sensorData.alphaBodyMaterial.SetFloat("_ColorResX", (float)sensorData.colorImageWidth);
					sensorData.alphaBodyMaterial.SetFloat("_ColorResY", (float)sensorData.colorImageHeight);
					sensorData.alphaBodyMaterial.SetFloat("_DepthResX", (float)sensorData.depthImageWidth);
					sensorData.alphaBodyMaterial.SetFloat("_DepthResY", (float)sensorData.depthImageHeight);
					
					sensorData.color2DepthBuffer = new ComputeBuffer(sensorData.colorImageWidth * sensorData.colorImageHeight, sizeof(float) * 2);
					sensorData.alphaBodyMaterial.SetBuffer("_DepthCoords", sensorData.color2DepthBuffer);
				}

				Shader color2DepthShader = !sensorData.invertAlphaColorMask ? Shader.Find("Kinect/Color2DepthShader") : Shader.Find("Kinect/Color2DepthShaderInv");
				if(color2DepthShader)
				{
                    if (sensorData.color2DepthTexture == null || sensorData.color2DepthTexture.width != sensorData.colorImageWidth || sensorData.color2DepthTexture.height != sensorData.colorImageHeight)
                    {
                        sensorData.color2DepthTexture = new RenderTexture(sensorData.colorImageWidth, sensorData.colorImageHeight, 0);
                        sensorData.color2DepthTexture.wrapMode = TextureWrapMode.Clamp;
                        sensorData.color2DepthTexture.filterMode = FilterMode.Point;
                    }

                    sensorData.color2DepthMaterial = new Material(color2DepthShader);
					
//					sensorData.color2DepthMaterial.SetFloat("_ColorResX", (float)sensorData.colorImageWidth);
//					sensorData.color2DepthMaterial.SetFloat("_ColorResY", (float)sensorData.colorImageHeight);
//					sensorData.color2DepthMaterial.SetFloat("_DepthResX", (float)sensorData.depthImageWidth);
//					sensorData.color2DepthMaterial.SetFloat("_DepthResY", (float)sensorData.depthImageHeight);
//					
//					sensorData.color2DepthBuffer = new ComputeBuffer(sensorData.colorImageWidth * sensorData.colorImageHeight, sizeof(float) * 2);
//					sensorData.color2DepthMaterial.SetBuffer("_DepthCoords", sensorData.color2DepthBuffer);
				}
			}
			else
			{
				sensorData.alphaBodyTexture = new RenderTexture(sensorData.depthImageWidth, sensorData.depthImageHeight, 0);
				sensorData.alphaBodyTexture.wrapMode = TextureWrapMode.Clamp;
				//sensorData.alphaBodyTexture.filterMode = FilterMode.Point;
			}
		}

		if(isHiResPrefered && sensorData != null && sensorData.bodyIndexImage != null && sensorData.colorImage != null)
		{
			sensorData.color2DepthCoords = new Vector2[sensorData.colorImageWidth * sensorData.colorImageHeight];
		}

        sensorData.backgroundRemovalInited = true;
        sensorData.backgroundRemovalHiRes = isHiResPrefered;

		return true;
	}

	// releases background removal shader resources
	public static void FinishBackgroundRemoval(SensorData sensorData)
	{
		if(sensorData == null)
			return;

		if(sensorData.alphaBodyTexture != null)
		{
			sensorData.alphaBodyTexture.Release();
			sensorData.alphaBodyTexture = null;
		}
		
		sensorData.erodeBodyMaterial = null;
		sensorData.dilateBodyMaterial = null;
		sensorData.medianBodyMaterial = null;
		sensorData.blurBodyMaterial = null;

		if(sensorData.color2DepthBuffer != null)
		{
			sensorData.color2DepthBuffer.Release();
			sensorData.color2DepthBuffer = null;
		}

		if(sensorData.color2DepthTexture != null)
		{
			sensorData.color2DepthTexture.Release();
			sensorData.color2DepthTexture = null;
		}

		sensorData.alphaBodyMaterial = null;
		sensorData.color2DepthMaterial = null;
		sensorData.color2DepthCoords = null;

        sensorData.backgroundRemovalInited = false;
    }

	// computes current background removal texture
	public static bool UpdateBackgroundRemoval(SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bAlphaTexOnly)
	{
		if(sensorData.color2DepthMaterial != null && sensorData.color2DepthCoords != null && sensorData.depthCoordsBufferReady)
		{
			if(sensorData.alphaBodyMaterial != null && sensorData.alphaBodyTexture)
			{
				sensorData.color2DepthBuffer.SetData(sensorData.color2DepthCoords);
				
				sensorData.alphaBodyMaterial.SetTexture("_BodyTex", sensorData.bodyIndexTexture);
				Graphics.Blit(null, sensorData.alphaBodyTexture, sensorData.alphaBodyMaterial);
				
				if(sensorData.erodeBodyMaterial != null && sensorData.dilateBodyMaterial != null && sensorData.blurBodyMaterial)
				{
					ApplyErodeDilate(sensorData.alphaBodyTexture, sensorData.alphaBodyTexture, sensorData.erodeBodyMaterial, 
 					                 sensorData.dilateBodyMaterial, sensorData.erodeIterations, sensorData.dilateIterations);

					if (sensorData.applyMedianFilter && sensorData.medianBodyMaterial) 
					{
						ApplyMedianFilter(sensorData.alphaBodyTexture, sensorData.alphaBodyTexture, sensorData.medianBodyMaterial);
					}

					ApplyImageBlur(sensorData.alphaBodyTexture, sensorData.alphaBodyTexture, sensorData.blurBodyMaterial, 1, 0.6f);
				}
			}

			// blit the hi-res texture
			if(!bAlphaTexOnly)
			{
				//sensorData.color2DepthBuffer.SetData(sensorData.color2DepthCoords);
				
				sensorData.color2DepthMaterial.SetTexture("_BodyTex", sensorData.alphaBodyTexture);
				sensorData.color2DepthMaterial.SetTexture("_ColorTex", sensorData.colorImageTexture2D ? sensorData.colorImageTexture2D : sensorData.colorImageTexture);
				//sensorData.color2DepthMaterial.SetColor("_DefaultClr", defaultColor);
				
				Graphics.Blit(null, sensorData.color2DepthTexture, sensorData.color2DepthMaterial);
			}

			// buffer is released
			lock(sensorData.depthCoordsBufferLock)
			{
				sensorData.depthCoordsBufferReady = false;
			}
		}
		else if(sensorData.depth2ColorMaterial != null && sensorData.depth2ColorCoords != null && sensorData.depthCoordsBufferReady)
		{
			if(sensorData.erodeBodyMaterial != null && sensorData.dilateBodyMaterial != null && sensorData.blurBodyMaterial)
			{
				ApplyErodeDilate(sensorData.bodyIndexTexture, sensorData.alphaBodyTexture, sensorData.erodeBodyMaterial, 
				                 sensorData.dilateBodyMaterial, sensorData.erodeIterations, sensorData.dilateIterations);
				
				if (sensorData.applyMedianFilter && sensorData.medianBodyMaterial) 
				{
					ApplyMedianFilter(sensorData.alphaBodyTexture, sensorData.alphaBodyTexture, sensorData.medianBodyMaterial);
				}

				ApplyImageBlur(sensorData.alphaBodyTexture, sensorData.alphaBodyTexture, sensorData.blurBodyMaterial, 0, 0.6f);
			}

			// blit the lo-res texture
			if(!bAlphaTexOnly)
			{
				sensorData.depth2ColorBuffer.SetData(sensorData.depth2ColorCoords);
				
				sensorData.depth2ColorMaterial.SetTexture("_BodyTex", sensorData.alphaBodyTexture);
				sensorData.depth2ColorMaterial.SetTexture("_ColorTex", sensorData.colorImageTexture2D ? sensorData.colorImageTexture2D : sensorData.colorImageTexture);
				//sensorData.depth2ColorMaterial.SetColor("_DefaultClr", defaultColor);
				
				Graphics.Blit(null, sensorData.depth2ColorTexture, sensorData.depth2ColorMaterial);
			}

			// buffer is released
			lock(sensorData.depthCoordsBufferLock)
			{
				sensorData.depthCoordsBufferReady = false;
			}
		}
		
		return true;
	}

	private static void ApplyErodeDilate(RenderTexture source, RenderTexture destination, Material erodeMaterial, 
	                                     Material dilateMaterial, int erodeIterations, int dilateIterations)
	{
		if(!source || !destination || !erodeMaterial || !dilateMaterial)
			return;

		RenderTexture[] tempTexture = new RenderTexture[2];
		tempTexture[0] = RenderTexture.GetTemporary(source.width, source.height, 0);
		tempTexture[1] = RenderTexture.GetTemporary(source.width, source.height, 0);

		Graphics.Blit(source, tempTexture[0]);

		for(int i = 0; i < erodeIterations; i++)
		{
			Graphics.Blit(tempTexture[i % 2], tempTexture[(i + 1) % 2], erodeMaterial);
		}

		if((erodeIterations % 2) != 0)
		{
			Graphics.Blit(tempTexture[1], tempTexture[0]);
		}
		
		for(int i = 0; i < dilateIterations; i++)
		{
			Graphics.Blit(tempTexture[i % 2], tempTexture[(i + 1) % 2], dilateMaterial);
		}

		Graphics.Blit(tempTexture[dilateIterations % 2], destination);

		RenderTexture.ReleaseTemporary(tempTexture[0]);
		RenderTexture.ReleaseTemporary(tempTexture[1]);
	}

	private static void ApplyMedianFilter(RenderTexture source, RenderTexture destination, Material medianMaterial)
	{
		if(!source || !destination || !medianMaterial)
			return;

		RenderTexture buffer = RenderTexture.GetTemporary(source.width, source.height, 0);
		Graphics.Blit(source, buffer, medianMaterial);

		Graphics.Blit(buffer, destination);
		RenderTexture.ReleaseTemporary(buffer);
	}

	private static void ApplyImageBlur(RenderTexture source, RenderTexture destination, Material blurMaterial, int blurIterations, float blurSpread)
	{
		if(!source || !destination || !blurMaterial)
			return;

//		Graphics.Blit(source, destination, blurMaterial);
//		return;

		int rtW = source.width / 4;
		int rtH = source.height / 4;
		RenderTexture buffer = RenderTexture.GetTemporary(rtW, rtH, 0);

		// Copy source to the 4x4 smaller texture.
		Downsample4x(source, buffer, blurMaterial);

		// Blur the small texture
		for(int i = 0; i < blurIterations; i++)
		{
			RenderTexture buffer2 = RenderTexture.GetTemporary(rtW, rtH, 0);
			FourTapCone(buffer, buffer2, blurMaterial, i, blurSpread);
			RenderTexture.ReleaseTemporary(buffer);
			buffer = buffer2;
		}

		Graphics.Blit(buffer, destination);
		RenderTexture.ReleaseTemporary(buffer);
	}

	// downsamples the texture to a quarter resolution.
	private static void Downsample4x(RenderTexture source, RenderTexture dest, Material material)
	{
		float off = 1.0f;

		Graphics.BlitMultiTap (source, dest, material,
			new Vector2(-off, -off),
			new Vector2(-off,  off),
			new Vector2( off,  off),
			new Vector2( off, -off)
		);
	}

	// performs one blur iteration.
	private static void FourTapCone (RenderTexture source, RenderTexture dest, Material material, int iteration, float blurSpread)
	{
		float off = 0.5f + iteration * blurSpread;

		Graphics.BlitMultiTap (source, dest, material,
			new Vector2(-off, -off),
			new Vector2(-off,  off),
			new Vector2( off,  off),
			new Vector2( off, -off)
		);
	}

	// returns the foregound frame rectangle, as to the required resolution
	public static Rect GetForegroundFrameRect(SensorData sensorData, bool isHiResPrefered)
	{
		if(isHiResPrefered && sensorData != null && sensorData.sensorInterface != null)
		{
			if(sensorData.sensorInterface.IsBRHiResSupported() && sensorData.colorImage != null)
			{
				return new Rect(0f, 0f, sensorData.colorImageWidth, sensorData.colorImageHeight);
			}
		}
		
		return sensorData != null ? new Rect(0f, 0f, sensorData.depthImageWidth, sensorData.depthImageHeight) : new Rect();
	}

	// returns the foregound frame length, as to the required resolution
	public static int GetForegroundFrameLength(SensorData sensorData, bool isHiResPrefered)
	{
		if(isHiResPrefered && sensorData != null && sensorData.sensorInterface != null)
		{
			if(sensorData.sensorInterface.IsBRHiResSupported() && sensorData.colorImage != null)
			{
				return sensorData.colorImage.Length;
			}
		}

		return (sensorData != null && sensorData.bodyIndexImage != null) ? sensorData.bodyIndexImage.Length * 4 : 0;
	}
	
	private static bool GetForegroundAlphaFrame(SensorData sensorData, bool bLimitedUsers, ICollection<int> alTrackedIndexes, ref byte[] fgAlphaFrame)
	{
		if(sensorData == null || sensorData.bodyIndexImage == null)
			return false;

//		CvMat cvAlphaMap = new CvMat(sensorData.depthImageHeight, sensorData.depthImageWidth, MatrixType.U8C1);
//
//		System.IntPtr rawPtrAlpha;
//		cvAlphaMap.GetRawData(out rawPtrAlpha);
//
//		if(sensorData.selectedBodyIndex != 255 || bLimitedUsers)
//		{
//			// copy body-index selectively
//			byte btSelBI = sensorData.selectedBodyIndex;
//			int iBodyIndexLength = sensorData.bodyIndexImage.Length;
//
//			for (int i = 0; i < iBodyIndexLength; i++)
//			{
//				byte btBufBI = sensorData.bodyIndexImage[i];
//
//				bool bUserTracked = btSelBI != 255 ? btSelBI == btBufBI : 
//					(btBufBI != 255 ? alTrackedIndexes.Contains((int)btBufBI) : false);
//
//				if(bUserTracked)
//				{
//					cvAlphaMap.Set1D(i, btBufBI);
//				}
//				else
//				{
//					cvAlphaMap.Set1D(i, 255);
//				}
//			}
//		}
//		else
//		{
//			// copy the entire body-index buffer
//			Marshal.Copy(sensorData.bodyIndexImage, 0, rawPtrAlpha, sensorData.bodyIndexImage.Length);
//		}
//
//		// make the image b&w
//		cvAlphaMap.Threshold(cvAlphaMap, 254, 255, ThresholdType.BinaryInv);
//
//		// apply erode, dilate and blur
//		cvAlphaMap.Erode(cvAlphaMap);
//		cvAlphaMap.Dilate(cvAlphaMap);
//		cvAlphaMap.Smooth(cvAlphaMap, SmoothType.Blur, 5, 5);
//		//cvAlphaMap.Smooth(cvAlphaMap, SmoothType.Median, 7);
//
//		// get the foreground image
//		Marshal.Copy(rawPtrAlpha, fgAlphaFrame, 0, fgAlphaFrame.Length);
//
//		return true;
		return false;
	}
	
	// gets the updated foreground frame, as to the required resolution
	public static bool PollForegroundFrame(SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bLimitedUsers, ICollection<int> alTrackedIndexes, ref byte[] fgImageFrame)
	{
//		if(IsDirectX11Available())
//			return false;
//
//		if(sensorData.colorImage == null)
//			return false;
//
//		// get the alpha frame
//		byte[] fgAlphaFrame = new byte[sensorData.bodyIndexImage.Length];
//		if(!GetForegroundAlphaFrame(sensorData, bLimitedUsers, alTrackedIndexes, ref fgAlphaFrame))
//			return false;
//		
//		int alphaImageLength = fgAlphaFrame.Length;
//		int colorImageLength = sensorData.colorImageWidth * sensorData.colorImageHeight;
//		
//		Array.Clear(fgImageFrame, 0, fgImageFrame.Length);
//		
//		// try to get the full color frame coordinates
//		if(isHiResPrefered && sensorData.color2DepthCoords != null && sensorData.depthCoordsBufferReady)
//		{
//			for (int i = 0, fi = 0; i < colorImageLength; i++, fi += 4)
//			{
//				Vector2 vDepthPos = sensorData.color2DepthCoords[i];
//
//				if(!float.IsInfinity(vDepthPos.x) && !float.IsInfinity(vDepthPos.y))
//				{
//					int dx = Mathf.RoundToInt(vDepthPos.x);
//					int dy = Mathf.RoundToInt(vDepthPos.y);
//
//					int di = dx + dy * sensorData.depthImageWidth;
//
//					if(di >= 0 && di < fgAlphaFrame.Length)
//					{
//						int ci = i << 2;
//
//						fgImageFrame[fi] = sensorData.colorImage[ci];
//						fgImageFrame[fi + 1] = sensorData.colorImage[ci + 1];
//						fgImageFrame[fi + 2] = sensorData.colorImage[ci + 2];
//						fgImageFrame[fi + 3] = fgAlphaFrame[di];
//					}
//				}
//				else
//				{
//					fgImageFrame[fi + 3] = 0;
//				}
//			}
//			
//			// buffer is released
//			lock(sensorData.depthCoordsBufferLock)
//			{
//				sensorData.depthCoordsBufferReady = false;
//			}
//		}
//		else
//		{
//			for (int i = 0, fi = 0; i < alphaImageLength; i++, fi += 4)
//			{
//				Vector2 vColorPos = Vector2.zero;
//				
//				if(sensorData.depth2ColorCoords != null && sensorData.depthCoordsBufferReady)
//				{
//					vColorPos = sensorData.depth2ColorCoords[i];
//				}
//				else
//				{
//					Vector2 vDepthPos = Vector2.zero;
//					vDepthPos.x = i % sensorData.depthImageWidth;
//					vDepthPos.y = i / sensorData.depthImageWidth;
//					
//					ushort userDepth = sensorData.depthImage[i];
//					vColorPos = MapDepthPointToColorCoords(sensorData, vDepthPos, userDepth);
//				}
//				
//				if(!float.IsInfinity(vColorPos.x) && !float.IsInfinity(vColorPos.y))
//				{
//					int cx = (int)vColorPos.x;
//					int cy = (int)vColorPos.y;
//					int colorIndex = cx + cy * sensorData.colorImageWidth;
//					
//					if(colorIndex >= 0 && colorIndex < colorImageLength)
//					{
//						int ci = colorIndex << 2;
//						
//						fgImageFrame[fi] = sensorData.colorImage[ci];
//						fgImageFrame[fi + 1] = sensorData.colorImage[ci + 1];
//						fgImageFrame[fi + 2] = sensorData.colorImage[ci + 2];
//						fgImageFrame[fi + 3] = fgAlphaFrame[i];
//					}
//				}
//				else
//				{
//					fgImageFrame[fi] = defaultColor.r;
//					fgImageFrame[fi + 1] = defaultColor.g;
//					fgImageFrame[fi + 2] = defaultColor.b;
//					fgImageFrame[fi + 3] = fgAlphaFrame[i];
//				}
//			}
//			
//			// buffer is released
//			lock(sensorData.depthCoordsBufferLock)
//			{
//				sensorData.depthCoordsBufferReady = false;
//			}
//		}
//
//		return true;
		return false;
	}
	
	// reads render texture contents into tex2d (it must have the same width and height).
	public static bool RenderTex2Tex2D(RenderTexture rt, ref Texture2D tex) 
	{
		if(!rt || !tex || rt.width != tex.width || rt.height != tex.height)
			return false;
		
		RenderTexture currentActiveRT = RenderTexture.active;
		RenderTexture.active = rt;
		
		tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
		tex.Apply();
		
		RenderTexture.active = currentActiveRT;
		
		return true;
	}

	// reads render texture contents into tex2d (it must have the same width and height).
	public static bool RenderTex2Tex2D(RenderTexture rt, int rtX, int rtY, int rtW, int rtH, ref Texture2D tex) 
	{
		if(!rt || !tex || rtW != tex.width || rtH != tex.height)
			return false;

		RenderTexture currentActiveRT = RenderTexture.active;
		RenderTexture.active = rt;

		tex.ReadPixels(new Rect(rtX, rtY, rtW, rtH), 0, 0);
		tex.Apply();

		RenderTexture.active = currentActiveRT;

		return true;
	}

	// copies source texture pixels into destination texture. Creates it, if needed.
	public static bool CopyTex2D(Texture2D src, ref Texture2D dest)
	{
		if (src == null)
			return false;

		if (dest == null) 
		{
			dest = new Texture2D(src.width, src.height, src.format, false);
		}

		if (src.width != dest.width || src.height != dest.height)
			return false;

		Color32[] pix = src.GetPixels32();
		dest.SetPixels32(pix);
		dest.Apply();

		return true;
	}

	// DLL Imports for native library functions
	[DllImport("kernel32", SetLastError=true, CharSet = CharSet.Ansi)]
	static extern IntPtr LoadLibrary([MarshalAs(UnmanagedType.LPStr)]string lpFileName);
	
	[DllImport("kernel32", SetLastError=true)]
	static extern bool FreeLibrary(IntPtr hModule);

	// load the native dll to ensure the library is loaded
	public static bool LoadNativeLib(string sLibName)
	{
		string sTargetPath = KinectInterop.GetTargetDllPath(".", Is64bitArchitecture());
		string sFullLibPath = sTargetPath + "/" + sLibName;

		IntPtr hLibrary = LoadLibrary(sFullLibPath);

		return (hLibrary != IntPtr.Zero);
	}
	
	// unloads native library
	public static void UnloadNativeLib(string sLibName)
	{
		string sTargetPath = KinectInterop.GetTargetDllPath(".", Is64bitArchitecture());
		string sFullLibPath = sTargetPath + "/" + sLibName;

		IntPtr hLibrary = LoadLibrary(sFullLibPath);

		if(hLibrary != IntPtr.Zero)
		{
			FreeLibrary(hLibrary);
			FreeLibrary(hLibrary);
		}
	}

	// unloads and deletes native library
	public static void DeleteNativeLib(string sLibName, bool bUnloadLib)
	{
		string sTargetPath = KinectInterop.GetTargetDllPath(".", Is64bitArchitecture());
		string sFullLibPath = sTargetPath + "/" + sLibName;
		
		if(bUnloadLib)
		{
			IntPtr hLibrary = LoadLibrary(sFullLibPath);
			
			if(hLibrary != IntPtr.Zero)
			{
				FreeLibrary(hLibrary);
				FreeLibrary(hLibrary);
			}
		}
		
		try 
		{
			// delete file
			if(File.Exists(sFullLibPath))
			{
				File.Delete(sFullLibPath);
			}
		} 
		catch (Exception) 
		{
			Debug.Log("Could not delete file: " + sFullLibPath);
		}
	}

	// universal windows platform specific functions

#if UNITY_WSA
	[DllImport("kernelbase")]
	public static extern void Sleep(int dwMilliseconds);
#else
	[DllImport("kernel32")]
	public static extern void Sleep(int dwMilliseconds);
#endif


	public static bool IsFileExists(string sFilePath, long iFileSize)
	{
#if UNITY_WSA
		return File.Exists(sFilePath);
#else
		System.IO.FileInfo targetFile = new System.IO.FileInfo(sFilePath);
		return iFileSize >= 0 ? targetFile.Exists && targetFile.Length == iFileSize : targetFile.Exists;
#endif
	}


	public static string GetEnvironmentVariable(string sEnvVar)
	{
#if !UNITY_WSA
		return System.Environment.GetEnvironmentVariable(sEnvVar);
#else
		return String.Empty;
#endif
	}

}