using System;
using System.Linq;

public class Crc16
{
    private const ushort polynomial = 0xa001;
    private ushort[] table = new ushort[0x100];

    public Crc16()
    {
        for (ushort i = 0; i < this.table.Length; i = (ushort) (i + 1))
        {
            ushort num = 0;
            ushort num2 = i;
            for (byte j = 0; j < 8; j = (byte) (j + 1))
            {
                if (((num2 ^ num) & 1) != 0)
                {
                    num = (ushort) ((num >> 1) ^ 0xa001);
                }
                else
                {
                    num = (ushort) (num >> 1);
                }
                num2 = (ushort) (num2 >> 1);
            }
            this.table[i] = num;
        }
    }

    public ushort ComputeChecksum(byte[] bytes, ushort num)
    {
        //ushort num = 0;
        for (int i = 0; i < bytes.Length; i++)
        {
            byte index = (byte) (num ^ bytes[i]);
            num = (ushort) ((num >> 8) ^ this.table[index]);
        }
        return num;
    }

    public byte[] ComputeChecksumBytes(byte[] bytes, ushort num)
    {
        byte[] first = BitConverter.GetBytes(this.ComputeChecksum(bytes,num)).Reverse<byte>().ToArray<byte>();
        byte[] second = new byte[] { 0xff, 0xff };
        return first.Concat<byte>(second).ToArray<byte>();
    }

    public ushort CCB(byte[] arr, int start, int stopp, ushort InitialValue)
    {
        int st = (stopp - start);

        byte[] block = new byte[st];
        Buffer.BlockCopy(arr, start, block, 0x00, st);

        ushort crc = this.ComputeChecksum(block, InitialValue);


        //return new byte[] { (byte)(crc >> 8), (byte)(crc & 0x00ff) };

        //return (UInt16)((crc & 0xFFU) << 8 | (crc & 0xFF00U) >> 8);

        return crc;
    }
}

