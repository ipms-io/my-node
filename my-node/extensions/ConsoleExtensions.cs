using System;
using System.Threading;
using System.Threading.Tasks;

namespace my_node.extensions
{
    public static class ConsoleExtensions
    {
        public static Task ConsoleWait(Task task)
        {
            var count = 0;
            while (!task.IsCompleted)
            {
                if (count++ % 4 == 1)
                    Console.Write("\rWait");
                else if (count++ % 4 == 2)
                    Console.Write("\rWait.");
                else if (count++ % 4 == 3)
                    Console.Write("\rWait..");
                else if (count++ % 4 == 4)
                    Console.Write("\rWait...");

                Thread.Sleep(100);
            }

            Console.Write("\r");

            return task;
        }
    }
}
