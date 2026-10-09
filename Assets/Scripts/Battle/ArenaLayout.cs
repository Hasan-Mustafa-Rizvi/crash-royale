using System.Collections.Generic;
using UnityEngine;

namespace CrashRoyale.Battle
{
    /// <summary>
    /// Geometry of the arena: the field, the river (the book trough in the library arena), the two bridges,
    /// tower spots and where each side is allowed to deploy. Also does the lane routing for troops.
    /// All values are in world units relative to this transform, which sits on the center of the river.
    /// </summary>
    public class ArenaLayout : MonoBehaviour
    {
        [Header("Field")]
        public float halfWidth = 4.55f;
        public float halfHeight = 6.45f;

        [Header("River and bridges")]
        public float riverHalfThickness = 0.5f;
        public float laneX = 2.85f;
        public float bridgeHalfWidth = 0.48f;

        [Header("Towers")]
        public float princessY = 4f;
        public float kingY = 5.46f;

        [Header("Deployment")]
        [Tooltip("How far past the river a side may deploy once it has destroyed the enemy tower in that lane.")]
        public float pocketDepth = 2.3f;
        public float deployEdgeMargin = 0.25f;

        private const float BridgeApproach = 0.4f;
        private static readonly List<Tower> towerBuffer = new List<Tower>(6);

        public static ArenaLayout Instance { get; private set; }

        public Vector2 Center => transform.position;
        public float RiverY => transform.position.y;

        private void Awake() => Instance = this;

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public float BridgeX(int bridge) => Center.x + (bridge == 0 ? -laneX : laneX);

        public Vector2 TowerSpot(Team team, bool king, int lane)
        {
            float y = (king ? kingY : princessY) * -team.Forward();
            float x = king ? 0f : (lane == 0 ? -laneX : laneX);
            return Center + new Vector2(x, y);
        }

        public bool IsOnSide(Team team, Vector2 p) => team == Team.Blue ? p.y < RiverY : p.y > RiverY;

        public int LaneOf(Vector2 p) => p.x < Center.x ? 0 : 1;

        public bool InField(Vector2 p, float margin = 0f)
        {
            Vector2 d = p - Center;
            return Mathf.Abs(d.x) <= halfWidth - margin && Mathf.Abs(d.y) <= halfHeight - margin;
        }

        public bool InRiverBand(Vector2 p, float extra = 0f) => Mathf.Abs(p.y - RiverY) < riverHalfThickness + extra;

        /// <summary>A side gets a pocket in a lane once the enemy princess tower in that lane is gone.</summary>
        public bool PocketOpen(Team team, int lane)
        {
            BattleRegistry.GetTowers(team.Opponent(), towerBuffer, aliveOnly: false);
            for (int i = 0; i < towerBuffer.Count; i++)
            {
                var t = towerBuffer[i];
                if (!t.IsKing && t.Lane == lane) return !t.IsAlive;
            }

            return false;
        }

        public bool IsDeployable(Team team, Vector2 p)
        {
            if (!InField(p, deployEdgeMargin)) return false;
            if (InRiverBand(p, 0.12f)) return false;

            if (!IsOnSide(team, p))
            {
                if (!PocketOpen(team, LaneOf(p))) return false;
                if (Mathf.Abs(p.y - RiverY) > riverHalfThickness + pocketDepth) return false;
            }

            var all = BattleRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c is Tower t && t.IsAlive && Vector2.Distance(p, t.Position) < t.Radius + 0.2f) return false;
            }

            return true;
        }

        /// <summary>Nearest point on this side's own half, used by the AI to keep its placements legal.</summary>
        public Vector2 ClampToOwnSide(Team team, Vector2 p)
        {
            float margin = deployEdgeMargin + 0.05f;
            float x = Mathf.Clamp(p.x, Center.x - halfWidth + margin, Center.x + halfWidth - margin);
            float minDistFromRiver = riverHalfThickness + 0.2f;
            float y;
            if (team == Team.Blue)
                y = Mathf.Clamp(p.y, Center.y - halfHeight + margin, RiverY - minDistFromRiver);
            else
                y = Mathf.Clamp(p.y, RiverY + minDistFromRiver, Center.y + halfHeight - margin);

            var result = new Vector2(x, y);

            // Nudge off any tower footprint.
            var all = BattleRegistry.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Tower t) || !t.IsAlive) continue;
                Vector2 away = result - t.Position;
                float min = t.Radius + 0.3f;
                if (away.magnitude < min)
                {
                    if (away.sqrMagnitude < 0.0001f) away = new Vector2(Center.x - t.Position.x, 0f).normalized;
                    if (away.sqrMagnitude < 0.0001f) away = Vector2.right;
                    result = t.Position + away.normalized * min;
                }
            }

            return result;
        }

        /// <summary>
        /// Returns the next point a troop should walk toward to reach <paramref name="to"/>.
        /// Troops that need to cross the river commit to a bridge, line up with it, walk its centre line and only
        /// let go of it once they are off the far end.
        /// </summary>
        public Vector2 Steer(Vector2 from, Vector2 to, ref int bridge, float radius = 0.2f)
        {
            bool fromTop = from.y > RiverY;
            bool toTop = to.y > RiverY;
            bool inBand = InRiverBand(from, 0.05f);

            if (fromTop == toTop && !inBand)
            {
                bridge = -1;
                return to;
            }

            if (bridge < 0)
            {
                bool onABridge = inBand && Mathf.Abs(from.x - BridgeX(LaneOf(from))) < bridgeHalfWidth + 0.3f;
                bridge = onABridge ? LaneOf(from) : BestBridge(from, to);
            }

            float bx = BridgeX(bridge);
            float travel = toTop ? 1f : -1f;
            float progress = (from.y - RiverY) * travel;
            float reach = riverHalfThickness + BridgeApproach;
            var entry = new Vector2(bx, RiverY - travel * reach);
            var exit = new Vector2(bx, RiverY + travel * reach);

            if (progress >= reach - 0.08f)
            {
                bridge = -1;
                return to;
            }

            // On the bridge: keep to the centre line until the far end.
            if (progress > -riverHalfThickness) return exit;

            // Far from the river: head for the bridge mouth.
            if (progress < -(reach + 0.05f)) return entry;

            // At the mouth: step on if lined up, otherwise line up first (stops troops grinding against the bank).
            float fit = Mathf.Max(0.05f, bridgeHalfWidth - radius - 0.03f);
            return Mathf.Abs(from.x - bx) <= fit ? exit : entry;
        }

        /// <summary>
        /// Bends a straight walk around any standing tower in the way, other than <paramref name="ignore"/> (usually the
        /// troop's own target). Without this, troops deployed behind a tower walk into it and stop.
        /// </summary>
        public Vector2 AroundTowers(Vector2 from, Vector2 to, float radius, Combatant ignore)
        {
            Vector2 segment = to - from;
            float length = segment.magnitude;
            if (length < 0.001f) return to;

            Vector2 direction = segment / length;
            Tower blocker = null;
            float nearest = float.MaxValue;
            float blockerSide = 0f;
            var all = BattleRegistry.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (!(all[i] is Tower tower) || !tower.IsAlive || tower == ignore) continue;

                Vector2 offset = tower.Position - from;
                float along = Vector2.Dot(offset, direction);
                if (along <= 0f || along > length + tower.Radius) continue;

                float side = direction.x * offset.y - direction.y * offset.x;
                if (Mathf.Abs(side) >= tower.Radius + radius + 0.06f) continue;

                if (along < nearest)
                {
                    nearest = along;
                    blocker = tower;
                    blockerSide = side;
                }
            }

            if (blocker == null) return to;

            // Pass on the side away from the tower centre; head-on, pass on the side facing the middle of the arena.
            Vector2 left = new Vector2(-direction.y, direction.x);
            float pass = blockerSide > 0f ? -1f : 1f;
            if (Mathf.Abs(blockerSide) < 0.05f)
            {
                float towardMiddle = Vector2.Dot(Center - blocker.Position, left);
                pass = towardMiddle >= 0f ? 1f : -1f;
            }

            return blocker.Position + left * pass * (blocker.Radius + radius + 0.3f);
        }

        public int BestBridge(Vector2 from, Vector2 to)
        {
            float left = CrossingCost(from, to, 0);
            float right = CrossingCost(from, to, 1);
            if (Mathf.Abs(left - right) < 0.01f) return LaneOf(from);
            return left < right ? 0 : 1;
        }

        /// <summary>Walking distance between two points, going around the river when needed.</summary>
        public float PathLength(Vector2 from, Vector2 to)
        {
            bool sameSide = (from.y > RiverY) == (to.y > RiverY);
            if (sameSide) return Vector2.Distance(from, to);
            return Mathf.Min(CrossingCost(from, to, 0), CrossingCost(from, to, 1));
        }

        private float CrossingCost(Vector2 from, Vector2 to, int bridge)
        {
            float bx = BridgeX(bridge);
            float fromSide = from.y > RiverY ? 1f : -1f;
            var entry = new Vector2(bx, RiverY + fromSide * (riverHalfThickness + BridgeApproach));
            var exit = new Vector2(bx, RiverY - fromSide * (riverHalfThickness + BridgeApproach));
            return Vector2.Distance(from, entry) + Vector2.Distance(entry, exit) + Vector2.Distance(exit, to);
        }

        private void OnDrawGizmos()
        {
            Vector3 c = transform.position;
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawWireCube(c, new Vector3(halfWidth * 2f, halfHeight * 2f, 0f));

            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireCube(c, new Vector3(halfWidth * 2f, riverHalfThickness * 2f, 0f));

            Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.9f);
            for (int b = 0; b < 2; b++)
            {
                Gizmos.DrawWireCube(new Vector3(BridgeX(b), c.y, 0f), new Vector3(bridgeHalfWidth * 2f, riverHalfThickness * 2f + 0.2f, 0f));
            }

            Gizmos.color = Color.red;
            foreach (Team team in new[] { Team.Blue, Team.Red })
            {
                Gizmos.DrawWireSphere(TowerSpot(team, true, -1), 0.8f);
                Gizmos.DrawWireSphere(TowerSpot(team, false, 0), 0.55f);
                Gizmos.DrawWireSphere(TowerSpot(team, false, 1), 0.55f);
            }
        }
    }
}
