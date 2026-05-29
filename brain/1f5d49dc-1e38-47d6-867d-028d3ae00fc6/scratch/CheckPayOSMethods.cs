using System;
using System.Reflection;
using Net.payOS;

namespace AuraCinema.Scratch
{
    public class Program
    {
        public static void Main()
        {
            var type = typeof(PayOS);
            Console.WriteLine($"Methods in {type.Name}:");
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Console.WriteLine($"- {method.Name}");
            }
        }
    }
}
