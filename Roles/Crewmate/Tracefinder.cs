using AmongUs.GameOptions;
using Hazel;
using System;
using System.Collections.Generic;
using TOHE.Roles.Core;
using UnityEngine;
using static TOHE.Options;

namespace TOHE.Roles.Crewmate;

internal class Tracefinder : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 7300;
    private static bool On = false;
    public override bool IsEnable => On;
    public static bool HasEnabled => CustomRoles.Tracefinder.IsClassEnable();
    public override CustomRoles ThisRoleBase => CustomRoles.Scientist;
    //==================================================================\\

    private static OptionItem VitalsDuration;
    private static OptionItem VitalsCooldown;
    private static OptionItem ArrowDelayMin;
    private static OptionItem ArrowDelayMax;

    private static List<byte> playerIdList = [];
    public static void SetupCustomOption()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Tracefinder);
        VitalsCooldown = FloatOptionItem.Create(Id + 10, "VitalsCooldown", new(1f, 60f, 1f), 5f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        VitalsDuration = FloatOptionItem.Create(Id + 11, "VitalsDuration", new(1f, 30f, 1f), 25f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        ArrowDelayMin = FloatOptionItem.Create(Id + 12, "ArrowDelayMin", new(0f, 30f, 1f), 2f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
        ArrowDelayMax = FloatOptionItem.Create(Id + 13, "ArrowDelayMax", new(0f, 30f, 1f), 7f, TabGroup.CrewmateRoles, false)
            .SetParent(CustomRoleSpawnChances[CustomRoles.Tracefinder])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Init()
    {
        playerIdList = [];
        On = false;
    }
    public override void Add(byte playerId)
    {
        playerIdList.Add(playerId);
        On = true;

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.CheckDeadBodyOthers.Add(CheckDeadBody);
        }
    }
    public override void Remove(byte playerId)
    {
        playerIdList.Remove(playerId);

        if (AmongUsClient.Instance.AmHost)
        {
            CustomRoleManager.CheckDeadBodyOthers.Remove(CheckDeadBody);
        }
    }
    private static void SendRPC(byte playerId, bool add, Vector3 loc = new())
    {
        MessageWriter writer = AmongUsClient.Instance.StartRpcImmediately(PlayerControl.LocalPlayer.NetId, (byte)CustomRPC.SetTracefinderArrow, SendOption.Reliable, -1);
        writer.Write(playerId);
        writer.Write(add);
        if (add)
        {
            writer.Write(loc.x);
            writer.Write(loc.y);
            writer.Write(loc.z);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }
    public override void ApplyGameOptions(IGameOptions opt, byte playerid)
    {
        AURoleOptions.ScientistCooldown = VitalsCooldown.GetFloat();
        AURoleOptions.ScientistBatteryCharge = VitalsDuration.GetFloat();
    }
    public static void ReceiveRPC(MessageReader reader)
    {
        byte playerId = reader.ReadByte();
        bool add = reader.ReadBoolean();
        if (add)
            LocateArrow.Add(playerId, new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()));
        else
            LocateArrow.RemoveAllTarget(playerId);
    }

    public override void OnReportDeadBody(PlayerControl GODZILLA_VS, PlayerControl KINGKONG)
    {
        foreach (var apc in playerIdList)
        {
            LocateArrow.RemoveAllTarget(apc);
            SendRPC(apc, false);
        }
    }

    public static void CheckDeadBody(PlayerControl killer, PlayerControl target)
    {
        var pos = target.GetCustomPosition();

        float delay;
        if (ArrowDelayMax.GetFloat() < ArrowDelayMin.GetFloat()) delay = 0f;
        else delay = IRandom.Instance.Next((int)ArrowDelayMin.GetFloat(), (int)ArrowDelayMax.GetFloat() + 1);
        delay = Math.Max(delay, 0.15f);

        _ = new LateTask(() => {
            if (!GameStates.IsMeeting && GameStates.IsInTask)
            {
                foreach (var pc in playerIdList)
                {
                    var player = Utils.GetPlayerById(pc);
                    if (player == null || !player.IsAlive()) continue;
                    LocateArrow.Add(pc, target.transform.position);
                    SendRPC(pc, true, target.transform.position);
                    Utils.NotifyRoles(SpecifySeer: player);
                }
            }
        }, delay, "Get Arrow Tracefinder");
    }
    public override string GetSuffix(PlayerControl seer, PlayerControl target = null, bool isForMeeting = false)
    {
        if (!seer.Is(CustomRoles.Tracefinder)) return "";
        if (target != null && seer.PlayerId != target.PlayerId) return "";
        return Utils.ColorString(Color.white, LocateArrow.GetArrows(seer));
    }
}