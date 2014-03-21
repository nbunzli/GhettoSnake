/***************************************
Author: Nicku Bunzli - nbunzli@gmail.com
Feel free to steal, modify, distribute, make love to, or do whatever else you want with this code.
***************************************/

using UnityEngine;
using System.Collections;

public class Obstacle : MonoBehaviour 
{
	public float Speed;			// X axis speed
	public float MoveTimer;		// Idle time before obstacle starts moving
	public GameLogic Game;

	void Update() 
	{
		if(MoveTimer > 0.0f)
		{
			// Not yet moving
			MoveTimer -= Time.deltaTime;
		}
		else
		{
			// Update movement
			Vector3 Pos = transform.position;
			Pos.x += Speed * Time.deltaTime;
			transform.position = Pos;
		}
	}

	void OnBecameInvisible()
	{
		Game.NotifyObstaclePassed(gameObject);
		Destroy(gameObject);
	}

	void OnTriggerEnter2D(Collider2D other) 
	{
		// Hopefully the snake pieces are the only gameobjects containing "sna"
		if(MoveTimer <= 0.0f && other.gameObject.name.Contains("Sna"))
		{
			Game.NotifyGameOver();
		}
	}
}
