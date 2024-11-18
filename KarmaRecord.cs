using BepInEx;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

namespace MusicCancelMod;

public class KarmaRecordItem
{
    public int karma;
    public float time;
}

public class KarmaRecord
{
    // Using a deque to store the karma record
    // Records more than five minutes ago are considered timeout
    static LinkedList<KarmaRecordItem> record = new LinkedList<KarmaRecordItem>();

    static public void RecordSin(int a)
    {
        CheckTimeout();
        KarmaRecordItem item = new KarmaRecordItem();
        item.karma = -a;
        item.time = Time.time;
        record.AddLast(item);
    }

    static public KarmaRecordItem PopFirstSin()
    {
        CheckTimeout();
        if (record.Count > 0)
        {
            KarmaRecordItem rtn = record.First.Value;
            record.RemoveFirst();
            return rtn;
        }
        return null;
    }

    static public int GetSinCount()
    {
        CheckTimeout();
        return record.Count;
    }

    static private void CheckTimeout()
    {
        while (record.Count > 0)
        {
            if (Time.time - record.First.Value.time > 300)
            {
                record.RemoveFirst();
            }
            else
            {
                break;
            }
        }
    }
}


// Player.ModKarma
[HarmonyPatch(typeof(Player), nameof(Player.ModKarma))]
class ModKarmaPatch
{
    static void Postfix(int a)
    {
        if (a <= -1)
        {
            KarmaRecord.RecordSin(a);
        }
    }
}