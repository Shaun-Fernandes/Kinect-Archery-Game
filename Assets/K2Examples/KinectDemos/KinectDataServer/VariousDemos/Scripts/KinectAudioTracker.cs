#if (UNITY_STANDALONE_WIN)
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Kinect;
using Windows.Kinect;

using KinectAudioSource = Windows.Kinect.AudioSource;


public class KinectAudioTracker : MonoBehaviour
{
	[Tooltip("UI-Text to display status messages.")]
	public UnityEngine.UI.Text statusText;

	[Tooltip("Last observed audio beam angle in radians, in the range [-pi/2, +pi/2]")]
	[NonSerialized]
    public float beamAngleRad = 0;

	[Tooltip("Last observed audio beam angle in degrees, in the range [-180, +180]")]
	public float beamAngleDeg = 0;

	[Tooltip("Last observed audio beam angle confidence, in the range [0, 1]")]
	public float beamAngleConfidence = 0;

	private KinectAudioSource audioSource ;
	private AudioBeamFrameReader audioReader = null;


	void Start()
	{
		Kinect2Interface sensorInterface = KinectManager.Instance.GetSensorData().sensorInterface as Kinect2Interface;
		Windows.Kinect.KinectSensor kinectSensor = sensorInterface != null ? sensorInterface.kinectSensor : null;

		if (kinectSensor != null) 
		{
			this.audioSource = kinectSensor.AudioSource;
			this.audioReader = audioSource.OpenReader();

			Debug.Log("AudioSource IsActive: " + audioSource.IsActive);

			if (audioReader != null)
			{
				Debug.Log("KinectAudio successfully initialized.");
			}
			else
			{
				Debug.Log("KinectAudio initialization failed.");
			}
		}
	}

	void Update()
	{
		if (audioReader != null) 
		{
			ProcessAudioFrame();
		}
	}

	void OnDestroy()
	{
		if (audioReader != null)
		{
			this.audioReader.Dispose();
			this.audioReader = null;

			//Debug.Log("KinectAudio destroyed.");
		}
	}


	private void ProcessAudioFrame()
    {
		IList<AudioBeamFrame> frameList = audioReader.AcquireLatestBeamFrames();
		//AudioBeamFrameList frameList = (AudioBeamFrameList)reader.AcquireLatestBeamFrames();

        if (frameList != null)
        {
            if (frameList[0] != null)
            {
                if (frameList[0].SubFrames != null && frameList[0].SubFrames.Count > 0)
                {
                    // Only one audio beam is supported. Get the sub frame list for this beam
                    List<AudioBeamSubFrame> subFrameList = frameList[0].SubFrames.ToList();

                    // Loop over all sub frames, extract audio buffer and beam information
                    foreach (AudioBeamSubFrame subFrame in subFrameList)
                    {
                        // Check if beam angle and/or confidence have changed
                        bool updateBeam = false;

                        if (subFrame.BeamAngle != this.beamAngleRad)
                        {
                            this.beamAngleRad = subFrame.BeamAngle;
							this.beamAngleDeg = this.beamAngleRad * 180.0f / Mathf.PI;
                            updateBeam = true;

							//Debug.Log("beam angle: " + beamAngleDegrees);
                        }

                        if (subFrame.BeamAngleConfidence != this.beamAngleConfidence)
                        {
                            this.beamAngleConfidence = subFrame.BeamAngleConfidence;
                            updateBeam = true;

							//Debug.Log("beam angle confidence: " + beamAngleRadians);
                        }

                        if (updateBeam)
                        {
                            // Refresh display of audio beam
							if (statusText) 
							{
								statusText.text = string.Format("Audio beam angle: {0:F0} deg., Confidence: {1:F0}%", beamAngleDeg, beamAngleConfidence * 100f);
							}
                        }
                    }
                }
//                else
//                {
//                    this.beamAngle = frameList[0].AudioBeam.BeamAngle;
//                    Debug.Log("No SubFrame: "+ frameList[0].AudioBeam.BeamAngle);
//                }
            }
//            else
//            {
//                Debug.Log("Empty Audio Frame: "+ audioSource.AudioBeams.Count());
//                if (audioSource.AudioBeams.Count() > 0)
//                    Debug.Log(audioSource.AudioBeams[0].BeamAngle);
//
//            }
        }
//        else
//        {
//            Debug.Log("Empty Audio Frame");
//        }

		// clean up
		for(int i = frameList.Count - 1; i >= 0; i--)
		{
			AudioBeamFrame frame = frameList[i];

			if(frame != null)
			{
				frame.Dispose();
			}
		}

		//frameList.Clear();
    }

    private void Reader_FrameArrived(object sender, AudioBeamFrameArrivedEventArgs e)
    {
        ProcessAudioFrame();
    }

}
#endif
