using UnityEngine;
using System.Collections;

public class DummyK2Interface : DepthSensorInterface
{
	public KinectInterop.DepthSensorPlatform GetSensorPlatform ()
	{
		return KinectInterop.DepthSensorPlatform.DummyK2;
	}

	public bool InitSensorInterface (bool bCopyLibs, ref bool bNeedRestart)
	{
		bool bOnceRestarted = bNeedRestart;
		bNeedRestart = false;

		if(!bCopyLibs)
		{
			// skip dummy interface on the 1st pass
			return bOnceRestarted;
		}

		return true;
	}

	public void FreeSensorInterface (bool bDeleteLibs)
	{
	}

	public bool IsSensorAvailable ()
	{
		return true;
	}

	public int GetSensorsCount ()
	{
		return 1;
	}

	public KinectInterop.SensorData OpenDefaultSensor (KinectInterop.FrameSource dwFlags, float sensorAngle, bool bUseMultiSource)
	{
		KinectInterop.SensorData sensorData = new KinectInterop.SensorData();

		sensorData.bodyCount = 6;
		sensorData.jointCount = 25;

		sensorData.depthCameraFOV = 60f;
		sensorData.colorCameraFOV = 53.8f;
		sensorData.depthCameraOffset = 0f;
		sensorData.faceOverlayOffset = 0f;

		sensorData.colorImageWidth = 1920;
		sensorData.colorImageHeight = 1080;

		// flip color image vertically
		sensorData.colorImageScale = new Vector3(1f, -1f, 1f);

		sensorData.depthImageWidth = 512;
		sensorData.depthImageHeight = 424;

		Debug.Log("DummyK2-sensor opened");

		return sensorData;
	}

	public void CloseSensor (KinectInterop.SensorData sensorData)
	{
		Debug.Log("DummyK2-sensor closed");
	}

	public bool UpdateSensorData (KinectInterop.SensorData sensorData)
	{
		return true;
	}

	public bool GetMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public void FreeMultiSourceFrame (KinectInterop.SensorData sensorData)
	{
	}

	public bool PollBodyFrame (KinectInterop.SensorData sensorData, ref KinectInterop.BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
	{
		return false;
	}

	public bool PollColorFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public bool PollDepthFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public bool PollInfraredFrame (KinectInterop.SensorData sensorData)
	{
		return false;
	}

	public void FixJointOrientations (KinectInterop.SensorData sensorData, ref KinectInterop.BodyData bodyData)
	{
	}

	public bool IsBodyTurned (ref KinectInterop.BodyData bodyData)
	{
		return false;
	}

	public Vector2 MapSpacePointToDepthCoords (KinectInterop.SensorData sensorData, Vector3 spacePos)
	{
		return Vector2.zero;
	}

	public Vector3 MapDepthPointToSpaceCoords (KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		return Vector3.zero;
	}

	public bool MapDepthFrameToSpaceCoords (KinectInterop.SensorData sensorData, ref Vector3[] vSpaceCoords)
	{
		return false;
	}

	public Vector2 MapDepthPointToColorCoords (KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
	{
		return Vector2.zero;
	}

	public bool MapDepthFrameToColorCoords (KinectInterop.SensorData sensorData, ref Vector2[] vColorCoords)
	{
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
		return false;
	}

	public int InitSpeechRecognition (string sRecoCriteria, bool bUseKinect, bool bAdaptationOff)
	{
		return -1;
	}

	public void FinishSpeechRecognition ()
	{
	}

	public int UpdateSpeechRecognition ()
	{
		return -1;
	}

	public int LoadSpeechGrammar (string sFileName, short iLangCode, bool bDynamic)
	{
		return -1;
	}

	public int AddGrammarPhrase (string sFromRule, string sToRule, string sPhrase, bool bClearRulePhrases, bool bCommitGrammar)
	{
		return -1;
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

	public float GetPhraseConfidence ()
	{
		return 0f;
	}

	public string GetRecognizedPhraseTag ()
	{
		return string.Empty;
	}

	public void ClearRecognizedPhrase ()
	{
	}

	public bool IsBackgroundRemovalAvailable (ref bool bNeedRestart)
	{
		return false;
	}

	public bool InitBackgroundRemoval (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return false;
	}

	public void FinishBackgroundRemoval (KinectInterop.SensorData sensorData)
	{
	}

	public bool UpdateBackgroundRemoval (KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bAlphaTexOnly)
	{
		return false;
	}

	public bool IsBackgroundRemovalActive ()
	{
		return false;
	}

	public bool IsBRHiResSupported ()
	{
		return true;
	}

	public Rect GetForegroundFrameRect (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return new Rect();
	}

	public int GetForegroundFrameLength (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
		return 0;
	}

	public bool PollForegroundFrame (KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bLimitedUsers, System.Collections.Generic.ICollection<int> alTrackedIndexes, ref byte[] foregroundImage)
	{
		return false;
	}

}
