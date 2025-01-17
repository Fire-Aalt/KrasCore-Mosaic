using UnityEngine;

namespace KrasCore.Mosaic
{
    public enum RandomBehavior
    {
        [Tooltip("Random is unique to Sprites and Entities of this rule")]
        UniqueRandom,
        
        [Tooltip("Random is shared to Sprites and Entities of this rule. The effect only works if the amount of Sprites and Entities is equal and their weights are mirrored")]
        SharedRandom
    }
}