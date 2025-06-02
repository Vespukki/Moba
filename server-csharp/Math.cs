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
}