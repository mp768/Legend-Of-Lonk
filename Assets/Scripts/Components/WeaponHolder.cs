using Godot;
using System;
using System.Collections.Generic;

// The alternate weapons an entity can use and which one is selected. Firing checks the weapon's limit on
// live projectiles, asks the entity to pay for the shot, then spawns it.
[GlobalClass]
public partial class WeaponHolder : Node
{
    [Export] public Weapon[] Weapons { get; set; } = [];

    // Starts as the first weapon. The player character's selection is driven by GameState instead.
    public Weapon Selected { get; set; }

    // Whether any projectile fired from this holder is still alive.
    public bool HasActiveProjectiles => totalActive > 0;

    private readonly Dictionary<Weapon, int> activeCounts = [];
    private int totalActive;

    public override void _Ready()
    {
        Selected = Weapons.Length > 0 ? Weapons[0] : null;
    }

    // Returns false, and spends nothing, if the selected weapon can't fire right now.
    public bool TryFire(Entity shooter)
    {
        Weapon weapon = Selected;
        if (weapon?.Scene == null)
        {
            return false;
        }

        int shotCount = Mathf.Max(1, weapon.SpreadCount);
        if (weapon.MaxActive > 0 && ActiveCount(weapon) + shotCount > weapon.MaxActive)
        {
            return false;
        }

        if (!shooter.TrySpendAmmo(weapon))
        {
            return false;
        }

        Vector2 aim = AimDirection(weapon, shooter);
        for (int i = 0; i < shotCount; i++)
        {
            float offset = (i - (shotCount - 1) / 2f) * Mathf.DegToRad(weapon.SpreadAngle);
            Projectile projectile = Projectile.Spawn(weapon.Scene, shooter, aim.Rotated(offset), weapon.SpawnDistance);
            if (projectile != null)
            {
                Track(weapon, projectile);
            }
        }

        return true;
    }

    public int ActiveCount(Weapon weapon) => activeCounts.GetValueOrDefault(weapon);

    // Selects the weapon of the given kind, or nothing if this holder doesn't have one.
    public void Select(AltWeapon? kind)
    {
        Selected = kind.HasValue ? Array.Find(Weapons, w => w.Kind == kind.Value) : null;
    }

    // Moves the selection to the next weapon that `isAvailable` accepts, wrapping around.
    public void CycleNext(Func<Weapon, bool> isAvailable)
    {
        if (Weapons.Length == 0)
        {
            return;
        }

        int start = Array.IndexOf(Weapons, Selected);
        for (int step = 1; step <= Weapons.Length; step++)
        {
            Weapon candidate = Weapons[(start + step + Weapons.Length) % Weapons.Length];
            if (isAvailable(candidate))
            {
                Selected = candidate;
                return;
            }
        }
    }

    private static Vector2 AimDirection(Weapon weapon, Entity shooter)
    {
        if (weapon.AimAtTarget && shooter.GetTree().GetFirstNodeInGroup(Constants.PLAYER_GROUP) is Node2D target)
        {
            Vector2 toTarget = target.GlobalPosition - shooter.GlobalPosition;
            if (toTarget != Vector2.Zero)
            {
                return toTarget.Normalized();
            }
        }

        return shooter.FacingDirection;
    }

    private void Track(Weapon weapon, Projectile projectile)
    {
        activeCounts[weapon] = ActiveCount(weapon) + 1;
        totalActive++;

        projectile.TreeExiting += () =>
        {
            // The holder may be gone already, e.g. a Goriya killed while its boomerang is in flight.
            if (!IsInstanceValid(this))
            {
                return;
            }

            activeCounts[weapon] = Mathf.Max(0, ActiveCount(weapon) - 1);
            totalActive = Mathf.Max(0, totalActive - 1);
        };
    }
}
