namespace HowToVibeCoder;

internal readonly record struct CoopBalance(float EnemyHpScale,float BossHpScale,float BossCadenceScale,
    float SpawnCapScale,int QuarantineUnlockTarget,float HealCooldownScale)
{
    internal static CoopBalance For(int count) => Math.Clamp(count,1,3) switch
    {
        2 => new(1.35f,1.55f,1.12f,1.45f,4,.85f),
        3 => new(1.70f,2.10f,1.22f,1.85f,5,.72f),
        _ => new(1,1,1,1,3,1)
    };
    internal int Cap(int solo) => (int)MathF.Round(solo*SpawnCapScale);
}
