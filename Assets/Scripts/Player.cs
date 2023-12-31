﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/// <summary>
/// Core class for holding Player/AI data. References to units, bases, points, etc...
/// </summary>
public class Player : MonoBehaviour {

	const float DNA_UPDATE_INTERVAL = 1f;

	//Time interval range for when Ai is pushing an attack
	const float ATTACK_INTERVAL_MIN = 0.25f;
	const float ATTACK_INTERVAL_MAX = 1.5f;
	const float ATTACK_VARIANCE = 0.125f;

	//Time interval range for when Ai is defending
	const float DEFEND_INTERVAL_MIN = 0.25f;
	const float DEFEND_INTERVAL_MAX = 1f;
	const float DEFEND_VARIANCE = 0.125f;

	//These ranges are the upper/lower bounds of the interval for AI to spawn units 'normally'
	const float NORMAL_INTERVAL_MIN = 0.5f;
	const float NORMAL_INTERVAL_MAX = 2.5f;
	const float NORMAL_VARIANCE = 0.25f;


	const float EVO_INTERVAL_MIN = 60;
	const float EVO_INTERVAL_MAX = 300;
	const float EVO_VARIANCE = 30;


	public Base Base;

	[Space]

	public int DNA; // Total amount of DNA the player has gained
	public int DNAPerMinute = 600;

	[Space]

	public Evolution Evolutions;
	public UnitModifiers WorkerModifiers = new UnitModifiers();
	public UnitModifiers SoldierModifiers = new UnitModifiers();
	public UnitModifiers SpitterModifiers = new UnitModifiers();
	public UnitModifiers DefenderModifiers = new UnitModifiers();

	[Space]

	public int PlayerID;

	[Space]

	public Color Color;

	[Space]

	[SerializeField] private int evolutionWindowIncrement;
	public GameObject PlayerUnitObjects;



	//----------------------------------------------------------------------------------------------------------------------------------<


	bool isAI; public bool IsAI => isAI;
	Path selectedPath = Path.Surface; public Path SelectedPath => selectedPath;

	float remainingDNA = 0;
	float counterDNA = 0;


	float aiTickCounter = 0;
	float aiTickInterval = 0.1f; //Might make into const (only if this will end up being independant of the difficulty range)

	float aiEvoCounter = 0;
	float aiEvoInterval = EVO_INTERVAL_MAX;

	float aiSpawnCounter = 0; //this counter is shared between attack, defend & normal spawn states
	float aiNormalInterval = NORMAL_INTERVAL_MAX;
	float aiAttackInterval = ATTACK_INTERVAL_MAX;
	float aiDefendInterval = DEFEND_INTERVAL_MAX;

	private float nextEvolutionWindow;


	//----------------------------------------------------------------------------------------------------------------------------------<


	private void Awake() {
		Evolutions = new Evolution(this);

		aiNormalInterval = GetInterval(NORMAL_INTERVAL_MIN, NORMAL_INTERVAL_MAX, NORMAL_VARIANCE);
		aiAttackInterval = GetInterval(ATTACK_INTERVAL_MIN, ATTACK_INTERVAL_MAX, ATTACK_VARIANCE);
		aiDefendInterval = GetInterval(DEFEND_INTERVAL_MIN, DEFEND_INTERVAL_MAX, DEFEND_VARIANCE);
		aiEvoInterval    = GetInterval(EVO_INTERVAL_MIN, EVO_INTERVAL_MAX, EVO_VARIANCE);
	}


	private void Start() {
		Game.Current.UI.UpdateDNA(DNA, PlayerID);
		Game.Current.UI.UpdateEvolutionCost(Evolutions.GetEvolutionCost(), PlayerID);
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	private void OnDestroy() {
		Evolutions = null;
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	void Update() {
		if (Game.Current.Freeze) return;

		GatherDNA();

		if (isAI) UpdateAI();
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	void GatherDNA() {
		float rate = DNAPerMinute / 60;
		remainingDNA += Time.deltaTime * rate;

		counterDNA += Time.deltaTime;
		if(counterDNA >= DNA_UPDATE_INTERVAL) {
			counterDNA = 0;

			int whole = (int)remainingDNA;
			remainingDNA = remainingDNA % 1;

			ChangeDNA(whole);
		}
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	public void ChangeDNA(int amount) {
		DNA += amount;

		Game.Current.UI.UpdateDNA(DNA, PlayerID);
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	public UnitModifiers GetModifierReference(UnitType type) {
		switch(type) {
			case UnitType.Worker: {
				return WorkerModifiers;
			}
			case UnitType.Soldier: {
				return SoldierModifiers;
			}
			case UnitType.Spitter: {
				return SpitterModifiers;
			}
			case UnitType.Defender: {
				return DefenderModifiers;
			}
		}
		return null;
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	public void SelectPath(Path path) {
		selectedPath = path;
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	public void SetMode(PlayerMode mode) {
		isAI = mode == PlayerMode.AI;

		Game.Current.UI.SetPlayerControls(PlayerID, !isAI);

		if(isAI) {
			if(PlayerID == 0) {
				Game.Current.UI.Player1ParticleSystemSurface.gameObject.SetActive(false);
				Game.Current.UI.Player1ParticleSystemTunnel.gameObject.SetActive(false);
			} else {
				Game.Current.UI.Player2ParticleSystemSurface.gameObject.SetActive(false);
				Game.Current.UI.Player2ParticleSystemTunnel.gameObject.SetActive(false);
			}
		}
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	#region AI


	//----------------------------------------------------------------------------------------------------------------------------------<


	void UpdateAI() 
	{
		//Options.GetLinearDifficulty will return a value of [0, 1]. You can use this as 't' for a Lerp func to get AI frame interval with upper/lower bounds.
		aiTickCounter += Time.deltaTime;

		if(aiTickCounter > aiTickInterval) {
			aiTickCounter = 0;

			float top = CheckLaneDominance(Path.Surface);
			float bottom = CheckLaneDominance(Path.Tunnels);

			CheckPriorityStates(aiTickInterval, top, bottom);
		}
	}
	

    //----------------------------------------------------------------------------------------------------------------------------------<


    /// <summary>
    /// 
    /// </summary>
    float CheckLaneDominance(Path path)
    {
		Base homeBase = Base;
		Base enemyBase = (PlayerID == 0 ? Game.Current.Player2 : Game.Current.Player1).Base;

		UnitController allyUnit = Base.GetUnit(0, path);
		UnitController enemyUnit = enemyBase.GetUnit(0, path);

		float allyP = 0;
		float enemyP = 0;

		if (allyUnit != null) allyP = Mathf.Clamp01(allyUnit.transform.localPosition.x / enemyBase.transform.localPosition.x);
		if (enemyUnit != null) enemyP = Mathf.Clamp01(enemyUnit.transform.localPosition.x / homeBase.transform.localPosition.x);

		return allyP - enemyP;
	}
	

    //----------------------------------------------------------------------------------------------------------------------------------<


    /// <summary>
    /// Checks if the AI needs to defend or push a hard attack.
    /// </summary>
    /// <param name="timeDelta">The change in time, between each AI frame.</param>
    /// <param name="a">Surface Lane Dominance [-1, 1]</param>
    /// <param name="b">Tunnels Lane Dominance [-1, 1]</param>
    void CheckPriorityStates(float timeDelta, float a, float b) {
		const float RUSH_ATTACK_THRESHOLD = 0.5f;
		const float DEFEND_THRESHOLD = -0.5f;

		if(a < DEFEND_THRESHOLD || b < DEFEND_THRESHOLD) {

			aiSpawnCounter += timeDelta;
			if(aiSpawnCounter > aiDefendInterval) {
				aiSpawnCounter = 0;
				aiDefendInterval = GetInterval(DEFEND_INTERVAL_MIN, DEFEND_INTERVAL_MAX, DEFEND_VARIANCE);

				Path path = a < b ? Path.Surface : Path.Tunnels;
				UnitType type = PickUnitType(0.5f, 0.25f, 0.25f);

				Base.SpawnUnit(type, path);
			}
			
			return;
		}

		if (a > RUSH_ATTACK_THRESHOLD || b > RUSH_ATTACK_THRESHOLD) {

			aiSpawnCounter += timeDelta;
			if(aiSpawnCounter > aiAttackInterval) {
				aiSpawnCounter = 0;
				aiAttackInterval = GetInterval(ATTACK_INTERVAL_MIN, ATTACK_INTERVAL_MAX, ATTACK_VARIANCE);

				Path path = a > b ? Path.Surface : Path.Tunnels;
				UnitType type = PickUnitType(0.65f, 0.3f, 0.05f);

				Base.SpawnUnit(type, path);
			}

			return;
		}

		CheckEvolutionStates(timeDelta);
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	void CheckEvolutionStates(float timeDelta) {
		//Debug.Log("ENTERED");

		aiEvoCounter += timeDelta;
        if (aiEvoCounter > aiEvoInterval) {
            if (DNA > Evolutions.GetEvolutionCost()) {
                Evolutions.Evolve(Random.Range(0, 2));
				aiEvoCounter = 0;
				aiEvoInterval = GetInterval(EVO_INTERVAL_MIN, EVO_INTERVAL_MAX, EVO_VARIANCE);
			}
        }
        else
        {
			//Spawn units normally
			aiSpawnCounter += timeDelta;
			if (aiSpawnCounter > aiNormalInterval) {
				aiSpawnCounter = 0;
				aiNormalInterval = GetInterval(NORMAL_INTERVAL_MIN, NORMAL_INTERVAL_MAX, NORMAL_VARIANCE);

				Base.SpawnUnit(PickUnitType(0.5f, 0.25f, 0.25f), (Path)Random.Range(0, 2));
			}
        }
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	static UnitType PickUnitType(float soldier, float spitter, float defender) {
		soldier = Mathf.Abs(soldier);
		spitter = Mathf.Abs(spitter);
		defender = Mathf.Abs(defender);

		float m = soldier + spitter + defender;

		float t = Random.value * m;

		if (t > soldier + spitter) return UnitType.Defender;
		else if (t > soldier) return UnitType.Spitter;
		else return UnitType.Soldier;
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	float GetInterval(float lower, float upper, float variance) {
		return Mathf.Lerp(lower, upper, 1 - Options.GetLinearDifficulty(PlayerID)) + Random.Range(-variance, variance);
	}


	//----------------------------------------------------------------------------------------------------------------------------------<


	#endregion

}

public enum PlayerMode {
	Real = 0,
	AI = 1,
}