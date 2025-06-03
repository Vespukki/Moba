using SpacetimeDB;


// This allows us to store 2D points in tables.
[SpacetimeDB.Type]
public partial struct DbVector2
{
    public float x;
    public float y;

    public DbVector2(float x, float y)
    {
        this.x = x;
        this.y = y;
    }

    public DbVector2 Normalized()
    {
        float magnitude = (float)Math.Sqrt(x * x + y * y);

        // Handle division by zero for zero-length vectors
        if (magnitude > 0)
        {
            return new DbVector2(x / magnitude, y / magnitude);
        }



        // Return zero vector if original vector has zero length
        return new DbVector2(0, 0);
    }

    // Returns the magnitude (length) of the vector
    public float Magnitude()
    {
        return (float)Math.Sqrt(x * x + y * y);
    }

    // Adds two vectors
    public static DbVector2 operator +(DbVector2 a, DbVector2 b)
    {
        return new DbVector2(a.x + b.x, a.y + b.y);
    }

    // Subtracts one vector from another
    public static DbVector2 operator -(DbVector2 a, DbVector2 b)
    {
        return new DbVector2(a.x - b.x, a.y - b.y);
    }

    // Returns the Euclidean distance between two points
    public static float Distance(DbVector2 a, DbVector2 b)
    {
        float dx = a.x - b.x;
        float dy = a.y - b.y;
        return (float)Math.Sqrt(dx * dx + dy * dy);
    }

    public static float RotationFromDirection(DbVector2 direction)
    {
        return MathF.Atan2(direction.x, direction.y) * 180 / MathF.PI;
    }
}

public static partial class Module
{
    /// <summary>
    /// returns absolute time difference between 2 Timestamps
    /// </summary>
    /// <param name="a"></param>
    /// <param name="b"></param>
    /// <returns></returns>
    public static float GetTimestampDifferenceInSeconds(Timestamp a, Timestamp b)
    {
        return Math.Abs((float)a.TimeDurationSince(b).Microseconds / 1_000_000f);
    }
}