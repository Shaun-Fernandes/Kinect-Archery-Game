#if (UNITY_STANDALONE_WIN)
using RootSystem = System;
using System.Linq;
using System.Collections.Generic;
namespace Windows.Kinect
{
    //
    // Windows.Kinect.TrackingConfidence
    //
    public enum TrackingConfidence : int
    {
        Low                                      =0,
        High                                     =1,
    }

}
#endif
