namespace Tenray.ZoneTree.Segments.Block;

public sealed class BlockPin
{
    public DecompressedBlock Device1;
    public DecompressedBlock Device2;
    public BlockPin(DecompressedBlock device1 = null, DecompressedBlock device2 = null)
    {
        Device1 = device1;
        Device2 = device2;
    }
    public SingleBlockPin ToSingleBlockPin(int num)
    {
        if (num == 1) return new SingleBlockPin(Device1);
        if (num == 2) return new SingleBlockPin(Device2);
        throw new ArgumentException("Supported device numbers are 1 and 2 but given " + num);
    }

    public void SetDevice1(DecompressedBlock device)
    {
        Device1 = device;
    }

    public void SetDevice2(DecompressedBlock device)
    {
        Device2 = device;
    }
}


