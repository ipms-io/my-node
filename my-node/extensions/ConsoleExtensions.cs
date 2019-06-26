using System;
using System.Threading;
using System.Threading.Tasks;

namespace my_node.extensions
{
    public static class ConsoleExtensions
    {
        public static Task ConsoleWait(Task task)
        {
            int count = 0;
            while (!task.IsCompleted)
            {
                switch (count++ % 4)
                {
                    case 1:
                        Console.Write("\rWait");
                        break;
                    case 2:
                        Console.Write("\rWait.");
                        break;
                    case 3:
                        Console.Write("\rWait..");
                        break;
                    case 4:
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
