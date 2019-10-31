using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class DeathMenu : MonoBehaviour {

    public Text scoreText;
    public GameObject gm;
	// Use this for initialization
	void Start () {
        gameObject.SetActive(false);
		
	}
	
	// Update is called once per frame
	void Update () {
		
	}

    public void ToggleEndMenu (int score)
    {
        gameObject.SetActive(true);
        scoreText.text = score.ToString();

    }

    public void Restart()
    {
        
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        Instantiate(gm);
    }

    public void Menu()
    {
        SceneManager.LoadScene(2);
    }
}
