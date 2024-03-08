﻿using System;
using System.Linq;
using TOHE.Modules;
using TOHE.Roles.AddOns.Common;
using TOHE.Roles.Core;
using static TOHE.Options;
using static TOHE.Translator;

namespace TOHE.Roles.Crewmate;

internal class Randomizer : RoleBase
{
    //===========================SETUP================================\\
    private const int Id = 7500;
    private static bool On = false;
    public override bool IsEnable => On;
    public static bool HasEnabled => CustomRoles.Randomizer.IsClassEnable();
    public override CustomRoles ThisRoleBase => CustomRoles.Crewmate;
    //==================================================================\\

    public static OptionItem BecomeBaitDelayNotify;
    public static OptionItem BecomeBaitDelayMin;
    public static OptionItem BecomeBaitDelayMax;
    public static OptionItem BecomeTrapperBlockMoveTime;

    public static void SetupCustomOptions()
    {
        SetupRoleOptions(Id, TabGroup.CrewmateRoles, CustomRoles.Randomizer);
        BecomeBaitDelayNotify = BooleanOptionItem.Create(Id + 10, "BecomeBaitDelayNotify", false, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Randomizer]);
        BecomeBaitDelayMin = FloatOptionItem.Create(Id + 11, "BaitDelayMin", new(0f, 5f, 1f), 0f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);
        BecomeBaitDelayMax = FloatOptionItem.Create(Id +12, "BaitDelayMax", new(0f, 10f, 1f), 0f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);
        BecomeTrapperBlockMoveTime = FloatOptionItem.Create(Id + 13, "BecomeTrapperBlockMoveTime", new(1f, 180f, 1f), 5f, TabGroup.CrewmateRoles, false).SetParent(CustomRoleSpawnChances[CustomRoles.Randomizer])
            .SetValueFormat(OptionFormat.Seconds);
    }
    public override void Init()
    {
        On = false;
    }
    public override void Add(byte playerId)
    {
        On = true;
    }
    public override bool OnCheckMurderAsTarget(PlayerControl killer, PlayerControl target)
    {
        var Fg = IRandom.Instance;
        int Randomizer = Fg.Next(1, 5);
        if (Randomizer == 1)
        {
            if (killer.PlayerId != target.PlayerId || (target.GetRealKiller()?.GetCustomRole() is CustomRoles.Swooper or CustomRoles.Wraith) || !killer.Is(CustomRoles.Oblivious) || (killer.Is(CustomRoles.Oblivious) && !Oblivious.ObliviousBaitImmune.GetBool()))
            {
                killer.RPCPlayCustomSound("Congrats");
                target.RPCPlayCustomSound("Congrats");

                float delay;
                if (BecomeBaitDelayMax.GetFloat() < BecomeBaitDelayMin.GetFloat())
                {
                    delay = 0f;
                }
                else
                {
                    delay = IRandom.Instance.Next((int)BecomeBaitDelayMin.GetFloat(), (int)BecomeBaitDelayMax.GetFloat() + 1);
                }
                delay = Math.Max(delay, 0.15f);
                if (delay > 0.15f && BecomeBaitDelayNotify.GetBool())
                {
                    killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Bait), string.Format(GetString("KillBaitNotify"), (int)delay)), delay);
                }

                Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发自动报告 => {target.GetNameWithRole()}", "Randomizer");

                killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), GetString("YouKillRandomizer1")));

                _ = new LateTask(() =>
                {
                    if (GameStates.IsInTask) killer.CmdReportDeadBody(target.Data);
                }, delay, "Bait Self Report");
            }
        }
        else if (Randomizer == 2)
        {
            Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发暂时无法移动 => {target.GetNameWithRole()}", "Randomizer");

            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), GetString("YouKillRandomizer2")));
            var tmpSpeed = Main.AllPlayerSpeed[killer.PlayerId];
            Main.AllPlayerSpeed[killer.PlayerId] = Main.MinSpeed;
            ReportDeadBodyPatch.CanReport[killer.PlayerId] = false;
            killer.MarkDirtySettings();

            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[killer.PlayerId] = Main.AllPlayerSpeed[killer.PlayerId] - Main.MinSpeed + tmpSpeed;
                ReportDeadBodyPatch.CanReport[killer.PlayerId] = true;
                killer.MarkDirtySettings();
                RPC.PlaySoundRPC(killer.PlayerId, Sounds.TaskComplete);
            }, BecomeTrapperBlockMoveTime.GetFloat(), "Trapper BlockMove");
        }
        else if (Randomizer == 3)
        {
            Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发凶手CD变成600 => {target.GetNameWithRole()}", "Randomizer");
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), GetString("YouKillRandomizer3")));
            Main.AllPlayerKillCooldown[killer.PlayerId] = 600f;
            killer.SyncSettings();
        }
        else if (Randomizer == 4)
        {
            Logger.Info($"{killer.GetNameWithRole()} 击杀了萧暮触发随机复仇 => {target.GetNameWithRole()}", "Randomizer");
            killer.Notify(Utils.ColorString(Utils.GetRoleColor(CustomRoles.Randomizer), GetString("YouKillRandomizer4")));
            {
                var pcList = Main.AllAlivePlayerControls.Where(x => x.PlayerId != target.PlayerId).ToList();
                var rp = pcList[IRandom.Instance.Next(0, pcList.Count)];
                if (!rp.Is(CustomRoles.Pestilence))
                {
                    Main.PlayerStates[rp.PlayerId].deathReason = PlayerState.DeathReason.Revenge;
                    rp.SetRealKiller(target);
                    rp.RpcMurderPlayerV3(rp);
                }
            }
        }
        return true;
    }
}
