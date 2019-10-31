using UnityEngine;
using System.Collections;

/// <summary>
/// Background color image is component that displays the color camera feed on GUI texture, usually the scene background.
/// </summary>
public class BackgroundColorImage : MonoBehaviour 
{
	[Tooltip("RawImage used to display the color camera feed.")]
	public UnityEngine.UI.RawImage backgroundImage;


	void Start()
	{
		if (backgroundImage == null) 
		{
			backgroundImage = GetComponent<UnityEngine.UI.RawImage>();
		}
	}


	void Update () 
	{
		KinectManager manager = KinectManager.Instance;

		if (manager && manager.IsInitialized()) 
		{
			if (backgroundImage && (backgroundImage.texture == null)) 
			{
				backgroundImage.texture = manager.GetUsersClrTex();
				backgroundImage.rectTransform.localScale = manager.GetColorImageScale();
				backgroundImage.color = Color.white;
			}
		}	
	}
}
