﻿using RaceElement.Data.Common.SimulatorData;
using RaceElement.Data.Games.iRacing.SDK;
using System.Diagnostics;
using static RaceElement.Data.Games.iRacing.SDK.IRacingSdkSessionInfo.DriverInfoModel;
using RaceElement.Data.Common;
using System.Drawing;
using static RaceElement.Data.Games.iRacing.SDK.IRacingSdkEnum;
using System.Numerics;

// https://github.com/mherbold/IRSDKSharper
// https://sajax.github.io/irsdkdocs/telemetry/
// https://members-login.iracing.com/?ref=https%3A%2F%2Fmembers-ng.iracing.com%2Fdata%2Fdoc&signout=true (access to results data from iRacing.com. needs credentials)
// https://us.v-cdn.net/6034148/uploads/8DD84H30FIC8/telemetry-11-23-15.pdf Official doc
namespace RaceElement.Data.Games.iRacing
{
    public class IRacingDataProvider : AbstractSimDataProvider
    {
        Dictionary<string, Color> carClassColor = [];
        HashSet<string> carClasses = null;

        bool hasTelemetry = false;

        private IRSDKSharper _iRacingSDK;
        private int lastSessionNumber = -1;
       
        private int lastLapIndex = 0;
        private float lastLapFuelLevelLiters = 0;
        private float lastLapFuelConsumption = 0;

        public CarLeftRight SpotterCallout { get; private set; }

        public IRacingDataProvider() {                    
            if (_iRacingSDK == null)
            {
                _iRacingSDK = new IRSDKSharper
                {
                    UpdateInterval = 1, // update every 1/60 second                    
                };
                _iRacingSDK.OnTelemetryData += OnTelemetryData;
                _iRacingSDK.OnSessionInfo += OnSessionInfo;
                _iRacingSDK.OnStopped += OnStopped;

                _iRacingSDK.Start();
            }                
        }

        private void OnStopped()
        {
            hasTelemetry = false;
        }

        /// <summary>
        /// Handle update of telemetry. That means update the data that can be retrieved with calls to _iRacingSDK.Data.GetXXX 
        /// (telemetry as opposed to the session data updated below in OnSessionInfo)
        /// </summary>
        /// The telemetry variables are documented here: https://sajax.github.io/irsdkdocs/telemetry/
        private void OnTelemetryData()
        {         
           if (!_iRacingSDK.IsConnected && _iRacingSDK.IsStarted) {
            return;
           }
           if (_iRacingSDK.Data.SessionInfo == null)
           { 
            Debug.WriteLine("No session info");
                return;
           }

           if (SessionData.Instance.Cars.Count == 0 || _iRacingSDK.Data.SessionInfo.DriverInfo == null) {
            Debug.WriteLine("No SessionData.Instance.Cars or DriverInfo");
            return;
           };
            
            try
            {
                // for each class, the time to get to the track position for the leader in that class
                Dictionary<string, float> classLeaderTrackPositionTimeDict = new Dictionary<string, float>();
                
                for (var index = 0; index < SessionData.Instance.Cars.Count; index++)
                {                                    
                    CarInfo carInfo = SessionData.Instance.Cars[index].Value;
                    carInfo.Position = _iRacingSDK.Data.GetInt("CarIdxPosition", index);
                    carInfo.CupPosition = _iRacingSDK.Data.GetInt("CarIdxClassPosition", index);
                    
                    carInfo.TrackPercentCompleted = _iRacingSDK.Data.GetFloat("CarIdxLapDistPct", index);

                    TrkLoc trackSurface = (TrkLoc)_iRacingSDK.Data.GetInt("CarIdxTrackSurface", index);
                    // TODO: more finer mapping for CarLocation? Figure out how to detect whether the player is in the garage or pit.
                    // And allow HUDs to check if players are off track/approaching pits
                    // PlayerCarInPitStall
                    /* CarIdxTrackSurface irsdk_TrkLoc :
                        NotInWorld = -1,
                        OffTrack,
                        InPitStall,
                        AproachingPits,
                        OnTrack
                    */
                    /* if (index == SessionData.Instance.PlayerCarIndex)
                    {
                        Debug.WriteLine("CarIdxOnPitRoad {0} garage {1} PlayerCarInPitStall {2} IsGarageVisible {3} IsSpectator {4} CarIdxTrackSurface {5} CamCarIdx {6}", 
                            _iRacingSDK.Data.GetBool("CarIdxOnPitRoad", index), _iRacingSDK.Data.GetBool("IsInGarage", index), 
                            _iRacingSDK.Data.GetBool("PlayerCarInPitStall", index), _iRacingSDK.Data.GetBool("IsGarageVisible", index),
                            _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers[index].IsSpectator, trackSurface,
                            _iRacingSDK.Data.GetInt("CamCarIdx"));
                    } */
                    if (_iRacingSDK.Data.GetBool("CarIdxOnPitRoad", index)) {
                        carInfo.CarLocation = CarInfo.CarLocationEnum.Pitlane;
                    } else
                    {
                        carInfo.CarLocation = CarInfo.CarLocationEnum.Track;                        
                    }                                        
                    
                    carInfo.CurrentDriverIndex = 0;
                    LapInfo lapInfo = new LapInfo();
                    lapInfo.LaptimeMS = (int)(_iRacingSDK.Data.GetFloat("CarIdxLastLapTime", index) * 1000.0);                    
                    carInfo.LastLap = lapInfo;
                    
                    carInfo.LapIndex = _iRacingSDK.Data.GetInt("CarIdxLap", index);

                    lapInfo = new LapInfo();
                    float fl = _iRacingSDK.Data.GetFloat("CarIdxBestLapTime", index);
                    lapInfo.LaptimeMS = (int) (_iRacingSDK.Data.GetFloat("CarIdxBestLapTime", index) * 1000.0);
                    carInfo.FastestLap = lapInfo;

                    lapInfo = new LapInfo();
                    carInfo.CurrentLap = lapInfo;
                    if (trackSurface == TrkLoc.OffTrack)
                    {
                        // TODO: we need to reset this for other cars. Right now we only do so for player's car
                        carInfo.CurrentLap.IsInvalid = true;                         
                    }

                    // "CarIdxF2Time: Race time behind leader or fastest lap time otherwise"
                    // "CarIdxEstTime":  Estimated time to reach current location on track
                    //    f2time is 0 until the driver has done a (valid?) lap. So we use CarIdxEstTime to get the time it should take a player
                    //    to get to the current position on a track
                    float trackPositionTime = _iRacingSDK.Data.GetFloat("CarIdxEstTime", index);
                    if (!classLeaderTrackPositionTimeDict.ContainsKey(carInfo.CarClass) || classLeaderTrackPositionTimeDict[carInfo.CarClass] > trackPositionTime)
                    {
                        classLeaderTrackPositionTimeDict[carInfo.CarClass] = trackPositionTime;
                    }
                    carInfo.GapToRaceLeaderMs = (int)(trackPositionTime * 1000.0);                                        
                    
                    carInfo.GapToPlayerMs = GetGapToPlayerMs(index, SessionData.Instance.PlayerCarIndex);
                }

                // determine the gaps for each car to the class leader
                for (var index = 0; index < SessionData.Instance.Cars.Count; index++)
                {
                    var position = _iRacingSDK.Data.GetInt("CarIdxClassPosition", index);
                    float f2Time = _iRacingSDK.Data.GetFloat("CarIdxF2Time", index);
                    if (position <= 0) continue;
                    
                    CarInfo carInfo = SessionData.Instance.Cars[index].Value;
                    carInfo.GapToClassLeaderMs = 0;
                    // special case for multi-class qualifying
                    if (classLeaderTrackPositionTimeDict.ContainsKey(carInfo.CarClass))
                    {
                        carInfo.GapToClassLeaderMs = (int)((classLeaderTrackPositionTimeDict[carInfo.CarClass] - f2Time) * 1000.0);
                    }
                }

                // DEBUG PrintAllCarInfo();

                // fill player's car from telemetry
                LocalCarData localCar = SimDataProvider.LocalCar;
                int playerCarIdx = _iRacingSDK.Data.SessionInfo.DriverInfo.DriverCarIdx;
                CarInfo playerCarInfo = SessionData.Instance.Cars[playerCarIdx].Value;
                localCar.Race.GlobalPosition = _iRacingSDK.Data.GetInt("PlayerCarPosition");
                
                localCar.Engine.FuelLiters = _iRacingSDK.Data.GetFloat("FuelLevel");
                int lapIndex = playerCarInfo.LapIndex;
                // check if we completed a lap
                if ( lapIndex > lastLapIndex)
                {
                    lastLapFuelConsumption = lastLapFuelLevelLiters - localCar.Engine.FuelLiters;
                    lastLapFuelLevelLiters = localCar.Engine.FuelLiters;
                    playerCarInfo.CurrentLap.IsInvalid = false;
                    lastLapIndex = lapIndex;
                    Debug.WriteLine("new lap lastLapFuelConsumption {0} lastLapFuelLevelLiters {1}", lastLapFuelConsumption, lastLapFuelLevelLiters);
                }                

                localCar.Engine.Rpm = (int)_iRacingSDK.Data.GetFloat("RPM");
                // m/s -> km/h
                localCar.Physics.Velocity = _iRacingSDK.Data.GetFloat("Speed") * 3.6f;
                localCar.Physics.Rotation = Quaternion.CreateFromYawPitchRoll(_iRacingSDK.Data.GetFloat("YawNorth"), _iRacingSDK.Data.GetFloat("Pitch"), _iRacingSDK.Data.GetFloat("Roll"));

                localCar.Race.GlobalPosition = _iRacingSDK.Data.GetInt("PlayerCarPosition");

                localCar.Inputs.Gear = _iRacingSDK.Data.GetInt("Gear") + 1;
                localCar.Inputs.Brake = _iRacingSDK.Data.GetFloat("Brake");
                localCar.Inputs.Throttle = _iRacingSDK.Data.GetFloat("Throttle");
                localCar.Inputs.Steering = _iRacingSDK.Data.GetFloat("SteeringWheelAngle");

                
                SessionData.Instance.LapDeltaToSessionBestLapMs = _iRacingSDK.Data.GetFloat("LapDeltaToSessionBestLap");

                /* as per https://github.com/alexanderzobnin/grafana-simracing-telemetry/blob/8c008f01003502c687aa4e5278018b000b0a5eaf/pkg/iracing/sharedmemory/models.go#L193
                   we can add these to local car (not available for opponent cars)
                             Lap                             int32
                             LapCompleted                    int32
                             LapDist                         float32
                             LapDistPct                      float32
                             RaceLaps                        int32
                             LapBestLap                      int32
                             LapBestLapTime                  float32
                             LapLastLapTime                  float32
                             LapCurrentLapTime               float32
                             LapLasNLapSeq                   int32
                             LapLastNLapTime                 float32
                             LapBestNLapLap                  int32
                             LapBestNLapTime                 float32
                             LapDeltaToBestLap               float32
                             LapDeltaToBestLap_DD            float32
                             LapDeltaToBestLap_OK            bool
                             LapDeltaToOptimalLap            float32
                             LapDeltaToOptimalLap_DD         float32
                             LapDeltaToOptimalLap_OK         bool
                             LapDeltaToSessionBestLap        float32
                             LapDeltaToSessionBestLap_DD     float32
                             LapDeltaToSessionBestLap_OK     bool
                             LapDeltaToSessionOptimalLap     float32
                             LapDeltaToSessionOptimalLap_DD  float32
                             LapDeltaToSessionOptimalLap_OK  bool
                             LapDeltaToSessionLastlLap       float32
                             LapDeltaToSessionLastlLap_DD    float32
                             LapDeltaToSessionLastlLap_OK    bool
                         */

                SessionData.Instance.Weather.AirTemperature = _iRacingSDK.Data.GetFloat("AirTemp");
                SessionData.Instance.Weather.AirVelocity = _iRacingSDK.Data.GetFloat("WindVel") * 3.6f;
                SessionData.Instance.Weather.AirDirection = _iRacingSDK.Data.GetFloat("WindDir");

                SessionData.Instance.Track.Temperature = _iRacingSDK.Data.GetFloat("TrackTempCrew");

                // Fuel telemetry and fuel consumption calc. This is using the last lap                
                var fuelLevelPercent = _iRacingSDK.Data.GetFloat("FuelLevelPct");
                localCar.Engine.MaxFuelLiters = _iRacingSDK.Data.SessionInfo.DriverInfo.DriverCarFuelMaxLtr;

                // TODO: iRacing gives unreasonable values for fuelUseKgPerHour. At least off by a factor 10
                // We keep track of fuel usage in the last lap until this is worked out.
                /* var fuelUseKgPerHour = _iRacingSDK.Data.GetFloat("FuelUsePerHour");                 
                float fuelKgPerLtr = _iRacingSDK.Data.SessionInfo.DriverInfo.DriverCarFuelKgPerLtr;
                float lapsPerHour = (float)TimeSpan.FromMinutes(60).TotalMilliseconds /
                    ((float) SessionData.Instance.Cars[SessionData.Instance.PlayerCarIndex].Value.LastLap.LaptimeMS);
                float fuelKgPerLap = fuelUseKgPerHour / lapsPerHour;
                float fuelLitersXLap = fuelKgPerLap / fuelKgPerLtr; 
                Debug.WriteLine("Fuel kgPerLr {0} KgPerHour {1} lapsPerHour {2} kgPerLap {3} LitersXLap {4} ", fuelKgPerLtr, fuelUseKgPerHour, lapsPerHour, fuelKgPerLap, fuelLitersXLap);
                */

                localCar.Engine.FuelLitersXLap = lastLapFuelConsumption; 
                localCar.Engine.FuelEstimatedLaps = localCar.Engine.FuelLiters / localCar.Engine.FuelLitersXLap;                

                // ABS. We don't seem to get any other info for localCar.Electronics such as TC and engine map.
                // There is brake bias in CarSetupModel, but it is unclear if that would be updated when the user changes it when driving.
                localCar.Electronics.AbsActivation = _iRacingSDK.Data.GetBool("BrakeABSactive") ? 1.0F : 0.0F; 

                // TODO : public int DriverIncidentCount { get; set; } and other incident info (CurDriverIncidentCount , TeamIncidentCount, ..)

                int sessionNumber = _iRacingSDK.Data.GetInt("SessionNum");
                var sessionType = _iRacingSDK.Data.SessionInfo.SessionInfo.Sessions[sessionNumber].SessionType;
                switch (sessionType)
                {
                    case "Race":
                        SessionData.Instance.SessionType = RaceSessionType.Race; break;
                    case "Practice":
                        SessionData.Instance.SessionType = RaceSessionType.Practice; break;
                    case "Qualify":
                    case "Lone Qualify":
                    case "Open Qualify":
                        SessionData.Instance.SessionType = RaceSessionType.Qualifying; break;
                    default:
                        Debug.WriteLine("Uknown session type " + sessionType);
                        SessionData.Instance.SessionType = RaceSessionType.Race; break;
                }
                
                IRacingSdkEnum.SessionState sessionState = (SessionState)_iRacingSDK.Data.GetInt("SessionState");
                switch (sessionState)
                {
                    case SessionState.GetInCar:
                    case SessionState.Warmup:
                        SessionData.Instance.Phase = SessionPhase.PreSession;
                        break;
                    case SessionState.ParadeLaps:
                        SessionData.Instance.Phase = SessionPhase.FormationLap;
                        break;
                    case SessionState.Checkered:
                    case SessionState.CoolDown:
                        SessionData.Instance.Phase = SessionPhase.SessionOver;
                        break;
                    case SessionState.Racing:
                        SessionData.Instance.Phase = SessionPhase.Session;
                        break;
                    default:
                        Debug.WriteLine("Unknow session state " + sessionState);
                        break;
                }

                
                if (sessionNumber != lastSessionNumber)
                {
                    Debug.WriteLine("session change. curr# {0} last# {1} new state {2} type {3}", sessionNumber, lastSessionNumber, sessionState, sessionType);
                    SimDataProvider.CallSessionTypeChanged(this, SessionData.Instance.SessionType);
                    SimDataProvider.CallSessionPhaseChanged(this, SessionData.Instance.Phase);
                    lastSessionNumber = sessionNumber;
                }
                SessionData.Instance.SessionTimeLeftSecs = _iRacingSDK.Data.GetDouble("SessionTimeRemain");                
                /* TODO more session info
	                SessionLapsRemain               int32
	                SessionLapsRemainEx             int32
	                SessionTimeTotal                float64
	                SessionLapsTotal                int32
	                SessionTimeOfDay                float32 */

                SpotterCallout = (CarLeftRight)_iRacingSDK.Data.GetInt("CarLeftRight");

                hasTelemetry = true;
            } catch (Exception ex) {
                Debug.WriteLine(ex.ToString);
            }
        }

        /// <summary>
        /// Called when a session changes.
        /// </summary>
        /// This updates all data from _iRacingSDK.Data.SessionInfo.* (as opposed to telemetry's Data.GetXXX data above).
        /// The data is coming from a YAML string described here: https://sajax.github.io/irsdkdocs/yaml/
        /// 
        /// This will be called by IRSDKSharper only if session data changed, which will be much less frequently than 
        /// the telemetry
        /// 
        /// Sets the global state about session. e.g.
        /// - SessionData.Instance.*
        /// - SessionData.Instance.*
        /// - carClasses field

        private void OnSessionInfo()
        {
            // Debug.WriteLine("OnSessionInfo\n{0}", _iRacingSDK.Data.SessionInfoYaml);
            
            int playerCarIdx = _iRacingSDK.Data.SessionInfo.DriverInfo.DriverCarIdx;
            SessionData.Instance.PlayerCarIndex = playerCarIdx;
            SessionData.Instance.FocusedCarIndex = playerCarIdx; // TODO: we don't have a mechanism yet to set the focussed driver. iRacing has https://sajax.github.io/irsdkdocs/telemetry/camcaridx.html
                                                                 // Does that give the spectating car? The doc seems to list available telemetry too.

            string TrackLengthText = _iRacingSDK.Data.SessionInfo.WeekendInfo.TrackLength; // e.g. "3.70 km"
            string[] parts = TrackLengthText.Split(' ');
            SessionData.Instance.Track.Length = (int)(double.Parse(parts[0]) * 1000); // convert to meters
            // TODO: we can get the sectors and their start and endpoints (in track%) from the session info. struct is  "SplitTimeInfoModel".

            LocalCarData localCar = SimDataProvider.LocalCar;
            localCar.Engine.MaxRpm = (int)_iRacingSDK.Data.SessionInfo.DriverInfo.DriverCarSLLastRPM;
            
            DriverModel driverModel = _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers[SessionData.Instance.PlayerCarIndex];
            localCar.Race.CarNumber = driverModel.CarNumberRaw;
            localCar.CarModel.CarClass = driverModel.CarClassShortName != null ? driverModel.CarClassShortName : driverModel.CarScreenNameShort;
            localCar.CarModel.GameName = driverModel.CarScreenNameShort;

            // TODO: pit limiter doesn't seem to work properly
            // EngineWarnings.PitSpeedLimiter.HasFlag(EngineWarnings.PitSpeedLimiter)
            localCar.Engine.IsPitLimiterOn = false;

            SessionData.Instance.Track.GameName = _iRacingSDK.Data.SessionInfo.WeekendInfo.TrackName;


            for (var index = 0; index < _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers.Count; index++)
            {                
                driverModel = _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers[index];
                if (driverModel.CarIsPaceCar > 0) continue;

                var carInfo = new CarInfo(index);
                carInfo.RaceNumber = driverModel.CarNumberRaw;
                // For multi-make classes like GT4/GT3, we use CarClassShortName otherwise (e.g. MX5 or GR86) we use CarScreenNameShort
                carInfo.CarClass = driverModel.CarClassShortName;
                if (carInfo.CarClass == null)
                {
                    carInfo.CarClass = driverModel.CarScreenNameShort;
                }
                carInfo.IsSpectator = driverModel.IsSpectator == 1;

                string currCarClassColor = driverModel.CarClassColor;
                AddCarClassEntry(carInfo.CarClass, Color.Aquamarine);  // TODO: need mapping from string currCarClassColor to Color

                // TODO: it looks like this might change in a team race when the driver changes. We need to test this with a team race at some point.
                // None of the currently ported HUDs do want to display the non-driving drivers in the team anyway.
                DriverInfo driver = new DriverInfo();                                
                DriverModel currDriverModel = _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers[index];
                driver.Name = currDriverModel.UserName;
                driver.Rating = currDriverModel.IRating;
                // LicString is "<class> <SR>".
                driver.Category = currDriverModel.LicString;
                // TODO LicColor
                carInfo.AddDriver(driver);                

                SessionData.Instance.AddOrUpdateCar(index, carInfo);

                // TODO: add qualifying time info
            }
        }

        // for debugging
        private void PrintAllCarInfo()
        {
            
            for (var index = 0; index < SessionData.Instance.Cars.Count; index++)
            {
                CarInfo carInfo = SessionData.Instance.Cars[index].Value;
                Debug.WriteLine("Car " + index + " #" + carInfo.RaceNumber + " " + carInfo.Drivers[0].Name + " pos: " + carInfo.CupPosition + " GL: " + carInfo.GapToClassLeaderMs + " GP:" + carInfo.GapToPlayerMs);                
            }               
        }

        public override void SetupPreviewData()
        {
            SessionData.Instance.FocusedCarIndex = 1;
            SessionData.Instance.PlayerCarIndex = 1;

            SessionData.Instance.Track.GameName = "Spa";
            SessionData.Instance.Track.Length = 7004;
            SessionData.Instance.Track.Temperature = 21;

            SimDataProvider.LocalCar.CarModel.CarClass = "F1";

            var lap1 = new LapInfo();
            lap1.Splits = [10000, 22000, 11343];
            lap1.LaptimeMS = lap1.Splits.Sum();
            var lap2 = new LapInfo();
            lap2.Splits = [9000, 22000, 11343];
            lap2.LaptimeMS = lap2.Splits.Sum();

            var car1 = new CarInfo(1);
            car1.TrackPercentCompleted = 10.0f;
            car1.Position = 1;
            car1.CarLocation = CarInfo.CarLocationEnum.Track;
            car1.CurrentDriverIndex = 0;            
            car1.Kmh = 140;
            car1.CupPosition = 1;
            car1.RaceNumber = 17;
            car1.LastLap = lap1;
            car1.FastestLap = lap1;
            car1.CurrentLap = lap2;
            car1.GapToClassLeaderMs = 0;
            car1.CarClass = "F1";
            SessionData.Instance.AddOrUpdateCar(1, car1);
            var car1driver0 = new DriverInfo();
            car1driver0.Name = "Max Verstappen";
            car1driver0.Rating = 7123;
            car1driver0.Category = "A 2.7";
            car1.AddDriver(car1driver0);
            SimDataProvider.LocalCar.Engine.FuelEstimatedLaps = 3;
            SimDataProvider.LocalCar.Engine.FuelLiters = 26.35f;


            CarInfo car2 = new CarInfo(2);
            // 1 meter behind car1
            car2.TrackPercentCompleted = car1.TrackPercentCompleted + (1.0F / ((float) SessionData.Instance.Track.Length));
            car2.Position = 2;
            car2.CarLocation = CarInfo.CarLocationEnum.Track;
            car2.CurrentDriverIndex = 0;            
            car2.Kmh = 160;
            car2.CupPosition = 2;
            car2.RaceNumber = 5;
            car2.LastLap = lap2;
            car2.FastestLap = lap2;
            car2.CurrentLap = lap1;
            car2.GapToClassLeaderMs = 1000;
            car2.CarClass = "F1";
            SessionData.Instance.AddOrUpdateCar(2, car2);
            var car2driver0 = new DriverInfo();
            car2driver0.Name = "Michael Schumacher";
            car2driver0.Rating = 8123;
            car2driver0.Category = "A 2.2";
            car2.AddDriver(car2driver0);
            
            AddCarClassEntry("F1", Color.Sienna);

            SpotterCallout = CarLeftRight.CarLeft;
            
            hasTelemetry = true; // TODO: will this work when we have real telemetry? And is this sample telemetry valid for all sim providers?
        }

        // Gap calculation according to https://github.com/lespalt/iRon 
        private int GetGapToPlayerMs(int index, int playerCarIdx)
        {            
            float bestForPlayer = _iRacingSDK.Data.GetFloat("CarIdxBestLapTime", playerCarIdx);
            if (bestForPlayer == 0)
                bestForPlayer = _iRacingSDK.Data.SessionInfo.DriverInfo.Drivers[playerCarIdx].CarClassEstLapTime;
            
            float C = _iRacingSDK.Data.GetFloat("CarIdxEstTime", index);
            float S = _iRacingSDK.Data.GetFloat("CarIdxEstTime", playerCarIdx);

            // Does the delta between us and the other car span across the start/finish line?
            bool wrap = Math.Abs(_iRacingSDK.Data.GetFloat("CarIdxLapDistPct", index) - _iRacingSDK.Data.GetFloat("CarIdxLapDistPct", playerCarIdx)) > 0.5f;
            float delta;
            if (wrap)
            {
                delta = S > C ? (C - S) + bestForPlayer : (C - S) - bestForPlayer;
                // lapDelta += S > C ? -1 : 1;
            }
            else
            {
                delta = C - S;
            }
            return (int)(delta * 1000);
        }

        // Gap to player ahead according to  https://github.com/LEMPLS/iracing-companion-server/blob/af4ad01325f74fc81326eaad6ae986231fecaf9e/src/index.js#L98C1-L106C61
        // This is independent of classes
        private float GetGapToPlayerAheadMs(int index, int playerCarIdx)
        {

            // use position independent of classes
            int playerCarPosition = SessionData.Instance.Cars[playerCarIdx].Value.Position;
            int carAheadIdx = SessionData.Instance.Cars[playerCarPosition - 1].Value.CarIndex;

            var playerCarF2Time = _iRacingSDK.Data.GetFloat("CarIdxF2Time", playerCarIdx);
            var carAheadF2Time = _iRacingSDK.Data.GetFloat("CarIdxF2Time", (int)carAheadIdx);

            return playerCarF2Time - carAheadF2Time;
        }


        internal override void Stop()
        {
            _iRacingSDK?.Stop();
            hasTelemetry = false;
        }


        public override void Update(ref LocalCarData localCar, ref SessionData sessionData, ref GameData gameData)
        {            
            gameData.Name = Game.iRacing.ToShortName();
            // Updates for iRacing are done with event handlers and don't need to be driven by Race Element with this Update method
        }

        public override Color GetColorForCarClass(String carClass)
        {
            return carClassColor[carClass];
        }
        public override List<string> GetCarClasses() { return carClasses.ToList();}

        public override bool HasTelemetry()
        {
            return hasTelemetry;
        }        

        private void AddCarClassEntry(string carClass, System.Drawing.Color color)
        {
            if (carClasses == null)
            {
                carClasses = new HashSet<string>();
            }                        
            carClasses.Add(carClass);
            // TODO: map string->Color instead of hardcoded.  
            carClassColor.TryAdd(carClass, Color.AliceBlue); 
        }

        public CarLeftRight GetSpotterCallout()
        {
            return SpotterCallout;            
        }

        override public bool IsSpectating(int playerCarIndex, int focusedIndex)
        {
            // TODO We need to test how spotting team mates works in a multi-driver team race. 
            // E.g. what telemetry is available
            return false;
        }
    }

    
}
