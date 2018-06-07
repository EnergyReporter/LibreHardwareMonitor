﻿using System;
using System.Collections.Generic;
using System.Text;
using HidLibrary;

namespace OpenHardwareMonitor.Hardware.Aquacomputer
{
    /*
     * TODO:
     * Check tested and fix unknown variables in Update()
     * Check if property "Variant" is valid interpreted
     * Implement Fan Control in SetControl()
     */
    internal class AquastreamXT : Hardware
    {
        [Flags]
        enum PumpAlarms : byte
        {
            ALARM_SENSOR1 = 1,
            ALARM_SENSOR2 = 2,
            ALARM_PUMP = 4,
            ALARM_FAN = 8,
            ALARM_FLOW = 16,
            ALARM_FAN_SHORT = 32,
            ALARM_FAN_TEMP90 = 64,
            ALARM_FAN_TEMP70 = 128
        }

        [Flags]
        enum PumpMode : byte
        {
            MODE_PUMP_ADV = 1,
            MODE_FAN_AMP = 2,
            MODE_FAN_CONTROLLER = 4
        }

        #region USB
        private HidDevice device;
        private byte[] rawData;
        public UInt16 FirmwareVersion { get; private set; }
        #endregion

        private readonly Sensor _fanControl, _pumpPower, _pumpFlow;
        private readonly Sensor[] _rpmSensors = new Sensor[2];
        #if DEBUG
        private readonly Sensor[] _debugSensor = new Sensor[5];
        #endif
        private readonly Sensor[] _temperatures = new Sensor[3];
        private readonly Sensor[] _voltages = new Sensor[2];
        private readonly Sensor[] _frequencys = new Sensor[2];
               
        public AquastreamXT(HidDevice dev, ISettings settings) : base("Aquastream XT", new Identifier(dev.DevicePath), settings)
        {
            device = dev;

            do
            {
                device.ReadFeatureData(out rawData, 0x4);
            } while (rawData[0] != 0x4);

            Name = $"Aquastream XT {Variant}";
            FirmwareVersion = BitConverter.ToUInt16(rawData, 50);
#if DEBUG
            debugSensor[0] = new Sensor("", 0, SensorType.Debug, this, settings);
            ActivateSensor(debugSensor[0]);
            debugSensor[1] = new Sensor("", 1, SensorType.Debug, this, settings);
            ActivateSensor(debugSensor[1]);
            debugSensor[2] = new Sensor("", 2, SensorType.Debug, this, settings);
            ActivateSensor(debugSensor[2]);
            debugSensor[3] = new Sensor("", 3, SensorType.Debug, this, settings);
            ActivateSensor(debugSensor[3]);
            debugSensor[4] = new Sensor("", 4, SensorType.Debug, this, settings);
            ActivateSensor(debugSensor[4]);
#endif
            _temperatures[0] = new Sensor("External Fan VRM", 0, SensorType.Temperature, this, new ParameterDescription[0], settings);
            ActivateSensor(_temperatures[0]);
            _temperatures[1] = new Sensor("External", 1, SensorType.Temperature, this, new ParameterDescription[0], settings);
            ActivateSensor(_temperatures[1]);
            _temperatures[2] = new Sensor("Internal Water", 2, SensorType.Temperature, this, new ParameterDescription[0], settings);
            ActivateSensor(_temperatures[2]);

            _voltages[0] = new Sensor("External Fan", 1, SensorType.Voltage, this, new ParameterDescription[0], settings);
            ActivateSensor(_voltages[0]);
            _voltages[1] = new Sensor("Pump", 2, SensorType.Voltage, this, new ParameterDescription[0], settings);
            ActivateSensor(_voltages[1]);

            _pumpPower = new Sensor("Pump", 0, SensorType.Power, this, new ParameterDescription[0], settings);
            ActivateSensor(_pumpPower);

            _pumpFlow = new Sensor("Pump", 0, SensorType.Flow, this, new ParameterDescription[0], settings);
            ActivateSensor(_pumpFlow);

            _rpmSensors[0] = new Sensor("External Fan", 0, SensorType.Fan, this, new ParameterDescription[0], settings);
            ActivateSensor(_rpmSensors[0]);
            _rpmSensors[1] = new Sensor("Pump", 1, SensorType.Fan, this, new ParameterDescription[0], settings);
            ActivateSensor(_rpmSensors[1]);

            _fanControl = new Sensor("External Fan", 0, SensorType.Control, this, new ParameterDescription[0], settings);
            Control control = new Control(_fanControl, settings, 0, 100);
            control.ControlModeChanged += (cc) => {
                switch (cc.ControlMode)
                {
                    case ControlMode.Undefined:
                        return;
                    case ControlMode.Default:
                        SetControl(null);
                        break;
                    case ControlMode.Software:
                        SetControl((byte)(cc.SoftwareValue * 2.55));
                        break;
                    default:
                        return;
                }
            };
            control.SoftwareControlValueChanged += (cc) => {
                if (cc.ControlMode == ControlMode.Software)
                    SetControl((byte)(cc.SoftwareValue * 2.55));
            };

            switch (control.ControlMode)
            {
                case ControlMode.Undefined:
                    break;
                case ControlMode.Default:
                    SetControl(null);
                    break;
                case ControlMode.Software:
                    SetControl((byte)(control.SoftwareValue * 2.55));
                    break;
                default:
                    break;
            }
            _fanControl.Control = control;
            ActivateSensor(_fanControl);

            _frequencys[0] = new Sensor("Pump Frequency", 0, SensorType.Frequency, this, new ParameterDescription[0], settings);
            ActivateSensor(_frequencys[0]);
            _frequencys[1] = new Sensor("Pump MaxFrequency", 1, SensorType.Frequency, this, new ParameterDescription[0], settings);
            ActivateSensor(_frequencys[1]);            
        }

        //TODO: Implement Fan Control
        private void SetControl(byte? v)
        {
            throw new NotImplementedException();
        }

        //TODO: Check if valid
        public string Variant
        {
            get
            {
                PumpMode mode = (PumpMode)rawData[33];

                if (mode.HasFlag(PumpMode.MODE_PUMP_ADV))
                    return "Ultra + Internal Flow Sensor";
                else if (mode.HasFlag(PumpMode.MODE_FAN_CONTROLLER))
                    return "Ultra";
                else if (mode.HasFlag(PumpMode.MODE_FAN_AMP))
                    return "Advanced";
                else
                    return "Standard";
            }
        }

        public override HardwareType HardwareType
        {
            get
            {
                return HardwareType.Aquacomputer;
            }
        }

        public string Status {
            get {
                FirmwareVersion = BitConverter.ToUInt16(rawData, 50);

                if (FirmwareVersion < 1008)
                {
                    return $"Status: Untested Firmware Version {FirmwareVersion}! Please consider Updating to Version 1018";
                }
                return "Status: OK";
            }
        }

        public override void Close()
        {
            device.CloseDevice();
            base.Close();
        }

        //TODO: Check tested and fix unknown variables
        public override void Update()
        {
            device.ReadFeatureData(out rawData, 0x4);

            if (rawData[0] != 0x4)
                return;

            //var rawSensorsFan = BitConverter.ToUInt16(rawData, 1);                        //unknown - redundant?
            //var rawSensorsExt = BitConverter.ToUInt16(rawData, 3);                        //unknown - redundant?
            //var rawSensorsWater = BitConverter.ToUInt16(rawData, 5);                      //unknown - redundant?

            _voltages[0].Value = BitConverter.ToUInt16(rawData, 7) / 61f;                    //External Fan Voltage: tested - OK
            _voltages[1].Value = BitConverter.ToUInt16(rawData, 9) / 61f;                    //Pump Voltage: tested - OK
            _pumpPower.Value = _voltages[1].Value * BitConverter.ToInt16(rawData, 11) / 625f; //Pump Voltage * Pump Current: tested - OK

            _temperatures[0].Value = BitConverter.ToUInt16(rawData, 13) / 100f;              //External Fan VRM Temperature: untested
            _temperatures[1].Value = BitConverter.ToUInt16(rawData, 15) / 100f;              //External Temperature Sensor: untested
            _temperatures[2].Value = BitConverter.ToUInt16(rawData, 17) / 100f;              //Internal Water Temperature Sensor: tested - OK

            _frequencys[0].Value = (1f / BitConverter.ToInt16(rawData, 19)) * 750000;        //Pump Frequency: tested - OK
            _rpmSensors[1].Value = _frequencys[0].Value * 60f;                                      //Pump RPM: tested - OK
            _frequencys[1].Value = (1f / BitConverter.ToUInt16(rawData, 21)) * 750000;    //Pump Max Frequency: tested - OK

            _pumpFlow.Value = BitConverter.ToUInt32(rawData, 23);                            //Internal Pump Flow Sensor: unknown

            _rpmSensors[0].Value = BitConverter.ToUInt32(rawData, 27);                              //External Fan RPM: untested

            _fanControl.Value = 100f / byte.MaxValue * rawData[31];                          //External Fan Control: tested, External Fan Voltage scales by this value - OK

#if DEBUG
            debugSensor[0].Name = "Alarms: " + ((PumpAlarms)rawData[32]).ToString();        //Show Alarms (only if interpretation is activated in Aquasuit!): tested - OK
            debugSensor[0].Value = rawData[32];
            debugSensor[1].Name = "Modes: " + ((PumpMode)rawData[33]).ToString();           //Shows unlocked Pump Modes: tested - OK
            debugSensor[1].Value = rawData[33];

            var controllerOut = BitConverter.ToUInt32(rawData, 34);                         //unknown
            var controllerI = BitConverter.ToInt32(rawData, 38);                            //unknown
            var controllerP = BitConverter.ToInt32(rawData, 42);                            //unknown
            var controllerD = BitConverter.ToInt32(rawData, 46);                            //unknown
            debugSensor[2].Name = $"Controller - Out: {controllerOut} I: {controllerI} P: {controllerP} D: {controllerD}";

            var FirmwareVersion = BitConverter.ToUInt16(rawData, 50);                       //tested - OK
            var BootloaderVersion = BitConverter.ToUInt16(rawData, 52);                     //unknown
            var HardwareVersion = BitConverter.ToUInt16(rawData, 54);                       //unknown
            debugSensor[3].Name = $"Version - Firmware: {FirmwareVersion} Bootloader: {BootloaderVersion} Hardware: {HardwareVersion}";

            var unk1 = rawData[56];                                                         //unknown
            var unk2 = rawData[57];                                                         //unknown
            var SerialNumber = BitConverter.ToUInt16(rawData, 58);                          //tested - OK
            var PublicKey = BitConverter.ToString(rawData, 60, 6);                          //tested - OK
            debugSensor[4].Name = $"Unk1: {unk1} Unk2: {unk2} SerialNumber: {SerialNumber} PublicKey: {PublicKey}";
#endif
        }
    }
}
