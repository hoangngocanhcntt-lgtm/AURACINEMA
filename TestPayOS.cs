using System;
using PayOS;
using PayOS.Models;
class Program { 
    static void Main() { 
        var data = new PaymentData(123, 1000, "desc", new System.Collections.Generic.List<ItemData>(), "cancel", "return");
        Console.WriteLine(data);
    } 
}
