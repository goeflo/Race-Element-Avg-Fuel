﻿using RaceElement.HUD.Overlay.Configuration;
using RaceElement.HUD.Overlay.Internal;
using RaceElement.Util;
using RaceElement.Util.SystemExtensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RaceElement.HUD.ACC.Overlays.Driving.OverlayDualSenseX
{
    [Overlay(Name = "DualSense X",
        Description = "Adds variable trigger haptics and feedback for the Dual Sense 5 controller.\n See Discord Guide section for instructions.",
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
                [ToolTip("Frequency of the ABS and TC haptics.")]
                [IntRange(75, 150, 1)]
                public int Frequency { get; set; } = 75;

                [ToolTip("Adds progressive load to the left trigger(braking).")]
                public bool BrakeLoad { get; set; } = true;

                [ToolTip("Force of the haptics.")]
                [IntRange(10, 255, 1)]
                public int MaxForce { get; set; } = 255;
            }
        }

        private FileInfo _textFile;
        public DualSenseXOverlay(Rectangle rectangle) : base(rectangle, "Dual Sense X")
        {
            this.Width = 1; this.Height = 1;
            RefreshRateHz = 50;
        }

        public override void BeforeStart()
        {
            _textFile = new FileInfo($"{FileUtil.AppDirectory}{Path.DirectorySeparatorChar}DualSenseXTriggerStates.txt");
            if (!_textFile.Exists)
                _textFile.Create();
        }

        public override bool ShouldRender()
        {
            return true;
        }

        public override void Render(Graphics g)
        {

            StringBuilder sb = new StringBuilder();
            if (pagePhysics.Abs > 0)
            {
                sb.AppendLine($"{PropertyLeftTrigger}={TriggerStates.CustomTriggerValue}\n");
                sb.AppendLine($"{PropertyCustomTriggerValueLeftMode}={GetStringValue(CustomTriggerValues.VibrateResistanceB)}");
                int force = (int)(pagePhysics.Brake * _config.Haptics.MaxForce);
                sb.AppendLine($"{PropertyForceLeftTrigger}=({_config.Haptics.Frequency})({force})(0)(0)(0)(0)(0)");
            }
            else
            {
                if (_config.Haptics.BrakeLoad && pagePhysics.Brake > 0.001f)
                {
                    sb.AppendLine($"{PropertyLeftTrigger}={TriggerStates.CustomTriggerValue}\n");
                    sb.AppendLine($"{PropertyCustomTriggerValueLeftMode}={GetStringValue(CustomTriggerValues.Rigid)}");

                    int force = (int)(pagePhysics.Brake * _config.Haptics.MaxForce);
                    force.Clip(0, _config.Haptics.MaxForce);
                    sb.AppendLine($"{PropertyForceLeftTrigger}=({force})({force})(0)(0)(0)(0)(0)");
                }
                else
                {
                    sb.AppendLine($"{PropertyLeftTrigger}={TriggerStates.Normal}");
                    sb.AppendLine($"{PropertyForceLeftTrigger}=(0)(0)(0)(0)(0)(0)(0)");
                }
            }

            if (pagePhysics.TC > 0)
            {
                sb.AppendLine($"{PropertyRightTrigger}={TriggerStates.CustomTriggerValue}");
                sb.AppendLine($"{PropertyCustomTriggerValueRightMode}={GetStringValue(CustomTriggerValues.VibrateResistanceB)}");
                sb.AppendLine($"{PropertyForceRightTrigger}=({_config.Haptics.Frequency})({_config.Haptics.MaxForce})(0)(0)(0)(0)(0)");
            }
            else
            {
                sb.AppendLine($"{PropertyRightTrigger}={TriggerStates.Normal}");
                sb.AppendLine($"{PropertyForceRightTrigger}=(0)(0)(0)(0)(0)(0)(0)");
            }

            UpdateDualSenseFile(sb.ToString());
        }

        string last = string.Empty;
        public void UpdateDualSenseFile(string newTriggers)
        {
            if (newTriggers != last)
                for (int i = 0; i < 10; i++)
                    try
                    {
                        using FileStream stream = File.Open(_textFile.FullName, FileMode.Truncate);
                        stream.Close();

                        File.WriteAllText(_textFile.FullName, $"{newTriggers}");
                        last = newTriggers;
                        break;
                    }
                    catch (IOException)
                    {
                        Thread.Sleep(1);
                    }
        }


        // https://dualsensex.com/game-devs/

        private static string PropertyLeftTrigger = "LeftTrigger";
        private static string PropertyRightTrigger = "RightTrigger";
        private static string PropertyVibrateTriggerIntensity = "VibrateTriggerIntensity";
        private static string PropertyCustomTriggerValueLeftMode = "CustomTriggerValueLeftMode";
        private static string PropertyCustomTriggerValueRightMode = "CustomTriggerValueRightMode";
        private static string PropertyForceLeftTrigger = "ForceLeftTrigger";
        private static string PropertyForceRightTrigger = "ForceRightTrigger";

        private enum TriggerStates
        {
            Normal,
            CustomTriggerValue,
            GameCube,
            Resistance,
            Bow,
            Galloping,
            SemiAutomaticGun,
            AutomaticGun,
            Machine,
            Choppy,
            VerySoft,
            Soft,
            Medium,
            Hard,
            VeryHard,
            Hardest,
            Rigid,
            VibrateTriggerPulse,
            VibrateTrigger,
        }

        private enum CustomTriggerValues
        {
            [StringValue("OFF")]
            OFF,
            [StringValue("Rigid")]
            Rigid,
            [StringValue("Rigid A")]
            RigidA,
            [StringValue("Rigid B")]
            RigidB,
            [StringValue("Rigid AB")]
            RigidAB,
            [StringValue("Pulse")]
            Pulse,
            [StringValue("Pulse A")]
            PulseA,
            [StringValue("Pulse B")]
            PulseB,
            [StringValue("Pulse AB")]
            PulseAB,
            [StringValue("VibrateResistance")]
            VibrateResistance,
            [StringValue("VibrateResistance A")]
            VibrateResistanceA,
            [StringValue("VibrateResistance B")]
            VibrateResistanceB,
            [StringValue("VibrateResistance AB")]
            VibrateResistanceAB,
            [StringValue("Vibrate Pulse")]
            VibratePulse,
            [StringValue("Vibrate Pulse A")]
            VibratePulseA,
            [StringValue("Vibrate Pulse B")]
            VibratePulseB,
            [StringValue("Vibrate Pulse AB")]
            VibratePulseAB,
        }

        private sealed class StringValueAttribute : Attribute
        {
            public string StringValue { get; private set; }

            public StringValueAttribute(string value)
            {
                this.StringValue = value;
            }
        }

        public static string GetStringValue(Enum value)
        {
            // Get the type
            Type type = value.GetType();

            // Get fieldinfo for this type
            FieldInfo fieldInfo = type.GetField(value.ToString());

            // Get the stringvalue attributes
            StringValueAttribute[] attribs = fieldInfo.GetCustomAttributes(
                typeof(StringValueAttribute), false) as StringValueAttribute[];

            // Return the first if there was a match.
            return attribs.Length > 0 ? attribs[0].StringValue : "";
        }

    }
}
