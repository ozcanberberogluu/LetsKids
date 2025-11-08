using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using System.Collections.Generic;

public class PlayerStats : MonoBehaviourPunCallbacks
{
    [Header("Raw Stats (from CharacterCreation)")]
    public int spd;
    public int pow;
    public int defn;
    public int atkspd;
    public int hp;
    public float jump = 1.6f;

    [Header("Movement Mappings")]
    public float baseMoveSpeed = 2.8f;
    public float spdToSpeed = 0.25f;     // moveSpeed = base + spd * spdToSpeed
    public float minMoveSpeed = 2.5f;
    public float maxMoveSpeed = 8.0f;

    public float GetComputedMoveSpeed() => Mathf.Clamp(baseMoveSpeed + spd * spdToSpeed, minMoveSpeed, maxMoveSpeed);
    public float GetJumpHeight() => jump;

    void Start()
    {
        ApplyFromOwnerProperties();
    }

    public override void OnPlayerPropertiesUpdate(Player targetPlayer, ExitGames.Client.Photon.Hashtable changedProps)
    {
        if (targetPlayer == photonView.Owner &&
            changedProps != null &&
            changedProps.ContainsKey(NetKeys.PLAYER_STATS))
        {
            ApplyFromOwnerProperties();
        }
    }

    void ApplyFromOwnerProperties()
    {
        var owner = photonView.Owner;
        if (owner == null || owner.CustomProperties == null) return;

        if (owner.CustomProperties.TryGetValue(NetKeys.PLAYER_STATS, out var st) &&
            st is string json && !string.IsNullOrEmpty(json))
        {
            var dict = MiniJson.Deserialize(json) as Dictionary<string, object>;
            if (dict != null)
            {
                spd = dict.ContainsKey("spd") ? System.Convert.ToInt32(dict["spd"]) : spd;
                pow = dict.ContainsKey("pow") ? System.Convert.ToInt32(dict["pow"]) : pow;
                defn = dict.ContainsKey("def") ? System.Convert.ToInt32(dict["def"]) : defn;
                atkspd = dict.ContainsKey("atkspd") ? System.Convert.ToInt32(dict["atkspd"]) : atkspd;
                hp = dict.ContainsKey("hp") ? System.Convert.ToInt32(dict["hp"]) : hp;
                jump = dict.ContainsKey("jump") ? System.Convert.ToSingle(dict["jump"]) : jump;

                // Hareket scriptine canlý uygula (varsa)
                var mover = GetComponent<PlayerMovementController>();
                if (mover)
                {
                    mover.moveSpeed = GetComputedMoveSpeed();
                    mover.jumpHeight = GetJumpHeight();
                }
            }
        }
    }
}
