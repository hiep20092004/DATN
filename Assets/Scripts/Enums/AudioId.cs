namespace WaterFlow.Enums
{
    public enum AudioId : ushort
    {
        None = 0,

        // UI
        ButtonClick = 1,
        Items_Collected = 2,
        PopUp_NoAdsJustFun = 3,
        PopUp_StarterBundle = 4,
        PopUp_VideoBar = 5,
        
        // Block
        Block_Pick = 6,
        Block_Put = 7,
        Block_Clear = 8,

        Block_Fill_water_Short = 9,
        Block_Fill_water_Medium = 10,
        Block_Fill_water_Long = 11,
        Block_Fill_water_SuperLong = 12,

        // Game State
        Win = 13,
        Lose = 14,
        Win_Welldone = 15,
        Warning_Hit = 16,
        Time_Warning = 76,

        // Booster
        Booster_Denied = 17,
        Booster_Expand = 18,
        Booster_Freeze = 19,
        Booster_Hammer1 = 20,
        Booster_Hammer2 = 21,
        Booster_PreHammer = 22,
        Booster_Gun = 23,
        Booster_Unlock = 24,
        Booster_Received = 25,
        
        // Obstacle
        Obstacle_Bomb_in = 26,
        Obstacle_Bomb_out = 27,
        Obstacle_Bomb_Explosion = 28,

        Obstacle_Ice_break_01 = 29,
        Obstacle_Ice_break_02 = 30,
        Obstacle_Ice_break_03 = 31,

        Obstacle_TieBlock_01 = 32,
        Obstacle_TieBlock_02 = 33,
        Obstacle_TieBlock_03 = 34,

        Obstacle_Generator = 35,
        Obstacle_Generator_Break = 36,
        
        Obstacle_Locked_exit = 37,
        Obstacle_LocknKey = 38,
        Obstacle_LocknKey_Unlock = 39,

        Pre_Booster_MagicWand = 41,
        Pre_Booster_Time = 42,

        Obstacle_Grinder_break_01 = 53,
        Obstacle_Grinder_break_02 = 54,
        Obstacle_Grinder_break_03 = 55,
        Obstacle_Grinder_destroy = 56,
        
        Click_obs_ice = 44,
        Click_obs_locked = 46,

        // Pass / daily / coins
        Coin_Received = 47,
        DailyBonus_Open_box = 48,
        Pass_Progress_count = 49,
        Pass_Progress_full = 50,
        Pass_Stage_gate_close = 51,
        Pass_Stage_gate_open = 52,

        Journey_Complete = 60,
        
        Obstacle_Box_01 = 70,
        Obstacle_Box_02 = 71,
        Obstacle_Box_03 = 72,
        
        Obstacle_Lift = 73,
        Click_obs_box = 74,
        Obstacle_TimeCapsule = 75,
        
        // UI
        Frame_Reward_scene = 120,
        WinStreak_Chest_Open = 121,
        WinStreak_Chest_Appear = 122,
        WinStreak_Item_Dissapear = 123,

        // Quest Streak progress
        Progress_Completed_stage = 124,
        Progress_Multi_hit = 125,
        Progress_Multi_nextstage = 126,

        // Card Collection
        Card_collection_Appear_open = 127,
        Card_Appear_effect = 128,
        Card_Appear_fly_01 = 129,
        Card_Appear_fly_02 = 130,
        Card_Appear_fly_03 = 131,
        Card_Flip_new = 132,
        Card_Flip_normal = 133,
        Card_Dissapear_normal = 134,
        Star_Card_collect = 135,
        Collection_Completed_Single_album = 136,
        Collection_Completed_Full_album = 137,
        Chest_Collection_Appear = 138,
        Chest_Collection_Open = 139,

        // Super League
        BoardLeague_Level_up = 140,
        League_Completed_stage = 141,
        League_Door_open = 142,
        League_Door_zoom_in = 143,
        League_Opening_music = 144,
        League_Completed_rank_effect = 145,
        
        // ===== BGM =====
        BGM_Home_Funny = 200,
        BGM_Ingame_Funny = 201,
        BGM_Ingame_Universal_HardLevel = 202,
        BGM_Ingame_Universal_SuperHardLevel = 203,
        
    }
}
