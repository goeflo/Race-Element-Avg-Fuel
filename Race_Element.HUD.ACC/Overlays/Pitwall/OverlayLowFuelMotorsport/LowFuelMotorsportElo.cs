﻿using System;
using System.Collections.Generic;

namespace RaceElement.HUD.ACC.Overlays.Pitwall.LowFuelMotorsport.API;

public class LowFuelMotorsportElo
{
    static readonly float LN2 = 0.69314718056f;
    static readonly float E   = 2.71828182845f;

    private RaceInfo _raceInfo;
    private float _magic;

    public LowFuelMotorsportElo(RaceInfo raceInfo)
    {
        _raceInfo = raceInfo;
        _magic = ComputeMagic(_raceInfo.entries);
    }

    public int GetPositionThreshold()
    {
        return (int)Math.Floor(_raceInfo.entries.Count - _magic);
    }

    public int GetElo(int position)
    {
        float elo = (_raceInfo.entries.Count - position - _magic - ((_raceInfo.entries.Count / 2.0f) - position) / 100.0f) * 200.0f / _raceInfo.entries.Count * _raceInfo.kFactor;
        return (int)Math.Round(elo);
    }

    public int GetCarNumber()
    {
        SplitEntry player = _raceInfo.entries.Find(x => x.IsPlayer);
        return player.RaceNumber;
    }

    private float ComputeMagic(int selfElo, int otherElo)
    {
        float e = (1600.0f / LN2);
        float youExp   = (-selfElo / e);
        float otherExp = (-otherElo / e);

        double you   = Math.Pow(E, youExp);
        double other = Math.Pow(E, otherExp);

        double result = ((1 - you) * other)/((1 - other) * you + (1 - you) * other);
        return (float)result;
    }

    private float ComputeMagic(List<SplitEntry> entries)
    {
        float magic = 0;
        SplitEntry player = entries.Find(x => x.IsPlayer);

        foreach (SplitEntry e in entries)
        {
            if (e.IsPlayer) continue;
            magic += ComputeMagic(player.Elo, e.Elo);
        }

        return magic;
    }
}
