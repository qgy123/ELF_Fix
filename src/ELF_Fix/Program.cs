using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ELFSharp.ELF;
using Console = System.Console;

namespace ELF_Fix
{
    class Program
    {
        static void Main(string[] args)
        {
            PrintInfo();
            var path = Console.ReadLine();
            path = path?.Replace("\"", "");
            if (File.Exists(path))
            {
                var type = ELFReader.CheckELFType(path);

                switch (type)
                {
                    case Class.Bit32:
                        Console.WriteLine("32Bit Elf detected!");
                        var myElf = new MyELF<Int32>(path);
                        myElf.ReadAndFixHeader();
                        myElf.PrintHeaderInfo();
                        myElf.ReadSegmentHeaders();
                        myElf.PrintSegmentHeaderInfo();
                        myElf.RebuildSegmentByLoad();
                        break;
                    case Class.Bit64:
                        Console.WriteLine("64Bit Elf is not support currently!");
                        return;
                    case Class.NotELF:
                        Console.WriteLine("Given File is not a valid Elf! Make sure the magic is correct!");
                        return;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }

        static void PrintInfo()
        {
            Console.WriteLine("\'    _____  _      _____   _____  ___ __  __ _____  ____  \r\n\'   | ____|| |    |  ___| |  ___||_ _|\\ \\/ /| ____||  _ \\ \r\n\'   |  _|  | |    | |_    | |_    | |  \\  / |  _|  | |_) |\r\n\'   | |___ | |___ |  _|   |  _|   | |  /  \\ | |___ |  _ < \r\n\'   |_____||_____||_|     |_|    |___|/_/\\_\\|_____||_| \\_\\\r\n\'                                                         ");
            Console.WriteLine("Author: YueLuo");
            Console.WriteLine("QQ: 578903564");
            Console.WriteLine();
            Console.WriteLine("Please drag your Elf file here:");
        }
    }
}
