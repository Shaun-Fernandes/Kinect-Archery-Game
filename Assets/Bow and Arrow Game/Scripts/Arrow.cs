using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class Arrow : MonoBehaviour {

    public bool isAttached = false;
	private bool isFired = false;
	Rigidbody rb;

	private void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	private void FixedUpdate()
	{
		if (isFired)
		{
			transform.forward = rb.velocity;
			//transform.LookAt(transform.position + rb.velocity);
		}
		if (transform.position.y < 0)
			Destroy(gameObject);
	}

	public void FireArrow()
	{
		isFired = true;
		GetComponent<TrailRenderer>().enabled = true;
	}

	private void OnTriggerStay(Collider other)
    {
        if (!isAttached && other.tag == "Bow")
        {
            ArrowManager.Instance.AttachArrowToBow();
			isAttached = true;
        }
    }

	private void OnTriggerEnter(Collider other)
	{
		if (isFired && other.CompareTag("Target"))
		{
			ArrowManager.score++;
			AutoFireScript.score++;
			//Vector3 pos = transform.position;
			isFired = false;
			//Vector3 lookDirection = transform.forward;
			Debug.LogError("Hit the target!!!");
			transform.SetParent(other.transform.parent.gameObject.transform, true);
			rb.isKinematic = true;
			rb.useGravity = false;
			//transform.rotation = rot;
			//transform.position = pos;
		}
	}

	private void OnCollisionEnter(Collision collision)
	{
		if (isFired)
		{
			isFired = false;
			Debug.LogError("Hit a random target!!! it was :" + collision);
			transform.SetParent(collision.transform.parent.gameObject.transform, true);
			rb.isKinematic = true;
			rb.useGravity = false;
			GetComponent<TrailRenderer>().autodestruct = true;
		}
	}
}
