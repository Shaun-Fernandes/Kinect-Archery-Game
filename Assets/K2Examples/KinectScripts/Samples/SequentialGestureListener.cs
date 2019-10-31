using UnityEngine;
//using Windows.Kinect;
using System.Collections;
using System;


public class SequentialGestureListener : MonoBehaviour, KinectGestures.GestureListenerInterface
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("UI-Text to display gesture-listener messages and gesture information.")]
	public UnityEngine.UI.Text gestureInfo;
	
	private long userId;
	private int nextStage = -1;


	// initialize stage 0 gestures
	private void InitStage0()
	{
		KinectManager manager = KinectManager.Instance;
		manager.ClearGestures(userId);

		manager.DetectGesture(userId, KinectGestures.Gestures.RaiseLeftHand);
		// add more gestures here

		if(gestureInfo != null)
		{
			gestureInfo.text = "RaiseLeftHand";
		}
	}
	
	// initialize stage 1 gestures
	private void InitStage1()
	{
		KinectManager manager = KinectManager.Instance;
		manager.ClearGestures(userId);

		manager.DetectGesture(userId, KinectGestures.Gestures.RaiseRightHand);
		// add more gestures here

		if(gestureInfo != null)
		{
			gestureInfo.text = "RaiseRightHand";
		}
	}

	public void UserDetected(long userId, int userIndex)
	{
		if (userIndex != playerIndex)
			return;

		this.userId = userId;
		this.nextStage = -1;

		InitStage0();
	}
	
	public void UserLost(long userId, int userIndex)
	{
		if (userIndex != playerIndex)
			return;

		this.userId = 0;
		this.nextStage = -1;

		if(gestureInfo != null)
		{
			gestureInfo.text = string.Empty;
		}
	}

	public void GestureInProgress(long userId, int userIndex, KinectGestures.Gestures gesture, 
	                              float progress, KinectInterop.JointType joint, Vector3 screenPos)
	{
		if (userIndex != playerIndex)
			return;

		// check for the progress of continuous gestures here
	}

	public bool GestureCompleted(long userId, int userIndex, KinectGestures.Gestures gesture, 
	                              KinectInterop.JointType joint, Vector3 screenPos)
	{
		if (userIndex != playerIndex)
			return false;

		string sGestureText = gesture + " detected";

		switch (gesture) 
		{
		case KinectGestures.Gestures.RaiseLeftHand:
			sGestureText = "RaiseLeftHand detected";
			// do something
			nextStage = 1; // this will setup gestures for stage 1
			break;

		case KinectGestures.Gestures.RaiseRightHand:
			sGestureText = "RaiseRightHand detected";
			// do something
			nextStage = 0; // this will setup gestures for stage 0
			break;
		}

		if(gestureInfo != null)
		{
			gestureInfo.text = sGestureText;
		}
		
		return true;
	}

	public bool GestureCancelled(long userId, int userIndex, KinectGestures.Gestures gesture, 
	                              KinectInterop.JointType joint)
	{
		if (userIndex != playerIndex)
			return false;

		return true;
	}

	public void Update()
	{
		switch (nextStage) 
		{
		case 0:
			InitStage0();
			break;

		case 1:
			InitStage1();
			break;
		}

		if (nextStage >= 0) 
		{
			nextStage = -1;
		}
	}
	
}
