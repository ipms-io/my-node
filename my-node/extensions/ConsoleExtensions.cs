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
                switch (count++ % 4)
                {
                    case 0:
                        Console.Write("\rWait");
                        break;
                    case 1:
                        Console.Write("\rWait.");
                        break;
                    case 2:
                        Console.Write("\rWait..");
                        break;
                    default:
                        Console.Write("\rWait...");
                        break;
                }

                Thread.Sleep(100);
            }

            Console.Write("\r");

            return task;
        }
    }
}
