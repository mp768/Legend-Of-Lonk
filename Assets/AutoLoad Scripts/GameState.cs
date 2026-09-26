using Godot;
using System;

public partial class GameState : Node
{	
	enum UILabels
	{
		HEALTH = 0,
		RUPEES = 1,
		KEYS = 2,
		WEAPONS = 3,
	}

	// Game state values to keep constant throughout the game
	// -1 sinifies infinity for our use case
	int _keys = 0;
	int _rupees = 0;
	int weapon = 0; // Idk what to do about this field, saving it as ints first (maybe an enum)

	/* KEYS FUNCTIONS*/

	// Gamestate function to invoke when trying to use keys to open doors or something
	// returns true / false to know if it worked
	public bool use_key()
	{
		if (_keys != 0)
		{
			// Logic: if -1, keep it at -1 to signify inf keys
			// else, subtract use_keys from number of keys
			// this avoids the problem of _keys becomming infinite (keys = 0 - use_keys)
			// because we don't run this when _keys = 0 
			_keys = Mathf.Max(-1, _keys - 1);
			GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _keys, (int)UILabels.KEYS);
			return true;
		} 
		return false;
	}

	public void gain_key()
	{
		// avoids incrementing _keys on -1 (inf)
		if (_keys >= 0)
		{
			_keys ++;
			GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _keys, (int)UILabels.KEYS);
		}
	}

	/* RUPEES FUNCTION */

	public bool use_rupees(int num_rupees)
	{
		if (_rupees == -1 || _rupees - num_rupees >= 0)
		{
			// Logic: if -1, keep it at -1 to signify inf keys
			// else, subtract use_keys from number of keys
			// this avoids the problem of _rupees becomming infinite (keys = 0 - num_rupees)
			// because we don't run this when _rupees = 0 
			_rupees = Mathf.Max(-1, _rupees - num_rupees);
			GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _rupees, (int)UILabels.RUPEES);
			return true;
		} 
		return false;
	}

	public void gain_rupee(int num_rupees)
	{
		// avoids incrementing _keys on -1 (inf)
		if (_rupees >= 0)
		{
			_rupees += num_rupees;
			GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _rupees, (int)UILabels.RUPEES);
		}
	}

	/* CHEATCODE FUNCTIONS AND OTHER UTILS */

	// function to give the player infinite resources
	public void setCheatMode(bool set)
	{
		if (set)
		{
			_keys = -1;
			_rupees = -1;	
		} else
		{
			_keys = 9999;
			_rupees = 9999;
		}
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.SetCheatMode, set);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _keys, (int)UILabels.KEYS);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _rupees, (int)UILabels.RUPEES);
	}

	// function to reset game state to all 0
	public void resetGameState()
	{
		_keys = 0;
		_rupees = 0;
		
		// change this once weapons is figured out
		weapon = 0;

		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _keys, (int)UILabels.KEYS);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, _rupees, (int)UILabels.RUPEES);
		GameSignals.Instance.EmitSignal(GameSignals.SignalName.UpdateUI, weapon, (int)UILabels.WEAPONS);
	}

	// Called when the node enters the scene tree for the first time.
	public override void _Ready()
	{
	}

	// Called every frame. 'delta' is the elapsed time since the previous frame.
	public override void _Process(double delta)
	{
	}
}
