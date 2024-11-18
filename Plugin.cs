using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
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
    public MusicCancelState type;
    private UnityAction<AI_PlayMusic> action;

    public CancelWindow(int start, int end, MusicCancelState type)
    {
        this.start = start;
        this.end = end;
        this.type = type;
        this.action = null;
    }

    public CancelWindow(int start, int end, MusicCancelState type, UnityAction<AI_PlayMusic> action)
    {
        this.start = start;
        this.end = end;
        this.type = type;
        this.action = action;
    }

    public MusicCancelState Check(int progress)
    {
        if (start <= progress && progress <= end)
        {
            return type;
        }
        return MusicCancelState.Disable;
    }

    public void Do(AI_PlayMusic __instance)
    {
        if (!__instance.owner.IsPC)
        {
            Plugin.ModLog("AI_PlayMusic.OnCancel: Not PC, skipping. This should never happen!", MusicCancelLogLevel.Warning);
            return;
        }

        switch (type)
        {
            case MusicCancelState.NormalCancel:
                Plugin.ModLog($"AI_PlayMusic.OnCancel: NormalCancel", MusicCancelLogLevel.Debug);
                EClass.player.forceTalk = true;
                //__instance.owner.KillAnime();
                __instance.owner.Talk("fishing_Good", null, null, false);
                Msg.SayRaw("Good Cancel! ");
                break;
            case MusicCancelState.PerfectCancel:
                Plugin.ModLog($"AI_PlayMusic.OnCancel: PerfectCancel", MusicCancelLogLevel.Debug);
                EClass.player.forceTalk = true;
                //__instance.owner.KillAnime();
                __instance.owner.Talk("tail_after", null, null, false);
                Msg.SayRaw("Perfect Cancel!! ");
                break;
            default:
                return;
        }

        if (action != null)
        {
            action(__instance);
        }
        else
        {
            if (type == MusicCancelState.NormalCancel)
            {
                Plugin.normalCancelStandardAction(__instance);
            }
            if (type == MusicCancelState.PerfectCancel)
            {
                Plugin.perfectCancelStandardAction(__instance);
            }
        }
    }
}

[BepInPlugin("105gun.musiccancel.mod", "Music Cancel Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    static MusicCancelLogLevel pluginLogLevel = MusicCancelLogLevel.Info;
    static KarmaRecord karmaRecord = new KarmaRecord();
    static Dictionary<string, List<CancelWindow>> cancelWindowsDict = new Dictionary<string, List<CancelWindow>>();
    static CancelWindow nullCancelWindow = new CancelWindow(0, 0, MusicCancelState.Disable);

    public static UnityAction<AI_PlayMusic> normalCancelStandardAction;
    public static UnityAction<AI_PlayMusic> perfectCancelStandardAction;

    private void Start()
    {
        ModLog("Initializing");
        InitCancelWindows();

        // Patching Harmony
        var harmony = new Harmony("105gun.musiccancel.mod");
        harmony.PatchAll();

        ModLog("Initialization completed. Tryhard, player");
    }

    private void InitCancelWindows()
    {
        // Init standard actions
        normalCancelStandardAction = (__instance) =>
        {
            // score will /= 2 in origin method
            __instance.score *= 2;
            __instance.score += 10;

            AI_PlayMusicPatch.CancelExtraReward(__instance, 7, 1.0f, 1);
        };
        perfectCancelStandardAction = (__instance) =>
        {
            // score will /= 2 in origin method
            __instance.score *= 3;
            __instance.score += 50;

            // stamina reward
            __instance.owner.stamina.Mod(EClass.rnd(3));

            AI_PlayMusicPatch.CancelExtraReward(__instance, Mathf.Clamp(__instance.owner.Evalue(241), 7, 50), 1.0f, 1);
        };

        // Init Cancel Windows
        cancelWindowsDict.Clear();

        // [default]
        List<CancelWindow> defaultCancelWindows = new List<CancelWindow>();
        defaultCancelWindows.Add(new CancelWindow(11, 11, MusicCancelState.PerfectCancel));
        defaultCancelWindows.Add(new CancelWindow(9, 11, MusicCancelState.NormalCancel));
        cancelWindowsDict.Add("default", defaultCancelWindows);

        // [mokugyo]
        List<CancelWindow> mokugyoCancelWindows = new List<CancelWindow>();
        mokugyoCancelWindows.Add(new CancelWindow(8, 9, MusicCancelState.PerfectCancel, (__instance) =>
        {
            // [Atone]
            // Each sin happened in 5 mins has a chance to restore 1 karma, no matter how many karmas was lost
            KarmaRecordItem karmaRecordItem = KarmaRecord.PopFirstSin();
            if (karmaRecordItem != null)
            {
                // Yeeeart!
                EClass.player.ModKarma(1);
                Msg.SayRaw($"{KarmaRecord.GetSinCount()} more Karmas can be atoned... ");
            }

            // [Confession]
            // Has a 10% chance to restore random karma depends on the player's Faith skill
            if (EClass.rnd(10) == 0)
            {
                // Sayonara!
                int karma = 1 + EClass.rnd(__instance.owner.Evalue(306) / 10); 
                EClass.player.ModKarma(karma);
            }
        }));
        cancelWindowsDict.Add("mokugyo", mokugyoCancelWindows);

        // [panty]
        List<CancelWindow> pantyCancelWindows = new List<CancelWindow>();
        pantyCancelWindows.Add(new CancelWindow(11, 11, MusicCancelState.PerfectCancel, (__instance) =>
        {
            // [Dreambug spawn]
            // Has a chance to spawn a dreambug, depends on the player's Music skill
            if (EClass.rnd(100) < Mathf.Clamp(__instance.owner.Evalue(241) / 4, 0, 25))
            {
                __instance.owner.AddThing(ThingGen.Create("dreambug", -1, -1), true, -1, -1);
            }
            perfectCancelStandardAction(__instance);
        }));
        pantyCancelWindows.Add(new CancelWindow(9, 13, MusicCancelState.NormalCancel, (__instance) =>
        {
            // [Doki Doki]
            // Porting from SuccubusVisit
            List<Chara> tempVisitedCharas = new List<Chara>();
            List<Chara> totalCharas = ShuffleList(EClass._map.ListCharasInCircle(__instance.owner.pos, 15, true));
            foreach (Chara target in totalCharas)
            {
                if (tempVisitedCharas.Contains(target))
                {
                    continue;
                }
                foreach (Chara chara in totalCharas)
                {
                    if (tempVisitedCharas.Contains(chara))
                    {
                        continue;
                    }
                    if (chara != target && !chara.IsPC && EClass.rnd(3) == 0 && !chara.IsDisabled && chara.IsIdle && chara.host == null)
                    {
                        chara.Teleport(target.pos, false, false);
                        if (chara.Dist(target.pos) <= 2)
                        {
                            chara.SetAI(new AI_Fuck
                            {
                                target = target,
                                succubus = true
                            });

                            tempVisitedCharas.Add(chara);
                            tempVisitedCharas.Add(target);
                            break;
                        }
                    }
                }
            }
            normalCancelStandardAction(__instance);
        }));
        cancelWindowsDict.Add("panty", pantyCancelWindows);

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
        GetMusicCancelWindow(musicInstance, out MusicCancelState musicCancelState);
        return musicCancelState;
    }

    public static CancelWindow GetMusicCancelWindow(AI_PlayMusic musicInstance, out MusicCancelState musicCancelState)
    {
        if (!(musicInstance.child is Progress_Custom) || !musicInstance.owner.IsPC)
        {
            ModLog("Music Instance is not Progress_Custom or owner is not PC", MusicCancelLogLevel.Error);
            musicCancelState = MusicCancelState.Disable;
            return nullCancelWindow;
        }

        Progress_Custom actionProgress = musicInstance.child as Progress_Custom;
        MusicCancelState cancelState = MusicCancelState.Disable;
        CancelWindow cancelWindow = null;
        List<CancelWindow> cancelWindowList = null;
        int progress = actionProgress.progress;

        ModLog($"Progress: {progress} Item id: {musicInstance.tool.id}", MusicCancelLogLevel.Debug);
        if (cancelWindowsDict.ContainsKey(musicInstance.tool.id))
        {
            cancelWindowList = cancelWindowsDict[musicInstance.tool.id];
        }
        else
        {
            cancelWindowList = cancelWindowsDict["default"];
        }

        foreach (var item in cancelWindowList)
        {
            if (item.Check(progress) > cancelState)
            {
                cancelState = item.Check(progress);
                cancelWindow = item;
            }
        }

        if (cancelWindow == null)
        {
            cancelWindow = nullCancelWindow;
        }
        musicCancelState = cancelState;
        return cancelWindow;
    }

    public static List<Chara> ShuffleList(List<Chara> list)
    {
        int n = list.Count;
        System.Random rng = new System.Random();

        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            Chara value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
        return list;
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

        CancelWindow cancelWindow = Plugin.GetMusicCancelWindow(__instance, out MusicCancelState cancelState);
        cancelWindow.Do(__instance);
    }

    public static void CancelExtraReward(AI_PlayMusic __instance, float radius, float skillMultiplier = 1, int punishRate = 5)
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