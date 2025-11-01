public static class NetKeys
{
    // Player Custom Properties
    public const string PLAYER_READY = "p_ready";
    public const string PLAYER_NAME = "p_name";
    public const string PLAYER_GENDER = "p_gender"; // "F" / "M"
    public const string PLAYER_STATS = "p_stats";  // string json, örn: {"spd":5,"pow":3,...}

    // Room Custom Properties
    public const string ROOM_CREATED_AT = "r_created_at"; // ticks/string
    public const string ROOM_OWNER_USERID = "r_owner";      // MasterClient UserId

    // (önceden kullanmýþtýk, kalsýn zarar vermez)
    public const string PLAYER_SLOT = "p_slot";
    public const string ROOM_TAKEN_SLOTS = "r_taken";

    // YENÝ: Odayý kapatma sinyali
    public const string ROOM_SHUTDOWN = "r_shutdown";   // bool true => oda kapat
    public const byte EVT_ROOM_SHUTDOWN = 1;
}
