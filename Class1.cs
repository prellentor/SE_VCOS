using System;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using VRageMath;
using VRage.Game;
using Sandbox.ModAPI.Interfaces;
using Sandbox.ModAPI.Ingame;
using Sandbox.Game.EntityComponents;
using VRage.Game.Components;
using VRage.Collections;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.Game.ModAPI.Ingame;
using SpaceEngineers.Game.ModAPI.Ingame;

public sealed class Program : MyGridProgram
{

    enum State { Idle, Running, Stop };

    class Vehicle
    {
        private readonly bool stopIfUncontrolled = true;
        private bool handBrake;
        private bool pressedKeyC;

        private float _currentSpeed = 0.0f;
        private float acceleration = 0.0f;
        private float maxForwardSpeed = 0.0f;
        private float maxReverseSpeed = 0.0f;

        private Program program;

        private IMyShipController controller;
        private string controllerName = "";

        private State state;

        private List<IMyMotorSuspension> wheels;

        private List<IMyLightingBlock> reverseLights;
        private List<IMyLightingBlock> brakeLights;

        //░░Methods░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░

        private IMyTerminalBlock GetFirstWithName(string name)
        {
            List<IMyTerminalBlock> units = new List<IMyTerminalBlock>();
            program.GridTerminalSystem.SearchBlocksOfName(name, units);
            foreach (IMyTerminalBlock unit in units)
                if (unit.IsSameConstructAs(program.Me))
                    return unit;
            return null;
        }

        private void InitializeProperies()
        {
            handBrake = true;
            pressedKeyC = false;

            if (program.Me.CustomData == "")
                throw new Exception("\nWrite Vehicle controller name in this PB custom data:\n");

            string data = program.Me.CustomData;
            string[] words;
            words = data.Split('\n');
            if (words.Length < 1)
                throw new Exception("\nWrite Vehicle controller name in this PB custom data:\n");

            controllerName = words[0].Trim(' ');
            acceleration = float.Parse(words[1].Trim(' '));
            maxForwardSpeed = float.Parse(words[2].Trim(' '));
            maxReverseSpeed = float.Parse(words[3].Trim(' '));
        }

        private void InitializeSystems()
        {

            controller = GetFirstWithName(controllerName) as IMyShipController;

            List<IMyTerminalBlock> units = new List<IMyTerminalBlock>();

            units.Clear();

            wheels = new List<IMyMotorSuspension>();

            program.GridTerminalSystem.GetBlocksOfType<IMyMotorSuspension>(units);
            foreach (IMyTerminalBlock unit in units)
                if (unit.IsSameConstructAs(program.Me))
                    wheels.Add(unit as IMyMotorSuspension);

            units.Clear();

            reverseLights = new List<IMyLightingBlock>();

            program.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(units);
            foreach (IMyTerminalBlock unit in units)
                if (unit.IsSameConstructAs(program.Me) & unit.CustomName.ToUpper().Contains("LIGHT REVERSE"))
                    reverseLights.Add(unit as IMyLightingBlock);

            foreach (IMyLightingBlock light in reverseLights)
                light.Enabled = false;

            units.Clear();

            brakeLights = new List<IMyLightingBlock>();
            program.GridTerminalSystem.GetBlocksOfType<IMyLightingBlock>(units);
            foreach (IMyTerminalBlock unit in units)
                if (unit.IsSameConstructAs(program.Me) & unit.CustomName.ToUpper().Contains("LIGHT BRAKE"))
                    brakeLights.Add(unit as IMyLightingBlock);

            foreach (IMyLightingBlock light in brakeLights)
            {
                light.Color = new Color(235, 75, 25);
                light.Radius = 4.25f;
                light.Intensity = 10.0f;
            }
        }

        public Vehicle(Program newProgram)
        {
            program = newProgram;
            program.Me.CustomName = "PB VCOS";

            InitializeProperies();
            InitializeSystems();
        }

        private void ControlHandbrake()
        {
            if (controller.MoveIndicator.Y < 0.0f & !pressedKeyC)
            {
                handBrake = !handBrake;
                _currentSpeed = 0.0f;
                pressedKeyC = true;
            }
            else if (controller.MoveIndicator.Y == 0.0f)
                pressedKeyC = false;

        }

        private void LightController()
        {
            int inputDirection = Math.Sign(-controller.MoveIndicator.Z);
            int movementDirection = Math.Sign(_currentSpeed);
            bool brake = false;

            if (inputDirection == 0)
                brake = false;
            else if (inputDirection > movementDirection)
                brake = true;
            else if (inputDirection < movementDirection)
                brake = true;

            if (controller.HandBrake || controller.MoveIndicator.Y > 0.0f || brake)
                foreach (IMyLightingBlock light in brakeLights)
                    light.SetValueFloat("Intensity", 10.0f);
            else
                foreach (IMyLightingBlock light in brakeLights)
                    light.SetValueFloat("Intensity", 1.0f);

            bool reverse = _currentSpeed < -1.0f ? true : false;
            foreach (IMyLightingBlock light in reverseLights)
                light.Enabled = reverse;
        }

        private void SetSpeed(float newSpeed)
        {
            if (-maxReverseSpeed <= newSpeed && newSpeed <= maxForwardSpeed)
                _currentSpeed = newSpeed;
        }

        private void SpeedControl()
        {
            SetSpeed(_currentSpeed + acceleration * (-controller.MoveIndicator.Z));

            float currentSpeed = Math.Abs(_currentSpeed);
            float movementDirection = Math.Sign(_currentSpeed);
            float currentRightOverride = -movementDirection;
            float currentLeftOverride = movementDirection;

            if (controller.GetShipSpeed() * 3.7f > currentSpeed + 5.0f)
                controller.HandBrake = true;
            else
                controller.HandBrake = handBrake;


            foreach (IMyMotorSuspension wheel in wheels)
            {
                wheel.SetValueFloat("Speed Limit", currentSpeed);
                if (wheel.CustomName.Contains("Right"))
                    wheel.SetValueFloat("Propulsion override", currentRightOverride);
                else
                    wheel.SetValueFloat("Propulsion override", currentLeftOverride);
            }
        }

        public int Proceed()
        {
            if (controller == null)
                return 1;
            try
            {
                switch (state)
                {
                    case State.Idle:
                        if (controller.IsUnderControl)
                        {
                            state = State.Running;
                            program.Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        }
                        break; // End Idle

                    case State.Running:

                        if (!controller.IsUnderControl)
                        {
                            state = State.Stop;
                            break;
                        }

                        ControlHandbrake();
                        SpeedControl();
                        LightController();

                        break; // End Running

                    case State.Stop:

                        if (stopIfUncontrolled)
                            handBrake = true;

                        ControlHandbrake();
                        LightController();

                        state = State.Idle;
                        program.Runtime.UpdateFrequency = UpdateFrequency.Update100;

                        break; // End Stop

                    default:
                        state = State.Idle;

                        break;
                }
            }
            catch (Exception e) { return 1; }
            return 0;
        }
    }

    //░░End░of░class░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░░

    public Program() { Runtime.UpdateFrequency = UpdateFrequency.Update100; }

    Vehicle unit;
    bool initialize = false;
    void Main(string argument)
    {
        // initialize
        if (!initialize)
        {
            unit = new Vehicle(this);
            initialize = true;
        }
        else if (1 == unit.Proceed())
            initialize = false;
        //Echo("PB Instruction Count : " + Runtime.CurrentInstructionCount);
    }

    public void Save()
    { }

}