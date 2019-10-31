using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UserAvatarMatcher : MonoBehaviour 
{

	[Tooltip("Humanoid model used for avatar instatiation.")]
	public GameObject avatarModel;

	[Tooltip("Smooth factor used by the avatar controller.")]
	public float smoothFactor = 10f;

	[Tooltip("If enabled, makes the avatar position relative to this camera to be the same as the player's position to the sensor.")]
	public Camera posRelativeToCamera;

	[Tooltip("Whether the avatar is facing the player or not.")]
	public bool mirroredMovement = true;

	[Tooltip("Whether the avatar is allowed to move vertically or not.")]
	public bool verticalMovement = true;

	[Tooltip("Whether the avatar's feet must stick to the ground.")]
	public bool groundedFeet = false;

	[Tooltip("Whether to apply the humanoid model's muscle limits or not.")]
	public bool applyMuscleLimits = false;


	private KinectManager kinectManager;
	private int userCount = 0;

	private long userChecksum = 0;
	private Dictionary<long, GameObject> alUserAvatars = new Dictionary<long, GameObject>();


	void Start () 
	{
		kinectManager = KinectManager.Instance;
	}
	
	void Update () 
	{
		long checksum = GetUserChecksum(out userCount);

		if (userChecksum != checksum) 
		{
			userChecksum = checksum;
			List<long> alAvatarToRemove = new List<long>(alUserAvatars.Keys);

			for (int i = 0; i < userCount; i++) 
			{
				long userId = kinectManager.GetUserIdByIndex(i);

				if(alAvatarToRemove.Contains(userId))
					alAvatarToRemove.Remove(userId);

				if (!alUserAvatars.ContainsKey(userId)) 
				{
					Debug.Log("Creating avatar for userId: " + userId);

					// create avatar for the user
					int userIndex = kinectManager.GetUserIndexById(userId);
					GameObject avatarObj = CreateUserAvatar(userId, userIndex);

					alUserAvatars[userId] = avatarObj;
				}
			}

			// remove the missing users from the list
			foreach (long userId in alAvatarToRemove) 
			{
				if (alUserAvatars.ContainsKey(userId)) 
				{
					Debug.Log("Destroying avatar for userId: " + userId);

					GameObject avatarObj = alUserAvatars[userId];
					alUserAvatars.Remove(userId);

					// destroy the user's avatar
					DestroyUserAvatar(avatarObj);
				}
			}

		}
	}

	// returns the checksum of current users
	private long GetUserChecksum(out int userCount)
	{
		userCount = 0;
		long checksum = 0;

		if (kinectManager && kinectManager.IsInitialized ()) 
		{
			userCount = kinectManager.GetUsersCount();

			for (int i = 0; i < userCount; i++) 
			{
				checksum ^= kinectManager.GetUserIdByIndex(i);
			}
		}

		return checksum;
	}


	// creates avatar for the given user
	private GameObject CreateUserAvatar(long userId, int userIndex)
	{
		GameObject avatarObj = null;

		if (avatarModel) 
		{
			Vector3 userPos = new Vector3(userIndex, 0, 0);
			Quaternion userRot = Quaternion.Euler(!mirroredMovement ? Vector3.zero : new Vector3(0, 180, 0));

			avatarObj = Instantiate(avatarModel, userPos, userRot);
			avatarObj.name = "User-" + userId;

			AvatarController ac = avatarObj.GetComponent<AvatarController>();
			if (ac == null) 
			{
				ac = avatarObj.AddComponent<AvatarController>();
				ac.playerIndex = userIndex;

				ac.smoothFactor = smoothFactor;
				ac.posRelativeToCamera = posRelativeToCamera;

				ac.mirroredMovement = mirroredMovement;
				ac.verticalMovement = verticalMovement;

				ac.groundedFeet = groundedFeet;
				ac.applyMuscleLimits = applyMuscleLimits;
			}

			// start the avatar controller
			ac.SuccessfulCalibration(userId, false);

			// refresh the KM-list of available avatar controllers
			MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
			kinectManager.avatarControllers.Clear();

			foreach(MonoBehaviour monoScript in monoScripts)
			{
				if((monoScript is AvatarController) && monoScript.enabled)
				{
					AvatarController avatar = (AvatarController)monoScript;
					kinectManager.avatarControllers.Add(avatar);
				}
			}
		}

		return avatarObj;
	}

	// destroys the avatar and refreshes the list of avatar controllers
	private void DestroyUserAvatar(GameObject avatarObj)
	{
		if (avatarObj) 
		{
			Destroy(avatarObj);

			if (kinectManager) 
			{
				// refresh the KM-list of available avatar controllers
				MonoBehaviour[] monoScripts = FindObjectsOfType(typeof(MonoBehaviour)) as MonoBehaviour[];
				kinectManager.avatarControllers.Clear();

				foreach(MonoBehaviour monoScript in monoScripts)
				{
					if((monoScript is AvatarController) && monoScript.enabled)
					{
						AvatarController avatar = (AvatarController)monoScript;
						kinectManager.avatarControllers.Add(avatar);
					}
				}
			}

		}
	}

}
