using BepInEx;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;

namespace MusicCancelMod;

public enum MusicCancelState
{
    Disable,
    NormalCancel,
    PerfectCancel
};

public enum MusicCancelLogLevel
{
    None,
    Error,
    Warning,
    Info,
    Debug
};

public class CancelWindow
{
    // If Progress_Custom.progress is in this range [start, end], it will be considered as a hit
    // and the type will be returned, otherwise, it will return Disable
    private int start;
    private int end;
    private MusicCancelState type;

    public CancelWindow(int start, int end, MusicCancelState type)
    {
        this.start = start;
        this.end = end;
        this.type = type;
    }

    public MusicCancelState Check(int progress)
    {
        if (start <= progress && progress <= end)
        {
            return type;
        }
        return MusicCancelState.Disable;
    }
}

[BepInPlugin("105gun.musiccancel.mod", "Music Cancel Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    static MusicCancelLogLevel pluginLogLevel = MusicCancelLogLevel.Info;
    static List<CancelWindow> cancelWindows = new List<CancelWindow>();

    private void Start()
    {
        ModLog("Initializing");

        // Add Cancel Windows
        cancelWindows.Add(new CancelWindow(11, 11, MusicCancelState.PerfectCancel));
        cancelWindows.Add(new CancelWindow(9, 11, MusicCancelState.NormalCancel));

        // Patching Harmony
        var harmony = new Harmony("105gun.musiccancel.mod");
        harmony.PatchAll();

        ModLog("Initialization completed. Tryhard, player");
    }

    public static void ModLog(string message, MusicCancelLogLevel logLevel = MusicCancelLogLevel.Info)
    {
        if (logLevel > pluginLogLevel)
        {
            return;
        }
        switch (logLevel)
        {
            case MusicCancelLogLevel.Error:
                message = $"[MusicCancel][Error] {message}";
                break;
            case MusicCancelLogLevel.Warning:
                message = $"[MusicCancel][Warning] {message}";
                break;
            case MusicCancelLogLevel.Info:
                message = $"[MusicCancel][Info] {message}";
                break;
            case MusicCancelLogLevel.Debug:
                message = $"[MusicCancel][Debug] {message}";
                break;
            default:
                break;
        }
        System.Console.WriteLine(message);
    }

    public static MusicCancelState GetMusicCancelState(AI_PlayMusic musicInstance)
    {
        if (!(musicInstance.child is Progress_Custom) || !musicInstance.owner.IsPC)
        {
            ModLog("Music Instance is not Progress_Custom or owner is not PC", MusicCancelLogLevel.Error);
            return MusicCancelState.Disable;
        }

        Progress_Custom actionProgress = musicInstance.child as Progress_Custom;
        MusicCancelState cancelState = MusicCancelState.Disable;
        int progress = actionProgress.progress;

        foreach (var item in cancelWindows)
        {
            if (item.Check(progress) > cancelState)
            {
                cancelState = item.Check(progress);
            }
        }

        switch (cancelState)
        {
            case MusicCancelState.NormalCancel:
                // ModLog($"GetMusicCancelState: NormalCancel {progress}", MusicCancelLogLevel.Debug);
                return MusicCancelState.NormalCancel;
            case MusicCancelState.PerfectCancel:
                // ModLog($"GetMusicCancelState: PerfectCancel {progress}", MusicCancelLogLevel.Debug);
                return MusicCancelState.PerfectCancel;
        }
        return MusicCancelState.Disable;
    }
}

// AIAct.CanManualCancel
[HarmonyPatch(typeof(AIAct), nameof(AIAct.CanManualCancel))]
class AIActPatch
{
    static void Postfix(ref bool __result, AIAct __instance)
    {
        if (__instance is AI_PlayMusic && __instance.owner.IsPC)
        {
            __result = Plugin.GetMusicCancelState(__instance as AI_PlayMusic) != MusicCancelState.Disable;
        }
    }
}

// AI_PlayMusic
[HarmonyPatch(typeof(AI_PlayMusic))]
class AI_PlayMusicPatch
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(AI_PlayMusic.OnCancel))]
    static void OnCancelPrefix(AI_PlayMusic __instance)
    {
        if (!__instance.owner.IsPC)
        {
            // Not Player, skipping
            return;
        }
        MusicCancelState cancelState = Plugin.GetMusicCancelState(__instance);
        switch (cancelState)
        {
            case MusicCancelState.NormalCancel:
                Plugin.ModLog($"AI_PlayMusic.OnCancel: NormalCancel", MusicCancelLogLevel.Debug);
                Msg.SayRaw("Good Cancel! ");

                // score will /= 2 in origin method
                __instance.score *= 2;
                __instance.score += 10;

                CancelExtraReward(__instance, 7, 1.0f, 1);
                break;
            case MusicCancelState.PerfectCancel:
                Plugin.ModLog($"AI_PlayMusic.OnCancel: PerfectCancel", MusicCancelLogLevel.Debug);
                Msg.SayRaw("Perfect Cancel!! ");

                // score will /= 2 in origin method
                __instance.score *= 3;
                __instance.score += 50;

                // stamina reward
                __instance.owner.stamina.Mod(EClass.rnd(2));

                CancelExtraReward(__instance, Mathf.Clamp(__instance.owner.Evalue(241), 7, 50), 1.0f, 1);
                break;
            case MusicCancelState.Disable:
                Plugin.ModLog($"AI_PlayMusic.OnCancel: Disable", MusicCancelLogLevel.Debug);
                break;
        }
    }

    static void CancelExtraReward(AI_PlayMusic __instance, float radius, float skillMultiplier = 1, int punishRate = 5)
    {
        // Porting from origin method
        List<Chara> list = EClass._map.ListCharasInCircle(__instance.owner.pos, radius, true);
        int skill = (int)(__instance.owner.Evalue(241) * (100 + __instance.toolLv) / 100 * skillMultiplier);
        foreach (Chara chara2 in list)
        {
            if (__instance.owner == null)
            {
                break;
            }
            // We are using ListCharasInCircle, so exclude PC
            if (chara2.IsPC)
            {
                continue;
            }

            chara2.interest += EClass.rnd(5);
            if (EClass.rnd(5) == 0)
            {
                bool isMinion = chara2.IsMinion;
                if (skill < chara2.LV && EClass.rnd(2) != 0)
                {
                    if (!isMinion)
                    {
                        // __instance.score -= chara2.LV / 2 - 10;
                    }
                    if (EClass.rnd(2) == 0)
                    {
                        chara2.Talk("musicBad", null, null, false);
                    }
                    else
                    {
                        chara2.Say("musicBad", chara2, __instance.owner, null, null);
                    }
                    chara2.ShowEmo(Emo.sad, 0f, true);
                    __instance.owner.elements.ModExp(241, 5, false);
                    if (EClass.rnd(punishRate) == 0)
                    {
                        __instance.ThrowReward(chara2, true);
                    }
                }
                else if (EClass.rnd(skill + 5) > EClass.rnd(chara2.LV * 5 + 1))
                {
                    if (!isMinion)
                    {
                        __instance.score += EClass.rnd(chara2.LV / 2 + 5) + 5;
                    }
                    if (EClass.rnd(2) == 0)
                    {
                        chara2.Talk("musicGood", null, null, false);
                    }
                    else
                    {
                        chara2.Say("musicGood", chara2, __instance.owner, null, null);
                    }
                    chara2.ShowEmo(Emo.happy, 0f, true);
                    chara2.renderer.PlayAnime((EClass.rnd(2) == 0) ? AnimeID.Jump : AnimeID.Fishing, default(Vector3), false);
                    __instance.owner.elements.ModExp(241, 10, false);
                    if (!isMinion)
                    {
                        __instance.ThrowReward(chara2, false);
                    }
                }
            }
        }
    }
}