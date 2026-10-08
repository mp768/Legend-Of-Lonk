using Godot;

// Hit points for an entity, counted in half-hearts (6 = three hearts).
[GlobalClass]
public partial class Health : Node
{
    [Signal]
    public delegate void ChangedEventHandler(int current, int max);

    [Signal]
    public delegate void DepletedEventHandler();

    [Export] public int Max { get; set; } = 6;

    // After a hit lands, further damage is ignored for this long. 0 disables invincibility frames.
    [Export] public float InvincibilityDuration { get; set; }

    private int current;
    private Timer invincibilityTimer;

    public int Current
    {
        get => current;
        private set
        {
            current = Mathf.Clamp(value, 0, Max);
            EmitSignal(SignalName.Changed, current, Max);
        }
    }

    public bool IsDepleted => Current == 0;

    // Ignores all damage regardless of invincibility frames, e.g. for cheat mode.
    public bool IsImmune { get; set; }

    public bool IsInvincible => IsImmune || !invincibilityTimer.IsStopped();

    public override void _Ready()
    {
        Current = Max;

        invincibilityTimer = new Timer { OneShot = true };
        AddChild(invincibilityTimer);
    }

    // Returns false if the damage was ignored.
    public bool TakeDamage(int amount)
    {
        if (amount <= 0 || IsDepleted || IsInvincible)
        {
            return false;
        }

        Current -= amount;

        if (InvincibilityDuration > 0f)
        {
            invincibilityTimer.Start(InvincibilityDuration);
        }

        if (IsDepleted)
        {
            EmitSignal(SignalName.Depleted);
        }

        return true;
    }

    public void Heal(int amount)
    {
        if (amount > 0 && !IsDepleted)
        {
            Current += amount;
        }
    }

    public void Refill()
    {
        Current = Max;
    }
}
