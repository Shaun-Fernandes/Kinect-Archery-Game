using UnityEngine;
using System.Collections;

public class SimpleVisualGestureListener : MonoBehaviour, VisualGestureListenerInterface
{
	[Tooltip("Index of the player, tracked by this component. 0 means the 1st player, 1 - the 2nd one, 2 - the 3rd one, etc.")]
	public int playerIndex = 0;

	[Tooltip("UI-Text to display the discrete gesture information.")]
	public UnityEngine.UI.Text discreteInfo;

	[Tooltip("UI-Text to display the continuous gesture information.")]
	public UnityEngine.UI.Text continuousInfo;


	private bool discreteGestureDisplayed;
	private bool continuousGestureDisplayed;

	private float discreteGestureTime;
	private float continuousGestureTime;


	public void GestureInProgress(long userId, int userIndex, string gesture, float progress)
	{
		if (userIndex != playerIndex)
			return;
		
		if(continuousInfo != null)
		{
			string sGestureText = string.Format ("{0} {1:F0}%", gesture, progress * 100f);
			continuousInfo.text = sGestureText;

			continuousGestureDisplayed = true;
			continuousGestureTime = Time.realtimeSinceStartup;
		}
	}

	public bool GestureCompleted(long userId, int userIndex, string gesture, float confidence)
	{
		if (userIndex != playerIndex)
			return false;

		if(discreteInfo != null)
		{
			string sGestureText = string.Format ("{0}-gesture detected, confidence: {1:F0}%", gesture, confidence * 100f);
			discreteInfo.text = sGestureText;

			discreteGestureDisplayed = true;
			discreteGestureTime = Time.realtimeSinceStartup;
		}

		// reset the gesture
		return true;
	}
	
	public void Update()
	{
		// clear gesture infos after a while
		if(continuousGestureDisplayed && ((Time.realtimeSinceStartup - continuousGestureTime) > 2f))
		{
			continuousGestureDisplayed = false;

			if(continuousInfo != null)
			{
				continuousInfo.text = string.Empty;
			}
		}

		if(discreteGestureDisplayed && ((Time.realtimeSinceStartup - discreteGestureTime) > 2f))
		{
			discreteGestureDisplayed = false;
			
			if(discreteInfo != null)
			{
				discreteInfo.text = string.Empty;
			}
		}
	}

}
