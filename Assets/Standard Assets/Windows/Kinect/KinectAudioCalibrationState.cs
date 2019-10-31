#if (UNITY_STANDALONE_WIN)
using RootSystem = System;
using System.Linq;
using System.Collections.Generic;
namespace Windows.Kinect
{
    //
    // Windows.Kinect.KinectAudioCalibrationState
    //
    public enum KinectAudioCalibrationState : int
    {
        Unknown                                  =0,
        CalibrationRequired                      =1,
        Calibrated                               =2,
    }

}
#endif
