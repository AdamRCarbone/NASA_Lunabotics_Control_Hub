using System;
using System.Text;

namespace NASA_Lunabotics_Control_Hub.Components
{
    /// <summary>
    /// Lean binary protocol for OCTANE rover-ground communication.
    /// Matches protocol.py and messages.md in workspace/src/octane_network/resource/
    ///
    /// Frame format: [MAGIC:1B][TYPE:1B][LEN:1B][PAYLOAD:N][CRC:1B]
    /// Total overhead: 4 bytes per message
    /// </summary>
    public static class NetworkProtocol
    {
        // Constants
        public const byte MAGIC = 0x4F; // 'O' for OCTANE
        public const byte TYPE_TELEMETRY = 0x54;    // 'T'
        public const byte TYPE_HEARTBEAT = 0x48;    // 'H'
        public const byte TYPE_COMMAND = 0x43;      // 'C'
        public const byte TYPE_ACK = 0x41;          // 'A'
        public const byte TYPE_FAULT = 0x46;        // 'F'
        public const byte TYPE_MANIPULATOR = 0x4D;  // 'M'
        public const byte TYPE_VIDEO = 0x56;        // 'V'
        public const byte VARIANT_RGB = 0x52;       // 'R'
        public const byte VARIANT_DEPTH = 0x44;     // 'D'
        public const byte VIDEO_SOURCE_STOP    = 0xFF;
        public const byte SOURCE_TERRAIN       = 0x07; // source_id for terrain UDP stream
        public const byte VARIANT_TERRAIN      = 0x54; // 'T' — terrain grid variant

        // Far ESP32 camera source IDs (RGB only, JPEG reassembly identical to IDs 0–5)
        public const byte SOURCE_FAR_FRONT     = 0x08;
        public const byte SOURCE_FAR_RIGHT     = 0x09;
        public const byte SOURCE_FAR_BACK      = 0x0A;
        public const byte SOURCE_FAR_LEFT      = 0x0B;

        // State/Mode codes
        public const byte STATE_STANDBY = 0x30; // '0'
        public const byte STATE_MANUAL = 0x31; // '1'
        public const byte STATE_AUTONOMOUS = 0x32; // '2'
        public const byte STATE_FAULT = 0x33; // '3'

        // Severity codes
        public const byte SEV_INFO = 0x30; // '0'
        public const byte SEV_WARNING = 0x31; // '1'
        public const byte SEV_CRITICAL = 0x32; // '2'

        /// <summary>
        /// Calculate CRC-8 using polynomial 0x07
        /// </summary>
        public static byte CalcCrc8(byte[] data)
        {
            byte crc = 0;
            foreach (byte b in data)
            {
                crc ^= b;
                for (int i = 0; i < 8; i++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ 0x07);
                    else
                        crc = (byte)(crc << 1);
                }
            }
            return crc;
        }

        /// <summary>
        /// Encode a Command frame (Ground → Rover)
        /// Format: [O][C][2][mode_byte][estop_byte][crc]
        /// Always 6 bytes on wire
        /// </summary>
        public static byte[] EncodeCommand(char mode, bool estop = false)
        {
            byte modeByte = (byte)mode;
            byte estopByte = estop ? (byte)'1' : (byte)'0';

            byte[] payload = { modeByte, estopByte };
            byte[] frame = new byte[6];

            frame[0] = MAGIC;
            frame[1] = TYPE_COMMAND;
            frame[2] = (byte)payload.Length;
            frame[3] = payload[0];
            frame[4] = payload[1];
            frame[5] = CalcCrc8(frame, 5);

            return frame;
        }

        /// <summary>
        /// Encode a Manipulator frame (Ground → Rover)
        /// Format: [O][M][3][bitfield][speed_hi][speed_lo][crc] — 7 bytes
        /// Bitfield: bit0=W, bit1=A, bit2=S, bit3=D, bit4=↑, bit5=↓, bit6=←, bit7=→
        /// speedModifier: 0–100 (percentage; 100 = 20% motor cap, the normal operating speed)
        /// </summary>
        public static byte[] EncodeManipulator(byte keyBitfield, ushort speedModifier = 100)
        {
            ushort speed = Math.Clamp(speedModifier, (ushort)0, (ushort)500);
            var frame = new byte[7];
            frame[0] = MAGIC;
            frame[1] = TYPE_MANIPULATOR;
            frame[2] = 3;
            frame[3] = keyBitfield;
            frame[4] = (byte)(speed >> 8);
            frame[5] = (byte)(speed & 0xFF);
            frame[6] = CalcCrc8(frame, 6);
            return frame;
        }

        /// <summary>
        /// Encode a video stream request (Ground → Rover)
        /// Format: [O][V][4][source_id][variant][quality][fps][CRC8] — 8 bytes
        /// </summary>
        public static byte[] EncodeVideoRequest(byte sourceId, byte variant, byte quality, byte fps)
        {
            var frame = new byte[8];
            frame[0] = MAGIC;
            frame[1] = TYPE_VIDEO;
            frame[2] = 0x04;
            frame[3] = sourceId;
            frame[4] = variant;
            frame[5] = quality;
            frame[6] = fps;
            frame[7] = CalcCrc8(frame, 7);
            return frame;
        }

        /// <summary>
        /// Encode a Telemetry frame (Rover → Ground)
        /// Format: [O][T][len][state_byte][optional_fields][crc]
        /// Minimal: 4 bytes (state only)
        /// </summary>
        public static byte[] EncodeTelemetry(byte state, byte? fault = null)
        {
            int payloadLen = fault.HasValue ? 2 : 1;
            byte[] payload = fault.HasValue
                ? new byte[] { state, fault.Value }
                : new byte[] { state };

            byte[] frame = new byte[4 + payloadLen];

            frame[0] = MAGIC;
            frame[1] = TYPE_TELEMETRY;
            frame[2] = (byte)payload.Length;
            frame[3] = payload[0];

            if (fault.HasValue)
                frame[4] = payload[1];

            frame[4 + payloadLen - 1] = CalcCrc8(frame, 3 + payloadLen);

            return frame;
        }

        /// <summary>
        /// Encode an ACK frame (Rover → Ground)
        /// Format: [O][A][1][success_byte][crc]
        /// Always 5 bytes on wire
        /// </summary>
        public static byte[] EncodeAck(bool success)
        {
            byte successByte = success ? (byte)'1' : (byte)'0';
            byte[] frame = new byte[5];

            frame[0] = MAGIC;
            frame[1] = TYPE_ACK;
            frame[2] = 1;
            frame[3] = successByte;
            frame[4] = CalcCrc8(frame, 4);

            return frame;
        }

        /// <summary>
        /// Encode a Fault Alert frame (Rover → Ground)
        /// Format: [O][F][2][severity_byte][fault_char][crc]
        /// Always 6 bytes on wire
        /// </summary>
        public static byte[] EncodeFault(byte severity, char faultChar)
        {
            byte[] frame = new byte[6];

            frame[0] = MAGIC;
            frame[1] = TYPE_FAULT;
            frame[2] = 2;
            frame[3] = severity;
            frame[4] = (byte)faultChar;
            frame[5] = CalcCrc8(frame, 5);

            return frame;
        }

        /// <summary>
        /// Decode a received frame
        /// Returns null if frame is incomplete or invalid
        /// </summary>
        public static DecodedMessage? DecodeFrame(byte[] data)
        {
            if (data.Length < 4)
                return null;

            // Parse header
            byte magic = data[0];
            byte msgType = data[1];
            byte payloadLen = data[2];

            // Verify magic
            if (magic != MAGIC)
                return null;

            // Check if we have complete frame
            int expectedLen = 3 + payloadLen + 1; // header + payload + crc
            if (data.Length < expectedLen)
                return null;

            // Verify CRC
            byte receivedCrc = data[expectedLen - 1];
            byte calcCrc = CalcCrc8(data, expectedLen - 1);
            if (receivedCrc != calcCrc)
                return null;

            // Extract payload
            byte[] payload = new byte[payloadLen];
            Array.Copy(data, 3, payload, 0, payloadLen);

            // Decode based on type
            switch (msgType)
            {
                case TYPE_HEARTBEAT:
                    if (payloadLen >= 3)
                    {
                        return new DecodedMessage
                        {
                            Type = "heartbeat",
                            State = payload[0],
                            SeqNum = (ushort)((payload[1] << 8) | payload[2])
                        };
                    }
                    break;

                case TYPE_TELEMETRY:
                    if (payloadLen >= 1)
                    {
                        var tmsg = new DecodedMessage { Type = "telemetry", State = payload[0] };
                        int idx = 1;
                        while (idx < payloadLen)
                        {
                            byte marker = payload[idx];
                            if (marker == 0x42 && idx + 5 <= payloadLen)        // 'B' battery float32
                                idx += 5;
                            else if (marker == 0x46 && idx + 2 <= payloadLen)   // 'F' fault char
                                idx += 2;
                            else if (marker == 0x49 && idx + 13 <= payloadLen)  // 'I' accel 3×float32 LE
                            {
                                tmsg.AccelX   = BitConverter.ToSingle(payload, idx + 1);
                                tmsg.AccelY   = BitConverter.ToSingle(payload, idx + 5);
                                tmsg.AccelZ   = BitConverter.ToSingle(payload, idx + 9);
                                tmsg.HasAccel = true;
                                idx += 13;
                            }
                            else if (marker == 0x4C && idx + 13 <= payloadLen)  // 'L' localization pose
                            {
                                tmsg.PoseX     = BitConverter.ToSingle(payload, idx + 1);
                                tmsg.PoseY     = BitConverter.ToSingle(payload, idx + 5);
                                tmsg.PoseTheta = BitConverter.ToSingle(payload, idx + 9);
                                tmsg.HasPose   = true;
                                idx += 13;
                            }
                            else if (marker == 0x47 && idx + 2 <= payloadLen)   // 'G' AprilTag observations
                            {
                                byte count  = Math.Min(payload[idx + 1], (byte)3);
                                int  needed = 2 + count * 9;
                                if (idx + needed <= payloadLen)
                                {
                                    tmsg.TagCount  = count;
                                    tmsg.TagIds    = new byte[count];
                                    tmsg.TagDists  = new float[count];
                                    tmsg.TagAngles = new float[count];
                                    for (int t = 0; t < count; t++)
                                    {
                                        int o = idx + 2 + t * 9;
                                        tmsg.TagIds[t]    = payload[o];
                                        tmsg.TagDists[t]  = BitConverter.ToSingle(payload, o + 1);
                                        tmsg.TagAngles[t] = BitConverter.ToSingle(payload, o + 5);
                                    }
                                    idx += needed;
                                }
                                else break;
                            }
                            else break;
                        }
                        return tmsg;
                    }
                    break;

                case TYPE_COMMAND:
                    if (payloadLen >= 2)
                    {
                        return new DecodedMessage
                        {
                            Type = "command",
                            Mode = payload[0],
                            EStop = payload[1] == '1'
                        };
                    }
                    break;

                case TYPE_ACK:
                    if (payloadLen >= 1)
                    {
                        return new DecodedMessage
                        {
                            Type = "ack",
                            Success = payload[0] == '1'
                        };
                    }
                    break;

                case TYPE_FAULT:
                    if (payloadLen >= 2)
                    {
                        return new DecodedMessage
                        {
                            Type = "fault",
                            Severity = payload[0],
                            FaultChar = (char)payload[1]
                        };
                    }
                    break;
            }

            return null;
        }

        /// <summary>
        /// Calculate CRC-8 over a portion of the array
        /// </summary>
        private static byte CalcCrc8(byte[] data, int length)
        {
            byte crc = 0;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x80) != 0)
                        crc = (byte)((crc << 1) ^ 0x07);
                    else
                        crc = (byte)(crc << 1);
                }
            }
            return crc;
        }
    }

    /// <summary>
    /// Represents a decoded protocol message
    /// </summary>
    public class DecodedMessage
    {
        public string Type { get; set; } = "";
        public byte State { get; set; }
        public ushort SeqNum { get; set; }
        public byte Mode { get; set; }
        public bool EStop { get; set; }
        public bool Success { get; set; }
        public byte Severity { get; set; }
        public char FaultChar { get; set; }
        public bool HasAccel { get; set; }
        public float AccelX { get; set; }
        public float AccelY { get; set; }
        public float AccelZ { get; set; }

        // Localization pose (marker 'L')
        public bool HasPose    { get; set; }
        public float PoseX     { get; set; }
        public float PoseY     { get; set; }
        public float PoseTheta { get; set; }

        // AprilTag observations (marker 'G')
        public byte    TagCount  { get; set; }
        public byte[]?  TagIds    { get; set; }
        public float[]? TagDists  { get; set; }
        public float[]? TagAngles { get; set; }
    }
}
