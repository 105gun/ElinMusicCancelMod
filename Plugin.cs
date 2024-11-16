using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace MusicCancelMod;

[BepInPlugin("105gun.musiccancel.mod", "Music Cancel Mod", "1.0.0.0")]
public class Plugin : BaseUnityPlugin
{
    private void Start()
    {
        System.Console.WriteLine("Hello World from Elin Example Mod!");
        var harmony = new Harmony("105gun.musiccancel.mod");
        harmony.PatchAll();
    }
}