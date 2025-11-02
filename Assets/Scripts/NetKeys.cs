public static class NetKeys
{
    // Player Custom Properties
    public const string PLAYER_READY = "p_ready";
    public const string PLAYER_NAME = "p_name";
    public const string PLAYER_GENDER = "p_gender";
    public const string PLAYER_STATS = "p_stats";
    public const string PLAYER_SELECTED_CHAR = "p_sel_char";

    // Room Custom Properties
    public const string ROOM_CREATED_AT = "r_created_at";
    public const string ROOM_OWNER_USERID = "r_owner";
    public const string ROOM_SHUTDOWN = "r_shutdown";

    public const string ROOM_SAVED_JSON = "r_saved_json";
    public const string ROOM_CLAIMS_JSON = "r_claims";
    public const string ROOM_SAVE_ID = "r_save_id";   // YENÝ: kayýt kimliði (GUID)

    // Events
    public const byte EVT_ROOM_SHUTDOWN = 1;
}
