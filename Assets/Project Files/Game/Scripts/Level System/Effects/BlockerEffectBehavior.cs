using WaterFlow.Game;
using UnityEngine;

public class BlockerEffectBehavior : BlockEffectBehavior<BlockedBlockEffectData>
{
    [SerializeField] Material blockedMaterial;
    [SerializeField] Material blockedGlassMaterial;

    // The block's water renderer shows the blocked material and is owned by this effect. It must stay
    // hidden whenever the blocker itself is hidden (inside a Container and/or under Ice) and only show once
    // no source hides the blocker. Reporting this makes the map-spawn water refresh keep it off while the
    // block is still concealed, instead of force-enabling it.
    public override bool HidesBlockWater => IsActive && hiddenVisualSources.Count > 0;

    public override void OnCreated(LevelBlockBehavior blockBehavior)
    {
        if (!ValidateDataOrDisable())
            return;

        blockBehavior.BlockCollectable = false;
        
        blockBehavior.MeshRenderer.material = blockedMaterial;
        blockBehavior.MeshGlass.material = blockedGlassMaterial;
        blockBehavior.ApplyBlockedWaterMaterial(blockedMaterial);

        foreach (BlockEffectBehavior effect in linkedBlock.Effects)
        {
            if (effect == this) continue;
            effect.OnToggleVisual(false);
        }
    }

    protected override void ApplyVisualState(bool visible)
    {
        base.ApplyVisualState(visible);
        // Drive the block water from the resolved visibility: off while hidden by Container/Ice, on once the
        // blocker is the front-most visible layer. This keeps water off after a Container clears if Ice is
        // still up, and turns it on only when Ice finally releases.
        linkedBlock.SetVisibleBlockWater(visible);
    }

    public override BlockGateState AllowGateEntered()
    {
        return BlockGateState.Blocked;
    }

    public override void OnDisabled(LevelBlockBehavior blockBehavior, DisableSource disableSource)
    {
        foreach (BlockEffectBehavior effect in blockBehavior.Effects)
        {
            if(effect != this && effect.IsActive) effect.DisableEffect();
        }
        if (blockBehavior)
        {
            DestroyImmediate(blockBehavior.gameObject);
        }
    }
}
