/// Enemy can reduce incoming player melee damage by returning a multiplier below 1.
/// Called exactly once per player hit from PlayerMovement.CheckPendingMeleeHit.
/// Implementations may roll probability inside the getter.
public interface IBlockable
{
    // 1.0 = full damage, 0.0 = no damage.
    float BlockDamageMultiplier { get; }
}
