public static class NetKeys
{
    // Player Custom Properties
    public const string PLAYER_READY = "p_ready";
    public const string PLAYER_NAME = "p_name";
    public const string PLAYER_GENDER = "p_gender"; // "F" / "M"
    public const string PLAYER_STATS = "p_stats";   // string json, örn: {"spd":5,"pow":3,...}

    // Room Custom Properties
    public const string ROOM_CREATED_AT = "r_created_at"; // ticks/string
    public const string ROOM_OWNER_USERID = "r_owner";    // MasterClient UserId
    public const string PLAYER_SLOT = "p_slot";      // int? (null = seçilmemiþ)
    public const string ROOM_TAKEN_SLOTS = "r_taken"; // string, örn: "0,2,3" (boþ = hiçbiri)

}
