namespace CrashRoyale.Battle
{
    /// <summary>How the next battle should be set up. The menu fills this in before loading the Battle scene.</summary>
    public static class BattleSettings
    {
        public static Difficulty Difficulty = Difficulty.Normal;

        /// <summary>Lets an AI play the blue side too: used by the automated tests and the attract mode.</summary>
        public static bool BlueIsAI;
        public static Difficulty BlueDifficulty = Difficulty.Normal;
        public static string BlueNameOverride;

        /// <summary>Turn the red AI off (rules tests drive the battle by hand).</summary>
        public static bool RedIsAI = true;

        public static float TimeScale = 1f;
        public static int Seed;
        public static bool SkipIntro;
        public static bool ApplyRewards = true;
        public static string[] BlueDeckOverride;
        public static string[] RedDeckOverride;

        public static void ResetDebugOptions()
        {
            BlueIsAI = false;
            BlueDifficulty = Difficulty.Normal;
            BlueNameOverride = null;
            RedIsAI = true;
            TimeScale = 1f;
            Seed = 0;
            SkipIntro = false;
            ApplyRewards = true;
            BlueDeckOverride = null;
            RedDeckOverride = null;
        }
    }
}
