using Godot;
using System;

public partial class GameState : Node
{	
	// Game state values to keep constant throughout the game
	// -1 sinifies infinity for our use case
	int _keys = 0;
	int _rupees = 0;
	int weapon = 0; // Idk what to do about this field, saving it as ints first


	// Gamestate function to invoke when trying to use keys to open doors or something
	// returns true / false to know if it worked
	public bool use_key(int use_keys)
	{
		if (_keys == -1 || _keys - use_keys >= 0)
		{
			// Logic: if -1, keep it at -1 to signify inf keys
			// else, subtract use_keys from number of keys
			// this avoids the problem of _keys becomming infinite (keys = 0 - use_keys)
			// because we don't run this when _keys = 0 
			_keys = Mathf.Max(-1, _keys - use_keys);
			return true;
		} 
		return false;
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
