using System;
using System.Reflection;
class Program { 
    static void Main() { 
        var dllPath = @"D:\NuGetCache\payos\2.1.0\lib\net7.0\PayOS.dll";
        var bytes = System.IO.File.ReadAllBytes(dllPath);
        var asm = Assembly.Load(bytes);
        foreach(var t in asm.GetExportedTypes()) {
            Console.WriteLine(t.Namespace + " - " + t.Name);
        }
    } 
}
