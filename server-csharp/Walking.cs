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

    [Table(Name = "nav_mesh_vertex", Public = true)]
    public partial struct NavMeshVertex(DbVector2 position)
    {
        [PrimaryKey, AutoInc]
        public uint vertex_id;

        public DbVector2 position = position;
    }

    [Table(Name = "nav_mesh_edge", Public = true)]
    public partial struct NavMeshEdge(uint from_id, uint to_id)
    {
        [PrimaryKey, AutoInc]
        public uint edge_id;

        public uint from_vertex_id = from_id;
        public uint to_vertex_id = to_id;
    }

    [Table(Name = "nav_mesh_polygon", Public = true)]
    public partial struct NavMeshPolygon(List<uint> vertex_ids, DbVector2 centroid)
    {
        [PrimaryKey, AutoInc]
        public uint polygon_id;

        // Let's store a list of vertex ids that define the polygon
        public List<uint> vertex_ids = vertex_ids;

        // Optionally precompute centroid
        public DbVector2 centroid = centroid;
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

    public static NavMeshPolygon FindNearestPolygon(ReducerContext ctx, DbVector2 position)
    {
        NavMeshPolygon nearestPolygon = default;
        float closestDistance = float.MaxValue;

        foreach (var polygon in ctx.Db.nav_mesh_polygon.Iter())
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

    public static List<DbVector2> AStarPolygon(ReducerContext ctx, uint startPolygonId, uint goalPolygonId)
    {
        var openSet = new PriorityQueue<uint, float>();
        openSet.Enqueue(startPolygonId, 0f);

        var cameFrom = new Dictionary<uint, uint>();
        var gScore = new Dictionary<uint, float> { [startPolygonId] = 0f };
        var fScore = new Dictionary<uint, float> { [startPolygonId] = HeuristicPolygon(ctx, startPolygonId, goalPolygonId) };

        // Store edge transitions
        var edgeTransitions = new Dictionary<uint, NavMeshPolygonEdge>();

        while (openSet.Count > 0)
        {
            uint current = openSet.Dequeue();

            if (current == goalPolygonId)
                return ReconstructPathPolygon(ctx, cameFrom, edgeTransitions, current);

            foreach (var edge in ctx.Db.nav_mesh_polygon_edge.Iter()
                     .Where(e => e.from_polygon_id == current || e.to_polygon_id == current))
            {
                uint neighbor = edge.from_polygon_id == current ? edge.to_polygon_id : edge.from_polygon_id;

                // Calculate actual edge midpoint as transition cost
                float edgeLength = (edge.shared_vertex_a - edge.shared_vertex_b).Magnitude();
                float tentativeGScore = gScore[current] + edgeLength;

                if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                {
                    cameFrom[neighbor] = current;
                    edgeTransitions[neighbor] = edge;
                    gScore[neighbor] = tentativeGScore;
                    fScore[neighbor] = tentativeGScore + HeuristicPolygon(ctx, neighbor, goalPolygonId);

                    if (!openSet.UnorderedItems.Any(item => item.Element == neighbor))
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                }
            }
        }

        return new List<DbVector2>();
    }

    public static List<DbVector2> ReconstructPathPolygon(
    ReducerContext ctx,
    Dictionary<uint, uint> cameFrom,
    Dictionary<uint, NavMeshPolygonEdge> edgeTransitions,
    uint current)
    {
        var path = new List<DbVector2>();
        var polygonPath = new Stack<uint>();

        // Reconstruct polygon path
        while (cameFrom.ContainsKey(current))
        {
            polygonPath.Push(current);
            current = cameFrom[current];
        }
        polygonPath.Push(current);

        // Convert polygon path to edge points
        if (polygonPath.Count > 1)
        {
            uint prevPoly = polygonPath.Pop();
            while (polygonPath.Count > 0)
            {
                uint nextPoly = polygonPath.Pop();
                var edge = edgeTransitions[nextPoly];

                // Use the midpoint of the shared edge
                var edgeMid = (edge.shared_vertex_a + edge.shared_vertex_b) * 0.5f;
                path.Add(edgeMid);

                prevPoly = nextPoly;
            }
        }

        return path;
    }

    public static bool PointInPolygon(ReducerContext ctx, NavMeshPolygon polygon, DbVector2 point)
    {
        var vertices = polygon.vertex_ids.Select(id => ctx.Db.nav_mesh_vertex.vertex_id.Find(id).Value.position).ToList();

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
    public static void CreatePath(ReducerContext ctx, Entity entity, DbVector2 position)
    {
        var startPoly = FindNearestPolygon(ctx, entity.position);
        var endPoly = FindNearestPolygon(ctx, position);

        var edgePath = AStarPolygon(ctx, startPoly.polygon_id, endPoly.polygon_id);

        List<DbVector2> finalPath = new();

        // Add start position
        finalPath.Add(entity.position);

      
        // Add all edge points
        finalPath.AddRange(edgePath);


        // Add final position
        finalPath.Add(position);

        // Clear old path and insert new one
        ctx.Db.path.entity_id.Delete(entity.entity_id);

        for (int i = 0; i < finalPath.Count; i++)
        {
            ctx.Db.path.Insert(new Path
            {
                entity_id = entity.entity_id,
                position = finalPath[i],
                order = (uint)i
            });
        }
    }

    private static DbVector2 GetClosestEdgePoint(ReducerContext ctx, NavMeshPolygon poly, DbVector2 point)
    {
        var vertices = poly.vertex_ids
            .Select(id => ctx.Db.nav_mesh_vertex.vertex_id.Find(id).Value.position)
            .ToList();

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
        CreatePath(ctx, entity, position);

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

    public static NavMeshVertex FindNearestNode(ReducerContext ctx, DbVector2 position)
    {
        NavMeshVertex nearestNode = default;
        float closestDistance = float.MaxValue;

        foreach (var node in ctx.Db.nav_mesh_vertex.Iter())
        {
            float distance = (node.position - position).Magnitude();
            if (distance < closestDistance)
            {
                closestDistance = distance;
                nearestNode = node;
            }
        }

        return nearestNode;
    }

    [Reducer]
    public static void GenerateNavmesh(ReducerContext ctx)
    {
        // Define vertices for our U-shape (3 rectangles forming a U)
        // Left vertical rectangle
        NavMeshVertex v1 = new(new(-700, 700));   // Top-left
        NavMeshVertex v2 = new(new(-700, -700));  // Bottom-left
        NavMeshVertex v3 = new(new(-200, -700));  // Bottom-right
        NavMeshVertex v4 = new(new(-200, 700));   // Top-right

        // Bottom horizontal rectangle
        NavMeshVertex v5 = new(new(-200, -200));  // Top-left
        NavMeshVertex v6 = new(new(200, -700));   // Bottom-right
        NavMeshVertex v7 = new(new(200, -200));   // Top-right
        //and uses v3

        // Right vertical rectangle
        NavMeshVertex v8 = new(new(200, 700));    // Top-left
        NavMeshVertex v9 = new(new(700, -700));  // Bottom-right
        NavMeshVertex v10 = new(new(700, 700));   // Top-right
        //and uses v6

        // Insert all vertices
        v1 = ctx.Db.nav_mesh_vertex.Insert(v1);
        v2 = ctx.Db.nav_mesh_vertex.Insert(v2);
        v3 = ctx.Db.nav_mesh_vertex.Insert(v3);
        v4 = ctx.Db.nav_mesh_vertex.Insert(v4);
        
        v5 = ctx.Db.nav_mesh_vertex.Insert(v5);
        v6 = ctx.Db.nav_mesh_vertex.Insert(v6);
        v7 = ctx.Db.nav_mesh_vertex.Insert(v7);

        v8 = ctx.Db.nav_mesh_vertex.Insert(v8);
        v9 = ctx.Db.nav_mesh_vertex.Insert(v9);
        v10 = ctx.Db.nav_mesh_vertex.Insert(v10);

        // Create polygons with their vertices and centroids
        // Left vertical rectangle
        var leftVertices = new List<uint> { v1.vertex_id, v2.vertex_id, v3.vertex_id, v4.vertex_id };
        var leftCentroid = CalculateCentroid(new List<DbVector2> { v1.position, v2.position, v3.position, v4.position });
        var leftPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(leftVertices, leftCentroid));

        // Bottom horizontal rectangle
        var bottomVertices = new List<uint> { v5.vertex_id, v3.vertex_id, v6.vertex_id, v7.vertex_id };
        var bottomCentroid = CalculateCentroid(new List<DbVector2> { v5.position, v3.position, v6.position, v7.position });
        var bottomPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(bottomVertices, bottomCentroid));

        // Right vertical rectangle
        var rightVertices = new List<uint> { v8.vertex_id, v6.vertex_id, v9.vertex_id, v10.vertex_id };
        var rightCentroid = CalculateCentroid(new List<DbVector2> { v8.position, v6.position, v9.position, v10.position });
        var rightPoly = ctx.Db.nav_mesh_polygon.Insert(new NavMeshPolygon(rightVertices, rightCentroid));

        // Create connections between polygons
        // Left connects to Bottom
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = leftPoly.polygon_id,
            to_polygon_id = bottomPoly.polygon_id,
            shared_vertex_a = v3.position,  // Bottom-right of left rectangle
            shared_vertex_b = v5.position   // Bottom-left of bottom rectangle
        });

        // Bottom connects to Right
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = bottomPoly.polygon_id,
            to_polygon_id = rightPoly.polygon_id,
            shared_vertex_a = v6.position,  // Bottom-right of bottom rectangle
            shared_vertex_b = v7.position  // Bottom-left of right rectangle
        });

        // Create reverse connections for bidirectional movement
        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = bottomPoly.polygon_id,
            to_polygon_id = leftPoly.polygon_id,
            shared_vertex_a = v3.position,
            shared_vertex_b = v5.position
        });

        ctx.Db.nav_mesh_polygon_edge.Insert(new NavMeshPolygonEdge
        {
            from_polygon_id = rightPoly.polygon_id,
            to_polygon_id = bottomPoly.polygon_id,
            shared_vertex_a = v6.position,
            shared_vertex_b = v7.position
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
