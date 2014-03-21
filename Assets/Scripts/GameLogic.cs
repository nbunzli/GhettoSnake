/***************************************
Author: Nicku Bunzli - nbunzli@gmail.com
Feel free to steal, modify, distribute, make love to, or do whatever else you want with this code.
***************************************/

using UnityEngine;
using System.Collections;

public class GameLogic : MonoBehaviour 
{
	// Objects in scene
	public GameObject[] Snakes;

	public GameObject ObstaclePrefab;
	public GameObject PickupPrefab;

	public GameObject WallLeft;
	public GameObject WallRight;
	public GameObject WallTop;
	public GameObject WallBottom;

	public GameObject TextGrabSnake;
	public GameObject TextScore;
	public GameObject TextHighScore;
	public GameObject TextGameOver;
	
	public GUISkin Skin;
	
	public GameObject GameOverSound;
	
	// Params to adjust dificulty
	public float InitialObstacleSpeed = 8.0f;	 	// Current Obstacle Speed = InitialObstacleSpeed + (Score * ObstacleSpeedIncrement)
	public float ObstacleSpeedIncrement = 0.3f;
	public float ObstacleMoveTimer = 0.2f;		 	// How long a new obstacle will sit idly before starting to move (so the player has time to react)
	public float ObstacleScale = 3.0f;				// Y scale of the obstacle (x scale is 1)
	public float PickupAttemptFrequency = 0.05f; 	// How often we will attempt to spawn a pickup (seconds)
	public float PickupProbability = 0.15f;		 	// Probability of a single attempt spawning a pickup
	
	enum GameState
	{
		None,
		Pregame,
		Game,
		GameOver,
	};
	GameState CurrentState = GameState.None;

	// Pixel resolution of the screen
	float ScreenWidth;
	float ScreenHeight;
	
	// Radius of each snake piece
	float SnakeRadius;
	
	// Limits for snake position, with snake radius taken into account
	float LimitLeft;
	float LimitRight;
	float LimitTop;
	float LimitBottom;

	// Snake piece currently being dragged by the player, NULL if not currently dragging
	GameObject DraggedSnake = null;
	
	// Offset of the touch or mouse cursor from the center of the dragged snake
	Vector2 DraggedSnakeOffset;

	int Score = 0;

	// Time until next pickup spawn attempt
	float PickupAttemptCountdown = 0.0f;

	GameObject CurrentObstacle = null;
	
	ArrayList Pickups = new ArrayList();
	
	void Start() 
	{
		ScreenWidth = Camera.main.pixelWidth;
		ScreenHeight = Camera.main.pixelHeight;
		
		Layout(TextScore, 0.05f, 0.9f);
		Layout(TextHighScore, 0.05f, 0.83f);
		Layout(WallLeft, 0.0f, 0.5f);
		Layout(WallRight, 1.0f, 0.5f);
		Layout(WallBottom, 0.5f, 0.0f);
		Layout(WallTop, 0.5f, 1.0f);
		
		SnakeRadius = ((CircleCollider2D)Snakes[0].collider2D).radius;

		// Use wall position and snake radius to calculate limits for the snake's location
		LimitLeft = WallLeft.transform.position.x + (WallLeft.transform.localScale.x / 2) + SnakeRadius;
		LimitRight = WallRight.transform.position.x - (WallRight.transform.localScale.x / 2) - SnakeRadius;
		LimitTop = WallTop.transform.position.y - (WallTop.transform.localScale.y / 2) - SnakeRadius;
		LimitBottom = WallBottom.transform.position.y + (WallBottom.transform.localScale.y / 2) + SnakeRadius;

		TextGrabSnake.SetActive(false);
		TextScore.SetActive(false);
		TextHighScore.SetActive(false);
		TextGameOver.SetActive(false);
		
		SetState(GameState.Pregame);
	}
	
	void Update() 
	{
		UpdateSnakeRotation();

		switch(CurrentState)
		{
		case GameState.Pregame:
			UpdatePregame();
			break;
		case GameState.Game:
			UpdateGame();
			break;
		case GameState.GameOver:
			UpdateGameOver();
			break;
		}
	}

	// Updates snake's head rotation so that his mouth is always facing away from the first body piece
	void UpdateSnakeRotation()
	{
		Vector3 HeadLoc = Snakes[0].transform.position;
		Vector3 BodyLoc = Snakes[1].transform.position;

		float YDiff = HeadLoc.y - BodyLoc.y;
		float XDiff = HeadLoc.x - BodyLoc.x;

		Vector3 HeadRot = new Vector3(0,0,0);

		if(XDiff == 0.0f)
		{
			// Handle the 2 cases where tan is undefined
			if(YDiff > 0.0f)
			{
				HeadRot.z = 90;
			}
			else
			{
				HeadRot.z = 270;
			}
		}
		else
		{
			HeadRot.z = Mathf.Rad2Deg * Mathf.Atan(YDiff/XDiff);

			if(XDiff < 0.0f)
			{
				// Atan will only give us -90 to 90, so add 180 if snake is facing left
				HeadRot.z += 180;
			}
		}

		Snakes[0].transform.localRotation = Quaternion.Euler(HeadRot);
	}

	void UpdatePregame()
	{
		if(IsGrabStarting())
		{
			SetDraggedSnake();
		}
		
		if(DraggedSnake != null)
		{
			// User grabbed the snake, so start the game
			SetState(GameState.Game);
		}
	}
	
	void UpdateGame()
	{
		if(IsGrabStarting())
		{
			SetDraggedSnake();
		}
		
		if(DraggedSnake != null)
		{
			if(IsGrabEnding())
			{
				// Disable isKinematic so that the joints will affect the snake again
				DraggedSnake.rigidbody2D.isKinematic = false;
				DraggedSnake = null;
			}
			else
			{
				// Update snake's location
				Vector2 InputPos = Camera.main.ScreenToWorldPoint(GetGrabPos());
				Vector2 NewPos = InputPos + DraggedSnakeOffset;
				NewPos.x = Mathf.Clamp(NewPos.x, LimitLeft, LimitRight);
				NewPos.y = Mathf.Clamp(NewPos.y, LimitBottom, LimitTop);
				DraggedSnake.transform.position = NewPos;
			}
		}

		PickupAttemptCountdown -= Time.deltaTime;
		if(PickupAttemptCountdown <= 0.0f)
		{
			// Attempt to spawn a pickup
			PickupAttemptCountdown = PickupAttemptFrequency;
			if(Random.Range(0.0f, 1.0f) <= PickupProbability)
			{
				SpawnPickup();
			}
		}
	}

	void UpdateGameOver()
	{
		if(!Snakes[0].gameObject.rigidbody2D.IsAwake())
		{
			// Wake the snake's rigid bodies, which have been sleeping since the last update.
			// This get rid of any momentum and the snake will stay in the same position for a split second, and then slowly fall to the ground.
			for(int i = 0; i < Snakes.Length; i++)
			{
				Snakes[i].gameObject.rigidbody2D.WakeUp();
			}
		}
	}

	void OnGUI()
	{
		GUI.skin = Skin;
		if(CurrentState == GameState.GameOver)
		{
			if(GUI.Button(MakeGUIRect(new Vector2(0.5f, 0.8f), new Vector2(0.25f, 0.15f)), "Again"))
			{
				SetState(GameState.Pregame);
			}
		}
	}
	
	Rect MakeGUIRect(Vector2 RelativePos, Vector2 RelativeScale)
	{
		float width = ScreenWidth * RelativeScale.x;
		float height = ScreenHeight * RelativeScale.y;
		float left = (ScreenWidth * RelativePos.x) - (width / 2.0f);
		float top = (ScreenHeight * RelativePos.y) - (height / 2.0f);
		return new Rect(left, top, width, height);
	}

	void SetDraggedSnake()
	{
		DraggedSnake = null;
		Vector2 InputPos = Camera.main.ScreenToWorldPoint(GetGrabPos());
		for(int i = 0; i < Snakes.Length; i++)
		{
			Vector2 SnakePos = Snakes[i].transform.position;
			if(Vector2.Distance(InputPos, SnakePos) < SnakeRadius)
			{
				DraggedSnake = Snakes[i];
				DraggedSnakeOffset = SnakePos - InputPos;
				
				// Setting isKinematic to true will cause the rigidbody to ignore the joints.
				// Necessary because while dragging a snake piece, it's location should be defined only by user input.
				DraggedSnake.rigidbody2D.isKinematic = true;
				return;
			}
		}
	}

	void SpawnPickup()
	{
		GameObject NewPickup = Instantiate(PickupPrefab) as GameObject;
		Pickup P = NewPickup.GetComponent<Pickup>();
		if(P != null)
		{
			P.Game = this;
			P.LifeTime = 2.0f;

			Vector3 Position = new Vector3(0, 0, -1.0f);
			Position.x = Random.Range(LimitLeft, LimitRight);
			Position.y = Random.Range(LimitBottom, LimitTop);
			P.transform.position = Position;

			Pickups.Add(NewPickup);
		}
		else
		{
			Destroy(NewPickup);
			Debug.Log("No pickup spawned because PickupPrefab is missing the Pickup script");
		}
	}

	void SpawnObstacle()
	{
		GameObject NewObstacle = Instantiate (ObstaclePrefab) as GameObject;
		Obstacle O = NewObstacle.GetComponent<Obstacle>();
		if(O != null)
		{
			O.Game = this;
			O.MoveTimer = ObstacleMoveTimer;
	
			Vector3 Position = new Vector3(0, 0, 0);
			Vector3 Scale = new Vector3(1, ObstacleScale, 1);
			float Speed = 0.0f;
			int Side = Random.Range(0, 2);
	
			switch(Side)
			{
			case 0:
				// Left
				Position.x = WallLeft.transform.position.x;
				Speed = InitialObstacleSpeed + (Score * ObstacleSpeedIncrement);
				break;
			case 1:
				// Right
				Position.x = WallRight.transform.position.x;
				Speed = -InitialObstacleSpeed - (Score * ObstacleSpeedIncrement);
				break;
			}
			
			Position.y = Random.Range(LimitBottom, LimitTop);
			O.Speed = Speed;
			O.transform.position = Position;
			O.transform.localScale = Scale;
	
			CurrentObstacle = NewObstacle;
		}
		else
		{
			Destroy(NewObstacle);
			Debug.Log("No obstacle spawned because ObstaclePrefab is missing the Obstacle script");
		}
	}

	void SetState(GameState NewState)
	{
		OnExitState(CurrentState);
		OnEnterState(NewState);
		CurrentState = NewState;
	}

	void OnEnterState(GameState NewState)
	{
		switch(NewState)
		{
		case GameState.Pregame:
			TextGrabSnake.SetActive(true);
			break;
		case GameState.Game:
			Score = 0;
			TextMesh TM = TextScore.GetComponent<TextMesh>();
			if(TM != null)
			{
				TM.text = "Score:" + Score;
			}
			TM = TextHighScore.GetComponent<TextMesh>();
			if(TM != null)
			{
				TM.text = "High Score:" + PlayerPrefs.GetInt("HighScore");
			}
			TextScore.SetActive(true);
			TextHighScore.SetActive(true);
			PickupAttemptCountdown = PickupAttemptFrequency;
			SpawnObstacle();
			break;
		case GameState.GameOver:
			TextGameOver.SetActive(true);
			if(DraggedSnake != null)
			{
				// Disable isKinematic so that the joints will affect the snake again
				DraggedSnake.rigidbody2D.isKinematic = false;
				DraggedSnake = null;
			}
			for(int i = 0; i < Snakes.Length; i++)
			{
				// sleep the rigidbody until the next update.
				// this get rid of any momentum and the snake will stay in the same position for a split second, and then slowly fall to the ground
				Snakes[i].gameObject.rigidbody2D.Sleep();
			}
			for(int i = Pickups.Count - 1; i >= 0; i--)
			{
				Destroy((GameObject)Pickups[i]);
				Pickups.Remove(Pickups[i]);
			}
			AudioSource AS = GameOverSound.GetComponent<AudioSource>();
			if(AS != null)
			{
				AS.Play();
			}
			break;
		}
	}

	void OnExitState(GameState OldState)
	{
		switch(OldState)
		{
		case GameState.Pregame:
			TextGrabSnake.SetActive(false);
			break;
		case GameState.Game:
			break;
		case GameState.GameOver:
			TextGameOver.SetActive(false);
			TextScore.SetActive(false);
			TextHighScore.SetActive(false);
			if(CurrentObstacle != null)
			{
				Destroy(CurrentObstacle);
			}
			break;
		}
	}
	
	// Called in Obstacle.cs
	public void NotifyGameOver()
	{
		if(CurrentState == GameState.Game)
		{
			if(CurrentObstacle != null)
			{
				Obstacle O = CurrentObstacle.GetComponent<Obstacle>();
				if(O != null)
				{
					O.Speed = 0.0f;
				}
			}
			if(Score > PlayerPrefs.GetInt("HighScore"))
			{
				PlayerPrefs.SetInt("HighScore", Score);
				TextMesh TM = TextHighScore.GetComponent<TextMesh>();
				if(TM != null)
				{
					TM.text = "High Score:" + Score;
				}
			}
			SetState(GameState.GameOver);
		}
	}

	// Called in Obstacle.cs
	public void NotifyObstaclePassed(GameObject Obstacle)
	{
		if(CurrentState == GameState.Game)
		{
			Score++;
			TextMesh TM = TextScore.GetComponent<TextMesh>();
			if(TM != null)
			{
				TM.text = "Score:" + Score;
			}
			SpawnObstacle();
		}
	}

	// Called in Pickup.cs
	public void NotifyPickupCollected(GameObject Pickup)
	{
		Score++;
		TextMesh TM = TextScore.GetComponent<TextMesh>();
		if(TM != null)
		{
			TM.text = "Score:" + Score;
		}
		Pickups.Remove(Pickup);
	}

	// Called in Pickup.cs
	public void NotifyPickupDestroyed(GameObject Pickup)
	{
		Pickups.Remove(Pickup);
	}
	
	// Sets an opject's position, relative to the camera position.
	// Passing in (0,0) for x and y will put the object in the bottom left corner of the screen, (0.5,0.5) is the middle, (1,1) is top right, etc.
	// Mostly useful in the editor, when changing the size of the play window.
	void Layout(GameObject Obj, float RelativeX, float RelativeY)
	{
		float z = Obj.transform.position.z;
		Vector3 NewPos = Camera.main.ScreenToWorldPoint(new Vector3(ScreenWidth * RelativeX, ScreenHeight * RelativeY, Camera.main.nearClipPlane));
		NewPos.z = z;
		Obj.transform.position = NewPos;
	}	
	
	public bool IsGrabStarting()
	{
		bool Result = false;
#if UNITY_EDITOR || UNITY_STANDALONE
		Result = Input.GetMouseButtonDown(0);
#else
		// Ignore multi-touch input
		Result = (Input.touches.Length == 1) &&	(Input.touches[0].phase == TouchPhase.Began);
#endif
		return Result;
	}

	public bool IsGrabEnding()
	{
		bool Result = false;
#if UNITY_EDITOR || UNITY_STANDALONE
		Result = Input.GetMouseButtonUp(0);
#else
		// Ignore multi-touch input
		Result = (Input.touches.Length != 1) || (Input.touches[0].phase == TouchPhase.Ended);
#endif
		return Result;
	}

	public Vector3 GetGrabPos()
	{
		Vector3 Result = new Vector3(0,0,0);
#if UNITY_EDITOR || UNITY_STANDALONE
		Result = Input.mousePosition;
#else
		if(Input.touches.Length == 1)
		{
			Result = Input.touches[0].position;
		}
#endif
		return Result;
	}
}
