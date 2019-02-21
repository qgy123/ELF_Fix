using System;
using System.Collections.Generic;
using System.IO;
using ELFSharp.ELF;
using ELFSharp.ELF.Sections;
using ELFSharp.ELF.Segments;
using ELFSharp.Utilities;
using Syroot.BinaryData;

namespace ELF_Fix
{
    public class MyELF<T>/* : ELF<T>*/ where T : struct
    {
        public string Path { get; }
        public long FileSize { get; }
        public string FixPath { get; }

        public Class Class { get; private set; }
        public Endianess Endianess { get; private set; }
        public FileType Type { get; private set; }
        public Machine Machine { get; private set; }
        public T EntryPoint { get; private set; }
        public T MachineFlags { get; private set; }

        private readonly FileStream stream;
        private readonly FileStream streamNeedFix;
        private readonly BinaryStream binaryWriter;
        private readonly BinaryStream binaryReader;

        private Int64 segmentHeaderOffset;
        private Int64 sectionHeaderOffset;
        private UInt16 elfHeaderSize;
        private UInt16 segmentHeaderEntrySize;
        private UInt16 segmentHeaderEntryCount;
        private UInt16 sectionHeaderEntrySize;
        private UInt16 sectionHeaderEntryCount;
        private UInt16 stringTableIndex;
        private Func<SimpleEndianessAwareReader> readerSource;
        private Func<SimpleEndianessAwareReader> localReaderSource;
        private List<Segment<T>> segments;

        public MyELF(string path)
        {
            Path = path;
            FixPath = path + "_fixed";
            stream = GetNewStream();
            binaryReader = new BinaryStream(stream);
            FileSize = new FileInfo(path).Length;
            if (File.Exists(FixPath))
            {
                File.Delete(FixPath);
            }
            File.Copy(Path, FixPath);

            streamNeedFix = GetRwStream();
            binaryWriter = new BinaryStream(streamNeedFix);
            //ReadAndFixHeader();
            //ReadStringTable();
            //ReadSections();
            //ReadSegmentHeaders();
        }

        public void RebuildSegmentByLoad()
        {
            for (var index = 0; index < segments.Count; index++)
            {
                var segment = segments[index];
                if (segment.Type == SegmentType.Load) //.text
                {
                    var segOffset1 = segment.Offset;
                    var segFileLen1 = segment.FileSize;

                    var nextSegment = segments[index + 1];

                    if (nextSegment.Type == SegmentType.Load) //.data
                    {
                        var segOffset2 = nextSegment.Offset;
                        var segVaddr2 = long.Parse(nextSegment.Address.ToString());
                        var segPhddr2 = long.Parse(nextSegment.PhysicalAddress.ToString());
                        var segFileLen2 = nextSegment.FileSize;

                        Console.WriteLine("Begin to rebuild Segments...");
                        if (segFileLen2 + segVaddr2 <= FileSize)
                        {
                            binaryWriter.Position = segFileLen1;

                            for (int i = 0; i < segOffset2 - (segOffset1 + segFileLen1); i++)
                            {
                                binaryWriter.WriteByte(0);
                            }

                            binaryReader.Position = segPhddr2;

                            for (int i = 0; i < segFileLen2; i++)
                            {
                                var b = binaryReader.Read1Byte();
                                binaryWriter.WriteByte(b);
                            }
                            //Todo: Clean the EOF

                            Console.WriteLine("Segment Rebuild successed!");
                            return;
                        }
                    }

                    throw new Exception("Load Segment cannot be located, fail to fix! Please verify the program table is corrected!");

                }

            }
            throw new Exception("Fail to find Load Segments in Program table! Please verify the program table is corrected!");

        }

        public void PrintSegmentHeaderInfo()
        {
            Console.WriteLine("Elf Segments:");
            Console.WriteLine("-> Type \tOffset \tAddress \tPhysical Address \tFileSize \tFlags \tAlignment");
            foreach (var segment in segments)
            {
                Console.WriteLine($"-> {segment.Type} \t{segment.Offset} \t{segment.Address} \t{segment.PhysicalAddress} \t{segment.FileSize} \t{segment.Flags} \t{segment.Alignment}");
            }
        }

        public void ReadSegmentHeaders()
        {
            segments = new List<Segment<T>>(segmentHeaderEntryCount);
            for (var i = 0u; i < segmentHeaderEntryCount; i++)
            {
                var segment = new Segment<T>(
                    segmentHeaderOffset + i * segmentHeaderEntrySize,
                    Class,
                    readerSource
                );
                segments.Add(segment);
            }
        }

        public void PrintHeaderInfo()
        {
            Console.WriteLine("Elf Header:");
            Console.WriteLine($"->Class: {Class}");
            Console.WriteLine($"->Endianess: {Endianess}");
            Console.WriteLine($"->FileType: {Type}");
            Console.WriteLine($"->Machine: {Machine}");
            Console.WriteLine($"->EntryPoint: {EntryPoint}");
            Console.WriteLine($"->MachineFlags: {MachineFlags}");
            Console.WriteLine();
            Console.WriteLine($"->Program Header Offset(e_phoff): {segmentHeaderOffset}");
            Console.WriteLine($"->Section Header Offset(e_shoff): {sectionHeaderOffset}");
            Console.WriteLine($"->Elf Header Size(e_ehsize): {elfHeaderSize}");
            Console.WriteLine($"->Program Header Entry Size(e_phentsize): {segmentHeaderEntrySize}");
            Console.WriteLine($"->Program Header Entries(e_phnum): {segmentHeaderEntryCount}");
            Console.WriteLine($"->Section Header Entry Size(e_shentsize): {sectionHeaderEntrySize}");
            Console.WriteLine($"->Section Header Entries(e_shnum): {sectionHeaderEntryCount}");
            Console.WriteLine($"->String Table Index(e_shtrndx): {stringTableIndex}");
        }

        public void ReadAndFixHeader()
        {
            ReadAndFixIdentificator();
            readerSource = () => new SimpleEndianessAwareReader(GetNewStream(), Endianess);
            localReaderSource = () => new SimpleEndianessAwareReader(stream, Endianess, true);
            ReadFields();
        }

        private void ReadFields()
        {
            using (var reader = localReaderSource())
            {
                Type = (FileType)reader.ReadUInt16();
                Machine = (Machine)reader.ReadUInt16();
                var version = reader.ReadUInt32();
                if (version != 1)
                {
                    Console.WriteLine($"Given ELF file is of unknown version {version}.");
                    Console.WriteLine($"Fix to 1.");
                    binaryWriter.Position = reader.BaseStream.Position - 1;
                    binaryWriter.WriteByte(1);
                }
                EntryPoint = (Class == Class.Bit32 ? reader.ReadUInt32() : reader.ReadUInt64()).To<T>();
                // TODO: assertions for (u)longs
                segmentHeaderOffset = Class == Class.Bit32 ? reader.ReadUInt32() : reader.ReadInt64();
                sectionHeaderOffset = Class == Class.Bit32 ? reader.ReadUInt32() : reader.ReadInt64();
                MachineFlags = reader.ReadUInt32().To<T>(); // TODO: always 32bit?
                elfHeaderSize = reader.ReadUInt16(); // elf header size
                segmentHeaderEntrySize = reader.ReadUInt16();
                segmentHeaderEntryCount = reader.ReadUInt16();
                sectionHeaderEntrySize = reader.ReadUInt16();
                sectionHeaderEntryCount = reader.ReadUInt16();
                stringTableIndex = reader.ReadUInt16();
            }
        }


        private void ReadAndFixIdentificator()
        {
            var reader = new BinaryReader(stream);
            reader.ReadBytes(4); // ELF magic
            var classByte = reader.ReadByte();
            switch (classByte)
            {
                case 1:
                    Class = Class.Bit32;
                    break;
                case 2:
                    Class = Class.Bit64;
                    break;
                default:
                    throw new ArgumentException($"Given ELF file is of unknown class {classByte}.");
            }
            var endianessByte = reader.ReadByte();
            switch (endianessByte)
            {
                case 1:
                    Endianess = Endianess.LittleEndian;
                    binaryWriter.ByteConverter = new ByteConverterLittle();
                    break;
                case 2:
                    Endianess = Endianess.BigEndian;
                    binaryWriter.ByteConverter = new ByteConverterBig();
                    break;
                default:
                    Console.WriteLine($"Given ELF file uses unknown endianess {endianessByte}.");
                    Console.WriteLine($"Fix to LittleEndian.");
                    Endianess = Endianess.LittleEndian;
                    binaryWriter.ByteConverter = new ByteConverterLittle();
                    binaryWriter.Position = reader.BaseStream.Position - 1;
                    binaryWriter.WriteByte(1);
                    //streamNeedFix.Position = reader.BaseStream.Position - 1;
                    //streamNeedFix.WriteByte(1);
                    break;
            }
            reader.ReadBytes(10); // padding bytes of section e_ident
        }

        private FileStream GetNewStream()
        {
            return new FileStream(
                Path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read
            );
        }

        private FileStream GetRwStream()
        {
            return new FileStream(
                FixPath,
                FileMode.Open,
                FileAccess.ReadWrite,
                FileShare.None
            );
        }

        public void Dispose()
        {
            stream.Close();
            binaryReader.Close();
            binaryWriter.Close();
        }
    }
}

namespace ELF_Fix
{
    internal static class Utilities
    {
        internal static T To<T>(this object source)
        {
            return (T)Convert.ChangeType(source, typeof(T));
        }
    }
}