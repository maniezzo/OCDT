namespace Checkfeas
{
   internal class Program
   {
      static void Main(string[] args)
      {  Checker C = new Checker();
         C.readCuts();
         C.checkBoundaries();
      }
   }
}
