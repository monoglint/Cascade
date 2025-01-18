namespace Cascade
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Executor.Executor.ExecuteFile(args.Length == 0 ? "C:\\Users\\jghig\\source\\repos\\Cascade\\Cascade\\Test.cascade" : args[0]);

            Console.WriteLine("Press any key to exit...");
            Console.ReadLine();
        }
    }
}