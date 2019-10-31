using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MainCamer : MonoBehaviour {

	Animator anim;
	Camera camera;
	public GameObject menu;
	public GameObject score;

	// Use this for initialization
	void Start () {
		anim = GetComponent<Animator>();
		camera = GetComponent<Camera>();
	}
	
	// Update is called once per frame
	void Update ()
	{
		if (anim.GetCurrentAnimatorStateInfo(0).IsName("FollowDude"))
		{
			menu.SetActive(false);
			score.SetActive(true);
			StopCamera();
		}
	}

	void StopCamera()
	{
			camera.depth = -1;
			enabled = false;
	}

	public void HideButtons()
	{
		menu.SetActive(false);
		score.SetActive(true);
		StopCamera();
	}
}
