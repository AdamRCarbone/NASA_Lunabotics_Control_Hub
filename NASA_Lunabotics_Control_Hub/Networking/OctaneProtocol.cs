using System;
using System.IO;
using System.Text;

namespace NASA_Lunabotics_Control_Hub.Networking
{
    /// <summary>
    /// Lean TCP protocol encoder/decoder for OCTANE rover communication.
    /// Frame format: [Magic:1B][Type:1B][Length:1B][Payload:N][CRC8:1B]
    /// Total overhead: 4 bytes
    /// </summary>
    public static class OctaneProtocol
    {
        // Magic byte
        public const byte MAGIC = 0x4F; // 'O' for OCTANE

        // Message types
        public const byte TYPE_TELEMETRY = 0x54; // 'T'
        public const byte TYPE_COMMAND = 0x43;   // 'C'
        public const byte TYPE_ACK = 0x41;       // 'A'
        public const byte TYPE_FAULT = 0x46;     // 'F'

        // Mode/state codes
        public const byte MODE_STANDBY = 0x30;   // '0'
        public const byte MODE_MANUAL = 0x31;    // '1'
        public const byte MODE_AUTONOMOUS = 0x32; // '2'
        public const byte MODE_FAULT_RESET = 0x33; // '3'

        // Severity codes
        public const byte SEV_INFO = 0x30;       // '0'
        public const byte SEV_WARNING = 0x31;    // '1'
        public const byte SEV_CRITICAL = 0x32;   // '2'

        /// <summary>
        /// Encode telemetry message.
        /// Payload: state_byte + [optional B + float] + [optional F + fault_char]
        /// </summary>
        public static byte[] EncodeTelemetry(byte state, float? battery = null, byte? fault = null)
        {
            using var stream = new MemoryStream();

            // State byte
            stream.WriteByte(state);

            // Optional battery
            if (battery.HasValue)
            {
                stream.WriteByte(0x42); // 'B' marker
                var bytes = BitConverter.GetBytes(battery.Value);
                if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                stream.Write(bytes, 0, 4);
            }

            // Optional fault
            if (fault.HasValue)
            {
                stream.WriteByte(0x46); // 'F' marker
                stream.WriteByte(fault.Value);
            }

            var payload = stream.ToArray();
            return BuildFrame(TYPE_TELEMETRY, payload);
        }

        /// <summary>
        /// Encode command message.
        /// Payload: mode_byte + estop_byte
        /// </summary>
        public static byte[] EncodeCommand(byte mode, bool estop = false)
        {
            var payload = new byte[] { mode, (byte)(estop ? 0x31 : 0x30) };
            return BuildFrame(TYPE_COMMAND, payload);
        }

        /// <summary>
        /// Encode ACK message.
        /// Payload: success_byte
        /// </summary>
        public static byte[] EncodeAck(bool success)
        {
            var payload = new byte[] { (byte)(success ? 0x31 : 0x30) };
            return BuildFrame(TYPE_ACK, payload);
        }

        /// <summary>
        /// Encode fault alert message.
        /// Payload: severity_byte + fault_char
        /// </summary>
        public static byte[] EncodeFault(byte severity, byte faultChar)
        {
            var payload = new byte[] { severity, faultChar };
            return BuildFrame(TYPE_FAULT, payload);
        }

        /// <summary>
        /// Decode incoming message. Returns null if incomplete or invalid.
        /// </summary>
        public static OctaneMessage? DecodeMessage(byte[] data, int offset, int length)
        {
            if (length < 4) return null; // Minimum frame size

            var magic = data[offset];
            if (magic != MAGIC) return null;

            var type = data[offset + 1];
            var payloadLen = data[offset + 2];
            var expectedTotal = 3 + payloadLen + 1; // header + payload + CRC

            if (length < expectedTotal) return null; // Incomplete

            // Verify CRC
            var crc = data[offset + expectedTotal - 1];
            var computedCrc = ComputeCrc8(data, offset, expectedTotal - 1);
            if (crc != computedCrc) return null;

            // Extract payload
            var payloadStart = offset + 3;
            var payload = new byte[payloadLen];
            Array.Copy(data, payloadStart, payload, 0, payloadLen);

            return ParsePayload(type, payload);
        }

        private static byte[] BuildFrame(byte type, byte[] payload)
        {
            var frame = new byte[3 + payload.Length + 1];
            frame[0] = MAGIC;
            frame[1] = type;
            frame[2] = (byte)payload.Length;
            Array.Copy(payload, 0, frame, 3, payload.Length);
            frame[frame.Length - 1] = ComputeCrc8(frame, frame.Length - 1);
            return frame;
        }

        private static byte ComputeCrc8(byte[] data, int length)
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
                        crc <<= 1;
                }
            }
            return crc;
        }

        private static OctaneMessage? ParsePayload(byte type, byte[] payload)
        {
            switch (type)
            {
                case TYPE_TELEMETRY:
                    return ParseTelemetry(payload);
                case TYPE_COMMAND:
                    return ParseCommand(payload);
                case TYPE_ACK:
                    return ParseAck(payload);
                case TYPE_FAULT:
                    return ParseFault(payload);
                default:
                    return null;
            }
        }

        private static OctaneMessage ParseTelemetry(byte[] payload)
        {
            var msg = new OctaneMessage { Type = MessageType.Telemetry };
            var idx = 0;

            if (idx < payload.Length)
            {
                msg.State = payload[idx++];
            }

            while (idx < payload.Length - 1)
            {
                var marker = payload[idx];
                if (marker == 0x42 && idx + 5 <= payload.Length) // 'B' + float
                {
                    var bytes = new byte[4];
                    Array.Copy(payload, idx + 1, bytes, 0, 4);
                    if (!BitConverter.IsLittleEndian) Array.Reverse(bytes);
                    msg.Battery = BitConverter.ToSingle(bytes, 0);
                    idx += 5;
                }
                else if (marker == 0x46 && idx + 2 <= payload.Length) // 'F' + char
                {
                    msg.FaultChar = payload[idx + 1];
                    idx += 2;
                }
                else
                {
                    idx++;
                }
            }

            return msg;
        }

        private static OctaneMessage ParseCommand(byte[] payload)
        {
            return new OctaneMessage
            {
                Type = MessageType.Command,
                Mode = payload.Length > 0 ? payload[0] : MODE_STANDBY,
                EStop = payload.Length > 1 && payload[1] == 0x31
            };
        }

        private static OctaneMessage ParseAck(byte[] payload)
        {
            return new OctaneMessage
            {
                Type = MessageType.Ack,
                Success = payload.Length > 0 && payload[0] == 0x31
            };
        }

        private static OctaneMessage ParseFault(byte[] payload)
        {
            return new OctaneMessage
            {
                Type = MessageType.Fault,
                Severity = payload.Length > 0 ? payload[0] : SEV_INFO,
                FaultChar = payload.Length > 1 ? payload[1] : (byte)0
            };
        }
    }

    public enum MessageType
    {
        Unknown,
        Telemetry,
        Command,
        Ack,
        Fault
    }

    public class OctaneMessage
    {
        public MessageType Type { get; set; }

        // Telemetry fields
        public byte State { get; set; }
        public float? Battery { get; set; }
        public byte? FaultChar { get; set; }

        // Command fields
        public byte Mode { get; set; }
        public bool EStop { get; set; }

        // ACK fields
        public bool Success { get; set; }

        // Fault fields
        public byte Severity { get; set; }
    }
}
