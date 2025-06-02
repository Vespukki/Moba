using SpacetimeDB;

public static partial class Module
{

    [Table(Name = "entity", Public = true)]
    public partial struct Entity
    {
        [PrimaryKey, Unique, AutoInc]
        public uint entity_id;
        public DbVector2 position;
        public DbVector2 last_position;
        
    }


    
}
