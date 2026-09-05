namespace RailwaySimulator.Cli;

public static class Program
{
    public static Task<int> Main(string[] args)
    {
        var application = new CliApplication(Console.Out, Console.Error);
        return application.RunAsync(args, CancellationToken.None);
    }
}
