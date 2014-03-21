/***************************************
Author: Nicku Bunzli - nbunzli@gmail.com
Feel free to steal, modify, distribute, make love to, or do whatever else you want with this code.
***************************************/

using UnityEngine;
using System.Collections;

public class Pickup : MonoBehaviour 
{
	public float LifeTime = 3.0f;
	public GameObject PickupParticles;
	public GameLogic Game;

	bool PickedUp = false;

	void Update () 
	{
		LifeTime -= Time.deltaTime;
		if(LifeTime <= 0.0f)
		{
			Game.NotifyPickupDestroyed(gameObject);
			Destroy(gameObject);
		}
	}

	void OnTriggerEnter2D(Collider2D other) 
	{
		if(!PickedUp)
		{
			if(other.gameObject.name.Contains("Sna"))
			{
				// Snake collided with pickup
				Game.NotifyPickupCollected(gameObject);
				AudioSource AS = GetComponent<AudioSource>();
				if(AS != null)
				{
					AS.Play();
				}
				ParticleSystem PS = GetComponent<ParticleSystem>();
				if(PS != null)
				{
					PS.Play();
				}
				Destroy(GetComponent<SpriteRenderer>());
				PickedUp = true;
				LifeTime = 0.6f;
			}
			else if(other.gameObject.name.Contains("Obs"))
			{
				// Obstacle collided with pickup
				Game.NotifyPickupDestroyed(gameObject);
				Destroy(gameObject);
			}
		}
	}
}
