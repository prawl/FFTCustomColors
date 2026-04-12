using System.Collections.Generic;

namespace FFTColorCustomizer.GameBridge
{
    /// <summary>
    /// Decodes FFT status effect bitfields (5 bytes) into human-readable status names.
    /// Byte layout matches PSX FFT Battle Stats 0x0058-0x005C (current status).
    /// Source: FFHacktics Wiki "Extra Battle Stats" page.
    /// </summary>
    public static class StatusDecoder
    {
        private static readonly (int byteIndex, byte mask, string name)[] StatusMap =
        {
            // Byte 1
            (0, 0x40, "Crystal"),
            (0, 0x20, "Dead"),
            (0, 0x10, "Undead"),
            (0, 0x08, "Charging"),
            (0, 0x04, "Jump"),
            (0, 0x02, "Defending"),
            (0, 0x01, "Performing"),
            // Byte 2
            (1, 0x80, "Petrify"),
            (1, 0x40, "Invite"),
            (1, 0x20, "Blind"),
            (1, 0x10, "Confuse"),
            (1, 0x08, "Silence"),
            (1, 0x04, "Vampire"),
            (1, 0x02, "Cursed"),
            (1, 0x01, "Treasure"),
            // Byte 3
            (2, 0x80, "Oil"),
            (2, 0x40, "Float"),
            (2, 0x20, "Reraise"),
            (2, 0x10, "Transparent"),
            (2, 0x08, "Berserk"),
            (2, 0x04, "Chicken"),
            (2, 0x02, "Frog"),
            (2, 0x01, "Critical"),
            // Byte 4
            (3, 0x80, "Poison"),
            (3, 0x40, "Regen"),
            (3, 0x20, "Protect"),
            (3, 0x10, "Shell"),
            (3, 0x08, "Haste"),
            (3, 0x04, "Slow"),
            (3, 0x02, "Stop"),
            (3, 0x01, "Wall"),
            // Byte 5
            (4, 0x80, "Faith"),
            (4, 0x40, "Innocent"),
            (4, 0x20, "Charm"),
            (4, 0x10, "Sleep"),
            (4, 0x08, "DontMove"),
            (4, 0x04, "DontAct"),
            (4, 0x02, "Reflect"),
            (4, 0x01, "DeathSentence"),
        };

        /// <summary>
        /// Returns the unit's life state: "alive", "dead" (can be raised), "crystal", or "treasure" (permanently gone).
        /// </summary>
        public static string GetLifeState(byte[] statusBytes)
        {
            if (statusBytes == null || statusBytes.Length < 2)
                return "alive";
            if ((statusBytes[0] & 0x40) != 0) return "crystal";
            if ((statusBytes[1] & 0x01) != 0) return "treasure";
            if ((statusBytes[0] & 0x20) != 0) return "dead";
            return "alive";
        }

        /// <summary>
        /// Decode 5 status bytes into a list of active status effect names.
        /// Returns empty list if no statuses are active.
        /// </summary>
        public static List<string> Decode(byte[] statusBytes)
        {
            var result = new List<string>();
            if (statusBytes == null || statusBytes.Length < 5)
                return result;

            foreach (var (byteIndex, mask, name) in StatusMap)
            {
                if ((statusBytes[byteIndex] & mask) != 0)
                    result.Add(name);
            }

            return result;
        }

        /// <summary>
        /// FFT element byte: each bit represents an element.
        /// Used for elemental absorb/cancel/half/weak/strengthen fields.
        /// </summary>
        private static readonly (byte mask, string name)[] ElementMap =
        {
            (0x01, "Fire"),
            (0x02, "Lightning"),
            (0x04, "Ice"),
            (0x08, "Wind"),
            (0x10, "Earth"),
            (0x20, "Water"),
            (0x40, "Holy"),
            (0x80, "Dark"),
        };

        /// <summary>
        /// Decode an element byte into a list of element names.
        /// Returns null if no bits are set (no elements).
        /// NOT YET CALLED — needs the elemental property addresses (PSX 0x6D-0x70)
        /// discovered in a live session.
        /// </summary>
        public static List<string>? DecodeElements(byte elementByte)
        {
            if (elementByte == 0) return null;
            var result = new List<string>();
            foreach (var (mask, name) in ElementMap)
            {
                if ((elementByte & mask) != 0)
                    result.Add(name);
            }
            return result;
        }
    }
}
