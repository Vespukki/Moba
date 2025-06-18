using UnityEngine;
using SpacetimeDB.Types;
using System.Collections.Generic;

public class NavmeshVisualizer : MonoBehaviour
{
    public static NavmeshVisualizer instance;

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Debug.LogError("2 navmesh Visualizers in scene!");
            Destroy(this);
        }
    }

    public static Dictionary<uint,NavMeshVertex> vertices = new();
    public static Dictionary<uint, NavMeshEdge> edges = new();

    public static Dictionary<uint, NavMeshPolygon> polygons = new();

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;


       /* foreach (var vertex in vertices.Values)
        {
            Gizmos.DrawSphere(new(vertex.Position.X, 0, vertex.Position.Y), 5f);
        }

        foreach (var edge in edges.Values)
        {
            NavMeshVertex from = vertices[edge.FromVertexId];
            NavMeshVertex to = vertices[edge.ToVertexId];

            Gizmos.DrawLine(new(from.Position.X, 0, from.Position.Y), new(to.Position.X, 0, to.Position.Y));
        }*/

        foreach (var polygon in polygons.Values)
        {
            List<NavMeshVertex> tempVertices = new();

            foreach (var vertexId in polygon.VertexIds)
            {
                NavMeshVertex vertex = vertices[vertexId];

                Gizmos.DrawSphere(new(vertex.Position.X, 0, vertex.Position.Y), 5f);

                foreach (var otherVertex in tempVertices)
                {
                    Gizmos.DrawLine(new(vertex.Position.X, 0, vertex.Position.Y), new(otherVertex.Position.X, 0, otherVertex.Position.Y));
                }

                tempVertices.Add(vertex);
            }

            Gizmos.DrawCube(new(polygon.Centroid.X, 0, polygon.Centroid.Y), Vector3.one * 10f);
        }
    }

}
