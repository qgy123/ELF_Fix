namespace ELFSharp.ELF.Segments
{
	public enum SegmentType : uint
	{
		Null = 0,
		Load,
		Dynamic,
		Interpreter,
		Note,
		SharedLibrary,
		ProgramHeader,
	    GNUStack = 0x6474E551,
	    GNURELRO,
        ARMEXIDX = 0x70000001,
    }
}

