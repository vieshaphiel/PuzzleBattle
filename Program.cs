using System;

namespace PuzzleBattle
{
    public static class Program
    {
        [STAThread]
        static void Main()
        {
            using (var game = new GameBoard())
                game.Run();
        }
    }
}
