using SpacetimeDB;
using System;
public static partial class Module
{
    [Table(Name = "walking", Public = true)]
    public partial struct Walking
    {
        [PrimaryKey, Unique]
        public uint entity_id;
        public DbVector2 target_walk_pos;
    }

    [Table(Name = "set_walk_target_timer", Scheduled = nameof(SetFutureTargetWalkPos), ScheduledAt = nameof(scheduled_at))]
    public partial struct SetWalkTargetTimer
    {
        [PrimaryKey, AutoInc]
        public ulong scheduled_id;

        [Unique]
        public uint entity_id;
        public ScheduleAt scheduled_at;
        public DbVector2 position;
        public bool remove_other_actions;
    }

    [Table(Name = "nav_mesh_polygon", Public = true)]
    public partial struct NavMeshPolygon(List<DbVector2> vertices, DbVector2 centroid, uint radius)
    {
        [PrimaryKey, AutoInc]
        public uint polygon_id;

        public List<DbVector2> vertices = vertices;

        public DbVector2 centroid = centroid;

        [SpacetimeDB.Index.BTree]
        public uint radius = radius;
    }

    [Table(Name = "nav_mesh_polygon_edge", Public = true)]
    public partial struct NavMeshPolygonEdge
    {
        [PrimaryKey, AutoInc]
        public uint edge_id;

        public uint from_polygon_id;
        public uint to_polygon_id;

        // The shared edge points
        public DbVector2 shared_vertex_a;
        public DbVector2 shared_vertex_b;
    }

    public static NavMeshPolygon FindNearestPolygon(ReducerContext ctx, DbVector2 position, uint radius)
    {
        NavMeshPolygon nearestPolygon = default;
        float closestDistance = float.MaxValue;

        foreach (var polygon in ctx.Db.nav_mesh_polygon.radius.Filter(radius))
        {
            if (PointInPolygon(ctx, polygon, position))
                return polygon; // Best case: inside polygon.

            float distance = (polygon.centroid - position).Magnitude();
            if (distance < closestDistance)
            {
                closestDistance = distance;
                nearestPolygon = polygon;
            }
        }

        return nearestPolygon;
    }

    public class PathNode
    {
        public uint PolygonId;
        public DbVector2 EntryPoint; // Optimal point where we entered this polygon
    }

    public static List<DbVector2> AStarPolygon(ReducerContext ctx, NavMeshPolygon startPoly, NavMeshPolygon goalPoly, DbVector2 startPos, DbVector2 endPos, uint radius)
    {
        var openSet = new PriorityQueue<PathNode, float>();

        // Initialize with optimal entry point from start position
        var startNode = new PathNode
        {
            PolygonId = startPoly.polygon_id,
            EntryPoint = startPos
        };
        openSet.Enqueue(startNode, 0f);

        var cameFrom = new Dictionary<uint, PathNode>();
        var gScore = new Dictionary<uint, float> { [startPoly.polygon_id] = 0f };
        var fScore = new Dictionary<uint, float> { [startPoly.polygon_id] = HeuristicPolygon(ctx, startPoly.polygon_id, goalPoly.polygon_id) };
        var bestEntryPoints = new Dictionary<uint, DbVector2> { [startPoly.polygon_id] = startNode.EntryPoint };

        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();

            if (current.PolygonId == goalPoly.polygon_id)
                return ReconstructPath(ctx, cameFrom, current, startPoly.polygon_id, endPos);

            foreach (var edge in ctx.Db.nav_mesh_polygon_edge.Iter()
                     .Where(e => e.from_polygon_id == current.PolygonId || e.to_polygon_id == current.PolygonId))
            {
                uint neighbor = edge.from_polygon_id == current.PolygonId ? edge.to_polygon_id : edge.from_polygon_id;

                // Find the optimal point along this edge
                DbVector2 edgePoint = FindOptimalEdgePoint(
                    current.EntryPoint,
                    edge.shared_vertex_a,
                    edge.shared_vertex_b,
                    endPos
                );

                // Calculate exact distance through this point
                float segmentLength = (current.EntryPoint - edgePoint).Magnitude();
                float tentativeGScore = gScore[current.PolygonId] + segmentLength;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor] ||
                    !bestEntryPoints.ContainsKey(neighbor))
                {
                    cameFrom[neighbor] = current;
                    bestEntryPoints[neighbor] = edgePoint;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HeuristicPolygon(ctx, neighbor, goalPoly.polygon_id);

                    openSet.Enqueue(new PathNode
                    {
                        PolygonId = neighbor,
                        EntryPoint = edgePoint
                    }, fScore[neighbor]);
                }
            }
        }

        return new List<DbVector2>();
    }

    private static DbVector2 FindOptimalEdgePoint(DbVector2 fromPoint, DbVector2 edgeA, DbVector2 edgeB, DbVector2 target)
    {
        // This finds the point along the edge that creates the straightest path to target
        // while still being on the edge

        // First check if we can "see" the target directly through the edge
        if (LineSegmentsIntersect(fromPoint, target, edgeA, edgeB))
        {
            var intersection = GetLineIntersection(fromPoint, target, edgeA, edgeB);
            return intersection;
        }

        // Otherwise find the edge point that minimizes the total path length
        float minCost = float.MaxValue;
        DbVector2 bestPoint = (edgeA + edgeB) * 0.5f; // Default to midpoint

        // Sample several points along the edge
        for (float t = 0; t <= 1; t += 0.02f)
        {
            DbVector2 candidate = edgeA + (edgeB - edgeA) * t;
            float cost = (fromPoint - candidate).Magnitude() + (candidate - target).Magnitude();

            if (cost < minCost)
            {
                minCost = cost;
                bestPoint = candidate;
            }
        }

        return bestPoint;
    }

    private static bool LineSegmentsIntersect(DbVector2 a1, DbVector2 a2, DbVector2 b1, DbVector2 b2)
    {
        // Implementation of line segment intersection check
        float d = (a2.x - a1.x) * (b2.y - b1.y) - (a2.y - a1.y) * (b2.x - b1.x);
        if (d == 0) return false;

        float t = ((b1.x - a1.x) * (b2.y - b1.y) - (b1.y - a1.y) * (b2.x - b1.x)) / d;
        float u = ((b1.x - a1.x) * (a2.y - a1.y) - (b1.y - a1.y) * (a2.x - a1.x)) / d;

        return t >= 0 && t <= 1 && u >= 0 && u <= 1;
    }

    private static DbVector2 GetLineIntersection(DbVector2 a1, DbVector2 a2, DbVector2 b1, DbVector2 b2)
    {
        // Calculate intersection point of two lines
        float d = (a2.x - a1.x) * (b2.y - b1.y) - (a2.y - a1.y) * (b2.x - b1.x);
        float t = ((b1.x - a1.x) * (b2.y - b1.y) - (b1.y - a1.y) * (b2.x - b1.x)) / d;
        return new DbVector2(
            a1.x + t * (a2.x - a1.x),
            a1.y + t * (a2.y - a1.y)
        );
    }

    public static List<DbVector2> ReconstructPath(
     ReducerContext ctx,
     Dictionary<uint, PathNode> cameFrom,
     PathNode endNode,
     uint startPolygonId,
     DbVector2 endPos)
    {
        var path = new List<DbVector2>();
        var current = endNode;

        // Add final position
        path.Add(endPos);

        // Add optimal entry points in reverse order
        while (current.PolygonId != startPolygonId && cameFrom.ContainsKey(current.PolygonId))
        {
            path.Add(current.EntryPoint);
            current = cameFrom[current.PolygonId];
        }

        // Add start position
        path.Reverse();
        return path;
    }

    public static bool PointInPolygon(ReducerContext ctx, NavMeshPolygon polygon, DbVector2 point)
    {
        var vertices = polygon.vertices;//vertex_ids.Select(id => ctx.Db.nav_mesh_vertex.vertex_id.Find(id).Value.position).ToList();

        int crossings = 0;
        for (int i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];

            if (((a.y > point.y) != (b.y > point.y)) &&
                (point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y + float.Epsilon) + a.x))
            {
                crossings++;
            }
        }
        return (crossings % 2) == 1;
    }


    public static float HeuristicPolygon(ReducerContext ctx, uint fromPolygonId, uint toPolygonId)
    {
        var fromPoly = ctx.Db.nav_mesh_polygon.polygon_id.Find(fromPolygonId);
        var toPoly = ctx.Db.nav_mesh_polygon.polygon_id.Find(toPolygonId);

        if (fromPoly == null || toPoly == null)
            return 0;

        // Find the closest pair of edges between polygons
        float minDist = float.MaxValue;

        foreach (var edge in ctx.Db.nav_mesh_polygon_edge.Iter()
                 .Where(e => (e.from_polygon_id == fromPolygonId && e.to_polygon_id == toPolygonId) ||
                             (e.from_polygon_id == toPolygonId && e.to_polygon_id == fromPolygonId)))
        {
            var edgeCenter = (edge.shared_vertex_a + edge.shared_vertex_b) * 0.5f;
            float dist = (fromPoly.Value.centroid - edgeCenter).Magnitude() +
                         (toPoly.Value.centroid - edgeCenter).Magnitude();

            if (dist < minDist)
                minDist = dist;
        }

        return minDist;
    }



    [Table(Name = "path", Public = true)]
    public partial struct Path
    {
        [PrimaryKey, AutoInc]
        public uint path_id;

        [SpacetimeDB.Index.BTree]
        public uint entity_id;

        public DbVector2 position;

        public uint order;
    }

    /// <summary>
    /// Creates path from entities position to the given position
    /// </summary>
    [Reducer]
    public static void CreatePath(ReducerContext ctx, Entity entity, DbVector2 position, uint radius)
    {
        var startPoly = FindNearestPolygon(ctx, entity.position, radius);
        var endPoly = FindNearestPolygon(ctx, position, radius);

        DbVector2 realPos = position;

        if (!PointInPolygon(ctx, endPoly, position))
        {
            realPos = GetClosestEdgePoint(ctx, endPoly, position);
        }



        var path = AStarPolygon(ctx, startPoly, endPoly, entity.position, realPos,radius );

        // Clear old path and insert new one
        ctx.Db.path.entity_id.Delete(entity.entity_id);

        for (int i = 0; i < path.Count; i++)
        {
            ctx.Db.path.Insert(new Path
            {
                entity_id = entity.entity_id,
                position = path[i],
                order = (uint)i
            });
        }
    }

    private static DbVector2 GetClosestEdgePoint(ReducerContext ctx, NavMeshPolygon poly, DbVector2 point)
    {
        var vertices = poly.vertices;
            //.Select(id => ctx.Db.nav_mesh_vertex.vertex_id.Find(id).Value.position)
            //.ToList();

        DbVector2 closestPoint = default;
        float closestDistance = float.MaxValue;

        // Check each edge of the polygon
        for (int i = 0; i < vertices.Count; i++)
        {
            var a = vertices[i];
            var b = vertices[(i + 1) % vertices.Count];

            var edgePoint = GetClosestPointOnLine(a, b, point);
            float dist = (edgePoint - point).Magnitude();

            if (dist < closestDistance)
            {
                closestDistance = dist;
                closestPoint = edgePoint;
            }
        }

        return closestPoint;
    }

    private static DbVector2 GetClosestPointOnLine(DbVector2 lineA, DbVector2 lineB, DbVector2 point)
    {
        var lineVec = lineB - lineA;
        var pointVec = point - lineA;
        float lineLength = lineVec.Magnitude();
        var lineUnit = lineVec / lineLength;

        float projection = DbVector2.Dot(pointVec, lineUnit);
        projection = Math.Clamp(projection, 0f, lineLength);

        return lineA + lineUnit * projection;
    }

    [Reducer]
    public static void SetFutureTargetWalkPos(ReducerContext ctx, SetWalkTargetTimer caller)
    {
        SetTargetWalkPos(ctx, caller.entity_id, caller.position, caller.remove_other_actions);
    }

    [Reducer]
    public static void SetTargetWalkPos(ReducerContext ctx, uint entityId, DbVector2 position, bool removeOtherActions = true)
    {
        var nEntity = ctx.Db.entity.entity_id.Find(entityId);
        if (nEntity == null) return;
        Entity entity = nEntity.Value;

        if (entity.busy) return;

        ctx.Db.path.entity_id.Delete(entity.entity_id); // THIS PART IS TEMP
        CreatePath(ctx, entity, position, 35);//TEMP RADIUS

        ctx.Db.set_walk_target_timer.entity_id.Delete(entityId);

        var nullableAttacking = ctx.Db.attacking.entity_id.Find(entityId);

        if (nullableAttacking != null)
        {
            float timeSinceAttackStarted = GetTimestampDifferenceInSeconds(nullableAttacking.Value.attack_start_time, ctx.Timestamp);

            var nullableActor = ctx.Db.actor.entity_id.Find(entityId);
            if (nullableActor == null) return;
            Actor actor = nullableActor.Value;

            var nStats = ctx.Db.actor_base_stats.actor_id.Find(actor.actor_id);
            if (nStats == null) return;
            ActorBaseStats stats = nStats.Value;

            float windupTime = (1f / stats.attack_speed) * stats.windup_percent;
            float timeUntilHit = windupTime - timeSinceAttackStarted;

            if (Math.Abs(timeUntilHit) < .4f * windupTime)
            {
                ctx.Db.set_walk_target_timer.Insert(new()
                {
                    scheduled_at = new Timestamp(nullableAttacking.Value.attack_start_time.MicrosecondsSinceUnixEpoch + (int)(windupTime * 1.4f * 1_000_000f)),
                    entity_id = entityId,
                    position = position,
                    remove_other_actions = removeOtherActions
                });
                return;
            }
        }

        if (removeOtherActions) ctx.Db.attacking.entity_id.Delete(entityId);


        var newWalking = new Walking()
        {
            entity_id = entityId,
            target_walk_pos = position
        };

        ctx.Db.walking.entity_id.Delete(entityId);
        ctx.Db.walking.Insert(newWalking);

    }

    [Reducer]
    public static void MoveActor(ReducerContext ctx, Walking walker, Entity entity)
    {
        float moveSpeed = 250; //Units per Second

        #region find entity and actor

        

        List<Path> paths = ctx.Db.path.entity_id.Filter(entity.entity_id).ToList();
        if (paths.Count == 0) return;

        paths.Sort((a, b) => a.order.CompareTo(b.order));


        var nullableUnit = ctx.Db.actor.entity_id.Find(entity.entity_id);
        if (nullableUnit == null)
        {
            ctx.Db.walking.entity_id.Delete(entity.entity_id);
            return;
        }
        Actor actor = nullableUnit.Value;
        #endregion

        #region movement math
        var difference = paths[0].position - entity.position;

        float distance = difference.Magnitude();

        float distanceToMove = moveSpeed * deltaTime.Microseconds / 1_000_000; //in seconds

        var direction = difference.Normalized();

        float velocity = (entity.last_position - entity.position).Magnitude();

        DbVector2 newPos;
        if (distance <= distanceToMove)
        {
            newPos = paths[0].position;
            if (paths.Count - 1 <= 0)
            {
                ctx.Db.walking.entity_id.Delete(entity.entity_id);
            }
            else
            {
                //target next point
                ctx.Db.path.path_id.Delete(paths[0].path_id);
            }
        }
        else
        {
            newPos = new(entity.position.x + (direction.x * distanceToMove), entity.position.y + (direction.y * distanceToMove));
        }

        float finalRotation = DbVector2.RotationFromDirection(direction);
        #endregion

        #region update entity and actor

        Actor newActor = actor;

        newActor.rotation = finalRotation;

        ctx.Db.actor.entity_id.Delete(entity.entity_id);
        ctx.Db.actor.Insert(newActor);


        DbVector2 newLastPos = entity.position;

        ctx.Db.entity.entity_id.Delete(entity.entity_id);
        ctx.Db.entity.Insert(new Entity()
        {
            entity_id = entity.entity_id,
            position = newPos,
            last_position = newLastPos,
            busy = false //walking isnt a busy action
        });
        #endregion
    }

    [Reducer]
    public static void MoveAllPlayers(ReducerContext ctx)
    {

        var list = ctx.Db.walking.Iter();
        foreach (var walker in list)
        {
            var nullableEntity = ctx.Db.entity.entity_id.Find(walker.entity_id);
            if (nullableEntity == null)
            {
                Log.Info("null entity, skipping walk and deleting it");
                ctx.Db.walking.entity_id.Delete(walker.entity_id);
                continue;
            }
            Entity entity = nullableEntity.Value;

            MoveActor(ctx, walker, entity);

        }
    }


    public static List<DbVector2> GetShrunkenVertices(List<DbVector2> tempVertices, DbVector2 centroid, float amount)
    {
        var vertices = new List<DbVector2>();
        foreach (var vertex in tempVertices)
        {
            vertices.Add(vertex.MovedTowards(centroid, amount));
        }
        return vertices;
    }

    [Reducer]
    public static void GenerateNavmesh(ReducerContext ctx, uint radius)
    {
        // Define vertices for our U-shape (5 rectangles forming a U)
        // Left vertical rectangle (top part)
        DbVector2 v1 = new(-700 + radius, 700 - radius);   // top left
        DbVector2 v2 = new(-700 + radius, 200);            // bottom left
        DbVector2 v3 = new(-200- radius, 200 - radius);                     // bottom right
        DbVector2 v4 = new(-200 - radius, 700 - radius);   // top right

        // Left vertical rectangle (bottom part)
        DbVector2 v6 = new(-700 + radius, -700 + radius);  // bottom left
        DbVector2 v7 = new(-200, -700 + radius);           // bottom right

        // Bottom horizontal rectangle
        DbVector2 v11 = new(200, -700 + radius);           // bottom right
        DbVector2 v12 = new(200 + radius, 200 - radius);   // top right

        // Right vertical rectangle (bottom part)
        DbVector2 v15 = new(700 - radius, -700 + radius);  // bottom right
        DbVector2 v16 = new(700 - radius, 200);            // top right

        // Right vertical rectangle (top part)
        DbVector2 v19 = new(700 - radius, 700 - radius);   // top right
        DbVector2 v20 = new(200 + radius, 700 - radius);            // top left

        // Create polygons with their vertices and centroids
        // Left top vertical rectangle
        var leftTopVertices = new List<DbVector2> { v1, v2, v3, v4 };
        var leftTopCentroid = CalculateCentroid(leftTopVertices);
        var leftTopPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(leftTopVertices, leftTopCentroid, radius));

        // Left bottom vertical rectangle
        var leftBottomVertices = new List<DbVector2> { v2, v6, v7, v3 };
        var leftBottomCentroid = CalculateCentroid(leftBottomVertices);
        var leftBottomPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(leftBottomVertices, leftBottomCentroid, radius));

        // Bottom horizontal rectangle
        var bottomVertices = new List<DbVector2> { v3, v7, v11, v12 };
        var bottomCentroid = CalculateCentroid(bottomVertices);
        var bottomPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(bottomVertices, bottomCentroid, radius));

        // Right bottom vertical rectangle
        var rightBottomVertices = new List<DbVector2> { v12, v11, v15, v16 };
        var rightBottomCentroid = CalculateCentroid(rightBottomVertices);
        var rightBottomPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(rightBottomVertices, rightBottomCentroid, radius));

        // Right top vertical rectangle
        var rightTopVertices = new List<DbVector2> { v12, v16, v19, v20 };
        var rightTopCentroid = CalculateCentroid(rightTopVertices);
        var rightTopPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(rightTopVertices, rightTopCentroid, radius));

        // Create connections between polygons
        // Left top connects to Left bottom
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = leftTopPoly.polygon_id,
            to_polygon_id = leftBottomPoly.polygon_id,
            shared_vertex_a = v2,  // bottom left of top rectangle
            shared_vertex_b = v3   // bottom right of top rectangle (top of bottom rectangle)
        });

        // Left bottom connects to Bottom
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = leftBottomPoly.polygon_id,
            to_polygon_id = bottomPoly.polygon_id,
            shared_vertex_a = v7,  // bottom right of left rectangle
            shared_vertex_b = v3   // top right of left rectangle (left of bottom rectangle)
        });

        // Bottom connects to Right bottom
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = bottomPoly.polygon_id,
            to_polygon_id = rightBottomPoly.polygon_id,
            shared_vertex_a = v11,  // bottom right of bottom rectangle
            shared_vertex_b = v12   // top right of bottom rectangle (bottom of right rectangle)
        });

        // Right bottom connects to Right top
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = rightBottomPoly.polygon_id,
            to_polygon_id = rightTopPoly.polygon_id,
            shared_vertex_a = v16,  // top right of bottom rectangle
            shared_vertex_b = v12   // top left of bottom rectangle (bottom of top rectangle)
        });

        // Create reverse connections for bidirectional movement
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = leftBottomPoly.polygon_id,
            to_polygon_id = leftTopPoly.polygon_id,
            shared_vertex_a = v2,
            shared_vertex_b = v3
        });

        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = bottomPoly.polygon_id,
            to_polygon_id = leftBottomPoly.polygon_id,
            shared_vertex_a = v7,
            shared_vertex_b = v3
        });

        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = rightBottomPoly.polygon_id,
            to_polygon_id = bottomPoly.polygon_id,
            shared_vertex_a = v11,
            shared_vertex_b = v12
        });

        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = rightTopPoly.polygon_id,
            to_polygon_id = rightBottomPoly.polygon_id,
            shared_vertex_a = v16,
            shared_vertex_b = v12
        });
    }

    public static DbVector2 CalculateCentroid(List<DbVector2> vertices)
    {
        float signedArea = 0;
        float centroidX = 0;
        float centroidY = 0;

        int count = vertices.Count;

        for (int i = 0; i < count; i++)
        {
            var current = vertices[i];
            var next = vertices[(i + 1) % count];

            float a = (current.x * next.y) - (next.x * current.y);
            signedArea += a;

            centroidX += (current.x + next.x) * a;
            centroidY += (current.y + next.y) * a;
        }

        signedArea *= 0.5f;

        if (Math.Abs(signedArea) < float.Epsilon)
        {
            // Fallback: degenerate polygon, just average the points
            float avgX = vertices.Sum(v => v.x) / count;
            float avgY = vertices.Sum(v => v.y) / count;
            return new DbVector2(avgX, avgY);
        }

        centroidX /= (6 * signedArea);
        centroidY /= (6 * signedArea);

        return new DbVector2(centroidX, centroidY);
    }

}
