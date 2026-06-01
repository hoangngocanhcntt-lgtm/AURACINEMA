using System;
using System.Net.Http;
using System.Threading.Tasks;

class Program {
    static async Task Main() {
        var client = new HttpClient();
        var res = await client.GetAsync("https://generativelanguage.googleapis.com/v1beta/models?key=AIzaSyDwaQrylnqOhTojasCJdQDEJX-jocb9ziQ");
        Console.WriteLine(await res.Content.ReadAsStringAsync());
    }
}
