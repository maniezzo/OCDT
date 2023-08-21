namespace ODTMIPmodel
{
   internal class Program
   {
      static void Main(string[] args)
      {
         Console.WriteLine("Starting");
         MIPmodel MIP = new MIPmodel();
         MIP.run_MIP();
      }
   }
}