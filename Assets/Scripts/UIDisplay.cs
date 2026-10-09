using Godot;
using System;

public partial class UIDisplay : ColorRect
{
	// Used to look up the icon for the selected alternate weapon.
	[Export] private Weapon[] weapons = [];

	private Label health;
	private Label rupees;
	private Label keys;
	private Label bombs;
	private TextureRect weaponIcon;

	public override void _Ready()
	{
		health = GetNode<Label>("%Health");
		rupees = GetNode<Label>("%Rupees");
		keys = GetNode<Label>("%Keys");
		bombs = GetNode<Label>("%Bombs");
		weaponIcon = GetNode<TextureRect>("%Weapon");

		GameSignals.Instance.PlayerHealthChanged += OnPlayerHealthChanged;
		GameSignals.Instance.RupeesChanged += OnRupeesChanged;
		GameSignals.Instance.KeysChanged += OnKeysChanged;
		GameSignals.Instance.BombsChanged += OnBombsChanged;
		GameSignals.Instance.SelectedWeaponChanged += OnSelectedWeaponChanged;

		// GameState survives scene reloads, so read its current values instead of waiting for a change.
		// Health comes from the player when he spawns.
		OnRupeesChanged(GameState.Instance.Rupees);
		OnKeysChanged(GameState.Instance.Keys);
		OnBombsChanged(GameState.Instance.Bombs);
		AltWeapon? selected = GameState.Instance.SelectedWeapon;
		OnSelectedWeaponChanged(selected.HasValue ? (int)selected.Value : -1);
	}

	public override void _ExitTree()
	{
		GameSignals.Instance.PlayerHealthChanged -= OnPlayerHealthChanged;
		GameSignals.Instance.RupeesChanged -= OnRupeesChanged;
		GameSignals.Instance.KeysChanged -= OnKeysChanged;
		GameSignals.Instance.BombsChanged -= OnBombsChanged;
		GameSignals.Instance.SelectedWeaponChanged -= OnSelectedWeaponChanged;
	}

	// Health is in half-hearts, shown as hearts.
	private void OnPlayerHealthChanged(int current, int max)
	{
		health.Text = $"Hearts: {current / 2f:0.#}/{max / 2f:0.#}";
	}

	private void OnRupeesChanged(int value) => ShowStat(rupees, value);

	private void OnKeysChanged(int value) => ShowStat(keys, value);

	private void OnBombsChanged(int value) => ShowStat(bombs, value);

	private void OnSelectedWeaponChanged(int kind)
	{
		Weapon weapon = kind < 0 ? null : Array.Find(weapons, w => w != null && (int)w.Kind == kind);
		weaponIcon.Texture = weapon?.Icon;
		weaponIcon.TooltipText = weapon?.DisplayName ?? "";
	}

	private static void ShowStat(Label label, int value)
	{
		label.Text = $"{label.Name}: {value}";
	}
}
