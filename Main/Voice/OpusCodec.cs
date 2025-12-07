using System;

namespace EscapeFromDuckovCoopMod.Main.Voice;

public class OpusCodec
{
    public byte[] Encode(float[] pcmSamples)
    {
        byte[] encoded = new byte[pcmSamples.Length * 2];
        Buffer.BlockCopy(pcmSamples, 0, encoded, 0, encoded.Length);
        return encoded;
    }

    public float[] Decode(byte[] encodedData)
    {
        float[] decoded = new float[encodedData.Length / 2];
        Buffer.BlockCopy(encodedData, 0, decoded, 0, encodedData.Length);
        return decoded;
    }
}
