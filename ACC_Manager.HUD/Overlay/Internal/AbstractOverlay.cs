﻿using ACCManager.Broadcast.Structs;
using ACCManager.Data.ACC.Tracker;
using ACCManager.HUD.Overlay.Configuration;
using ACCManager.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using static ACCManager.ACCSharedMemory;
using static ACCManager.HUD.Overlay.Configuration.OverlaySettings;

namespace ACCManager.HUD.Overlay.Internal
{
    public abstract class AbstractOverlay : FloatingWindow
    {
        public string Name { get; private set; }
        private bool Draw = false;

        public bool IsRepositioning { get; internal set; }
        public bool AllowReposition { get; set; } = true;
        private bool AllowRescale { get; set; } = false;

        public int RefreshRateHz = 30;

        private float Scale = 1f;

        private Window RepositionWindow;

        public SPageFilePhysics pagePhysics;
        public SPageFileGraphic pageGraphics;
        public SPageFileStatic pageStatic;
        public Broadcast.Structs.RealtimeUpdate broadCastRealTime;
        public Broadcast.Structs.TrackData broadCastTrackData;
        public Broadcast.Structs.RealtimeCarUpdate broadCastRealtimeCarUpdate;


        public int ScreenWidth => (int)System.Windows.SystemParameters.PrimaryScreenWidth;
        public int ScreenHeight => (int)System.Windows.SystemParameters.PrimaryScreenHeight;

        public bool RequestsDrawItself = false;

        protected AbstractOverlay(Rectangle rectangle, string Name)
        {
            this.X = rectangle.X;
            this.Y = rectangle.Y;
            this.Width = rectangle.Width;
            this.Height = rectangle.Height;
            this.Alpha = 255;
            this.Name = Name;



            if (AllowReposition)
                ApplyOverlaySettings();

            LoadFieldConfig();
        }


        private void LoadFieldConfig()
        {
            FieldInfo[] fields = this.GetType().GetRuntimeFields().ToArray();
            foreach (var nested in fields)
            {
                if (nested.FieldType.BaseType == typeof(OverlayConfiguration))
                {
                    var overlayConfig = (OverlayConfiguration)Activator.CreateInstance(nested.FieldType, new object[] { });

                    OverlaySettingsJson savedSettings = OverlaySettings.LoadOverlaySettings(this.Name);

                    if (savedSettings == null)
                        return;

                    overlayConfig.SetConfigFields(savedSettings.Config);

                    nested.SetValue(this, overlayConfig);

                    if (overlayConfig.AllowRescale)
                    {
                        this.AllowRescale = true;
                        this.Scale = overlayConfig.Scale;
                    }
                }
            }
        }

        private void ApplyOverlaySettings()
        {
            OverlaySettingsJson settings = OverlaySettings.LoadOverlaySettings(this.Name);
            if (settings != null)
            {
                this.X = settings.X;
                this.Y = settings.Y;
            }
        }

        public abstract void BeforeStart();
        public void Start()
        {
            try
            {
                PageStaticTracker.Instance.Tracker += PageStaticChanged;
                PageGraphicsTracker.Instance.Tracker += PageGraphicsChanged;
                PagePhysicsTracker.Instance.Tracker += PagePhysicsChanged;
                BroadcastTracker.Instance.OnRealTimeUpdate += BroadCastRealTimeChanged;
                BroadcastTracker.Instance.OnTrackDataUpdate += BroadCastTrackDataChanged;
                BroadcastTracker.Instance.OnRealTimeCarUpdate += BroadCastRealTimeCarUpdateChanged;

                ACCSharedMemory mem = new ACCSharedMemory();

                pageStatic = mem.ReadStaticPageFile();
                pageGraphics = mem.ReadGraphicsPageFile();
                pagePhysics = mem.ReadPhysicsPageFile();

                try
                {
                    BeforeStart();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    LogWriter.WriteToLog(ex);
                }
                if (AllowRescale)
                {
                    this.Width = (int)Math.Ceiling(this.Width * Scale);
                    this.Height = (int)Math.Ceiling(this.Height * Scale);
                }
                Draw = true;
                this.Show();

                new Thread(x =>
                {
                    while (Draw)
                    {
                        lock (this)
                        {
                            Thread.Sleep(1000 / RefreshRateHz);
                            if (this == null || this._disposed)
                            {
                                this.Stop();
                                return;
                            }

                            if (!RequestsDrawItself)
                                this.UpdateLayeredWindow();
                        }
                    }

                    this.Stop();
                }).Start();
            }
            catch (Exception ex) { Debug.WriteLine(ex); }
        }

        private void BroadCastRealTimeCarUpdateChanged(object sender, RealtimeCarUpdate e)
        {
            broadCastRealtimeCarUpdate = e;
        }

        private void BroadCastTrackDataChanged(object sender, TrackData e)
        {
            broadCastTrackData = e;
        }

        private void BroadCastRealTimeChanged(object sender, Broadcast.Structs.RealtimeUpdate e)
        {
            broadCastRealTime = e;
        }

        public void RequestRedraw()
        {
            this.UpdateLayeredWindow();
        }

        private void PagePhysicsChanged(object sender, SPageFilePhysics e)
        {
            pagePhysics = e;
        }

        private void PageGraphicsChanged(object sender, SPageFileGraphic e)
        {
            pageGraphics = e;
        }

        private void PageStaticChanged(object sender, SPageFileStatic e)
        {
            pageStatic = e;
        }

        public abstract void BeforeStop();
        public void Stop()
        {
            this.EnableReposition(false);
            try
            {
                BeforeStop();
            }
            catch (Exception ex)
            {
                LogWriter.WriteToLog(ex);
            }
            PageStaticTracker.Instance.Tracker -= PageStaticChanged;
            PageGraphicsTracker.Instance.Tracker -= PageGraphicsChanged;
            PagePhysicsTracker.Instance.Tracker -= PagePhysicsChanged;
            BroadcastTracker.Instance.OnRealTimeUpdate -= BroadCastRealTimeChanged;
            BroadcastTracker.Instance.OnTrackDataUpdate -= BroadCastTrackDataChanged;
            BroadcastTracker.Instance.OnRealTimeCarUpdate -= BroadCastRealTimeCarUpdateChanged;

            Draw = false;
            this.Close();
            this.Dispose();
        }

        public abstract bool ShouldRender();
        public abstract void Render(Graphics g);

        protected sealed override void PerformPaint(PaintEventArgs e)
        {
            if (base.Handle == IntPtr.Zero)
                return;

            if (Draw)
            {
                if (ShouldRender())
                {
                    try
                    {
                        if (AllowRescale)
                            e.Graphics.ScaleTransform(Scale, Scale);

                        Render(e.Graphics);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine(ex);
                        LogWriter.WriteToLog(ex);
                    }
                }
                else
                {
                    e.Graphics.Clear(Color.Transparent);
                }
            }
        }

        public void EnableReposition(bool enabled)
        {
            try
            {
                if (!AllowReposition)
                    return;

                this.IsRepositioning = enabled;

                if (enabled)
                {
                    this.RepositionWindow = new Window()
                    {
                        Width = this.Width,
                        Height = this.Height,
                        WindowStyle = WindowStyle.None,
                        ResizeMode = ResizeMode.NoResize,
                        Left = X,
                        Top = Y,
                        Title = this.Name,
                        ToolTip = this.Name,
                        Topmost = true,
                        BorderBrush = System.Windows.Media.Brushes.Red,
                        BorderThickness = new Thickness(1),
                        ShowInTaskbar = false,
                        AllowsTransparency = true,
                        Opacity = 0.3
                    };
                    this.RepositionWindow.MouseLeftButtonDown += (s, e) =>
                    {
                        this.RepositionWindow.BorderBrush = System.Windows.Media.Brushes.Green;
                        this.RepositionWindow.BorderThickness = new Thickness(3);
                        this.RepositionWindow.DragMove();
                    };

                    this.RepositionWindow.LocationChanged += (s, e) =>
                    {
                        X = (int)this.RepositionWindow.Left;
                        Y = (int)this.RepositionWindow.Top;
                    };

                    this.RepositionWindow.Deactivated += (s, e) =>
                    {
                        this.RepositionWindow.BorderBrush = System.Windows.Media.Brushes.Red;
                        this.RepositionWindow.BorderThickness = new Thickness(1);
                    };
                  
                    this.RepositionWindow.KeyDown += (s, e) =>
                    {
                        this.RepositionWindow.BorderBrush = System.Windows.Media.Brushes.Green;
                        this.RepositionWindow.BorderThickness = new Thickness(3);
                        switch (e.Key)
                        {
                            case System.Windows.Input.Key.Right:
                                {
                                    this.RepositionWindow.Left += 1;
                                    break;
                                }
                            case System.Windows.Input.Key.Left:
                                {
                                    this.RepositionWindow.Left -= 1;
                                    break;
                                }

                            case System.Windows.Input.Key.Up:
                                {
                                    this.RepositionWindow.Top -= 1;
                                    break;
                                }
                            case System.Windows.Input.Key.Down:
                                {
                                    this.RepositionWindow.Top += 1;
                                    break;
                                }
                            default: break;
                        }

                    };

                    RepositionWindow.Show();
                }
                else
                {
                    if (this.RepositionWindow != null)
                    {
                        this.RepositionWindow.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            try
                            {
                                if (this.RepositionWindow != null)
                                {
                                    this.RepositionWindow.Hide();
                                    this.RepositionWindow.Close();
                                    this.RepositionWindow = null;
                                }
                            }
                            catch (Exception ex) { Debug.WriteLine(ex); }
                        }));
                    }



                    OverlaySettingsJson settings = OverlaySettings.LoadOverlaySettings(this.Name);
                    settings.X = X;
                    settings.Y = Y;

                    OverlaySettings.SaveOverlaySettings(this.Name, settings);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                LogWriter.WriteToLog(ex);
            }
        }
    }
}
