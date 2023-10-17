﻿using Newtonsoft.Json;
using RaceElement.HUD.Overlay.Configuration;
using RaceElement.HUD.Overlay.Internal;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using static RaceElement.HUD.ACC.Overlays.Pitwall.OverlayDualSenseX.DualSenseXResources;

namespace RaceElement.HUD.ACC.Overlays.Pitwall.OverlayDualSenseX
{
    [Overlay(Name = "DualSense X",
        Description = "Adds variable trigger haptics and feedback for the DualSense 5 controller using DualSense V2($5 on steam).\n See Guide in the Discord of Race Element for instructions.",
        OverlayCategory = OverlayCategory.Inputs,
        OverlayType = OverlayType.Debug)]
    internal sealed class DualSenseXOverlay : AbstractOverlay
    {
        private readonly DualSenseXConfiguration _config = new DualSenseXConfiguration();
        private sealed class DualSenseXConfiguration : OverlayConfiguration
        {
            [ConfigGrouping("Haptics", "Adjust the haptics for the left and right trigger.")]
            public HapticsGrouping Haptics { get; set; } = new HapticsGrouping();
            public class HapticsGrouping
            {
                [ToolTip("Adds progressive load to the left trigger(braking).")]
                public bool BrakeLoad { get; set; } = true;

                [ToolTip("Force of the haptics.")]
                [IntRange(1, 50, 1)]
                public int MaxForce { get; set; } = 5;
            }
        }

        private UdpClient _client;
        private IPEndPoint _endPoint;
        private DateTime _timeSent;

        /// <summary>
        /// This is set to true when the preview data is set up, avoids any connection as long as the overlay is not running but just rendering a preview.
        /// </summary>
        private bool IsRenderingPreview = false;

        public DualSenseXOverlay(Rectangle rectangle) : base(rectangle, "DualSense X")
        {
            this.Width = 1; this.Height = 1;
            RefreshRateHz = 50;
        }

        public override void SetupPreviewData() => IsRenderingPreview = true;

        public override void BeforeStop()
        {
            _client?.Close();
            _client?.Dispose();
        }

        public override bool ShouldRender() => !IsRenderingPreview;

        public override void Render(Graphics g)
        {
            if (IsRenderingPreview) return;
            if (_client == null)
            {
                CreateEndPoint();
                Thread.Sleep(500);
                SetLighting();
            }

            Packet tcPacket = TriggerHaptics.HandleTractionControl(pagePhysics);
            if (tcPacket != null)
            {
                Send(tcPacket);
                ServerResponse response = Receive(ref _endPoint);
                HandleResponse(response);
            }

            Packet absPacket = TriggerHaptics.HandleABS(pagePhysics);
            if (absPacket != null)
            {
                Send(absPacket);
                ServerResponse response = Receive(ref _endPoint);
                HandleResponse(response);
            }
        }

        private void SetLighting()
        {
            Debug.WriteLine("Changing RGB");
            Packet p = new Packet();
            int controllerIndex = 0;

            p.instructions = new Instruction[1];  // send only 1 instruction
            p.instructions[0].type = InstructionType.RGBUpdate;
            p.instructions[0].parameters = new object[] { controllerIndex, 255, 69, 0 };

            Send(p);
            ServerResponse lightingReponse = Receive(ref _endPoint);
            HandleResponse(lightingReponse);
        }

        private void CreateEndPoint()
        {
            _client = new UdpClient();
            var portNumber = File.ReadAllText(@"C:\Temp\DualSenseX\DualSenseX_PortNumber.txt");
            _endPoint = new IPEndPoint(Triggers.localhost, Convert.ToInt32(portNumber));
            Debug.WriteLine($"Port number found is: {portNumber}\n");
        }

        private void Send(Packet data)
        {
            var RequestData = Encoding.ASCII.GetBytes(Triggers.PacketToJson(data));
            _client.Send(RequestData, RequestData.Length, _endPoint);
            _timeSent = DateTime.Now;
        }

        private ServerResponse Receive(ref IPEndPoint endPoint)
        {
            byte[] bytesReceivedFromServer = _client.Receive(ref _endPoint);

            if (bytesReceivedFromServer.Length > 0)
            {
                ServerResponse ServerResponseJson = JsonConvert.DeserializeObject<ServerResponse>($"{Encoding.ASCII.GetString(bytesReceivedFromServer, 0, bytesReceivedFromServer.Length)}");
                return ServerResponseJson;
            }

            return null;
        }

        private void HandleResponse(ServerResponse response)
        {
            if (response != null)
            {
                Debug.WriteLine("===================================================================");

                Debug.WriteLine($"Status: {response.Status}");
                DateTime CurrentTime = DateTime.Now;
                TimeSpan Timespan = CurrentTime - _timeSent;
                // First send shows high Milliseconds response time for some reason
                Debug.WriteLine($"Time Received: {response.TimeReceived}, took: {Timespan.TotalMilliseconds} to receive response from DSX");
                Debug.WriteLine($"isControllerConnected: {response.isControllerConnected}");
                Debug.WriteLine($"BatteryLevel: {response.BatteryLevel}");

                Debug.WriteLine("===================================================================\n");
            }
        }
    }
}
