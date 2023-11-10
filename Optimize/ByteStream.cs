using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

public partial class ByteStream
{
    private int _position;
    private byte[] _buffer;

    public int position => _position;

    public int available => Math.Max(_buffer.Length - _position, 0);

    public bool readable => _position < _buffer.Length;

    public ByteStream(int capacity)
    {
        capacity = Math.Max(1, capacity);
        _buffer = new byte[capacity];
    }

    public ByteStream(byte[] bytes)
    {
        Debug.Assert(bytes != null);
        _buffer = bytes;
    }

    public void WriteByte(byte byt)
    {
        EnsureCapacity(_position + 1);
        _buffer[_position++] = byt;
    }

    public byte ReadByte() =>
        _buffer[_position++];

    public void WriteBoolean(bool value) =>
        WriteByte((byte) (value ? 1 : 0));

    public bool ReadBoolean() =>
        ReadByte() == 1;

    public void WriteInt16(short value) =>
        Write(zigzag_encode(value));

    public short ReadInt16() =>
        (short) zigzag_decode(Read());

    public void WriteInt32(int value) =>
        Write(zigzag_encode(value));

    public int ReadInt32() =>
        (int) zigzag_decode(Read());

    public void WriteInt64(long value) =>
        Write(zigzag_encode(value));

    public long ReadInt64() =>
        zigzag_decode(Read());

    public void WriteSingle(float single) =>
        WriteInt32(new SingleHelper {single = single}.int32);

    public void WriteDouble(double @double) =>
        WriteInt64(new DoubleHelper {@double = @double}.int64);

    public void WriteBytes(byte[] bytes)
    {
        var length = bytes?.Length ?? 0;
        WriteInt32(length);
        for (var i = 0; i < length; i++)
            WriteByte(bytes[i]);
    }

    public void WriteString(string str)
    {
        if (string.IsNullOrEmpty(str))
        {
            WriteByte(0);
            return;
        }

        WriteBytes(Encoding.UTF8.GetBytes(str));
    }

    public float ReadSingle() =>
        new SingleHelper {int32 = ReadInt32()}.single;

    public double ReadDouble() =>
        new DoubleHelper {int64 = ReadInt64()}.@double;

    public byte[] ReadBytes()
    {
        var length = (int) ReadInt32();
        if (length > 0)
        {
            var bytes = new byte[length];
            Buffer.BlockCopy(_buffer, _position, bytes, 0, length);
            _position += length;
            return bytes;
        }

        return null;
    }

    public string ReadString()
    {
        var str = string.Empty;
        var length = (int) ReadInt32();
        if (length > 0) str = Encoding.UTF8.GetString(_buffer, _position, length);

        return str;
    }

    public bool Readable() =>
        _position < _buffer.Length;

    private void Write(ulong uint64)
    {
        do
        {
            var value = (byte) ((uint64 & 0x7f) << 1);
            if ((uint64 >>= 7) == 0)
            {
                WriteByte(value);
                break;
            }

            WriteByte((byte) (value | 0x01));
        } while (true);
    }

    private ulong Read()
    {
        ulong result = 0L;
        for (var i = 0;; i++)
        {
            var value = _buffer[_position++];
            result |= (ulong) (value >> 1) << (7 * i);
            if ((value & 0x01) == 0)
                break;
        }

        return result;
    }

    public byte[] ToBytes()
    {
        var length = _position;
        var bytes = new byte[length];
        Array.Copy(_buffer, bytes, length);
        return bytes;
    }

    private void EnsureCapacity(int capacity)
    {
        var length = _buffer.Length;
        if (length < capacity)
        {
            var newLength = length;
            while (newLength < capacity)
                newLength = (newLength << 1) + 1;
            var newBuffer = new byte[newLength];
            Array.Copy(_buffer, newBuffer, length);
            _buffer = newBuffer;
        }
    }

    #region Zigzag

    /*
     * 算数左移低位补0,算数右移若符号位位0高位补0,若符号位位1,高位补1
     */

    private static ushort zigzag_encode(short int16) =>
        (ushort) ((int16 << 1) ^ (int16 >> 15));

    private static uint zigzag_encode(int int32) =>
        (uint) ((int32 << 1) ^ (int32 >> 31));

    private static ulong zigzag_encode(long int64) =>
        (ulong) ((int64 << 1) ^ (int64 >> 63));

    private static long zigzag_decode(ulong uint64) =>
        (long) (uint64 >> 1) ^ (-(long) (uint64 & 1));

    #endregion

    #region Float Helper

    [StructLayout(LayoutKind.Explicit)]
    private struct SingleHelper
    {
        [FieldOffset(0)] public int int32;
        [FieldOffset(0)] public float single;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DoubleHelper
    {
        [FieldOffset(0)] public long int64;
        [FieldOffset(0)] public double @double;
    }

    #endregion
}