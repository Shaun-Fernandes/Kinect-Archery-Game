using UnityEngine;
using System.Collections;

public class OffsetNodeMover : MonoBehaviour 
{
	[Tooltip("Speed at which the offset node will move through the scene.")]
	public float moveSpeed = 1f;

	[Tooltip("Smooth factor used for offset node movements.")]
	public float smoothFactor = 1f;


	void Update () 
	{
		float h = Input.GetAxis ("Horizontal");
		float v = Input.GetAxis ("Vertical");

		if (h != 0f || v != 0f) 
		{
			Vector3 vMoveStep = new Vector3 (h * moveSpeed, 0f, v * moveSpeed);
			Vector3 vMoveTo = transform.position + vMoveStep;

			transform.position = Vector3.Lerp (transform.position, vMoveTo, Time.deltaTime * smoothFactor);
		}
	}

}
