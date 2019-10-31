using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class LoadLevelWhenNoUser : MonoBehaviour 
{
	[Tooltip("Next level number. No level is loaded, if the number is negative.")]
	public int nextLevel = -1;

	[Tooltip("Whether to check for initialized KinectManager or not.")]
	public bool validateKinectManager = true;

	[Tooltip("UI-Text used to display the debug messages.")]
	public UnityEngine.UI.Text debugText;

	private bool levelLoaded = false;


	void Start()
	{
		if(validateKinectManager && debugText != null)
		{
			KinectManager manager = KinectManager.Instance;

			if(manager == null || !manager.IsInitialized())
			{
				debugText.text = "KinectManager is not initialized!";
				levelLoaded = true;
			}
		}
	}

	
	void Update() 
	{
		if(!levelLoaded && nextLevel >= 0)
		{
			KinectManager manager = KinectManager.Instance;
			
			if(manager != null && !manager.IsUserDetected())
			{
				levelLoaded = true;
				SceneManager.LoadScene(nextLevel);
			}
		}
	}
	
}
