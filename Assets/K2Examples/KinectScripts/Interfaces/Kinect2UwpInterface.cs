#if (UNITY_WSA_10_0 && NETFX_CORE)
using UnityEngine;
using System.Collections;

using MultiK2;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using MultiK2.Tracking;
using Windows.Media.Capture.Frames;
using Windows.Media.SpeechRecognition;


public class Kinect2UwpInterface : DepthSensorInterface
{
    private KinectInterop.SensorData sensorData;
    private KinectInterop.FrameSource sensorFlags;

    private Sensor _kinectSensor;
    private ColorFrameReader _colorReader;
    private DepthFrameReader _depthReader;
    private BodyIndexFrameReader _bodyIndexReader;
    private BodyFrameReader _bodyReader;

    private CameraIntrinsics _colorCameraIntrinsics;
    private CameraIntrinsics _depthCameraIntrinsics;
    private CoordinateMapper _coordinateMapper;
    private CoordinateMapper2 _coordinateMapper2;

    private SpeechRecognizer speechRecognizer;
    private Task<SpeechRecognitionResult> speechRecognizeTask;
    private float requiredPhraseConfidence = 0f;

    private bool isPhraseRecognized;
    private string recognizedPhraseTag;
    private float recognizedPhraseConfidence;

    private byte[] _colorDataBuf = null;
    private bool _colorDataReady = false;
    private long _colorDataTime = 0;
    private object _colorDataLock = new object();

    private byte[] _depthDataBuf = null;
    private bool _depthDataReady = false;
    private long _depthDataTime = 0;
    private object _depthDataLock = new object();

    private byte[] _bodyIndexDataBuf = null;
    private bool _bodyIndexDataReady = false;
    private long _bodyIndexDataTime = 0;
    private object _bodyIndexDataLock = new object();

    private BodyFrame _bodyFrame = null;
    private bool _bodyFrameReady = false;
    private long _bodyFrameTime = 0;
    private object _bodyFrameLock = new object();

    private bool _isDoubleDepthBufNeeded = false;
    private ushort[] _lastDepthDataBuf = null;
    private long _lastDepthDataTime = 0;

    private bool _depth2spaceTaskStarted = false;
    //private bool _depth2colorTaskStarted = false;
    //private bool _color2depthTaskStarted = false;

    private bool _saveLatestFrames = false;
    private bool _clearLatestFrames = false;

    private MediaFrameReference _latestColorFrame = null;
    private MediaFrameReference _latestDepthFrame = null;
    private MediaFrameReference _latestBodyIndexFrame = null;
    private MediaFrameReference _latestBodyFrame = null;
    private MediaFrameReference _latestInfraredFrame = null;

    private System.Numerics.Vector3[] _color2SpacePoints = null;
    //private float[] _color2depthDepth = null;

    private Vector3[] _depth2SpaceTable = null;

    private ComputeShader _coordMapperShader = null;
    private int _depth2colorKernel = 0;
    private int _color2depthKernel = 0;

    private ComputeBuffer _depthPlaneCoordsBuf = null;
    private ComputeBuffer _depthDepthValuesBuf = null;
    private ComputeBuffer _colorPlaneCoordsBuf = null;
    private ComputeBuffer _colorSpaceCoordsBuf = null;
    private ComputeBuffer _colorDepthCoordsBuf = null;

    private bool _backgroundRemovalInited = false;


    public KinectInterop.DepthSensorPlatform GetSensorPlatform()
    {
        return KinectInterop.DepthSensorPlatform.KinectUWPv2;
    }

    public bool InitSensorInterface(bool bCopyLibs, ref bool bNeedRestart)
    {
        bNeedRestart = false;
        return true;
    }

    public void FreeSensorInterface(bool bDeleteLibs)
    {
    }

    public bool IsSensorAvailable()
    {
        return true;
    }

    public int GetSensorsCount()
    {
        return 1;
    }

    public KinectInterop.SensorData OpenDefaultSensor(KinectInterop.FrameSource dwFlags, float sensorAngle, bool bUseMultiSource)
    {
        if (sensorData == null)
        {
            sensorData = new KinectInterop.SensorData();
        }

        sensorFlags = dwFlags;

        sensorData.bodyCount = 6;
        sensorData.jointCount = 25;

        sensorData.depthCameraFOV = 60f;
        sensorData.colorCameraFOV = 53.8f;
        sensorData.depthCameraOffset = 0f;
        sensorData.faceOverlayOffset = 0f;

        // by-default image widths & heights
        sensorData.colorImageWidth = 1920;
        sensorData.colorImageHeight = 1080;

		// flip color image vertically
		sensorData.colorImageScale = new Vector3(1f, -1f, 1f);

        sensorData.depthImageWidth = 512;
        sensorData.depthImageHeight = 424;

        _saveLatestFrames = bUseMultiSource;

        Task task = null;
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            task = InitializeKinect();
        }, true);

        while (task != null && !task.IsCompleted)
        {
            task.Wait(100);
        }

        return (_kinectSensor != null && _kinectSensor.IsActive) ? sensorData : null;
    }

    private async Task InitializeKinect()
    {
        _kinectSensor = await Sensor.GetDefaultAsync();

        if (_kinectSensor != null)
        {
            await _kinectSensor.OpenAsync();

            if ((sensorFlags & KinectInterop.FrameSource.TypeColor) != 0)
            {
                if (sensorData.colorImage == null)
                {
                    sensorData.colorImage = new byte[sensorData.colorImageWidth * sensorData.colorImageHeight * 4];
                }

                _colorReader = await _kinectSensor.OpenColorFrameReaderAsync(ReaderConfig.HalfRate | ReaderConfig.HalfResolution);
                if (_colorReader != null)
                {
                    _colorReader.FrameArrived += ColorReader_FrameArrived;
                }
            }

            if ((sensorFlags & KinectInterop.FrameSource.TypeDepth) != 0)
            {
                if (sensorData.depthImage == null)
                {
                    sensorData.depthImage = new ushort[sensorData.depthImageWidth * sensorData.depthImageHeight];
                }

                _depthReader = await _kinectSensor.OpenDepthFrameReaderAsync();
                if (_depthReader != null)
                {
                    _depthReader.FrameArrived += DepthReader_FrameArrived;
                }
            }

            if ((sensorFlags & KinectInterop.FrameSource.TypeBodyIndex) != 0)
            {
                if (sensorData.bodyIndexImage == null)
                {
                    sensorData.bodyIndexImage = new byte[sensorData.depthImageWidth * sensorData.depthImageHeight];
                }

                _bodyIndexReader = await _kinectSensor.OpenBodyIndexFrameReaderAsync();
                if (_bodyIndexReader != null)
                {
                    _bodyIndexReader.FrameArrived += BodyIndexReader_FrameArrived;
                }
            }

            if ((sensorFlags & KinectInterop.FrameSource.TypeBody) != 0)
            {
                _bodyReader = await _kinectSensor.OpenBodyFrameReaderAsync();
                if (_bodyReader != null)
                {
                    _bodyReader.FrameArrived += BodyReader_FrameArrived;
                }
            }

            // get the coordinate mapper
            _coordinateMapper = _kinectSensor.GetCoordinateMapper();
            _coordinateMapper2 = new CoordinateMapper2();

            Debug.Log("UWP-K2 sensor opened");
        }
        else
        {
            Debug.Log("UWP-K2 sensor not found");
        }
    }

    private void ColorReader_FrameArrived(object sender, ColorFrameArrivedEventArgs e)
    {
        _colorCameraIntrinsics = e.CameraIntrinsics;

        if (_colorDataBuf == null || sensorData.colorImageWidth != e.Bitmap.PixelWidth || sensorData.colorImageHeight != e.Bitmap.PixelHeight)
        {
            sensorData.colorImageWidth = e.Bitmap.PixelWidth;
            sensorData.colorImageHeight = e.Bitmap.PixelHeight;

            int imageLen = e.Bitmap.PixelWidth * e.Bitmap.PixelHeight * 4;

            lock (_colorDataLock)
            {
                //_colorDataBuf = new byte[imageLen];
                //sensorData.colorImage = new byte[imageLen];
                Array.Resize<byte>(ref _colorDataBuf, imageLen);
                Array.Resize<byte>(ref sensorData.colorImage, imageLen);
            }
        }

        if (_colorDataBuf != null)
        {
            // convert the bitmap
            SoftwareBitmap convertedBitmap = SoftwareBitmap.Convert(e.Bitmap, BitmapPixelFormat.Rgba8, BitmapAlphaMode.Straight);

            lock (_colorDataLock)
            {
                convertedBitmap?.CopyToBuffer(_colorDataBuf.AsBuffer());
                convertedBitmap?.Dispose();

                if (_saveLatestFrames)
                {
                    _latestColorFrame = e.Frame;
                }

                _colorDataTime = DateTime.Now.Ticks; // colorFrame.RelativeTime.Ticks;
                _colorDataReady = true;
            }

        }
    }

    private void DepthReader_FrameArrived(object sender, DepthFrameArrivedEventArgs e)
    {
        _depthCameraIntrinsics = e.CameraIntrinsics;

        if (_depthDataBuf == null || sensorData.depthImageWidth != e.Bitmap.PixelWidth || sensorData.depthImageHeight != e.Bitmap.PixelHeight)
        {
            sensorData.depthImageWidth = e.Bitmap.PixelWidth;
            sensorData.depthImageHeight = e.Bitmap.PixelHeight;

            int imageLen = e.Bitmap.PixelWidth * e.Bitmap.PixelHeight * sizeof(ushort);

            lock (_depthDataLock)
            {
                //_depthDataBuf = new byte[imageLen];
                //sensorData.depthImage = new ushort[e.Bitmap.PixelWidth * e.Bitmap.PixelHeight];
                Array.Resize<byte>(ref _depthDataBuf, imageLen);
                Array.Resize<ushort>(ref sensorData.depthImage, e.Bitmap.PixelWidth * e.Bitmap.PixelHeight);
            }

            int biImageLen = e.Bitmap.PixelWidth * e.Bitmap.PixelHeight;

            lock (_bodyIndexDataLock)
            {
                //_bodyIndexDataBuf = new byte[biImageLen];
                //sensorData.bodyIndexImage = new byte[biImageLen];
                Array.Resize<byte>(ref _bodyIndexDataBuf, biImageLen);
                Array.Resize<byte>(ref sensorData.bodyIndexImage, biImageLen);
            }
        }

        if (_depthDataBuf != null)
        {
            lock (_depthDataLock)
            {
                e.Bitmap.CopyToBuffer(_depthDataBuf.AsBuffer());

                if (_saveLatestFrames)
                {
                    _latestDepthFrame = e.Frame;
                }

                _depthDataTime = DateTime.Now.Ticks; // depthFrame.RelativeTime.Ticks;
                _depthDataReady = true;
            }

        }
    }

    private void BodyIndexReader_FrameArrived(object sender, BodyIndexFrameArrivedEventArgs e)
    {
        if (_bodyIndexDataBuf != null)
        {
            lock (_bodyIndexDataLock)
            {
                e.Bitmap.CopyToBuffer(_bodyIndexDataBuf.AsBuffer());

                if (_saveLatestFrames)
                {
                    _latestBodyIndexFrame = e.Frame;
                }

                _bodyIndexDataTime = DateTime.Now.Ticks; // bodyIndexFrame.RelativeTime.Ticks;
                _bodyIndexDataReady = true;
            }

        }
    }

    private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
    {
        lock (_bodyFrameLock)
        {
            _bodyFrame = e.BodyFrame;

            if (_saveLatestFrames)
            {
                _latestBodyFrame = e.Frame;
            }

            _bodyFrameTime = DateTime.Now.Ticks; // _bodyFrame.SystemRelativeTime.Value.Ticks;
            _bodyFrameReady = true;
        }
    }

    public void CloseSensor(KinectInterop.SensorData sensorData)
    {
        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
			if(_kinectSensor != null)
			{
				_kinectSensor?.CloseAsync();
				Debug.Log("UWP-K2 sensor closed");
			}
        }, true);

        if (_depthPlaneCoordsBuf != null)
        {
            _depthPlaneCoordsBuf.Release();
            _depthPlaneCoordsBuf = null;
        }

        if (_depthDepthValuesBuf != null)
        {
            _depthDepthValuesBuf.Release();
            _depthDepthValuesBuf = null;
        }

        if (_colorPlaneCoordsBuf != null)
        {
            _colorPlaneCoordsBuf.Release();
            _colorPlaneCoordsBuf = null;
        }

        if (_colorSpaceCoordsBuf != null)
        {
            _colorSpaceCoordsBuf.Release();
            _colorSpaceCoordsBuf = null;
        }

        if (_colorDepthCoordsBuf != null)
        {
            _colorDepthCoordsBuf.Release();
            _colorDepthCoordsBuf = null;
        }

        _colorCameraIntrinsics = null;
        _depthCameraIntrinsics = null;
        _coordinateMapper = null;
        _coordinateMapper2 = null;

        _coordMapperShader = null;
        _lastDepthDataBuf = null;

        _clearLatestFrames = true;
        FreeMultiSourceFrame(sensorData);
    }

    public bool UpdateSensorData(KinectInterop.SensorData sensorData)
    {
        return true;
    }

    public bool GetMultiSourceFrame(KinectInterop.SensorData sensorData)
    {
        if (_saveLatestFrames)
        {
            bool bAllSet =
                ((sensorFlags & KinectInterop.FrameSource.TypeColor) == 0 || _latestColorFrame != null) &&
                ((sensorFlags & KinectInterop.FrameSource.TypeDepth) == 0 || _latestDepthFrame != null) &&
                ((sensorFlags & KinectInterop.FrameSource.TypeBodyIndex) == 0 || _latestBodyIndexFrame != null) &&
                ((sensorFlags & KinectInterop.FrameSource.TypeBody) == 0 || _latestBodyFrame != null) &&
                ((sensorFlags & KinectInterop.FrameSource.TypeInfrared) == 0 || _latestInfraredFrame != null);

            return bAllSet;
        }

        return false;
    }

    public void FreeMultiSourceFrame(KinectInterop.SensorData sensorData)
    {
        if (_clearLatestFrames)
        {
            lock (_colorDataLock)
            {
                _latestColorFrame = null;
            }

            lock (_depthDataLock)
            {
                _latestDepthFrame = null;
            }

            lock (_bodyIndexDataLock)
            {
                _latestBodyIndexFrame = null;
            }

            lock (_bodyFrameLock)
            {
                _latestBodyFrame = null;
            }

            _clearLatestFrames = false;
        }
    }

    public bool PollBodyFrame(KinectInterop.SensorData sensorData, ref KinectInterop.BodyFrameData bodyFrame, ref Matrix4x4 kinectToWorld, bool bIgnoreJointZ)
    {
        bool bNewFrame = _bodyFrameReady;

        if (_bodyFrameReady)
        {
            lock (_bodyFrameLock)
            {
                bodyFrame.liPreviousTime = bodyFrame.liRelativeTime;
                bodyFrame.liRelativeTime = _bodyFrameTime;

                if (sensorData.hintHeightAngle)
                {
                    //// get the floor plane
                    //Windows.Kinect.Vector4 vFloorPlane = _bodyFrame.FloorClipPlane;
                    //Vector3 floorPlane = new Vector3(vFloorPlane.X, vFloorPlane.Y, vFloorPlane.Z);

                    //sensorData.sensorRotDetected = Quaternion.FromToRotation(floorPlane, Vector3.up);
                    //sensorData.sensorHgtDetected = vFloorPlane.W;
                }

                for (int i = 0; i < sensorData.bodyCount; i++)
                {
                    Body body = i < _bodyFrame.Bodies.Length ? _bodyFrame.Bodies[i] : null;

                    if (body == null)
                    {
                        bodyFrame.bodyData[i].bIsTracked = 0;
                        continue;
                    }

                    bodyFrame.bodyData[i].bIsTracked = (short)(body.IsTracked ? 1 : 0);

                    if (body.IsTracked)
                    {
                        // transfer body and joints data
                        byte[] entityBytes = body.EntityId.ToByteArray();
                        bodyFrame.bodyData[i].liTrackingID = BitConverter.ToInt64(entityBytes, 8);

                        // cache the body joints (following the advice of Brian Chasalow)
                        //Dictionary<Windows.Kinect.JointType, Windows.Kinect.Joint> bodyJoints = body.Joints;

//                        // calculate the inter-frame time
//                        float frameTime = 0f;
//                        if (bodyFrame.bTurnAnalisys && bodyFrame.liPreviousTime > 0)
//                        {
//                            frameTime = (float)(bodyFrame.liRelativeTime - bodyFrame.liPreviousTime) / 100000000000;
//                        }

                        for (int j = 0; j < sensorData.jointCount; j++)
                        {
                            if (j >= body.Joints.Count)
                                continue;

                            MultiK2.Tracking.Joint joint = body.Joints[(MultiK2.Tracking.JointType)j];
                            KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];

                            //jointData.jointType = (KinectInterop.JointType)j;
                            jointData.trackingState = (KinectInterop.TrackingState)joint.PositionTrackingState;

                            if ((int)joint.PositionTrackingState != (int)TrackingState.NotTracked)
                            {
                                float jPosZ = (bIgnoreJointZ && j > 0) ? bodyFrame.bodyData[i].joint[0].kinectPos.z : joint.Position.Z;
                                jointData.kinectPos = new Vector3(joint.Position.X, joint.Position.Y, joint.Position.Z);
                                jointData.position = kinectToWorld.MultiplyPoint3x4(new Vector3(joint.Position.X, joint.Position.Y, jPosZ));
                            }

                            jointData.orientation = Quaternion.identity;

                            if (j == 0)
                            {
                                bodyFrame.bodyData[i].position = jointData.position;
                                bodyFrame.bodyData[i].orientation = jointData.orientation;
                            }

                            bodyFrame.bodyData[i].joint[j] = jointData;
                        }

                        //if (bodyFrame.bTurnAnalisys && bodyFrame.liPreviousTime > 0)
                        //{
                        //    for (int j = 0; j < sensorData.jointCount; j++)
                        //    {
                        //        KinectInterop.JointData jointData = bodyFrame.bodyData[i].joint[j];

                        //        int p = (int)GetParentJoint((KinectInterop.JointType)j);
                        //        Vector3 parentPos = bodyFrame.bodyData[i].joint[p].position;

                        //        jointData.posRel = jointData.position - parentPos;
                        //        jointData.posDrv = frameTime > 0f ? (jointData.position - jointData.posPrev) / frameTime : Vector3.zero;
                        //        jointData.posPrev = jointData.position;

                        //        bodyFrame.bodyData[i].joint[j] = jointData;
                        //    }
                        //}

                        // tranfer hand states
                        bodyFrame.bodyData[i].leftHandState = (KinectInterop.HandState)body.HandStateLeft;
                        bodyFrame.bodyData[i].leftHandConfidence = (KinectInterop.TrackingConfidence)body.ConfidenceLeft;

                        bodyFrame.bodyData[i].rightHandState = (KinectInterop.HandState)body.HandStateRight;
                        bodyFrame.bodyData[i].rightHandConfidence = (KinectInterop.TrackingConfidence)body.ConfidenceRight;
                    }
                }

                _bodyFrameReady = false;
            }
        }

        return bNewFrame;
    }

    public bool PollColorFrame(KinectInterop.SensorData sensorData)
    {
        bool bNewFrame = _colorDataReady;

        if (_colorDataReady)
        {
            lock (_colorDataLock)
            {
                Buffer.BlockCopy(_colorDataBuf, 0, sensorData.colorImage, 0, _colorDataBuf.Length);
                sensorData.lastColorFrameTime = _colorDataTime;
                _colorDataReady = false;
            }
        }

        return bNewFrame;
    }

    public bool PollDepthFrame(KinectInterop.SensorData sensorData)
    {
        bool bNewFrame = _depthDataReady || _bodyIndexDataReady;

        if (_depthDataReady)
        {
            if (_isDoubleDepthBufNeeded)
            {
                if (_lastDepthDataBuf == null)
                {
                    _lastDepthDataBuf = new ushort[sensorData.depthImage.Length];
                }

                Buffer.BlockCopy(sensorData.depthImage, 0, _lastDepthDataBuf, 0, _lastDepthDataBuf.Length * sizeof(ushort));
                _lastDepthDataTime = sensorData.lastDepthFrameTime;
            }

            lock (_depthDataLock)
            {
                Buffer.BlockCopy(_depthDataBuf, 0, sensorData.depthImage, 0, _depthDataBuf.Length);
                sensorData.lastDepthFrameTime = _depthDataTime;
                _depthDataReady = false;
            }
        }

        if (_bodyIndexDataReady)
        {
            lock (_bodyIndexDataLock)
            {
                Buffer.BlockCopy(_bodyIndexDataBuf, 0, sensorData.bodyIndexImage, 0, _bodyIndexDataBuf.Length);
                sensorData.lastBodyIndexFrameTime = _bodyIndexDataTime;
                _bodyIndexDataReady = false;
            }
        }

        return bNewFrame;
    }

    public bool PollInfraredFrame(KinectInterop.SensorData sensorData)
    {
        return false;
    }

    public void FixJointOrientations(KinectInterop.SensorData sensorData, ref KinectInterop.BodyData bodyData)
    {
    }

    public bool IsBodyTurned(ref KinectInterop.BodyData bodyData)
    {
        return false;
    }

    public Vector2 MapSpacePointToDepthCoords(KinectInterop.SensorData sensorData, Vector3 spacePos)
    {
        Vector2 vPoint = Vector2.zero;

        if (_depthCameraIntrinsics != null)
        {
            System.Numerics.Vector3 camPoint = new System.Numerics.Vector3(spacePos.x, spacePos.y, spacePos.z);
            System.Numerics.Vector2 depthPoint = _depthCameraIntrinsics.ProjectOntoFrame(camPoint);

            if (depthPoint.X >= 0 && depthPoint.X < sensorData.depthImageWidth &&
               depthPoint.Y >= 0 && depthPoint.Y < sensorData.depthImageHeight)
            {
                vPoint = new Vector2(depthPoint.X, depthPoint.Y);
            }
        }

        return vPoint;
    }

    public Vector3 MapDepthPointToSpaceCoords(KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
    {
        Vector3 vPoint = Vector3.zero;

        if (_depthCameraIntrinsics != null && depthPos != Vector2.zero)
        {
            System.Numerics.Vector2 depthPoint = new System.Numerics.Vector2(depthPos.x, depthPos.y);
            System.Numerics.Vector3 camPoint = _depthCameraIntrinsics.UnprojectFromFrame(depthPoint, (float)depthVal / 1000f);

            vPoint = new Vector3(camPoint.X, camPoint.Y, camPoint.Z);
        }

        return vPoint;
    }

    public bool MapDepthFrameToSpaceCoords(KinectInterop.SensorData sensorData, ref Vector3[] vSpaceCoords)
    {
        _isDoubleDepthBufNeeded = true;

        if (_depthCameraIntrinsics != null && sensorData.depthImage != null)
        {
            int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;

            if (_depth2SpaceTable == null || _depth2SpaceTable.Length != depthImageLength)
            {
                _depth2SpaceTable = new Vector3[depthImageLength];

                for (int dy = 0, di = 0; dy < sensorData.depthImageHeight; dy++)
                {
                    for (int dx = 0; dx < sensorData.depthImageWidth; dx++)
                    {
                        System.Numerics.Vector2 depthPoint = new System.Numerics.Vector2(dx, dy);
                        System.Numerics.Vector3 camPoint = _depthCameraIntrinsics.UnprojectFromFrame(depthPoint, 1f);

                        _depth2SpaceTable[di] = new Vector3(camPoint.X, camPoint.Y, camPoint.Z);
                        di++;
                    }
                }
            }

            if (_lastDepthDataBuf != null && !_depth2spaceTaskStarted)
            {
                _depth2spaceTaskStarted = true;

                //long timeStamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                //Task.Run( () => {

                for (int dy = 0, di = 0; dy < sensorData.depthImageHeight; dy++)
                {
                    for (int dx = 0; dx < sensorData.depthImageWidth; dx++)
                    {
                        if (di >= 0 && di < _lastDepthDataBuf.Length &&
                            sensorData.depthImage[di] != _lastDepthDataBuf[di])
                        {
                            if (sensorData.depthImage[di] != 0)
                            {
                                float depthVal = (float)sensorData.depthImage[di] / 1000f;
                                vSpaceCoords[di] = _depth2SpaceTable[di] * depthVal;
                            }
                            else
                            {
                                vSpaceCoords[di] = Vector3.zero;
                            }
                        }

                        di++;
                    }
                }

                _depth2spaceTaskStarted = false;
                //});

                //long timeDuration = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - timeStamp);
                //Debug.Log("depth2spaceTask() took " + timeDuration + " ms");
            }
        }

        return true;
    }

    public Vector2 MapDepthPointToColorCoords(KinectInterop.SensorData sensorData, Vector2 depthPos, ushort depthVal)
    {
        Vector2 vPoint = Vector2.zero;

        if (_coordinateMapper != null && _depthCameraIntrinsics != null && _colorCameraIntrinsics != null && depthPos != Vector2.zero)
        {
            System.Numerics.Vector2 depthPoint = new System.Numerics.Vector2(depthPos.x, depthPos.y);
            System.Numerics.Vector3 depthSpace = _depthCameraIntrinsics.UnprojectFromFrame(depthPoint, (float)depthVal / 1000f);
            System.Numerics.Vector3 colorSpace = _coordinateMapper.MapDepthSpacePointToColor(depthSpace);
            System.Numerics.Vector2 colorPoint = _colorCameraIntrinsics.ProjectOntoFrame(colorSpace);

            vPoint = new Vector2(colorPoint.X, colorPoint.Y);
        }

        return vPoint;
    }

    public bool MapDepthFrameToColorCoords(KinectInterop.SensorData sensorData, ref Vector2[] vColorCoords)
    {
        bool bReadyToMap = //_saveLatestFrames ? (_latestColorFrame != null && _latestDepthFrame != null) :
            sensorData.depthImage != null && sensorData.colorImage != null;

        if (bReadyToMap)
        {
            if (_coordMapperShader == null || _colorPlaneCoordsBuf == null)
            {
                CreateCoordMapperShader(sensorData, false);
            }

            if (_coordMapperShader)
            {
                //long timeStamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;
                int[] depthDepthValues = new int[depthImageLength];

                for (int di = 0; di < depthImageLength; di++)
                {
                    depthDepthValues[di] = sensorData.depthImage[di];
                }

                _depthDepthValuesBuf.SetData(depthDepthValues);

                _coordMapperShader.Dispatch(_depth2colorKernel, depthImageLength / 64, 1, 1);

                if (vColorCoords == null || vColorCoords.Length != depthImageLength)
                {
                    vColorCoords = new Vector2[depthImageLength];
                }

                _colorPlaneCoordsBuf.GetData(vColorCoords);

                //long timeDuration = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - timeStamp);
                //Debug.Log("depth2colorTask() took " + timeDuration + " ms");
            }

            _clearLatestFrames = true;
        }

        return true;
    }

    private bool CreateCoordMapperShader(KinectInterop.SensorData sensorData, bool bColor2Depth)
    {
        if (_depthCameraIntrinsics == null || _colorCameraIntrinsics == null || _coordinateMapper == null)
            return false;

        System.Numerics.Matrix4x4? matrix = !bColor2Depth ? _coordinateMapper.DepthToColorMatrix : _coordinateMapper.ColorToDepthMatrix;
        if (_coordMapperShader == null)
        {
            _coordMapperShader = matrix.HasValue ? Resources.Load("CoordMapper") as ComputeShader : null;
        }

        if (_coordMapperShader)
        {
            _depth2colorKernel = _coordMapperShader.FindKernel("MapDepthFrame2ColorFrame");
            _color2depthKernel = _coordMapperShader.FindKernel("MapColorSpace2DepthFrame");

            float[] depthFocalLength = new float[] { _depthCameraIntrinsics.FocalLengthX, _depthCameraIntrinsics.FocalLengthY };
            float[] depthPrincipalPoint = new float[] { _depthCameraIntrinsics.PrincipalPointX, _depthCameraIntrinsics.PrincipalPointY };
            float[] depthRadialDistortion = new float[] { _depthCameraIntrinsics.RadialDistortionSecondOrder, _depthCameraIntrinsics.RadialDistortionFourthOrder, _depthCameraIntrinsics.RadialDistortionSixthOrder };

            _coordMapperShader.SetFloats("depthFocalLength", depthFocalLength);
            _coordMapperShader.SetFloats("depthPrincipalPoint", depthPrincipalPoint);
            _coordMapperShader.SetFloats("depthRadialDistortion", depthRadialDistortion);

            float[] colorFocalLength = new float[] { _colorCameraIntrinsics.FocalLengthX, _colorCameraIntrinsics.FocalLengthY };
            float[] colorPrincipalPoint = new float[] { _colorCameraIntrinsics.PrincipalPointX, _colorCameraIntrinsics.PrincipalPointY };
            float[] colorRadialDistortion = new float[] { _colorCameraIntrinsics.RadialDistortionSecondOrder, _colorCameraIntrinsics.RadialDistortionFourthOrder, _colorCameraIntrinsics.RadialDistortionSixthOrder };

            _coordMapperShader.SetFloats("colorFocalLength", colorFocalLength);
            _coordMapperShader.SetFloats("colorPrincipalPoint", colorPrincipalPoint);
            _coordMapperShader.SetFloats("colorRadialDistortion", colorRadialDistortion);

            float[] space2spaceMat = new float[] {
                    matrix.Value.M11, matrix.Value.M12, matrix.Value.M13, matrix.Value.M14,
                    matrix.Value.M21, matrix.Value.M22, matrix.Value.M23, matrix.Value.M24,
                    matrix.Value.M31, matrix.Value.M32, matrix.Value.M33, matrix.Value.M34,
                    matrix.Value.M41, matrix.Value.M42, matrix.Value.M43, matrix.Value.M44
                };

            if (!bColor2Depth)
            {
                _coordMapperShader.SetFloats("depth2colorMat", space2spaceMat);
            }
            else
            {
                _coordMapperShader.SetFloats("color2depthMat", space2spaceMat);
            }

            // compute buffers
            int depthImageLength = sensorData.depthImageWidth * sensorData.depthImageHeight;

            if(_depthDepthValuesBuf == null)
            {
                _depthDepthValuesBuf = new ComputeBuffer(depthImageLength, sizeof(int));
                _coordMapperShader.SetBuffer(_depth2colorKernel, "depthDepthValues", _depthDepthValuesBuf);
            }

            if (!bColor2Depth)
            {
                _depthPlaneCoordsBuf = new ComputeBuffer(depthImageLength, 2 * sizeof(float));
                _colorPlaneCoordsBuf = new ComputeBuffer(depthImageLength, 2 * sizeof(float));

                // set plane coords
                Vector2[] depthPlaneCoords = new Vector2[depthImageLength];
                for (int dy = 0, di = 0; dy < sensorData.depthImageHeight; dy++)
                {
                    for (int dx = 0; dx < sensorData.depthImageWidth; dx++)
                    {
                        depthPlaneCoords[di] = new Vector2(dx, dy);
                        di++;
                    }
                }

                _depthPlaneCoordsBuf.SetData(depthPlaneCoords);
                _coordMapperShader.SetBuffer(_depth2colorKernel, "depthPlaneCoords", _depthPlaneCoordsBuf);
                _coordMapperShader.SetBuffer(_depth2colorKernel, "colorPlaneCoords", _colorPlaneCoordsBuf);
            }
            else
            {
                int colorImageLength = sensorData.colorImageWidth * sensorData.colorImageHeight;

                _colorSpaceCoordsBuf = new ComputeBuffer(colorImageLength, 3 * sizeof(float));
                _colorDepthCoordsBuf = new ComputeBuffer(colorImageLength, 2 * sizeof(float));

                _coordMapperShader.SetBuffer(_color2depthKernel, "colorSpaceCoords", _colorSpaceCoordsBuf);
                _coordMapperShader.SetBuffer(_color2depthKernel, "colorDepthCoords", _colorDepthCoordsBuf);
            }
        }

        return (_coordMapperShader != null);
    }

    public bool MapColorFrameToDepthCoords(KinectInterop.SensorData sensorData, ref Vector2[] vDepthCoords)
    {
        if (_coordMapperShader == null || _colorDepthCoordsBuf == null)
        {
            CreateCoordMapperShader(sensorData, true);
        }

        bool bReadyToMap = _saveLatestFrames ? (_latestColorFrame != null && _latestDepthFrame != null && _latestBodyFrame != null) : 
            sensorData.depthImage != null && sensorData.colorImage != null;

        if (bReadyToMap)
        {
            //long timeStamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

            if (_coordMapperShader && _coordinateMapper2 != null &&
                _coordinateMapper2.MapColorFrameToDepthSpace(_latestColorFrame, _latestDepthFrame, ref _color2SpacePoints))
            {
                //long timeDuration = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - timeStamp);
                //Debug.Log("mapColorFrameToDepthSpace() took " + timeDuration + " ms");

                int pointArrayLength = _color2SpacePoints.Length;

                //timeStamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

                _colorSpaceCoordsBuf.SetData(_color2SpacePoints);
                //_coordMapperShader.SetBuffer(_color2depthKernel, "colorSpaceCoords", _colorSpaceCoordsBuf);

                _coordMapperShader.Dispatch(_color2depthKernel, pointArrayLength / 64, 1, 1);

                if (vDepthCoords == null || vDepthCoords.Length != pointArrayLength)
                {
                    vDepthCoords = new Vector2[pointArrayLength];
                }

                _colorDepthCoordsBuf.GetData(vDepthCoords);

                //timeDuration = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond - timeStamp);
                //Debug.Log("color2DepthTask() took " + timeDuration + " ms");
            }

            _clearLatestFrames = true;
        }

        return true;
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
		return true;
	}

	public int InitSpeechRecognition (string sRecoCriteria, bool bUseKinect, bool bAdaptationOff)
	{
        speechRecognizer = new SpeechRecognizer();
        return 0;
	}

	public void FinishSpeechRecognition ()
	{
        if(speechRecognizer != null)
        {
            speechRecognizer.Dispose();
            speechRecognizer = null;
        }
    }

	public int UpdateSpeechRecognition ()
	{
        if(speechRecognizer != null)
        {
            if(speechRecognizeTask == null)
            {
                UnityEngine.WSA.Application.InvokeOnUIThread(() =>
                {
                    speechRecognizeTask = RecognizeSpeechAsync();
                }, true);
            }

            if (speechRecognizeTask != null)
            {
                // check for error
                if (speechRecognizeTask.IsFaulted)
                {
                    Debug.LogError("RecognizeSpeechAsync() has faulted.");
                    if (speechRecognizeTask.Exception != null)
                        Debug.LogError(speechRecognizeTask.Exception);

                    speechRecognizeTask = null;
                }
                else if(speechRecognizeTask.IsCanceled)
                {
                    speechRecognizeTask = null;
                }
                else if(speechRecognizeTask.IsCompleted)
                {
                    SpeechRecognitionResult result = speechRecognizeTask.Result;

                    if(result.Status == SpeechRecognitionResultStatus.Success)
                    {
                        if(result.Confidence != SpeechRecognitionConfidence.Rejected)
                        {
                            //Debug.LogError("Phrase: " + result.Text + ", Confidence: " + result.Confidence.ToString() + ", RawConf: " + result.RawConfidence);

                            float fConfidence = (float)result.RawConfidence; // (3f - (float)result.Confidence) / 3f;
                            if(fConfidence >= requiredPhraseConfidence)
                            {
                                isPhraseRecognized = true;
                                recognizedPhraseTag = result.SemanticInterpretation.Properties.ContainsKey("<ROOT>") ?
                                    result.SemanticInterpretation.Properties["<ROOT>"][0] : result.Text;
                                recognizedPhraseConfidence = fConfidence;
                            }
                        }
                    }
                    //else
                    //{
                    //    Debug.LogError("Speech recognition failed: " + result.Status.ToString());
                    //}

                    speechRecognizeTask = null;
                }
            }
        }

        return 0;
	}

    private async Task<SpeechRecognitionResult> RecognizeSpeechAsync()
    {
        SpeechRecognitionResult result = null;

        if (speechRecognizer != null)
        {
            result = await speechRecognizer.RecognizeAsync();
        }

        return result;
    }


	public int LoadSpeechGrammar (string sFileName, short iLangCode, bool bDynamic)
	{
        Task<int> task = null;

        UnityEngine.WSA.Application.InvokeOnUIThread(() =>
        {
            task = LoadGrammarFileAsync(sFileName);
        }, true);

        while (task != null && !task.IsCompleted && !task.IsFaulted)
        {
            task.Wait(100);
        }

        if(task.IsFaulted)
        {
            Debug.LogError("LoadGrammarFileAsync() has faulted.");
            if (task.Exception != null)
                Debug.LogError(task.Exception);

            return -1;
        }

        return 0;
	}

    private async Task<int> LoadGrammarFileAsync(string sFileName)
    {
        if(speechRecognizer != null)
        {
            string sUrl = "ms-appdata:///local/" + sFileName;
            var storageFile = await Windows.Storage.StorageFile.GetFileFromApplicationUriAsync(new Uri(sUrl));
            var grammarFile = new SpeechRecognitionGrammarFileConstraint(storageFile, sFileName);

            speechRecognizer.Constraints.Add(grammarFile);
            await speechRecognizer.CompileConstraintsAsync();
        }

        return 0;
    }

	public int AddGrammarPhrase (string sFromRule, string sToRule, string sPhrase, bool bClearRulePhrases, bool bCommitGrammar)
	{
		return -1;
	}

	public void SetSpeechConfidence (float fConfidence)
	{
        requiredPhraseConfidence = fConfidence;
    }

	public bool IsSpeechStarted ()
	{
		return speechRecognizer != null ? speechRecognizer.State == SpeechRecognizerState.SoundStarted : false;
	}

	public bool IsSpeechEnded ()
	{
        return speechRecognizer != null ? speechRecognizer.State == SpeechRecognizerState.SoundEnded : false;
    }

	public bool IsPhraseRecognized ()
	{
		return isPhraseRecognized;
	}

	public float GetPhraseConfidence ()
	{
		return recognizedPhraseConfidence;
	}

	public string GetRecognizedPhraseTag ()
	{
		return recognizedPhraseTag;
	}

	public void ClearRecognizedPhrase ()
	{
        isPhraseRecognized = false;
        recognizedPhraseTag = string.Empty;
        recognizedPhraseConfidence = 0f;
    }

	public bool IsBackgroundRemovalAvailable (ref bool bNeedRestart)
	{
		return true;
	}

	public bool InitBackgroundRemoval (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
        _backgroundRemovalInited = KinectInterop.InitBackgroundRemoval(sensorData, isHiResPrefered);
        return _backgroundRemovalInited;
	}

	public void FinishBackgroundRemoval (KinectInterop.SensorData sensorData)
	{
        KinectInterop.FinishBackgroundRemoval(sensorData);
        _backgroundRemovalInited = false;
    }

	public bool UpdateBackgroundRemoval (KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bAlphaTexOnly)
	{
        return KinectInterop.UpdateBackgroundRemoval(sensorData, isHiResPrefered, defaultColor, bAlphaTexOnly);
    }

	public bool IsBackgroundRemovalActive ()
	{
		return _backgroundRemovalInited;
	}

	public bool IsBRHiResSupported ()
	{
		return true;
	}

	public Rect GetForegroundFrameRect (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
        return KinectInterop.GetForegroundFrameRect(sensorData, isHiResPrefered);
    }

	public int GetForegroundFrameLength (KinectInterop.SensorData sensorData, bool isHiResPrefered)
	{
        return KinectInterop.GetForegroundFrameLength(sensorData, isHiResPrefered);
    }

	public bool PollForegroundFrame (KinectInterop.SensorData sensorData, bool isHiResPrefered, Color32 defaultColor, bool bLimitedUsers, System.Collections.Generic.ICollection<int> alTrackedIndexes, ref byte[] foregroundImage)
	{
        return KinectInterop.PollForegroundFrame(sensorData, isHiResPrefered, defaultColor, bLimitedUsers, alTrackedIndexes, ref foregroundImage);
    }

}
#endif
