using UnityEngine;
using System.Collections;

public class CameraPitchCorrector : MonoBehaviour 
{
	[Tooltip("Smooth factor used for the camera re-orientation.")]
	public float smoothFactor = 10f;
	
	void LateUpdate () 
	{
		Vector3 jointDir = transform.rotation * Vector3.up;
		Vector3 projectedDir = Vector3.ProjectOnPlane(jointDir, Vector3.forward);

		Quaternion invPitchRot = Quaternion.FromToRotation (projectedDir, Vector3.up);
		transform.rotation = Quaternion.Slerp(transform.rotation, transform.rotation * invPitchRot, smoothFactor * Time.deltaTime);
	}
}
