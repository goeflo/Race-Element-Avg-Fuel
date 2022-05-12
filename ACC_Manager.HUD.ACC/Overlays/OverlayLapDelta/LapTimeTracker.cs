﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACCManager.HUD.ACC.Overlays.OverlayLapDelta
{
    internal class LapTimingData
    {
        public int Index { get; set; } = -1;
        public int Time { get; set; } = -1;
        public bool IsValid { get; set; } = true;
        public int Sector1 { get; set; } = -1;
        public int Sector2 { get; set; } = -1;
        public int Sector3 { get; set; } = -1;
    }

    internal class LapTimeTracker
    {
        private static LapTimeTracker _instance;
        public static LapTimeTracker Instance
        {
            get
            {
                if (_instance == null) _instance = new LapTimeTracker();
                return _instance;
            }
        }

        private bool IsCollecting = false;
        private ACCSharedMemory sharedMemory;
        private int CurrentSector = 0;

        internal List<LapTimingData> LapTimeDatas = new List<LapTimingData>();
        internal LapTimingData CurrentLap;

        public event EventHandler<LapTimingData> LapFinished;

        private LapTimeTracker()
        {
            sharedMemory = new ACCSharedMemory();
            CurrentLap = new LapTimingData();

            this.Start();
        }

        private void Start()
        {
            IsCollecting = true;
            new Thread(x =>
            {
                while (IsCollecting)
                {
                    Thread.Sleep(1000 / 10);

                    var pageGraphics = sharedMemory.ReadGraphicsPageFile();

                    if (CurrentLap.IsValid != pageGraphics.IsValidLap)
                    {
                        CurrentLap.IsValid = pageGraphics.IsValidLap;
                        Debug.WriteLine("Invalidated current lap");
                    }

                    if (CurrentSector != pageGraphics.CurrentSectorIndex)
                    {
                        if (CurrentLap.Sector1 == -1 && CurrentSector != 0)
                        {
                            Debug.WriteLine($"Not sector 1 {CurrentSector}");
                        }
                        else
                            switch (pageGraphics.CurrentSectorIndex)
                            {
                                case 1: CurrentLap.Sector1 = pageGraphics.LastSectorTime; break;
                                case 2: CurrentLap.Sector2 = pageGraphics.LastSectorTime - CurrentLap.Sector1; break;
                                case 0: CurrentLap.Sector3 = pageGraphics.LastTimeMs - CurrentLap.Sector2 - CurrentLap.Sector1; break;
                            }

                        CurrentSector = pageGraphics.CurrentSectorIndex;
                        Debug.WriteLine("collected sector time");
                    }

                    if (CurrentLap.Index != pageGraphics.CompletedLaps && pageGraphics.LastTimeMs != int.MaxValue)
                    {
                        CurrentLap.Time = pageGraphics.LastTimeMs;
                        CurrentLap.Index = pageGraphics.CompletedLaps - 1;

                        if (CurrentLap.Sector1 != -1)
                        {
                            LapTimeDatas.Add(CurrentLap);
                            LapFinished?.Invoke(this, CurrentLap);
                        }

                        CurrentLap = new LapTimingData() { Index = pageGraphics.CompletedLaps };
                    }
                }
            }).Start();
        }

        internal void Stop()
        {
            IsCollecting = false;
        }
    }


}