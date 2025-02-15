﻿using System.Diagnostics;

namespace RaceElement.Data.Games;

[Flags]
public enum Game : int
{
    Any = (1 << 0),
    AssettoCorsa1 = (1 << 1),
    AssettoCorsaCompetizione = (1 << 2),
    iRacing = (1 << 3),
    RaceRoom = (1 << 4),
    Automobilista2 = (1 << 5),
    EuroTruckSimulator2 = (1 << 6),
    AmericanTruckSimulator = (1 << 7),
    //rFactor2,
    // LMU,
}

public static class GameExtensions
{
    private static class FriendlyNames
    {
        public const string AssettoCorsaCompetizione = "Assetto Corsa Competizione";
        public const string AssettoCorsa = "Assetto Corsa";
        public const string IRacing = "iRacing";
        public const string RaceRoom = "RaceRoom Racing Experience";
        public const string Automobilista2 = "Automobilista 2";
        public const string EuroTruckSimulator2 = "Euro Truck Simulator 2";
        public const string AmericanTruckSimulator = "American Truck Simulator";
    }

    private static class ShortNames
    {
        public const string AssettoCorsaCompetizione = "ACC";
        public const string AssettoCorsa = "AC";
        public const string IRacing = "iRacing";
        public const string RaceRoom = "RaceRoom";
        public const string Automobilista2 = "AMS2";
        public const string EuroTruckSimulator2 = "ETS2";
        public const string AmericanTruckSimulator = "ATS";
    }

    private static class ExeNames
    {
        public static readonly string[] All = [AssettoCorsa, AssettoCorsaCompetizione, IRacing, RaceRoom, RaceRoomX64, Automobilista2, EuroTruckSimulator2, AmericanTruckSimulator];

        public const string AssettoCorsaCompetizione = "AC2-Win64-Shipping";
        public const string AssettoCorsa = "acs";
        public const string IRacing = "iRacingSim64DX11";
        public const string RaceRoomX64 = "RRRE64";
        public const string RaceRoom = "RRRE";
        public const string Automobilista2 = "AMS2AVX";
        public const string EuroTruckSimulator2 = "eurotrucks2";
        public const string AmericanTruckSimulator = "amtrucks";
    }

    public static Game GameFromProcessName(string processName) => processName switch
    {
        ExeNames.AssettoCorsa => Game.AssettoCorsa1,
        ExeNames.AssettoCorsaCompetizione => Game.AssettoCorsaCompetizione,
        ExeNames.Automobilista2 => Game.Automobilista2,
        ExeNames.IRacing => Game.iRacing,
        ExeNames.RaceRoom => Game.RaceRoom,
        ExeNames.RaceRoomX64 => Game.RaceRoom,
        ExeNames.EuroTruckSimulator2 => Game.EuroTruckSimulator2,
        ExeNames.AmericanTruckSimulator => Game.AmericanTruckSimulator,
        _ => Game.Any,
    };

    public static string ToFriendlyName(this Game game) => game switch
    {
        Game.AssettoCorsa1 => FriendlyNames.AssettoCorsa,
        Game.AssettoCorsaCompetizione => FriendlyNames.AssettoCorsaCompetizione,
        Game.iRacing => FriendlyNames.IRacing,
        Game.RaceRoom => FriendlyNames.RaceRoom,
        Game.Automobilista2 => FriendlyNames.Automobilista2,
        Game.EuroTruckSimulator2 => FriendlyNames.EuroTruckSimulator2,
        Game.AmericanTruckSimulator => FriendlyNames.AmericanTruckSimulator,
        _ => string.Empty
    };

    public static string ToShortName(this Game game) => game switch
    {
        Game.AssettoCorsa1 => ShortNames.AssettoCorsa,
        Game.AssettoCorsaCompetizione => ShortNames.AssettoCorsaCompetizione,
        Game.iRacing => ShortNames.IRacing,
        Game.RaceRoom => ShortNames.RaceRoom,
        Game.Automobilista2 => ShortNames.Automobilista2,
        Game.EuroTruckSimulator2 => ShortNames.EuroTruckSimulator2,
        Game.AmericanTruckSimulator => ShortNames.AmericanTruckSimulator,
        _ => string.Empty
    };

    public static Game ToGame(this string friendlyName) => friendlyName switch
    {
        FriendlyNames.AssettoCorsa => Game.AssettoCorsa1,
        FriendlyNames.AssettoCorsaCompetizione => Game.AssettoCorsaCompetizione,
        FriendlyNames.IRacing => Game.iRacing,
        FriendlyNames.RaceRoom => Game.RaceRoom,
        FriendlyNames.Automobilista2 => Game.Automobilista2,
        FriendlyNames.EuroTruckSimulator2 => Game.EuroTruckSimulator2,
        FriendlyNames.AmericanTruckSimulator => Game.AmericanTruckSimulator,
        _ => Game.AssettoCorsaCompetizione,
    };

    public static int GetSteamID(this Game game) => game switch
    {
        Game.AssettoCorsa1 => 244210,
        Game.AssettoCorsaCompetizione => 805550,
        Game.iRacing => 266410,
        Game.RaceRoom => 211500,
        Game.Automobilista2 => 1066890,
        Game.EuroTruckSimulator2 => 227300,
        Game.AmericanTruckSimulator => 270880,
        _ => -1
    };

    public static Stream? GetSteamLogo(this Game game)
    {
        var enumAssembly = typeof(Game).Assembly;

        var resourceNames = enumAssembly.GetManifestResourceNames();
        var found = resourceNames.FirstOrDefault(x => x.EndsWith($"Logos.{game.ToShortName()}.jpg"));
        if (found == null) return null;

        return enumAssembly.GetManifestResourceStream(found);
    }

    public static Stream? GetGameClientIcon(this Game game)
    {
        var enumAssembly = typeof(Game).Assembly;

        var resourceNames = enumAssembly.GetManifestResourceNames();
        var found = resourceNames.FirstOrDefault(x => x.EndsWith($"Icons.{game.ToShortName()}.ico"));
        if (found == null) return null;

        return enumAssembly.GetManifestResourceStream(found);
    }

    public static Game GetRunningGame()
    {
        var processes = Process.GetProcesses();

        foreach (string exeName in ExeNames.All)
        {
            using Process? process = processes.FirstOrDefault(x => x.ProcessName == exeName);
            if (process != null)
                return GameFromProcessName(process.ProcessName);
        }

        return Game.Any;
    }
}
