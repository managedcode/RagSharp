using System;

namespace SampleApp;

public class Greeter
{
    public string Hello(string name) => $"Hello, {name}";
}

public static class Program
{
    public static void Main()
    {
        var greeter = new Greeter();
        Console.WriteLine(greeter.Hello("world"));
    }
}
