#if (UNITY_STANDALONE_WIN)
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System;
using System.IO;


public class OrbbecAstraInterface : DepthSensorInterface 
{

	private static class Constants
	{
		public const int SkeletonCount = 6;
		public const int JointCount = 25;
		public const float SmoothingFactor = 0.7f;
	}

//	private enum AstraStatus : int
//	{
//		ASTRA_STATUS_SUCCESS = 0,
//		ASTRA_STATUS_INVALID_PARAMETER = 1,
//		ASTRA_STATUS_DEVICE_ERROR = 2,
//		ASTRA_STATUS_TIMEOUT = 3,
//		ASTRA_STATUS_INVALID_PARAMETER_TOKEN = 4,
//		ASTRA_STATUS_INVALID_OPERATION = 5,
//		ASTRA_STATUS_INTERNAL_ERROR = 6,
//		ASTRA_STATUS_UNINITIALIZED = 7
//	};

	private enum OniStatus : int
	{
		ONI_STATUS_OK = 0,
		ONI_STATUS_ERROR = 1,
		ONI_STATUS_NOT_IMPLEMENTED = 2,
		ONI_STATUS_NOT_SUPPORTED = 3,
		ONI_STATUS_BAD_PARAMETER = 4,
		ONI_STATUS_OUT_OF_FLOW = 5,
		ONI_STATUS_NO_DEVICE = 6,
		ONI_STATUS_TIME_OUT = 102,
	};

	private enum BodyTrackingStatus : int
	{
		OBT_STATUS_SUCCESS                              = 0,
		OBT_STATUS_BODY_FRAME_NOT_RELEASED              = 1,
		OBT_STATUS_DATA_FILE_INVALID_FORMAT             = 2,
		OBT_STATUS_DATA_FILE_NOT_FOUND                  = 3,
		OBT_STATUS_ASYNC_BODY_FRAME_UNKNOWN             = 4,
		OBT_STATUS_ASYNC_BODY_FRAME_INVALID             = 5,
		OBT_STATUS_INVALID_DEPTH_MAP_SIZE               = 6,
		OBT_STATUS_INVALID_PARAMETER                    = 7,
		OBT_STATUS_RESULT_DATA_ALREADY_TAKEN            = 8,
		OBT_STATUS_INTERNAL_ERROR_JOINT_TYPE            = 100,
		OBT_STATUS_INTERNAL_ERROR_JOINT_STATUS          = 101,
		OBT_STATUS_INTERNAL_ERROR_BODY_STATUS           = 102,
		OBT_STATUS_INTERNAL_ERROR_UNHANDLED_EXCEPTION   = 103,
		OBT_STATUS_INTERNAL_ERROR_NULL_BODY_FRAME       = 104,
		OBT_STATUS_INTERNAL_ERROR_WORKER_QUEUE_FULL     = 105,
		OBT_STATUS_UNKNOWN_ERROR                        = 127
	};

	private const int OBT_MAX_JOINTS = 16;
	private enum AstraJoint : int
	{
		JOINT_HEAD              = 0,
		JOINT_SHOULDER_SPINE    = 1,
		JOINT_LEFT_SHOULDER     = 2,
		JOINT_LEFT_ELBOW        = 3,
		JOINT_LEFT_HAND         = 4,
		JOINT_RIGHT_SHOULDER    = 5,
		JOINT_RIGHT_ELBOW       = 6,
		JOINT_RIGHT_HAND        = 7,
		JOINT_MID_SPINE         = 8,
		JOINT_BASE_SPINE        = 9,
		JOINT_LEFT_HIP          = 10,
		JOINT_LEFT_KNEE         = 11,
		JOINT_LEFT_FOOT         = 12,
		JOINT_RIGHT_HIP         = 13,
		JOINT_RIGHT_KNEE        = 14,
		JOINT_RIGHT_FOOT        = 15
	};

	private static readonly int[] BodyJoint2AstraJoint = {
		(int)AstraJoint.JOINT_BASE_SPINE, 		//SpineBase
		(int)AstraJoint.JOINT_MID_SPINE,		//SpineMid
		(int)AstraJoint.JOINT_SHOULDER_SPINE,	//Neck
		(int)AstraJoint.JOINT_HEAD, 			//Head
		(int)AstraJoint.JOINT_LEFT_SHOULDER,	//ShoulderLeft
		(int)AstraJoint.JOINT_LEFT_ELBOW, 		//ElbowLeft
		-1, 									//WristLeft
		(int)AstraJoint.JOINT_LEFT_HAND,		//HandLeft
		(int)AstraJoint.JOINT_RIGHT_SHOULDER,	//ShoulderRight
		(int)AstraJoint.JOINT_RIGHT_ELBOW,		//ElbowRight
		-1,										//WristRight
		(int)AstraJoint.JOINT_RIGHT_HAND,		//HandRight
		(int)AstraJoint.JOINT_LEFT_HIP,			//HipLeft
		(int)AstraJoint.JOINT_LEFT_KNEE,		//KneeLeft
		(int)AstraJoint.JOINT_LEFT_FOOT,		//AnkleLeft
		(int)AstraJoint.JOINT_LEFT_FOOT, 		//FootLeft
		(int)AstraJoint.JOINT_RIGHT_HIP,		//HipRight
		(int)AstraJoint.JOINT_RIGHT_KNEE,		//KneeRight
		(int)AstraJoint.JOINT_RIGHT_FOOT,		//AnkleRight
		(int)AstraJoint.JOINT_RIGHT_FOOT,		//FootRight
		-1,										//SpineShoulder
		-1,										//HandTipLeft
		-1,										//ThumbLeft
		-1,										//HandTipRight
		-1										//ThumbRight
	};

	private enum ObtJointStatus : byte
	{
		JOINT_STATUS_NOT_TRACKED    = 0,
		JOINT_STATUS_LOW_CONFIDENCE = 1,
		JOINT_STATUS_TRACKED        = 2,
	};

	private struct ObtJoint 
	{
		public byte type;
		public byte status;
		public Vector2 depthPosition;
		public Vector3 worldPosition;
	};

	private enum ObtBodyStatus : byte
	{
		BODY_STATUS_NOT_TRACKING = 0,
		BODY_STATUS_LOST = 1,
		BODY_STATUS_TRACKING_STARTED = 2,
		BODY_STATUS_TRACKING = 3,
	};

	private struct ObtBody 
	{
		public byte id;
		[MarshalAsAttribute(UnmanagedType.ByValArray, SizeConst = OBT_MAX_JOINTS, ArraySubType = UnmanagedType.Struct)]
		public ObtJoint[] joints;
		public byte status;
	};

	// Floor info
	public struct FloorInfo
	{
		public float a, b, c, d;
	}

	// local variables
	private bool bMultiSource = false;
	private bool bBackgroundRemovalInited = false;
	private bool bWebColorStream = false;
	private WebCamTexture colorWebCam = null;

	private int lastColorFrameIndex = -1;
	private int lastDepthFrameIndex = -1;
	private int lastBodyFrameIndex = -1;

	private ObtBody obtBody;
	private bool obtBodyInited = false;

	private int lastBodyCount = 0;
	private System.Text.StringBuilder sbDebugBodies = new System.Text.StringBuilder();


	[DllImport("OrbbecAstraInterface")]
	private static extern int InitAstraInterface();
	[DllImport("OrbbecAstraInterface")]
	private static extern int ShutdownAstraInterface();
	[DllImport("OrbbecAstraInterface")]
	private static extern int UpdateAstraInterface();
	[DllImport("OrbbecAstraInterface")]
	private static extern IntPtr GetLastErrorString();

	[DllImport("OrbbecAstraInterface")]
	private static extern int OpenAstraColorStream();
	[DllImport("OrbbecAstraInterface")]
	private static extern int OpenAstraDepthStream();
	[DllImport("OrbbecAstraInterface")]
	private static extern int OpenAstraBodyStream();

	[DllImport("OrbbecAstraInterface")]
	private static extern int CloseAstraBodyStream();
	[DllImport("OrbbecAstraInterface")]
	private static extern int CloseAstraDepthStream();
	[DllImport("OrbbecAstraInterface")]
	private static extern int CloseAstraColorStream();

	[DllImport("OrbbecAstraInterface")]
	private static extern int PollColorFrame(int maxWaitMs);
	[DllImport("OrbbecAstraInterface")]
	private static extern int ReleaseColorFrame();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetColorFrameIndex();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetColorDataSize();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetColorData(IntPtr destColorData);

	[DllImport("OrbbecAstraInterface")]
	private static extern int PollDepthFrame(int maxWaitMs);
	[DllImport("OrbbecAstraInterface")]
	private static extern int ReleaseDepthFrame();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetDepthFrameIndex();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetDepthDataSize();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetDepthData(IntPtr destDepthData);

	[DllImport("OrbbecAstraInterface")]
	private static extern int PollBodyFrame();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyFrameIndex();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyCount();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyDataSize();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyData(int bodyIndex, ref ObtBody pbodyData);
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyIndexDataSize();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetBodyIndexData(IntPtr pBodyIndexData);
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetFloorInfo(ref Vector4 floorInfo);
	[DllImport("OrbbecAstraInterface")]
	private static extern int ReleaseBodyFrame();

	[DllImport("OrbbecAstraInterface")]
	private static extern int GetColorWidth();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetColorHeight();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetDepthWidth();
	[DllImport("OrbbecAstraInterface")]
	private static extern int GetDepthHeight();

	[DllImport("OrbbecAstraInterface")]
	private static extern int ConvertDepthToWorld(float depthX, float depthY, float depthZ, out float worldX, out float worldY, out float worldZ);
	[DllImport("OrbbecAstraInterface")]
	private static extern int ConvertWorldToDepth(float worldX, float worldY, float worldZ, out float depthX, out float depthY, out float depthZ);
	[DllImport("OrbbecAstraInterface")]
	private static extern int ConvertDepthToColor(int depthX, int depthY, ushort depthZ, out int colorX, out int colorY);


	public KinectInterop.DepthSensorPlatform GetSensorPlatform ()
	{
		return KinectInterop.DepthSensorPlatform.OrbbecAstra;
	}

	public bool InitSensorInterface (bool bCopyLibs, ref bool bNeedRestart)
	{
		bool bOneCopied = false, bAllCopied = true;
		string sTargetPath = KinectInterop.GetTargetDllPath(".", KinectInterop.Is64bitArchitecture()) + "/";

		if(!bCopyLibs)
		{
			// check if the native library is there
			string sTargetLib = sTargetPath + "OrbbecAstraInterface.dll";
			bNeedRestart = false;

			string sZipFileName = !KinectInterop.Is64bitArchitecture() ? "OrbbecAstraInt.x86.zip" : "OrbbecAstraInt.x64.zip";
			long iTargetSize = KinectInterop.GetUnzippedEntrySize(sZipFileName, "KinectUnityAddin.dll");

			return KinectInterop.IsFileExists(sTargetLib, iTargetSize);
		}

		// unzip the needed files
		Dictionary<string, string> dictFilesToUnzip = new Dictionary<string, string>();
		dictFilesToUnzip["OpenNI.ini"] = sTargetPath + "OpenNI.ini";
		dictFilesToUnzip["OpenNI2.dll"] = sTargetPath + "OpenNI2.dll";
		dictFilesToUnzip["OrbbecAstraInterface.dll"] = sTargetPath + "OrbbecAstraInterface.dll";
		dictFilesToUnzip["OrbbecBodyTracking.dll"] = sTargetPath + "OrbbecBodyTracking.dll";
		dictFilesToUnzip["OrbbecBodyTracking.data"] = sTargetPath + "OrbbecBodyTracking.data";
		dictFilesToUnzip["OpenNI2/Drivers/orbbec.dll"] = sTargetPath + "OpenNI2/Drivers/orbbec.dll";
		dictFilesToUnzip["OpenNI2/Drivers/orbbec.ini"] = sTargetPath + "OpenNI2/Drivers/orbbec.ini";
//		dictFilesToUnzip["Plugins/openni_sensor.dll"] = sTargetPath + "Plugins/openni_sensor.dll";
//		dictFilesToUnzip["Plugins/orbbec_hand.dll"] = sTargetPath + "Plugins/orbbec_hand.dll";
//		dictFilesToUnzip["Plugins/orbbec_hand.toml"] = sTargetPath + "Plugins/orbbec_hand.toml";
//		dictFilesToUnzip["Plugins/orbbec_xs.dll"] = sTargetPath + "Plugins/orbbec_xs.dll";
		dictFilesToUnzip["msvcp140.dll"] = sTargetPath + "msvcp140.dll";
		dictFilesToUnzip["vcruntime140.dll"] = sTargetPath + "vcruntime140.dll";
		dictFilesToUnzip["msvcp140d.dll"] = sTargetPath + "msvcp140d.dll";
		dictFilesToUnzip["vcruntime140d.dll"] = sTargetPath + "vcruntime140d.dll";

		if(!KinectInterop.Is64bitArchitecture())
		{
			Debug.Log("x32-architecture detected.");
			KinectInterop.UnzipResourceFiles(dictFilesToUnzip, "OrbbecAstraInt.x86.zip", ref bOneCopied, ref bAllCopied);
		}
		else
		{
			Debug.Log("x64-architecture detected.");
			KinectInterop.UnzipResourceFiles(dictFilesToUnzip, "OrbbecAstraInt.x64.zip", ref bOneCopied, ref bAllCopied);
		}

		bNeedRestart = (bOneCopied && bAllCopied);

		return true;
	}

	public void FreeSensorInterface (bool bDeleteLibs)
	{
		if(bDeleteLibs)
		{
			KinectInterop.DeleteNativeLib("OrbbecAstraInterface.dll", true);
			KinectInterop.DeleteNativeLib("msvcp140.dll", false);
			KinectInterop.DeleteNativeLib("vcruntime140.dll", false);
		}
	}

	public bool IsSensorAvailable ()
	{
		bool bAvailable = GetSensorsCount() > 0;
		return bAvailable;
	}

	public int GetSensorsCount ()
	{
		int hr = InitAstraInterface();

		if(hr == 0)
		{
			ShutdownAstraInterface();
		}
		
		return (hr == 0 ? 1 : 0);

//		// workaround for astra-sdk
//		return 1;
	}

	public KinectInterop.SensorData OpenDefaultSensor (KinectInterop.FrameSource dwFlags, float sensorAngle, bool bUseMultiSource)
	{
		// init interface
		int hr = InitAstraInterface();
		if(hr != 0)
			return null;

		bMultiSource = bUseMultiSource;

		KinectInterop.SensorData sensorData = new KinectInterop.SensorData();

		if((dwFlags & KinectInterop.FrameSource.TypeColor) != 0)
		{
			hr = OpenAstraColorStream();

			// try to get a color frame
			hr = PollColorFrame(500); 
			if(hr == 0)
			{
				ReleaseColorFrame();
			}

			if(hr != 0)
			{
				bWebColorStream = true;

				for(int i = 0; i < WebCamTexture.devices.Length; i++)
				{
					if(WebCamTexture.devices[i].name.IndexOf("astra", StringComparison.CurrentCultureIgnoreCase) >= 0)
					{
						colorWebCam = new WebCamTexture(WebCamTexture.devices[i].name);
						break;
					}
				}

				if(colorWebCam)
				{
					colorWebCam.Play();

					sensorData.colorImageWidth = colorWebCam.width;
					sensorData.colorImageHeight = colorWebCam.height;
					sensorData.colorImageTexture = colorWebCam;

					//Debug.Log("Webcam - vMirrored: " + colorWebCam.videoVerticallyMirrored + ", rotAngle: " + colorWebCam.videoRotationAngle);
				}
			}
		}
		
		if((dwFlags & KinectInterop.FrameSource.TypeDepth) != 0)
		{
			hr = OpenAstraDepthStream();

			// try to get a depth frame
			hr = PollDepthFrame(500); 
			if(hr == 0)
			{
				ReleaseDepthFrame();
			}
		}
		
		if(((dwFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0) ||
			((dwFlags & KinectInterop.FrameSource.TypeBody) != 0))
		{
			hr = OpenAstraBodyStream();

			if(hr == 0)
			{
				obtBody = new ObtBody();
				obtBody.joints = new ObtJoint[OBT_MAX_JOINTS];

				obtBodyInited = true;
			}
		}

//		if((dwFlags & KinectInterop.FrameSource.TypeInfrared) != 0)
//		{
//		}
		
		sensorData.bodyCount = Constants.SkeletonCount;
		sensorData.jointCount = Constants.JointCount;
		
		sensorData.depthCameraFOV = 49.5f;
		sensorData.colorCameraFOV = 48.6f;
		sensorData.depthCameraOffset = 0f;
		sensorData.faceOverlayOffset = 0f;

		if(!bWebColorStream)
		{
			sensorData.colorImageWidth = GetColorWidth();
			sensorData.colorImageHeight = GetColorHeight();

			// flip color image vertically
			sensorData.colorImageScale = new Vector3(1f, -1f, 1f);
		}
		else
		{
			// flip color image horizontally
			sensorData.colorImageScale = new Vector3(-1f, 1f, 1f);
		}

		sensorData.depthImageWidth = GetDepthWidth();
		sensorData.depthImageHeight = GetDepthHeight();

		if((dwFlags & KinectInterop.FrameSource.TypeColor) != 0)
		{
			int colorImageSize = !colorWebCam ? (GetColorDataSize() * 4 / 3) : 0;
			sensorData.colorImage = new byte[colorImageSize];
		}

		if((dwFlags & KinectInterop.FrameSource.TypeDepth) != 0)
		{
			int depthImageSize = GetDepthDataSize() / sizeof(ushort);
			sensorData.depthImage = new ushort[depthImageSize];
		}
		
		if((dwFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0)
		{
			int bodyIndexImageSize = GetBodyIndexDataSize();
			sensorData.bodyIndexImage = new byte[bodyIndexImageSize];
		}
		
		if((dwFlags & KinectInterop.FrameSource.TypeInfrared) != 0)
		{
			int depthImageSize = GetDepthDataSize() / sizeof(ushort);
			sensorData.infraredImage = new ushort[depthImageSize];
		}
		
		Debug.Log("Orbbec Astra sensor opened");
		
		return sensorData;
	}

	public void CloseSensor (KinectInterop.SensorData sensorData)
	{
		if(colorWebCam)
		{
			colorWebCam.Stop();
			colorWebCam = null;
		}

		ShutdownAstraInterface();

		// try to unload the library, to prevent the crash
		//KinectInterop.UnloadNativeLib("OrbbecAstraInterface.dll");

		Debug.Log("Orbbec Astra sensor closed");
	}

	public bool UpdateSensorData (KinectInterop.SensorData sensorData)
	{
		UpdateAstraInterface();
		return true;
	}

	public bool GetMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public void FreeMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
	}

	public bool PollBodyFrame (KinectInterop.SensorData sensorData, ref KinectInterop.BodyFrameData bodyFrame, 
	                           ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
	{
		bool bNewFrame = false;

		// look for frame
		int hr = PollBodyFrame();

		if(hr != 0)
		{
			Debug.Log("PollBodyFrame error: " + hr);
		}

		if(hr == 0)
		{
			int bodyFrameIndex = GetBodyFrameIndex();

			if(bodyFrameIndex != lastBodyFrameIndex)
			{
				lastBodyFrameIndex = bodyFrameIndex;
				long timeNowTicks = DateTime.Now.Ticks;

				// get body index frame
				var pBodyIndexData = GCHandle.Alloc(sensorData.bodyIndexImage, GCHandleType.Pinned);
				hr = GetBodyIndexData(pBodyIndexData.AddrOfPinnedObject());
				pBodyIndexData.Free();

				if(hr != 0)
				{
					Debug.Log("GetBodyIndexData() error: " + hr);
				}

				sensorData.lastBodyIndexFrameTime = (hr == 0) ? timeNowTicks : sensorData.lastBodyIndexFrameTime;

				bodyFrame.liPreviousTime = bodyFrame.liRelativeTime;
				bodyFrame.liRelativeTime = timeNowTicks;

				if(sensorData.hintHeightAngle)
				{
					Vector4 floorInfo = Vector4.zero;
					if(GetFloorInfo(ref floorInfo) != 0)
					{
						Vector3 floorPlane = (Vector3)floorInfo;

						sensorData.sensorRotDetected = Quaternion.FromToRotation(floorPlane, Vector3.up);
						sensorData.sensorHgtDetected = Mathf.Abs(floorInfo.w / 1000f);
					}
				}

//				int bodySize1 = Marshal.SizeOf(typeof(ObtBody));
//				int bodySize2 = GetBodyDataSize();

				int bodyCount = GetBodyCount();
				if(lastBodyCount != bodyCount)
				{
					sbDebugBodies.Append(bodyCount).Append(" bodies - ");
				}

				for(int i = 0; i < sensorData.bodyCount; i++)
				{
					// compare to real body count
					if(i >= bodyCount || !obtBodyInited)
					{
						bodyFrame.bodyData[i].bIsTracked = 0;
						continue;
					}

					// get body and joints data
					hr = GetBodyData(i, ref obtBody);
					if(lastBodyCount != bodyCount)
					{
						sbDebugBodies.Append(obtBody.id).Append(":").Append(obtBody.status).Append(":").Append(hr).Append("  ");
					}

					// if there is error, consider body as not-tracked
					if (hr != 0)
					{
						bodyFrame.bodyData[i].bIsTracked = 0;
						continue;
					}

					// set body tracking state
					bodyFrame.bodyData[i].bIsTracked = (short)(obtBody.status != (byte)ObtBodyStatus.BODY_STATUS_NOT_TRACKING ? 1 : 0);

					if(obtBody.status != (byte)ObtBodyStatus.BODY_STATUS_NOT_TRACKING)
					{
						// transfer body and joints data
						bodyFrame.bodyData[i].liTrackingID = (long)obtBody.id;

						for(int j = 0; j < sensorData.jointCount; j++)
						{
							KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];
							int obtJI = BodyJoint2AstraJoint[j];

							if(obtJI >= 0)
							{
								jointData.trackingState = (KinectInterop.TrackingState)obtBody.joints[obtJI].status;
							}
							else
							{
								jointData.trackingState = KinectInterop.TrackingState.NotTracked;
							}

							if(jointData.trackingState != KinectInterop.TrackingState.NotTracked)
							{
								Vector3 jointPos = obtBody.joints[obtJI].worldPosition / 1000f;

								float jPosZ = (bIgnoreJointZ && j > 0) ? bodyFrame.bodyData[i].joint[0].kinectPos.z : jointPos.z;
								jointData.kinectPos = jointPos;
								jointData.position = kinectToWorld.MultiplyPoint3x4(new Vector3(jointPos.x, jointPos.y, jPosZ));
							}

							jointData.orientation = Quaternion.identity;

							if(j == 0)
							{
								bodyFrame.bodyData[i].position = jointData.position;
								bodyFrame.bodyData[i].orientation = jointData.orientation;
							}

							bodyFrame.bodyData[i].joint[j] = jointData;
						}

						// processes some special joint cases
						ProcessBodyFrameSpecialCases(i, ref bodyFrame);

//						// hand states - is this available on orbbec?
//						bodyFrame.bodyData[i].leftHandState = (KinectInterop.HandState)body.HandLeftState;
//						bodyFrame.bodyData[i].leftHandConfidence = (KinectInterop.TrackingConfidence)body.HandLeftConfidence;
//
//						bodyFrame.bodyData[i].rightHandState = (KinectInterop.HandState)body.HandRightState;
//						bodyFrame.bodyData[i].rightHandConfidence = (KinectInterop.TrackingConfidence)body.HandRightConfidence;
					}
				}

				if(sbDebugBodies.Length > 0)
				{
					Debug.Log(sbDebugBodies.ToString());
					sbDebugBodies.Remove(0, sbDebugBodies.Length);
				}

				lastBodyCount = bodyCount;
				bNewFrame = true;
			}

			// release the frame
			ReleaseBodyFrame();
		}

		return bNewFrame;
	}

	// processes some special cases in body joints
	private void ProcessBodyFrameSpecialCases(int i, ref KinectInterop.BodyFrameData bodyFrame)
	{
		// special case - wrist left
		int h = (int)KinectInterop.JointType.HandLeft;
		//if(bodyFrame.bodyData[i].joint[h].trackingState == KinectInterop.TrackingState.Tracked)
		{
			int w = h - 1;  // wrist
			int e = h - 2;  // elbow

			KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[w];
			jointData.trackingState = bodyFrame.bodyData[i].joint[h].trackingState;
			jointData.orientation = Quaternion.identity;

			Vector3 posHand = bodyFrame.bodyData[i].joint[h].kinectPos;
			Vector3 posElbow = bodyFrame.bodyData[i].joint[e].kinectPos;
			jointData.kinectPos = posElbow + (posHand - posElbow) * 0.9f;

			posHand = bodyFrame.bodyData[i].joint[h].position;
			posElbow = bodyFrame.bodyData[i].joint[e].position;
			jointData.position = posElbow + (posHand - posElbow) * 0.9f;

			bodyFrame.bodyData[i].joint[w] = jointData;
		}

		// special case - wrist right
		h = (int)KinectInterop.JointType.HandRight;
		//if(bodyFrame.bodyData[i].joint[h].trackingState == KinectInterop.TrackingState.Tracked)
		{
			int w = h - 1;  // wrist
			int e = h - 2;  // elbow

			KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[w];
			jointData.trackingState = bodyFrame.bodyData[i].joint[h].trackingState;
			jointData.orientation = Quaternion.identity;

			Vector3 posHand = bodyFrame.bodyData[i].joint[h].kinectPos;
			Vector3 posElbow = bodyFrame.bodyData[i].joint[e].kinectPos;
			jointData.kinectPos = posElbow + (posHand - posElbow) * 0.9f;

			posHand = bodyFrame.bodyData[i].joint[h].position;
			posElbow = bodyFrame.bodyData[i].joint[e].position;
			jointData.position = posElbow + (posHand - posElbow) * 0.9f;

			bodyFrame.bodyData[i].joint[w] = jointData;
		}

		// special case - shoulder center
		int r = (int)KinectInterop.JointType.ShoulderRight;
		int l = (int)KinectInterop.JointType.ShoulderLeft;

		if(bodyFrame.bodyData[i].joint[r].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyFrame.bodyData[i].joint[l].trackingState == KinectInterop.TrackingState.Tracked)
		{
			int c = (int)KinectInterop.JointType.SpineShoulder;
			KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[c];

			jointData.trackingState = bodyFrame.bodyData[i].joint[r].trackingState;
			jointData.orientation = Quaternion.identity;

			Vector3 posRight = bodyFrame.bodyData[i].joint[r].kinectPos;
			Vector3 posLeft = bodyFrame.bodyData[i].joint[l].kinectPos;
			jointData.kinectPos = (posRight + posLeft) * 0.5f;

			posRight = bodyFrame.bodyData[i].joint[r].position;
			posLeft = bodyFrame.bodyData[i].joint[l].position;
			jointData.position = (posRight + posLeft) * 0.5f;

			bodyFrame.bodyData[i].joint[c] = jointData;
		}

		// special case - hip center
		r = (int)KinectInterop.JointType.HipRight;
		l = (int)KinectInterop.JointType.HipLeft;

		if(bodyFrame.bodyData[i].joint[r].trackingState == KinectInterop.TrackingState.Tracked &&
			bodyFrame.bodyData[i].joint[l].trackingState == KinectInterop.TrackingState.Tracked)
		{
			int c = (int)KinectInterop.JointType.SpineBase;
			KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[c];

			jointData.trackingState = bodyFrame.bodyData[i].joint[r].trackingState;
			jointData.orientation = Quaternion.identity;

			Vector3 posRight = bodyFrame.bodyData[i].joint[r].kinectPos;
			Vector3 posLeft = bodyFrame.bodyData[i].joint[l].kinectPos;
			jointData.kinectPos = (posRight + posLeft) * 0.5f;

			posRight = bodyFrame.bodyData[i].joint[r].position;
			posLeft = bodyFrame.bodyData[i].joint[l].position;
			jointData.position = (posRight + posLeft) * 0.5f;

			bodyFrame.bodyData[i].joint[c] = jointData;

			// modify the body position, too
			bodyFrame.bodyData[i].position = jointData.position;
			bodyFrame.bodyData[i].orientation = jointData.orientation;
		}
	}

	public bool PollColorFrame (KinectInterop.SensorData sensorData)
	{
		bool bNewFrame = false;

		// check if color camera is webcam
		if(bWebColorStream && colorWebCam && colorWebCam.didUpdateThisFrame)
		{
			if(sensorData.colorImageTexture2D)
			{
				if(sensorData.colorImageTexture2D.width == colorWebCam.width && sensorData.colorImageTexture2D.height == colorWebCam.height)
				{
					sensorData.colorImageTexture2D.SetPixels32(colorWebCam.GetPixels32());
				}
			}

			sensorData.lastColorFrameTime = DateTime.Now.Ticks;
			bNewFrame = true;

			return bNewFrame;
		}

		// poll for color frame
		int hr = !bWebColorStream ? PollColorFrame(1) : -1;

		if(hr == 0) 
		{
			int colorFrameIndex = GetColorFrameIndex();

			if(colorFrameIndex != lastColorFrameIndex)
			{
				lastColorFrameIndex = colorFrameIndex;

				// warning - check data to be copied as ARGB
				var pColorData = GCHandle.Alloc(sensorData.colorImage, GCHandleType.Pinned);
				hr = GetColorData(pColorData.AddrOfPinnedObject());
				pColorData.Free();

				sensorData.lastColorFrameTime = DateTime.Now.Ticks;

				bNewFrame = (hr == 0);
			}

			ReleaseColorFrame();
		}

		return bNewFrame;
	}

	public bool PollDepthFrame (KinectInterop.SensorData sensorData)
	{
		bool bNewFrame = false;

		// poll for depth frame
		int hr = PollDepthFrame(1);

		if(hr == 0) 
		{
			int depthFrameIndex = GetDepthFrameIndex();

			if(depthFrameIndex != lastDepthFrameIndex)
			{
				lastDepthFrameIndex = depthFrameIndex;

				var pDepthData = GCHandle.Alloc(sensorData.depthImage, GCHandleType.Pinned);
				hr = GetDepthData(pDepthData.AddrOfPinnedObject());
				pDepthData.Free();

				sensorData.lastDepthFrameTime = DateTime.Now.Ticks;

				bNewFrame = (hr == 0);
			}

			ReleaseDepthFrame();
		}

		return bNewFrame;
	}

	public bool PollInfraredFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public void FixJointOrientations (KinectInterop.SensorData sensorData, ref KinectInterop.BodyData bodyData)
	{
	}

	public bool IsBodyTurned(ref KinectInterop.BodyData bodyData)
	{
		return false;
	}

	public Vector2 MapSpacePointToDepthCoords (KinectInterop.SensorData sensorData, Vector3 spacePos)
	{
		Vector2 depthPos = Vector3.zero;

		float depthX = 0f, depthY = 0f, depthZ = 0f;
		int hr = ConvertWorldToDepth(spacePos.x * 1000f, spacePos.y * 1000f, spacePos.z * 1000f, out depthX, out depthY, out depthZ);

		if(hr == 0)
		{
			depthPos = new Vector2(depthX, depthY);
		}

		return depthPos;
	}
	
	public Vector3 MapDepthPointToSpaceCoords (KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		Vector3 spacePos = Vector3.zero;

		float spaceX = 0f, spaceY = 0f, spaceZ = 0f;
		int hr = ConvertDepthToWorld(depthPos.x, depthPos.y, depthVal, out spaceX, out spaceY, out spaceZ);

		if(hr == 0)
		{
			spacePos = new Vector3(spaceX / 1000f, spaceY / 1000f, spaceZ / 1000f);
		}

		return spacePos;
	}
	
	public bool MapDepthFrameToSpaceCoords (KinectInterop.SensorData sensorData, ref Vector3[] vSpaceCoords)
	{
		return false;
	}
	
	public Vector2 MapDepthPointToColorCoords (KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		Vector2 colorPos = new Vector2(float.NegativeInfinity, float.NegativeInfinity);

		int colorX = 0, colorY = 0;
		int hr = ConvertDepthToColor((int)depthPos.x, (int)depthPos.y, depthVal, out colorX, out colorY);

		if(hr == 0)
		{
			colorPos = new Vector2(colorX, colorY);
		}

		return colorPos;
	}

	public bool MapDepthFrameToColorCoords (KinectInterop.SensorData sensorData, ref Vector2[] vColorCoords)
	{
//		if(sensorData.depthImage != null && sensorData.colorImage != null)
//		{
//			Vector2 vInfinity = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
//			int i = 0, cx = 0, cy = 0;
//			
//			for(int y = 0; y < sensorData.depthImageHeight; y++)
//			{
//				for(int x = 0; x < sensorData.depthImageWidth; x++)
//				{
//					ushort dv = sensorData.depthImage[i];
//
//					if(ConvertDepthToColor(x, y, dv, out cx, out cy))
//						vColorCoords[i] = new Vector2(cx, cy);
//					else
//						vColorCoords[i] = vInfinity;
//					
//					i++;
//				}
//			}
//			
//			return true;
//		}
		
		return false;
	}

	public bool MapColorFrameToDepthCoords (KinectInterop.SensorData sensorData, ref Vector2[] vDepthCoords)
	{
		return false;
	}

	public int GetJointIndex (KinectInterop.JointType joint)
	{
		return (int)joint;
	}

//	// returns the joint at given index
//	public KinectInterop.JointType GetJointAtIndex(int index)
//	{
//		return (KinectInterop.JointType)(index);
//	}

	public KinectInterop.JointType GetParentJoint (KinectInterop.JointType joint)
	{
		switch(joint)
		{
		case KinectInterop.JointType.SpineBase:
			return KinectInterop.JointType.SpineBase;

		case KinectInterop.JointType.Neck:
			return KinectInterop.JointType.SpineShoulder;

		case KinectInterop.JointType.SpineShoulder:
			return KinectInterop.JointType.SpineMid;

		case KinectInterop.JointType.ShoulderLeft:
		case KinectInterop.JointType.ShoulderRight:
			return KinectInterop.JointType.SpineShoulder;

		case KinectInterop.JointType.HipLeft:
		case KinectInterop.JointType.HipRight:
			return KinectInterop.JointType.SpineBase;

		case KinectInterop.JointType.HandTipLeft:
			return KinectInterop.JointType.HandLeft;

		case KinectInterop.JointType.ThumbLeft:
			return KinectInterop.JointType.WristLeft;

		case KinectInterop.JointType.HandTipRight:
			return KinectInterop.JointType.HandRight;

		case KinectInterop.JointType.ThumbRight:
			return KinectInterop.JointType.WristRight;
		}

		return (KinectInterop.JointType)((int)joint - 1);
	}

	public KinectInterop.JointType GetNextJoint (KinectInterop.JointType joint)
	{
		switch(joint)
		{
		case KinectInterop.JointType.SpineBase:
			return KinectInterop.JointType.SpineMid;
		case KinectInterop.JointType.SpineMid:
			return KinectInterop.JointType.SpineShoulder;
		case KinectInterop.JointType.SpineShoulder:
			return KinectInterop.JointType.Neck;
		case KinectInterop.JointType.Neck:
			return KinectInterop.JointType.Head;

		case KinectInterop.JointType.ShoulderLeft:
			return KinectInterop.JointType.ElbowLeft;
		case KinectInterop.JointType.ElbowLeft:
			return KinectInterop.JointType.WristLeft;
		case KinectInterop.JointType.WristLeft:
			return KinectInterop.JointType.HandLeft;
		case KinectInterop.JointType.HandLeft:
			return KinectInterop.JointType.HandTipLeft;

		case KinectInterop.JointType.ShoulderRight:
			return KinectInterop.JointType.ElbowRight;
		case KinectInterop.JointType.ElbowRight:
			return KinectInterop.JointType.WristRight;
		case KinectInterop.JointType.WristRight:
			return KinectInterop.JointType.HandRight;
		case KinectInterop.JointType.HandRight:
			return KinectInterop.JointType.HandTipRight;

		case KinectInterop.JointType.HipLeft:
			return KinectInterop.JointType.KneeLeft;
		case KinectInterop.JointType.KneeLeft:
			return KinectInterop.JointType.AnkleLeft;
		case KinectInterop.JointType.AnkleLeft:
			return KinectInterop.JointType.FootLeft;

		case KinectInterop.JointType.HipRight:
			return KinectInterop.JointType.KneeRight;
		case KinectInterop.JointType.KneeRight:
			return KinectInterop.JointType.AnkleRight;
		case KinectInterop.JointType.AnkleRight:
			return KinectInterop.JointType.FootRight;
		}

		return joint;  // in case of end joint - Head, HandTipLeft, HandTipRight, FootLeft, FootRight
	}

	public bool IsFaceTrackingAvailable (ref bool bNeedRestart)
	{
		bNeedRestart = false;
		return false;
	}

	public bool InitFaceTracking (bool bUseFaceModel, bool bDrawFaceRect)
	{
		return false;
	}

	public void FinishFaceTracking ()
	{
	}

	public bool UpdateFaceTracking ()
	{
		return false;
	}

	public bool IsFaceTrackingActive ()
	{
		return false;
	}

	public bool IsDrawFaceRect ()
	{
		return false;
	}

	public bool IsFaceTracked (long userId)
	{
		return false;
	}

	public bool GetFaceRect (long userId, ref Rect faceRect)
	{
		return false;
	}

	public void VisualizeFaceTrackerOnColorTex (Texture2D texColor)
	{
	}

	public bool GetHeadPosition (long userId, ref Vector3 headPos)
	{
		return false;
	}

	public bool GetHeadRotation (long userId, ref Quaternion headRot)
	{
		return false;
	}

	public bool GetAnimUnits (long userId, ref System.Collections.Generic.Dictionary<KinectInterop.FaceShapeAnimations, float> afAU)
	{
		return false;
	}

	public bool GetShapeUnits (long userId, ref System.Collections.Generic.Dictionary<KinectInterop.FaceShapeDeformations, float> afSU)
	{
		return false;
	}

	public int GetFaceModelVerticesCount (long userId)
	{
		return 0;
	}

	public bool GetFaceModelVertices (long userId, ref Vector3[] avVertices)
	{
		return false;
	}

	public int GetFaceModelTrianglesCount ()
	{
		return 0;
	}

	public bool GetFaceModelTriangles (bool bMirrored, ref int[] avTriangles)
	{
		return false;
	}

	public bool IsSpeechRecognitionAvailable (ref bool bNeedRestart)
	{
		bNeedRestart = false;
		return false;
	}

	public int InitSpeechRecognition (string sRecoCriteria, bool bUseKinect, bool bAdaptationOff)
	{
		return 0;
	}

	public void FinishSpeechRecognition ()
	{
	}

	public int UpdateSpeechRecognition ()
	{
		return 0;
	}

	public int LoadSpeechGrammar (string sFileName, short iLangCode, bool bDynamic)
	{
		return 0;
	}

	public int AddGrammarPhrase(string sFromRule, string sToRule, string sPhrase, bool bClearRulePhrases, bool bCommitGrammar)
	{
		return 0;
	}
	
	public void SetSpeechConfidence (float fConfidence)
	{
	}

	public bool IsSpeechStarted ()
	{
		return false;
	}

	public bool IsSpeechEnded ()
	{
		return false;
	}

	public bool IsPhraseRecognized ()
	{
		return false;
	}

	public float GetPhraseConfidence()
	{
		return 0;
	}
	
	public string GetRecognizedPhraseTag ()
	{
		return string.Empty;
	}

	public void ClearRecognizedPhrase ()
	{
	}

	public bool IsBackgroundRemovalAvailable(ref bool bNeedRestart)
	{
		bBackgroundRemovalInited = KinectInterop.IsOpenCvAvailable(ref bNeedRestart);
		return bBackgroundRemovalInited;
	}
	
	public bool InitBackgroundRemoval(KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return KinectInterop.InitBackgroundRemoval(sensorData, isHiResPrefered);
	}
	
	public void FinishBackgroundRemoval(KinectInterop.SensorData sensorData)
	{
		KinectInterop.FinishBackgroundRemoval(sensorData);
		bBackgroundRemovalInited = false;
	}
	
	public bool UpdateBackgroundRemoval(KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bAlphaTexOnly)
	{
		return KinectInterop.UpdateBackgroundRemoval(sensorData, isHiResPrefered, defaultColor, bAlphaTexOnly);
	}
	
	public bool IsBackgroundRemovalActive()
	{
		return bBackgroundRemovalInited;
	}
	
	public bool IsBRHiResSupported()
	{
		return false;
	}
	
	public Rect GetForegroundFrameRect(KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return KinectInterop.GetForegroundFrameRect(sensorData, isHiResPrefered);
	}
	
	public int GetForegroundFrameLength(KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return KinectInterop.GetForegroundFrameLength(sensorData, isHiResPrefered);
	}
	
	public bool PollForegroundFrame(KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bLimitedUsers, ICollection<int> alTrackedIndexes, ref byte[] foregroundImage)
	{
		return KinectInterop.PollForegroundFrame(sensorData, isHiResPrefered, defaultColor, bLimitedUsers, alTrackedIndexes, ref foregroundImage);
	}
	
}
#endif