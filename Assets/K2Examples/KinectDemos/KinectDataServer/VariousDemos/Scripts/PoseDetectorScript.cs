using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class PoseDetectorScript : MonoBehaviour 
{
	[Tooltip("User avatar model, who needs to reach the target pose.")]
	public PoseModelHelper avatarModel;

	[Tooltip("Model in pose that need to be reached by the user.")]
	public PoseModelHelper poseModel;

	[Tooltip("List of joints to compare.")]
	public List<KinectInterop.JointType> poseJoints = new List<KinectInterop.JointType>();

	[Tooltip("Threshold, above which we consider the pose is matched.")]
	public float matchThreshold = 0.7f;

	[Tooltip("GUI-Text to display information messages.")]
	public UnityEngine.UI.Text infoText;

	// match percent (between 0 and 1)
	private float fMatchPercent = 0f;
	// whether the pose is matched or not
	private bool bPoseMatched = false;


	/// <summary>
	/// Gets the pose match percent.
	/// </summary>
	/// <returns>The match percent (value between 0 and 1).</returns>
	public float GetMatchPercent()
	{
		return fMatchPercent;
	}


	/// <summary>
	/// Determines whether the target pose is matched or not.
	/// </summary>
	/// <returns><c>true</c> if the target pose is matched; otherwise, <c>false</c>.</returns>
	public bool IsPoseMatched()
	{
		return bPoseMatched;
	}


	void Update () 
	{
		KinectManager kinectManager = KinectManager.Instance;
		AvatarController avatarCtrl = avatarModel ? avatarModel.gameObject.GetComponent<AvatarController>() : null;

		if(kinectManager != null && kinectManager.IsInitialized() && 
		   avatarModel != null && avatarCtrl && kinectManager.IsUserTracked(avatarCtrl.playerId))
		{
			// get mirrored state
			bool isMirrored = avatarCtrl.mirroredMovement;
			
			// get the difference
			string sDiffDetails = string.Empty;
			fMatchPercent = 1f - GetPoseDifference(isMirrored, true, ref sDiffDetails);
			bPoseMatched = (fMatchPercent >= matchThreshold);

			string sPoseMessage = string.Format("Pose match: {0:F0}% {1}", fMatchPercent * 100f, 
			                                    (bPoseMatched ? "- Matched" : ""));
			if(infoText != null)
			{
				infoText.text = sPoseMessage + "\n\n" + sDiffDetails;
			}
		}
		else
		{
			// no user found
			if(infoText != null)
			{
				infoText.text = "Try to match the pose on the left.";
			}
		}
	}


	// gets angle or percent difference in pose
	public float GetPoseDifference(bool isMirrored, bool bPercentDiff, ref string sDiffDetails)
	{
		float fAngleDiff = 0f;
		float fMaxDiff = 0f;
		sDiffDetails = string.Empty;

		KinectManager kinectManager = KinectManager.Instance;
		if(!kinectManager || !avatarModel || !poseModel || poseJoints.Count == 0)
		{
			return 0f;
		}

		// copy model rotation
		Quaternion poseSavedRotation = poseModel.GetBoneTransform(0).rotation;
		poseModel.GetBoneTransform(0).rotation = avatarModel.GetBoneTransform(0).rotation;

		StringBuilder sbDetails = new StringBuilder();
		sbDetails.Append("Joint differences:").AppendLine();

		for(int i = 0; i < poseJoints.Count; i++)
		{
			KinectInterop.JointType joint = poseJoints[i];
			KinectInterop.JointType nextJoint = kinectManager.GetNextJoint(joint);

			if(nextJoint != joint && (int)nextJoint >= 0 && (int)nextJoint < KinectInterop.Constants.MaxJointCount)
			{
				Transform avatarTransform1 = avatarModel.GetBoneTransform(avatarModel.GetBoneIndexByJoint(joint, isMirrored));
				Transform avatarTransform2 = avatarModel.GetBoneTransform(avatarModel.GetBoneIndexByJoint(nextJoint, isMirrored));

				Transform poseTransform1 = poseModel.GetBoneTransform(poseModel.GetBoneIndexByJoint(joint, isMirrored));
				Transform poseTransform2 = poseModel.GetBoneTransform(poseModel.GetBoneIndexByJoint(nextJoint, isMirrored));

				if(avatarTransform1 != null && avatarTransform2 != null && poseTransform1 != null && poseTransform2 != null)
				{
					Vector3 vAvatarBone = (avatarTransform2.position - avatarTransform1.position).normalized;
					Vector3 vPoseBone = (poseTransform2.position - poseTransform1.position).normalized;

					float fDiff = Vector3.Angle(vPoseBone, vAvatarBone);
					if(fDiff > 90f) fDiff = 90f;

					fAngleDiff += fDiff;
					fMaxDiff += 90f;  // we assume the max diff could be 90 degrees

					sbDetails.AppendFormat("{0} - {1:F0} deg.", joint, fDiff).AppendLine();
				}
				else
				{
					sbDetails.AppendFormat("{0} - n/a", joint).AppendLine();
				}
			}
		}

		poseModel.GetBoneTransform(0).rotation = poseSavedRotation;

		// calculate percent diff
		float fPercentDiff = 0f;
		if(bPercentDiff && fMaxDiff > 0f)
		{
			fPercentDiff = fAngleDiff / fMaxDiff;
		}

		// details info
		sbDetails.AppendLine();
		sbDetails.AppendFormat("Sum-Diff: - {0:F0} deg out of {1:F0} deg", fAngleDiff, fMaxDiff).AppendLine();
		sbDetails.AppendFormat("Percent-Diff: {0:F0}%", fPercentDiff * 100).AppendLine();
		sDiffDetails = sbDetails.ToString();
		
		return (bPercentDiff ? fPercentDiff : fAngleDiff);
	}

}
