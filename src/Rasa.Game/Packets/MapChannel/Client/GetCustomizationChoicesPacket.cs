﻿namespace Rasa.Packets.MapChannel.Client
{
    using Data;
    using Memory;

    public class GetCustomizationChoicesPacket : ClientPythonPacket
    {
        public override GameOpcode Opcode { get; } = GameOpcode.GetCustomizationChoices;

        public ulong EntityId { get; set; }

        public override void Read(PythonReader pr)
        {
            pr.ReadTuple();
            EntityId = pr.ReadULong();
        }
    }
}
