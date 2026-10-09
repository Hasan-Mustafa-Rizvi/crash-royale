namespace CrashRoyale.Battle
{
    public enum Team
    {
        Blue = 0,
        Red = 1
    }

    public enum Difficulty
    {
        Easy = 0,
        Normal = 1,
        Hard = 2
    }

    public static class TeamExtensions
    {
        public static Team Opponent(this Team team) => team == Team.Blue ? Team.Red : Team.Blue;

        /// <summary>World-space Y direction this team attacks toward. Blue starts at the bottom.</summary>
        public static float Forward(this Team team) => team == Team.Blue ? 1f : -1f;
    }
}
