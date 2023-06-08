﻿using RaceElement.Controls.Setup;
using RaceElement.Data;
using RaceElement.Data.ACC.Tracks;
using RaceElement.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using static RaceElement.Data.ACC.Tracks.TrackData;
using static RaceElement.Data.ConversionFactory;

namespace RaceElement.Controls
{
    /// <summary>
    /// Interaction logic for SetupImporter.xaml
    /// </summary>
    public partial class SetupImporter : UserControl
    {
        internal static SetupImporter Instance { get; private set; }

        private string _originalSetupFile;
        private string _setupName;
        private SetupJson.Root _currentSetup;
        private FlowDocSetupRenderer _renderer;

        public SetupImporter()
        {
            InitializeComponent();
            this.Visibility = Visibility.Hidden;

            buttonCancel.Click += (s, e) => Close();
            buttonImportMulti.Click += (s, e) =>
            {
                MainWindow.Instance.EnqueueSnackbarMessage($"Importing setups, please wait...");
                if (listViewTracks.SelectedItems.Count > 0 && listViewTracks.SelectionMode == SelectionMode.Multiple)
                {
                    StringBuilder sb = new StringBuilder();

                    foreach (ListViewItem selectedItem in listViewTracks.SelectedItems)
                    {
                        Import((string)selectedItem.DataContext, false);
                        sb.Append($", {Tracks[(string)selectedItem.DataContext].FullName}");
                    }

                    CarModels model = ConversionFactory.ParseCarName(_currentSetup.CarName);
                    string modelName = ConversionFactory.GetNameFromCarModel(model);
                    MainWindow.Instance.EnqueueSnackbarMessage($"Imported setup \"{_setupName}\" for {modelName} at{sb}");

                    SetupBrowser.Instance.FetchAllSetups();

                    Close();
                }
            };

            _renderer = new FlowDocSetupRenderer();

            Instance = this;
        }

        private void BuildTrackList()
        {
            this.listViewTracks.Items.Clear();
            foreach (KeyValuePair<string, AbstractTrackData> kv in Tracks.ToImmutableArray().Sort((x, y) => x.Key.CompareTo(y.Key)))
            {
                ListViewItem trackItem = new ListViewItem()
                {
                    FontWeight = FontWeights.Bold,
                    Content = kv.Value.FullName,
                    DataContext = kv.Key
                };

                if (listViewTracks.SelectionMode == SelectionMode.Single)
                    trackItem.MouseLeftButtonUp += (s, e) =>
                    {
                        Import(kv.Key, true);

                        SetupBrowser.Instance.FetchAllSetups();

                        Close();
                    };

                this.listViewTracks.Items.Add(trackItem);
            }
        }


        private void Import(string track, bool displayMessage)
        {
            CarModels model = ConversionFactory.ParseCarName(_currentSetup.CarName);
            string modelName = ConversionFactory.GetNameFromCarModel(model);


            FileInfo targetFile = new FileInfo(FileUtil.AccPath + "Setups\\" + _currentSetup.CarName + "\\" + track + "\\" + _setupName + ".json");

            if (targetFile.Exists)
            {
                MainWindow.Instance.EnqueueSnackbarMessage($"Setup already exists: {targetFile.FullName}");
                return;
            }

            if (!targetFile.Directory.Exists)
                targetFile.Directory.Create();

            FileInfo originalFile = new FileInfo(_originalSetupFile);
            if (originalFile.Exists)
                originalFile.CopyTo(targetFile.FullName);

            if (displayMessage)
                MainWindow.Instance.EnqueueSnackbarMessage($"Imported setup \"{_setupName}\" for {modelName} at {Tracks[track].FullName}");
        }

        public bool Open(string setupFile, bool selectMultipleTracks, bool showTrack = false)
        {
            listViewTracks.SelectionMode = selectMultipleTracks ? SelectionMode.Multiple : SelectionMode.Single;
            buttonImportMulti.Visibility = selectMultipleTracks ? Visibility.Visible : Visibility.Collapsed;

            FileInfo file = null;

            if (setupFile.StartsWith("https"))
            {
                try
                {
                    using (var client = new WebClient())
                    {
                        string fileName;
                        string[] splits = setupFile.Split(new char[] { '/' });
                        fileName = splits[splits.Length - 1];

                        DirectoryInfo downloadCache = new DirectoryInfo(FileUtil.RaceElementDownloadCachePath);

                        if (!downloadCache.Exists) downloadCache.Create();

                        string fullName = FileUtil.RaceElementDownloadCachePath + fileName;
                        client.DownloadFile(setupFile, fullName);
                        setupFile = fullName;
                        file = new FileInfo(fullName);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                }
            }
            else
            {
                file = new FileInfo(setupFile);
            }

            if (file == null || !file.Exists)
                return false;

            SetupJson.Root setupRoot = ConversionFactory.GetSetupJsonRoot(file);
            if (setupRoot == null)
                return false;

            Debug.WriteLine($"Importing {file.FullName}");
            CarModels model = ConversionFactory.ParseCarName(setupRoot.CarName);
            string modelName = ConversionFactory.GetNameFromCarModel(model);
            Debug.WriteLine($"Trying to import a setup for {modelName}");
            _currentSetup = setupRoot;
            _setupName = file.Name.Replace(".json", "");
            _originalSetupFile = setupFile;

            try
            {
                BuildTrackList();
                this.textBlockSetupName.Text = $"{modelName} - {file.Name}";

                _renderer.LogSetup(ref this.flowDocument, setupFile, showTrack);

                this.Visibility = Visibility.Visible;
                SetupsTab.Instance.tabControl.IsEnabled = false;
            }
            catch (Exception ex) { Debug.WriteLine(ex); }

            return true;
        }

        public void Close()
        {
            _setupName = String.Empty;
            _currentSetup = null;
            this.Visibility = Visibility.Hidden;
            this.listViewTracks.Items.Clear();
            this.flowDocument.Blocks.Clear();
            SetupsTab.Instance.tabControl.IsEnabled = true;
        }
    }
}
